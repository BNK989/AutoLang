using System.Runtime.InteropServices;
using AutoLang.Engine;
using AutoLang.Session;
using static AutoLang.Mac.ObjC;

namespace AutoLang.Mac;

/// <summary>
/// The macOS menu bar app: an accessory NSApplication (no Dock icon) whose main run loop carries the event tap, the
/// menu and the notifications. Word lists load in the background; until then the controller runs with no languages
/// and never corrects.
/// </summary>
internal static class Program
{
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string AccessibilitySettings = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility";

    private static Settings _settings = null!;
    private static InputSources _sources = null!;
    private static SessionController _controller = null!;
    private static Corrector _corrector = null!;
    private static EventTap _tap = null!;
    private static MenuBar _menu = null!;
    private static Timer? _watchdog;

    private static string _appName = "?";
    private static string? _bundleId;
    private static volatile bool _loaded;
    private static volatile KeyboardLayoutTable[] _active = [];
    private static bool _trusted, _askedListen;

    private static readonly object ProfilesGate = new();
    /// <summary>Built profiles by word-list code: a re-probe after the user adds a layout reuses them.</summary>
    private static readonly Dictionary<string, LanguageProfile> Profiles = new(StringComparer.Ordinal);
    private static int _generation;

    private static int Main()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Console.Error.WriteLine("This is the macOS build of AutoLang.");
            return 1;
        }
        Log.Init();
        _settings = Settings.Load();

        NativeLibrary.Load(AppKit);
        var pool = Send(Send(Class("NSAutoreleasePool"), Sel("alloc")), Sel("init"));
        var app = Send(Class("NSApplication"), Sel("sharedApplication"));
        SendLong(app, Sel("setActivationPolicy:"), 1); // NSApplicationActivationPolicyAccessory: menu bar only
        RegisterBridge();

        _sources = new InputSources();
        Probe();
        _controller = new SessionController(new PhraseDecider([]), []);
        _controller.AddExceptions(_settings.ExceptionWords);
        _controller.ExceptionsAdded += SaveExceptions;

        _corrector = new Corrector(langId => _sources.Select(langId));
        _corrector.Applied += job => { if (job.Correction.Kind == CorrectionKind.Undo) SaveExceptions(); };
        _corrector.Start();

        _tap = new EventTap(_controller, _corrector, _sources, () => _appName);
        _menu = new MenuBar(MenuState, OnCommand);

        ObjC.On("notified:", OnNotification);
        var workspace = Send(Class("NSWorkspace"), Sel("sharedWorkspace"));
        Observe(Send(workspace, Sel("notificationCenter")), "NSWorkspaceDidActivateApplicationNotification");
        Observe(Send(Class("NSDistributedNotificationCenter"), Sel("defaultCenter")),
            "com.apple.Carbon.TISNotifyEnabledKeyboardInputSourcesChanged");
        FrontApp(Send(workspace, Sel("frontmostApplication")));

        Reconfigure();
        StartTap();
        _watchdog = new Timer(_ => OnMain(Watch), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

        Send(pool, Sel("drain"));
        Send(app, Sel("run"));
        return 0;
    }

    // ---- Languages ----

    private static void Probe()
    {
        _sources.Probe();
        Log.Info($"input sources: {string.Join(", ", _sources.All)}");
    }

    /// <summary>
    /// Builds the profiles of the enabled layouts' languages on the thread pool, then hands them to the controller. Only
    /// languages with a bundled word list take part. A newer call wins over an older one still running.
    /// </summary>
    private static void Reconfigure()
    {
        var tables = _sources.Tables; // TIS data: read on the main thread
        int generation = Interlocked.Increment(ref _generation);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var active = new List<KeyboardLayoutTable>();
                var profiles = new List<LanguageProfile>();
                foreach (var t in tables)
                {
                    if (WordLists.SourceCode(t.LangId) is not { } code || !WordLists.IsBundled(code))
                    {
                        Log.Info($"{t}: no bundled word list, not corrected");
                        continue;
                    }
                    var p = Profile(code, t);
                    active.Add(t);
                    profiles.Add(p.LangId == t.LangId ? p : new LanguageProfile(t.LangId, t.Name, p.Dictionary, p.Model,
                        LanguageProfile.DefaultRulesFor(t.LangId), p.Normalize));
                }
                lock (ProfilesGate)
                {
                    if (generation != _generation) return;
                    _controller.Configure(new PhraseDecider(profiles), active);
                    _active = active.ToArray();
                    _loaded = true;
                }
                Log.Info($"languages ready: {Summary()}");
            }
            catch (Exception ex) { Log.Warn($"loading languages failed: {ex.GetType().Name}: {ex.Message}"); }
            OnMain(_menu.Refresh);
        });
    }

    private static LanguageProfile Profile(string code, KeyboardLayoutTable table)
    {
        lock (ProfilesGate)
            if (Profiles.TryGetValue(code, out var cached)) return cached;
        var started = Environment.TickCount64;
        var p = LanguageProfile.FromFrequencyLines(table.LangId, table.Name, WordLists.ReadLines(code),
            LanguageProfile.DefaultRulesFor(table.LangId), LanguageProfile.DefaultNormalizeFor(table.LangId));
        Log.Info($"word list {code} loaded in {Environment.TickCount64 - started} ms");
        lock (ProfilesGate) return Profiles.TryAdd(code, p) ? p : Profiles[code];
    }

    /// <summary>"English ⇄ עברית": the corrected languages grouped by alphabet.</summary>
    private static string Summary() =>
        string.Join("  ⇄  ", _active.GroupBy(t => t.Script).Select(g => string.Join(", ", g.Select(t => t.Name))));

    private static void SaveExceptions()
    {
        var words = _controller.Exceptions.ToList();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            lock (_settings)
            {
                _settings.ExceptionWords = words;
                _settings.Save();
            }
        });
    }

    // ---- Apps and notifications ----

    private static void Observe(nint center, string name)
    {
        // The name string is kept: the center may hold on to it for as long as the observer is registered.
        Send(center, Sel("addObserver:selector:name:object:"), Bridge, Sel("notified:"), Native.CreateString(name), 0);
    }

    private static void OnNotification(nint notification)
    {
        var name = Native.ToManaged(Send(notification, Sel("name")));
        if (name == "NSWorkspaceDidActivateApplicationNotification")
        {
            var key = Native.CreateString("NSWorkspaceApplicationKey");
            try { FrontApp(Send(Send(notification, Sel("userInfo")), Sel("objectForKey:"), key)); }
            finally { Native.CFRelease(key); }
        }
        else if (name.EndsWith("TISNotifyEnabledKeyboardInputSourcesChanged", StringComparison.Ordinal))
        {
            Log.Info("enabled input sources changed");
            Probe();
            Reconfigure();
        }
    }

    /// <summary>A new app is in front: what was typed before belongs to another app, and the app may be excluded.</summary>
    private static void FrontApp(nint runningApp)
    {
        _controller.Reset(ResetScope.All);
        if (runningApp == 0) return;
        _bundleId = Native.ToManaged(Send(runningApp, Sel("bundleIdentifier")));
        _appName = Native.ToManaged(Send(runningApp, Sel("localizedName")));
        if (_appName.Length == 0) _appName = _bundleId.Length > 0 ? _bundleId : "?";
        UpdateBlocked();
    }

    private static void UpdateBlocked() => _controller.Blocked = _settings.Paused || _settings.IsExcluded(_bundleId);

    // ---- Permission and the tap ----

    private static void StartTap()
    {
        // Shows macOS's own "AutoLang would like to control this computer" prompt the first time.
        var options = Native.CreateDictionary((Native.kAXTrustedCheckOptionPrompt, Native.kCFBooleanTrue));
        try { _trusted = Native.AXIsProcessTrustedWithOptions(options) != 0; }
        finally { Native.CFRelease(options); }
        Log.Info(_trusted ? "accessibility: trusted" : "accessibility: not trusted yet");
        if (_trusted) TryInstall();
    }

    /// <summary>Every 2 s on the main thread: install the tap once permission arrives, re-enable it if macOS turned it off.</summary>
    private static void Watch()
    {
        bool trusted = Native.AXIsProcessTrusted() != 0;
        if (trusted != _trusted)
        {
            _trusted = trusted;
            Log.Info(trusted ? "accessibility: granted" : "accessibility: revoked");
            _menu.Refresh();
        }
        if (!_tap.Installed) { if (trusted) TryInstall(); }
        else _tap.EnsureEnabled();
    }

    private static void TryInstall()
    {
        if (_tap.Install()) { _menu.Refresh(); return; }
        // Accessibility is granted but the tap still fails: some macOS versions also want Input Monitoring. Ask once.
        if (!_askedListen && Native.CGPreflightListenEventAccess() == 0)
        {
            _askedListen = true;
            Log.Info("event tap refused; requesting Input Monitoring");
            Native.CGRequestListenEventAccess();
        }
    }

    // ---- Menu ----

    private static MenuBar.State MenuState()
    {
        bool needsPermission = !_trusted || !_tap.Installed;
        string status =
            !_loaded ? "Loading word lists…"
            : _settings.Paused ? "Paused"
            : needsPermission ? "Needs Accessibility permission"
            : _settings.IsExcluded(_bundleId) ? $"Off in {_appName}"
            : _active.Select(t => t.Script).Where(s => s != Scripts.Unknown).Distinct().Count() < 2
                ? "Add a Hebrew or English keyboard in System Settings → Keyboard → Input Sources"
            : Summary();
        return new MenuBar.State(status, _settings.Paused, needsPermission);
    }

    private static void OnCommand(MenuBar.Command command)
    {
        switch (command)
        {
            case MenuBar.Command.Pause:
                lock (_settings) { _settings.Paused = !_settings.Paused; _settings.Save(); }
                Log.Info(_settings.Paused ? "paused" : "resumed");
                UpdateBlocked();
                _controller.Reset(ResetScope.All);
                _menu.Refresh();
                break;
            case MenuBar.Command.Grant:
                MenuBar.Open(AccessibilitySettings);
                break;
            case MenuBar.Command.Login:
                _menu.ToggleLogin();
                break;
            case MenuBar.Command.Logs:
                Directory.CreateDirectory(Log.Dir);
                MenuBar.Open(Log.Dir);
                break;
            case MenuBar.Command.Quit:
                Log.Info("quit");
                _watchdog?.Dispose();
                _corrector.Dispose();
                Send(Send(Class("NSApplication"), Sel("sharedApplication")), Sel("terminate:"), 0);
                break;
        }
    }
}
