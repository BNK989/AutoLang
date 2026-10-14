using System.Text.Json;

namespace AutoLang.Mac;

/// <summary>
/// The only thing the host persists (~/Library/Application Support/AutoLang/settings.json): the user's own choices.
/// Never typed text.
/// </summary>
public sealed class Settings
{
    public static string Path_ => Path.Combine(Log.DataDir, "settings.json");

    public bool Paused { get; set; }

    /// <summary>Words the user undid a correction on: never auto-corrected again.</summary>
    public List<string> ExceptionWords { get; set; } = new();

    /// <summary>Apps (bundle identifiers) AutoLang never touches. Editable in the JSON file.</summary>
    public List<string> ExcludedApps { get; set; } = new(DefaultExclusions);

    /// <summary>
    /// Terminals (every keystroke may be a command), remote desktops and VMs (the keys belong to another machine),
    /// password managers. Password fields everywhere are covered by Secure Event Input, which macOS turns on for them.
    /// </summary>
    public static readonly string[] DefaultExclusions =
    {
        "com.apple.Terminal", "com.googlecode.iterm2", "dev.warp.Warp-Stable", "net.kovidgoyal.kitty", "io.alacritty",
        "com.github.wez.wezterm", "co.zeit.hyper", "com.mitchellh.ghostty",
        "com.microsoft.rdc.macos", "com.apple.ScreenSharing", "com.parallels.desktop.console", "com.vmware.fusion",
        "org.virtualbox.app.VirtualBoxVM", "com.utmapp.UTM", "com.teamviewer.TeamViewer", "com.anydesk.anydeskmac",
        "com.apple.keychainaccess", "com.1password.1password", "com.agilebits.onepassword7", "com.bitwarden.desktop",
        "com.apple.Passwords",
    };

    public bool IsExcluded(string? bundleId) =>
        bundleId is not null && ExcludedApps.Any(e => e.Equals(bundleId, StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(Path_)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path_), JsonOptions) ?? new Settings();
        }
        catch (Exception ex) { Log.Warn($"settings unreadable, using defaults: {ex.Message}"); }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Log.DataDir);
            File.WriteAllText(Path_, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) { Log.Warn($"settings not saved: {ex.Message}"); }
    }
}
