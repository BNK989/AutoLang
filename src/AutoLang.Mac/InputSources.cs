using System.Globalization;
using AutoLang.Engine;
using static AutoLang.Mac.Native;

namespace AutoLang.Mac;

/// <summary>One enabled keyboard layout: the TIS input source (retained), its key layout data and the engine's view of it.</summary>
internal sealed record InputSource(string Id, nint Ref, nint KeyLayout, KeyboardLayoutTable Table)
{
    public ushort LangId => Table.LangId;
    public override string ToString() => $"{Table.Name} [{Id}]";
}

/// <summary>
/// The user's enabled keyboard layouts (System Settings → Keyboard → Input Sources), read from macOS at runtime with
/// UCKeyTranslate, the counterpart of the Windows host's LayoutProbe. Language ids are Windows LANGIDs derived from each
/// layout's language tag, so the engine and the word lists work unchanged. Layouts of the same language (Hebrew and
/// Hebrew-QWERTY, U.S. and ABC) share one id, like layout variants on Windows. Main thread only: TIS asserts otherwise.
/// </summary>
internal sealed class InputSources
{
    /// <summary>
    /// Mac virtual key codes of the character keys (ANSI letters, digits and punctuation, plus the ISO § key 0x0A),
    /// without Return 0x24, Tab 0x30 and Space 0x31. They stand in for Windows VKs; the engine treats both as opaque.
    /// </summary>
    private static readonly ushort[] ProbedKeys = Enumerable.Range(0x00, 0x33).Select(k => (ushort)k)
        .Where(k => k is not (0x24 or 0x30 or 0x31)).ToArray();

    private readonly Dictionary<string, InputSource> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, InputSource> _firstOfLang = new();
    /// <summary>The variant the user last typed with per language, so a switch lands on it.</summary>
    private readonly Dictionary<ushort, InputSource> _lastOfLang = new();
    private nint _cachedRef;
    private InputSource? _cached;

    /// <summary>Every enabled layout, in the order macOS lists them.</summary>
    public IReadOnlyList<InputSource> All { get; private set; } = [];

    /// <summary>One table per language: the first enabled layout of each.</summary>
    public KeyboardLayoutTable[] Tables => _firstOfLang.Values.Select(s => s.Table).ToArray();

    public IReadOnlyList<string> Scripts => Tables.Select(t => t.Script).Where(s => s != Engine.Scripts.Unknown).Distinct().ToArray();

    /// <summary>(Re)reads the enabled layouts. Main thread.</summary>
    public void Probe()
    {
        foreach (var s in All) CFRelease(s.Ref);
        _byId.Clear(); _firstOfLang.Clear(); _lastOfLang.Clear();
        _cachedRef = 0; _cached = null;

        var filter = CreateDictionary(
            (kTISPropertyInputSourceCategory, kTISCategoryKeyboardInputSource),
            (kTISPropertyInputSourceIsSelectCapable, kCFBooleanTrue));
        var list = TISCreateInputSourceList(filter, 0);
        CFRelease(filter);
        var all = new List<InputSource>();
        if (list != 0)
        {
            var layoutType = ToManaged(kTISTypeKeyboardLayout);
            for (nint i = 0, n = CFArrayGetCount(list); i < n; i++)
            {
                var src = CFArrayGetValueAtIndex(list, i);
                if (StringProperty(src, kTISPropertyInputSourceType) != layoutType) continue; // input methods (IMEs) have no fixed keys
                var data = TISGetInputSourceProperty(src, kTISPropertyUnicodeKeyLayoutData);
                if (data == 0) continue;
                var id = StringProperty(src, kTISPropertyInputSourceID);
                var lang = StringArray(TISGetInputSourceProperty(src, kTISPropertyInputSourceLanguages)).FirstOrDefault();
                var langId = LangIdOf(lang);
                if (langId == 0) { Log.Info($"input source {id}: language '{lang}' has no language id, ignored"); continue; }
                var layout = CFDataGetBytePtr(data);
                var table = BuildTable(StringProperty(src, kTISPropertyLocalizedName), langId, layout);
                var source = new InputSource(id, CFRetain(src), layout, table);
                all.Add(source);
                _byId[id] = source;
                _firstOfLang.TryAdd(langId, source);
            }
            CFRelease(list);
        }
        All = all;
    }

