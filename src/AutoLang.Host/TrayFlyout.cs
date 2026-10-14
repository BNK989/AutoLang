using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoLang.Host;

/// <summary>
/// A Windows 11-style flyout at the notification area, in the tray menu's colors: the welcome card and the pause /
/// resume status. Finds the tray's corner, slides in and fades out (unless the user turned animations off), follows
/// the light / dark theme and the monitor's DPI. Subclasses lay out and paint the content.
/// </summary>
internal abstract class TrayFlyout : Form
{
    private readonly bool _activate;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 10 };
    private MenuTheme _theme = MenuTheme.Current();
    private CardFonts? _fonts;

    // Placement and the open / close animation.
    private Rectangle _workArea;
    private bool _right = true, _bottom = true;
    private long _animStart;
    private bool _closing;

    private const int OpenMs = 220, CloseMs = 120;

    protected TrayFlyout(bool activate)
    {
        _activate = activate;
        Text = "AutoLang";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        BackColor = _theme.Back;
        _timer.Tick += (_, _) => Animate();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    internal MenuTheme Theme => _theme;
    internal CardFonts Fonts => _fonts ??= new CardFonts(DeviceDpi);

    /// <summary>Fading out: about to close, so a new flyout should be made instead of reusing this one.</summary>
    public bool IsClosing => _closing;

    /// <summary>Sizes the window (<see cref="Form.ClientSize"/>) and positions the content for the current model and DPI.</summary>
    protected abstract void Relayout();

    /// <summary>A flyout that pops up by itself must not take the keyboard from whatever the user is typing in.</summary>
    protected override bool ShowWithoutActivation => !_activate;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST; // no Alt+Tab entry; stays above the window the user works in
            return cp;
        }
    }

    /// <summary>Places the flyout at the notification area and slides it in.</summary>
    public void Present()
    {
        FindTray();
        Location = _workArea.Location; // create the window on the tray's monitor, so DeviceDpi is that monitor's
        _ = Handle;
        Relayout();
        var rest = RestingPlace();
        if (Animations())
        {
            Opacity = 0;
            Location = rest with { Y = rest.Y + SlideOffset() };
            _animStart = Environment.TickCount64;
            _timer.Start();
        }
        else Location = rest;
        Show();
        if (_activate) Activate();
    }

    /// <summary>Lays out again after the content changed, keeping the flyout anchored at the tray.</summary>
    protected void Reflow()
    {
        Relayout();
        if (!_timer.Enabled) Location = RestingPlace();
        Invalidate(true);
    }

    /// <summary>Fades out and closes.</summary>
    public void Dismiss()
    {
        if (_closing || IsDisposed) return;
        _closing = true;
        if (!Animations() || !Visible) { Close(); return; }
        _animStart = Environment.TickCount64;
        _timer.Start();
    }

    private void Animate()
    {
        double t = Math.Min(1, (Environment.TickCount64 - _animStart) / (double)(_closing ? CloseMs : OpenMs));
        if (_closing)
        {
            Opacity = 1 - t;
            if (t >= 1) { _timer.Stop(); Close(); }
            return;
        }
        double k = 1 - Math.Pow(1 - t, 3); // ease-out cubic
        var rest = RestingPlace();
        Location = rest with { Y = rest.Y + (int)Math.Round((1 - k) * SlideOffset()) };
        Opacity = k;
        if (t >= 1) { _timer.Stop(); Location = rest; Opacity = 1; }
    }

    private int SlideOffset() => _bottom ? S(12) : -S(12);

    // ---- placement ----

    /// <summary>
    /// Which corner of which screen the tray is in. The notification area is at the right end of a bottom taskbar
    /// normally, at the left end with a right-to-left Windows language, and at the bottom of a side taskbar.
    /// </summary>
    private void FindTray()
    {
        Rectangle? tray = null;
        var taskbar = Native.FindWindowW("Shell_TrayWnd", null);
        if (taskbar != 0)
        {
            var notify = Native.FindWindowExW(taskbar, 0, "TrayNotifyWnd", null);
            if (Native.GetWindowRect(notify != 0 ? notify : taskbar, out var r) && r.right > r.left)
                tray = Rectangle.FromLTRB(r.left, r.top, r.right, r.bottom);
        }
        var screen = tray is { } t ? Screen.FromRectangle(t) : Screen.PrimaryScreen ?? Screen.AllScreens[0];
        _workArea = screen.WorkingArea;
        var b = screen.Bounds;
        var c = tray is { } tr ? new Point(tr.Left + tr.Width / 2, tr.Top + tr.Height / 2) : new Point(b.Right, b.Bottom);
        _right = c.X >= b.Left + b.Width / 2;
        _bottom = c.Y >= b.Top + b.Height / 2;
    }

    private Point RestingPlace()
    {
        int m = S(12); // Windows 11 flyouts keep 12 px from the taskbar and the screen edge
        return new Point(
            _right ? _workArea.Right - Width - m : _workArea.Left + m,
            _bottom ? _workArea.Bottom - Height - m : _workArea.Top + m);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        _fonts?.Dispose();
        _fonts = null;
        Reflow();
    }

    // ---- painting helpers ----

    protected int S(int px) => TrayMenu.Scale(this, px);

    protected static int TextHeight(Font f) => TextRenderer.MeasureText("Ag", f, Size.Empty, TextFormatFlags.NoPadding).Height + 2;

    /// <summary>The width of one line of text as <see cref="TextRenderer"/> draws it, padding included.</summary>
    protected static int Measure(string s, Font f) => TextRenderer.MeasureText(s, f, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;

    /// <summary>
    /// The width both flyouts share: 368 px (at 96 DPI), as in English, or wider where the UI language's longest
    /// title, subtitle or tip needs it, up to 480; past that the text ends in "…". The same in every state, so the
    /// flyout never jumps when the state changes.
    /// </summary>
    protected int CardWidth()
        => Math.Clamp(Math.Max(WelcomeCard.NeededWidth(Fonts, S), StatusFlyout.NeededWidth(Fonts, S)), S(368), S(480));

    // ---- right to left (Hebrew, Arabic, Persian) ----

    protected static bool Rtl => UiText.Current.RightToLeft;

    /// <summary>Right-aligned, right-to-left reading order, in a right-to-left UI language.</summary>
    protected static TextFormatFlags Dir => Rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : 0;

    /// <summary>The content is laid out left to right; this mirrors a box in a flyout <paramref name="width"/> wide.</summary>
    protected static Rectangle Mirror(Rectangle r, int width) => Rtl ? r with { X = width - r.Right } : r;

    /// <summary>Windows 10 has square corners and no DWM border: draw a hairline instead.</summary>
    protected void PaintBorder(Graphics g)
    {
        if (TrayMenu.FrameByDwm) return;
        using var border = new Pen(_theme.Border);
        g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    internal static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---- theme and lifetime ----

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TrayMenu.ApplyFrame(Handle, _theme);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            var theme = MenuTheme.Current();
            if (theme == _theme || IsDisposed) return;
            _theme = theme;
            BackColor = theme.Back;
            TrayMenu.ApplyFrame(Handle, theme);
            Invalidate(true);
        });
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _timer.Stop();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _fonts?.Dispose(); }
        base.Dispose(disposing);
    }

    /// <summary>Respects "Animation effects" in Windows settings.</summary>
    protected static bool Animations() => !SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION, 0, out int on, 0) || on != 0;

    private const int CS_DROPSHADOW = 0x20000, WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8;
    private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);
}

