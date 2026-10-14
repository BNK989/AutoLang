using System.Diagnostics;
using System.Globalization;

namespace AutoLang.Mac;

/// <summary>
/// Diagnostics log at ~/Library/Logs/AutoLang/host.log. Same rule as the Windows host (PLAN.md §6): it never contains
/// typed text, not even a single character. Lifecycle messages, layout and app names, counts, margins and timings only.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static StreamWriter? _w;
    private static int _slowCallbacks;

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static string Dir => Path.Combine(Home, "Library", "Logs", "AutoLang");
    public static string Path_ => Path.Combine(Dir, "host.log");
    /// <summary>Where settings live (not the log folder: macOS keeps preferences and logs apart).</summary>
    public static string DataDir => Path.Combine(Home, "Library", "Application Support", "AutoLang");

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            if (File.Exists(Path_) && new FileInfo(Path_).Length > 2 * 1024 * 1024) File.Delete(Path_);
            _w = new StreamWriter(Path_, append: true) { AutoFlush = true };
            Info($"---- AutoLang (macOS) start, pid {Environment.ProcessId}, {Environment.OSVersion}, {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        }
        catch (Exception ex) { Debug.WriteLine($"log init failed: {ex.Message}"); }
    }

    public static void Info(string message) => Write("info", message);
    public static void Warn(string message) => Write("WARN", message);

    /// <summary>A correction was injected. Sizes and timings only.</summary>
    public static void Correction(string kind, string app, int backspaces, int typed, double margin, double injectMs)
        => Write("corr", $"{kind} in {app}: -{backspaces} +{typed} chars, margin {margin.ToString("F1", CultureInfo.InvariantCulture)}, inject {injectMs.ToString("F2", CultureInfo.InvariantCulture)} ms");

    public static void SlowCallback(double ms)
    {
        int n = Interlocked.Increment(ref _slowCallbacks);
        if (n <= 20 || n % 100 == 0) Write("slow", $"tap callback took {ms.ToString("F1", CultureInfo.InvariantCulture)} ms (#{n})");
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
