using System.Globalization;

namespace AutoLang.Mac;

/// <summary>
/// The word lists bundled in the app (data/words, the same resources the Windows host embeds). The Mac tester build
/// has no downloads yet, so a language without a bundled list is simply not corrected.
/// </summary>
internal static class WordLists
{
    public const string Credit = "FrequencyWords by Hermit Dave (OpenSubtitles 2018), CC BY-SA 4.0";

    /// <summary>The list's name for the language of <paramref name="langId"/>; null for IME languages and unknown ones. As on Windows.</summary>
    public static string? SourceCode(ushort langId)
    {
        CultureInfo c;
        try { c = CultureInfo.GetCultureInfo(langId); } catch (CultureNotFoundException) { return null; }
        if (c.Name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase)) return "pt_br";
        return c.TwoLetterISOLanguageName switch
        {
            "nb" or "nn" => "no",
            "ja" or "zh" or "ko" => null,
            var iso when iso.Length == 2 => iso,
            _ => null,
        };
    }

    private static string Resource(string code) => $"words/{code}_50k.txt";

    public static bool IsBundled(string code) => typeof(WordLists).Assembly.GetManifestResourceInfo(Resource(code)) is not null;

    public static IEnumerable<string> ReadLines(string code)
    {
        using var stream = typeof(WordLists).Assembly.GetManifestResourceStream(Resource(code))
            ?? throw new FileNotFoundException("no bundled word list", Resource(code));
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line) yield return line;
    }
}
