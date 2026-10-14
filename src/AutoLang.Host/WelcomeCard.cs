using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AutoLang.Engine;

namespace AutoLang.Host;

/// <summary>What the welcome card says. The tray builds it from its own state and rebuilds it on every change.</summary>
internal sealed record WelcomeModel(string Title, string Subtitle, string Body, Color Mark, string Action, (string Typed, string Fixed)? Example);

/// <summary>
/// The first-run card, also shown when AutoLang is launched while it is already running. AutoLang has no window, so
/// without it nothing says that it started, what it does, or where it went: an example built from the user's own
/// keyboards, the two things worth knowing (Shift twice, click to pause) and where Windows hides new tray icons.
/// Painted by hand; only the two buttons are controls, for focus, keyboard and screen readers.
/// </summary>
internal sealed class WelcomeCard : TrayFlyout
{
    private readonly Func<WelcomeModel> _model;
    private readonly Action _primary;
    private readonly bool _activate;
    private readonly CardButton _action, _close;
    private WelcomeModel _m;
    private long _actionChanged = long.MinValue / 2;

    // Layout in client pixels at the current DPI (Relayout).
    private Rectangle _markBox, _titleBox, _subtitleBox, _bodyBox, _panel, _tipUndo, _tipPause, _tipHidden, _footer;

    private const TextFormatFlags Line = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
    private const TextFormatFlags Wrap = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
    private const TextFormatFlags Run = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;

    public WelcomeCard(Func<WelcomeModel> model, Action primary, bool activate) : base(activate)
    {
        _model = model;
        _primary = primary;
        _activate = activate;
        _m = model();
        KeyPreview = true;

        _action = new CardButton(this, accent: true);
        _action.Click += (_, _) => Primary();
        _close = new CardButton(this, accent: false) { Icon = "dismiss", AccessibleName = UiText.Current.Close };
        _close.Click += (_, _) => Dismiss();
        Controls.Add(_action);
        Controls.Add(_close);
    }

