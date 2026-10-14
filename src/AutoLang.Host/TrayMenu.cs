using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoLang.Host;

/// <summary>Menu colors for the taskbar's light or dark mode, close to the Windows 11 shell menus.</summary>
internal sealed record MenuTheme(Color Back, Color Hover, Color Text, Color Secondary, Color Disabled, Color Separator, Color Border, Color ToggleOn)
{
    public static readonly MenuTheme Light = new(
        Color.FromArgb(249, 249, 249), Color.FromArgb(234, 234, 234), Color.FromArgb(27, 27, 27), Color.FromArgb(96, 96, 96),
        Color.FromArgb(160, 160, 160), Color.FromArgb(224, 224, 224), Color.FromArgb(214, 214, 214), IconArt.Brand);

    public static readonly MenuTheme Dark = new(
        Color.FromArgb(44, 44, 44), Color.FromArgb(61, 61, 61), Color.FromArgb(255, 255, 255), Color.FromArgb(170, 170, 170),
        Color.FromArgb(120, 120, 120), Color.FromArgb(68, 68, 68), Color.FromArgb(72, 72, 72), IconArt.Brand);

    /// <summary>The tray sits on the taskbar, so it follows the Windows mode ("SystemUsesLightTheme"), not the app mode.</summary>
    public static MenuTheme Current()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("SystemUsesLightTheme") is int light && light == 0 ? Dark : Light;
        }
        catch { return Light; }
    }
}

/// <summary>Styling for the tray's <see cref="ContextMenuStrip"/> and its submenus.</summary>
internal static class TrayMenu
{
    private static readonly bool Win11 = Environment.OSVersion.Version.Build >= 22000;

    public static int Scale(Control? c, int px) => (int)Math.Round(px * (c?.DeviceDpi ?? 96) / 96.0);

    public static ContextMenuStrip Create()
    {
        var menu = new ContextMenuStrip { ShowCheckMargin = false, ShowImageMargin = true, DropShadowEnabled = true };
        Style(menu);
        menu.Opening += (_, _) => ApplyTheme(menu, MenuTheme.Current());
        return menu;
    }

    /// <summary>Gives a dropdown the renderer and (on Windows 11) rounded corners. Submenus need it too.</summary>
    public static void Style(ToolStripDropDownMenu dd)
    {
        dd.Renderer = new TrayMenuRenderer(MenuTheme.Current());
        dd.HandleCreated += (_, _) => ApplyFrame(dd);
    }

    /// <summary>Re-reads the theme each time the menu opens, so a switch in Windows settings shows up without a restart.</summary>
    private static void ApplyTheme(ToolStripDropDownMenu dd, MenuTheme theme)
    {
        if (dd.Renderer is TrayMenuRenderer r && r.Theme != theme)
        {
            r.Theme = theme;
            if (dd.IsHandleCreated) ApplyFrame(dd);
            dd.Invalidate();
        }
        foreach (var item in dd.Items.OfType<ToolStripMenuItem>())
            if (item.HasDropDown && item.DropDown is ToolStripDropDownMenu sub) ApplyTheme(sub, theme);
    }

    private static void ApplyFrame(ToolStripDropDown dd)
    {
        if (dd.Renderer is TrayMenuRenderer r) ApplyFrame(dd.Handle, r.Theme);
    }

    /// <summary>Rounded corners and a theme border drawn by DWM (Windows 11; no-op before). Also used by <see cref="WelcomeCard"/>.</summary>
    internal static void ApplyFrame(IntPtr hwnd, MenuTheme theme)
    {
        if (!Win11) return;
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        int border = ColorTranslator.ToWin32(theme.Border);
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    /// <summary>Windows 11 draws the rounded frame and border; Windows 10 keeps square corners and our own border.</summary>
    internal static bool FrameByDwm => Win11;

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34, DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}

/// <summary>A menu row with a Fluent icon (<see cref="MenuIcons"/>) and, for on/off settings, a switch instead of a check mark.</summary>
internal class TrayMenuItem : ToolStripMenuItem
{
    // Any image makes the item lay out an icon slot; the renderer draws the vector icon into it.
    private static readonly Bitmap Slot = new(1, 1);
    // The dropdown sizes itself from the text and shortcut columns only, so a switch reserves its room as a blank
    // shortcut (figure spaces, which the renderer skips) instead of through GetPreferredSize.
    internal const string SwitchSpace = "       ";
    private string? _icon;
    private readonly bool _toggle;

