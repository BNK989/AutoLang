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
    private readonly ToolStripMenuItem _status, _layouts, _pause, _disableApp, _typing, _undo, _convert, _fullscreen, _autoStart;
    private readonly ToolStripMenuItem[] _typingItems;
    private bool _adminNoticeShown;
    private readonly Icon _green, _grey, _orange;
    private readonly SynchronizationContext _ui;
    private readonly Action _onExit;

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

        var menu = new ContextMenuStrip();
        _status = new ToolStripMenuItem("Starting…") { Enabled = false };
        _layouts = new ToolStripMenuItem(LayoutSummary()) { Enabled = false };
        // ToolStrip draws a "\t" in the text as nothing; the shortcut column only shows ShortcutKeyDisplayString.
        _pause = new ToolStripMenuItem(PauseText(), null, (_, _) => TogglePause()) { ShortcutKeyDisplayString = "Click icon" };
        _disableApp = new ToolStripMenuItem("Disable for this app", null, (_, _) => ToggleDisableApp());
        _typingItems = new[]
        {
            TypingItem("Unicode (default)", TypingMethod.Unicode),
            TypingItem("Simulated keys", TypingMethod.Keys),
            TypingItem("Paste via clipboard", TypingMethod.Paste),
        };
        _typing = new ToolStripMenuItem("Typing method for this app");
        _typing.DropDownItems.AddRange(_typingItems);
        _typing.DropDownItems.Add(new ToolStripSeparator());
        _typing.DropDownItems.Add(new ToolStripMenuItem("Only change this if corrections come out garbled in this app.") { Enabled = false });
        _undo = new ToolStripMenuItem("Undo last correction", null, (_, _) => HotkeyFromMenu(
            "Press the Pause key in the text field, right after a correction, to undo it.")) { ShortcutKeyDisplayString = "Pause" };
        _convert = new ToolStripMenuItem("Convert what you just typed", null, (_, _) => HotkeyFromMenu(
            "Press Shift+Pause in the text field, right after typing, to switch it to the other layout.")) { ShortcutKeyDisplayString = "Shift+Pause" };
        _fullscreen = new ToolStripMenuItem("Skip fullscreen windows", null, (_, _) => ToggleFullscreen()) { Checked = settings.SkipFullscreen };
        // Packaged, the start-at-login entry is the manifest's StartupTask, switched in Windows Settings (PLAN.md §5, M4).
        _autoStart = Native.IsPackaged
            ? new ToolStripMenuItem("Start with Windows…", null, (_, _) => OpenStartupSettings())
            : new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleAutoStart()) { Checked = StartupRegistration.IsEnabled() };
        menu.Items.AddRange(new ToolStripItem[]
        {
            _status, _layouts, new ToolStripSeparator(),
            _pause, _disableApp, _typing, new ToolStripSeparator(),
            _undo, _convert, new ToolStripSeparator(),
            new ToolStripMenuItem("Languages…", null, (_, _) => ShowLanguages()),
            _fullscreen,
            _autoStart,
            new ToolStripMenuItem(Native.IsPackaged ? "Show log" : "Open log file", null, (_, _) => OpenLog()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => Exit()),
        });
        menu.Opening += (_, _) => RefreshMenu();

        _icon = new NotifyIcon { Icon = _green, Visible = true, ContextMenuStrip = menu, Text = "AutoLang" };
        // One click, not a double-click: a double-click still raises one MouseClick, so it toggles once too.
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) TogglePause(); };

        _focus.StatusChanged += s => _ui.Post(_ => Refresh(s), null);
        _corrector.Applied += _ => _ui.Post(_ => Refresh(_focus.Status), null);
        _languages.Changed += () => _ui.Post(_ => { _layouts.Text = LayoutSummary(); Refresh(_focus.Status); }, null);
        Refresh(_focus.Status);
    }

    private void Refresh(FocusStatus s)
    {
        if (s.IsAdminWindow && !_settings.Paused && !_adminNoticeShown)
        {
            // Once per session: the orange icon alone does not say why nothing gets corrected here.
            _adminNoticeShown = true;
            _icon.ShowBalloonTip(6000, "AutoLang can't help in this window",
                $"{s.Process} is running as administrator, and Windows does not let ordinary apps read or type into it. " +
                "AutoLang keeps working everywhere else.", ToolTipIcon.Info);
        }
        string state = _settings.Paused ? "Paused" : !_controller.CanCorrect ? "Idle: needs two alphabets" : s.Blocked ? $"Blocked: {s.Reason}" : "Watching";
        string app = string.IsNullOrEmpty(s.Process) ? "" : $" — {s.Process}";
        _status.Text = state + app;
        _icon.Icon = _settings.Paused || !_controller.CanCorrect ? _grey : s.Blocked ? _orange : _green;
        var tip = $"AutoLang: {state}{app}\n{_corrector.AppliedCount} corrections this session\nClick to {(_settings.Paused ? "resume" : "pause")}";
        _icon.Text = tip.Length > 127 ? tip[..127] : tip;
    }

    private void RefreshMenu()
    {
        var p = _focus.Status.Process;
        bool hasApp = !string.IsNullOrEmpty(p) && !p.StartsWith("pid ") && !_settings.IsExcluded(p);
        _disableApp.Enabled = hasApp;
        _disableApp.Text = hasApp ? $"Disable for {p}" : "Disable for this app";
        _disableApp.Checked = hasApp && _settings.IsDisabled(p);
        _typing.Enabled = hasApp;
        _typing.Text = hasApp ? $"Typing method for {p}" : "Typing method for this app";
        TypingMethod current;
        lock (_settings) current = hasApp ? _settings.TypingMethodFor(p) : TypingMethod.Unicode;
        foreach (var item in _typingItems) item.Checked = (TypingMethod)item.Tag! == current;
        _undo.Enabled = _controller.CanUndo;
        _convert.Enabled = !_settings.Paused;
        _pause.Text = PauseText();
        if (!Native.IsPackaged) _autoStart.Checked = StartupRegistration.IsEnabled();
    }

    private ToolStripMenuItem TypingItem(string text, TypingMethod method)
        => new(text, null, (_, _) => SetTypingMethod(method)) { Tag = method };

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
            _icon.ShowBalloonTip(6000, "AutoLang",
                $"Corrections in {p} now go through the clipboard. Your copied content is put back right after each one " +
                "and is never kept or sent anywhere.", ToolTipIcon.Info);
    }

    /// <summary>Active languages grouped by alphabet, with any word list still on its way.</summary>
    private string LayoutSummary()
    {
        var summary = _languages.Summary();
        int downloading = _languages.Languages.Count(l => l.Active && l.State == WordListState.Downloading);
        return downloading > 0 ? $"{summary}  (downloading {downloading} word list{(downloading == 1 ? "" : "s")})" : summary;
    }

    private void ShowLanguages()
    {
        if (_languagesForm is { IsDisposed: false }) { _languagesForm.Activate(); return; }
        _languagesForm = new LanguagesForm(_languages);
        _languagesForm.FormClosed += (_, _) => { _languagesForm?.Dispose(); _languagesForm = null; };
        _languagesForm.Show();
    }

    private void TogglePause()
    {
        _settings.Paused = !_settings.Paused;
        _settings.Save();
        _controller.Reset(ResetScope.All);
        _focus.Poke();
        Refresh(_focus.Status);
        Log.Info(_settings.Paused ? "paused by user" : "resumed by user");
        // The icon only changes colour, which is easy to miss: say what happened and how to undo it.
        if (_settings.Paused)
            _icon.ShowBalloonTip(3000, "AutoLang is paused", "Nothing is corrected until you click the tray icon again.", ToolTipIcon.None);
        else
            _icon.ShowBalloonTip(3000, "AutoLang is on", "Fixing words typed in the wrong layout again.", ToolTipIcon.None);
    }

    private string PauseText() => _settings.Paused ? "Resume AutoLang" : "Pause AutoLang";

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
        _icon.ShowBalloonTip(5000, "AutoLang", "Turn AutoLang on or off in the list of startup apps.", ToolTipIcon.Info);
    }

    private void ToggleAutoStart()
    {
        bool want = !StartupRegistration.IsEnabled();
        if (!StartupRegistration.SetEnabled(want))
            _icon.ShowBalloonTip(4000, "AutoLang", "Could not change the startup setting.", ToolTipIcon.Warning);
        _autoStart.Checked = StartupRegistration.IsEnabled();
    }

    private void HotkeyFromMenu(string how)
    {
        // The menu click already moved focus away from the text, so the edit would land in the wrong window.
        // Pressing the key inside the text field is the reliable path; say so instead of guessing.
        _icon.ShowBalloonTip(4000, "AutoLang", how, ToolTipIcon.Info);
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
