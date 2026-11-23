using System.Diagnostics;
using System.Globalization;
using AutoLang.Session;

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
    private const int SkipLinesPerMinute = 30;
    private static readonly object SkipGate = new();
    private static DateTime _skipWindow, _clearSince;
    private static int _skipLines, _skipSuppressed, _clearRuns;
    private static double _clearClosest;

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

    /// <summary>The hotkey looked for selected text. Outcome code and length only, never the text.</summary>
    public static void Selection(string app, string outcome, int chars, double ms)
        => Write("sel ", $"{outcome} in {app}{(chars > 0 ? $", {chars} chars" : "")} ({ms.ToString("F0", CultureInfo.InvariantCulture)} ms)");
    public static void Warn(string message) => Write("WARN", message);

    /// <summary>A correction was injected. Sizes and timings only.</summary>
    public static void Correction(string kind, string app, int backspaces, int typed, double margin, double injectMs)
        => Write("corr", $"{kind} in {app}: -{backspaces} +{typed} chars, margin {margin.ToString("F1", CultureInfo.InvariantCulture)}, inject {injectMs.ToString("F2", CultureInfo.InvariantCulture)} ms");

    /// <summary>
    /// A finished run was left as typed, and why. Reason code, lengths and scores only. A clear keep (the typed language
    /// plainly won) is what nearly every word is, so those are summed into one line a minute at most; every other reason
    /// is written per run, at most <see cref="SkipLinesPerMinute"/> a minute, the rest counted on the next line.
    /// </summary>
    public static void Skip(SkippedRun s, string process)
    {
        string? summary = null, line = null;
        var now = DateTime.Now;
        lock (SkipGate)
        {
            if (s.Reason == SkipReason.Clear)
            {
                if (_clearRuns++ == 0) { _clearSince = now; _clearClosest = s.Margin; }
                else _clearClosest = Math.Max(_clearClosest, s.Margin);
            }
            if (_clearRuns > 0 && now - _clearSince >= TimeSpan.FromMinutes(1))
            {
                summary = $"clear: {_clearRuns} run(s) kept in the typed language since {_clearSince:HH:mm:ss}, closest margin {F1(_clearClosest)}";
                _clearRuns = 0;
            }
            if (s.Reason != SkipReason.Clear)
            {
                if (now - _skipWindow >= TimeSpan.FromMinutes(1)) { _skipWindow = now; _skipLines = 0; }
                if (_skipLines < SkipLinesPerMinute)
                {
                    _skipLines++;
                    line = SkipLine(s, process) + (_skipSuppressed > 0 ? $" (+{_skipSuppressed} not logged)" : "");
                    _skipSuppressed = 0;
                }
                else _skipSuppressed++;
            }
        }
        if (summary is not null) Write("skip", summary);
        if (line is not null) Write("skip", line);
    }

    private static string SkipLine(SkippedRun s, string process)
    {
        var langs = s.TargetLangId == 0 ? Lang(s.FromLangId) : $"{Lang(s.FromLangId)}->{Lang(s.TargetLangId)}";
        var text = $"{SkipCode(s.Reason)} in {process}: {langs}, {s.Chars} chars";
        if (s.Threshold > 0)
            text += $", {s.Words} word(s)/{s.Letters} letters, margin {F1(s.Margin)} of {F1(s.Threshold)}, in dictionary: typed {s.TypedKnown}/{s.Words}, other {s.OtherKnown}/{s.Words}";
        return text;
    }

    private static string SkipCode(SkipReason r) => r switch
    {
        SkipReason.BelowThreshold => "below-threshold",
        SkipReason.Short => "short (judged with the next word)",
        SkipReason.Learnt => "learnt word",
        SkipReason.Retyping => "retyping an erased correction",
        SkipReason.Retyped => "retyped an erased correction (now learnt)",
        SkipReason.NoTarget => "no layout to switch to",
        SkipReason.Address => "address",
        SkipReason.NoWords => "nothing to score",
        SkipReason.NoProfile => "no word list",
        SkipReason.LayoutChanged => "layout changed mid-word",
        SkipReason.Unmanaged => "layout not handled",
        _ => r.ToString().ToLowerInvariant(),
    };

    private static string Lang(ushort langId)
    {
        try { return CultureInfo.GetCultureInfo(langId).Name; }
        catch (CultureNotFoundException) { return $"{langId:x4}"; }
    }
    private static string F1(double v) => v.ToString("F1", CultureInfo.InvariantCulture);

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
