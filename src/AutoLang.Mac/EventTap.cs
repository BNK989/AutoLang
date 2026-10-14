using System.Diagnostics;
using System.Runtime.InteropServices;
using AutoLang.Session;
using static AutoLang.Mac.Native;

namespace AutoLang.Mac;

/// <summary>
/// The CGEventTap, the macOS counterpart of the Windows low-level hook. Its callback runs on the main run loop (input
/// source calls must happen there) and is the hot path: it resolves the typed character, feeds the
/// <see cref="SessionController"/> and, when a correction comes back, swallows the boundary key and hands the correction
/// to the <see cref="Corrector"/>. It never injects and never logs key content.
/// </summary>
internal sealed unsafe class EventTap
{
    private const double SlowCallbackMs = 5.0;
    /// <summary>Double-tapping Shift: each tap held at most this long, the second pressed at most this long after the first.</summary>
    private const long ShiftTapMs = 300, ShiftGapMs = 400;

    // Mac virtual key codes (HIToolbox Events.h).
    private const ushort KeyReturn = 0x24, KeyEnter = 0x4C, KeyTab = 0x30, KeyDelete = 0x33, KeyEscape = 0x35,
        KeyForwardDelete = 0x75, KeyHelp = 0x72, KeyHome = 0x73, KeyEnd = 0x77, KeyPageUp = 0x74, KeyPageDown = 0x79,
        KeyLeft = 0x7B, KeyRight = 0x7C, KeyDown = 0x7D, KeyUp = 0x7E, KeyLeftShift = 0x38, KeyRightShift = 0x3C;
    private static readonly HashSet<ushort> FunctionKeys =
        [0x7A, 0x78, 0x63, 0x76, 0x60, 0x61, 0x62, 0x64, 0x65, 0x6D, 0x67, 0x6F, 0x69, 0x6B, 0x71, 0x6A, 0x40, 0x4F, 0x50, 0x5A];

    private const ulong CommandKeys = kCGEventFlagMaskCommand | kCGEventFlagMaskControl | kCGEventFlagMaskAlternate;

    private static EventTap? _instance;

    private readonly SessionController _controller;
    private readonly Corrector _corrector;
    private readonly InputSources _sources;
    private readonly Func<string> _app;
    private nint _tap, _runLoopSource;

    // Double-tap Shift state. _shiftTapAt is when a clean tap (no other key during it) was released, 0 = none.
    private bool _shiftHeld, _shiftAlone;
    private long _shiftDownAt, _shiftTapAt;

    public EventTap(SessionController controller, Corrector corrector, InputSources sources, Func<string> app)
    {
        _controller = controller;
        _corrector = corrector;
        _sources = sources;
        _app = app;
    }

    public bool Installed => _tap != 0;

    /// <summary>True while the tap exists and macOS has not disabled it.</summary>
    public bool Healthy => _tap != 0 && CGEventTapIsEnabled(_tap) != 0;

    /// <summary>Creates the tap. False when macOS refuses (no Accessibility permission yet). Main thread.</summary>
    public bool Install()
    {
        if (_tap != 0) return true;
        ulong mask = (1UL << (int)kCGEventKeyDown) | (1UL << (int)kCGEventKeyUp) | (1UL << (int)kCGEventFlagsChanged)
            | (1UL << (int)kCGEventLeftMouseDown) | (1UL << (int)kCGEventRightMouseDown) | (1UL << (int)kCGEventOtherMouseDown);
        _instance = this;
        _tap = CGEventTapCreate(kCGSessionEventTap, kCGHeadInsertEventTap, kCGEventTapOptionDefault, mask, &Callback, 0);
        if (_tap == 0) return false;
        _runLoopSource = CFMachPortCreateRunLoopSource(0, _tap, 0);
        CFRunLoopAddSource(CFRunLoopGetMain(), _runLoopSource, kCFRunLoopCommonModes);
        CGEventTapEnable(_tap, 1);
        Log.Info("event tap installed");
        return true;
    }

    /// <summary>Turns the tap back on if macOS switched it off. Main thread.</summary>
    public void EnsureEnabled()
    {
        if (_tap == 0 || CGEventTapIsEnabled(_tap) != 0) return;
        Log.Warn("event tap was disabled; re-enabling");
        CGEventTapEnable(_tap, 1);
        _controller.Reset(ResetScope.All);
    }

    [UnmanagedCallersOnly]
    private static nint Callback(nint proxy, uint type, nint ev, nint userInfo)
    {
        var self = _instance;
        if (self is null) return ev;
        var t0 = Stopwatch.GetTimestamp();
        bool swallow = false;
        try { swallow = self.Handle(type, ev); }
        catch (Exception ex) { Log.Warn($"tap: {ex.GetType().Name}: {ex.Message}"); self._controller.Reset(ResetScope.All); }
        var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        if (ms > SlowCallbackMs) Log.SlowCallback(ms);
        return swallow ? 0 : ev;
    }

