using AutoLang.Session;
using Xunit;

namespace AutoLang.Engine.Tests;

/// <summary>
/// The hotkey on selected text (<see cref="SessionController.ConvertSelection"/>): which layout the selection was typed
/// in and which it becomes, what is refused, the race guard, and undo.
/// </summary>
public class SelectionTests
{
    private const ushort De = 0x0407;
    private static SessionController New() => new(Fixtures.Decider!, new[] { Fixtures.English, Fixtures.Hebrew });
    private static ushort En => Fixtures.English.LangId;
    private static ushort He => Fixtures.Hebrew.LangId;
    private static string EnToHe(string s) => Fixtures.English.TransliterateTo(Fixtures.Hebrew, s);
    private static string HeToEn(string s) => Fixtures.Hebrew.TransliterateTo(Fixtures.English, s);

    private static Correction? Convert(SessionController c, string text, ushort windowLang) =>
        c.ConvertSelection(text, windowLang, c.InputStamp, out _);

    private static SelectionRefusal Refusal(SessionController c, string text, ushort windowLang = 0)
    {
        Assert.Null(c.ConvertSelection(text, windowLang == 0 ? En : windowLang, c.InputStamp, out var why));
        return why;
    }

    private static Correction? Type(SessionController c, string text, ushort lang)
    {
        foreach (var ch in text)
            if (c.OnChar(ch, lang) is { } corr) return corr;
        return null;
    }

    [DataFact]
    public void Hebrew_typed_on_the_English_layout_becomes_Hebrew()
    {
        var c = New();
        var corr = Convert(c, "akuo", En)!;
        Assert.Equal(CorrectionKind.Selection, corr.Kind);
        Assert.Equal(0, corr.Backspaces); // typing replaces the selection
        Assert.Equal("שלום", corr.Text);
        Assert.Equal(En, corr.FromLangId);
        Assert.Equal(He, corr.TargetLangId);
        Assert.False(corr.PressEnter);
    }

    [DataFact]
    public void English_typed_on_the_Hebrew_layout_becomes_English_whatever_the_window_layout()
    {
        var c = New();
        var typed = EnToHe("hello world");
        Assert.Equal("hello world", Convert(c, typed, He)!.Text);
        Assert.Equal("hello world", Convert(New(), typed, En)!.Text);
    }

    [DataFact]
    public void Punctuation_is_re_read_with_the_letters()
    {
        // Every key of the selection was pressed under the wrong layout, the "," and "?" too.
        var c = New();
        const string typed = "akuo, nv aunl?";
        Assert.Equal(EnToHe(typed), Convert(c, typed, En)!.Text);
    }

    [DataFact]
    public void The_alphabet_most_letters_belong_to_is_the_source_and_other_letters_stay()
    {
        var c = New();
        var corr = Convert(c, "akuo nv שלום", He)!;
        Assert.Equal(En, corr.FromLangId);
        Assert.Equal(EnToHe("akuo nv ") + "שלום", corr.Text);
    }

    [DataFact]
    public void A_tie_goes_to_the_window_layout()
    {
        Assert.Equal(He, Convert(New(), "ab אב", He)!.FromLangId);
        Assert.Equal(En, Convert(New(), "ab אב", En)!.FromLangId);
    }

    [DataFact]
    public void Refused_selections_leave_everything_alone()
    {
        var c = New();
        Assert.Equal(SelectionRefusal.Empty, Refusal(c, ""));
        Assert.Equal(SelectionRefusal.LineBreak, Refusal(c, "akuo\r\nakuo"));
        Assert.Equal(SelectionRefusal.LineBreak, Refusal(c, "akuo\nakuo"));
        Assert.Equal(SelectionRefusal.LineBreak, Refusal(c, "akuo\takuo"));
        Assert.Equal(SelectionRefusal.LineBreak, Refusal(c, "akuo" + (char)0x2029 + "akuo")); // paragraph separator
        Assert.Equal(SelectionRefusal.TooLong, Refusal(c, new string('a', SessionController.MaxSelectionChars + 1)));
        Assert.Equal(SelectionRefusal.NoLetters, Refusal(c, "123 456"));
        Assert.Equal(SelectionRefusal.NoLetters, Refusal(c, "日本語")); // letters no active layout types
        Assert.False(c.CanUndo);

        Assert.NotNull(Convert(New(), new string('a', SessionController.MaxSelectionChars), En));
    }

    [DataFact]
    public void Nothing_happens_in_a_blocked_control()
    {
        var c = New();
        c.Blocked = true;
        Assert.Equal(SelectionRefusal.Blocked, Refusal(c, "akuo"));
    }

    [DataFact]
    public void Anything_after_the_hotkey_makes_it_stale()
    {
        foreach (Action<SessionController> input in new Action<SessionController>[]
                 {
                     c => c.OnChar('x', En),
                     c => c.OnBackspace(),
                     c => c.OnEnter(En),
                     c => c.Reset(ResetScope.Run), // an arrow key or a click
                     c => c.Blocked = true,        // focus moved
                 })
        {
            var c = New();
            long stamp = c.InputStamp;
            input(c);
            c.Blocked = false;
            Assert.Null(c.ConvertSelection("akuo", En, stamp, out var why));
            Assert.Equal(SelectionRefusal.Stale, why);
        }
    }

