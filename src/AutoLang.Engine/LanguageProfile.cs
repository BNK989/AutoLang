namespace AutoLang.Engine;

/// <summary>Language-specific hard knowledge that can't be learned from a word list. Optional per language.</summary>
public interface ILanguageRules
{
    /// <summary>True if no word of this language can look like this (e.g. a Hebrew final form mid-word).</summary>
    bool IsImpossible(string word);
}

/// <summary>Hebrew: final forms ך ם ן ף ץ occur only as the last letter of a word.</summary>
public sealed class HebrewRules : ILanguageRules
{
    public static readonly HebrewRules Instance = new();

    public bool IsImpossible(string word)
    {
        for (int i = 0; i < word.Length - 1; i++)
        {
            if (IsFinal(word[i]))
            {
                // Allow a final letter before a trailing apostrophe/geresh or quote ("ך'" is rare but legal in abbreviations).
                bool restIsPunct = true;
                for (int j = i + 1; j < word.Length; j++) if (char.IsLetter(word[j])) { restIsPunct = false; break; }
                if (!restIsPunct) return true;
            }
        }
        return false;
    }

    public static bool IsFinal(char c) => c is 'ך' or 'ם' or 'ן' or 'ף' or 'ץ';
}

/// <summary>
/// Greek: final sigma ς occurs only at the end of a word. (The converse, medial σ at the end, is not a rule here:
/// all-caps ΣΑΣ lower-cases to σασ, so it would flag correctly typed capitals.)
/// </summary>
public sealed class GreekRules : ILanguageRules
{
    public static readonly GreekRules Instance = new();

    public bool IsImpossible(string word)
    {
        int end = word.Length;
        while (end > 0 && !char.IsLetter(word[end - 1])) end--;
        for (int i = 0; i < end - 1; i++) if (word[i] == 'ς') return true;
        return false;
    }
}

/// <summary>Everything the scorer needs to judge "is this a word of language X": dictionary, char LM, normalizer, rules.</summary>
public sealed class LanguageProfile
{
    public ushort LangId { get; }
    public string Name { get; }
    public WordDictionary Dictionary { get; }
    public CharNGramModel Model { get; }
    public ILanguageRules? Rules { get; }
    public Func<string, string> Normalize { get; }

    public LanguageProfile(ushort langId, string name, WordDictionary dictionary, CharNGramModel model,
        ILanguageRules? rules = null, Func<string, string>? normalize = null)
    {
        LangId = langId;
        Name = name;
        Dictionary = dictionary;
        Model = model;
        Rules = rules;
        Normalize = normalize ?? (s => s);
    }

    /// <summary>
    /// Build a profile from a frequency list: the dictionary is the list, the LM is trained on it.
    /// LM weights are damped (default ln(1+count)): with raw token counts Witten-Bell's novel-event mass
    /// (types / (tokens + types)) collapses and every unseen n-gram costs ~15 nats, which made rare real words
    /// lose to gibberish. Apostrophes are stripped before training so the LM never sees them (see PhraseDecider).
    /// </summary>
    public static LanguageProfile FromFrequencyFile(ushort langId, string name, string path, ILanguageRules? rules = null,
        Func<string, string>? normalize = null, int order = 5, int maxEntries = int.MaxValue, Func<double, double>? lmWeight = null)
        => FromFrequencyLines(langId, name, File.ReadLines(path), rules, normalize, order, maxEntries, lmWeight);

    /// <summary><see cref="FromFrequencyFile"/> for a list that is not a file on disk (the host's are embedded in the exe).</summary>
    public static LanguageProfile FromFrequencyLines(ushort langId, string name, IEnumerable<string> lines, ILanguageRules? rules = null,
        Func<string, string>? normalize = null, int order = 5, int maxEntries = int.MaxValue, Func<double, double>? lmWeight = null)
    {
        // Supplement first, so a maxEntries cut never drops it.
        var dict = WordDictionary.FromFrequencyLines(WordSupplements.Lines(langId).Concat(lines), normalize, maxEntries);
        lmWeight ??= DefaultLmWeight;
        var lm = new CharNGramModel(order);
        foreach (var (w, c) in dict.Entries)
        {
            var stripped = w.Replace("'", "");
            if (stripped.Length > 0) lm.Add(stripped, lmWeight(c));
        }
        return new LanguageProfile(langId, name, dict, lm, rules, normalize);
    }

    public static double DefaultLmWeight(double count) => Math.Log(1 + count);

    /// <summary>Rules we know how to attach by language id. The host passes whatever layouts are installed; unknown ids get no rules.</summary>
    public static ILanguageRules? DefaultRulesFor(ushort langId) => (langId & 0xFF) switch
    {
        0x0D => HebrewRules.Instance, // he
        0x08 => GreekRules.Instance,  // el
        _ => null,
    };

    /// <summary>
    /// Lower-case for every language (the downloadable word lists are lower-case; scripts without case are unchanged
    /// by it). Hebrew keeps the identity so its tuned scores stay exactly as measured (PLAN.md §4).
    /// </summary>
    public static Func<string, string>? DefaultNormalizeFor(ushort langId) => (langId & 0xFF) switch
    {
        0x0D => null, // he
        _ => s => s.ToLowerInvariant(),
    };
}
