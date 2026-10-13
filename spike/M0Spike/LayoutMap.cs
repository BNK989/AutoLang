using System.Globalization;
using System.Text;

namespace M0Spike;

/// <summary>
/// The key→character table of one installed keyboard layout, read from Windows at runtime
/// via ToUnicodeEx. Nothing language-specific lives here: whatever layouts the user has
/// installed are what we transliterate between.
/// </summary>
internal sealed class LayoutMap
{
    public readonly record struct Key(uint Vk, bool Shift);

    public nint Hkl { get; }
    public string Name { get; }
    public ushort LangId => (ushort)((long)Hkl & 0xFFFF);

    private readonly Dictionary<Key, string> _keyToText = new();
    private readonly Dictionary<char, Key> _charToKey = new();

    /// <summary>Virtual keys we probe: letters, digits and the OEM punctuation keys.</summary>
    private static readonly uint[] ProbedVks = BuildProbedVks();

    private static uint[] BuildProbedVks()
    {
        var list = new List<uint>();
        for (uint vk = 0x30; vk <= 0x39; vk++) list.Add(vk); // 0-9
        for (uint vk = 0x41; vk <= 0x5A; vk++) list.Add(vk); // A-Z
        list.AddRange(new uint[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE, 0xE2 }); // OEM_1..OEM_102
        return list.ToArray();
    }

    private LayoutMap(nint hkl)
    {
        Hkl = hkl;
        string culture;
        try { culture = CultureInfo.GetCultureInfo(LangId).DisplayName; }
        catch { culture = $"lang 0x{LangId:X4}"; }
        Name = $"{culture} [hkl 0x{(long)hkl:X16}]";

        foreach (var vk in ProbedVks)
        {
            foreach (var shift in new[] { false, true })
            {
                var text = Probe(vk, shift);
                if (text is null) continue;
                var key = new Key(vk, shift);
                _keyToText[key] = text;
                // Prefer the unshifted key when the same char is reachable both ways.
                if (text.Length == 1 && !_charToKey.ContainsKey(text[0])) _charToKey[text[0]] = key;
            }
        }
    }

    private string? Probe(uint vk, bool shift)
    {
        var scan = Native.MapVirtualKeyExW(vk, Native.MAPVK_VK_TO_VSC, Hkl);
        if (scan == 0) return null;
        var state = new byte[256];
        if (shift) state[Native.VK_SHIFT] = 0x80;
        var sb = new StringBuilder(8);
        int rc = Native.ToUnicodeEx(vk, scan, state, sb, sb.Capacity, Native.TOUNICODE_NO_STATE_CHANGE, Hkl);
        if (rc <= 0) return null; // 0 = no char, <0 = dead key — both unmapped for the spike
        var s = sb.ToString(0, rc);
        return s.Length > 0 && !char.IsControl(s[0]) ? s : null;
    }

    public static LayoutMap[] Installed()
    {
        int n = Native.GetKeyboardLayoutList(0, null);
        var hkls = new nint[n];
        Native.GetKeyboardLayoutList(n, hkls);
        return hkls.Select(h => new LayoutMap(h)).ToArray();
    }

    public bool TryGetKey(char c, out Key key) => _charToKey.TryGetValue(c, out key);
    public string? TextFor(Key key) => _keyToText.TryGetValue(key, out var t) ? t : null;

    /// <summary>
    /// Re-read <paramref name="typed"/> as if the same physical keys had been pressed with
    /// <paramref name="target"/> active. Characters not on this layout pass through unchanged.
    /// </summary>
    public string TransliterateTo(LayoutMap target, string typed)
    {
        var sb = new StringBuilder(typed.Length);
        foreach (var c in typed)
        {
            if (TryGetKey(c, out var key) && target.TextFor(key) is { } t) sb.Append(t);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    public void Dump(TextWriter w)
    {
        w.WriteLine($"== {Name}");
        foreach (var vk in ProbedVks)
        {
            var plain = TextFor(new Key(vk, false));
            var shifted = TextFor(new Key(vk, true));
            if (plain is null && shifted is null) continue;
            w.WriteLine($"  vk 0x{vk:X2} {VkLabel(vk),-8} -> '{plain ?? " "}'   shift -> '{shifted ?? " "}'");
        }
    }

    /// <summary>Same shape the engine's KeyboardLayoutTable.FromJson reads: {name, langId, keys:[{vk, shift, text}]}.</summary>
    public string ToJson()
    {
        var keys = _keyToText.OrderBy(k => k.Key.Vk).ThenBy(k => k.Key.Shift)
            .Select(k => new { vk = k.Key.Vk, shift = k.Key.Shift, text = k.Value });
        return System.Text.Json.JsonSerializer.Serialize(new { name = Name, langId = LangId, keys }, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string VkLabel(uint vk) => vk switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        0xBA => "OEM_1;", 0xBB => "OEM_+", 0xBC => "OEM_,", 0xBD => "OEM_-", 0xBE => "OEM_.",
        0xBF => "OEM_/", 0xC0 => "OEM_`", 0xDB => "OEM_[", 0xDC => "OEM_\\", 0xDD => "OEM_]",
        0xDE => "OEM_'", 0xE2 => "OEM_102", _ => $"0x{vk:X2}"
    };
}