    /// <summary>"he" → 0x040D, "en" → 0x0409, "pt-BR" → 0x0416; 0 when .NET does not know the language.</summary>
    private static ushort LangIdOf(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return 0;
        try
        {
            var lcid = CultureInfo.CreateSpecificCulture(tag).LCID;
            return lcid is > 0 and < 0x10000 and not 0x1000 ? (ushort)lcid : (ushort)0; // 0x1000 = custom, unspecified
        }
        catch (CultureNotFoundException) { return 0; }
    }

    private static unsafe KeyboardLayoutTable BuildTable(string name, ushort langId, nint layout)
    {
        var keys = new List<KeyValuePair<Key, string>>();
        uint kbdType = LMGetKbdType();
        char* buf = stackalloc char[8];
        foreach (var code in ProbedKeys)
        {
            foreach (var shift in new[] { false, true })
            {
                uint dead = 0;
                nuint len = 0;
                int rc = UCKeyTranslate(layout, code, kUCKeyActionDown, shift ? ShiftModifier : 0, kbdType, 0, &dead, 8, &len, buf);
                if (rc != 0 || dead != 0 || len == 0) continue; // error, dead key or no char: unmapped
                var s = new string(buf, 0, (int)len);
                if (char.IsControl(s[0])) continue;
                keys.Add(new KeyValuePair<Key, string>(new Key(code, shift), s));
            }
        }
        if (string.IsNullOrEmpty(name))
        {
            try { name = CultureInfo.GetCultureInfo(langId).DisplayName; } catch (CultureNotFoundException) { name = $"lang 0x{langId:X4}"; }
        }
        return new KeyboardLayoutTable(name, langId, keys);
    }

    /// <summary>The layout the user is typing with now, or null for an input method or a layout we do not know. Main thread.</summary>
    public InputSource? Current()
    {
        var cur = TISCopyCurrentKeyboardInputSource();
        if (cur == 0) return null;
        try
        {
            if (cur == _cachedRef && _cached is not null) return _cached;
            _byId.TryGetValue(StringProperty(cur, kTISPropertyInputSourceID), out var s);
            _cachedRef = cur;
            _cached = s;
            if (s is not null) _lastOfLang[s.LangId] = s;
            return s;
        }
        finally { CFRelease(cur); }
    }

    /// <summary>Selects the layout of <paramref name="langId"/> the user last typed with. Main thread.</summary>
    public void Select(ushort langId)
    {
        var s = _lastOfLang.TryGetValue(langId, out var last) ? last : _firstOfLang.GetValueOrDefault(langId);
        if (s is null) return;
        int rc = TISSelectInputSource(s.Ref);
        if (rc != 0) Log.Warn($"TISSelectInputSource({s.Id}) failed: {rc}");
    }

    /// <summary>
    /// The character a key types on <paramref name="source"/>, or null for a dead key, no character or more than one.
    /// <paramref name="dead"/> reports a dead key, whose accent then combines with the next key.
    /// </summary>
    public static unsafe char? Translate(InputSource source, ushort keyCode, bool shift, bool caps, out bool dead)
    {
        uint state = 0;
        nuint len = 0;
        char* buf = stackalloc char[4];
        uint mods = (shift ? ShiftModifier : 0) | (caps ? AlphaLockModifier : 0);
        int rc = UCKeyTranslate(source.KeyLayout, keyCode, kUCKeyActionDown, mods, LMGetKbdType(), 0, &state, 4, &len, buf);
        dead = rc == 0 && state != 0;
        if (rc != 0 || dead || len != 1) return null;
        return buf[0];
    }
}
