using System.Runtime.InteropServices;
using System.Text;

namespace AutoLang.Host;

/// <summary>
/// Raw Win32 surface of the host. Ported from the M0 spike; additions: mouse hook, WinEvents, timers, monitors, and
/// for M4 elevation checks, last-input time, the clipboard, restart registration and the package check.
/// </summary>
internal static class Native
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public const int WM_QUIT = 0x0012;
    public const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
    public const int WM_TIMER = 0x0113;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_XBUTTONDOWN = 0x020B;

    public const uint MAPVK_VK_TO_VSC = 0;
    public const uint TOUNICODE_NO_STATE_CHANGE = 1 << 2;

    public const int VK_BACK = 0x08;
    public const int VK_TAB = 0x09;
    public const int VK_RETURN = 0x0D;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_PAUSE = 0x13;
    public const int VK_CAPITAL = 0x14;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_SPACE = 0x20;
    public const int VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_END = 0x23, VK_HOME = 0x24;
    public const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
    public const int VK_INSERT = 0x2D, VK_DELETE = 0x2E;
    public const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    public const int VK_APPS = 0x5D;
    public const int VK_F1 = 0x70, VK_F24 = 0x87;
    public const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4, VK_RMENU = 0xA5;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_OBJECT_FOCUS = 0x8005;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        // MOUSEINPUT is the largest member (32 bytes on x64); pad so sizeof(INPUT) is right.
        [FieldOffset(0)] public long pad0;
        [FieldOffset(8)] public long pad1;
        [FieldOffset(16)] public long pad2;
        [FieldOffset(24)] public long pad3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public int ptX, ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public delegate nint LowLevelProc(int nCode, nuint wParam, nint lParam);
    public delegate void WinEventProc(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookExW(int idHook, LowLevelProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc, WinEventProc pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern int GetKeyboardLayoutList(int nBuff, [Out] nint[]? lpList);

    [DllImport("user32.dll")]
    public static extern nint GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyExW(uint uCode, uint uMapType, nint dwhkl);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyW(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)] StringBuilder pwszBuff,
        int cchBuff, uint wFlags, nint dwhkl);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool PostMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool PostThreadMessageW(uint idThread, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern nint DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern nuint SetTimer(nint hWnd, nuint nIDEvent, uint uElapse, nint lpTimerFunc);

    [DllImport("user32.dll")]
    public static extern bool KillTimer(nint hWnd, nuint uIDEvent);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassNameW(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("kernel32.dll")]
    public static extern nint OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(nint h);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageNameW(nint hProcess, uint flags, StringBuilder exeName, ref int size);

    /// <summary>Exe name of the process owning <paramref name="hwnd"/>, or "pid N" when it cannot be opened (elevated).</summary>
    public static string ProcessNameOf(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == 0) return $"pid {pid}";
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageNameW(h, 0, sb, ref size) ? Path.GetFileName(sb.ToString()) : $"pid {pid}";
        }
        finally { CloseHandle(h); }
    }

    public static string ClassNameOf(nint hwnd)
    {
        var sb = new StringBuilder(256);
        return GetClassNameW(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    /// <summary>True when the window covers its whole monitor (borderless fullscreen: games, video).</summary>
    public static bool IsFullscreen(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out var r)) return false;
        var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfoW(mon, ref mi)) return false;
        var m = mi.rcMonitor;
        return r.left <= m.left && r.top <= m.top && r.right >= m.right && r.bottom >= m.bottom;
    }

    public static nint HklOfWindow(nint hwnd) => GetKeyboardLayout(GetWindowThreadProcessId(hwnd, out _));

    // ---- M4: elevation, hook watchdog, typing methods, restart, packaging ----

    public const int VK_V = 0x56;
    public const uint TOKEN_QUERY = 0x0008;
    public const int TokenElevation = 20;
    public const uint RESTART_NO_REBOOT = 8;
    public const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern short VkKeyScanExW(char ch, nint dwhkl);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetTokenInformation(nint tokenHandle, int tokenInformationClass, out uint tokenInformation, uint tokenInformationLength, out uint returnLength);

    [DllImport("kernel32.dll")]
    public static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegisterApplicationRestart(string? pwzCommandline, uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);

    // Clipboard (the Paste typing method). Plain Win32 with no owner window: the corrector thread has no message loop.
    public const uint CF_TEXT = 1, CF_BITMAP = 2, CF_METAFILEPICT = 3, CF_OEMTEXT = 7, CF_PALETTE = 9, CF_UNICODETEXT = 13, CF_ENHMETAFILE = 14;
    public const uint CF_OWNERDISPLAY = 0x80, CF_DSPBITMAP = 0x82, CF_DSPMETAFILEPICT = 0x83, CF_DSPENHMETAFILE = 0x8E;
    public const uint CF_PRIVATEFIRST = 0x200, CF_GDIOBJLAST = 0x3FF;
    public const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll")]
    public static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    public static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    public static extern uint EnumClipboardFormats(uint format);

    [DllImport("user32.dll")]
    public static extern nint GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    public static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterClipboardFormatW(string lpszFormat);

    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();

    [DllImport("kernel32.dll")]
    public static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll")]
    public static extern nint GlobalFree(nint hMem);

    [DllImport("kernel32.dll")]
    public static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll")]
    public static extern bool GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll")]
    public static extern nuint GlobalSize(nint hMem);

    /// <summary>
    /// True when the process runs elevated (as administrator). Windows keeps a non-elevated AutoLang out of such windows
    /// (UIPI): its hook sees none of their keys and its input never reaches them. Anything that cannot be inspected
    /// counts as elevated, because that is the usual reason it cannot be.
    /// </summary>
    public static bool IsElevated(uint pid)
    {
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == 0) return true;
        try { return IsElevatedHandle(h); }
        finally { CloseHandle(h); }
    }

    private static bool IsElevatedHandle(nint process)
    {
        if (!OpenProcessToken(process, TOKEN_QUERY, out var token)) return true;
        try { return !GetTokenInformation(token, TokenElevation, out uint elevated, 4, out _) || elevated != 0; }
        finally { CloseHandle(token); }
    }

    /// <summary>AutoLang itself runs as administrator: then nothing is out of reach and elevated windows are fine.</summary>
    public static readonly bool SelfElevated = IsElevatedHandle(GetCurrentProcess());

    /// <summary>The foreground window belongs to an elevated process that a non-elevated AutoLang cannot reach.</summary>
    public static bool IsOutOfReach(nint hwnd)
    {
        if (SelfElevated || hwnd == 0) return false;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid != 0 && IsElevated(pid);
    }

    /// <summary>Running from an MSIX package (the Store build) rather than from the zip.</summary>
    public static readonly bool IsPackaged = PackageCheck();

    private static bool PackageCheck()
    {
        try
        {
            uint len = 0;
            return GetCurrentPackageFullName(ref len, null) != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch (EntryPointNotFoundException) { return false; } // Windows 7/8: no packages at all
    }
}
