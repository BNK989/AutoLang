using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static M0Spike.Native;

namespace M0Spike;

/// <summary>
/// M0 spike: low-level keyboard hook + word buffer + replace-in-place.
///
/// What it proves:
///   * we can see the characters the user typed, resolved against the layout of the window they typed into;
///   * on a word boundary we can delete the word and re-type it transliterated, before the next key lands;
///   * we can switch the foreground window's input language;
///   * how long all of that takes, per app.
///
/// What it deliberately does NOT do: any real language decision. "Auto" mode fires only on a
/// hard-reject rule (a Hebrew final letter in the middle of a word) and a tiny demo list; F8
/// converts the last word unconditionally. The session buffer is the current word only and
/// is never written anywhere but the console log of this spike.
/// </summary>
internal sealed class Hook : IDisposable
{
    private readonly LayoutMap[] _layouts;
    private readonly LowLevelKeyboardProc _proc; // keep delegate alive for the lifetime of the hook
    private nint _hhk;

    private readonly StringBuilder _word = new();
    private bool _capsOn;

    // Tiny demo vocabulary so "auto" mode has something to fire on without a real engine.
    private static readonly HashSet<string> DemoWords = new(StringComparer.OrdinalIgnoreCase)
    { "שלום", "תודה", "הי", "יום", "טוב", "בוקר", "hello", "hi", "thanks", "ok", "good", "morning" };

    public Hook(LayoutMap[] layouts)
    {
        _layouts = layouts;
        _proc = Callback;
        _capsOn = (GetKeyState(VK_CAPITAL) & 1) != 0;
    }

    public void Install()
    {
        _hhk = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(null), 0);
        if (_hhk == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx failed");
        Log.Info("hook installed. Type in any app. SPACE after a word = auto rule; F8 = force-convert last word; Ctrl+C here = quit.");
    }