    public TrayMenuItem(string text, string? icon = null, Action? onClick = null) : base(text)
    {
        Icon = icon;
        if (onClick != null) Click += (_, _) => onClick();
    }

    public string? Icon
    {
        get => _icon;
        set { _icon = value; Image = value == null ? null : Slot; Invalidate(); }
    }

    /// <summary>Shows <see cref="ToolStripMenuItem.Checked"/> as a switch on the right.</summary>
    public bool Toggle
    {
        get => _toggle;
        init { _toggle = value; if (value) ShortcutKeyDisplayString = SwitchSpace; }
    }

    public override Size GetPreferredSize(Size constrainingSize)
    {
        var s = base.GetPreferredSize(constrainingSize);
        return new Size(s.Width, Math.Max(s.Height, TrayMenu.Scale(Owner, 32)));
    }
}

/// <summary>A thin rule with some air around it.</summary>
internal sealed class TrayMenuSeparator : ToolStripSeparator
{
    public override Size GetPreferredSize(Size constrainingSize)
        => new(base.GetPreferredSize(constrainingSize).Width, TrayMenu.Scale(Owner, 9));
}

/// <summary>The top of the menu: the AutoLang mark in the state color, the state, and the active layouts.</summary>
/// <remarks>A menu item so the dropdown lays it out full width and counts its <see cref="ToolStripItem.Text"/> (the
/// longer line) when sizing itself; it paints itself entirely.</remarks>
internal sealed class TrayMenuHeader : ToolStripMenuItem
{
    private string _title = "", _subtitle = "";
    private Color _mark = IconArt.Brand;

    public TrayMenuHeader() { Enabled = false; }

    public string Title { get => _title; set { _title = value; Relabel(); } }
    public string Subtitle { get => _subtitle; set { _subtitle = value; Relabel(); } }
    public Color Mark { get => _mark; set { _mark = value; Invalidate(); } }

    private int S(int px) => TrayMenu.Scale(Owner, px);

    private Font TitleFont() => new("Segoe UI Semibold", Font.Size, FontStyle.Regular, Font.Unit);

    private void Relabel()
    {
        AccessibleName = $"{_title}. {_subtitle}";
        // The header's text starts a little right of the item text column; the em spaces make up for it.
        using var bold = TitleFont();
        Text = (TextRenderer.MeasureText(_title, bold).Width > TextRenderer.MeasureText(_subtitle, Font).Width ? _title : _subtitle)
            + "  ";
        Invalidate();
    }

    public override Size GetPreferredSize(Size constrainingSize)
        => new(base.GetPreferredSize(constrainingSize).Width, S(56));

    protected override void OnPaint(PaintEventArgs e)
    {
        var theme = (Owner?.Renderer as TrayMenuRenderer)?.Theme ?? MenuTheme.Light;
        var g = e.Graphics;
        int mark = S(28);
        using (var bmp = IconArt.Render(mark, _mark))
            g.DrawImage(bmp, S(12), (Height - mark) / 2, mark, mark);
        int x = S(12) + mark + S(12);
        using var bold = TitleFont();
        int line = TextRenderer.MeasureText("Ag", bold).Height;
        int y = (Height - 2 * line) / 2;
        const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, _title, bold, new Rectangle(x, y, Width - x - S(8), line), theme.Text, flags);
        TextRenderer.DrawText(g, _subtitle, Font, new Rectangle(x, y + line, Width - x - S(8), line), theme.Secondary, flags);
    }
}

/// <summary>Flat Windows 11-style drawing: rounded hover pill, no image margin band, thin rules, switches for toggles.</summary>
internal sealed class TrayMenuRenderer(MenuTheme theme) : ToolStripRenderer
{
    public MenuTheme Theme { get; set; } = theme;