    /// <summary>Rereads the model (pause, word lists arriving) and keeps the card anchored at the tray.</summary>
    public void Reload()
    {
        if (IsDisposed || IsClosing) return;
        _m = _model();
        Reflow();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_activate) _action.Focus();
    }

    /// <summary>
    /// "Resume" turns into "Got it" under the pointer: the second half of a double-click must not close the card the
    /// user just brought back to life.
    /// </summary>
    private void Primary()
    {
        if (Environment.TickCount64 - _actionChanged < SystemInformation.DoubleClickTime) return;
        _primary();
    }

    // ---- layout and painting ----

    protected override void Relayout()
    {
        var f = Fonts;
        // Laid out left to right; the boxes off the center line are mirrored for a right-to-left language.
        int w = CardWidth(), pad = S(24);

        var markBox = new Rectangle(pad, pad, S(40), S(40));
        int tx = markBox.Right + S(14);
        int titleH = TextHeight(f.Title), subH = TextHeight(f.Body);
        int ty = markBox.Top + (markBox.Height - titleH - subH) / 2;
        _markBox = Mirror(markBox, w);
        _titleBox = Mirror(new Rectangle(tx, ty, w - tx - S(48), titleH), w);
        _subtitleBox = Mirror(new Rectangle(tx, ty + titleH, w - tx - pad, subH), w);
        int y = markBox.Bottom + S(18);

        int inner = w - 2 * pad;
        _bodyBox = new Rectangle(pad, y, inner, TextRenderer.MeasureText(_m.Body, f.Body, new Size(inner, int.MaxValue), Wrap | Dir).Height);
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
        _action.Bounds = Mirror(new Rectangle(w - pad - bw - S(3), _footer.Top + (_footer.Height - S(38)) / 2, bw + S(6), S(38)), w);
        _close.Bounds = Mirror(new Rectangle(w - S(12) - S(32), S(12), S(32), S(32)), w);

        ClientSize = new Size(w, _footer.Bottom);
        AccessibleName = _m.Title;
        AccessibleDescription = $"{_m.Subtitle}. {_m.Body}";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var f = Fonts;
        var t = Theme;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        bool dark = t == MenuTheme.Dark;

        using (var bmp = IconArt.Render(_markBox.Width, _m.Mark)) g.DrawImage(bmp, _markBox);
        TextRenderer.DrawText(g, _m.Title, f.Title, _titleBox, t.Text, Line | Dir);
        TextRenderer.DrawText(g, _m.Subtitle, f.Body, _subtitleBox, t.Secondary, Line | Dir);
        TextRenderer.DrawText(g, _m.Body, f.Body, _bodyBox, t.Text, Wrap | Dir);

        if (_m.Example is { } ex) PaintExample(g, ex.Typed, ex.Fixed, dark);

        PaintTip(g, _tipUndo, "arrow_undo", f.Body, t.Text, UndoTip);
        PaintTip(g, _tipPause, "pause", f.Body, t.Text, PauseTip);
        PaintTip(g, _tipHidden, null, f.Caption, t.Secondary, HiddenTip);

        // Footer band, as in Windows 11 dialogs.
        using (var band = new SolidBrush(dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243)))
            g.FillRectangle(band, _footer);
        using (var rule = new Pen(t.Separator)) g.DrawLine(rule, _footer.Left, _footer.Top, _footer.Right, _footer.Top);

        PaintBorder(g);
    }

    /// <summary>"akuo → שלום": what the keys typed, and what AutoLang turns it into.</summary>
    private void PaintExample(Graphics g, string typed, string fixedText, bool dark)
    {
        var f = Fonts;
        var t = Theme;
        using (var path = Rounded(_panel, S(8)))
        using (var fill = new SolidBrush(dark ? Color.FromArgb(56, 56, 56) : Color.FromArgb(240, 240, 240)))
            g.FillPath(fill, path);

        string arrow = Rtl ? "←" : "→";
        // Laid out left to right, then mirrored in the panel: the typed word comes first in reading order.
        Rectangle M(Rectangle r) => Rtl ? r with { X = _panel.Left + _panel.Right - r.Right } : r;
        int gap = S(14), check = S(18);
        int wTyped = TextRenderer.MeasureText(g, typed, f.ExampleTyped, Size.Empty, Run).Width;
        int wArrow = TextRenderer.MeasureText(g, arrow, f.ExampleTyped, Size.Empty, Run).Width;
        int wFixed = TextRenderer.MeasureText(g, fixedText, f.ExampleFixed, Size.Empty, Run).Width;
        int total = wTyped + gap + wArrow + gap + wFixed + S(8) + check;
        int x = _panel.Left + (_panel.Width - total) / 2;
        int h = _panel.Height;
        var typedBox = M(new Rectangle(x, _panel.Top, wTyped, h));
        TextRenderer.DrawText(g, typed, f.ExampleTyped, typedBox, t.Secondary, Run);
        // Struck through: these are the letters that came out wrong.
        using (var strike = new Pen(Color.FromArgb(150, t.Secondary), Math.Max(1f, S(3) / 2f)))
        {
            float sy = _panel.Top + h / 2f + S(1);
            g.DrawLine(strike, typedBox.Left - S(2), sy, typedBox.Right + S(2), sy);
        }
        x += wTyped + gap;
        TextRenderer.DrawText(g, arrow, f.ExampleTyped, M(new Rectangle(x, _panel.Top, wArrow, h)), t.Secondary, Run);
        x += wArrow + gap;
        TextRenderer.DrawText(g, fixedText, f.ExampleFixed, M(new Rectangle(x, _panel.Top, wFixed, h)), t.Text, Run);
        x += wFixed + S(8);
        MenuIcons.Draw(g, "checkmark", M(new Rectangle(x, _panel.Top + (h - check) / 2, check, check)), IconArt.Brand);
    }

    private sealed record Keycap(string Label);
    private sealed record TrayMark;

    private static object[] UndoTip => Tip(UiText.Current.TipUndo, new Keycap(UiText.Current.ShiftKey));
    private static object[] PauseTip => Tip(UiText.Current.TipPause, new TrayMark());
    private static object[] HiddenTip => Tip(UiText.Current.TipHidden, new Keycap("^"));

    /// <summary>A tip's sentence split around its "{0}", which is drawn as a keycap or the tray mark.</summary>
    private static object[] Tip(string format, object mark)
    {
        int i = format.IndexOf("{0}", StringComparison.Ordinal);
        if (i < 0) return new object[] { format };
        var parts = new List<object>();
        if (format[..i].Trim() is { Length: > 0 } before) parts.Add(before);
        parts.Add(mark);
        if (format[(i + 3)..].Trim() is { Length: > 0 } after) parts.Add(after);
        return parts.ToArray();
    }

    private static int PartWidth(object part, Font font, CardFonts f, Func<int, int> S) => part switch
    {
        string s => TextRenderer.MeasureText(s, font, Size.Empty, Run).Width,
        Keycap k => k.Label == "^" ? S(28) : Math.Max(S(28), TextRenderer.MeasureText(k.Label, f.Key, Size.Empty, Run).Width + S(16)),
        _ => S(18),
    };

    /// <summary>The icon column, then the parts with a space between.</summary>
    private static int TipWidth(object[] parts, Font font, CardFonts f, Func<int, int> S)
        => S(30) + parts.Sum(p => PartWidth(p, font, f, S)) + S(5) * (parts.Length - 1);

    /// <summary>What the longest title, subtitle and tip need in the UI language (see <see cref="TrayFlyout.CardWidth"/>).</summary>
    internal static int NeededWidth(CardFonts f, Func<int, int> S)
    {
        var t = UiText.Current;
        int pad = S(24), tx = pad + S(40) + S(14);
        int need = new[] { t.PausedTitle, t.ReadyingTitle, t.IdleTitle, t.OnTitle }.Max(s => tx + Measure(s, f.Title) + S(48));
        need = Math.Max(need, new[] { t.WelcomePausedSubtitle, t.DownloadingWordLists, t.NeedsTwoAlphabets, t.WelcomeOnSubtitle }
            .Max(s => tx + Measure(s, f.Body) + pad));
        need = Math.Max(need, 2 * pad + TipWidth(UndoTip, f.Body, f, S));
        need = Math.Max(need, 2 * pad + TipWidth(PauseTip, f.Body, f, S));
        return Math.Max(need, 2 * pad + TipWidth(HiddenTip, f.Caption, f, S));
    }

    /// <summary>One line of text with inline keycaps and the tray mark, after an optional icon; mirrored right to left.</summary>
    private void PaintTip(Graphics g, Rectangle row, string? icon, Font font, Color color, object[] parts)
    {
        Rectangle M(Rectangle r) => Rtl ? r with { X = row.Left + row.Right - r.Right } : r;
        int iconSize = S(16), cy = row.Top + row.Height / 2;
        if (icon is not null) MenuIcons.Draw(g, icon, M(new Rectangle(row.Left, cy - iconSize / 2, iconSize, iconSize)), Theme.Secondary);
        int x = row.Left + S(30), space = S(5);
        foreach (var part in parts)
        {
            int w = PartWidth(part, font, Fonts, S);
            switch (part)
            {
                case string s:
                    TextRenderer.DrawText(g, s, font, M(new Rectangle(x, row.Top, w + S(2), row.Height)), color, Run | Dir);
                    break;
                case Keycap k:
                    PaintKeycap(g, k.Label, M(new Rectangle(x, cy - S(12), w, S(24))));
                    break;
                case TrayMark:
                    using (var bmp = IconArt.Render(w, IconArt.Brand)) g.DrawImage(bmp, M(new Rectangle(x, cy - w / 2, w, w)));
                    break;
            }
            x += w + space;
        }
    }

    /// <summary>A key as Windows draws it in its own tips: a light cap with a heavier bottom edge. "^" is the hidden-icons chevron.</summary>
    private void PaintKeycap(Graphics g, string label, Rectangle r)
    {
        var f = Fonts;
        var theme = Theme;
        bool dark = theme == MenuTheme.Dark;
        bool chevron = label == "^";
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
            using var pen = new Pen(theme.Text, Math.Max(1.2f, S(3) / 2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[] { new PointF(cx - a, my + a / 2), new PointF(cx, my - a / 2), new PointF(cx + a, my + a / 2) });
        }
        else TextRenderer.DrawText(g, label, f.Key, r, theme.Text, Run | TextFormatFlags.HorizontalCenter);
    }

    // ---- input ----

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape) { Dismiss(); return true; }
        if (keyData == Keys.Enter) { Primary(); return true; }
        return base.ProcessDialogKey(keyData);
    }

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

