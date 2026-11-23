namespace AutoLang.Linux;

/// <summary>
/// ~/.local/state/autolang/autolang.log (XDG_STATE_HOME), and stderr. Records timing, counts and decisions, never what
/// was typed: text appears only with AUTOLANG_LOG_TEXT=1, which is for test machines. Over 2 MB at start-up the log
/// moves to autolang.log.old, so it never grows without bound.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();
    private static string? _path;

    public static readonly bool Text = Environment.GetEnvironmentVariable("AUTOLANG_LOG_TEXT") == "1";

    public static string Dir
    {
        get
        {
            var state = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            if (string.IsNullOrEmpty(state))
                state = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
            return Path.Combine(state, "autolang");
        }
    }

    public static string FilePath => Path.Combine(Dir, "autolang.log");

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var info = new FileInfo(FilePath);
            if (info.Exists && info.Length > MaxBytes) File.Move(FilePath, FilePath + ".old", overwrite: true);
            _path = FilePath;
        }
        catch (Exception) { _path = null; }
    }

    public static void Info(string message) => Write("info", message);
    public static void Warn(string message) => Write("warn", message);

    /// <summary>Text the user typed: logged only with AUTOLANG_LOG_TEXT=1, else its length.</summary>
    public static string Show(string s) => Text ? $"\"{s}\"" : $"<{s.Length} chars>";

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}";
        lock (Gate)
        {
            Console.Error.WriteLine(line);
            if (_path is null) return;
            try { File.AppendAllText(_path, line + Environment.NewLine); }
            catch (Exception) { /* a log must never break typing */ }
        }
    }
}
