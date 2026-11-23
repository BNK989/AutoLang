using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using AutoLang.Engine;

namespace AutoLang.Linux;

/// <summary>
/// Which layouts AutoLang types in, and their engine profiles. With AutoLang as the only GNOME input source, GNOME no
/// longer lists the user's layouts, so the list is AutoLang's own: AUTOLANG_LAYOUTS ("us,il"), else the XKB sources in
/// GNOME's settings (the state before AutoLang was added), else us + il.
/// </summary>
internal static partial class Languages
{
    /// <summary>XKB layout → language tag. Spike-sized; the real build reads evdev.xml's languageList.</summary>
    private static readonly Dictionary<string, string> LayoutLanguage = new(StringComparer.Ordinal)
    {
        ["us"] = "en-US", ["gb"] = "en-GB", ["il"] = "he-IL", ["ru"] = "ru-RU", ["ua"] = "uk-UA", ["gr"] = "el-GR",
        ["ara"] = "ar-SA", ["by"] = "be-BY", ["bg"] = "bg-BG", ["de"] = "de-DE", ["fr"] = "fr-FR", ["es"] = "es-ES",
    };

    public static IReadOnlyList<string> ConfiguredLayoutIds()
    {
        var env = Environment.GetEnvironmentVariable("AUTOLANG_LAYOUTS");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fromGnome = GnomeXkbSources();
        if (fromGnome.Count >= 2) return fromGnome;
        return ["us", "il"];
    }

    /// <summary>"[('xkb', 'us'), ('xkb', 'il'), ('ibus', 'autolang')]" → ["us", "il"]. Empty when gsettings is missing.</summary>
    private static List<string> GnomeXkbSources()
    {
        try
        {
            var psi = new ProcessStartInfo("gsettings", "get org.gnome.desktop.input-sources sources")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            using var p = Process.Start(psi);
            if (p is null) return [];
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            return XkbSource().Matches(output).Select(m => m.Groups[1].Value).ToList();
        }
        catch (Exception) { return []; }
    }

    [GeneratedRegex(@"\('xkb',\s*'([^']+)'\)")]
    private static partial Regex XkbSource();

    /// <summary>The Windows LANGID of a layout's language, which the engine and the word lists key on (as on Mac). 0 = unknown.</summary>
    public static ushort LangIdOf(string layoutId)
    {
        var (layout, _) = XkbLayout.Split(layoutId);
        if (!LayoutLanguage.TryGetValue(layout, out var tag)) return 0;
        try
        {
            var lcid = CultureInfo.CreateSpecificCulture(tag).LCID;
            return lcid is > 0 and < 0x10000 and not 0x1000 ? (ushort)lcid : (ushort)0;
        }
        catch (CultureNotFoundException) { return 0; }
    }

    public static string DisplayName(ushort langId)
    {
        try { return CultureInfo.GetCultureInfo(langId).NativeName; }
        catch (CultureNotFoundException) { return $"0x{langId:X4}"; }
    }

    /// <summary>Two-letter label for the top bar: "EN", "HE".</summary>
    public static string Symbol(ushort langId)
    {
        try { return CultureInfo.GetCultureInfo(langId).TwoLetterISOLanguageName.ToUpperInvariant(); }
        catch (CultureNotFoundException) { return "??"; }
    }

    /// <summary>The bundled word list's code for a language, as on Windows and Mac; null when none ships.</summary>
    public static string? WordListCode(ushort langId)
    {
        CultureInfo c;
        try { c = CultureInfo.GetCultureInfo(langId); } catch (CultureNotFoundException) { return null; }
        var iso = c.TwoLetterISOLanguageName;
        var code = iso is "nb" or "nn" ? "no" : iso;
        return typeof(Languages).Assembly.GetManifestResourceInfo($"words/{code}_50k.txt") is not null ? code : null;
    }

    public static LanguageProfile BuildProfile(KeyboardLayoutTable table, string code)
    {
        return LanguageProfile.FromFrequencyLines(table.LangId, table.Name, ReadLines(code),
            LanguageProfile.DefaultRulesFor(table.LangId), LanguageProfile.DefaultNormalizeFor(table.LangId));
    }

    private static IEnumerable<string> ReadLines(string code)
    {
        using var stream = typeof(Languages).Assembly.GetManifestResourceStream($"words/{code}_50k.txt")
            ?? throw new FileNotFoundException("no bundled word list", code);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line) yield return line;
    }
}
