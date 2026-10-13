using AutoLang.Engine;

namespace AutoLang.Eval;

/// <param name="Intended">What the user meant to write, in its own script.</param>
/// <param name="IntendedLayout">The layout that produces <paramref name="Intended"/>.</param>
/// <param name="TypedLayout">The layout that was actually active.</param>
public sealed record EvalCase(string Intended, KeyboardLayoutTable IntendedLayout, KeyboardLayoutTable TypedLayout, string Category)
{
    public bool WrongLayout => IntendedLayout.LangId != TypedLayout.LangId;

    /// <summary>The text as it appeared on screen.</summary>
    public string Typed => WrongLayout ? IntendedLayout.TransliterateTo(TypedLayout, Intended) : Intended;
}

public static class Corpus
{
    /// <summary>Tatoeba per-language export: "id\tlang\ttext". Keeps sentences every char of which the layout can type.</summary>
    public static IEnumerable<string> LoadTatoeba(string path, KeyboardLayoutTable layout, int max)
    {
        if (!File.Exists(path)) yield break;
        int n = 0;
        var seen = new HashSet<string>();
        foreach (var line in File.ReadLines(path))
        {
            if (n >= max) yield break;
            int tab = line.LastIndexOf('\t');
            if (tab < 0) continue;
            var text = line[(tab + 1)..].Trim();
            if (text.Length < 2 || text.Length > 80) continue;
            if (!text.All(c => c == ' ' || layout.TryGetKey(c, out _))) continue;
            if (!text.Any(char.IsLetter)) continue;
            if (!seen.Add(text)) continue;
            n++;
            yield return text;
        }
    }
}

public sealed class Report
{
    // Every point where the host would act: prefixes of 1, 2, 3 words and the full run.
    public int Corrections, CorrectionsRight, CorrectionsOnGoodText, CorrectionsWrongText;
    // Full-run outcomes (recall is measured here: by the end of the run, did we fix it?)
    public int FullWrong, FullFixed, FullMissed, FullDeferred, FullGood, FullMangled;
    // First-word outcomes (the critical case: can we act before the user types more?)
    public int FirstWordCases, FirstWordDeferred, FirstWordFalsePositives, FirstWordTruePositives, FirstWordMissed;
    // Defer resolution: of runs deferred at word 1, how many were decided (either way) by word ≤ 3
    public int DeferStarted, DeferResolvedBy3;
    public readonly Dictionary<string, (int fp, int fn, int total)> ByCategory = new();
    public readonly List<string> FalsePositiveSamples = new(), FalseNegativeSamples = new(), WrongReplacementSamples = new();
    public int SampleLimit = 50;

    /// <summary>Of all corrections the host would have made, the share that produced the intended text.</summary>
    public double Precision => Corrections == 0 ? 1 : (double)CorrectionsRight / Corrections;
    /// <summary>Of all wrong-layout runs, the share fixed by the end of the run.</summary>
    public double Recall => FullWrong == 0 ? 0 : (double)FullFixed / FullWrong;
    public double FirstWordPrecision => FirstWordTruePositives + FirstWordFalsePositives == 0 ? 1 : (double)FirstWordTruePositives / (FirstWordTruePositives + FirstWordFalsePositives);
    public double DeferRateFirstWord => FirstWordCases == 0 ? 0 : (double)FirstWordDeferred / FirstWordCases;
    public double DeferResolvedWithin2 => DeferStarted == 0 ? 1 : (double)DeferResolvedBy3 / DeferStarted;

    public void Print(TextWriter w, int samples = 15)
    {
        w.WriteLine();
        w.WriteLine($"corrections (any prefix): {Corrections}  right {CorrectionsRight}  on-good-text {CorrectionsOnGoodText}  wrong-text {CorrectionsWrongText}");
        w.WriteLine($"precision {Precision:P2}   recall {Recall:P1}  (full runs: {FullWrong} wrong-layout → fixed {FullFixed}, missed {FullMissed}, deferred {FullDeferred}; {FullGood} good → mangled {FullMangled})");
        w.WriteLine($"first word only:  TP {FirstWordTruePositives}  FP {FirstWordFalsePositives}  missed {FirstWordMissed}  deferred {FirstWordDeferred} / {FirstWordCases} ({DeferRateFirstWord:P1})  precision {FirstWordPrecision:P2}");
        w.WriteLine($"defers resolved within 2 more words: {DeferResolvedWithin2:P1} of {DeferStarted}");
        foreach (var (cat, v) in ByCategory) w.WriteLine($"  [{cat}] FP {v.fp}  FN {v.fn}  of {v.total}");
        Samples(w, "false positives (would have mangled correct text)", FalsePositiveSamples, samples);
        Samples(w, "wrong replacement", WrongReplacementSamples, samples);
        Samples(w, "false negatives (missed at end of run)", FalseNegativeSamples, samples);
    }

