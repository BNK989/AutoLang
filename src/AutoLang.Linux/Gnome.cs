using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoLang.Linux;

/// <summary>
/// GNOME's keyboard input sources (org.gnome.desktop.input-sources), read and written with the gsettings command, the
/// same way GNOME Settings stores them. Only <c>autolang enable</c> / <c>disable</c> write, and only when the user runs them.
/// </summary>
internal static partial class Gnome
{
    private const string Schema = "org.gnome.desktop.input-sources";
    public static readonly (string Type, string Id) AutoLangSource = ("ibus", IBus.EngineName);

    public static string? Get(string key)
    {
        var (exit, output) = Run("gsettings", $"get {Schema} {key}");
        return exit == 0 ? output.Trim() : null;
    }

    public static bool Set(string key, IEnumerable<(string Type, string Id)> sources)
    {
        var value = "[" + string.Join(", ", sources.Select(s => $"('{s.Type}', '{s.Id}')")) + "]";
        var (exit, _) = Run("gsettings", ["set", Schema, key, value]);
        return exit == 0;
    }

    /// <summary>"[('xkb', 'us'), ('ibus', 'autolang')]" (or "@a(ss) []") → pairs. Null when gsettings has no such schema.</summary>
    public static List<(string Type, string Id)>? Sources(string key = "sources") =>
        Get(key) is { } raw ? Pair().Matches(raw).Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList() : null;

    [GeneratedRegex(@"\('([^']*)',\s*'([^']*)'\)")]
    private static partial Regex Pair();

    public static bool HasAutoLang() => Sources()?.Contains(AutoLangSource) == true;

    /// <summary>Adds AutoLang to the input sources (other sources stay) and makes it the current one.</summary>
    public static string Enable()
    {
        var sources = Sources();
        if (sources is null) return "gsettings has no GNOME input sources here. Add AutoLang in your desktop's keyboard settings, or run: ibus engine autolang";
        if (!sources.Contains(AutoLangSource)) sources.Insert(0, AutoLangSource);
        if (!Set("sources", sources)) return "gsettings could not change the input sources.";
        // GNOME selects the most recently used source that exists: putting AutoLang first there selects it.
        var mru = Sources("mru-sources") ?? [];
        mru.Remove(AutoLangSource);
        mru.Insert(0, AutoLangSource);
        Set("mru-sources", mru);
        return "AutoLang is now an input source. If the top bar does not show it yet, press Super+Space or pick it there.";
    }

    public static string Disable()
    {
        var sources = Sources();
        if (sources is null || !sources.Remove(AutoLangSource)) return "AutoLang was not in the GNOME input sources.";
        Set("sources", sources); // an empty list is GNOME's own default: the system keyboard layout
        var mru = Sources("mru-sources") ?? [];
        if (mru.Remove(AutoLangSource)) Set("mru-sources", mru);
        return "AutoLang removed from the input sources. Your other keyboard layouts are unchanged.";
    }

    public static (int Exit, string Output) Run(string file, string arguments) => Run(file, arguments.Split(' '));

    public static (int Exit, string Output) Run(string file, IEnumerable<string> arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in arguments) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return (-1, "");
            var output = p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            return p.WaitForExit(3000) ? (p.ExitCode, output) : (-1, output);
        }
        catch (Exception) { return (-1, ""); } // not installed
    }
}
