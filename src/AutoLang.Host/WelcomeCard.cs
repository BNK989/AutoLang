using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AutoLang.Engine;
using Microsoft.Win32;

namespace AutoLang.Host;

/// <summary>What the welcome card says. The tray builds it from its own state and rebuilds it on every change.</summary>
internal sealed record WelcomeModel(string Title, string Subtitle, string Body, Color Mark, string Action, (string Typed, string Fixed)? Example);

/// <summary>
/// The first-run card, also shown when AutoLang is launched while it is already running. AutoLang has no window, so
/// without it nothing says that it started, what it does, or where it went. A small Windows 11-style flyout at the
/// notification area: an example built from the user's own keyboards, the two things worth knowing (Shift twice, click
/// to pause) and where Windows hides new tray icons. Painted by hand in the tray menu's colors; only the two buttons
/// are controls, for focus, keyboard and screen readers.
/// </summary>
internal sealed class WelcomeCard : Form
{
    private readonly Func<WelcomeModel> _model;
    private readonly Action _primary;
    private readonly bool _activate;
    private readonly CardButton _action, _close;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 10 };
    private WelcomeModel _m;
    private MenuTheme _theme = MenuTheme.Current();
    private CardFonts? _f;

    // Layout in client pixels at the current DPI (Relayout).
    private Rectangle _markBox, _titleBox, _subtitleBox, _bodyBox, _panel, _tipUndo, _tipPause, _tipHidden, _footer;

    // Placement and the open / close animation.
    private Rectangle _workArea;
    private bool _right = true, _bottom = true;
    private long _animStart;
    private bool _closing;

    private const int OpenMs = 220, CloseMs = 120;
    private const TextFormatFlags Line = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
    private const TextFormatFlags Wrap = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
    private const TextFormatFlags Run = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;

    public WelcomeCard(Func<WelcomeModel> model, Action primary, bool activate)
    {
        _model = model;
        _primary = primary;
        _activate = activate;
        _m = model();
        Text = "AutoLang";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = _theme.Back;

        _action = new CardButton(this, accent: true);
        _action.Click += (_, _) => Primary();
        _close = new CardButton(this, accent: false) { Icon = "dismiss", AccessibleName = "Close" };
        _close.Click += (_, _) => Dismiss();
        Controls.Add(_action);
        Controls.Add(_close);

        _timer.Tick += (_, _) => Animate();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    internal MenuTheme Theme => _theme;
    internal CardFonts Fonts => _f!;

    /// <summary>A card that pops up by itself must not take the keyboard from whatever the user is typing in.</summary>
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

    /// <summary>Places the card at the notification area and slides it in.</summary>
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
        if (_activate) { Activate(); _action.Focus(); }
    }

    /// <summary>Rereads the model (pause, word lists arriving) and keeps the card anchored at the tray.</summary>
    public void Reload()
    {
        if (IsDisposed || _closing) return;
        _m = _model();
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

    /// <summary>
    /// "Resume" turns into "Got it" under the pointer: the second half of a double-click must not close the card the
    /// user just brought back to life.
    /// </summary>
    private void Primary()
    {
        if (Environment.TickCount64 - _actionChanged < SystemInformation.DoubleClickTime) return;
        _primary();
    }

    private long _actionChanged = long.MinValue / 2;

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
        Relayout();
        if (!_timer.Enabled) Location = RestingPlace();
        Invalidate(true);
    }

    // ---- layout and painting ----

    private int S(int px) => TrayMenu.Scale(this, px);

    private void Relayout()
    {
        _f?.Dispose();
        _f = new CardFonts(DeviceDpi);
        var f = _f;
        int w = S(368), pad = S(24);

        _markBox = new Rectangle(pad, pad, S(40), S(40));
        int tx = _markBox.Right + S(14);
        int titleH = TextHeight(f.Title), subH = TextHeight(f.Body);
        int ty = _markBox.Top + (_markBox.Height - titleH - subH) / 2;
        _titleBox = new Rectangle(tx, ty, w - tx - S(48), titleH);
        _subtitleBox = new Rectangle(tx, ty + titleH, w - tx - pad, subH);
        int y = _markBox.Bottom + S(18);

        int inner = w - 2 * pad;
        _bodyBox = new Rectangle(pad, y, inner, TextRenderer.MeasureText(_m.Body, f.Body, new Size(inner, int.MaxValue), Wrap).Height);
        y = _bodyBox.Bottom + S(14);
        if (_m.Example is not null) { _panel = new Rectangle(pad, y, inner, S(68)); y = _panel.Bottom + S(16); }
        else { _panel = Rectangle.Empty; y += S(2); }

        _tipUndo = new Rectangle(pad, y, inner, S(32)); y += S(32);
        _tipPause = new Rectangle(pad, y, inner, S(32)); y += S(32);
        _tipHidden = new Rectangle(pad, y, inner, S(28)); y += S(28) + S(18);

        if (_action.Text != _m.Action && _action.Text.Length > 0) _actionChanged = Environment.TickCount64;
        _action.Text = _m.Action;
        _action.AccessibleName = _m.Action;
        _action.Font = f.Button;
        int bw = Math.Max(S(104), TextRenderer.MeasureText(_m.Action, f.Button).Width + S(48));
        _footer = new Rectangle(0, y, w, S(68));
        // The button's bounds include a 3 px rim for the keyboard focus ring.
        _action.Bounds = new Rectangle(w - pad - bw - S(3), _footer.Top + (_footer.Height - S(38)) / 2, bw + S(6), S(38));
        _close.Bounds = new Rectangle(w - S(12) - S(32), S(12), S(32), S(32));

        ClientSize = new Size(w, _footer.Bottom);
        AccessibleName = _m.Title;
        AccessibleDescription = $"{_m.Subtitle}. {_m.Body}";
    }

    private static int TextHeight(Font f) => TextRenderer.MeasureText("Ag", f, Size.Empty, TextFormatFlags.NoPadding).Height + 2;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var f = _f!;
        var t = _theme;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        bool dark = t == MenuTheme.Dark;

        using (var bmp = IconArt.Render(_markBox.Width, _m.Mark)) g.DrawImage(bmp, _markBox);
        TextRenderer.DrawText(g, _m.Title, f.Title, _titleBox, t.Text, Line);
        TextRenderer.DrawText(g, _m.Subtitle, f.Body, _subtitleBox, t.Secondary, Line);
        TextRenderer.DrawText(g, _m.Body, f.Body, _bodyBox, t.Text, Wrap);

        if (_m.Example is { } ex) PaintExample(g, ex.Typed, ex.Fixed, dark);

        PaintTip(g, _tipUndo, "arrow_undo", f.Body, t.Text, "Tap", new Keycap("Shift"), "twice to undo a fix");
        PaintTip(g, _tipPause, "pause", f.Body, t.Text, "Click", new TrayMark(), "in the tray to pause");
        PaintTip(g, _tipHidden, null, f.Caption, t.Secondary, "Icon hidden? Look under", new Keycap("^"), "next to the clock.");

        // Footer band, as in Windows 11 dialogs.
        using (var band = new SolidBrush(dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243)))
            g.FillRectangle(band, _footer);
        using (var rule = new Pen(t.Separator)) g.DrawLine(rule, _footer.Left, _footer.Top, _footer.Right, _footer.Top);

        if (!TrayMenu.FrameByDwm) // Windows 10: square corners and no DWM border
            using (var border = new Pen(t.Border)) g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    /// <summary>"akuo → שלום": what the keys typed, and what AutoLang turns it into.</summary>
    private void PaintExample(Graphics g, string typed, string fixedText, bool dark)
    {
        var f = _f!;
        var t = _theme;
        using (var path = Rounded(_panel, S(8)))
        using (var fill = new SolidBrush(dark ? Color.FromArgb(56, 56, 56) : Color.FromArgb(240, 240, 240)))
            g.FillPath(fill, path);

        const string arrow = "→";
        int gap = S(14), check = S(18);
        int wTyped = TextRenderer.MeasureText(g, typed, f.ExampleTyped, Size.Empty, Run).Width;
        int wArrow = TextRenderer.MeasureText(g, arrow, f.ExampleTyped, Size.Empty, Run).Width;
        int wFixed = TextRenderer.MeasureText(g, fixedText, f.ExampleFixed, Size.Empty, Run).Width;
        int total = wTyped + gap + wArrow + gap + wFixed + S(8) + check;
        int x = _panel.Left + (_panel.Width - total) / 2;
        int h = _panel.Height;
        var typedBox = new Rectangle(x, _panel.Top, wTyped, h);
        TextRenderer.DrawText(g, typed, f.ExampleTyped, typedBox, t.Secondary, Run);
        // Struck through: these are the letters that came out wrong.
        using (var strike = new Pen(Color.FromArgb(150, t.Secondary), Math.Max(1f, S(3) / 2f)))
        {
            float sy = _panel.Top + h / 2f + S(1);
            g.DrawLine(strike, typedBox.Left - S(2), sy, typedBox.Right + S(2), sy);
        }
        x += wTyped + gap;
        TextRenderer.DrawText(g, arrow, f.ExampleTyped, new Rectangle(x, _panel.Top, wArrow, h), t.Secondary, Run);
        x += wArrow + gap;
        TextRenderer.DrawText(g, fixedText, f.ExampleFixed, new Rectangle(x, _panel.Top, wFixed, h), t.Text, Run);
        x += wFixed + S(8);
        MenuIcons.Draw(g, "checkmark", new Rectangle(x, _panel.Top + (h - check) / 2, check, check), IconArt.Brand);
    }

    private sealed record Keycap(string Label);
    private sealed record TrayMark;

    /// <summary>One line of text with inline keycaps and the tray mark, after an optional icon.</summary>
    private void PaintTip(Graphics g, Rectangle row, string? icon, Font font, Color color, params object[] parts)
    {
        int iconSize = S(16), cy = row.Top + row.Height / 2;
        if (icon is not null) MenuIcons.Draw(g, icon, new Rectangle(row.Left, cy - iconSize / 2, iconSize, iconSize), _theme.Secondary);
        int x = row.Left + S(30), space = S(5);
        foreach (var part in parts)
        {
            switch (part)
            {
                case string s:
                    int w = TextRenderer.MeasureText(g, s, font, Size.Empty, Run).Width;
                    TextRenderer.DrawText(g, s, font, new Rectangle(x, row.Top, w + S(2), row.Height), color, Run);
                    x += w;
                    break;
                case Keycap k:
                    x += PaintKeycap(g, k.Label, x, cy);
                    break;
                case TrayMark:
                    int m = S(18);
                    using (var bmp = IconArt.Render(m, IconArt.Brand)) g.DrawImage(bmp, x, cy - m / 2, m, m);
                    x += m;
                    break;
            }
            x += space;
        }
    }

    /// <summary>A key as Windows draws it in its own tips: a light cap with a heavier bottom edge. "^" is the hidden-icons chevron.</summary>
    private int PaintKeycap(Graphics g, string label, int x, int cy)
    {
        var f = _f!;
        bool dark = _theme == MenuTheme.Dark;
        bool chevron = label == "^";
        int h = S(24);
        int w = chevron ? S(28) : Math.Max(S(28), TextRenderer.MeasureText(g, label, f.Key, Size.Empty, Run).Width + S(16));
        var r = new Rectangle(x, cy - h / 2, w, h);
        using (var edge = Rounded(r with { Y = r.Y + 1 }, S(5)))
        using (var edgeBrush = new SolidBrush(dark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(200, 200, 200)))
            g.FillPath(edgeBrush, edge);
        using (var cap = Rounded(r, S(5)))
        using (var fill = new SolidBrush(dark ? Color.FromArgb(66, 66, 66) : Color.White))
        using (var border = new Pen(dark ? Color.FromArgb(84, 84, 84) : Color.FromArgb(218, 218, 218)))
        {
            g.FillPath(fill, cap);
            g.DrawPath(border, cap);
        }
        if (chevron)
        {
            float cx = r.Left + r.Width / 2f, my = r.Top + r.Height / 2f, a = S(8) / 2f;
            using var pen = new Pen(_theme.Text, Math.Max(1.2f, S(3) / 2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[] { new PointF(cx - a, my + a / 2), new PointF(cx, my - a / 2), new PointF(cx + a, my + a / 2) });
        }
        else TextRenderer.DrawText(g, label, f.Key, r, _theme.Text, Run | TextFormatFlags.HorizontalCenter);
        return w;
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

    // ---- input, theme, lifetime ----

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape) { Dismiss(); return true; }
        if (keyData == Keys.Enter) { Primary(); return true; }
        return base.ProcessDialogKey(keyData);
    }

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
        if (disposing) { _timer.Dispose(); _f?.Dispose(); }
        base.Dispose(disposing);
    }

    /// <summary>Respects "Animation effects" in Windows settings.</summary>
    private static bool Animations() => !SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION, 0, out int on, 0) || on != 0;

    private const int CS_DROPSHADOW = 0x20000, WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8;
    private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);

    // ---- the example ----

    /// <summary>"Hello" in the alphabets we ship word lists for, keyed by ISO 639-1 code.</summary>
    private static readonly Dictionary<string, string> Hello = new()
    {
        ["he"] = "שלום", ["ru"] = "привет", ["uk"] = "привіт", ["bg"] = "здравей", ["el"] = "γεια",
        ["ar"] = "مرحبا", ["fa"] = "سلام",
    };

    /// <summary>
    /// A real example for this user's keyboards: a word in their other alphabet as it comes out with the Latin layout
    /// on ("akuo" for שלום), or else "hello" as it comes out on the other layout. Null when there is no clean pair.
    /// </summary>
    public static (string Typed, string Fixed)? Example(IEnumerable<KeyboardLayoutTable> tables)
    {
        var list = tables.ToList();
        var latin = list.Where(t => t.Script == "Latin").OrderByDescending(t => LayoutProbe.IsoCode(t.LangId) == "en").FirstOrDefault();
        if (latin is null) return null;
        var others = list.Where(t => t.Script != "Latin" && t.Script != Scripts.Unknown).ToList();
        foreach (var other in others)
        {
            if (!Hello.TryGetValue(LayoutProbe.IsoCode(other.LangId), out var word)) continue;
            var typed = other.TransliterateTo(latin, word);
            if (typed != word && typed.All(c => char.IsLetter(c) && latin.Letters.Contains(c))) return (typed, word);
        }
        if (LayoutProbe.IsoCode(latin.LangId) == "en")
            foreach (var other in others)
            {
                var typed = latin.TransliterateTo(other, "hello");
                if (typed.All(c => char.IsLetter(c) && other.Letters.Contains(c))) return (typed, "hello");
            }
        return null;
    }
}

