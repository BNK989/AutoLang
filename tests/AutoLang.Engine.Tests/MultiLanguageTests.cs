using AutoLang.Session;
using Xunit;

namespace AutoLang.Engine.Tests;

/// <summary>
/// The alphabet rule of PLAN.md §2 with more than two keyboards: layouts are grouped by script, a run is only corrected
/// into another script, the user's last-used member of that script wins, and the user's language choices are honoured.
/// German is synthetic: the English keys with Y and Z swapped (as on a QWERTZ keyboard), sharing the English word list.
/// </summary>
public class MultiLanguageTests
{
    private const ushort De = 0x0407;
    private static ushort En => Fixtures.English.LangId;
    private static ushort He => Fixtures.Hebrew.LangId;

    private static readonly Lazy<KeyboardLayoutTable> GermanLazy = new(() =>
    {
        var keys = new List<KeyValuePair<Key, string>>();
        for (uint vk = 0x20; vk <= 0xFE; vk++)
            foreach (var shift in new[] { false, true })
            {
                uint src = vk switch { 0x59 => 0x5A, 0x5A => 0x59, _ => vk }; // Y ⇄ Z
                if (Fixtures.English.TextFor(new Key(src, shift)) is { } t) keys.Add(new(new Key(vk, shift), t));
            }
        return new KeyboardLayoutTable("German (synthetic)", De, keys);
    });

    private static KeyboardLayoutTable German => GermanLazy.Value;

    /// <summary>English, Hebrew and German, German sharing the English list as the host does for one word list per language.</summary>
    private static PhraseDecider Decider()
    {
        var en = Path.Combine(Fixtures.DataDir, "words", "en_50k.txt");
        var he = Path.Combine(Fixtures.DataDir, "words", "he_50k.txt");
        var enP = LanguageProfile.FromFrequencyFile(En, "en", en, LanguageProfile.DefaultRulesFor(En), LanguageProfile.DefaultNormalizeFor(En));
        var heP = LanguageProfile.FromFrequencyFile(He, "he", he, LanguageProfile.DefaultRulesFor(He), LanguageProfile.DefaultNormalizeFor(He));
        var deP = new LanguageProfile(De, "de", enP.Dictionary, enP.Model, LanguageProfile.DefaultRulesFor(De), enP.Normalize);
        return new PhraseDecider(new[] { enP, heP, deP });
    }

    private static readonly Lazy<PhraseDecider> Three = new(Decider);

    private static Correction? Type(SessionController c, string text, ushort lang)
    {
        foreach (var ch in text)
            if (c.OnChar(ch, lang) is { } corr) return corr;
        return null;
    }

    [Fact]
    public void Scripts_are_named_by_alphabet()
    {
        Assert.Equal("Latin", Scripts.Of('a'));
        Assert.Equal("Latin", Scripts.Of('ü'));
        Assert.Equal("Hebrew", Scripts.Of('א'));
        Assert.Equal("Greek", Scripts.Of('ς'));
        Assert.Equal("Cyrillic", Scripts.Of('ж'));
        Assert.Null(Scripts.Of('1'));
        Assert.Null(Scripts.Of(' '));
        Assert.Equal("Hebrew", Scripts.Majority("שלום עולם hi"));
        Assert.Equal(Scripts.Unknown, Scripts.Majority("123 !?"));
    }

    [LayoutFact]
    public void Layouts_get_the_script_of_their_unshifted_letters()
    {
        Assert.Equal("Latin", Fixtures.English.Script);
        Assert.Equal("Hebrew", Fixtures.Hebrew.Script); // Shift gives Latin capitals; unshifted letters decide
        Assert.Equal("Latin", German.Script);
    }

    [DataFact]
    public void Same_alphabet_is_never_corrected()
    {
        var c = new SessionController(Three.Value, new[] { Fixtures.English, Fixtures.Hebrew, German });
        // "yes" typed on the German keyboard shows "zes", which English reads as a real word. Same alphabet: left alone.
        Assert.Null(Type(c, "zes ", De));
        Assert.Null(Type(c, "zes ", De));
    }

    [DataFact]
    public void Other_alphabet_goes_to_the_last_used_member_of_its_group()
    {
        var c = new SessionController(Three.Value, new[] { Fixtures.English, Fixtures.Hebrew, German });
        Assert.Null(Type(c, "good morning ", De));
        var corr = Type(c, "יקךךם ", He);
        Assert.NotNull(corr);
        Assert.Equal(De, corr!.TargetLangId);
        Assert.Equal("hello ", corr.Text);

        c = new SessionController(Three.Value, new[] { Fixtures.English, Fixtures.Hebrew, German });
        Assert.Null(Type(c, "good morning ", En));
        corr = Type(c, "יקךךם ", He);
        Assert.NotNull(corr);
        Assert.Equal(En, corr!.TargetLangId);
    }

    [DataFact]
    public void Wrong_alphabet_in_a_three_keyboard_setup_is_still_corrected()
    {
        var c = new SessionController(Three.Value, new[] { Fixtures.English, Fixtures.Hebrew, German });
        var corr = Type(c, "akuo ", De); // Hebrew keys typed on German: no y/z, so it reads as on English
        Assert.NotNull(corr);
        Assert.Equal(He, corr!.TargetLangId);
        Assert.Equal("שלום ", corr.Text);
    }

    [DataFact]
    public void Never_switch_to_blocks_that_target_but_still_corrects_from_it()
    {
        var c = new SessionController(Fixtures.Decider!, new[] { Fixtures.English, Fixtures.Hebrew }, neverSwitchTo: new[] { He });
        Assert.Null(Type(c, "akuo ", En));
        Assert.True(c.CanCorrect); // Hebrew can still be corrected into English
        var corr = Type(c, "יקךךם ", He);
        Assert.NotNull(corr);
        Assert.Equal(En, corr!.TargetLangId);
    }

    [DataFact]
    public void Inactive_language_is_ignored()
    {
        // The host leaves inactive languages out of the controller altogether.
        var c = new SessionController(Three.Value, new[] { Fixtures.English, German });
        Assert.False(c.CanCorrect);
        Assert.Null(Type(c, "akuo ", En));
        Assert.Null(Type(c, "יקךךם ", He)); // typing in an unknown layout is ignored, not an error
    }

    [DataFact]
    public void One_language_is_idle_and_can_be_reconfigured()
    {
        var c = new SessionController(Fixtures.Decider!, new[] { Fixtures.English });
        Assert.False(c.CanCorrect);
        Assert.Null(Type(c, "akuo ", En));
        Assert.Null(c.Force(En));

        c.Configure(Fixtures.Decider!, new[] { Fixtures.English, Fixtures.Hebrew });
        Assert.True(c.CanCorrect);
        var corr = Type(c, "akuo ", En);
        Assert.NotNull(corr);
        Assert.Equal(He, corr!.TargetLangId);
    }

    [DataFact]
    public void Force_with_one_alphabet_converts_within_it()
    {
        // Only Latin keyboards active: nothing automatic, but the hotkey still converts when the user asks.
        var c = new SessionController(Three.Value, new[] { Fixtures.English, German });
        Assert.Null(Type(c, "zes", De));
        var corr = c.Force(De);
        Assert.NotNull(corr);
        Assert.Equal(En, corr!.TargetLangId);
        Assert.Equal("yes", corr.Text);
    }
}
