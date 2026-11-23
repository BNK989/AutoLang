using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using AutoLang.Session;

namespace AutoLang.Host;

/// <summary>
/// The hotkey's last resort: with nothing tracked to undo or convert, look for text the user selected in the focused
/// control and have it converted (<see cref="SessionController.ConvertSelection"/>). Runs on its own thread because UI
/// Automation can take hundreds of milliseconds and must never run in the hook callback.
/// <para>
/// Only UI Automation's <c>TextPattern</c> selection is read, never the clipboard: sending Ctrl+C would make the app put
/// the text into Windows clipboard history and the cloud clipboard, and with nothing selected some editors copy the whole
/// line. A control without <c>TextPattern</c>, or with nothing selected, means the hotkey does nothing. The selected text
/// lives in a local for the length of one conversion and is never logged (PLAN.md §6).
/// </para>
/// </summary>
internal sealed class SelectionReader : IDisposable
{
    /// <param name="LastHkl">The layout variant the user last typed with per language (a copy: the hook owns the original).</param>
    public sealed record Request(nint Hwnd, ushort WindowLang, long Stamp, string Process, bool CapsLock, IReadOnlyDictionary<ushort, nint> LastHkl);

    /// <summary>A reading slower than this is dropped: by then the user has given up on the hotkey.</summary>
    private const int MaxMs = 1500;

    private readonly SessionController _controller;
    private readonly Corrector _corrector;
    private readonly Func<ushort, nint> _hklOfLang;
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private Request? _pending;
    private volatile bool _stop;

    public SelectionReader(SessionController controller, Corrector corrector, Func<ushort, nint> hklOfLang)
    {
        _controller = controller;
        _corrector = corrector;
        _hklOfLang = hklOfLang;
        _thread = new Thread(Loop) { Name = "AutoLang.Selection", IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    public void Start() => _thread.Start();

    /// <summary>From the hook thread: look for a selection. Cheap; a newer request replaces one not yet started.</summary>
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
            catch (Exception ex) { Log.Selection(r.Process, $"failed ({ex.GetType().Name})", 0, 0); }
        }
    }

    private void Handle(Request r)
    {
        var sw = Stopwatch.StartNew();
        string? text;
        string reason;
        try { text = Read(r.Hwnd, out reason); }
        catch (Exception ex) { text = null; reason = "uia:" + ex.GetType().Name; } // UIA is best effort: some apps expose nothing
        double ms = sw.Elapsed.TotalMilliseconds;
        if (text is null) { Log.Selection(r.Process, "none: " + reason, 0, ms); return; }
        int length = text.Length;
        if (ms > MaxMs) { Log.Selection(r.Process, "dropped: too slow", length, ms); return; }
        if (Native.GetForegroundWindow() != r.Hwnd) { Log.Selection(r.Process, "dropped: window changed", length, ms); return; }

        var c = _controller.ConvertSelection(text, r.WindowLang, r.Stamp, out var why);
        text = null; // the corrector gets the converted text; the selection itself goes no further
        if (c is null) { Log.Selection(r.Process, "left alone: " + Code(why), length, ms); return; }
        var hkl = r.LastHkl.TryGetValue(c.TargetLangId, out var last) ? last : _hklOfLang(c.TargetLangId);
        _corrector.Enqueue(new Corrector.Job(c, r.Hwnd, hkl, r.Process, ShiftEnter: false, r.CapsLock));
        Log.Selection(r.Process, $"converted {LanguageManager.CultureOf(c.FromLangId)}->{LanguageManager.CultureOf(c.TargetLangId)}", length, ms);
    }

    /// <summary>The focused control's selected text, or null with why not. Reads at most one char past the limit.</summary>
    private static string? Read(nint hwnd, out string reason)
    {
        var el = AutomationElement.FocusedElement;
        if (el is null) { reason = "no focused control"; return null; }
        if (!el.TryGetCurrentPattern(TextPattern.Pattern, out var p)) { reason = "control exposes no text"; return null; }
        var tp = (TextPattern)p;
        if (tp.SupportedTextSelection == SupportedTextSelection.None) { reason = "control has no selection"; return null; }
        var ranges = tp.GetSelection();
        if (ranges.Length == 0) { reason = "nothing selected"; return null; }
        if (ranges.Length > 1) { reason = "several selections"; return null; }
        var range = ranges[0];
        // Text on a web page or in a document view can be selected but not typed over: the keys would reach the app as
        // shortcuts instead.
        if (IsReadOnly(el, range)) { reason = "read-only"; return null; }
        var text = range.GetText(SessionController.MaxSelectionChars + 1);
        if (text.Length == 0) { reason = "nothing selected"; return null; }
        // Belt and braces: focus must still be in the window the hotkey was pressed in.
        if (Native.GetForegroundWindow() != hwnd) { reason = "window changed"; return null; }
        reason = "";
        return text;
    }

    private static bool IsReadOnly(AutomationElement el, TextPatternRange range)
    {
        if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var v) && ((ValuePattern)v).Current.IsReadOnly) return true;
        return range.GetAttributeValue(TextPattern.IsReadOnlyAttribute) is true;
    }

    private static string Code(SelectionRefusal why) => why switch
    {
        SelectionRefusal.LineBreak => "spans lines",
        SelectionRefusal.TooLong => "too long",
        SelectionRefusal.NoLetters => "no letters of an active layout",
        SelectionRefusal.NoTarget => "no layout to switch to",
        SelectionRefusal.Stale => "input arrived meanwhile",
        _ => why.ToString().ToLowerInvariant(),
    };

    public void Dispose()
    {
        _stop = true;
        _signal.Set();
        if (_thread.IsAlive && !_thread.Join(1000)) Log.Warn("selection thread did not stop in time");
    }
}
