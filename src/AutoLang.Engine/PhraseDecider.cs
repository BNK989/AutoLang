namespace AutoLang.Engine;

public enum DecisionKind { Keep, Correct, Defer }

/// <summary>Why the decider ruled as it did, as a code: <see cref="Score"/> means the margin decided; the rest are early exits.</summary>
public enum DecisionCause { Score, Empty, Address, NoProfile, NoWords }

/// <param name="Replacement">Full run text re-read under the other layout (only for Correct).</param>
/// <param name="Margin">score(other) − score(typed): positive means the other layout reads better.</param>
public sealed record Decision(DecisionKind Kind, KeyboardLayoutTable? TargetLayout, string? Replacement, double Margin, string Reason)
{
    // Diagnostics for the host's log: counts and scores only, never text.
    public DecisionCause Cause { get; init; }
    /// <summary>Letters that were scored (tokens that read the same under both layouts, or are no word anywhere, are not).</summary>
    public int Letters { get; init; }
    /// <summary>Scored words, and how many of them the typed / other reading's dictionary knows.</summary>
    public int Words { get; init; }
    public int TypedKnown { get; init; }
    public int OtherKnown { get; init; }
    /// <summary>The margin a correction needed (THigh, raised for short runs).</summary>
    public double Threshold { get; init; }

    public static Decision Keep(double margin, string reason) => new(DecisionKind.Keep, null, null, margin, reason);
    public static Decision Defer(double margin, string reason) => new(DecisionKind.Defer, null, null, margin, reason);
}

/// <summary>Tunable knobs. Defaults were set by tools/AutoLang.Eval --sweep on Tatoeba he/en (2026-10-12), see PLAN.md §4.</summary>
public sealed record EngineOptions
{
    /// <summary>Correct when margin exceeds this (nats).</summary>
    public double THigh { get; init; } = 10;
    /// <summary>Below this |margin| a short run is deferred instead of kept.</summary>
    public double TLow { get; init; } = 4;
    /// <summary>Runs with fewer letters than this are eligible for Defer.</summary>
    public int MinDecisiveLetters { get; init; } = 5;
    /// <summary>Bonus for a reading whose word is in that language's dictionary.</summary>
    public double DictBonus { get; init; } = 2;
    /// <summary>Scale on ln(relative frequency) added for dictionary words (0 = membership only).</summary>
    public double DictFreqWeight { get; init; } = 0.25;
    /// <summary>Extra margin required per letter the run is short of MinDecisiveLetters (short runs collide more).</summary>
    public double ShortRunPerLetter { get; init; } = 0;
    /// <summary>Added on top of the LM score when a reading breaks a hard rule (finite so both-impossible nets out).</summary>
    public double ImpossiblePenalty { get; init; } = 30;
    /// <summary>Penalty per punctuation char inside a word (apostrophe/hyphen excluded).</summary>
    public double InnerPunctPenalty { get; init; } = 6;
    /// <summary>Margin shift from context (previous confirmed language in this input).</summary>
    public double ContextPrior { get; init; } = 3;
}

public sealed record DecisionContext(ushort? PreviousConfirmedLangId = null)
{
    public static readonly DecisionContext None = new();
}

/// <summary>
/// The decision: given a run of text as it appeared under the active layout, does it read better as the
/// same keystrokes under the other installed layout? Pure function of (text, layouts, profiles, options).
/// </summary>
public sealed class PhraseDecider
{
    private readonly IReadOnlyDictionary<ushort, LanguageProfile> _profiles;
    private readonly EngineOptions _opt;

    public PhraseDecider(IEnumerable<LanguageProfile> profiles, EngineOptions? options = null)
    {
        _profiles = profiles.ToDictionary(p => p.LangId);
        _opt = options ?? new EngineOptions();
    }

    public EngineOptions Options => _opt;