    private static void Samples(TextWriter w, string title, List<string> s, int n)
    {
        if (s.Count == 0) return;
        w.WriteLine($"{title} — first {Math.Min(s.Count, n)}:");
        foreach (var x in s.Take(n)) w.WriteLine("  " + x);
    }
}

public static class Evaluator
{
    public static Report Run(PhraseDecider decider, IReadOnlyList<EvalCase> cases, int sampleLimit = 50)
    {
        var r = new Report { SampleLimit = sampleLimit };
        foreach (var c in cases)
        {
            var other = c.WrongLayout ? c.IntendedLayout : OtherLayouts[c.TypedLayout.LangId];
            var typedWords = c.Typed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var intendedWords = c.Intended.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (typedWords.Length == 0 || typedWords.Length != intendedWords.Length) continue;
            int n = typedWords.Length;

            var cat = r.ByCategory.GetValueOrDefault(c.Category);
            cat.total++;

            // Evaluate prefixes of 1..3 words and the full run (deduplicated when the run is ≤ 3 words).
            var lengths = Enumerable.Range(1, Math.Min(3, n)).ToList();
            if (n > 3) lengths.Add(n);

            Decision? first = null, full = null;
            bool deferResolved = false;
            foreach (int k in lengths)
            {
                var typed = string.Join(' ', typedWords.Take(k));
                var intended = string.Join(' ', intendedWords.Take(k));
                var d = decider.Decide(typed, c.TypedLayout, other);
                if (k == 1) first = d;
                if (k == n) full = d;
                if (k > 1 && first!.Kind == DecisionKind.Defer && d.Kind != DecisionKind.Defer) deferResolved = true;

                if (d.Kind != DecisionKind.Correct) continue;
                r.Corrections++;
                if (!c.WrongLayout)
                {
                    r.CorrectionsOnGoodText++;
                    cat.fp++;
                    r.Sample(r.FalsePositiveSamples, $"[w{k}] '{typed}' → '{d.Replacement}' margin {d.Margin:F1}");
                }
                else if (d.Replacement == intended) r.CorrectionsRight++;
                else
                {
                    r.CorrectionsWrongText++;
                    cat.fp++;
                    r.Sample(r.WrongReplacementSamples, $"[w{k}] '{typed}' → '{d.Replacement}' expected '{intended}'");
                }
            }

            // Full-run bookkeeping
            if (c.WrongLayout)
            {
                r.FullWrong++;
                if (full!.Kind == DecisionKind.Correct && full.Replacement == c.Intended) r.FullFixed++;
                else if (full.Kind == DecisionKind.Defer) { r.FullDeferred++; cat.fn++; }
                else if (full.Kind == DecisionKind.Keep) { r.FullMissed++; cat.fn++; r.Sample(r.FalseNegativeSamples, $"'{c.Typed}' (meant '{c.Intended}') margin {full.Margin:F1}"); }
            }
            else
            {
                r.FullGood++;
                if (full!.Kind == DecisionKind.Correct) r.FullMangled++;
            }

            // First-word bookkeeping
            r.FirstWordCases++;
            switch (first!.Kind)
            {
                case DecisionKind.Defer:
                    r.FirstWordDeferred++;
                    r.DeferStarted++;
                    if (deferResolved || n < 3) r.DeferResolvedBy3++; // a short run that ends deferred is the user's Enter resolving it
                    break;
                case DecisionKind.Correct:
                    if (c.WrongLayout) r.FirstWordTruePositives++; else r.FirstWordFalsePositives++;
                    break;
                case DecisionKind.Keep:
                    if (c.WrongLayout) r.FirstWordMissed++;
                    break;
            }
            r.ByCategory[c.Category] = cat;
        }
        return r;
    }

    /// <summary>Set by Program: for a correctly-typed case we still need "the other layout" to score against.</summary>
    public static readonly Dictionary<ushort, KeyboardLayoutTable> OtherLayouts = new();

    private static void Sample(this Report r, List<string> list, string s) { if (list.Count < r.SampleLimit) list.Add(s); }
}
