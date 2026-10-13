using System.Diagnostics;
using System.Text;
using AutoLang.Session;
using static AutoLang.Host.Native;

namespace AutoLang.Host;

/// <summary>
/// Owns the low-level keyboard and mouse hooks and the focus WinEvent on a dedicated message-loop thread.
/// The callback is the hot path: it resolves the typed character, feeds the pure <see cref="SessionController"/>
/// and, when a correction comes back, swallows the boundary key and hands the correction to the <see cref="Corrector"/>.
/// It never injects, never touches UI Automation and never logs key content.
/// Mechanics are those validated by the M0 spike (see spike/M0Spike/Hook.cs).
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const double SlowCallbackMs = 5.0;
    private const uint RearmTimerId = 1;
    private const uint RearmIntervalMs = 5 * 60 * 1000; // Windows silently drops hooks that time out; re-arm periodically.
    private const uint WatchdogTimerId = 2;
    private const uint WatchdogIntervalMs = 2000;
    /// <summary>A key reaching the callback this late means the thread stalled: Windows may have dropped the hook.</summary>
    private const uint LateDeliveryMs = 500;
    /// <summary>Double-tapping Shift: each tap held at most this long, the second pressed at most this long after the first.</summary>
    private const uint ShiftTapMs = 300, ShiftGapMs = 400;

    private readonly SessionController _controller;
    private readonly Corrector _corrector;
    private readonly FocusMonitor _focus;
    private readonly Func<nint, InstalledLayout?> _layoutOf;
    private readonly Func<ushort, nint> _hklOfLang;
    /// <summary>Hook thread only: the layout variant the user last typed with per language, so a switch lands on it.</summary>
    private readonly Dictionary<ushort, nint> _lastHkl = new();

    private readonly LowLevelProc _kbProc;   // kept alive: the OS holds a raw function pointer to these
    private readonly LowLevelProc _mouseProc;
    private readonly WinEventProc _focusProc;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private uint _threadId;
    private nint _kbHook, _mouseHook, _focusHook;

    private bool _caps;
    // Double-tap Shift state, hook thread only. _shiftTapAt is when a clean tap (no other key during it) was released, 0 = none.
    private bool _shiftHeld, _shiftAlone;
    private uint _shiftDownAt, _shiftTapAt;
    // Watchdog state, hook thread only (PLAN.md §7: a silently dropped hook must not leave AutoLang deaf).
    private uint _lastHookTick;
    private bool _late;
    private uint _lastWatchdogRearm;
    private readonly byte[] _state = new byte[256];
    private readonly StringBuilder _sb = new(8);

    public KeyboardHook(SessionController controller, Corrector corrector, FocusMonitor focus,
        Func<nint, InstalledLayout?> layoutOf, Func<ushort, nint> hklOfLang)
    {
        _controller = controller;
        _corrector = corrector;
        _focus = focus;
        _layoutOf = layoutOf;
        _hklOfLang = hklOfLang;
        _kbProc = KeyboardCallback;
        _mouseProc = MouseCallback;
        _focusProc = FocusCallback;
        _thread = new Thread(Loop) { Name = "AutoLang.Hook", IsBackground = true, Priority = ThreadPriority.Highest };
    }

    public void Start()
    {
        _thread.Start();
        if (!_ready.Wait(5000) || _kbHook == 0) throw new InvalidOperationException("keyboard hook could not be installed");
    }

    private void Loop()
    {
        _threadId = GetCurrentThreadId();
        _caps = (GetKeyState(VK_CAPITAL) & 1) != 0;
        Install();
        _ready.Set();
        SetTimer(0, RearmTimerId, RearmIntervalMs, 0);
        SetTimer(0, WatchdogTimerId, WatchdogIntervalMs, 0);
        while (GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            if (msg.message == WM_TIMER && msg.hwnd == 0)
            {
                if (msg.wParam == RearmTimerId) { Uninstall(); Install(); continue; }
                if (msg.wParam == WatchdogTimerId) { Watchdog(); continue; }
            }
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        KillTimer(0, WatchdogTimerId);
        KillTimer(0, RearmTimerId);
        Uninstall();
    }

    private void Install()
    {
        var mod = GetModuleHandleW(null);
        _kbHook = SetWindowsHookExW(WH_KEYBOARD_LL, _kbProc, mod, 0);
        _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, _mouseProc, mod, 0);
        _focusHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_FOCUS, 0, _focusProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (_kbHook == 0) Log.Warn($"SetWindowsHookEx(keyboard) failed, err {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
        if (_mouseHook == 0) Log.Warn("SetWindowsHookEx(mouse) failed; clicks will not end runs");
        if (_focusHook == 0) Log.Warn("SetWinEventHook failed; focus changes detected per keystroke only");
        Log.Info($"hooks installed (kb 0x{(long)_kbHook:X}, mouse 0x{(long)_mouseHook:X}, focus 0x{(long)_focusHook:X})");
        _lastHookTick = GetTickCount();
        _late = false;
    }

    /// <summary>
    /// Re-arms the hooks early when they look dropped: a callback arrived late (Windows removes hooks that time out,
    /// without telling us), or Windows saw input that neither hook did while an ordinary window was in front. Input to
    /// admin windows is invisible to us by design, so that case is ruled out first. At most every 30 s.
    /// </summary>
    private void Watchdog()
    {
        uint now = GetTickCount();
        string? why = null;
        if (_late) why = "a hook callback ran late";
        else
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref lii) && (int)(lii.dwTime - _lastHookTick) > 1500 && now - lii.dwTime < 10_000)
            {
                var fg = GetForegroundWindow();
                if (fg != 0 && !IsOutOfReach(fg)) why = "input arrived that the hooks did not see";
            }
        }
        if (why is null || now - _lastWatchdogRearm < 30_000) return;
        _lastWatchdogRearm = now;
        Log.Warn($"watchdog: {why}; re-arming hooks");
        _controller.Reset(ResetScope.All);
        Uninstall();
        Install();
    }

    private void Uninstall()
    {
        if (_kbHook != 0) UnhookWindowsHookEx(_kbHook);
        if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
        if (_focusHook != 0) UnhookWinEvent(_focusHook);
        _kbHook = _mouseHook = _focusHook = 0;
    }

    // ---- focus & mouse: run boundaries (PLAN.md §3.2) ----

    private void FocusCallback(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        // Another window came to the front, or focus moved to another control in the same window (next form field):
        // either way the run is over and the new target must be vetted again (it may be a password box).
        if (eventType is EVENT_SYSTEM_FOREGROUND or EVENT_OBJECT_FOCUS)
        {
            _controller.Reset(ResetScope.All);
            _focus.Poke();
        }
    }

    private nint MouseCallback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode >= 0) _lastHookTick = GetTickCount();
        if (nCode >= 0 && (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN || wParam == WM_MBUTTONDOWN || wParam == WM_XBUTTONDOWN))
            _controller.Reset(ResetScope.All);
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    // ---- keyboard: the hot path ----

    private nint KeyboardCallback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode < 0) return CallNextHookEx(_kbHook, nCode, wParam, lParam);
        var t0 = Stopwatch.GetTimestamp();
        bool swallow = false;
        try
        {
            unsafe
            {
                ref var k = ref *(KBDLLHOOKSTRUCT*)lParam;
                _lastHookTick = GetTickCount();
                if (_lastHookTick - k.time > LateDeliveryMs && _lastHookTick - k.time < 60_000) _late = true;
                if (k.dwExtraInfo != Injector.Signature) swallow = HandleKey(ref k, wParam);
            }
        }
        catch (Exception ex) { Log.Warn($"hook: {ex.GetType().Name}: {ex.Message}"); _controller.Reset(ResetScope.All); }
        var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        if (ms > SlowCallbackMs) Log.SlowCallback(ms);
        if (ms > LateDeliveryMs) _late = true;
        return swallow ? 1 : CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    /// <summary>Returns true when the key must be swallowed (it is re-emitted by the Corrector after the replacement).</summary>
    private bool HandleKey(ref KBDLLHOOKSTRUCT k, nuint wParam)
    {
        bool down = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
        int vk = (int)k.vkCode;

        if (vk is VK_SHIFT or VK_LSHIFT or VK_RSHIFT) { ShiftKey(down, k.time); return false; }
        if (down) { _shiftAlone = false; _shiftTapAt = 0; } // any other key breaks a double tap

        if (vk == VK_CAPITAL) { if (down) _caps = !_caps; return false; }
        if (!down) return false;

        // Hotkeys (PLAN.md §3.4): Pause = undo last correction, Shift+Pause = force-convert the current run.
        if (vk == VK_PAUSE) return Hotkey(convert: (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0);
        if (vk is VK_CONTROL or VK_LCONTROL or VK_RCONTROL or VK_MENU or VK_LMENU or VK_RMENU or VK_LWIN or VK_RWIN or VK_APPS)
        { _controller.Reset(ResetScope.Run); return false; }

        var hwnd = GetForegroundWindow();
        if (hwnd != _focus.Status.Hwnd)
        {
            // Belt and braces for the WinEvent: a key in a window the focus worker has not vetted yet stays untracked.
            _controller.Reset(ResetScope.All);
            _focus.Poke();
            return false;
        }
        if (_controller.Blocked) return false;

        bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        bool win = ((GetAsyncKeyState(VK_LWIN) | GetAsyncKeyState(VK_RWIN)) & 0x8000) != 0;
        if (ctrl || alt || win) { _controller.Reset(ResetScope.Run); return false; }

        switch (vk)
        {
            case VK_BACK:
                if (_controller.OnBackspace() is { } back) SwitchBack(back, hwnd);
                return false;
            case VK_RETURN: return Apply(_controller.OnEnter(LangOf(hwnd, out _)), hwnd);
            case VK_ESCAPE or VK_TAB or VK_DELETE or VK_INSERT
              or VK_LEFT or VK_RIGHT or VK_UP or VK_DOWN or VK_HOME or VK_END or VK_PRIOR or VK_NEXT:
                _controller.Reset(ResetScope.Run); return false;
        }
        if (vk >= VK_F1 && vk <= VK_F24) { _controller.Reset(ResetScope.Run); return false; }

        var langId = LangOf(hwnd, out var hkl);
        if (langId == 0) { _controller.Reset(ResetScope.Run); return false; }

        bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        Array.Clear(_state);
        if (shift) _state[VK_SHIFT] = 0x80;
        if (_caps) _state[VK_CAPITAL] = 0x01;
        int rc = ToUnicodeEx(k.vkCode, k.scanCode, _state, _sb, _sb.Capacity, TOUNICODE_NO_STATE_CHANGE, hkl);
        if (rc != 1) { _controller.Reset(ResetScope.Run); return false; } // dead key, no char, or a multi-char sequence
        char c = _sb[0];
        if (char.IsControl(c)) { _controller.Reset(ResetScope.Run); return false; }

        return Apply(_controller.OnChar(c, langId), hwnd);
    }

    /// <summary>
    /// Double-tapping Shift, the hotkey every keyboard has (most laptops lack Pause): undoes the correction just made, or
    /// else converts the current run, so either way it flips what was just typed to the other layout. Shift is never
    /// swallowed; a lone Shift does nothing in apps, and keys injected while it is held are unaffected by it.
    /// </summary>
    private void ShiftKey(bool down, uint time)
    {
        if (down)
        {
            if (_shiftHeld) return; // auto-repeat, or the other Shift key
            _shiftHeld = true;
            if (_shiftTapAt != 0 && time - _shiftTapAt <= ShiftGapMs)
            {
                _shiftTapAt = 0;
                _shiftAlone = false;
                Hotkey(convert: !_controller.CanUndo);
                return;
            }
            // Shift with Ctrl, Alt or Win held is a layout switch (Alt+Shift, Ctrl+Shift) or a shortcut, not a tap.
            static bool Held(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
            _shiftAlone = !(Held(VK_CONTROL) || Held(VK_MENU) || Held(VK_LWIN) || Held(VK_RWIN));
            _shiftDownAt = time;
            return;
        }
        _shiftHeld = false;
        _shiftTapAt = _shiftAlone && time - _shiftDownAt <= ShiftTapMs ? Math.Max(time, 1) : 0;
        _shiftAlone = false;
    }

    private bool Hotkey(bool convert)
    {
        var hwnd = GetForegroundWindow();
        Correction? c = convert ? _controller.Force(LangOf(hwnd, out _)) : _controller.Undo();
        if (c is null) return false;
        Enqueue(c, hwnd);
        return true; // the hotkey did something: do not let the app see the Pause key
    }

    private bool Apply(Correction? c, nint hwnd)
    {
        if (c is null) return false;
        Enqueue(c, hwnd);
        return true; // boundary key is inside Correction.Text / PressEnter; the Corrector re-emits it
    }

    private void Enqueue(Correction c, nint hwnd)
    {
        // Shift+Enter means "newline, don't send" in WhatsApp, Teams, Outlook: the re-emitted Enter must keep it.
        bool shift = c.PressEnter && (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        var hkl = _lastHkl.TryGetValue(c.TargetLangId, out var last) ? last : _hklOfLang(c.TargetLangId);
        _corrector.Enqueue(new Corrector.Job(c, hwnd, hkl, _focus.Status.Process, shift, _caps));
    }

    /// <summary>
    /// The user erased our whole correction: put the window back in the layout they had typed it in. Queued behind any
    /// correction still being applied, so our own switch to the target cannot land after it. Harmless if the user
    /// already switched back themselves.
    /// </summary>
    private void SwitchBack(ushort lang, nint hwnd)
    {
        var hkl = _lastHkl.TryGetValue(lang, out var last) ? last : _hklOfLang(lang);
        if (hkl != 0) _corrector.EnqueueSwitch(hwnd, hkl);
    }

    private ushort LangOf(nint hwnd, out nint hkl)
    {
        hkl = HklOfWindow(hwnd);
        var lang = _layoutOf(hkl)?.LangId ?? 0;
        if (lang != 0) _lastHkl[lang] = hkl;
        return lang;
    }

    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
        if (_thread.IsAlive && !_thread.Join(2000)) Log.Warn("hook thread did not stop in time");
    }
}