/// <summary>The flyouts' type ramp (Windows 11: subtitle 20, body 14, caption 12), in pixels at the window's DPI.</summary>
internal sealed class CardFonts : IDisposable
{
    public readonly Font Title, Body, Strong, Caption, Button, Key, ExampleTyped, ExampleFixed;

    public CardFonts(int dpi)
    {
        float k = dpi / 96f;
        Font F(string face, float px) => new(face, px * k, FontStyle.Regular, GraphicsUnit.Pixel);
        // Segoe UI Variable has no Hebrew or Arabic letters; Segoe UI has both.
        bool plain = UiText.Current.RightToLeft;
        string displaySemibold = plain ? "Segoe UI Semibold" : DisplaySemibold, displayRegular = plain ? "Segoe UI" : DisplayRegular;
        string textRegular = plain ? "Segoe UI" : TextRegular, textSemibold = plain ? "Segoe UI Semibold" : TextSemibold;
        Title = F(displaySemibold, 20);
        Body = F(textRegular, 14);
        Strong = F(textSemibold, 14);
        Caption = F(textRegular, 12);
        Button = F(textSemibold, 14);
        Key = F(textSemibold, 12);
        ExampleTyped = F(displayRegular, 22);
        ExampleFixed = F(displaySemibold, 22);
    }

    // Segoe UI Variable is Windows 11's UI font; Windows 10 has only Segoe UI.
    private static readonly string DisplaySemibold = Face("Segoe UI Variable Display Semib", "Segoe UI Semibold");
    private static readonly string DisplayRegular = Face("Segoe UI Variable Display", "Segoe UI");
    private static readonly string TextRegular = Face("Segoe UI Variable Text", "Segoe UI");
    private static readonly string TextSemibold = Face("Segoe UI Variable Text Semibold", "Segoe UI Semibold");

    private static string Face(string preferred, string fallback)
    {
        try { using var family = new FontFamily(preferred); return preferred; }
        catch (ArgumentException) { return fallback; }
    }

    public void Dispose()
    {
        foreach (var f in new[] { Title, Body, Strong, Caption, Button, Key, ExampleTyped, ExampleFixed }) f.Dispose();
    }
}
