using Xunit;

namespace AutoLang.Engine.Tests;

public class CharNGramModelTests
{
    private static CharNGramModel Tiny()
    {
        var m = new CharNGramModel(3);
        m.AddAll(new[] { ("hello", 10.0), ("help", 5.0), ("world", 8.0), ("word", 4.0) });
        return m;
    }

    [Fact]
    public void Seen_words_beat_unseen_gibberish()
    {
        var m = Tiny();
        Assert.True(m.LogProb("hello") > m.LogProb("xqzvk"));
    }

    [Fact]
    public void Probabilities_are_proper()
    {
        var m = Tiny();
        Assert.True(m.LogProb("hello") < 0);
        Assert.True(m.LogProb("hello") > double.NegativeInfinity);
        Assert.Equal(0, m.LogProb(""));
    }

    [Fact]
    public void Unseen_but_plausible_scores_between()
    {
        var m = Tiny();
        double seen = m.LogProbPerChar("hello"), near = m.LogProbPerChar("held"), far = m.LogProbPerChar("qqqq");
        Assert.True(seen > near);
        Assert.True(near > far);
    }
}
