using AutoLang.Session;
using Xunit;

namespace AutoLang.Engine.Tests;

/// <summary>
/// The hotkey after the moment has passed: undo that survives the separators typed after a correction, and flipping the
/// last word once the run has moved on.
/// </summary>
public class HotkeyRecoveryTests
{
    private static SessionController New() => new(Fixtures.Decider!, new[] { Fixtures.English, Fixtures.Hebrew });
    private static ushort En => Fixtures.English.LangId;
    private static ushort He => Fixtures.Hebrew.LangId;
    private static string HeToEn(string s) => Fixtures.Hebrew.TransliterateTo(Fixtures.English, s);
    private static string EnToHe(string s) => Fixtures.English.TransliterateTo(Fixtures.Hebrew, s);

    private static Correction? Type(SessionController c, string text, ushort lang)
    {
        foreach (var ch in text)
            if (c.OnChar(ch, lang) is { } corr) return corr;
        return null;
    }

    [DataFact]
    public void Accidental_force_then_question_mark_can_still_be_undone()
    {
        // The reported case: a correct Hebrew word was flipped by the hotkey, the user typed "?" and then noticed.
        var c = New();
        Assert.Null(Type(c, "רלוונטי", He));
        var force = c.Force(He)!;
        Assert.Equal("rkuubyh", force.Text);
        Assert.Null(c.OnChar('?', En));
        Assert.True(c.CanUndo);

        var undo = c.Undo()!;
        Assert.Equal(CorrectionKind.Undo, undo.Kind);
        Assert.Equal(8, undo.Backspaces); // the flipped word and the "?" after it
        Assert.Equal("רלוונטי" + EnToHe("?"), undo.Text);
        Assert.Equal(He, undo.TargetLangId);
        Assert.Equal(0, c.ExceptionCount); // undoing a conversion the user asked for teaches the engine nothing
    }

    [DataFact]
    public void Undo_after_an_auto_correction_takes_the_separators_typed_after_it()
    {
        var c = New();
        var corr = Type(c, "akuo ", En)!;
        Assert.Null(Type(c, "? ", He));
        var undo = c.Undo()!;
        Assert.Equal(corr.Text.Length + 2, undo.Backspaces);
        Assert.Equal("akuo " + HeToEn("? "), undo.Text);
        Assert.Contains("akuo", c.Exceptions); // an automatic correction taken back is still learnt
    }

    [DataFact]
    public void Backspace_over_the_separators_keeps_undo_and_into_the_correction_ends_it()
    {
        var c = New();
        Type(c, "akuo ", En);
        Type(c, "??", He);
        c.OnBackspace();
        Assert.True(c.CanUndo);
        Assert.Equal(6, c.Undo()!.Backspaces); // "שלום " and the one "?" left

        var d = New();
        Type(d, "akuo ", En);
        d.OnBackspace(); // erases the space of the correction itself
        Assert.False(d.CanUndo);
    }

    [DataFact]
    public void Hotkey_flips_the_last_word_after_the_run_moved_on()
    {
        var c = New();
        Assert.Null(Type(c, "שלום? ", He)); // a clear Hebrew word: kept, and the run resets
        Assert.Equal(0, c.RunLength);

        var flip = c.Force(He)!;
        Assert.Equal(CorrectionKind.Force, flip.Kind);
        Assert.Equal(6, flip.Backspaces);
        Assert.Equal("akuo" + HeToEn("? "), flip.Text);
        Assert.Equal(En, flip.TargetLangId);

        // The hotkey again takes it back.
        Assert.True(c.CanUndo);
        var undo = c.Undo()!;
        Assert.Equal(flip.Text.Length, undo.Backspaces);
        Assert.Equal("שלום? ", undo.Text);
    }

    [DataFact]
    public void Only_the_last_word_is_flipped()
    {
        var c = New();
        Assert.Null(Type(c, "שלום עולם. ", He));
        var flip = c.Force(He)!;
        Assert.Equal("עולם. ".Length, flip.Backspaces);
        Assert.Equal(HeToEn("עולם. "), flip.Text);
    }

    [DataFact]
    public void The_last_word_is_out_of_reach_after_the_caret_moved()
    {
        var c = New();
        Type(c, "שלום ", He);
        c.Reset(ResetScope.Run); // an arrow key, a click, Ctrl+something
        Assert.Null(c.Force(He));

        Type(c, "שלום", He);
        c.OnEnter(He); // a new line, or a sent message
        Assert.Null(c.Force(He));

        Type(c, "שלום ", He);
        c.Blocked = true;
        c.Blocked = false;
        Assert.Null(c.Force(He));
    }

    [DataFact]
    public void Backspace_is_mirrored_before_flipping()
    {
        var c = New();
        Type(c, "שלום  ", He);
        c.OnBackspace();
        var flip = c.Force(He)!;
        Assert.Equal(5, flip.Backspaces);
        Assert.Equal("akuo" + HeToEn(" "), flip.Text);
    }

    [DataFact]
    public void A_letter_after_a_correction_moves_the_hotkey_to_the_new_word()
    {
        var c = New();
        Type(c, "akuo ", En); // corrected to "שלום "
        Type(c, "גם", He);
        Assert.False(c.CanUndo);
        var force = c.Force(He)!;
        Assert.Equal(2, force.Backspaces); // the new word only, not the correction before it
    }
}
