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
        var corr = Type(c, "tbh ", En);
        Assert.NotNull(corr);
        Assert.Equal(9, corr!.Backspaces);
        Assert.Equal("אם כן אני ", corr.Text);
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
