using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoLang.Host;

/// <summary>
/// The only thing the host persists (%LocalAppData%\AutoLang\settings.json): the user's own choices. Never typed text.
/// </summary>
public sealed class Settings
{
    public static string Path_ => Path.Combine(Log.Dir, "settings.json");

    public bool Paused { get; set; }

    /// <summary>The welcome card (<see cref="WelcomeCard"/>) has been shown once; a second launch still brings it back.</summary>
    public bool WelcomeShown { get; set; }

    /// <summary>The one-time "That was AutoLang" notice after the first automatic fix has been shown.</summary>
    public bool FirstFixNoticeShown { get; set; }

    /// <summary>Skip borderless-fullscreen windows (games, video). PLAN.md §6 default exclusion.</summary>
    public bool SkipFullscreen { get; set; } = true;

    /// <summary>Processes AutoLang never touches: terminals, remote desktops, VMs, password managers, credential UI (PLAN.md §6). Editable.</summary>
    public List<string> ExcludedProcesses { get; set; } = new(DefaultExclusions);

    /// <summary>Processes the user disabled from the tray menu.</summary>
    public List<string> DisabledProcesses { get; set; } = new();

    /// <summary>Words the user undid a correction on: never auto-corrected again.</summary>
    public List<string> ExceptionWords { get; set; } = new();

    /// <summary>
    /// Installed languages (culture names, e.g. "de-DE") the user switched off: AutoLang ignores typing in them and never
    /// switches to them. The inactive set is stored, not the active one, so a keyboard added later starts out active.
    /// </summary>
    public List<string> InactiveLanguages { get; set; } = new();

    /// <summary>Languages AutoLang may correct <em>from</em> but must never switch <em>to</em> (culture names).</summary>
    public List<string> NeverSwitchTo { get; set; } = new();

    /// <summary>Fetch a missing word list from FrequencyWords when a keyboard needs one. The only network use (PLAN.md §6).</summary>
    public bool AutoDownloadWordLists { get; set; } = true;

    /// <summary>
    /// Per-app override of how a correction is typed (exe name → method), for the few apps that mishandle the default
    /// Unicode injection. Set from the tray menu; apps not listed use <see cref="TypingMethod.Unicode"/>.
    /// </summary>
    public Dictionary<string, TypingMethod> TypingMethods { get; set; } = new();

    public TypingMethod TypingMethodFor(string process)
    {
        foreach (var (app, method) in TypingMethods)
            if (app.Equals(process, StringComparison.OrdinalIgnoreCase)) return method;
        return TypingMethod.Unicode;
    }

    public void SetTypingMethod(string process, TypingMethod method)
    {
        foreach (var app in TypingMethods.Keys.Where(k => k.Equals(process, StringComparison.OrdinalIgnoreCase)).ToList())
            TypingMethods.Remove(app);
        if (method != TypingMethod.Unicode) TypingMethods[process] = method;
    }

    public static readonly string[] DefaultExclusions =
    {
        // terminals (every keystroke may be a command; the shell has its own editing)
        "WindowsTerminal.exe", "OpenConsole.exe", "conhost.exe", "cmd.exe", "powershell.exe", "pwsh.exe", "wt.exe",
        "mintty.exe", "putty.exe", "kitty.exe", "alacritty.exe", "wezterm-gui.exe", "Hyper.exe", "Tabby.exe",
        // remote / virtual machines (the keys belong to another machine)
        "mstsc.exe", "msrdc.exe", "vmware", "VirtualBox", "parsec", "wfica32.exe", "CDViewer.exe", "Citrix",
        "AnyDesk.exe", "TeamViewer.exe", "vncviewer.exe", "RustDesk.exe", "chrome_remote", "SelfService.exe",
        // password managers
        "1Password.exe", "KeePass.exe", "KeePassXC.exe", "Bitwarden.exe", "LastPass.exe", "Dashlane.exe", "Enpass.exe",
        "NordPass.exe", "RoboForm", "Proton Pass.exe",
        // credential and lock UI
        "CredentialUIBroker.exe", "consent.exe", "LogonUI.exe", "LockApp.exe", "AuthHost.exe",
    };

    /// <summary>Entry with ".exe" = exact process name; without = prefix (so "vmware" covers vmware.exe and vmware-vmx.exe).</summary>
    public static bool Matches(string process, string entry)
    {
        if (entry.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return process.Equals(entry, StringComparison.OrdinalIgnoreCase);
        return process.StartsWith(entry, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsExcluded(string process) => ExcludedProcesses.Any(e => Matches(process, e));
    public bool IsDisabled(string process) => DisabledProcesses.Any(e => e.Equals(process, StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
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
            Directory.CreateDirectory(Log.Dir);
            File.WriteAllText(Path_, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) { Log.Warn($"settings not saved: {ex.Message}"); }
    }
}

/// <summary>How the Corrector types a replacement (PLAN.md §3.3).</summary>
public enum TypingMethod
{
    /// <summary>Default: characters as Unicode packets (VK_PACKET), independent of the keyboard layout.</summary>
    Unicode,
    /// <summary>Switch the layout first, then press the real keys that type each character on it.</summary>
    Keys,
    /// <summary>Put the text on the clipboard, press Ctrl+V, then put the user's clipboard back.</summary>
    Paste,
}