    /// <summary>Returns true when the event must be swallowed (the Corrector re-emits it after the replacement).</summary>
    private bool Handle(uint type, nint ev)
    {
        switch (type)
        {
            case kCGEventTapDisabledByTimeout or kCGEventTapDisabledByUserInput:
                // macOS turns a slow tap off; turn it straight back on. Whatever was typed meanwhile was not seen.
                Log.Warn(type == kCGEventTapDisabledByTimeout ? "event tap timed out; re-enabling" : "event tap disabled by user input; re-enabling");
                CGEventTapEnable(_tap, 1);
                _controller.Reset(ResetScope.All);
                return false;
            case kCGEventLeftMouseDown or kCGEventRightMouseDown or kCGEventOtherMouseDown:
                _controller.Reset(ResetScope.All); // the caret may have moved
                return false;
        }
        if (CGEventGetIntegerValueField(ev, kCGEventSourceUserData) == Corrector.Signature) return false; // our own keys
        var keyCode = (ushort)CGEventGetIntegerValueField(ev, kCGKeyboardEventKeycode);
        var flags = CGEventGetFlags(ev);

        if (type == kCGEventFlagsChanged) { ModifierChanged(keyCode, flags); return false; }
        if (type != kCGEventKeyDown) return false;

        _shiftAlone = false; _shiftTapAt = 0; // any other key breaks a double tap

        if (_controller.Blocked) return false;
        if (IsSecureEventInputEnabled() != 0) { _controller.Reset(ResetScope.All); return false; } // a password field
        if ((flags & CommandKeys) != 0) { _controller.Reset(ResetScope.Run); return false; } // a shortcut, or an Option character

        switch (keyCode)
        {
            case KeyDelete:
                if (_controller.OnBackspace() is { } back) _corrector.EnqueueSwitch(back);
                return false;
            case KeyReturn or KeyEnter:
                return Apply(_controller.OnEnter(_sources.Current()?.LangId ?? 0), flags);
            case KeyEscape or KeyTab or KeyForwardDelete or KeyHelp or KeyHome or KeyEnd or KeyPageUp or KeyPageDown
              or KeyLeft or KeyRight or KeyUp or KeyDown:
                _controller.Reset(ResetScope.Run);
                return false;
        }
        if (FunctionKeys.Contains(keyCode)) { _controller.Reset(ResetScope.Run); return false; }

        var source = _sources.Current();
        if (source is null) { _controller.Reset(ResetScope.Run); return false; } // an input method, or a layout we do not know

        bool shift = (flags & kCGEventFlagMaskShift) != 0, caps = (flags & kCGEventFlagMaskAlphaShift) != 0;
        if (InputSources.Translate(source, keyCode, shift, caps, out _) is not { } c || char.IsControl(c))
        { _controller.Reset(ResetScope.Run); return false; } // dead key, no character, or more than one

        return Apply(_controller.OnChar(c, source.LangId), flags);
    }

    /// <summary>
    /// Shift, Command, Control, Option, Caps Lock and fn arrive as flag changes, not key presses. Double-tapping Shift is
    /// the hotkey (as on Windows): it undoes the correction just made, or else converts the current run. Shift is never
    /// swallowed; a lone Shift does nothing in apps.
    /// </summary>
    private void ModifierChanged(ushort keyCode, ulong flags)
    {
        if (keyCode is not (KeyLeftShift or KeyRightShift))
        {
            _shiftAlone = false; _shiftTapAt = 0;
            if ((flags & CommandKeys) != 0) _controller.Reset(ResetScope.Run);
            return;
        }
        bool down = (flags & (keyCode == KeyLeftShift ? NX_DEVICELSHIFTKEYMASK : NX_DEVICERSHIFTKEYMASK)) != 0;
        long now = Environment.TickCount64;
        if (down)
        {
            if (_shiftHeld) return; // the other Shift key
            _shiftHeld = true;
            if (_shiftTapAt != 0 && now - _shiftTapAt <= ShiftGapMs)
            {
                _shiftTapAt = 0;
                _shiftAlone = false;
                Hotkey(convert: !_controller.CanUndo);
                return;
            }
            // Shift with Command, Control or Option held is a shortcut, not a tap.
            _shiftAlone = (flags & CommandKeys) == 0;
            _shiftDownAt = now;
            return;
        }
        if ((flags & (NX_DEVICELSHIFTKEYMASK | NX_DEVICERSHIFTKEYMASK)) != 0) return; // the other Shift is still down
        _shiftHeld = false;
        _shiftTapAt = _shiftAlone && now - _shiftDownAt <= ShiftTapMs ? Math.Max(now, 1) : 0;
        _shiftAlone = false;
    }

    private void Hotkey(bool convert)
    {
        Correction? c = convert ? _controller.Force(_sources.Current()?.LangId ?? 0) : _controller.Undo();
        if (c is not null) Enqueue(c, shiftEnter: false);
    }

    private bool Apply(Correction? c, ulong flags)
    {
        if (c is null) return false;
        // Shift+Enter means "newline, don't send" in Messages, WhatsApp, Slack: the re-emitted Enter must keep it.
        Enqueue(c, shiftEnter: c.PressEnter && (flags & kCGEventFlagMaskShift) != 0);
        return true; // the boundary key is inside Correction.Text / PressEnter; the Corrector re-emits it
    }

    private void Enqueue(Correction c, bool shiftEnter) => _corrector.Enqueue(new Corrector.Job(c, _app(), shiftEnter));
}
