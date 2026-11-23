using System.Runtime.InteropServices;
using AutoLang.Engine;

namespace AutoLang.Linux;

/// <summary>
/// One XKB layout ("us", "il", "ru", "us+intl") compiled with libxkbcommon: the Linux counterpart of ToUnicodeEx and
/// UCKeyTranslate. Key ids in the engine's tables are evdev key codes, the codes IBus hands an engine (X keycode − 8);
/// the engine treats them as opaque, like Windows VKs and Mac key codes. Not thread-safe: the engine translates on its
/// D-Bus thread only.
/// </summary>
internal sealed class XkbLayout : IDisposable
{
    private const string Lib = "libxkbcommon.so.0";

    /// <summary>The character keys of a PC keyboard (evdev codes): digits row, three letter rows, grave, backslash, ISO &lt;LSGT&gt;.</summary>
    public static readonly ushort[] CharacterKeys =
        Enumerable.Range(2, 12)          // 1..0 - =
        .Concat(Enumerable.Range(16, 12)) // q..]
        .Concat(Enumerable.Range(30, 12)) // a..' `
        .Concat(Enumerable.Range(43, 11)) // \ z../
        .Append(86)                       // the extra ISO key left of Z
        .Select(k => (ushort)k).ToArray();

    public const ushort KeySpace = 57;

    private readonly nint _context, _keymap, _state;
    private readonly uint _shiftMask, _lockMask;

    public string Id { get; }
    public KeyboardLayoutTable Table { get; }

    public XkbLayout(string id, ushort langId, string name)
    {
        Id = id;
        var (layout, variant) = Split(id);
        _context = xkb_context_new(0);
        if (_context == 0) throw new InvalidOperationException("xkb_context_new failed");
        var names = new RuleNames
        {
            Rules = Marshal.StringToHGlobalAnsi("evdev"),
            Model = Marshal.StringToHGlobalAnsi("pc105"),
            Layout = Marshal.StringToHGlobalAnsi(layout),
            Variant = Marshal.StringToHGlobalAnsi(variant),
            Options = 0,
        };
        try { _keymap = xkb_keymap_new_from_names(_context, ref names, 0); }
        finally
        {
            Marshal.FreeHGlobal(names.Rules); Marshal.FreeHGlobal(names.Model);
            Marshal.FreeHGlobal(names.Layout); Marshal.FreeHGlobal(names.Variant);
        }
        if (_keymap == 0) throw new InvalidOperationException($"xkb: no keymap for layout '{id}'");
        _state = xkb_state_new(_keymap);
        _shiftMask = 1u << (int)xkb_keymap_mod_get_index(_keymap, "Shift");
        _lockMask = 1u << (int)xkb_keymap_mod_get_index(_keymap, "Lock");

        var keys = new List<KeyValuePair<Key, string>>();
        foreach (var code in CharacterKeys)
            foreach (var shift in new[] { false, true })
                if (Translate(code, shift, caps: false) is { } c)
                    keys.Add(new KeyValuePair<Key, string>(new Key(code, shift), c.ToString()));
        Table = new KeyboardLayoutTable(name, langId, keys);
    }

    /// <summary>The character the key types with these modifiers, or null (no character, a control character, or more than one UTF-16 unit).</summary>
    public char? Translate(ushort evdevCode, bool shift, bool caps)
    {
        xkb_state_update_mask(_state, shift ? _shiftMask : 0, 0, caps ? _lockMask : 0, 0, 0, 0);
        uint cp = xkb_state_key_get_utf32(_state, (uint)evdevCode + 8);
        if (cp == 0 || cp > 0xFFFF) return null;
        var c = (char)cp;
        return char.IsControl(c) ? null : c;
    }

    /// <summary>"us+intl" (GNOME's form) or "us(intl)" (XKB's) → ("us", "intl").</summary>
    public static (string Layout, string Variant) Split(string id)
    {
        int plus = id.IndexOf('+');
        if (plus > 0) return (id[..plus], id[(plus + 1)..]);
        int paren = id.IndexOf('(');
        if (paren > 0 && id.EndsWith(')')) return (id[..paren], id[(paren + 1)..^1]);
        return (id, "");
    }

    public void Dispose()
    {
        if (_state != 0) xkb_state_unref(_state);
        if (_keymap != 0) xkb_keymap_unref(_keymap);
        if (_context != 0) xkb_context_unref(_context);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RuleNames { public nint Rules, Model, Layout, Variant, Options; }

    [DllImport(Lib)] private static extern nint xkb_context_new(int flags);
    [DllImport(Lib)] private static extern void xkb_context_unref(nint context);
    [DllImport(Lib)] private static extern nint xkb_keymap_new_from_names(nint context, ref RuleNames names, int flags);
    [DllImport(Lib)] private static extern void xkb_keymap_unref(nint keymap);
    [DllImport(Lib)] private static extern uint xkb_keymap_mod_get_index(nint keymap, [MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport(Lib)] private static extern nint xkb_state_new(nint keymap);
    [DllImport(Lib)] private static extern void xkb_state_unref(nint state);
    [DllImport(Lib)] private static extern int xkb_state_update_mask(nint state, uint depressedMods, uint latchedMods, uint lockedMods,
        uint depressedLayout, uint latchedLayout, uint lockedLayout);
    [DllImport(Lib)] private static extern uint xkb_state_key_get_utf32(nint state, uint keycode);
}
