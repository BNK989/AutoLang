namespace M0Spike;

internal static class Log
{
    private static readonly object Gate = new();

    public static void Info(string msg) => Write("     ", msg);
    public static void Warn(string msg) => Write("WARN ", msg);

    private static void Write(string tag, string msg)
    {
        lock (Gate) Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {tag}{msg}");
    }
}
