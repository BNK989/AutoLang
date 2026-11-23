using System.Text;
using AutoLang.Engine;
using AutoLang.Session;
using Tmds.DBus.Protocol;

namespace AutoLang.Linux;

/// <summary>
/// Spike entry point.
///   autolang-ibus --ibus          the engine, as ibus-daemon starts it (component exec)
///   autolang-ibus layouts         the configured layouts and what each key types
///   autolang-ibus try "akuo vh "  types those physical keys (named by their US-layout characters) through the real
///                                 engine and session, switching language as corrections do, and prints the result
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Log.Init();
        var mode = args.FirstOrDefault() ?? "--ibus";

        var layouts = new List<XkbLayout>();
        foreach (var id in Languages.ConfiguredLayoutIds())
        {
            var lang = Languages.LangIdOf(id);
            if (lang == 0) { Log.Warn($"layout {id}: unknown language, skipped"); continue; }
            try { layouts.Add(new XkbLayout(id, lang, $"{Languages.DisplayName(lang)} [{id}]")); }
            catch (Exception ex) { Log.Warn($"layout {id}: {ex.Message}"); }
        }
        if (layouts.Count == 0) { Log.Warn("no usable layouts"); return 1; }
        Log.Info($"layouts: {string.Join(", ", layouts.Select(l => l.Table))}");

        return mode switch
        {
            "layouts" => DumpLayouts(layouts),
            "try" => Try(layouts, string.Join(' ', args.Skip(1))),
            _ => await RunEngine(layouts),
        };
    }

    private static async Task<int> RunEngine(List<XkbLayout> layouts)
    {
        var address = IBus.Address();
        if (address is null) { Log.Warn("no ibus-daemon found (IBUS_ADDRESS unset, no live address file)"); return 1; }
        Log.Info($"connecting to ibus at {address.Split(',')[0]}");

        var typing = new Typing(layouts);
        typing.LoadInBackground();

        using var connection = new DBusConnection(address);
        await connection.ConnectAsync();
        connection.AddMethodHandler(new Factory(connection, (c, path) => new AutoLangEngine(c, path, typing)));
        await connection.RequestNameAsync(IBus.ComponentName, RequestNameOptions.Default);
        Log.Info($"owning {IBus.ComponentName}; waiting for the daemon");
        var reason = await connection.DisconnectedAsync();
        Log.Info($"disconnected: {reason?.Message ?? "closed"}");
        return 0;
    }

    private static int DumpLayouts(List<XkbLayout> layouts)
    {
        foreach (var layout in layouts)
        {
            Console.WriteLine($"{layout.Table}  letters: {layout.Table.Letters.Count}");
            var sb = new StringBuilder("  ");
            foreach (var code in XkbLayout.CharacterKeys)
                sb.Append(layout.Translate(code, false, false) ?? '·');
            sb.Append("\n  ");
            foreach (var code in XkbLayout.CharacterKeys)
                sb.Append(layout.Translate(code, true, false) ?? '·');
            Console.WriteLine(sb);
        }
        return 0;
    }

    /// <summary>Offline run of the engine and session over a key sequence, with a simulated text field.</summary>
    private static int Try(List<XkbLayout> layouts, string keys)
    {
        var typing = new Typing(layouts);
        var reference = layouts.FirstOrDefault(l => l.Id.StartsWith("us", StringComparison.Ordinal)) ?? layouts[0];
        var field = new StringBuilder();
        typing.LoadInBackground();
        while (!typing.Controller.CanCorrect) Thread.Sleep(50);

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
            Console.WriteLine($"  {c.Kind}: -{c.Backspaces} +\"{c.Text}\" {Languages.Symbol(c.FromLangId)}→{Languages.Symbol(c.TargetLangId)} margin {c.Margin:0.00}");
            if (c.TargetLangId != 0) typing.Lang = c.TargetLangId;
        }
        Console.WriteLine($"field: \"{field}\"  (now typing {Languages.Symbol(typing.Lang)})");
        return 0;
    }
}
