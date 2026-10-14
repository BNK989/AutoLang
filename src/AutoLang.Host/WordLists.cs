using System.Globalization;
using System.Net.Http;
using System.Net;
using AutoLang.Engine;

namespace AutoLang.Host;

/// <summary>
/// Where each language's word list comes from: bundled in the exe, or downloaded on demand from
/// hermitdave/FrequencyWords (CC-BY-SA 4.0) into %LocalAppData%\AutoLang\words. A download is the host's only network
/// use (PLAN.md §6): a plain GET of a public file, pinned to one commit, sending nothing about the user but which
/// language list it wants.
/// </summary>
internal static class WordLists
{
    /// <summary>Pinned so a later upstream change can never alter what users get without a new release of AutoLang.</summary>
    public const string Commit = "525f9b560de45753a5ea01069454e72e9aa541c6";
    public const string Credit = "FrequencyWords by Hermit Dave (OpenSubtitles 2018), CC BY-SA 4.0";
    public const string Attribution = "Word lists: " + Credit;
    public const string AttributionUrl = "https://github.com/hermitdave/FrequencyWords";

    private const long MaxBytes = 8L * 1024 * 1024; // the 50k lists are 0.5–1.5 MB
    private const int MinEntries = 1000;

    public static string DownloadDir => Path.Combine(Log.Dir, "words");

    /// <summary>
    /// FrequencyWords' name for the language of <paramref name="langId"/>, which is also the local file stem, or null
    /// when there is nothing usable: Japanese, Chinese and Korean are typed through an IME, so the keys are not the
    /// letters and there is nothing to transliterate.
    /// </summary>
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
            _ => null, // no two-letter code: FrequencyWords has no list for it
        };
    }

    public static string FileName(string code) => $"{code}_50k.txt";

    /// <summary>
    /// The list for <paramref name="code"/>: a downloaded one wins over the bundled one. Null if neither exists.
    /// Bundled lists are resources inside the exe (see the csproj), so the portable build is a single file.
    /// </summary>
    public static WordListSource? Find(string code)
    {
        var downloaded = Path.Combine(DownloadDir, FileName(code));
        if (File.Exists(downloaded)) return FromFile(downloaded);
        var resource = "words/" + FileName(code);
        if (typeof(WordLists).Assembly.GetManifestResourceInfo(resource) is null) return null;
        return new WordListSource(Bundled: true, () => ReadResource(resource));
    }

    public static WordListSource FromFile(string path) => new(Bundled: false, () => File.ReadLines(path));

    private static IEnumerable<string> ReadResource(string name)
    {
        using var stream = typeof(WordLists).Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException("no bundled word list", name);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line) yield return line;
    }

    public static string Url(string code) =>
        $"https://raw.githubusercontent.com/hermitdave/FrequencyWords/{Commit}/content/2018/{code}/{FileName(code)}";

    /// <summary>
    /// Download, validate and install the list for <paramref name="code"/>. The file only replaces anything once it has
    /// passed <see cref="Validate"/>, so a cut-off or wrong file never reaches the engine.
    /// </summary>
    /// <exception cref="WordListUnavailableException">FrequencyWords has no list for this language.</exception>
    public static async Task<string> DownloadAsync(HttpClient http, string code, string expectedScript, CancellationToken ct)
    {
        using var resp = await http.GetAsync(Url(code), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) throw new WordListUnavailableException(code);
        resp.EnsureSuccessStatusCode();
        if (resp.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException($"list is {resp.Content.Headers.ContentLength} bytes");

        Directory.CreateDirectory(DownloadDir);
        var final = Path.Combine(DownloadDir, FileName(code));
        var part = final + ".part";
        try
        {
            await using (var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buf = new byte[81920];
                long total = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
                {
                    total += n;
                    if (total > MaxBytes) throw new InvalidDataException($"list exceeds {MaxBytes} bytes");
                    await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                }
            }
            Validate(part, expectedScript);
            File.Move(part, final, overwrite: true);
            return final;
        }
        finally
        {
            try { if (File.Exists(part)) File.Delete(part); } catch (IOException) { /* best effort */ }
        }
    }

    /// <summary>
    /// A usable list: "word count" lines, at least <see cref="MinEntries"/> of them, whose words are written in the
    /// alphabet the keyboard types. The last check catches a list that is not what we think it is (Serbian in Latin
    /// for a Cyrillic keyboard, a mislabelled folder) before it can make AutoLang correct good text.
    /// </summary>
    public static void Validate(string path, string expectedScript)
    {
        var dict = WordDictionary.LoadFrequencyFile(path);
        if (dict.Count < MinEntries) throw new InvalidDataException($"only {dict.Count} entries");
        if (expectedScript == Scripts.Unknown) return;
        var script = Scripts.Majority(dict.Entries.Take(5000).SelectMany(e => e.Word));
        if (script != expectedScript) throw new InvalidDataException($"list is in {script}, keyboard types {expectedScript}");
    }
}

/// <summary>A word list to load: bundled in the exe or downloaded. <see cref="ReadLines"/> reads it from the start each call.</summary>
internal sealed record WordListSource(bool Bundled, Func<IEnumerable<string>> ReadLines);

internal sealed class WordListUnavailableException : Exception
{
    public WordListUnavailableException(string code) : base($"no word list for '{code}' at FrequencyWords") { }
}