    private nint Callback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hhk, nCode, wParam, lParam);
        var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

        // Our own injected keys: pass through untouched.
        if (k.dwExtraInfo == Injector.Signature) return CallNextHookEx(_hhk, nCode, wParam, lParam);

        bool down = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
        if (!down) return CallNextHookEx(_hhk, nCode, wParam, lParam);

        var sw = Stopwatch.StartNew();
        try
        {
            var r = OnKeyDown(k);
            if (r == KeyAction.Swallow) return 1;
        }
        catch (Exception ex) { Log.Warn($"callback: {ex.Message}"); }
        finally
        {
            if (sw.Elapsed.TotalMilliseconds > 5) Log.Warn($"slow callback: {sw.Elapsed.TotalMilliseconds:F1} ms");
        }
        return CallNextHookEx(_hhk, nCode, wParam, lParam);
    }

    private enum KeyAction { Pass, Swallow }

    private KeyAction OnKeyDown(KBDLLHOOKSTRUCT k)
    {
        int vk = (int)k.vkCode;

        if (vk == VK_CAPITAL) { _capsOn = !_capsOn; return KeyAction.Pass; }
        if (vk is VK_SHIFT or VK_LSHIFT or VK_RSHIFT) return KeyAction.Pass;
        // Modifier key-down itself (Alt before Alt+Tab, Ctrl before Ctrl+C): a chord is starting.
        if (vk is VK_MENU or VK_LMENU or VK_RMENU or VK_CONTROL or VK_LCONTROL or VK_RCONTROL or VK_LWIN or VK_RWIN)
        { Reset("modifier"); return KeyAction.Pass; }

        bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        bool win = ((GetAsyncKeyState(VK_LWIN) | GetAsyncKeyState(VK_RWIN)) & 0x8000) != 0;

        if (vk == VK_F8) { ForceConvert(); return KeyAction.Swallow; }

        // Shortcuts, navigation, editing keys other than Backspace: the run is broken.
        if (ctrl || alt || win) { Reset("chord"); return KeyAction.Pass; }
        switch (vk)
        {
            case VK_BACK:
                if (_word.Length > 0) _word.Length--;
                return KeyAction.Pass;
            case VK_ESCAPE or VK_TAB or VK_DELETE or VK_LEFT or VK_RIGHT or VK_UP or VK_DOWN
                 or VK_HOME or VK_END or VK_PRIOR or VK_NEXT:
                Reset("nav");
                return KeyAction.Pass;
            case VK_RETURN:
                return OnBoundary('\n', k);
        }

        var hwnd = GetForegroundWindow();
        var hkl = GetKeyboardLayout(GetWindowThreadProcessId(hwnd, out _));
        var text = ResolveChar(k, hkl);
        if (text is null) { Reset($"unresolved vk 0x{vk:X2}"); return KeyAction.Pass; }

        var c = text[0];
        if (c == ' ') return OnBoundary(' ', k);

        // A key is part of the word if it types a letter on ANY installed layout: the ',' key is 'ת'
        // in Hebrew, the ''' key is ',' — so punctuation as seen here may be a letter as intended.
        bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        if (IsWordKey(new LayoutMap.Key(k.vkCode, shift))) { _word.Append(text); return KeyAction.Pass; }

        // Digits and the rest: end the word without triggering (spike keeps it simple).
        Reset($"punct '{text}'");
        return KeyAction.Pass;
    }

    private bool IsWordKey(LayoutMap.Key key)
    {
        foreach (var l in _layouts)
        {
            var t = l.TextFor(key);
            if (t is { Length: 1 } && (char.IsLetter(t[0]) || t[0] == '\'')) return true;
        }
        return false;
    }

    /// <summary>Resolve the key to text using the *foreground window's* layout and current shift/caps.</summary>
    private string? ResolveChar(KBDLLHOOKSTRUCT k, nint hkl)
    {
        var state = new byte[256];
        bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        if (shift) state[VK_SHIFT] = 0x80;
        if (_capsOn) state[VK_CAPITAL] = 0x01;
        var sb = new StringBuilder(8);
        int rc = ToUnicodeEx(k.vkCode, k.scanCode, state, sb, sb.Capacity, TOUNICODE_NO_STATE_CHANGE, hkl);
        return rc > 0 ? sb.ToString(0, rc) : null;
    }

    private KeyAction OnBoundary(char boundary, KBDLLHOOKSTRUCT k)
    {
        var word = _word.ToString();
        _word.Clear();
        if (word.Length == 0) return KeyAction.Pass;

        var hwnd = GetForegroundWindow();
        var current = CurrentLayout(hwnd);
        var other = OtherLayout(current);
        if (current is null || other is null) return KeyAction.Pass;

        var remapped = current.TransliterateTo(other, word);
        var reason = AutoRule(word, remapped);
        Log.Info($"word '{word}' ({Short(current)}) -> '{remapped}' ({Short(other)})  {(reason is null ? "keep" : "CORRECT: " + reason)}  [{ForegroundProcessName(hwnd)}]");
        if (reason is null) return KeyAction.Pass;

        // Swallow the user's boundary key and re-emit it after the replacement so ordering is deterministic.
        var sw = Stopwatch.StartNew();
        Injector.Replace(word.Length, remapped + (boundary == '\n' ? "" : boundary.ToString()));
        Injector.SwitchLayout(hwnd, other.Hkl);
        if (boundary == '\n') Injector.Replace(0, "\r"); // KEYEVENTF_UNICODE '\r' → Enter in most edit controls
        Log.Info($"  replaced in {sw.Elapsed.TotalMilliseconds:F2} ms (queued; app render time not included)");
        return KeyAction.Swallow;
    }

    /// <summary>Placeholder for the real engine. Returns a reason to correct, or null to keep.</summary>
    private static string? AutoRule(string typed, string remapped)
    {
        if (HasMisplacedFinalLetter(typed) && !HasMisplacedFinalLetter(remapped)) return "final-letter rule";
        if (!DemoWords.Contains(typed) && DemoWords.Contains(remapped)) return "demo dictionary";
        return null;
    }

    // Hebrew final forms: ך ם ן ף ץ must be the last letter of a word.
    private static bool HasMisplacedFinalLetter(string s)
    {
        for (int i = 0; i < s.Length - 1; i++)
            if (s[i] is 'ך' or 'ם' or 'ן' or 'ף' or 'ץ') return true;
        return false;
    }

    private void ForceConvert()
    {
        var word = _word.ToString();
        if (word.Length == 0) { Log.Info("F8: nothing buffered"); return; }
        var hwnd = GetForegroundWindow();
        var current = CurrentLayout(hwnd);
        var other = OtherLayout(current);
        if (current is null || other is null) return;
        var remapped = current.TransliterateTo(other, word);
        _word.Clear().Append(remapped);
        var sw = Stopwatch.StartNew();
        Injector.Replace(word.Length, remapped);
        Injector.SwitchLayout(hwnd, other.Hkl);
        Log.Info($"F8: '{word}' -> '{remapped}' in {sw.Elapsed.TotalMilliseconds:F2} ms [{ForegroundProcessName(hwnd)}]");
    }

    private LayoutMap? CurrentLayout(nint hwnd)
    {
        var hkl = GetKeyboardLayout(GetWindowThreadProcessId(hwnd, out _));
        // Compare on language id: the HKL a window reports can differ in the high word from the list entry.
        return _layouts.FirstOrDefault(l => l.Hkl == hkl) ?? _layouts.FirstOrDefault(l => l.LangId == (ushort)((long)hkl & 0xFFFF));
    }

    private LayoutMap? OtherLayout(LayoutMap? current) =>
        current is null ? null : _layouts.FirstOrDefault(l => l.LangId != current.LangId);

    private static string Short(LayoutMap l) => $"0x{l.LangId:X4}";

    private void Reset(string why)
    {
        if (_word.Length > 0) Log.Info($"reset ({why}), dropped '{_word}'");
        _word.Clear();
    }

    public void Dispose()
    {
        if (_hhk != 0) { UnhookWindowsHookEx(_hhk); _hhk = 0; }
    }
}
