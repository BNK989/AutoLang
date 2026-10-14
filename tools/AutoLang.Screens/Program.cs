using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using AutoLang.Engine;
using AutoLang.Host;

// Usage: AutoLang.Screens <ui-code> <output-dir>
// Writes <output-dir>/<code>-tray-menu.png and <code>-languages.png: the tray menu (as in Notepad, as in the Store
// build) and the Languages window, in that UI language, with English and Hebrew keyboards installed.
if (args.Length != 2) { Console.Error.WriteLine("usage: AutoLang.Screens <ui-code> <output-dir>"); return 1; }
var code = args[0];
var outDir = args[1];
Directory.CreateDirectory(outDir);

Environment.SetEnvironmentVariable("AUTOLANG_UI", code);
UiText.Init();
if (UiText.Current.Code != code) { Console.Error.WriteLine($"no translation for '{code}'"); return 1; }

Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

// The repo root, found from the exe's folder or else the working directory (the exe may be built elsewhere).
static string? FindRoot(string? dir)
{
    while (dir is not null && !File.Exists(Path.Combine(dir, "AutoLang.sln"))) dir = Path.GetDirectoryName(dir);
    return dir;
}
var root = FindRoot(AppContext.BaseDirectory) ?? FindRoot(Directory.GetCurrentDirectory());
if (root is null) { Console.Error.WriteLine("AutoLang.sln not found above " + AppContext.BaseDirectory); return 1; }

// Names the way LayoutProbe makes them: the language's display name in the UI language.
KeyboardLayoutTable Layout(string file)
{
    var json = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "data", "layouts", file)))!;
    json["name"] = CultureInfo.GetCultureInfo((int)json["langId"]!).DisplayName;
    return KeyboardLayoutTable.FromJson(json.ToJsonString());
}
var settings = new Settings();
var languages = new LanguageManager(new[]
{
    new InstalledLayout(0x04090409, Layout("0409.json")),
    new InstalledLayout(0x040d040d, Layout("040d.json")),
}, settings);
languages.Start();

Save(TrayMenuImage(languages), Path.Combine(outDir, $"{code}-tray-menu.png"));
using (var form = new LanguagesForm(languages))
    Save(ClientImage(form), Path.Combine(outDir, $"{code}-languages.png"));
Console.WriteLine($"{code}: ok");
return 0;

static void Save(Bitmap b, string path) { b.Save(path, ImageFormat.Png); b.Dispose(); }

// The menu as TrayApp builds it (TrayApp's constructor needs the live hook, session and tray icon).
static Bitmap TrayMenuImage(LanguageManager languages)
{
    var T = UiText.Current;
    const string app = "Notepad";
    var menu = TrayMenu.Create();
    ((TrayMenuRenderer)menu.Renderer).Theme = MenuTheme.Light;   // the taskbar of the slide is light, whatever this PC uses
    var header = new TrayMenuHeader { Title = $"{T.StateWatching} — {app}", Subtitle = languages.Summary(), Mark = IconArt.Brand };
    var typing = new TrayMenuItem(UiText.F(T.CompatibilityFor, app), "keyboard");
    menu.Items.AddRange(new ToolStripItem[]
    {
        header, new TrayMenuSeparator(),
        new TrayMenuItem(T.PauseAutoLang, "pause") { ShortcutKeyDisplayString = T.ClickTrayIcon },
        new TrayMenuItem(UiText.F(T.DisableFor, app), "prohibited") { Toggle = true },
        typing, new TrayMenuSeparator(),
        new TrayMenuItem(T.SwitchTyped, "arrow_swap") { ShortcutKeyDisplayString = T.ShiftTwice }, new TrayMenuSeparator(),
        new TrayMenuItem(T.LanguagesMenu, "local_language"),
        new TrayMenuItem(T.SkipFullscreen, "full_screen_maximize") { Toggle = true, Checked = true },
        new TrayMenuItem(T.StartWithWindowsSettings, "rocket"),
        new TrayMenuItem(T.ShowLog, "document_text"),
        new TrayMenuSeparator(),
        new TrayMenuItem(T.Exit, "dismiss"),
    });
    typing.DropDownItems.Add(new TrayMenuItem(""));   // the arrow needs a submenu behind it
    menu.CreateControl();
    menu.Size = menu.GetPreferredSize(Size.Empty);
    var bmp = new Bitmap(menu.Width, menu.Height);
    menu.DrawToBitmap(bmp, new Rectangle(Point.Empty, menu.Size));
    return bmp;
}

// A form's client area only: the Store slide draws its own title bar.
static Bitmap ClientImage(Form form)
{
    form.StartPosition = FormStartPosition.Manual;
    form.Location = new Point(-20000, -20000);
    form.ShowInTaskbar = false;
    form.Show();
    Application.DoEvents();
    using var full = new Bitmap(form.Width, form.Height);
    form.DrawToBitmap(full, new Rectangle(Point.Empty, form.Size));
    var client = form.RectangleToScreen(form.ClientRectangle);
    var origin = new Point(client.X - form.Left, client.Y - form.Top);
    return full.Clone(new Rectangle(origin, form.ClientSize), full.PixelFormat);
}
