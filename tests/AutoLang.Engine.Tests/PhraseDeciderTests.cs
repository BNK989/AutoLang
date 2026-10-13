using Xunit;

namespace AutoLang.Engine.Tests;

/// <summary>
/// The spec from PLAN.md §1, as tests. These need the real word lists in /data/words and skip otherwise.
/// They pin the *behaviour* we want, not the thresholds — the eval harness owns those.
/// </summary>
public class PhraseDeciderTests
{
    private static Decision EnglishLayout(string typed) => Fixtures.Decider!.Decide(typed, Fixtures.English, Fixtures.Hebrew);
    private static Decision HebrewLayout(string typed) => Fixtures.Decider!.Decide(typed, Fixtures.Hebrew, Fixtures.English);

    // --- unambiguous: correct immediately ---

    [DataFact]
    public void Shalom_typed_in_English_is_corrected()
    {
        var d = EnglishLayout("akuo");
        Assert.Equal(DecisionKind.Correct, d.Kind);
        Assert.Equal("שלום", d.Replacement);
    }

    [DataFact]
    public void Hello_typed_in_Hebrew_is_corrected()
    {
        var d = HebrewLayout("יקךךם");
        Assert.Equal(DecisionKind.Correct, d.Kind);
        Assert.Equal("hello", d.Replacement);
    }

    [DataFact]
    public void Toda_with_comma_key_is_corrected()
    {
        var d = EnglishLayout(",usv");
        Assert.Equal(DecisionKind.Correct, d.Kind);
        Assert.Equal("תודה", d.Replacement);
    }

    // --- real words in the right layout: keep ---

    [DataFact]
    public void Real_English_is_kept()
    {
        Assert.Equal(DecisionKind.Keep, EnglishLayout("hello world").Kind);
        Assert.Equal(DecisionKind.Keep, EnglishLayout("thanks, see you tomorrow").Kind);
    }

    [DataFact]
    public void Real_Hebrew_is_kept()
    {
        Assert.Equal(DecisionKind.Keep, HebrewLayout("שלום אני לא מבין אם זה עובד").Kind);
        Assert.Equal(DecisionKind.Keep, HebrewLayout("תודה רבה").Kind);
    }

    // --- collision pairs: decide at phrase level ---

    [DataFact]
    public void Lone_collision_word_is_deferred_not_corrected()
    {
        foreach (var w in new[] { "to", "do", "go", "so", "re" })
            Assert.NotEqual(DecisionKind.Correct, EnglishLayout(w).Kind);
        foreach (var w in new[] { "אם", "גם", "עם", "דם", "רק" })
            Assert.NotEqual(DecisionKind.Correct, HebrewLayout(w).Kind);
    }

    [DataFact]
    public void Phrase_resolves_the_collision()
    {
        Assert.Equal(DecisionKind.Keep, EnglishLayout("to be").Kind);
        // "to fi" is two English dictionary words (sci-fi) as well as "אם כן": still ambiguous → must not Keep,
        // and must not Correct the wrong way; the third word decides.
        var two = EnglishLayout("to fi");
        Assert.NotEqual(DecisionKind.Keep, two.Kind);
        if (two.Kind == DecisionKind.Correct) Assert.Equal("אם כן", two.Replacement);
        var three = EnglishLayout("to fi tbh");
        Assert.Equal(DecisionKind.Correct, three.Kind);
        Assert.Equal("אם כן אני", three.Replacement);
    }

    [DataFact]
    public void Layout_neutral_tokens_do_not_tilt_the_decision()
    {
        // Digits and shifted Latin capitals read the same under both layouts; they must not push real Hebrew to English.
        Assert.Equal(DecisionKind.Keep, HebrewLayout("איך אומרים XXX").Kind);
        Assert.Equal(DecisionKind.Keep, HebrewLayout("שלום 2024").Kind);
        Assert.Equal(DecisionKind.Keep, EnglishLayout("hello 2024").Kind);
    }

    // --- never touch ---

    [DataFact]
    public void Addresses_are_never_touched()
    {
        Assert.Equal(DecisionKind.Keep, EnglishLayout("ben@aortho").Kind);
        Assert.Equal(DecisionKind.Keep, EnglishLayout("https://akuo").Kind);
    }

    [DataFact]
    public void Gibberish_on_both_sides_is_kept()
    {
        Assert.Equal(DecisionKind.Keep, EnglishLayout("qzxv jjq").Kind);
    }
}
