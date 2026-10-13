using System.Diagnostics;
using System.Windows.Automation;
using AutoLang.Session;

namespace AutoLang.Host;

public sealed record FocusStatus(nint Hwnd, string Process, string Control, bool Blocked, string Reason)
{
    public static readonly FocusStatus Unknown = new(0, "", "", true, "starting");

    /// <summary>Reason for a window run as administrator: Windows (UIPI) hides its keys from AutoLang and refuses its input.</summary>
    public const string AdminReason = "admin window (Windows keeps AutoLang out)";
    public bool IsAdminWindow => Reason == AdminReason;
}

/// <summary>
/// Decides whether the focused control may be tracked at all, on its own thread: UI Automation calls can take
/// hundreds of milliseconds and must never run inside the hook callback. Policy is default-deny: every focus change
/// blocks the session immediately and this worker lifts the block once it has looked at the new target.
/// Checks, in order: paused, admin (elevated) window, excluded or user-disabled process, fullscreen window,
/// password field (UIA IsPassword).
/// </summary>
internal sealed class FocusMonitor : IDisposable
{
    private readonly SessionController _controller;
    private readonly Settings _settings;
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private volatile bool _stop;
    private int _seq;
    private FocusStatus _status = FocusStatus.Unknown;

    public event Action<FocusStatus>? StatusChanged;
    public FocusStatus Status => _status;

    public FocusMonitor(SessionController controller, Settings settings)
    {
        _controller = controller;
        _settings = settings;
        _thread = new Thread(Loop) { Name = "AutoLang.Focus", IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    public void Start() => _thread.Start();

    /// <summary>Focus moved (or policy changed): block now, re-evaluate on the worker. Safe from any thread, cheap.</summary>
    public void Poke()
    {
        Interlocked.Increment(ref _seq);
        _controller.Blocked = true;
        _signal.Set();
    }

    private void Loop()
    {
        while (!_stop)
        {
            _signal.WaitOne();
            if (_stop) break;
            // Coalesce bursts: Chrome and Office raise several focus events per click.
            Thread.Sleep(30);
            int seq = Volatile.Read(ref _seq);
            FocusStatus status;
            bool passing = false;
            var sw = Stopwatch.StartNew();
            try
            {
                var hwnd = Native.GetForegroundWindow();
                // Clicking the tray icon focuses the taskbar, then our menu. The app the user was in stays the target
                // (for the status line and the per-app menu items), re-checked against the settings; no typing is tracked.
                passing = IsTrayOrSelf(hwnd) && _status.Hwnd != 0 && Native.IsWindow(_status.Hwnd);
                status = passing ? Evaluate(_status.Hwnd, _status) : Evaluate(hwnd);
            }
            catch (Exception ex) { status = new FocusStatus(0, "", "", true, "focus check failed"); Log.Warn($"focus: {ex.GetType().Name}: {ex.Message}"); }
            if (Volatile.Read(ref _seq) != seq) continue; // focus moved again while we looked; the newer poke wins
            _controller.Blocked = passing || status.Blocked;
            var prev = _status;
            _status = status;
            if (prev != status)
            {
                Log.Focus(status.Process, status.Control, status.Blocked, status.Reason, sw.Elapsed.TotalMilliseconds);
                StatusChanged?.Invoke(status);
            }
        }
    }

    /// <param name="previous">The app's last status, when it is not in front: UI Automation would see our menu, not its
    /// focused control, so what it said about that control is kept.</param>
    private FocusStatus Evaluate(nint hwnd, FocusStatus? previous = null)
    {
        if (hwnd == 0) return new FocusStatus(0, "", "", true, "no foreground window");
        var process = previous?.Process ?? Native.ProcessNameOf(hwnd);
        FocusStatus Blocked(string reason, string control = "") => new(hwnd, process, control, true, reason);
        if (_settings.Paused) return Blocked("paused");
        if (Native.IsOutOfReach(hwnd)) return Blocked(FocusStatus.AdminReason);
        if (process.StartsWith("pid ")) return Blocked("inaccessible process");
        if (_settings.IsExcluded(process)) return Blocked("excluded app");
        if (_settings.IsDisabled(process)) return Blocked("disabled by you");
        if (_settings.SkipFullscreen && Native.IsFullscreen(hwnd) && !IsShell(hwnd, process)) return Blocked("fullscreen window");
        if (previous is not null)
            return previous.Reason == "password field" ? previous : new FocusStatus(hwnd, process, previous.Control, false, "");

        string control = "";
        try
        {
            var el = AutomationElement.FocusedElement;
            if (el is not null)
            {
                var cur = el.Current;
                control = cur.ControlType?.ProgrammaticName?.Replace("ControlType.", "") ?? "";
                if (cur.IsPassword) return Blocked("password field", control);
                if (string.Equals(cur.ClassName, "PasswordBox", StringComparison.Ordinal)) return Blocked("password field", control);
            }
        }
        catch (Exception ex)
        {
            // UIA is best effort: some apps expose nothing. Note it; the default exclusions cover the risky ones.
            control = "uia:" + ex.GetType().Name;
        }
        return new FocusStatus(hwnd, process, control, false, "");
    }

    /// <summary>The taskbar and its notification area, or one of our own windows (tray menu, Languages, log).</summary>
    private static bool IsTrayOrSelf(nint hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Environment.ProcessId) return true;
        return Native.ClassNameOf(hwnd) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow"
            or "TopLevelWindowForOverflowXamlIsland";
    }

    private static bool IsShell(nint hwnd, string process)
    {
        if (process.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)) return true;
        var cls = Native.ClassNameOf(hwnd);
        return cls is "Progman" or "WorkerW" or "Shell_TrayWnd";
    }

    public void Dispose()
    {
        _stop = true;
        _signal.Set();
        if (_thread.IsAlive && !_thread.Join(1000)) Log.Warn("focus thread did not stop in time");
    }
}
