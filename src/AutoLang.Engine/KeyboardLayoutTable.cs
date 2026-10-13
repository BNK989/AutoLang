using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoLang.Engine;

/// <summary>A physical key in a shift state. Virtual-key codes are Windows VK_* values but the engine treats them as opaque ids.</summary>
public readonly record struct Key(uint Vk, bool Shift);

/// <summary>
/// What each key produces on one installed keyboard layout. The host builds this from
/// <c>ToUnicodeEx</c> at runtime; tests and the eval harness load the same shape from JSON that the
/// host dumped. Nothing in here knows what Hebrew or English is.
/// </summary>
public sealed class KeyboardLayoutTable
{
    private readonly Dictionary<Key, string> _keyToText;
    private readonly Dictionary<char, Key> _charToKey;

    public string Name { get; }
    public ushort LangId { get; }

    /// <summary>Letters this layout can produce (either shift state). Defines the layout's "script" for scoring.</summary>
    public IReadOnlySet<char> Letters { get; }

    /// <summary>
    /// The alphabet this layout types ("Latin", "Hebrew", "Greek", ...): the majority script of its <em>unshifted</em>
    /// letters. Unshifted only, because some layouts put another script on Shift (Hebrew Standard types Latin
    /// capitals). Layouts with the same script form one group and are never switched between automatically.
    /// </summary>
    public string Script { get; }

    public KeyboardLayoutTable(string name, ushort langId, IEnumerable<KeyValuePair<Key, string>> keys)
    {
        Name = name;
        LangId = langId;
        _keyToText = new Dictionary<Key, string>();
        _charToKey = new Dictionary<char, Key>();
        var letters = new HashSet<char>();
        var unshifted = new List<char>();
        foreach (var (key, text) in keys)
        {
            if (string.IsNullOrEmpty(text)) continue;
            _keyToText[key] = text;
            if (text.Length == 1)
            {
                var c = text[0];
                // Prefer the unshifted key when a char is reachable both ways.
                if (!_charToKey.TryGetValue(c, out var existing) || (existing.Shift && !key.Shift)) _charToKey[c] = key;
                if (char.IsLetter(c)) { letters.Add(c); if (!key.Shift) unshifted.Add(c); }
            }
        }
        Letters = letters;
        Script = Scripts.Majority(unshifted.Count > 0 ? unshifted : letters);
    }

    public string? TextFor(Key key) => _keyToText.TryGetValue(key, out var t) ? t : null;

    public bool TryGetKey(char c, out Key key) => _charToKey.TryGetValue(c, out key);

    /// <summary>
    /// Re-read <paramref name="typed"/> (text that appeared under this layout) as if the same keys had been
    /// pressed under <paramref name="target"/>. Characters this layout cannot produce pass through unchanged.
    /// </summary>
    public string TransliterateTo(KeyboardLayoutTable target, string typed)
    {
        var sb = new StringBuilder(typed.Length);
        foreach (var c in typed)
        {
            if (_charToKey.TryGetValue(c, out var key) && target.TextFor(key) is { } t) sb.Append(t);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>True if the key types a letter (or apostrophe, which is part of words in both of our first scripts) on this layout.</summary>
    public bool IsWordKey(Key key)
    {
        var t = TextFor(key);
        return t is { Length: 1 } && (char.IsLetter(t[0]) || t[0] == '\'');
    }

    // ---- JSON (the host's dump format) ----

    private sealed record Dto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("langId")] ushort LangId,
        [property: JsonPropertyName("keys")] List<KeyDto> Keys);

    private sealed record KeyDto(
        [property: JsonPropertyName("vk")] uint Vk,
        [property: JsonPropertyName("shift")] bool Shift,
        [property: JsonPropertyName("text")] string Text);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string ToJson()
    {
        var keys = _keyToText.OrderBy(k => k.Key.Vk).ThenBy(k => k.Key.Shift)
            .Select(k => new KeyDto(k.Key.Vk, k.Key.Shift, k.Value)).ToList();
        return JsonSerializer.Serialize(new Dto(Name, LangId, keys), JsonOptions);
    }

    public static KeyboardLayoutTable FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions) ?? throw new InvalidDataException("empty layout json");
        return new KeyboardLayoutTable(dto.Name, dto.LangId,
            dto.Keys.Select(k => new KeyValuePair<Key, string>(new Key(k.Vk, k.Shift), k.Text)));
    }

    public static KeyboardLayoutTable Load(string path) => FromJson(File.ReadAllText(path));

    public override string ToString() => $"{Name} (0x{LangId:X4}, {Script})";
}
