namespace AutoLang.Linux;

/// <summary>
/// Spike log: ~/.local/state/autolang/spike.log (XDG_STATE_HOME), and stderr. Typed text is never written unless
/// AUTOLANG_LOG_TEXT=1, which is for test machines only.
/// </summary>
internal static class Log
{
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

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            _path = Path.Combine(Dir, "spike.log");
        }
        catch (Exception) { _path = null; }
    }

    public static void Info(string message) => Write("info", message);
    public static void Warn(string message) => Write("warn", message);

    /// <summary>Text the user typed: logged only with AUTOLANG_LOG_TEXT=1, else its length.</summary>
    public static string Show(string s) => Text ? $"\"{s}\"" : $"<{s.Length} chars>";

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {level} {message}";
        lock (Gate)
        {
            Console.Error.WriteLine(line);
            if (_path is null) return;
            try { File.AppendAllText(_path, line + Environment.NewLine); }
            catch (Exception) { /* a log must never break typing */ }
        }
    }
}
