namespace AutoLang.Engine;

/// <summary>
/// Character n-gram language model with interpolated Witten-Bell smoothing. This is the "lightweight AI":
/// it answers "how much does this string look like a word of this language?" in microseconds, with no runtime.
/// Trained from a word-frequency list; a token-weighted model learns that `ing$` is English and `ים$` is Hebrew.
/// </summary>
public sealed class CharNGramModel
{
    private const char Start = '\u0002', End = '\u0003';

    private readonly int _order;
    // context → (next char → weighted count)
    private readonly Dictionary<string, Dictionary<char, double>> _counts = new();
    private readonly HashSet<char> _vocab = new();

    public int Order => _order;

    public CharNGramModel(int order = 5)
    {
        if (order < 1) throw new ArgumentOutOfRangeException(nameof(order));
        _order = order;
    }

    public void Add(string word, double weight = 1)
    {
        if (weight <= 0 || word.Length == 0) return;
        var padded = new string(Start, _order - 1) + word + End;
        for (int i = _order - 1; i < padded.Length; i++)
        {
            var next = padded[i];
            _vocab.Add(next);
            // every suffix of the history, so backoff contexts are counted too
            for (int len = 0; len <= _order - 1; len++)
            {
                var ctx = padded.Substring(i - len, len);
                if (!_counts.TryGetValue(ctx, out var row)) _counts[ctx] = row = new Dictionary<char, double>();
                row[next] = row.GetValueOrDefault(next) + weight;
            }
        }
    }

    public void AddAll(IEnumerable<(string Word, double Weight)> words)
    {
        foreach (var (w, c) in words) Add(w, c);
    }

    /// <summary>Natural-log probability of the whole word including its end marker.</summary>
    public double LogProb(string word)
    {
        if (word.Length == 0) return 0;
        var padded = new string(Start, _order - 1) + word + End;
        double lp = 0;
        for (int i = _order - 1; i < padded.Length; i++)
            lp += Math.Log(Prob(padded, i));
        return lp;
    }

    /// <summary>Per-character log-prob, handy for length-independent plausibility floors.</summary>
    public double LogProbPerChar(string word) => word.Length == 0 ? 0 : LogProb(word) / (word.Length + 1);

    private double Prob(string padded, int i)
    {
        // P(c | h) = (count(h,c) + T(h) * P(c | h')) / (count(h) + T(h)); base case uniform over vocab + 1 unseen.
        double p = 1.0 / (_vocab.Count + 1);
        var next = padded[i];
        for (int len = 0; len <= _order - 1; len++)
        {
            var ctx = padded.Substring(i - len, len);
            if (!_counts.TryGetValue(ctx, out var row)) break; // longer contexts can't exist either
            double total = 0;
            foreach (var v in row.Values) total += v;
            double types = row.Count;
            double seen = row.GetValueOrDefault(next);
            p = (seen + types * p) / (total + types);
        }
        return p;
    }
}
