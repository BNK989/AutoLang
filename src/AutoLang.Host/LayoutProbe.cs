using System.Globalization;
using System.Text;
using AutoLang.Engine;

namespace AutoLang.Host;

/// <summary>One of the user's installed layouts: the Windows handle plus the engine's view of its keys.</summary>
public sealed record InstalledLayout(nint Hkl, KeyboardLayoutTable Table)
{
    public ushort LangId => Table.LangId;
    public override string ToString() => $"{Table.Name} hkl 0x{(long)Hkl:X16}";
}

/// <summary>
/// Reads the key→character table of every installed layout from Windows at runtime via ToUnicodeEx. Nothing
/// language-specific lives here: whatever the user has installed is what the host transliterates between.
/// </summary>
internal static class LayoutProbe
{
    private static readonly uint[] ProbedVks = BuildProbedVks();

    private static uint[] BuildProbedVks()
    {
        var list = new List<uint>();
        for (uint vk = 0x30; vk <= 0x39; vk++) list.Add(vk); // 0-9
        for (uint vk = 0x41; vk <= 0x5A; vk++) list.Add(vk); // A-Z
        list.AddRange(new uint[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE, 0xE2 }); // OEM_1..OEM_102
        return list.ToArray();
    }

    public static InstalledLayout[] Installed()
    {
        int n = Native.GetKeyboardLayoutList(0, null);
        var hkls = new nint[n];
        Native.GetKeyboardLayoutList(n, hkls);
        return hkls.Select(Build).ToArray();
    }

    public static ushort LangIdOf(nint hkl) => (ushort)((long)hkl & 0xFFFF);

    /// <summary>ISO 639-1 code of a language id ("he", "en"), used to find the bundled word list.</summary>
    public static string IsoCode(ushort langId)
    {
        try { return CultureInfo.GetCultureInfo(langId).TwoLetterISOLanguageName; }
        catch { return $"{langId:x4}"; }
    }

    private static InstalledLayout Build(nint hkl)
    {
        var langId = LangIdOf(hkl);
        string name;
        try { name = CultureInfo.GetCultureInfo(langId).DisplayName; }
        catch { name = $"lang 0x{langId:X4}"; }

        var keys = new List<KeyValuePair<Key, string>>();
        var state = new byte[256];
        var sb = new StringBuilder(8);
        foreach (var vk in ProbedVks)
        {
            var scan = Native.MapVirtualKeyExW(vk, Native.MAPVK_VK_TO_VSC, hkl);
            if (scan == 0) continue;
            foreach (var shift in new[] { false, true })
            {
                Array.Clear(state);
                if (shift) state[Native.VK_SHIFT] = 0x80;
                int rc = Native.ToUnicodeEx(vk, scan, state, sb, sb.Capacity, Native.TOUNICODE_NO_STATE_CHANGE, hkl);
                if (rc <= 0) continue; // 0 = no char, <0 = dead key — unmapped
                var s = sb.ToString(0, rc);
                if (s.Length == 0 || char.IsControl(s[0])) continue;
                keys.Add(new KeyValuePair<Key, string>(new Key(vk, shift), s));
            }
        }
        return new InstalledLayout(hkl, new KeyboardLayoutTable(name, langId, keys));
    }
}
