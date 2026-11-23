using System.Globalization;
using System.Text.RegularExpressions;
using AutoLang.Engine;

namespace AutoLang.Linux;

/// <summary>
/// Layout ids, language ids and engine profiles. With AutoLang as a GNOME input source, GNOME no longer types in the
/// user's layouts, so the list AutoLang types in is its own (<see cref="Settings"/>); GNOME's list only seeds it.
/// </summary>
internal static partial class Languages
{
    /// <summary>XKB layout → language tag, for the layouts AutoLang ships word lists for and their neighbours.</summary>
    private static readonly Dictionary<string, string> LayoutLanguage = new(StringComparer.Ordinal)
    {
        ["us"] = "en-US", ["gb"] = "en-GB", ["il"] = "he-IL", ["ru"] = "ru-RU", ["ua"] = "uk-UA", ["gr"] = "el-GR",
        ["ara"] = "ar-SA", ["by"] = "be-BY", ["bg"] = "bg-BG", ["de"] = "de-DE", ["fr"] = "fr-FR", ["es"] = "es-ES",
    };

    /// <summary>"[('xkb', 'us'), ('xkb', 'il'), ('ibus', 'autolang')]" → ["us", "il"]. Empty when gsettings is missing.</summary>
    public static List<string> GnomeXkbSources() =>
        XkbSource().Matches(Gnome.Get("sources") ?? "").Select(m => m.Groups[1].Value).ToList();

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
