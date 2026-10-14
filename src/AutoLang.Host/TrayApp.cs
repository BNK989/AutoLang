using System.Drawing;
using System.Windows.Forms;
using AutoLang.Session;

namespace AutoLang.Host;

/// <summary>
/// The visible part of the host (PLAN.md §6: visible tray icon, one-click pause). Green = watching, grey = paused,
/// orange = blocked in the current window (excluded app, admin window, password field, fullscreen).
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly Settings _settings;
    private readonly SessionController _controller;
    private readonly FocusMonitor _focus;
    private readonly Corrector _corrector;
    private readonly LanguageManager _languages;
    private LanguagesForm? _languagesForm;
    private LogViewerForm? _logForm;
    private WelcomeCard? _welcome;
    private StatusFlyout? _status;
    private readonly TrayMenuHeader _header;
    private readonly TrayMenuItem _pause, _disableApp, _typing, _typingHint, _swap, _fullscreen, _autoStart;
    private readonly TrayMenuItem[] _typingItems;
    private bool _adminNoticeShown;
    private readonly Icon _green, _grey, _orange;
    private readonly SynchronizationContext _ui;
    private readonly Action _onExit;

    private static UiText T => UiText.Current;

    public TrayApp(Settings settings, SessionController controller, FocusMonitor focus, Corrector corrector, LanguageManager languages, Action onExit)
    {
        _settings = settings;
        _controller = controller;
        _focus = focus;
        _corrector = corrector;
        _languages = languages;
        _onExit = onExit;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _green = Draw(IconArt.Brand);
        _grey = Draw(IconArt.Paused);
        _orange = Draw(IconArt.Blocked);

        var menu = TrayMenu.Create();
        _header = new TrayMenuHeader { Title = T.Starting, Subtitle = LayoutSummary() };
        // ToolStrip draws a "\t" in the text as nothing; the shortcut column only shows ShortcutKeyDisplayString.
        _pause = new TrayMenuItem(PauseText(), PauseIcon(), TogglePause) { ShortcutKeyDisplayString = T.ClickTrayIcon };
        _disableApp = new TrayMenuItem(T.DisableForThisApp, "prohibited", ToggleDisableApp) { Toggle = true };
        _typingItems = new[]
        {
            TypingItem(T.TypingStandard, T.TypingStandardNote, TypingMethod.Unicode),
            TypingItem(T.TypingKeys, T.TypingKeysNote, TypingMethod.Keys),
            TypingItem(T.TypingPaste, T.TypingPasteNote, TypingMethod.Paste),
        };
        // Named for the problem it solves: the method only matters when an app mangles the default typing.
        _typing = new TrayMenuItem(T.CompatibilityForThisApp, "keyboard");
        TrayMenu.Style((ToolStripDropDownMenu)_typing.DropDown);
        _typing.DropDownItems.AddRange(_typingItems);
        _typing.DropDownItems.Add(new TrayMenuSeparator());
        _typingHint = new TrayMenuItem("") { Enabled = false };
        _typing.DropDownItems.Add(_typingHint);
        // One item for both hotkey jobs: double-tapping Shift undoes a fresh correction or else converts (KeyboardHook.ShiftKey).
        _swap = new TrayMenuItem(T.SwitchTyped, "arrow_swap", () => HotkeyFromMenu(T.SwitchTypedHow))
            { ShortcutKeyDisplayString = T.ShiftTwice };
        _fullscreen = new TrayMenuItem(T.SkipFullscreen, "full_screen_maximize", ToggleFullscreen) { Toggle = true, Checked = settings.SkipFullscreen };
        // Packaged, the start-at-login entry is the manifest's StartupTask, switched in Windows Settings (PLAN.md §5, M4).
        _autoStart = Native.IsPackaged
            ? new TrayMenuItem(T.StartWithWindowsSettings, "rocket", OpenStartupSettings)
            : new TrayMenuItem(T.StartWithWindows, "rocket", ToggleAutoStart) { Toggle = true, Checked = StartupRegistration.IsEnabled() };
        menu.Items.AddRange(new ToolStripItem[]
        {
            _header, new TrayMenuSeparator(),
            _pause, _disableApp, _typing, new TrayMenuSeparator(),
            _swap, new TrayMenuSeparator(),
            new TrayMenuItem(T.LanguagesMenu, "local_language", ShowLanguages),
            _fullscreen,
            _autoStart,
            new TrayMenuItem(Native.IsPackaged ? T.ShowLog : T.OpenLogFile, "document_text", OpenLog),
            new TrayMenuSeparator(),
            new TrayMenuItem(T.Exit, "dismiss", Exit),
        });
        menu.Opening += (_, _) => RefreshMenu();

        _icon = new NotifyIcon { Icon = _green, Visible = true, ContextMenuStrip = menu, Text = "AutoLang" };
        // One click, not a double-click: a double-click still raises one MouseClick, so it toggles once too.
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) TogglePause(); };

        _focus.StatusChanged += s => _ui.Post(_ => Refresh(s), null);
        _corrector.Applied += job => _ui.Post(_ => { Refresh(_focus.Status); if (job.Correction.Kind == CorrectionKind.Auto) FirstFixNotice(); }, null);
        _languages.Changed += () => _ui.Post(_ => { _header.Subtitle = LayoutSummary(); Refresh(_focus.Status); }, null);
        Refresh(_focus.Status);

        // First run: AutoLang has no window, so say that it started and where it lives. Posted, so it shows once the
        // message loop runs; not activated, so typing that is already under way keeps its focus.
        if (!_settings.WelcomeShown)
        {
            lock (_settings) { _settings.WelcomeShown = true; _settings.Save(); }
            _ui.Post(_ => ShowWelcome(activate: false), null);
        }
    }

    /// <summary>Shows the welcome card again, from any thread: a second launch of AutoLang asks for it (Program).</summary>
    public void RequestWelcome() => _ui.Post(_ => ShowWelcome(activate: true), null);

    private void ShowWelcome(bool activate)
    {
        _status?.Dismiss();
        if (_welcome is { IsDisposed: false, IsClosing: false })
        {
            _welcome.Reload();
            if (activate) _welcome.Activate();
            return;
        }
        var card = new WelcomeCard(WelcomeContent, WelcomeAction, activate);
        card.FormClosed += (_, _) => { card.Dispose(); if (_welcome == card) _welcome = null; };
        _welcome = card;
        card.Present();
    }

    private void WelcomeAction()
    {
        if (_settings.Paused) TogglePause(quiet: true); // the card itself turns into "AutoLang is on"
        else _welcome?.Dismiss();
    }

    private WelcomeModel WelcomeContent()
    {
        var active = _languages.Languages.Where(l => l.Active).Select(l => l.LangId).ToHashSet();
        var example = WelcomeCard.Example(_languages.Installed.Where(t => active.Contains(t.LangId)));
        string what = example is null ? T.WelcomeWhat : T.WelcomeWhatExample;
        if (_settings.Paused)
            return new(T.PausedTitle, T.WelcomePausedSubtitle, what, IconArt.Paused, T.Resume, example);
        if (!_controller.CanCorrect)
        {
            bool downloading = _languages.Languages.Any(l => l.Active && l.State == WordListState.Downloading);
            return downloading
                ? new(T.ReadyingTitle, T.DownloadingWordLists, what, IconArt.Paused, T.GotIt, example)
                : new(T.IdleTitle, T.NeedsTwoAlphabets, T.WelcomeIdleBody, IconArt.Paused, T.GotIt, null);
        }
        return new(T.OnTitle, T.WelcomeOnSubtitle, what, IconArt.Brand, T.GotIt, example);
    }

    /// <summary>
    /// Once ever, after the first automatic fix: a word changing by itself is startling the first time, so say who did
    /// it and how to take it back. Never names the word.
    /// </summary>
    private void FirstFixNotice()
    {
        if (_settings.FirstFixNoticeShown) return;
        lock (_settings) { _settings.FirstFixNoticeShown = true; _settings.Save(); }
        _icon.ShowBalloonTip(6000, T.FirstFixTitle, T.FirstFixBody, ToolTipIcon.None);
    }

    private void Refresh(FocusStatus s)
    {
        if (s.IsAdminWindow && !_settings.Paused && !_adminNoticeShown)
        {
            // Once per session: the orange icon alone does not say why nothing gets corrected here.
            _adminNoticeShown = true;
            _icon.ShowBalloonTip(6000, T.AdminTitle, UiText.F(T.AdminBody, AppName(s.Process)), ToolTipIcon.Info);
        }
        string state = _settings.Paused ? T.StatePaused
            : !_controller.CanCorrect ? T.StateIdle
            : s.Blocked ? UiText.F(T.StateBlocked, T.Reason(s.Reason))
            : T.StateWatching;
        string app = string.IsNullOrEmpty(s.Process) ? "" : $" — {AppName(s.Process)}";
        _header.Title = state + app;
        _header.Mark = _settings.Paused || !_controller.CanCorrect ? IconArt.Paused : s.Blocked ? IconArt.Blocked : IconArt.Brand;
        _icon.Icon = _settings.Paused || !_controller.CanCorrect ? _grey : s.Blocked ? _orange : _green;
        var tip = $"AutoLang: {state}{app}\n{UiText.F(T.CorrectionsThisSession, _corrector.AppliedCount)}\n" +
                  (_settings.Paused ? T.ClickToResume : T.ClickToPause);
        _icon.Text = tip.Length > 127 ? tip[..127] : tip;
        _welcome?.Reload();
        _status?.Reload();
    }

    private void RefreshMenu()
    {
        var p = _focus.Status.Process;
        bool hasApp = !string.IsNullOrEmpty(p) && !p.StartsWith("pid ") && !_settings.IsExcluded(p);
        _disableApp.Enabled = hasApp;
        _disableApp.Text = hasApp ? UiText.F(T.DisableFor, AppName(p)) : T.DisableForThisApp;
        _disableApp.Checked = hasApp && _settings.IsDisabled(p);
        _typing.Enabled = hasApp;
        _typing.Text = hasApp ? UiText.F(T.CompatibilityFor, AppName(p)) : T.CompatibilityForThisApp;
        _typingHint.Text = hasApp ? UiText.F(T.TypingHintFor, AppName(p)) : T.TypingHintThisApp;
        TypingMethod current;
        lock (_settings) current = hasApp ? _settings.TypingMethodFor(p) : TypingMethod.Unicode;
        foreach (var item in _typingItems) item.Checked = (TypingMethod)item.Tag! == current;
        _swap.Enabled = !_settings.Paused;
        _pause.Text = PauseText();
        _pause.Icon = PauseIcon();
        if (!Native.IsPackaged) _autoStart.Checked = StartupRegistration.IsEnabled();
    }

    private TrayMenuItem TypingItem(string text, string note, TypingMethod method)
        => new(text, null, () => SetTypingMethod(method)) { Tag = method, ShortcutKeyDisplayString = note };

    /// <summary>Per-app typing method (PLAN.md §3.3), for the target the menu was opened over.</summary>
    private void SetTypingMethod(TypingMethod method)
    {
        var p = _focus.Status.Process;
        if (string.IsNullOrEmpty(p) || p.StartsWith("pid ")) return;
        lock (_settings)
        {
            _settings.SetTypingMethod(p, method);
            _settings.Save();
        }
        Log.Info($"{p}: typing method {method.ToString().ToLowerInvariant()}");
        if (method == TypingMethod.Paste)
            _icon.ShowBalloonTip(6000, "AutoLang", UiText.F(T.PasteNotice, AppName(p)), ToolTipIcon.Info);
    }

    /// <summary>Active languages grouped by alphabet, with any word list still on its way.</summary>
    private string LayoutSummary()
    {
        var summary = _languages.Summary();
        int downloading = _languages.Languages.Count(l => l.Active && l.State == WordListState.Downloading);
        return downloading > 0 ? $"{summary}  ({UiText.F(T.WordListsDownloading, downloading)})" : summary;
    }

    private void ShowLanguages()
    {
        if (_languagesForm is { IsDisposed: false }) { _languagesForm.Activate(); return; }
        _languagesForm = new LanguagesForm(_languages);
        _languagesForm.FormClosed += (_, _) => { _languagesForm?.Dispose(); _languagesForm = null; };
        _languagesForm.Show();
    }

    private void TogglePause() => TogglePause(quiet: false);

    private void TogglePause(bool quiet)
    {
        _settings.Paused = !_settings.Paused;
        _settings.Save();
        _controller.Reset(ResetScope.All);
        _focus.Poke();
        Refresh(_focus.Status);
        Log.Info(_settings.Paused ? "paused by user" : "resumed by user");
        // The icon only changes colour, which is easy to miss: say what happened and how to undo it. The welcome card,
        // when it is up, already says so itself.
        if (quiet || _welcome is not null) return;
        ShowStatus();
    }

    private void ShowStatus()
    {
        if (_status is { IsDisposed: false, IsClosing: false }) { _status.Restart(); return; }
        var flyout = new StatusFlyout(StatusContent);
        flyout.FormClosed += (_, _) => { flyout.Dispose(); if (_status == flyout) _status = null; };
        _status = flyout;
        flyout.Present();
    }

    private StatusModel StatusContent()
    {
        if (_settings.Paused) return new(T.PausedTitle, T.PausedHowToResume, IconArt.Paused);
        if (!_controller.CanCorrect)
            return _languages.Languages.Any(l => l.Active && l.State == WordListState.Downloading)
                ? new(T.ReadyingTitle, T.DownloadingWordLists, IconArt.Paused)
                : new(T.IdleTitle, T.NeedsTwoAlphabets, IconArt.Paused);
        return new(T.OnTitle, T.OnWhatItDoes, IconArt.Brand);
    }

    private string PauseText() => _settings.Paused ? T.ResumeAutoLang : T.PauseAutoLang;

    private string PauseIcon() => _settings.Paused ? "play" : "pause";

    /// <summary>How an app is shown to the user: "chrome", not "chrome.exe". Settings keep the full process name.</summary>
    private static string AppName(string process)
        => process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? process[..^4] : process;

    private void ToggleDisableApp()
    {
        var p = _focus.Status.Process;
        if (string.IsNullOrEmpty(p)) return;
        if (_settings.IsDisabled(p)) _settings.DisabledProcesses.RemoveAll(x => x.Equals(p, StringComparison.OrdinalIgnoreCase));
        else _settings.DisabledProcesses.Add(p);
        _settings.Save();
        _focus.Poke();
        Log.Info($"{p}: {(_settings.IsDisabled(p) ? "disabled" : "re-enabled")} by user");
    }

    private void ToggleFullscreen()
    {
        _settings.SkipFullscreen = !_settings.SkipFullscreen;
        _fullscreen.Checked = _settings.SkipFullscreen;
        _settings.Save();
        _focus.Poke();
    }

    private void OpenStartupSettings()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { Log.Warn($"open startup settings: {ex.Message}"); return; }
        _icon.ShowBalloonTip(5000, "AutoLang", T.StartupAppsNotice, ToolTipIcon.Info);
    }

    private void ToggleAutoStart()
    {
        bool want = !StartupRegistration.IsEnabled();
        if (!StartupRegistration.SetEnabled(want))
            _icon.ShowBalloonTip(4000, "AutoLang", T.StartupChangeFailed, ToolTipIcon.Warning);
        _autoStart.Checked = StartupRegistration.IsEnabled();
    }

    private void HotkeyFromMenu(string how)
    {
        // The menu click already moved focus away from the text, so the edit would land in the wrong window.
        // Pressing the key inside the text field is the reliable path; say so instead of guessing.
        _icon.ShowBalloonTip(6000, "AutoLang", how, ToolTipIcon.Info);
    }

    private void OpenLog()
    {
        if (Native.IsPackaged)
        {
            // Packaged, AppData and the temp folder are redirected into the package, where Notepad cannot follow.
            if (_logForm is { IsDisposed: false }) { _logForm.Activate(); return; }
            _logForm = new LogViewerForm();
            _logForm.FormClosed += (_, _) => { _logForm?.Dispose(); _logForm = null; };
            _logForm.Show();
            return;
        }
        // The log file, not its folder: Explorer refuses to open folders under AppData\Local on some Windows builds.
        // Notepad explicitly, because .log often has no default app and Windows would show an "Open with" prompt.
        // Open a snapshot copy from the temp folder: when the host is started from a packaged (MSIX) app, AppData\Local\AutoLang
        // is redirected into that package and Store Notepad cannot resolve the path. The copy also leaves the live log unlocked.
        try
        {
            var copy = Path.Combine(Path.GetTempPath(), "AutoLang-host.log");
            using (var src = new FileStream(Log.Path_, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var dst = new FileStream(copy, FileMode.Create, FileAccess.Write))
                src.CopyTo(dst);
            System.Diagnostics.Process.Start("notepad.exe", $"\"{copy}\"")?.Dispose();
        }
        catch (Exception ex) { Log.Warn($"open log: {ex.Message}"); }
    }

    private void Exit()
    {
        _icon.Visible = false;
        _languagesForm?.Close();
        _logForm?.Close();
        _welcome?.Close();
        _status?.Close();
        _onExit();
        ExitThread();
    }

    /// <summary>The mark at the real tray size (see <see cref="IconArt"/>), as an icon that owns its handle.</summary>
    private static Icon Draw(Color fill)
    {
        using var bmp = IconArt.Render(Math.Max(16, SystemInformation.SmallIconSize.Width), fill);
        var h = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(h);
            return (Icon)tmp.Clone(); // Clone copies the HICON, so the original can be destroyed
        }
        finally { DestroyIcon(h); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _icon.Dispose(); _green.Dispose(); _grey.Dispose(); _orange.Dispose(); }
        base.Dispose(disposing);
    }
}
