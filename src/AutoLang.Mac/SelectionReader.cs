using System.Diagnostics;
using AutoLang.Session;
using static AutoLang.Mac.Native;

namespace AutoLang.Mac;

/// <summary>
/// The hotkey's last resort on the Mac, as on Windows: with nothing tracked to undo or convert, read the text selected in
/// the focused control through the Accessibility API (<c>AXSelectedText</c>, covered by the permission the event tap
/// already has) and have it converted. Never the clipboard. On its own thread: an app that does not answer can hold an
/// accessibility call for the whole messaging timeout. The selected text is never logged.
/// </summary>
internal sealed unsafe class SelectionReader : IDisposable
{
    public sealed record Request(ushort WindowLang, long Stamp, string App);

    private const float TimeoutSeconds = 0.5f;
    private const int MaxMs = 1500;

    private static readonly nint FocusedAttr = CreateString("AXFocusedUIElement");
    private static readonly nint SelectedTextAttr = CreateString("AXSelectedText");
    private static readonly nint ValueAttr = CreateString("AXValue");

    private readonly SessionController _controller;
    private readonly Corrector _corrector;
    private readonly Func<string> _app;
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private Request? _pending;
    private volatile bool _stop;

    public SelectionReader(SessionController controller, Corrector corrector, Func<string> app)
    {
        _controller = controller;
        _corrector = corrector;
        _app = app;
        _thread = new Thread(Loop) { Name = "AutoLang.Selection", IsBackground = true };
    }

    public void Start() => _thread.Start();

    /// <summary>From the tap callback: look for a selection. A newer request replaces one not yet started.</summary>
    public void Look(Request request)
    {
        Volatile.Write(ref _pending, request);
        _signal.Set();
    }

    private void Loop()
    {
        while (!_stop)
        {
            _signal.WaitOne();
            if (_stop) break;
            if (Interlocked.Exchange(ref _pending, null) is not { } r) continue;
            try { Handle(r); }
            catch (Exception ex) { Log.Selection(r.App, $"failed ({ex.GetType().Name})", 0, 0); }
        }
    }

    private void Handle(Request r)
    {
        var sw = Stopwatch.StartNew();
        var text = Read(out var reason);
        double ms = sw.Elapsed.TotalMilliseconds;
        if (text is null) { Log.Selection(r.App, "none: " + reason, 0, ms); return; }
        int length = text.Length;
        if (ms > MaxMs) { Log.Selection(r.App, "dropped: too slow", length, ms); return; }
        if (_app() != r.App) { Log.Selection(r.App, "dropped: app changed", length, ms); return; }

        var c = _controller.ConvertSelection(text, r.WindowLang, r.Stamp, out var why);
        text = null; // the corrector gets the converted text; the selection itself goes no further
        if (c is null) { Log.Selection(r.App, "left alone: " + why.ToString().ToLowerInvariant(), length, ms); return; }
        _corrector.Enqueue(new Corrector.Job(c, r.App, ShiftEnter: false));
        Log.Selection(r.App, "converted", length, ms);
    }

    /// <summary>The focused control's selected text, or null with why not.</summary>
    private static string? Read(out string reason)
    {
        var system = AXUIElementCreateSystemWide();
        if (system == 0) { reason = "no accessibility"; return null; }
        nint focused;
        try
        {
            AXUIElementSetMessagingTimeout(system, TimeoutSeconds);
            if (AXUIElementCopyAttributeValue(system, FocusedAttr, out focused) != 0 || focused == 0) { reason = "no focused control"; return null; }
        }
        finally { CFRelease(system); }
        try
        {
            AXUIElementSetMessagingTimeout(focused, TimeoutSeconds);
            // Text on a web page or in a document view can be selected but not typed over: the keys would reach the app
            // as shortcuts instead. An editable control lets its value be set.
            if (AXUIElementIsAttributeSettable(focused, ValueAttr, out var settable) != 0 || settable == 0) { reason = "read-only"; return null; }
            if (AXUIElementCopyAttributeValue(focused, SelectedTextAttr, out var selected) != 0 || selected == 0) { reason = "control exposes no selection"; return null; }
            try
            {
                var text = ToManaged(selected);
                if (text.Length == 0) { reason = "nothing selected"; return null; }
                reason = "";
                return text;
            }
            finally { CFRelease(selected); }
        }
        finally { CFRelease(focused); }
    }

    public void Dispose()
    {
        _stop = true;
        _signal.Set();
        if (_thread.IsAlive && !_thread.Join(1000)) Log.Warn("selection thread did not stop in time");
    }
}
