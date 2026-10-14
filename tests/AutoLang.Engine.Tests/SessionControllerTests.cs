using AutoLang.Session;
using Xunit;

namespace AutoLang.Engine.Tests;

/// <summary>The host's state machine against the real engine: what gets rewritten, by how much, and when.</summary>
public class SessionControllerTests
{
    private static SessionController New() => new(Fixtures.Decider!, new[] { Fixtures.English, Fixtures.Hebrew });
    private static ushort En => Fixtures.English.LangId;
    private static ushort He => Fixtures.Hebrew.LangId;

    private static Correction? Type(SessionController c, string text, ushort lang)
    {
        Correction? last = null;
        foreach (var ch in text)
        {
            last = c.OnChar(ch, lang);
            if (last is not null) return last;
        }
        return last;
    }

    [DataFact]
    public void Wrong_layout_word_is_corrected_at_the_space()
    {
        var c = New();
        Assert.Null(Type(c, "akuo", En));
        var corr = c.OnChar(' ', En);
        Assert.NotNull(corr);
        Assert.Equal(CorrectionKind.Auto, corr!.Kind);
        Assert.Equal(4, corr.Backspaces);          // "akuo" is on screen; the space was swallowed
        Assert.Equal("שלום ", corr.Text);           // replacement re-types the boundary too
        Assert.False(corr.PressEnter);
        Assert.Equal(He, corr.TargetLangId);
        Assert.Equal(0, c.RunLength);
        Assert.Equal(He, c.ConfirmedLangId);
        Assert.True(c.CanUndo);
    }

    [DataFact]
    public void English_word_typed_in_Hebrew_is_corrected()
    {
        var c = New();
        var corr = Type(c, "יקךךם ", He);
        Assert.NotNull(corr);
        Assert.Equal("hello ", corr!.Text);
        Assert.Equal(En, corr.TargetLangId);
    }

    [DataFact]
    public void Confident_keep_resets_the_run_and_confirms_the_language()
    {
        var c = New();
        Assert.Null(Type(c, "hello world ", En));
        Assert.Equal(0, c.RunLength);
        Assert.Equal(En, c.ConfirmedLangId);
        // A later wrong-layout word is judged on its own and still corrected: only that word is rewritten.
        var corr = Type(c, "akuo ", En);
        Assert.NotNull(corr);
        Assert.Equal(4, corr!.Backspaces);
    }

    [DataFact]
    public void Deferred_run_is_corrected_as_a_whole()
    {
        var c = New();
        Assert.Null(Type(c, "to fi ", En));
        Assert.Equal(6, c.RunLength);
        var corr = Type(c, "husg ", En);
        Assert.NotNull(corr);
        Assert.Equal(10, corr!.Backspaces);
        Assert.Equal("אם כן יודע ", corr.Text);
    }

    [DataFact]
    public void Undo_restores_the_original_and_blocks_a_repeat()
    {
        var c = New();
        var corr = Type(c, "akuo ", En)!;
        var undo = c.Undo();
        Assert.NotNull(undo);
        Assert.Equal(CorrectionKind.Undo, undo!.Kind);
        Assert.Equal(corr.Text.Length, undo.Backspaces);
        Assert.Equal("akuo ", undo.Text);
        Assert.Equal(En, undo.TargetLangId);
        Assert.False(c.CanUndo);
        Assert.Equal(1, c.ExceptionCount);
        Assert.Null(Type(c, "akuo ", En)); // the undone word is an exception for the rest of the session
    }

    [DataFact]
    public void Any_key_after_a_correction_cancels_undo()
    {
        var c = New();
        Type(c, "akuo ", En);
        c.OnChar('a', He);
        Assert.False(c.CanUndo);
        Assert.Null(c.Undo());
    }

    [DataFact]
    public void Enter_corrects_and_presses_enter_but_cannot_be_undone()
    {
        var c = New();
        Type(c, "akuo", En);
        var corr = c.OnEnter(En);
        Assert.NotNull(corr);
        Assert.Equal(4, corr!.Backspaces);
        Assert.Equal("שלום", corr.Text);
        Assert.True(corr.PressEnter);
        Assert.False(c.CanUndo);
        Assert.Equal(0, c.RunLength);
    }

    [DataFact]
    public void Blocked_buffers_nothing()
    {
        var c = New();
        c.Blocked = true;
        Assert.Null(Type(c, "akuo ", En));
        Assert.Equal(0, c.RunLength);
        c.Blocked = false;
        Assert.NotNull(Type(c, "akuo ", En));
    }

    [DataFact]
    public void Force_converts_the_run_regardless_of_score()
    {
        var c = New();
        Type(c, "aku", En);
        var corr = c.Force(En);
        Assert.NotNull(corr);
        Assert.Equal(CorrectionKind.Force, corr!.Kind);
        Assert.Equal(3, corr.Backspaces);
        Assert.Equal("שלו", corr.Text);
        Assert.Equal(He, corr.TargetLangId);
        Assert.True(c.CanUndo);
    }

    [DataFact]
    public void Backspace_edits_the_run()
    {
        var c = New();
        Type(c, "akuox", En);
        c.OnBackspace();
        var corr = c.OnChar(' ', En);
        Assert.Equal("שלום ", corr?.Text);
    }

