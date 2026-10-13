using System.Drawing;
using System.Drawing.Imaging;
using AutoLang.Host;

// Writes, relative to the repo root (found by walking up to AutoLang.sln):
//   src/AutoLang.Host/AutoLang.ico      exe / Start-menu icon, PNG entries 16..256
//   packaging/msix/Assets/*.png         MSIX logos, each rendered at its own size (no downscaling)
var root = AppContext.BaseDirectory;
while (root is not null && !File.Exists(Path.Combine(root, "AutoLang.sln"))) root = Path.GetDirectoryName(root);
if (root is null) { Console.Error.WriteLine("AutoLang.sln not found above " + AppContext.BaseDirectory); return 1; }

var ico = Path.Combine(root, "src", "AutoLang.Host", "AutoLang.ico");
WriteIco(ico, new[] { 16, 20, 24, 32, 40, 48, 64, 256 });
Console.WriteLine(ico);

var assets = Path.Combine(root, "packaging", "msix", "Assets");
Directory.CreateDirectory(assets);

// Square logos: the mark fills the plate, like the inbox apps. Scale variants are what the shell asks for by DPI.
foreach (var (name, size) in new[] { ("StoreLogo", 50), ("Square150x150Logo", 150), ("Square44x44Logo", 44), ("Square310x310Logo", 310), ("Square71x71Logo", 71) })
{
    Save(Path.Combine(assets, $"{name}.png"), Mark(size, size, 0.08f));
    foreach (var scale in new[] { 200, 400 })
        Save(Path.Combine(assets, $"{name}.scale-{scale}.png"), Mark(size * scale / 100, size * scale / 100, 0.08f));
}
// Taskbar / Start list icon at exact pixel sizes; "unplated" = drawn without the accent-color plate.
foreach (var t in new[] { 16, 24, 32, 48, 256 })
{
    Save(Path.Combine(assets, $"Square44x44Logo.targetsize-{t}.png"), Mark(t, t, 0));
    Save(Path.Combine(assets, $"Square44x44Logo.targetsize-{t}_altform-unplated.png"), Mark(t, t, 0));
    Save(Path.Combine(assets, $"Square44x44Logo.targetsize-{t}_altform-lightunplated.png"), Mark(t, t, 0));
}
Save(Path.Combine(assets, "Wide310x150Logo.png"), Mark(310, 150, 0.12f));
Save(Path.Combine(assets, "Wide310x150Logo.scale-200.png"), Mark(620, 300, 0.12f));
// Store listing tile (uploaded in Partner Center, not packaged).
Save(Path.Combine(root, "packaging", "store-300x300.png"), Mark(300, 300, 0.1f));
Console.WriteLine(assets);
return 0;

static Bitmap Mark(int w, int h, float margin)
{
    int side = (int)Math.Round(Math.Min(w, h) * (1 - 2 * margin));
    using var art = IconArt.Render(side, IconArt.Brand);
    var bmp = new Bitmap(w, h);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.Transparent);
    g.DrawImage(art, (w - side) / 2, (h - side) / 2, side, side);
    return bmp;
}

static void Save(string path, Bitmap bmp)
{
    using (bmp) bmp.Save(path, ImageFormat.Png);
}

/// <summary>An .ico of PNG-compressed entries (Vista+ reads them; keeps the 256 px entry small).</summary>
static void WriteIco(string path, int[] sizes)
{
    var images = sizes.Select(s =>
    {
        using var bmp = IconArt.Render(s, IconArt.Brand);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return (size: s, png: ms.ToArray());
    }).ToList();

    using var w = new BinaryWriter(File.Create(path));
    w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)images.Count); // ICONDIR
    int offset = 6 + 16 * images.Count;
    foreach (var (size, png) in images)
    {
        w.Write((byte)(size >= 256 ? 0 : size)); w.Write((byte)(size >= 256 ? 0 : size)); // 0 means 256
        w.Write((byte)0); w.Write((byte)0);
        w.Write((ushort)1); w.Write((ushort)32);
        w.Write(png.Length); w.Write(offset);
        offset += png.Length;
    }
    foreach (var (_, png) in images) w.Write(png);
}
