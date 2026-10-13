using System.Diagnostics;
using System.Globalization;

namespace AutoLang.Host;

/// <summary>
/// Diagnostics log at %LocalAppData%\AutoLang\host.log. Rule (PLAN.md §6): it never contains typed text, not even a
/// single character. What it may contain: lifecycle messages, layout and process names, counts, margins and timings.
/// The structured entry points below are the only ones the correction path uses, so content cannot leak by accident.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static StreamWriter? _w;
    private static int _slowCallbacks;

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoLang");
    public static string Path_ => Path.Combine(Dir, "host.log");

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            if (File.Exists(Path_) && new FileInfo(Path_).Length > 2 * 1024 * 1024) File.Delete(Path_);
            _w = new StreamWriter(Path_, append: true) { AutoFlush = true };
            Info($"---- AutoLang host start, pid {Environment.ProcessId}, {Environment.OSVersion}");
        }
        catch (Exception ex) { Debug.WriteLine($"log init failed: {ex.Message}"); }
    }

    public static void Info(string message) => Write("info", message);
    public static void Warn(string message) => Write("WARN", message);

    /// <summary>A correction was injected. Sizes and timings only.</summary>
    public static void Correction(string kind, string process, TypingMethod method, int backspaces, int typed, double margin, double injectMs, uint sent, uint wanted)
        => Write("corr", $"{kind} in {process}{(method == TypingMethod.Unicode ? "" : $" ({method.ToString().ToLowerInvariant()})")}: -{backspaces} +{typed} chars, margin {margin.ToString("F1", CultureInfo.InvariantCulture)}, inject {injectMs.ToString("F2", CultureInfo.InvariantCulture)} ms, sent {sent}/{wanted}");

    /// <summary>The focus worker resolved the foreground context.</summary>
    public static void Focus(string process, string control, bool blocked, string reason, double ms)
        => Write("focs", $"{process} [{control}] {(blocked ? "BLOCKED: " + reason : "active")} ({ms.ToString("F0", CultureInfo.InvariantCulture)} ms)");

    public static void SlowCallback(double ms)
    {
        int n = Interlocked.Increment(ref _slowCallbacks);
        if (n <= 20 || n % 100 == 0) Write("slow", $"hook callback took {ms.ToString("F1", CultureInfo.InvariantCulture)} ms (#{n})");
    }

    private static void Write(string tag, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {tag} {message}";
        Debug.WriteLine(line);
        lock (Gate)
        {
            try { _w?.WriteLine(line); } catch { /* logging must never take the host down */ }
        }
    }
}
