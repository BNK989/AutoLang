using Xunit;

namespace AutoLang.Engine.Tests;

public class TransliterationTests
{
    [LayoutFact]
    public void English_keys_read_as_Hebrew()
    {
        Assert.Equal("שלום", Fixtures.English.TransliterateTo(Fixtures.Hebrew, "akuo"));
        Assert.Equal("תודה", Fixtures.English.TransliterateTo(Fixtures.Hebrew, ",usv"));
        Assert.Equal("אם כן", Fixtures.English.TransliterateTo(Fixtures.Hebrew, "to fi"));
    }

    [LayoutFact]
    public void Hebrew_keys_read_as_English()
    {
        Assert.Equal("hello", Fixtures.Hebrew.TransliterateTo(Fixtures.English, "יקךךם"));
        Assert.Equal("to", Fixtures.Hebrew.TransliterateTo(Fixtures.English, "אם"));
    }

    [LayoutFact]
    public void Round_trip_is_identity_for_typeable_text()
    {
        const string he = "שלום, מה שלומך? הכל טוב.";
        var asEnglish = Fixtures.Hebrew.TransliterateTo(Fixtures.English, he);
        Assert.Equal(he, Fixtures.English.TransliterateTo(Fixtures.Hebrew, asEnglish));
    }

    [LayoutFact]
    public void Untypeable_chars_pass_through()
    {
        Assert.Equal("ש—ל", Fixtures.English.TransliterateTo(Fixtures.Hebrew, "a—k"));
    }

    [LayoutFact]
    public void Json_round_trip()
    {
        var copy = KeyboardLayoutTable.FromJson(Fixtures.Hebrew.ToJson());
        Assert.Equal(Fixtures.Hebrew.LangId, copy.LangId);
        Assert.Equal("שלום", Fixtures.English.TransliterateTo(copy, "akuo"));
    }
}

public class TokenizerTests
{
    [LayoutFact]
    public void Cross_layout_punctuation_stays_inside_words()
    {
        var tokens = Tokenizer.Split("n,h tjr, akuo.", Fixtures.English, Fixtures.Hebrew);
        Assert.Equal(new[] { "n,h", "tjr,", "akuo." }, tokens.Select(t => t.Text));
    }

    [LayoutFact]
    public void Digits_and_spaces_separate()
    {
        var tokens = Tokenizer.Split("abc 123 def", Fixtures.English, Fixtures.Hebrew);
        Assert.Equal(new[] { "abc", "def" }, tokens.Select(t => t.Text));
        Assert.Equal(8, tokens[1].Start);
    }
}

public class HebrewRulesTests
{
    [Theory]
    [InlineData("יקךךם", true)]   // hello typed in Hebrew: ך mid-word
    [InlineData("םי", true)]      // oh
    [InlineData("שלום", false)]
    [InlineData("שלומך", false)]
    [InlineData("ם", false)]      // single final letter is a legal (if odd) word
    [InlineData("ך'", false)]     // final letter before trailing geresh
    public void Final_forms_only_at_word_end(string word, bool impossible)
        => Assert.Equal(impossible, HebrewRules.Instance.IsImpossible(word));
}
