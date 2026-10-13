using Xunit;

namespace AutoLang.Engine.Tests;

/// <summary>End-to-end over the real layouts but toy word lists: proves the plumbing, not the accuracy.</summary>
public class PhraseDeciderSmokeTests
{
    private static PhraseDecider Build()
    {
        var he = WordDictionary.FromFrequencyLines(new[] { "שלום 100", "תודה 80", "מה 70", "נשמע 40", "אם 60", "כן 50", "לא 90", "אני 85" });
        var en = WordDictionary.FromFrequencyLines(new[] { "hello 100", "world 80", "thanks 70", "to 95", "be 90", "the 99", "do 60", "go 55" });
        LanguageProfile P(KeyboardLayoutTable l, WordDictionary d)
        {
            var lm = new CharNGramModel(3);
            lm.AddAll(d.Entries);
            return new LanguageProfile(l.LangId, l.Name, d, lm, LanguageProfile.DefaultRulesFor(l.LangId), LanguageProfile.DefaultNormalizeFor(l.LangId));
        }
        return new PhraseDecider(new[] { P(Fixtures.Hebrew, he), P(Fixtures.English, en) });
    }

    [LayoutFact]
    public void Corrects_Hebrew_typed_under_English()
    {
        var d = Build().Decide("akuo nv bang", Fixtures.English, Fixtures.Hebrew);
        Assert.Equal(DecisionKind.Correct, d.Kind);
        Assert.Equal("שלום מה נשמע", d.Replacement);
        Assert.Same(Fixtures.Hebrew, d.TargetLayout);
    }

    [LayoutFact]
    public void Corrects_English_typed_under_Hebrew()
    {
        var d = Build().Decide("יקךךם 'םרךג", Fixtures.Hebrew, Fixtures.English);
        Assert.Equal(DecisionKind.Correct, d.Kind);
        Assert.Equal("hello world", d.Replacement);
    }

    [LayoutFact]
    public void Keeps_text_in_its_own_layout()
    {
        var p = Build();
        Assert.Equal(DecisionKind.Keep, p.Decide("hello world", Fixtures.English, Fixtures.Hebrew).Kind);
        Assert.Equal(DecisionKind.Keep, p.Decide("שלום מה נשמע", Fixtures.Hebrew, Fixtures.English).Kind);
    }

    [LayoutFact]
    public void Lone_collision_word_is_not_corrected()
    {
        var p = Build();
        Assert.NotEqual(DecisionKind.Correct, p.Decide("to", Fixtures.English, Fixtures.Hebrew).Kind);
        Assert.NotEqual(DecisionKind.Correct, p.Decide("אם", Fixtures.Hebrew, Fixtures.English).Kind);
    }

    [LayoutFact]
    public void Context_prior_shifts_the_margin()
    {
        var p = Build();
        var neutral = p.Decide("to", Fixtures.English, Fixtures.Hebrew);
        var afterHebrew = p.Decide("to", Fixtures.English, Fixtures.Hebrew, new DecisionContext(Fixtures.Hebrew.LangId));
        Assert.True(afterHebrew.Margin > neutral.Margin);
    }
}
