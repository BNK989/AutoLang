using System.Runtime.InteropServices;
using static AutoLang.Host.Native;

namespace AutoLang.Host;

/// <summary>Sends keystrokes into the foreground app, tagged so our own hook passes them through untouched.</summary>
internal static class Injector
{
    /// <summary>Private marker in dwExtraInfo: "this keystroke is ours".</summary>
    public const nuint Signature = 0x4C414E47; // 'LANG'

    private static readonly ushort ReturnScan = (ushort)MapVirtualKeyW(VK_RETURN, MAPVK_VK_TO_VSC);
    private static readonly ushort ShiftScan = (ushort)MapVirtualKeyW(VK_SHIFT, MAPVK_VK_TO_VSC);
    private static readonly ushort ControlScan = (ushort)MapVirtualKeyW(VK_CONTROL, MAPVK_VK_TO_VSC);
    private static readonly ushort VScan = (ushort)MapVirtualKeyW(VK_V, MAPVK_VK_TO_VSC);

    private static INPUT Key(ushort vk, ushort scan, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = up ? KEYEVENTF_KEYUP : 0, dwExtraInfo = Signature } }
    };

    private static INPUT Unicode(char c, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0), dwExtraInfo = Signature } }
    };

    /// <summary>
    /// The default (Unicode) typing method: delete <paramref name="backspaces"/> characters, type <paramref name="text"/>
    /// and optionally press Enter, as one SendInput batch so no user keystroke can land in the middle. Returns (sent, wanted).
    /// </summary>
    public static (uint sent, uint wanted) Replace(int backspaces, string text, bool pressEnter, bool shiftEnter = false)
    {
        var inputs = new INPUT[backspaces * 2 + text.Length * 2 + (pressEnter ? 2 : 0) + (pressEnter && shiftEnter ? 2 : 0)];
        int i = 0;
        for (int b = 0; b < backspaces; b++) { inputs[i++] = Key(VK_BACK, 0, false); inputs[i++] = Key(VK_BACK, 0, true); }
        foreach (var c in text) { inputs[i++] = Unicode(c, false); inputs[i++] = Unicode(c, true); }
        if (pressEnter)
        {
            if (shiftEnter) inputs[i++] = Key(VK_SHIFT, ShiftScan, false);
            inputs[i++] = Key(VK_RETURN, ReturnScan, false); inputs[i++] = Key(VK_RETURN, ReturnScan, true);
            if (shiftEnter) inputs[i++] = Key(VK_SHIFT, ShiftScan, true);
        }
        return Send(inputs);
    }

    /// <summary>
    /// The Keys typing method: switch the window to <paramref name="hkl"/> first, then press the real keys that type each
    /// character on that layout, for apps that ignore Unicode packets. A character the layout has no plain or Shift key
    /// for goes as a Unicode packet. Returns null (caller types with Unicode) when the switch does not land in time, or
    /// when CapsLock or a held Shift would change what the keys type.
    /// </summary>
    public static (uint sent, uint wanted)? ReplaceWithKeys(nint hwnd, nint hkl, int backspaces, string text, bool pressEnter, bool shiftEnter, bool capsLock)
    {
        if (hwnd == 0) return null;
        if (capsLock || (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) return null;
        if (hkl != 0 && HklOfWindow(hwnd) != hkl)
        {
            SwitchLayout(hwnd, hkl);
            var deadline = Environment.TickCount64 + 150;
            while (HklOfWindow(hwnd) != hkl)
            {
                if (Environment.TickCount64 > deadline) { Log.Info("keys: layout switch not confirmed; typing Unicode"); return null; }
                Thread.Sleep(5);
            }
        }
        var layout = HklOfWindow(hwnd);

        var inputs = new List<INPUT>(backspaces * 2 + text.Length * 4 + 4);
        for (int b = 0; b < backspaces; b++) { inputs.Add(Key(VK_BACK, 0, false)); inputs.Add(Key(VK_BACK, 0, true)); }
        foreach (var c in text)
        {
            short scan = VkKeyScanExW(c, layout);
            int mods = scan == -1 ? -1 : (scan >> 8) & 0xFF;
            if (mods is not (0 or 1)) // missing, or needs Ctrl/Alt (AltGr): a key press would trigger shortcuts
            {
                inputs.Add(Unicode(c, false)); inputs.Add(Unicode(c, true));
                continue;
            }
            ushort vk = (ushort)(scan & 0xFF);
            ushort sc = (ushort)MapVirtualKeyExW(vk, MAPVK_VK_TO_VSC, layout);
            if (mods == 1) inputs.Add(Key(VK_SHIFT, ShiftScan, false));
            inputs.Add(Key(vk, sc, false)); inputs.Add(Key(vk, sc, true));
            if (mods == 1) inputs.Add(Key(VK_SHIFT, ShiftScan, true));
        }
        AddEnter(inputs, pressEnter, shiftEnter);
        return Send(inputs.ToArray());
    }

    /// <summary>The Paste typing method's keystrokes: delete, Ctrl+V, optional Enter. The clipboard is ClipboardPaste's job.</summary>
    public static (uint sent, uint wanted) Paste(int backspaces, bool pressEnter, bool shiftEnter)
    {
        var inputs = new List<INPUT>(backspaces * 2 + 8);
        for (int b = 0; b < backspaces; b++) { inputs.Add(Key(VK_BACK, 0, false)); inputs.Add(Key(VK_BACK, 0, true)); }
        inputs.Add(Key(VK_CONTROL, ControlScan, false));
        inputs.Add(Key(VK_V, VScan, false)); inputs.Add(Key(VK_V, VScan, true));
        inputs.Add(Key(VK_CONTROL, ControlScan, true));
        AddEnter(inputs, pressEnter, shiftEnter);
        return Send(inputs.ToArray());
    }

    private static void AddEnter(List<INPUT> inputs, bool pressEnter, bool shiftEnter)
    {
        if (!pressEnter) return;
        if (shiftEnter) inputs.Add(Key(VK_SHIFT, ShiftScan, false));
        inputs.Add(Key(VK_RETURN, ReturnScan, false)); inputs.Add(Key(VK_RETURN, ReturnScan, true));
        if (shiftEnter) inputs.Add(Key(VK_SHIFT, ShiftScan, true));
    }

    private static (uint sent, uint wanted) Send(INPUT[] inputs)
    {
        if (inputs.Length == 0) return (0, 0);
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length) Log.Warn($"SendInput sent {sent}/{inputs.Length} (err {Marshal.GetLastWin32Error()})");
        return (sent, (uint)inputs.Length);
    }

    /// <summary>Ask the window to switch its input language. Windows handles the per-thread bookkeeping and the language bar.</summary>
    public static void SwitchLayout(nint hwnd, nint hkl)
    {
        if (!PostMessageW(hwnd, WM_INPUTLANGCHANGEREQUEST, 0, hkl)) Log.Warn("WM_INPUTLANGCHANGEREQUEST post failed");
    }
}
