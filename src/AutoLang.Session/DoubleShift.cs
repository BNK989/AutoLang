namespace AutoLang.Session;

/// <summary>
/// Recognises the double-Shift hotkey from Shift presses and releases. Both taps must be clean: short, and with no other
/// key pressed while Shift is down. It fires on the second <em>release</em>, not the second press, because a second press
/// is just as often Shift held for a symbol: in Hebrew or Arabic, where Shift has no capitals, a stray tap followed by
/// Shift+/ for "?" within the gap looked like a double tap and flipped the word just typed. Pure state, no clock or OS:
/// the host passes event times in milliseconds. One instance per hook thread; not thread-safe.
/// </summary>
public sealed class DoubleShift
{
    /// <summary>Each tap is held at most this long.</summary>
    public const long TapMs = 300;
    /// <summary>The second tap starts at most this long after the first one ended.</summary>
    public const long GapMs = 400;

    private bool _held, _alone, _second;
    private long _downAt, _tapAt = -1;

    /// <param name="chord">Ctrl, Alt, Win, Command or Option is held: a layout switch or a shortcut, never a tap.</param>
    public void Down(long time, bool chord)
    {
        if (_held) return; // auto-repeat, or the other Shift key
        _held = true;
        _alone = !chord;
        long gap = time - _tapAt;
        _second = _tapAt >= 0 && gap >= 0 && gap <= GapMs;
        _tapAt = -1;
        _downAt = time;
    }

    /// <summary>Shift came up (both Shift keys, where the host can tell). True when this release completes a double tap.</summary>
    public bool Up(long time)
    {
        if (!_held) return false;
        _held = false;
        long held = time - _downAt;
        bool clean = _alone && held >= 0 && held <= TapMs;
        _alone = false;
        if (_second)
        {
            _second = false;
            return clean;
        }
        _tapAt = clean ? time : -1;
        return false;
    }

    /// <summary>Any other key went down (or a modifier changed): it spoils the tap in progress and the one before it.</summary>
    public void OtherKey()
    {
        _alone = false;
        _second = false;
        _tapAt = -1;
    }
}
