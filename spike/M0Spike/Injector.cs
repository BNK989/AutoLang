using static M0Spike.Native;

namespace M0Spike;

/// <summary>Sends keystrokes back into the foreground app, tagged so our own hook ignores them.</summary>
internal static class Injector
{
    /// <summary>Private marker in dwExtraInfo: "this keystroke is ours".</summary>
    public const nuint Signature = 0x4C414E47; // 'LANG'

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0, dwExtraInfo = Signature } }
    };

    private static INPUT Unicode(char c, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0), dwExtraInfo = Signature } }
    };

    /// <summary>Delete <paramref name="backspaces"/> chars, then type <paramref name="text"/>, as one atomic SendInput batch.</summary>
    public static void Replace(int backspaces, string text)
    {
        var inputs = new List<INPUT>(backspaces * 2 + text.Length * 2);
        for (int i = 0; i < backspaces; i++) { inputs.Add(Key(VK_BACK, false)); inputs.Add(Key(VK_BACK, true)); }
        foreach (var c in text) { inputs.Add(Unicode(c, false)); inputs.Add(Unicode(c, true)); }
        var arr = inputs.ToArray();
        uint sent = SendInput((uint)arr.Length, arr, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
        if (sent != arr.Length)
            Log.Warn($"SendInput sent {sent}/{arr.Length} (err {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
    }

    /// <summary>Ask the foreground window to switch its input language.</summary>
    public static void SwitchLayout(nint hwnd, nint hkl)
    {
        if (!PostMessageW(hwnd, WM_INPUTLANGCHANGEREQUEST, 0, hkl))
            Log.Warn("WM_INPUTLANGCHANGEREQUEST post failed");
    }
}
