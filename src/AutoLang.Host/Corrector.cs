using System.Collections.Concurrent;
using System.Diagnostics;
using AutoLang.Session;

namespace AutoLang.Host;

/// <summary>
/// Applies corrections on its own thread. The M0 spike measured SendInput at 4–22 ms inside the hook callback,
/// far over the ~5 ms budget, so the hook only enqueues and this thread does the injecting and the layout switch.
/// </summary>
internal sealed class Corrector : IDisposable
{
    public sealed record Job(Correction Correction, nint Hwnd, nint TargetHkl, string Process, bool ShiftEnter, bool CapsLock);

    private readonly BlockingCollection<Job> _queue = new();
    private readonly Thread _thread;
    private readonly Func<string, TypingMethod> _methodFor;
    private int _applied;

    public event Action<Job>? Applied;
    public int AppliedCount => Volatile.Read(ref _applied);

    /// <param name="methodFor">The typing method chosen for a process (PLAN.md §3.3); called on the corrector thread.</param>
    public Corrector(Func<string, TypingMethod> methodFor)
    {
        _methodFor = methodFor;
        _thread = new Thread(Loop) { Name = "AutoLang.Corrector", IsBackground = true, Priority = ThreadPriority.AboveNormal };
    }

    public void Start() => _thread.Start();

    public void Enqueue(Job job) => _queue.Add(job);

    private void Loop()
    {
        foreach (var job in _queue.GetConsumingEnumerable())
        {
            try
            {
                var c = job.Correction;
                var sw = Stopwatch.StartNew();
                var method = _methodFor(job.Process);
                (uint sent, uint wanted)? result = method switch
                {
                    // Keys switches the layout itself, before typing; the other methods switch after.
                    TypingMethod.Keys => Injector.ReplaceWithKeys(job.Hwnd, job.TargetHkl, c.Backspaces, c.Text, c.PressEnter, job.ShiftEnter, job.CapsLock),
                    TypingMethod.Paste => ClipboardPaste.Replace(c.Backspaces, c.Text, c.PressEnter, job.ShiftEnter),
                    _ => null,
                };
                if (result is null) method = TypingMethod.Unicode; // the chosen method declined; Unicode always works
                var (sent, wanted) = result ?? Injector.Replace(c.Backspaces, c.Text, c.PressEnter, job.ShiftEnter);
                if (job.TargetHkl != 0 && job.Hwnd != 0 && method != TypingMethod.Keys) Injector.SwitchLayout(job.Hwnd, job.TargetHkl);
                Log.Correction(c.Kind.ToString().ToLowerInvariant(), job.Process, method, c.Backspaces, c.Text.Length, c.Margin, sw.Elapsed.TotalMilliseconds, sent, wanted);
                Interlocked.Increment(ref _applied);
                Applied?.Invoke(job);
            }
            catch (Exception ex) { Log.Warn($"corrector: {ex.GetType().Name}: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        if (_thread.IsAlive && !_thread.Join(500)) Log.Warn("corrector thread did not stop in time");
    }
}