/// <summary>The card's type ramp (Windows 11: subtitle 20, body 14, caption 12), in pixels at the window's DPI.</summary>
internal sealed class CardFonts : IDisposable
{
    public readonly Font Title, Body, Caption, Button, Key, ExampleTyped, ExampleFixed;

    public CardFonts(int dpi)
    {
        float k = dpi / 96f;
        Font F(string face, float px) => new(face, px * k, FontStyle.Regular, GraphicsUnit.Pixel);
        Title = F(DisplaySemibold, 20);
        Body = F(TextRegular, 14);
        Caption = F(TextRegular, 12);
        Button = F(TextSemibold, 14);
        Key = F(TextSemibold, 12);
        ExampleTyped = F(DisplayRegular, 22);
        ExampleFixed = F(DisplaySemibold, 22);
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
        foreach (var f in new[] { Title, Body, Caption, Button, Key, ExampleTyped, ExampleFixed }) f.Dispose();
    }
}

/// <summary>The card's two buttons: the accent action ("Got it", "Resume") and the close glyph.</summary>
internal sealed class CardButton : Control
{
    private readonly WelcomeCard _card;
    private readonly bool _accent;
    private bool _hover, _pressed;

    public string? Icon { get; init; }

    public CardButton(WelcomeCard card, bool accent)
    {
        _card = card;
        _accent = accent;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.SupportsTransparentBackColor, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
    }

