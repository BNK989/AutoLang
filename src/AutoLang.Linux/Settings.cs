using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoLang.Linux;

/// <summary>
/// ~/.config/autolang/settings.json (XDG_CONFIG_HOME). Plain JSON on purpose: it is the documented way to change the
/// layouts on Linux. Created on first use with the XKB layouts GNOME had before AutoLang was added (us + il when
/// there are fewer than two). AUTOLANG_LAYOUTS overrides the file for one run (the tests use it).
/// </summary>
internal sealed class Settings
{
    [JsonPropertyName("layouts")] public List<string> Layouts { get; set; } = [];
    [JsonPropertyName("paused")] public bool Paused { get; set; }
    /// <summary>Words the user undid or erased and retyped: never corrected again.</summary>
    [JsonPropertyName("exceptionWords")] public List<string> ExceptionWords { get; set; } = [];

    public static string Dir
    {
        get
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(config))
                config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(config, "autolang");
        }
    }

    public static string FilePath => Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = SettingsJson.Default,
    };

    public static Settings Load()
    {
        Settings? s = null;
        try
        {
            if (File.Exists(FilePath)) s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options);
        }
        catch (Exception ex) { Log.Warn($"settings.json unreadable ({ex.Message}); using defaults"); }
        bool created = s is null;
        s ??= new Settings();
        if (s.Layouts.Count == 0)
        {
            s.Layouts = Languages.GnomeXkbSources();
            if (s.Layouts.Count < 2) s.Layouts = ["us", "il"];
            created = true;
        }
        if (created) s.Save();
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex) { Log.Warn($"saving settings failed: {ex.Message}"); }
    }

    /// <summary>The layouts for this run: AUTOLANG_LAYOUTS ("us,il") when set, else the file's.</summary>
    public IReadOnlyList<string> EffectiveLayouts()
    {
        var env = Environment.GetEnvironmentVariable("AUTOLANG_LAYOUTS");
        return string.IsNullOrWhiteSpace(env)
            ? Layouts
            : env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal partial class SettingsJson : JsonSerializerContext;