    private static int S(ToolStrip? ts, int px) => TrayMenu.Scale(ts, px);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Theme.Back);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (TrayMenu.FrameByDwm) return;
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var g = e.Graphics;
        if (item.Enabled && (item.Selected || item.Pressed))
        {
            var r = new Rectangle(S(e.ToolStrip, 4), 1, item.Width - S(e.ToolStrip, 8), item.Height - 2);
            using var path = Rounded(r, S(e.ToolStrip, 4));
            using var b = new SolidBrush(Theme.Hover);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillPath(b, path);
        }
        if (item is TrayMenuItem { Toggle: true } t) DrawSwitch(g, t, e.ToolStrip);
    }

    /// <summary>The Windows 11 toggle: filled with a white knob on the right when on, outlined with the knob on the left when off.</summary>
    private void DrawSwitch(Graphics g, TrayMenuItem item, ToolStrip? ts)
    {
        int w = S(ts, 36), h = S(ts, 18);
        var r = new RectangleF(item.Width - S(ts, 16) - w, (item.Height - h) / 2f, w, h);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = Rounded(r, h / 2f);
        if (item.Checked)
        {
            using var on = new SolidBrush(item.Enabled ? Theme.ToggleOn : Theme.Disabled);
            g.FillPath(on, track);
            float d = h - S(ts, 8);
            using var knob = new SolidBrush(Color.White);
            g.FillEllipse(knob, r.Right - S(ts, 4) - d, r.Y + (h - d) / 2, d, d);
        }
        else
        {
            var line = item.Enabled ? Theme.Secondary : Theme.Disabled;
            float px = Math.Max(1, S(ts, 1));
            using var outline = Rounded(RectangleF.Inflate(r, -px / 2, -px / 2), (h - px) / 2);
            using var pen = new Pen(line, px);
            g.DrawPath(pen, outline);
            float d = h - S(ts, 10);
            using var knob = new SolidBrush(line);
            g.FillEllipse(knob, r.X + S(ts, 5), r.Y + (h - d) / 2, d, d);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Text == TrayMenuItem.SwitchSpace) return;
        bool shortcut = e.Item is ToolStripMenuItem m && e.Text == m.ShortcutKeyDisplayString && e.Text != m.Text;
        var color = !e.Item.Enabled ? Theme.Disabled : shortcut ? Theme.Secondary : Theme.Text;
        // The layout keeps the text box at its default height at the top of the taller row; center it in the row.
        var r = new Rectangle(e.TextRectangle.X, 0, e.TextRectangle.Width, e.Item.Height);
        var flags = (e.TextFormat & ~TextFormatFlags.Bottom) | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, r, color, flags);
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Item is TrayMenuItem { Icon: { } icon } item)
            MenuIcons.Draw(e.Graphics, icon, e.ImageRectangle, item.Enabled ? Theme.Text : Theme.Disabled);
        else base.OnRenderItemImage(e);
    }

    /// <summary>A plain check mark for radio-style choices; items with an icon show their state as a switch instead.</summary>
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Item is TrayMenuItem { Icon: not null } || e.Item is TrayMenuItem { Toggle: true }) return;
        MenuIcons.Draw(e.Graphics, "checkmark", e.ImageRectangle, e.Item.Enabled ? Theme.Text : Theme.Disabled);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var r = e.ArrowRectangle;
        float s = S(e.Item?.Owner, 3), cx = r.X + r.Width / 2f, cy = e.Item is { } it ? it.Height / 2f : r.Y + r.Height / 2f;
        using var pen = new Pen(e.Item?.Enabled == false ? Theme.Disabled : Theme.Secondary, Math.Max(1, S(e.Item?.Owner, 1)))
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen, new[] { new PointF(cx - s / 2, cy - s), new PointF(cx + s / 2, cy), new PointF(cx - s / 2, cy + s) });
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(Theme.Separator);
        e.Graphics.DrawLine(pen, S(e.ToolStrip, 4), y, e.Item.Width - S(e.ToolStrip, 4), y);
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
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
}
