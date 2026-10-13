namespace AutoLang.Engine;

/// <summary>
/// Which alphabet a character belongs to, from Unicode block ranges. This is the grouping key for auto-switching
/// (PLAN.md §2): layouts that type the same script are one group and are never switched between automatically.
/// The table names Unicode scripts, not languages; a block it doesn't list is named by its range so two layouts
/// in the same unlisted script still group together.
/// </summary>
public static class Scripts
{
    public const string Unknown = "Unknown";

    private static readonly (int First, int Last, string Script)[] Ranges =
    {
        (0x0041, 0x024F, "Latin"), (0x1E00, 0x1EFF, "Latin"), (0xFF21, 0xFF5A, "Latin"),
        (0x0370, 0x03FF, "Greek"), (0x1F00, 0x1FFF, "Greek"),
        (0x0400, 0x052F, "Cyrillic"), (0x1C80, 0x1C8F, "Cyrillic"), (0x2DE0, 0x2DFF, "Cyrillic"), (0xA640, 0xA69F, "Cyrillic"),
        (0x0530, 0x058F, "Armenian"),
        (0x0590, 0x05FF, "Hebrew"), (0xFB1D, 0xFB4F, "Hebrew"),
        (0x0600, 0x06FF, "Arabic"), (0x0750, 0x077F, "Arabic"), (0x08A0, 0x08FF, "Arabic"), (0xFB50, 0xFDFF, "Arabic"), (0xFE70, 0xFEFF, "Arabic"),
        (0x0700, 0x074F, "Syriac"), (0x0780, 0x07BF, "Thaana"),
        (0x0900, 0x097F, "Devanagari"), (0x0980, 0x09FF, "Bengali"), (0x0A00, 0x0A7F, "Gurmukhi"), (0x0A80, 0x0AFF, "Gujarati"),
        (0x0B00, 0x0B7F, "Oriya"), (0x0B80, 0x0BFF, "Tamil"), (0x0C00, 0x0C7F, "Telugu"), (0x0C80, 0x0CFF, "Kannada"),
        (0x0D00, 0x0D7F, "Malayalam"), (0x0D80, 0x0DFF, "Sinhala"), (0x0E00, 0x0E7F, "Thai"), (0x0E80, 0x0EFF, "Lao"),
        (0x0F00, 0x0FFF, "Tibetan"), (0x1000, 0x109F, "Myanmar"), (0x10A0, 0x10FF, "Georgian"), (0x1C90, 0x1CBF, "Georgian"),
        (0x1100, 0x11FF, "Hangul"), (0x3130, 0x318F, "Hangul"), (0xAC00, 0xD7AF, "Hangul"),
        (0x1200, 0x139F, "Ethiopic"), (0x1780, 0x17FF, "Khmer"), (0x1800, 0x18AF, "Mongolian"),
        (0x3040, 0x309F, "Hiragana"), (0x30A0, 0x30FF, "Katakana"), (0x4E00, 0x9FFF, "Han"),
    };

    /// <summary>Script of one character, or null for anything that is not a letter.</summary>
    public static string? Of(char c)
    {
        if (!char.IsLetter(c)) return null;
        foreach (var (first, last, script) in Ranges)
            if (c >= first && c <= last) return script;
        return $"U+{c & 0xFF80:X4}"; // unlisted block: its 128-char range is still a stable group key
    }

    /// <summary>The script most of <paramref name="letters"/> belong to (ties: the lexically first, for determinism).</summary>
    public static string Majority(IEnumerable<char> letters)
    {
        var counts = new Dictionary<string, int>();
        foreach (var c in letters)
            if (Of(c) is { } s) counts[s] = counts.GetValueOrDefault(s) + 1;
        if (counts.Count == 0) return Unknown;
        return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
    }
}