    // White text on the brand green is too faint for 14 px; this darker green keeps 4.5:1.
    private static readonly Color Accent = Color.FromArgb(31, 132, 52), AccentHover = Color.FromArgb(27, 120, 47), AccentPressed = Color.FromArgb(23, 104, 41);

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) { _pressed = true; Invalidate(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && _pressed) { _pressed = false; Invalidate(); OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var theme = _card.Theme;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = DeviceDpi / 96f;
        var face = RectangleF.Inflate(ClientRectangle, -3 * s, -3 * s);

        if (_accent)
        {
            // Parent is the card; the area around the face shows the footer band, so paint it first.
            using (var back = new SolidBrush(theme == MenuTheme.Dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243)))
                g.FillRectangle(back, ClientRectangle);
            using (var path = WelcomeCard.Rounded(face, 4 * s))
            using (var fill = new SolidBrush(_pressed ? AccentPressed : _hover ? AccentHover : Accent))
                g.FillPath(fill, path);
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(face), _pressed ? Color.FromArgb(220, 255, 255, 255) : Color.White,
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else
        {
            using (var back = new SolidBrush(theme.Back)) g.FillRectangle(back, ClientRectangle);
            if (_hover || _pressed)
                using (var path = WelcomeCard.Rounded(face, 4 * s))
                using (var fill = new SolidBrush(_pressed ? theme.Separator : theme.Hover))
                    g.FillPath(fill, path);
            int icon = (int)Math.Round(12 * s);
            if (Icon is not null)
                MenuIcons.Draw(g, Icon, new Rectangle((Width - icon) / 2, (Height - icon) / 2, icon, icon), _hover ? theme.Text : theme.Secondary);
        }

        if (Focused && ShowFocusCues) // Windows 11 focus visual: a 2 px ring in the text color around the face
            using (var ring = WelcomeCard.Rounded(RectangleF.Inflate(face, 2 * s, 2 * s), 6 * s))
            using (var pen = new Pen(theme.Text, 2 * s))
                g.DrawPath(pen, ring);
    }
}
