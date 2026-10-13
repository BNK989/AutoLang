using Xunit;

namespace AutoLang.Engine.Tests;

/// <summary>Loads /data once per test run. Tests that need files the repo doesn't ship (word lists) skip cleanly.</summary>
public static class Fixtures
{
    public static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly Lazy<KeyboardLayoutTable[]> LayoutsLazy = new(() =>
        Directory.Exists(Path.Combine(DataDir, "layouts"))
            ? Directory.GetFiles(Path.Combine(DataDir, "layouts"), "*.json").Select(KeyboardLayoutTable.Load).ToArray()
            : Array.Empty<KeyboardLayoutTable>());

    public static KeyboardLayoutTable[] Layouts => LayoutsLazy.Value;
    public static KeyboardLayoutTable English => Layouts.First(l => (l.LangId & 0xFF) == 0x09);
    public static KeyboardLayoutTable Hebrew => Layouts.First(l => (l.LangId & 0xFF) == 0x0D);
    public static bool HaveLayouts => Layouts.Any(l => (l.LangId & 0xFF) == 0x09) && Layouts.Any(l => (l.LangId & 0xFF) == 0x0D);

    private static readonly Lazy<PhraseDecider?> DeciderLazy = new(() =>
    {
        var he = Path.Combine(DataDir, "words", "he_50k.txt");
        var en = Path.Combine(DataDir, "words", "en_50k.txt");
        if (!HaveLayouts || !File.Exists(he) || !File.Exists(en)) return null;
        var profiles = new[]
        {
            LanguageProfile.FromFrequencyFile(Hebrew.LangId, "he", he, LanguageProfile.DefaultRulesFor(Hebrew.LangId), LanguageProfile.DefaultNormalizeFor(Hebrew.LangId)),
            LanguageProfile.FromFrequencyFile(English.LangId, "en", en, LanguageProfile.DefaultRulesFor(English.LangId), LanguageProfile.DefaultNormalizeFor(English.LangId)),
        };
        return new PhraseDecider(profiles);
    });

    public static PhraseDecider? Decider => DeciderLazy.Value;
}

/// <summary>Fact that skips when the real layout dumps aren't present.</summary>
public sealed class LayoutFactAttribute : FactAttribute
{
    public LayoutFactAttribute()
    {
        if (!Fixtures.HaveLayouts) Skip = "data/layouts/*.json missing — run `M0Spike dumpjson data/layouts`";
    }
}

/// <summary>Fact that skips when layouts or word lists are missing.</summary>
public sealed class DataFactAttribute : FactAttribute
{
    public DataFactAttribute()
    {
        if (Fixtures.Decider is null) Skip = "data/layouts or data/words missing";
    }
}

/// <summary>Theory that skips when layouts or word lists are missing.</summary>
public sealed class DataTheoryAttribute : TheoryAttribute
{
    public DataTheoryAttribute()
    {
        if (Fixtures.Decider is null) Skip = "data/layouts or data/words missing";
    }
}
