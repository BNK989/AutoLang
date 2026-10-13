namespace AutoLang.Engine;

/// <summary>Word → count. Loaded from a "word count" per-line frequency list (the format of hermitdave/FrequencyWords).</summary>
public sealed class WordDictionary
{
    private readonly Dictionary<string, long> _counts;
    private readonly double _logTotal;

    public int Count => _counts.Count;

    public WordDictionary(IEnumerable<KeyValuePair<string, long>> entries)
    {
        _counts = new Dictionary<string, long>(StringComparer.Ordinal);
        long total = 0;
        foreach (var (w, c) in entries)
        {
            if (w.Length == 0 || c <= 0) continue;
            _counts[w] = _counts.GetValueOrDefault(w) + c;
            total += c;
        }
        _logTotal = Math.Log(Math.Max(total, 1));
    }

    public IEnumerable<(string Word, double Weight)> Entries => _counts.Select(kv => (kv.Key, (double)kv.Value));

    public bool Contains(string word) => _counts.ContainsKey(word);

    /// <summary>ln(count / total); 0 if absent (caller checks Contains first).</summary>
    public double LogFreq(string word) => _counts.TryGetValue(word, out var c) ? Math.Log(c) - _logTotal : 0;

    /// <summary>Parse "word count" lines. Lines that don't parse are skipped; <paramref name="normalize"/> lets a language lowercase etc.</summary>
    public static WordDictionary FromFrequencyLines(IEnumerable<string> lines, Func<string, string>? normalize = null, int maxEntries = int.MaxValue)
    {
        var list = new List<KeyValuePair<string, long>>();
        foreach (var raw in lines)
        {
            if (list.Count >= maxEntries) break;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int sp = line.LastIndexOf(' ');
            if (sp <= 0) continue;
            if (!long.TryParse(line.AsSpan(sp + 1), out var count)) continue;
            var word = line[..sp];
            if (normalize is not null) word = normalize(word);
            list.Add(new KeyValuePair<string, long>(word, count));
        }
        return new WordDictionary(list);
    }

    public static WordDictionary LoadFrequencyFile(string path, Func<string, string>? normalize = null, int maxEntries = int.MaxValue)
        => FromFrequencyLines(File.ReadLines(path), normalize, maxEntries);
}
