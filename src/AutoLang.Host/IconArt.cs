using System.Drawing;
using System.Drawing.Drawing2D;

namespace AutoLang.Host;

/// <summary>
/// The AutoLang mark: two overlapping cards in the style of the Google Translate icon, a white card with a Hebrew Aleph
/// behind and a card in the state color with a Latin A in front. Drawn on a 16-unit grid at the target size so small
/// sizes stay crisp instead of being downscaled. Shared by the tray (live state colors) and tools/AutoLang.Assets
/// (the exe icon and the MSIX tiles, in <see cref="Brand"/>).
/// </summary>
public static class IconArt
{
    public static readonly Color Brand = Color.FromArgb(46, 160, 67);   // also "watching"
    public static readonly Color Paused = Color.FromArgb(140, 140, 140);
    public static readonly Color Blocked = Color.FromArgb(230, 140, 20);

    public static Bitmap Render(int size, Color fill)
    {
        int s = size;
        float u = s / 16f;
        // At tray sizes a 1 px outline on half-pixel edges is sharpest; larger renders scale the stroke with the grid.
        float px = s <= 32 ? 1f : 0.6f * u;
        var bmp = new Bitmap(s, s);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Back card, top-right: white with a slate outline so it reads on both light and dark taskbars.
        using (var p = RoundRect(new RectangleF(6.5f * u + px / 2, px / 2, 9.5f * u - px, 9.5f * u - px), 2f * u))
        using (var white = new SolidBrush(Color.White))
        using (var outline = new Pen(Color.FromArgb(95, 99, 104), px))
        {
            g.FillPath(white, p);
            g.DrawPath(outline, p);
        }
        using (var dark = new SolidBrush(Color.FromArgb(50, 54, 57)))
            FillGlyph(g, "Arial", "א", new RectangleF(9.9f * u, 1.5f * u, 4.5f * u, 4.5f * u), dark); // Arial ships Hebrew everywhere

        // Front card, bottom-left, cut out of the back card with a transparent gap.
        var front = new RectangleF(0, 6.5f * u, 9.5f * u, 9.5f * u);
        g.CompositingMode = CompositingMode.SourceCopy;
        using (var p = RoundRect(RectangleF.Inflate(front, 1.1f * u, 1.1f * u), 3f * u))
        using (var clear = new SolidBrush(Color.Transparent))
            g.FillPath(clear, p);
        g.CompositingMode = CompositingMode.SourceOver;
        using (var p = RoundRect(front, 2f * u))
        using (var b = new SolidBrush(fill))
            g.FillPath(b, p);
        using (var w = new SolidBrush(Color.White))
            FillGlyph(g, "Segoe UI", "A", new RectangleF(1.9f * u, 8.4f * u, 5.7f * u, 5.7f * u), w);
        return bmp;
    }

    private static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Fills a bold glyph scaled to fit and centered on its ink bounds (DrawString pads unevenly at small sizes).</summary>
    private static void FillGlyph(Graphics g, string font, string text, RectangleF box, Brush brush)
    {
        using var path = new GraphicsPath();
        using var family = new FontFamily(font);
        path.AddString(text, family, (int)FontStyle.Bold, 100, PointF.Empty, StringFormat.GenericTypographic);
        var ink = path.GetBounds();
        float k = Math.Min(box.Width / ink.Width, box.Height / ink.Height);
        using var m = new Matrix();
        m.Translate(box.X + box.Width / 2, box.Y + box.Height / 2);
        m.Scale(k, k);
        m.Translate(-(ink.X + ink.Width / 2), -(ink.Y + ink.Height / 2));
        path.Transform(m);
        g.FillPath(brush, path);
    }
}