    [DataFact]
    public void The_hotkey_finds_nothing_tracked_after_a_click_so_the_host_looks_for_a_selection()
    {
        var c = New();
        Type(c, "שלום ", He);
        c.Reset(ResetScope.All); // the mouse click that selected text
        Assert.Null(c.Force(He));
        Assert.False(c.CanUndo);
        // The host reads the stamp after Force and the selection then; nothing came in between.
        Assert.NotNull(c.ConvertSelection("akuo", He, c.InputStamp, out _));
    }

    [DataFact]
    public void Double_shift_again_puts_the_original_back()
    {
        var c = New();
        var corr = Convert(c, "akuo nv", En)!;
        Assert.True(c.CanUndo);
        var undo = c.Undo()!;
        Assert.Equal(CorrectionKind.Undo, undo.Kind);
        Assert.Equal(corr.Text.Length, undo.Backspaces);
        Assert.Equal("akuo nv", undo.Text);
        Assert.Equal(En, undo.TargetLangId);
        Assert.Equal(0, c.ExceptionCount); // a conversion the user asked for teaches the engine nothing
        Assert.False(c.CanUndo);
    }

    [DataFact]
    public void Undo_reaches_all_of_a_long_selection()
    {
        var c = New();
        var text = string.Join(" ", Enumerable.Repeat("akuo nv", 100)); // longer than the screen copy typing keeps
        var corr = Convert(c, text, En)!;
        var undo = c.Undo()!;
        Assert.Equal(corr.Text.Length, undo.Backspaces);
        Assert.Equal(text, undo.Text);
    }

    [DataFact]
    public void Undo_takes_separators_typed_after_and_ends_on_a_new_word()
    {
        var c = New();
        Convert(c, "akuo", En);
        Assert.Null(c.OnChar('?', He));
        var undo = c.Undo()!;
        Assert.Equal(5, undo.Backspaces);
        Assert.Equal("akuo" + HeToEn("?"), undo.Text);

        var d = New();
        Convert(d, "akuo", En);
        d.OnChar(' ', He);
        d.OnChar('א', He);
        Assert.False(d.CanUndo);
    }

    [DataFact]
    public void The_window_continues_in_the_target_layout_and_new_words_are_judged_on_their_own()
    {
        var c = New();
        Convert(c, "akuo", En);
        Assert.Equal(He, c.ConfirmedLangId);
        Assert.Equal(0, c.RunLength);
    }

    // ---- same alphabet only: English and a synthetic German (QWERTZ: Y and Z swapped) ----

    private static readonly Lazy<KeyboardLayoutTable> GermanLazy = new(() =>
    {
        var keys = new List<KeyValuePair<Key, string>>();
        for (uint vk = 0x20; vk <= 0xFE; vk++)
            foreach (var shift in new[] { false, true })
            {
                uint src = vk switch { 0x59 => 0x5A, 0x5A => 0x59, _ => vk };
                if (Fixtures.English.TextFor(new Key(src, shift)) is { } t) keys.Add(new(new Key(vk, shift), t));
            }
        return new KeyboardLayoutTable("German (synthetic)", De, keys);
    });

    private static SessionController Latin(params KeyboardLayoutTable[] extra)
    {
        var en = Path.Combine(Fixtures.DataDir, "words", "en_50k.txt");
        var he = Path.Combine(Fixtures.DataDir, "words", "he_50k.txt");
        var enP = LanguageProfile.FromFrequencyFile(En, "en", en, LanguageProfile.DefaultRulesFor(En), LanguageProfile.DefaultNormalizeFor(En));
        var heP = LanguageProfile.FromFrequencyFile(He, "he", he, LanguageProfile.DefaultRulesFor(He), LanguageProfile.DefaultNormalizeFor(He));
        var deP = new LanguageProfile(De, "de", enP.Dictionary, enP.Model, LanguageProfile.DefaultRulesFor(De), enP.Normalize);
        return new SessionController(new PhraseDecider(new[] { enP, heP, deP }), new[] { Fixtures.English, GermanLazy.Value }.Concat(extra));
    }

    [DataFact]
    public void With_one_alphabet_it_converts_within_it()
    {
        var c = Latin();
        var corr = c.ConvertSelection("zes", De, c.InputStamp, out _)!; // "yes" typed on QWERTZ
        Assert.Equal("yes", corr.Text);
        Assert.Equal(En, corr.TargetLangId);
    }

    [DataFact]
    public void With_three_layouts_the_Latin_source_is_the_window_one_and_the_target_another_alphabet()
    {
        var c = Latin(Fixtures.Hebrew);
        var corr = c.ConvertSelection("akuo", De, c.InputStamp, out _)!;
        Assert.Equal(De, corr.FromLangId);
        Assert.Equal(He, corr.TargetLangId); // across alphabets first, as Force does
        Assert.Equal("שלום", corr.Text);

        // From Hebrew, the Latin target is the one typed in most recently.
        var d = Latin(Fixtures.Hebrew);
        Type(d, "the ", En);
        d.Reset(ResetScope.All);
        var back = d.ConvertSelection(EnToHe("hello"), He, d.InputStamp, out _)!;
        Assert.Equal(En, back.TargetLangId);
        Assert.Equal("hello", back.Text);
    }
}
