using M0Spike;
using static M0Spike.Native;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var layouts = LayoutMap.Installed();
if (layouts.Length < 2)
{
    Console.Error.WriteLine($"Need at least two installed keyboard layouts; found {layouts.Length}.");
    return 2;
}

var mode = args.Length > 0 ? args[0] : "run";
switch (mode)
{
    case "dump":
        foreach (var l in layouts) l.Dump(Console.Out);
        return 0;

    case "dumpjson":
        // m0spike dumpjson <dir>  — one <langid>.json per installed layout, for the engine tests / eval harness.
        {
            var dir = args.Length > 1 ? args[1] : "data/layouts";
            Directory.CreateDirectory(dir);
            foreach (var l in layouts)
            {
                var path = Path.Combine(dir, $"{l.LangId:x4}.json");
                File.WriteAllText(path, l.ToJson(), new System.Text.UTF8Encoding(false));
                Console.WriteLine($"wrote {path} ({l.Name})");
            }
            return 0;
        }

    case "translit":
        // m0spike translit <fromLangIdHex> <text...>  e.g. translit 0409 akuo
        {
            var from = layouts.First(l => l.LangId == Convert.ToUInt16(args[1], 16));
            var to = layouts.First(l => l.LangId != from.LangId);
            var text = string.Join(' ', args.Skip(2));
            Console.WriteLine(from.TransliterateTo(to, text));
            return 0;
        }

    case "run":
        {
            foreach (var l in layouts) Log.Info($"layout: {l.Name}");
            using var hook = new Hook(layouts);
            hook.Install();
            Console.CancelKeyPress += (_, e) => { e.Cancel = false; hook.Dispose(); };
            // A low-level hook needs a message loop on the installing thread.
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
            return 0;
        }

    default:
        Console.Error.WriteLine("usage: M0Spike [dump | dumpjson <dir> | translit <langid> <text> | run]");
        return 1;
}
