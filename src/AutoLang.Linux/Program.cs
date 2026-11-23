using System.Diagnostics;
using System.Reflection;
using System.Text;
using AutoLang.Engine;
using AutoLang.Session;
using Tmds.DBus.Protocol;

namespace AutoLang.Linux;

/// <summary>
/// One binary for the IBus engine and the command line.
///   autolang --ibus          the engine, as ibus-daemon starts it (see packaging/linux/autolang.xml)
///   autolang enable|disable  add AutoLang to GNOME's input sources, or take it out again
///   autolang doctor          checks every link from IBus to the word lists and says what to fix
///   autolang layouts         the layouts AutoLang types in, and what each key types on them
///   autolang try "akuo vh "  runs those keys (named by their US-layout characters) through the engine, offline
/// </summary>
internal static class Program
{
    public const string ComponentFile = "/usr/share/ibus/component/autolang.xml";

    public static string Version =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var command = args.FirstOrDefault() ?? "help";
        if (command == "--ibus") Log.Init();
        if (!OperatingSystem.IsLinux() && command is "--ibus" or "doctor" or "layouts" or "try")
        {
            Console.Error.WriteLine("This is the Linux build of AutoLang.");
            return 1;
        }

        switch (command)
        {
            case "--ibus": return await RunEngine();
            case "enable": Console.WriteLine(Gnome.Enable()); return 0;
            case "disable": Console.WriteLine(Gnome.Disable()); return 0;
            case "doctor": return Doctor.Run();
            case "layouts": return DumpLayouts(LoadLayouts(Settings.Load(), quiet: true));
            case "try": return Try(LoadLayouts(Settings.Load(), quiet: true), string.Join(' ', args.Skip(1)));
            case "--version" or "version": Console.WriteLine($"AutoLang {Version}"); return 0;
            default:
                Console.WriteLine($"""
                    AutoLang {Version}: fixes text typed in the wrong keyboard layout. An IBus input method.

                      autolang enable      add AutoLang to your GNOME input sources and select it
                      autolang disable     remove it again (your other layouts stay)
                      autolang doctor      check the setup and say what to fix
                      autolang layouts     the layouts AutoLang types in
                      autolang try KEYS    run keys through the engine offline, e.g. autolang try "akuo "

                    Settings: {Settings.FilePath}
                    Log:      {Log.FilePath}
                    Help:     https://github.com/BNK989/AutoLang
                    """);
                return command is "help" or "--help" or "-h" ? 0 : 2;
        }
    }

    /// <summary>The settings' layouts, compiled. Unknown or uncompilable ones are skipped with a warning.</summary>
    public static List<XkbLayout> LoadLayouts(Settings settings, bool quiet = false)
    {
        var layouts = new List<XkbLayout>();
        foreach (var id in settings.EffectiveLayouts())
        {
            var lang = Languages.LangIdOf(id);
            if (lang == 0) { Warn($"layout '{id}': AutoLang does not know its language yet, skipped"); continue; }
            try { layouts.Add(new XkbLayout(id, lang, $"{Languages.DisplayName(lang)} [{id}]")); }
            catch (Exception ex) { Warn($"layout '{id}': {ex.Message}"); }
        }
        return layouts;

        void Warn(string m) { if (quiet) Console.Error.WriteLine(m); else Log.Warn(m); }
    }

    private static async Task<int> RunEngine()
    {
        Log.Info($"AutoLang {Version} starting ({Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "?"} session)");
        var settings = Settings.Load();
        var layouts = LoadLayouts(settings);
        if (layouts.Count == 0) { Log.Warn($"no usable layouts in {Settings.FilePath}"); return 1; }
        Log.Info($"layouts: {string.Join(", ", layouts.Select(l => l.Table))}");

        var address = IBus.Address();
        if (address is null) { Log.Warn("no ibus-daemon found (IBUS_ADDRESS unset, no live address file)"); return 1; }

        var typing = new Typing(layouts, settings);
        typing.LoadInBackground();

        using var connection = new DBusConnection(address);
        await connection.ConnectAsync();
        connection.AddMethodHandler(new Factory(connection, (c, path) => new AutoLangEngine(c, path, typing)));
        await connection.RequestNameAsync(IBus.ComponentName, RequestNameOptions.Default);
        Log.Info($"registered as {IBus.ComponentName}");
        var reason = await connection.DisconnectedAsync();
        Log.Info($"ibus-daemon disconnected: {reason?.Message ?? "closed"}");
        return 0;
    }

    private static int DumpLayouts(List<XkbLayout> layouts)
    {
        Console.WriteLine($"From {Settings.FilePath}:");
        foreach (var layout in layouts)
        {
            var words = Languages.WordListCode(layout.Table.LangId) is { } list ? $"word list {list}" : "no word list: typed, never corrected";
            Console.WriteLine($"{layout.Table}, {words}");
            var sb = new StringBuilder("  ");
            foreach (var code in XkbLayout.CharacterKeys) sb.Append(layout.Translate(code, false, false) ?? '·');
            sb.Append("\n  ");
            foreach (var code in XkbLayout.CharacterKeys) sb.Append(layout.Translate(code, true, false) ?? '·');
            Console.WriteLine(sb);
        }
        return layouts.Count > 0 ? 0 : 1;
    }

    /// <summary>Offline run of the engine and session over a key sequence, with a simulated text field.</summary>
    private static int Try(List<XkbLayout> layouts, string keys)
    {
        if (layouts.Count == 0) return 1;
        if (keys.Length == 0) { Console.Error.WriteLine("usage: autolang try \"akuo vh \"   (keys named by their US-layout characters; \\n is Enter)"); return 2; }
        var typing = new Typing(layouts, new Settings { Layouts = layouts.Select(l => l.Id).ToList() });
        var reference = layouts.FirstOrDefault(l => l.Id.StartsWith("us", StringComparison.Ordinal)) ?? layouts[0];
        var field = new StringBuilder();
        var sw = Stopwatch.StartNew();
        typing.LoadInBackground();
        while (!typing.Controller.CanCorrect && sw.Elapsed < TimeSpan.FromSeconds(20)) Thread.Sleep(50);

        foreach (var k in keys.Replace("\\n", "\n"))
        {
            Correction? c;
            if (k == '\n') { c = typing.Controller.OnEnter(typing.Lang); if (c is null) { field.Append('\n'); continue; } c = c with { PressEnter = true }; }
            else
            {
                char typed = k;
                if (k != ' ' && reference.Table.TryGetKey(k, out var key)) typed = typing.Layout(typing.Lang).Translate((ushort)key.Vk, key.Shift, false) ?? k;
                c = typing.Controller.OnChar(typed, typing.Lang);
                if (c is null) { field.Append(typed); continue; }
            }
            field.Length -= Math.Min(c.Backspaces, field.Length);
            field.Append(c.Text);
            if (c.PressEnter) field.Append('\n');
            Console.WriteLine($"  {c.Kind}: -{c.Backspaces} +\"{c.Text}\" {Languages.Symbol(c.FromLangId)}→{Languages.Symbol(c.TargetLangId)} margin {c.Margin:0.00} ({c.Reason})");
            if (c.TargetLangId != 0) typing.Lang = c.TargetLangId;
        }
        Console.WriteLine($"field: \"{field}\"  (now typing {Languages.Symbol(typing.Lang)})");
        return 0;
    }
}
