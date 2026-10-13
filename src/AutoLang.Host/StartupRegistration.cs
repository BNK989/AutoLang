using Microsoft.Win32;

namespace AutoLang.Host;

/// <summary>
/// "Start with Windows": a per-user value under HKCU\...\Run (no admin rights, no installer). The registry is the
/// source of truth, so the tray checkmark also reflects an entry added or removed outside the app.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AutoLang";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>True when the Run entry points at the exe that is running now.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string v && v.Equals(Command, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) { Log.Warn($"startup entry unreadable: {ex.Message}"); return false; }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, Command);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
            Log.Info($"start with Windows: {(enabled ? "on" : "off")}");
            return true;
        }
        catch (Exception ex) { Log.Warn($"startup entry not saved: {ex.Message}"); return false; }
    }
}
