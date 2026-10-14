using System.Runtime.InteropServices;

namespace AutoLang.Mac;

/// <summary>
/// The C APIs the host needs: CoreFoundation, CoreGraphics events, Text Input Sources (HIToolbox), UCKeyTranslate
/// and the accessibility trust check. C <c>bool</c>/<c>Boolean</c> are one byte, so they are declared as byte here,
/// never as .NET bool (which marshals as a 4-byte BOOL).
/// </summary>
internal static unsafe class Native
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const string HIToolbox = "/System/Library/Frameworks/Carbon.framework/Frameworks/HIToolbox.framework/HIToolbox";

    // ---- CoreFoundation ----

    [StructLayout(LayoutKind.Sequential)]
    public struct CFRange { public nint Location, Length; public CFRange(nint location, nint length) { Location = location; Length = length; } }

    [DllImport(CoreFoundation)] public static extern void CFRelease(nint cf);
    [DllImport(CoreFoundation)] public static extern nint CFRetain(nint cf);
    [DllImport(CoreFoundation)] public static extern nint CFArrayGetCount(nint array);
    [DllImport(CoreFoundation)] public static extern nint CFArrayGetValueAtIndex(nint array, nint index);
    [DllImport(CoreFoundation)] public static extern nint CFStringCreateWithCharacters(nint alloc, char* chars, nint numChars);
    [DllImport(CoreFoundation)] public static extern nint CFStringGetLength(nint str);
    [DllImport(CoreFoundation)] public static extern void CFStringGetCharacters(nint str, CFRange range, char* buffer);
    [DllImport(CoreFoundation)] public static extern nint CFDataGetBytePtr(nint data);
    [DllImport(CoreFoundation)] public static extern nint CFDictionaryCreate(nint alloc, nint* keys, nint* values, nint count, nint keyCallBacks, nint valueCallBacks);
    [DllImport(CoreFoundation)] public static extern nint CFMachPortCreateRunLoopSource(nint alloc, nint port, nint order);
    [DllImport(CoreFoundation)] public static extern void CFMachPortInvalidate(nint port);
    [DllImport(CoreFoundation)] public static extern nint CFRunLoopGetMain();
    [DllImport(CoreFoundation)] public static extern void CFRunLoopAddSource(nint runLoop, nint source, nint mode);
    [DllImport(CoreFoundation)] public static extern void CFRunLoopRemoveSource(nint runLoop, nint source, nint mode);

    // ---- CoreGraphics events (via ApplicationServices) ----

    public const uint kCGHIDEventTap = 0, kCGSessionEventTap = 1;
    public const uint kCGHeadInsertEventTap = 0;
    public const uint kCGEventTapOptionDefault = 0;
    public const int kCGEventSourceStatePrivate = -1;

    public const uint kCGEventLeftMouseDown = 1, kCGEventRightMouseDown = 3, kCGEventKeyDown = 10, kCGEventKeyUp = 11,
        kCGEventFlagsChanged = 12, kCGEventOtherMouseDown = 25;
    public const uint kCGEventTapDisabledByTimeout = 0xFFFFFFFE, kCGEventTapDisabledByUserInput = 0xFFFFFFFF;

    public const uint kCGKeyboardEventAutorepeat = 8, kCGKeyboardEventKeycode = 9, kCGEventSourceUserData = 42;

    public const ulong kCGEventFlagMaskAlphaShift = 0x00010000, kCGEventFlagMaskShift = 0x00020000,
        kCGEventFlagMaskControl = 0x00040000, kCGEventFlagMaskAlternate = 0x00080000, kCGEventFlagMaskCommand = 0x00100000;
    /// <summary>Device-dependent bits of the flags (IOLLEvent.h): which Shift key is down.</summary>
    public const ulong NX_DEVICELSHIFTKEYMASK = 0x02, NX_DEVICERSHIFTKEYMASK = 0x04;

    [DllImport(ApplicationServices)]
    public static extern nint CGEventTapCreate(uint tap, uint place, uint options, ulong eventsOfInterest,
        delegate* unmanaged<nint, uint, nint, nint, nint> callback, nint userInfo);
    [DllImport(ApplicationServices)] public static extern void CGEventTapEnable(nint tap, byte enable);
    [DllImport(ApplicationServices)] public static extern byte CGEventTapIsEnabled(nint tap);
    [DllImport(ApplicationServices)] public static extern long CGEventGetIntegerValueField(nint ev, uint field);
    [DllImport(ApplicationServices)] public static extern void CGEventSetIntegerValueField(nint ev, uint field, long value);
    [DllImport(ApplicationServices)] public static extern ulong CGEventGetFlags(nint ev);
    [DllImport(ApplicationServices)] public static extern void CGEventSetFlags(nint ev, ulong flags);
    [DllImport(ApplicationServices)] public static extern nint CGEventSourceCreate(int stateId);
    [DllImport(ApplicationServices)] public static extern void CGEventSourceSetLocalEventsSuppressionInterval(nint source, double seconds);
    [DllImport(ApplicationServices)] public static extern nint CGEventCreateKeyboardEvent(nint source, ushort keycode, byte keyDown);
    [DllImport(ApplicationServices)] public static extern void CGEventKeyboardSetUnicodeString(nint ev, nuint length, char* chars);
    [DllImport(ApplicationServices)] public static extern void CGEventPost(uint tap, nint ev);
    [DllImport(ApplicationServices)] public static extern byte CGPreflightListenEventAccess();
    [DllImport(ApplicationServices)] public static extern byte CGRequestListenEventAccess();
    [DllImport(ApplicationServices)] public static extern byte AXIsProcessTrusted();
    [DllImport(ApplicationServices)] public static extern byte AXIsProcessTrustedWithOptions(nint options);

    // ---- Text Input Sources and key translation (via Carbon) ----

    [DllImport(Carbon)] public static extern nint TISCreateInputSourceList(nint properties, byte includeAllInstalled);
    [DllImport(Carbon)] public static extern nint TISCopyCurrentKeyboardInputSource();
    [DllImport(Carbon)] public static extern nint TISGetInputSourceProperty(nint source, nint key);
    [DllImport(Carbon)] public static extern int TISSelectInputSource(nint source);
    [DllImport(Carbon)] public static extern byte LMGetKbdType();
    [DllImport(Carbon)] public static extern byte IsSecureEventInputEnabled();
    [DllImport(Carbon)]
    public static extern int UCKeyTranslate(nint keyLayout, ushort virtualKeyCode, ushort keyAction, uint modifierKeyState,
        uint keyboardType, uint keyTranslateOptions, uint* deadKeyState, nuint maxStringLength, nuint* actualStringLength, char* unicodeString);

    public const ushort kUCKeyActionDown = 0;
    /// <summary>UCKeyTranslate modifier state: the Carbon modifier bits shifted right by 8.</summary>
    public const uint ShiftModifier = 0x0200 >> 8, AlphaLockModifier = 0x0400 >> 8;

    // ---- Exported constants (CFStringRef and friends are variables holding a pointer) ----

    private static readonly nint CfLib = NativeLibrary.Load(CoreFoundation);
    private static readonly nint AsLib = NativeLibrary.Load(ApplicationServices);
    private static readonly nint TisLib = NativeLibrary.TryLoad(HIToolbox, out var h) ? h : NativeLibrary.Load(Carbon);

    /// <summary>The value of an exported pointer variable, e.g. <c>kTISPropertyInputSourceID</c>.</summary>
    private static nint Constant(nint lib, string name) => *(nint*)NativeLibrary.GetExport(lib, name);

    public static readonly nint kCFRunLoopCommonModes = Constant(CfLib, "kCFRunLoopCommonModes");
    public static readonly nint kCFBooleanTrue = Constant(CfLib, "kCFBooleanTrue");
    /// <summary>A struct, not a pointer variable: its address is what CFDictionaryCreate takes.</summary>
    public static readonly nint kCFTypeDictionaryKeyCallBacks = NativeLibrary.GetExport(CfLib, "kCFTypeDictionaryKeyCallBacks");
    public static readonly nint kCFTypeDictionaryValueCallBacks = NativeLibrary.GetExport(CfLib, "kCFTypeDictionaryValueCallBacks");
    public static readonly nint kAXTrustedCheckOptionPrompt = Constant(AsLib, "kAXTrustedCheckOptionPrompt");

    public static readonly nint kTISPropertyInputSourceID = Constant(TisLib, "kTISPropertyInputSourceID");
    public static readonly nint kTISPropertyInputSourceCategory = Constant(TisLib, "kTISPropertyInputSourceCategory");
    public static readonly nint kTISCategoryKeyboardInputSource = Constant(TisLib, "kTISCategoryKeyboardInputSource");
    public static readonly nint kTISPropertyInputSourceIsSelectCapable = Constant(TisLib, "kTISPropertyInputSourceIsSelectCapable");
    public static readonly nint kTISPropertyInputSourceType = Constant(TisLib, "kTISPropertyInputSourceType");
    public static readonly nint kTISTypeKeyboardLayout = Constant(TisLib, "kTISTypeKeyboardLayout");
    public static readonly nint kTISPropertyUnicodeKeyLayoutData = Constant(TisLib, "kTISPropertyUnicodeKeyLayoutData");
    public static readonly nint kTISPropertyInputSourceLanguages = Constant(TisLib, "kTISPropertyInputSourceLanguages");
    public static readonly nint kTISPropertyLocalizedName = Constant(TisLib, "kTISPropertyLocalizedName");

    // ---- Helpers ----

    /// <summary>A CFString / NSString with a +1 reference; the caller releases it.</summary>
    public static nint CreateString(string s)
    {
        fixed (char* p = s) return CFStringCreateWithCharacters(0, p, s.Length);
    }

    /// <summary>The text of a CFString / NSString, or "" for null.</summary>
    public static string ToManaged(nint cfString)
    {
        if (cfString == 0) return "";
        var n = CFStringGetLength(cfString);
        if (n == 0) return "";
        Span<char> buf = n <= 256 ? stackalloc char[(int)n] : new char[(int)n];
        fixed (char* p = buf) CFStringGetCharacters(cfString, new CFRange(0, n), p);
        return new string(buf);
    }

    public static string StringProperty(nint source, nint key) => ToManaged(TISGetInputSourceProperty(source, key));

    public static string[] StringArray(nint cfArray)
    {
        if (cfArray == 0) return [];
        var n = CFArrayGetCount(cfArray);
        var result = new string[n];
        for (nint i = 0; i < n; i++) result[i] = ToManaged(CFArrayGetValueAtIndex(cfArray, i));
        return result;
    }

    /// <summary>A CFDictionary of CF objects, +1.</summary>
    public static nint CreateDictionary(params (nint Key, nint Value)[] pairs)
    {
        var keys = stackalloc nint[pairs.Length];
        var values = stackalloc nint[pairs.Length];
        for (int i = 0; i < pairs.Length; i++) { keys[i] = pairs[i].Key; values[i] = pairs[i].Value; }
        return CFDictionaryCreate(0, keys, values, pairs.Length, kCFTypeDictionaryKeyCallBacks, kCFTypeDictionaryValueCallBacks);
    }
}
