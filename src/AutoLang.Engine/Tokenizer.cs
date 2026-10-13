namespace AutoLang.Engine;

/// <summary>A maximal run of word keys in the typed text, with its position so the host can map decisions back.</summary>
public readonly record struct Token(string Text, int Start)
{
    public int Length => Text.Length;
}

/// <summary>
/// Splits typed text into words the layout-aware way: a character belongs to a word if the key that
/// produced it is a letter on <em>any</em> of the layouts in play. Under Hebrew, the ',' key is 'ת' and
/// '.' is 'ץ', so `,ush` (= תודה) must stay one token even though it looks like punctuation in Latin.
/// </summary>
public static class Tokenizer
{
    public static List<Token> Split(string typed, KeyboardLayoutTable typedLayout, params KeyboardLayoutTable[] others)
    {
        var tokens = new List<Token>();
        int start = -1;
        for (int i = 0; i < typed.Length; i++)
        {
            bool word = IsWordChar(typed[i], typedLayout, others);
            if (word && start < 0) start = i;
            else if (!word && start >= 0) { tokens.Add(new Token(typed[start..i], start)); start = -1; }
        }
        if (start >= 0) tokens.Add(new Token(typed[start..], start));
        return tokens;
    }

    public static bool IsWordChar(char c, KeyboardLayoutTable typedLayout, KeyboardLayoutTable[] others)
    {
        if (!typedLayout.TryGetKey(c, out var key))
            return char.IsLetter(c) || c == '\''; // not produced by this layout (pasted?) — judge by the char itself
        if (typedLayout.IsWordKey(key)) return true;
        foreach (var o in others) if (o.IsWordKey(key)) return true;
        return false;
    }
}