    [DataTheory]
    [InlineData("חח ")]
    [InlineData("חחחח ")]
    [InlineData("בסדר חח ")]
    [InlineData("זה היה מצחיק חחח ")]
    public void Hebrew_laughter_is_left_alone(string text)
    {
        var c = New();
        Assert.Null(Type(c, text, He));
    }

    [DataTheory]
    [InlineData("bnk989 ")]
    [InlineData("my user is bnk989 ")]
    public void Letters_glued_to_digits_are_left_alone(string text)
    {
        var c = New();
        Assert.Null(Type(c, text, En));
    }

    /// <summary>Backspace n times. Only the last may ask the host to switch layouts; returns what it asked for.</summary>
    private static ushort? Erase(SessionController c, int n)
    {
        for (int i = 1; i < n; i++) Assert.Null(c.OnBackspace());
        return n > 0 ? c.OnBackspace() : null;
    }

    [DataFact]
    public void Switching_back_by_hand_still_works()
    {
        var c = New();
        var corr = Type(c, "akuo ", En)!;
        Assert.Null(Erase(c, 2));
        c.Reset(ResetScope.Run);                 // Alt+Shift halfway through erasing
        Assert.Equal(En, Erase(c, corr.Text.Length - 2));
        Assert.Null(Type(c, "akuo ", En));
        Assert.Contains("akuo", c.Exceptions);
    }

    [DataFact]
    public void Erasing_a_correction_and_typing_it_again_keeps_it()
    {
        var c = New();
        bool learnt = false;
        c.ExceptionsAdded += () => learnt = true;
        var corr = Type(c, "akuo ", En)!;
        Assert.Equal(En, Erase(c, corr.Text.Length)); // the last of it: the host switches back to English
        Assert.Null(Type(c, "akuo ", En));
        Assert.True(learnt);
        Assert.Contains("akuo", c.Exceptions);
        // And from now on, like an undone word.
        Assert.Null(Type(c, "akuo ", En));
    }

    [DataFact]
    public void Erasing_a_multi_word_correction_and_typing_it_again_keeps_it()
    {
        var c = New();
        Assert.Null(Type(c, "to fi ", En));
        var corr = Type(c, "husg ", En)!;
        Assert.Equal(En, Erase(c, corr.Text.Length));
        Assert.Null(Type(c, "to fi husg ", En));
        Assert.Equal(new[] { "fi", "husg", "to" }, c.Exceptions.Order());
    }

    [DataFact]
    public void Typing_the_same_word_again_without_erasing_is_still_corrected()
    {
        var c = New();
        Assert.NotNull(Type(c, "akuo ", En));
        Assert.NotNull(Type(c, "akuo ", En));
        Assert.Empty(c.Exceptions);
    }

    [DataFact]
    public void Erasing_part_of_a_correction_is_an_edit_not_a_rejection()
    {
        var c = New();
        var corr = Type(c, "akuo ", En)!;
        Assert.Null(Erase(c, corr.Text.Length - 1)); // the user fixes a letter of the replacement: no switch
        Type(c, "ום ", He);
        Assert.NotNull(Type(c, "akuo ", En));   // nothing was learnt
    }

    [DataFact]
    public void Erasing_a_correction_and_typing_something_else_learns_nothing()
    {
        var c = New();
        var corr = Type(c, "akuo ", En)!;
        Assert.Equal(En, Erase(c, corr.Text.Length));
        Assert.Null(Type(c, "hello ", En));
        Assert.NotNull(Type(c, "akuo ", En));
        Assert.Empty(c.Exceptions);
    }

    [DataFact]
    public void Text_typed_after_the_correction_is_erased_first()
    {
        var c = New();
        var corr = Type(c, "akuo ", En)!;
        Type(c, "עו", He);
        Assert.Equal(En, Erase(c, 2 + corr.Text.Length));
        Assert.Null(Type(c, "akuo ", En));
        Assert.Contains("akuo", c.Exceptions);
    }

    [DataFact]
    public void Removed_exception_is_corrected_again()
    {
        var c = New();
        c.AddExceptions(new[] { "akuo", "to" });
        Assert.Null(Type(c, "akuo ", En));
        c.RemoveExceptions(new[] { "AKUO" });
        Assert.Equal(new[] { "to" }, c.Exceptions);
        Assert.NotNull(Type(c, "akuo ", En));
        c.ClearExceptions();
        Assert.Empty(c.Exceptions);
    }

    [DataFact]
    public void Reset_drops_the_run_and_reports_how_much()
    {
        var c = New();
        Type(c, "aku", En);
        Assert.Equal(3, c.Reset(ResetScope.All));
        Assert.Equal(0, c.RunLength);
        Assert.Null(c.ConfirmedLangId);
    }

    [DataFact]
    public void Layout_change_mid_run_starts_a_new_run()
    {
        var c = New();
        Type(c, "aku", En);
        Assert.Null(c.OnChar('ש', He));
        Assert.Equal(1, c.RunLength);
    }

    [DataFact]
    public void Real_Hebrew_sentence_is_never_touched()
    {
        var c = New();
        Assert.Null(Type(c, "שלום אני לא מבין אם זה עובד ", He));
        Assert.Null(c.OnEnter(He));
    }

    [DataFact]
    public void Addresses_are_never_touched()
    {
        var c = New();
        Assert.Null(Type(c, "akuo@aortho ", En));
    }
}
