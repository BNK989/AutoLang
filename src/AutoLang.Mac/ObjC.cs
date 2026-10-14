using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace AutoLang.Mac;

/// <summary>
/// Just enough of the Objective-C runtime to drive AppKit: class and selector lookup, typed <c>objc_msgSend</c>
/// entry points (each call site needs the exact C signature; on arm64 a mismatched one corrupts arguments), and
/// one runtime-registered class whose methods call back into C#: menu actions, notifications, and running work on
/// the main thread.
/// </summary>
internal static unsafe class ObjC
{
    private const string Lib = "/usr/lib/libobjc.A.dylib";

    [DllImport(Lib)] public static extern nint objc_getClass(string name);
    [DllImport(Lib)] public static extern nint sel_registerName(string name);
    [DllImport(Lib)] private static extern nint objc_allocateClassPair(nint superclass, string name, nint extraBytes);
    [DllImport(Lib)] private static extern void objc_registerClassPair(nint cls);
    [DllImport(Lib)] private static extern byte class_addMethod(nint cls, nint sel, nint imp, string types);
    [DllImport(Lib)] private static extern nint objc_getProtocol(string name);
    [DllImport(Lib)] private static extern byte class_addProtocol(nint cls, nint protocol);

    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint Send(nint receiver, nint sel);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint Send(nint receiver, nint sel, nint a);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint Send(nint receiver, nint sel, nint a, nint b);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint Send(nint receiver, nint sel, nint a, nint b, nint c);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint Send(nint receiver, nint sel, nint a, nint b, nint c, nint d);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint SendDouble(nint receiver, nint sel, double a);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint SendLong(nint receiver, nint sel, long a);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint SendBool(nint receiver, nint sel, byte a);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern nint SendPerform(nint receiver, nint sel, nint selector, nint arg, byte wait);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern byte SendRetBool(nint receiver, nint sel);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern byte SendRetBool(nint receiver, nint sel, nint a);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] public static extern long SendRetLong(nint receiver, nint sel);

    public static nint Class(string name) => objc_getClass(name);
    public static nint Sel(string name) => sel_registerName(name);

    /// <summary>[receiver selector] with a fresh NSString argument, released afterwards (the receiver copies or retains it).</summary>
    public static nint SendString(nint receiver, string selector, string value)
    {
        var s = Native.CreateString(value);
        try { return Send(receiver, Sel(selector), s); }
        finally { Native.CFRelease(s); }
    }

    // ---- The bridge object ----

    private static nint _bridge;
    private static readonly ConcurrentQueue<Action> MainQueue = new();
    private static readonly Dictionary<string, Action<nint>> Handlers = new(StringComparer.Ordinal);

    /// <summary>The instance of AutoLangBridge that menu items and notification observers target.</summary>
    public static nint Bridge => _bridge;

    /// <summary>
    /// Registers AutoLangBridge, an NSObject subclass with: <c>menuAction:</c> (sender = the NSMenuItem),
    /// <c>notified:</c> (an NSNotification), <c>menuWillOpen:</c> (NSMenuDelegate) and <c>drain:</c> (runs queued
    /// main-thread work). Call once, on the main thread, before anything targets it.
    /// </summary>
    public static void RegisterBridge()
    {
        var cls = objc_allocateClassPair(Class("NSObject"), "AutoLangBridge", 0);
        delegate* unmanaged<nint, nint, nint, void> imp = &Dispatch;
        foreach (var name in new[] { "menuAction:", "notified:", "menuWillOpen:", "drain:" })
            class_addMethod(cls, Sel(name), (nint)imp, "v@:@");
        var menuDelegate = objc_getProtocol("NSMenuDelegate");
        if (menuDelegate != 0) class_addProtocol(cls, menuDelegate);
        objc_registerClassPair(cls);
        _bridge = Send(Send(cls, Sel("alloc")), Sel("init"));
    }

    /// <summary>Handles a bridge selector ("menuAction:", "notified:", "menuWillOpen:") on the main thread.</summary>
    public static void On(string selector, Action<nint> handler) => Handlers[selector] = handler;

    /// <summary>Runs <paramref name="action"/> on the main thread, later. Safe from any thread.</summary>
    public static void OnMain(Action action)
    {
        MainQueue.Enqueue(action);
        SendPerform(_bridge, Sel("performSelectorOnMainThread:withObject:waitUntilDone:"), Sel("drain:"), 0, 0);
    }

    private static readonly nint DrainSel = Sel("drain:");

    [UnmanagedCallersOnly]
    private static void Dispatch(nint self, nint cmd, nint arg)
    {
        try
        {
            if (cmd == DrainSel)
            {
                while (MainQueue.TryDequeue(out var a)) a();
                return;
            }
            // Selectors are unique per name, so look up the name only for the rare UI callbacks.
            foreach (var (name, handler) in Handlers)
                if (Sel(name) == cmd) { handler(arg); return; }
        }
        catch (Exception ex) { Log.Warn($"main thread: {ex.GetType().Name}: {ex.Message}"); }
    }
}
