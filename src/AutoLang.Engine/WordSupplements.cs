namespace AutoLang.Engine;

/// <summary>
/// Words a language's frequency list misses. The FrequencyWords lists come from film subtitles, so chat and work
/// shorthand (tbh, idk, imo, ux…) is rare or absent, and a short English run with no dictionary hit loses to whatever it
/// spells on the other layout ("tbh" is אני on Hebrew keys). Kept here rather than edited into data/words, which stay
/// as published, and merged into every profile so it also applies to a downloaded list.
/// </summary>
public static class WordSupplements
{
    /// <summary>Count given to each supplement word: about rank 10–15k in en_50k, a common but not dominant word.</summary>
    public const long Count = 3000;

    private static readonly string[] English =
    [
        "tbh", "idk", "idc", "imo", "imho", "sth", "smth", "ux", "ui", "ngl", "afaik", "iirc", "irl", "tbf", "fwiw",
        "tldr", "wfh", "lmk", "nvm", "brb", "ttyl", "hmu", "omw", "thx", "pls", "plz", "np", "ofc", "ikr", "wtf",
        "lmao", "ppl", "btw", "lol", "omg", "fyi", "asap", "faq", "api", "url", "tba", "tbd", "eod", "wip", "pov",
        "fomo", "diy", "hbu", "wbu", "bday", "tmrw", "imma", "kinda", "sorta",
    ];

    /// <summary>The supplement for <paramref name="langId"/> as "word count" lines, the format of the lists it joins.</summary>
    public static IEnumerable<string> Lines(ushort langId) => (langId & 0xFF) switch
    {
        0x09 => English.Select(w => $"{w} {Count}"),
        _ => [],
    };
}
