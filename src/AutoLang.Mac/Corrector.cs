using System.Collections.Concurrent;
using System.Diagnostics;
using AutoLang.Session;
using static AutoLang.Mac.Native;

namespace AutoLang.Mac;

/// <summary>
/// Applies corrections on its own thread, like the Windows host: the tap callback runs on the main thread and must stay
/// fast, so it only enqueues. Injection is CGEventPost of Backspaces, the text as Unicode key events, and Enter;
/// then the input source is switched on the main thread (TIS calls belong there).
/// </summary>
internal sealed class Corrector : IDisposable
{
    /// <summary>Marks our own events (kCGEventSourceUserData) so the tap lets them through untouched. 'LANG', as on Windows.</summary>
    public const long Signature = 0x4C414E47;

    private const ushort KeyBackspace = 0x33, KeyReturn = 0x24;

    public sealed record Job(Correction Correction, string App, bool ShiftEnter);
    private sealed record Switch(ushort LangId);

    private readonly BlockingCollection<object> _queue = new();
    private readonly Thread _thread;
    private readonly Action<ushort> _select;
    private nint _source;

    public event Action<Job>? Applied;

    /// <param name="select">Selects a language's input source; called on the main thread.</param>
    public Corrector(Action<ushort> select)
    {
        _select = select;
        _thread = new Thread(Loop) { Name = "AutoLang.Corrector", IsBackground = true, Priority = ThreadPriority.AboveNormal };
    }

    public void Start() => _thread.Start();

    public void Enqueue(Job job) => _queue.Add(job);

    /// <summary>Switch back to a language after any correction queued before it (the user erased our correction).</summary>
    public void EnqueueSwitch(ushort langId) => _queue.Add(new Switch(langId));

    private void Loop()
    {
        // A private source: no modifier state leaks in from the keys the user is holding.
        _source = CGEventSourceCreate(kCGEventSourceStatePrivate);
        if (_source != 0) CGEventSourceSetLocalEventsSuppressionInterval(_source, 0); // never mute the user's own keys
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            try
            {
                if (item is Switch s)
                {
                    ObjC.OnMain(() => _select(s.LangId));
                    Log.Info("correction erased: input source switched back");
                    continue;
                }
                var job = (Job)item;
                var c = job.Correction;
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < c.Backspaces; i++) Key(KeyBackspace, 0);
                TypeText(c.Text);
                if (c.PressEnter) Key(KeyReturn, job.ShiftEnter ? kCGEventFlagMaskShift : 0);
                var injectMs = sw.Elapsed.TotalMilliseconds;
                ObjC.OnMain(() => _select(c.TargetLangId));
                Log.Correction(c.Kind.ToString().ToLowerInvariant(), job.App, c.Backspaces, c.Text.Length, c.Margin, injectMs);
                Applied?.Invoke(job);
            }
            catch (Exception ex) { Log.Warn($"corrector: {ex.GetType().Name}: {ex.Message}"); }
        }
        if (_source != 0) CFRelease(_source);
    }

    /// <summary>A key press and release with exactly these modifier flags.</summary>
    private void Key(ushort keyCode, ulong flags)
    {
        Post(keyCode, true, flags, default);
        Post(keyCode, false, flags, default);
    }

    /// <summary>
    /// Each character (a surrogate pair stays together) as a key event carrying the text itself, so it does not depend on
    /// the current input source. Key code 0 is a placeholder the text overrides.
    /// </summary>
    private void TypeText(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            int n = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
            var unit = text.AsSpan(i, n);
            Post(0, true, 0, unit);
            Post(0, false, 0, unit);
            i += n - 1;
        }
    }

    private unsafe void Post(ushort keyCode, bool down, ulong flags, ReadOnlySpan<char> text)
    {
        var ev = CGEventCreateKeyboardEvent(_source, keyCode, down ? (byte)1 : (byte)0);
        if (ev == 0) throw new InvalidOperationException("CGEventCreateKeyboardEvent failed");
        try
        {
            CGEventSetFlags(ev, flags);
            CGEventSetIntegerValueField(ev, kCGEventSourceUserData, Signature);
            if (!text.IsEmpty)
                fixed (char* p = text) CGEventKeyboardSetUnicodeString(ev, (nuint)text.Length, p);
            CGEventPost(kCGHIDEventTap, ev);
        }
        finally { CFRelease(ev); }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        if (_thread.IsAlive && !_thread.Join(500)) Log.Warn("corrector thread did not stop in time");
    }
}