    public Decision Decide(string typed, KeyboardLayoutTable typedLayout, KeyboardLayoutTable otherLayout, DecisionContext? ctx = null)
    {
        ctx ??= DecisionContext.None;
        if (typed.Length == 0) return Decision.Keep(0, "empty") with { Cause = DecisionCause.Empty };
        if (typed.Contains('@') || typed.Contains("://")) return Decision.Keep(0, "address") with { Cause = DecisionCause.Address };
        if (!_profiles.TryGetValue(typedLayout.LangId, out var typedProfile) || !_profiles.TryGetValue(otherLayout.LangId, out var otherProfile))
            return Decision.Keep(0, "no profile") with { Cause = DecisionCause.NoProfile };

        var tokens = Tokenizer.Split(typed, typedLayout, otherLayout);
        if (tokens.Count == 0) return Decision.Keep(0, "no words") with { Cause = DecisionCause.NoWords };

        double typedScore = 0, otherScore = 0;
        int letters = 0, words = 0, typedKnown = 0, otherKnown = 0;
        foreach (var t in tokens)
        {
            var remapped = typedLayout.TransliterateTo(otherLayout, t.Text);
            // A token that reads the same under both layouts (digits, shifted Latin capitals under Hebrew, "XXX")
            // says nothing about which layout was meant: it must not tilt the margin either way.
            if (remapped == t.Text) continue;
            // Neither is a token that is no word in any language: one letter repeated (laughter "חחח", "jjj") or letters
            // glued to digits (user names, codes: "bnk989"). Both readings score as gibberish, and whichever happens to
            // be a short dictionary word ("jj", "נמל") would win.
            if (IsRepeatedLetter(t.Text) || TouchesDigit(typed, t)) continue;
            typedScore += Score(t.Text, typedProfile, out bool tk);
            otherScore += Score(remapped, otherProfile, out bool ok);
            letters += t.Text.Count(char.IsLetter);
            words++;
            if (tk) typedKnown++;
            if (ok) otherKnown++;
        }

        double margin = otherScore - typedScore;
        if (ctx.PreviousConfirmedLangId == otherLayout.LangId) margin += _opt.ContextPrior;
        else if (ctx.PreviousConfirmedLangId == typedLayout.LangId) margin -= _opt.ContextPrior;

        double tHigh = _opt.THigh + _opt.ShortRunPerLetter * Math.Max(0, _opt.MinDecisiveLetters - letters);
        var decision = margin > tHigh
            ? new Decision(DecisionKind.Correct, otherLayout, typedLayout.TransliterateTo(otherLayout, typed), margin, $"margin {margin:F1} > {tHigh}")
            : letters < _opt.MinDecisiveLetters && Math.Abs(margin) < _opt.TLow
                ? Decision.Defer(margin, $"short run, |margin| {Math.Abs(margin):F1} < {_opt.TLow}")
                : Decision.Keep(margin, $"margin {margin:F1}");
        return decision with { Letters = letters, Words = words, TypedKnown = typedKnown, OtherKnown = otherKnown, Threshold = tHigh };
    }

    private static bool IsRepeatedLetter(string token)
    {
        if (token.Length < 2 || !char.IsLetter(token[0])) return false;
        var first = char.ToLowerInvariant(token[0]);
        for (int i = 1; i < token.Length; i++) if (char.ToLowerInvariant(token[i]) != first) return false;
        return true;
    }

    private static bool TouchesDigit(string typed, Token t)
        => (t.Start > 0 && char.IsDigit(typed[t.Start - 1])) || (t.Start + t.Length < typed.Length && char.IsDigit(typed[t.Start + t.Length]));

    /// <summary>How much does <paramref name="word"/> look like a word of <paramref name="p"/>? Higher is better; in nats-ish units.</summary>
    public double ScoreWord(string word, LanguageProfile p, KeyboardLayoutTable layout) => Score(word, p, out _);

    /// <param name="known">True when the word is in <paramref name="p"/>'s dictionary.</param>
    private double Score(string word, LanguageProfile p, out bool known)
    {
        known = false;
        var w = p.Normalize(word);
        // Trailing punctuation is normal in any language ("hello," / "שלום,") — strip it; inner punctuation is suspicious.
        int end = w.Length;
        while (end > 0 && !char.IsLetterOrDigit(w[end - 1]) && w[end - 1] != '\'') end--;
        w = w[..end];
        if (w.Length == 0) return 0;

        // Apostrophes are legal inside words in both scripts (don't, ג'ים) but absent from the word lists: score without them.
        var bare = w.Replace("'", "");
        if (bare.Length == 0) return 0;

        double s = p.Model.LogProb(bare);
        if (p.Rules?.IsImpossible(bare) == true) return s - _opt.ImpossiblePenalty;

        int inner = 0;
        for (int i = 0; i < bare.Length; i++)
            if (!char.IsLetter(bare[i]) && bare[i] != '-') inner++;
        s -= inner * _opt.InnerPunctPenalty;
        // A letter the layout can't type (Latin capital in a Hebrew reading comes from Shift; fine) — no extra rule for now.
        var dictKey = p.Dictionary.Contains(w) ? w : p.Dictionary.Contains(bare) ? bare : null;
        known = dictKey is not null;
        if (dictKey is not null) s += _opt.DictBonus + _opt.DictFreqWeight * (p.Dictionary.LogFreq(dictKey) + 10); // +10 ≈ centre of ln(rel.freq) range
        return s;
    }
}
