using AutoLang.Session;
using Xunit;

namespace AutoLang.Engine.Tests;

/// <summary>The double-Shift hotkey: two clean taps, recognised on the second release.</summary>
public class DoubleShiftTests
{
    [Fact]
    public void Two_clean_taps_fire_on_the_second_release()
    {
        var d = new DoubleShift();
        d.Down(1000, chord: false);
        Assert.False(d.Up(1080));
        d.Down(1250, chord: false);
        Assert.True(d.Up(1330));
    }

    [Fact]
    public void A_stray_tap_then_Shift_for_a_symbol_does_not_fire()
    {
        // The reported case: Shift tapped, then Shift+/ for "?" right after. The second press looked like a double tap.
        var d = new DoubleShift();
        d.Down(1000, chord: false);
        d.Up(1080);
        d.Down(1250, chord: false);
        d.OtherKey(); // the "/" key, while Shift is held
        Assert.False(d.Up(1330));
    }

    [Fact]
    public void Shift_for_a_symbol_then_a_tap_does_not_fire()
    {
        var d = new DoubleShift();
        d.Down(1000, chord: false);
        d.OtherKey();
        d.Up(1080);
        d.Down(1250, chord: false);
        Assert.False(d.Up(1330));
    }

    [Theory]
    [InlineData(1000, 1400, 1700, 1780)] // first tap held too long
    [InlineData(1000, 1080, 1600, 1680)] // too long between taps
    [InlineData(1000, 1080, 1250, 1600)] // second tap held too long
    public void Slow_taps_do_not_fire(long down1, long up1, long down2, long up2)
    {
        var d = new DoubleShift();
        d.Down(down1, chord: false);
        d.Up(up1);
        d.Down(down2, chord: false);
        Assert.False(d.Up(up2));
    }

    [Fact]
    public void Shift_with_Alt_or_Ctrl_is_a_layout_switch_not_a_tap()
    {
        var d = new DoubleShift();
        d.Down(1000, chord: true);
        d.Up(1080);
        d.Down(1250, chord: false);
        Assert.False(d.Up(1330));
        d.Down(1450, chord: true);
        Assert.False(d.Up(1500));
    }

    [Fact]
    public void Auto_repeat_is_one_press_and_a_third_tap_starts_over()
    {
        var d = new DoubleShift();
        d.Down(1000, chord: false);
        d.Down(1030, chord: false); // auto-repeat
        d.Up(1080);
        d.Down(1250, chord: false);
        Assert.True(d.Up(1330));
        d.Down(1450, chord: false);
        Assert.False(d.Up(1500)); // a third tap is the first of a new pair
        d.Down(1600, chord: false);
        Assert.True(d.Up(1650));
    }

    [Fact]
    public void A_clock_going_backwards_never_fires()
    {
        var d = new DoubleShift();
        d.Down(5000, chord: false);
        d.Up(5080);
        d.Down(100, chord: false); // the Windows tick count wrapped
        Assert.False(d.Up(180));
    }
}
