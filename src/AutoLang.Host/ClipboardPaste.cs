using System.Runtime.InteropServices;
using static AutoLang.Host.Native;

namespace AutoLang.Host;

/// <summary>
/// The Paste typing method (PLAN.md §3.3): put the replacement on the clipboard, press Ctrl+V, put the user's clipboard
/// back. Used only for apps the user switched to it, because it briefly takes over the clipboard. Guarantees:
/// <list type="bullet">
/// <item>The user's clipboard comes back format for format. If it holds something that cannot be copied faithfully,
/// or is too large, this returns null and the caller types with Unicode instead: losing a copied item is worse than
/// a mistyped word.</item>
/// <item>The replacement is marked for clipboard managers and Windows clipboard history to ignore, and is never synced
/// to the cloud clipboard. The saved clipboard is held in memory for a fraction of a second and never inspected.</item>
/// <item>If something else changes the clipboard while the paste settles, the newer content wins and nothing is restored.</item>
/// </list>
/// No owner window: the corrector thread has no message loop, and a window owner would be sent WM_DESTROYCLIPBOARD and
/// hang the next app that copies.
/// </summary>
internal static class ClipboardPaste
{
    /// <summary>Time for the target app to read the clipboard after Ctrl+V before the user's content goes back.</summary>
    private const int SettleMs = 250;
    private const long MaxSnapshotBytes = 32L * 1024 * 1024;

    private static readonly uint ExcludeFromMonitors = RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint CanIncludeInHistory = RegisterClipboardFormatW("CanIncludeInClipboardHistory");
    private static readonly uint CanUploadToCloud = RegisterClipboardFormatW("CanUploadToCloudClipboard");

    private sealed record Item(uint Format, nint Handle, bool Emf);

    /// <summary>Returns (sent, wanted) for the keystrokes, or null when the clipboard could not be borrowed safely.</summary>
    public static (uint sent, uint wanted)? Replace(int backspaces, string text, bool pressEnter, bool shiftEnter)
    {
        var saved = Snapshot(out var why);
        if (saved is null) { Log.Info($"paste: clipboard not borrowed ({why}); typing instead"); return null; }

        bool set = Open() && Write(new[] { Text(text) }.Concat(Markers()).ToList(), takeOwnership: true);
        if (!set)
        {
            Log.Warn("paste: clipboard could not be set; typing instead");
            Restore(saved);
            return null;
        }
        uint seq = GetClipboardSequenceNumber();
        var result = Injector.Paste(backspaces, pressEnter, shiftEnter);
        Thread.Sleep(SettleMs);
        if (GetClipboardSequenceNumber() == seq) Restore(saved);
        else { Log.Info("paste: clipboard changed meanwhile; left as it is"); Free(saved); }
        return result;
    }

    /// <summary>Copies every format on the clipboard into memory we own. Null (with a reason) when that cannot be faithful.</summary>
    private static List<Item>? Snapshot(out string why)
    {
        why = "";
        if (!Open()) { why = "clipboard busy"; return null; }
        var items = new List<Item>();
        try
        {
            long total = 0;
            for (uint f = EnumClipboardFormats(0); f != 0; f = EnumClipboardFormats(f))
            {
                // Rebuilt by Windows from the formats kept: bitmap and palette from the DIB, metafile picture from the EMF.
                // (Enumeration also lists the synthesized formats, so a bitmap-only clipboard still yields its DIB.)
                if (f is CF_BITMAP or CF_PALETTE or CF_METAFILEPICT) continue;
                if (f is CF_OWNERDISPLAY or CF_DSPBITMAP or CF_DSPMETAFILEPICT or CF_DSPENHMETAFILE || (f >= CF_PRIVATEFIRST && f <= CF_GDIOBJLAST))
                { why = $"format 0x{f:X} cannot be copied"; Free(items); return null; }

                var h = GetClipboardData(f);
                if (h == 0) continue; // nothing rendered for it
                if (f == CF_ENHMETAFILE)
                {
                    var copy = CopyEnhMetaFileW(h, null);
                    if (copy == 0) { why = "picture could not be copied"; Free(items); return null; }
                    items.Add(new Item(f, copy, true));
                    continue;
                }
                long size = (long)GlobalSize(h);
                total += size;
                if (total > MaxSnapshotBytes) { why = "clipboard content too large"; Free(items); return null; }
                var dup = CopyGlobal(h, size);
                if (dup == 0) { why = $"format 0x{f:X} unreadable"; Free(items); return null; }
                items.Add(new Item(f, dup, false));
            }
            return items; // empty when the clipboard was empty: restoring then just empties it again
        }
        finally { CloseClipboard(); }
    }

    private static void Restore(List<Item> saved)
    {
        if (!Open()) { Log.Warn("paste: clipboard busy, previous content not restored"); Free(saved); return; }
        // Clipboard managers saw this content when the user copied it; the restore is not a new copy.
        var items = saved.Concat(Markers().Where(m => saved.All(s => s.Format != m.Format))).ToList();
        if (!Write(items, takeOwnership: true)) Log.Warn("paste: previous content only partly restored");
    }

    /// <summary>Empties the clipboard and puts <paramref name="items"/> on it; the clipboard must be open. Always closes it.</summary>
    private static bool Write(List<Item> items, bool takeOwnership)
    {
        bool ok = true;
        try
        {
            if (!EmptyClipboard()) { Free(items); return false; }
            foreach (var it in items)
            {
                if (SetClipboardData(it.Format, it.Handle) != 0) continue; // the system owns the memory now
                ok = false;
                if (takeOwnership) Free(new[] { it });
            }
            return ok;
        }
        finally { CloseClipboard(); }
    }

    private static Item Text(string text)
    {
        var chars = (text + "\0").ToCharArray();
        var h = GlobalAlloc(GMEM_MOVEABLE, (nuint)(chars.Length * 2));
        var p = GlobalLock(h);
        Marshal.Copy(chars, 0, p, chars.Length);
        GlobalUnlock(h);
        return new Item(CF_UNICODETEXT, h, false);
    }

    private static IEnumerable<Item> Markers()
    {
        yield return Dword(ExcludeFromMonitors, 0);
        yield return Dword(CanIncludeInHistory, 0);
        yield return Dword(CanUploadToCloud, 0);
    }

    private static Item Dword(uint format, int value)
    {
        var h = GlobalAlloc(GMEM_MOVEABLE, 4);
        Marshal.WriteInt32(GlobalLock(h), value);
        GlobalUnlock(h);
        return new Item(format, h, false);
    }

    private static nint CopyGlobal(nint src, long size)
    {
        var dst = GlobalAlloc(GMEM_MOVEABLE, (nuint)Math.Max(size, 1));
        if (dst == 0) return 0;
        var s = GlobalLock(src);
        if (s == 0) { GlobalFree(dst); return 0; }
        var d = GlobalLock(dst);
        unsafe { Buffer.MemoryCopy((void*)s, (void*)d, size, size); }
        GlobalUnlock(dst);
        GlobalUnlock(src);
        return dst;
    }

    private static void Free(IEnumerable<Item> items)
    {
        foreach (var it in items)
            if (it.Emf) DeleteEnhMetaFile(it.Handle); else GlobalFree(it.Handle);
    }

    /// <summary>Another app may hold the clipboard open for a moment; retry briefly.</summary>
    private static bool Open()
    {
        for (int i = 0; i < 10; i++)
        {
            if (OpenClipboard(0)) return true;
            Thread.Sleep(10);
        }
        return false;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CopyEnhMetaFileW(nint hemfSrc, string? lpszFile);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteEnhMetaFile(nint hemf);
}
