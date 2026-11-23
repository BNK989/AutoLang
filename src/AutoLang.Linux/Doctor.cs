using System.Diagnostics;
using AutoLang.Engine;

namespace AutoLang.Linux;

/// <summary>
/// <c>autolang doctor</c>: walks the chain a key travels (IBus running, the component installed, AutoLang an input
/// source and selected, the engine running, layouts and word lists) and prints one line per link with what to do when
/// it is broken. Reads only; changes nothing. Safe to paste into a bug report: no typed text is involved.
/// </summary>
internal static class Doctor
{
    private static int _problems;

    public static int Run()
    {
        Console.WriteLine($"AutoLang {Version()} doctor");
        Console.WriteLine($"  binary   {Environment.ProcessPath}");
        Console.WriteLine($"  session  {Env("XDG_SESSION_TYPE")} · {Env("XDG_CURRENT_DESKTOP")} · {OsName()}");
        Console.WriteLine();

        var (ibusExit, ibusVersion) = Gnome.Run("ibus", "version");
        Check(ibusExit == 0, $"IBus installed ({ibusVersion.Trim()})", "IBus is not installed", "sudo apt install ibus");

        bool daemon = IBus.Address() is not null;
        Check(daemon, "ibus-daemon is running", "ibus-daemon is not running", "log out and back in, or run: ibus-daemon -drx");

        Check(File.Exists(Program.ComponentFile), $"IBus component installed ({Program.ComponentFile})",
            "the IBus component file is missing", "reinstall the package: sudo apt install --reinstall autolang");

        if (daemon)
        {
            var (_, engines) = Gnome.Run("ibus", "list-engine");
            Check(engines.Contains(" autolang - ", StringComparison.Ordinal), "IBus knows the AutoLang engine",
                "IBus has not picked up AutoLang yet", "run: ibus restart   (or log out and back in)");
        }

        var sources = Gnome.Sources();
        if (sources is null) Note("not GNOME (no org.gnome.desktop.input-sources): add AutoLang in your desktop's input method settings");
        else Check(sources.Contains(Gnome.AutoLangSource), "AutoLang is a GNOME input source",
            "AutoLang is not in your input sources", "run: autolang enable   (or Settings → Keyboard → Input Sources → + → AutoLang)");

        if (daemon)
        {
            var (_, current) = Gnome.Run("ibus", "engine");
            Check(current.Trim() == IBus.EngineName, "AutoLang is the current input source",
                $"the current input source is '{current.Trim()}'", "pick AutoLang in the top bar, or press Super+Space until it is selected");
        }

        var running = Process.GetProcessesByName("autolang").Any(p => p.Id != Environment.ProcessId);
        Check(running, "the AutoLang engine is running", "the AutoLang engine is not running",
            "it starts when AutoLang is selected; if it is selected, see the log below");

        foreach (var name in new[] { "GTK_IM_MODULE", "QT_IM_MODULE" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } v && v != "ibus")
                Problem($"{name}={v}: apps are told to use another input method", $"unset {name}, or set it to ibus");

        Console.WriteLine();
        var settings = Settings.Load();
        Console.WriteLine($"  settings {Settings.FilePath}{(settings.Paused ? "  (PAUSED)" : "")}");
        var layouts = Program.LoadLayouts(settings, quiet: true);
        foreach (var l in layouts)
        {
            var words = Languages.WordListCode(l.Table.LangId);
            Check(words is not null, $"layout {l.Id}: {l.Table.Name}, {l.Table.Script}, word list {words}",
                $"layout {l.Id}: no word list, typed but never corrected", "AutoLang corrects only languages it ships a word list for");
        }
        int scripts = layouts.Where(l => Languages.WordListCode(l.Table.LangId) is not null)
            .Select(l => l.Table.Script).Where(s => s != Scripts.Unknown).Distinct().Count();
        Check(scripts >= 2, "two alphabets to correct between",
            "fewer than two alphabets with word lists", $"edit \"layouts\" in {Settings.FilePath}, e.g. [\"us\", \"il\"], then run: ibus restart");
        Console.WriteLine($"  learnt   {settings.ExceptionWords.Count} word(s) never corrected");
        Console.WriteLine($"  log      {Log.FilePath}{(File.Exists(Log.FilePath) ? "" : " (not created yet)")}");

        Console.WriteLine();
        Console.WriteLine(_problems == 0 ? "Everything looks right." : $"{_problems} thing(s) to fix, top to bottom.");
        return _problems == 0 ? 0 : 1;
    }

    private static void Check(bool ok, string good, string bad, string fix)
    {
        if (ok) Console.WriteLine($"  ok   {good}");
        else Problem(bad, fix);
    }

    private static void Problem(string bad, string fix)
    {
        _problems++;
        Console.WriteLine($"  !!   {bad}");
        Console.WriteLine($"         → {fix}");
    }

    private static void Note(string text) => Console.WriteLine($"  --   {text}");

    private static string Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : "?";

    private static string Version() => Program.Version;

    private static string OsName()
    {
        try
        {
            foreach (var line in File.ReadLines("/etc/os-release"))
                if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal)) return line["PRETTY_NAME=".Length..].Trim('"');
        }
        catch (Exception) { }
        return "?";
    }
}