/// <summary>The card's two buttons: the accent action ("Got it", "Resume") and the close glyph.</summary>
internal sealed class CardButton : Control
{
    private readonly TrayFlyout _card;
    private readonly bool _accent;
    private bool _hover, _pressed;

    public string? Icon { get; init; }

    public CardButton(TrayFlyout card, bool accent)
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
            using (var path = TrayFlyout.Rounded(face, 4 * s))
            using (var fill = new SolidBrush(_pressed ? AccentPressed : _hover ? AccentHover : Accent))
                g.FillPath(fill, path);
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(face), _pressed ? Color.FromArgb(220, 255, 255, 255) : Color.White,
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else
        {
            using (var back = new SolidBrush(theme.Back)) g.FillRectangle(back, ClientRectangle);
            if (_hover || _pressed)
                using (var path = TrayFlyout.Rounded(face, 4 * s))
                using (var fill = new SolidBrush(_pressed ? theme.Separator : theme.Hover))
                    g.FillPath(fill, path);
            int icon = (int)Math.Round(12 * s);
            if (Icon is not null)
                MenuIcons.Draw(g, Icon, new Rectangle((Width - icon) / 2, (Height - icon) / 2, icon, icon), _hover ? theme.Text : theme.Secondary);
        }

        if (Focused && ShowFocusCues) // Windows 11 focus visual: a 2 px ring in the text color around the face
            using (var ring = TrayFlyout.Rounded(RectangleF.Inflate(face, 2 * s, 2 * s), 6 * s))
            using (var pen = new Pen(theme.Text, 2 * s))
                g.DrawPath(pen, ring);
    }
}
