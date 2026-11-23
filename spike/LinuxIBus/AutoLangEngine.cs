using System.Diagnostics;
using AutoLang.Engine;
using AutoLang.Session;
using Tmds.DBus.Protocol;

namespace AutoLang.Linux;

/// <summary>
/// The typing state of the process: the layouts AutoLang types in, the language in use and the session controller.
/// One per process, shared by every engine object the daemon creates (GNOME uses one global engine).
/// </summary>
internal sealed class Typing
{
    public IReadOnlyList<XkbLayout> Layouts { get; }
    public SessionController Controller { get; }
    public ushort Lang { get; set; }

    public Typing(IReadOnlyList<XkbLayout> layouts)
    {
        Layouts = layouts;
        Lang = layouts[0].Table.LangId;
        Controller = new SessionController(new PhraseDecider([]), []);
    }

    public XkbLayout Layout(ushort lang) => Layouts.FirstOrDefault(l => l.Table.LangId == lang) ?? Layouts[0];

    /// <summary>Loads the word lists in the background, as on Mac; until then every key is typed as is and nothing is corrected.</summary>
    public void LoadInBackground() => ThreadPool.QueueUserWorkItem(_ =>
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var active = new List<KeyboardLayoutTable>();
            var profiles = new List<LanguageProfile>();
            foreach (var layout in Layouts)
            {
                if (Languages.WordListCode(layout.Table.LangId) is not { } code) { Log.Info($"{layout.Table}: no word list, not corrected"); continue; }
                profiles.Add(Languages.BuildProfile(layout.Table, code));
                active.Add(layout.Table);
            }
            Controller.Configure(new PhraseDecider(profiles), active);
            Log.Info($"languages ready in {sw.ElapsedMilliseconds} ms: {string.Join(", ", active)}");
        }
        catch (Exception ex) { Log.Warn($"loading languages failed: {ex}"); }
    });
}

/// <summary>
/// The engine: every printable key is translated on the current language's XKB layout and committed by the engine
/// itself, so no GNOME layout switching is ever needed; the controller sees each character and, at a boundary, may
/// return a correction, which is applied as "delete N before the caret, commit the text". Keys it does not type
/// (shortcuts, navigation, Backspace, Enter) go to the app untouched.
/// </summary>
internal sealed class AutoLangEngine(DBusConnection connection, string path, Typing typing) : EngineObject(connection, path)
{
    // Keysyms
    private const uint XK_BackSpace = 0xff08, XK_Tab = 0xff09, XK_Return = 0xff0d, XK_Escape = 0xff1b, XK_Delete = 0xffff,
        XK_Home = 0xff50, XK_End = 0xff57, XK_KP_Enter = 0xff8d, XK_Shift_L = 0xffe1, XK_Shift_R = 0xffe2,
        XK_F1 = 0xffbe, XK_F35 = 0xffe0, XK_ModifiersFirst = 0xffe3, XK_ModifiersLast = 0xffee, XK_ISO_Level3_Shift = 0xfe03;
    private const uint EvdevBackspace = 14;
    private const uint ShortcutMods = IBus.ControlMask | IBus.Mod1Mask | IBus.Mod4Mask | IBus.SuperMask | IBus.HyperMask | IBus.MetaMask;


    /// <summary>
    /// How corrections reach the screen:
    ///   "surrounding" every key is committed; a correction is DeleteSurroundingText + CommitText.
    ///   "forward"     every key is committed; a correction is BackSpace key events + CommitText.
    ///   "auto"        surrounding when the client says it supports surrounding text, else forward.
    ///   "preedit"     text the controller can still rewrite stays uncommitted (preedit) and corrections edit it; it is
    ///                 committed when a new word starts, before Enter and navigation keys. Nothing is ever deleted
    ///                 unless a correction reaches past it (then auto's choice is used for that part).
    /// </summary>
    private static readonly string DeleteMode = Environment.GetEnvironmentVariable("AUTOLANG_DELETE") ?? "preedit";
    /// <summary>Forward mode only: wait this long after the BackSpaces before committing, so the client handles them first.</summary>
    private static readonly int ForwardDelayMs = int.TryParse(Environment.GetEnvironmentVariable("AUTOLANG_FORWARD_DELAY_MS"), out var ms) ? ms : 0;
    /// <summary>Preedit mode: show the uncommitted text without the usual underline (AUTOLANG_PREEDIT_UNDERLINE=1 keeps it).</summary>
    private static readonly bool NoUnderline = Environment.GetEnvironmentVariable("AUTOLANG_PREEDIT_UNDERLINE") != "1";

    private static bool Preedit => DeleteMode == "preedit";

    private readonly DoubleShift _doubleShift = new();
    private readonly System.Text.StringBuilder _pre = new();
    private uint _caps;
    private bool _noCorrect;
    private string _purpose = "free";

    private SessionController Controller => typing.Controller;

    protected override bool ProcessKeyEvent(uint keyval, uint keycode, uint state)
    {
        bool release = (state & IBus.ReleaseMask) != 0;
        long now = Environment.TickCount64;

        if (keyval is XK_Shift_L or XK_Shift_R)
        {
            if (!release) _doubleShift.Down(now, chord: (state & ShortcutMods) != 0);
            else if (_doubleShift.Up(now)) Hotkey();
            return false;
        }
        if (release) return false;
        _doubleShift.OtherKey();
        if (keyval is >= XK_ModifiersFirst and <= XK_ModifiersLast or XK_ISO_Level3_Shift) return false;

        if ((state & ShortcutMods) != 0 || (state & IBus.Mod5Mask) != 0) { CommitPreedit(); Controller.Reset(ResetScope.Run); return false; }

        switch (keyval)
        {
            case XK_BackSpace:
            {
                var back = _noCorrect ? null : Controller.OnBackspace();
                if (back is { } lang) SetLang(lang, "correction erased");
                if (_pre.Length == 0) return false;
                _pre.Length--; // the erased char is ours: edit the preedit instead of the app's text
                ShowPreedit();
                return true;
            }
            case XK_Return or XK_KP_Enter:
                // The corrected text is committed now; the Enter itself goes on to the app, after the commit.
                if (!_noCorrect && Controller.OnEnter(typing.Lang) is { } enter) Apply(enter, pressEnter: false);
                CommitPreedit();
                return false;
            case XK_Tab or XK_Escape or XK_Delete or (>= XK_Home and <= XK_End) or (>= XK_F1 and <= XK_F35):
                CommitPreedit();
                Controller.Reset(ResetScope.Run);
                return false;
        }

        char? c = keycode == XkbLayout.KeySpace ? ' '
            : typing.Layout(typing.Lang).Translate((ushort)keycode, (state & IBus.ShiftMask) != 0, (state & IBus.LockMask) != 0);
        if (c is null) { CommitPreedit(); Controller.Reset(ResetScope.Run); return false; } // keypad, media keys, a dead key...

        if (_noCorrect) { CommitPreedit(); CommitText(c.Value.ToString()); return true; }

        int runBefore = Controller.RunLength;
        if (Controller.OnChar(c.Value, typing.Lang) is { } correction)
        {
            Apply(correction, pressEnter: correction.PressEnter);
            return true;
        }
        if (!Preedit) { CommitText(c.Value.ToString()); return true; }
        // A new word started: nothing before it can be rewritten any more (undo and "last word" end here too).
        if (runBefore == 0 && Controller.RunLength > 0) CommitPreedit();
        _pre.Append(c.Value);
        ShowPreedit();
        return true;
    }

    /// <summary>Double Shift: undo the correction just made, else convert the run or the last word. With nothing to
    /// convert it switches the language (spike experiment: the Linux build has no other switch key).</summary>
    private void Hotkey()
    {
        if (_noCorrect) return;
        bool convert = !Controller.CanUndo;
        var c = convert ? Controller.Force(typing.Lang) : Controller.Undo();
        if (c is not null) { Apply(c, pressEnter: false); return; }
        var next = typing.Layouts.Select(l => l.Table.LangId).SkipWhile(l => l != typing.Lang).Skip(1)
            .DefaultIfEmpty(typing.Layouts[0].Table.LangId).First();
        SetLang(next, "double shift with nothing to convert");
    }

    private void Apply(Correction c, bool pressEnter)
    {
        var sw = Stopwatch.StartNew();
        string how;
        int erase = c.Backspaces;
        if (Preedit)
        {
            int fromPre = Math.Min(erase, _pre.Length);
            _pre.Length -= fromPre;
            erase -= fromPre;
            if (erase > 0) CommitPreedit(); // nothing is left in it, but keep the client's view consistent
        }
        bool surrounding = DeleteMode == "surrounding" || ((DeleteMode is "auto" or "preedit") && (_caps & IBus.CapSurroundingText) != 0);
        if (!Preedit) how = surrounding ? "surrounding" : "forward";
        else how = erase == 0 ? "preedit" : $"preedit, {erase} beyond via {(surrounding ? "surrounding" : "forward")}";
        if (erase > 0)
        {
            if (surrounding) DeleteSurroundingText(erase);
            else
            {
                for (int i = 0; i < erase; i++)
                {
                    ForwardKeyEvent(XK_BackSpace, EvdevBackspace, 0);
                    ForwardKeyEvent(XK_BackSpace, EvdevBackspace, IBus.ReleaseMask);
                }
                if (ForwardDelayMs > 0) Thread.Sleep(ForwardDelayMs);
            }
        }
        if (Preedit) { _pre.Append(c.Text); ShowPreedit(); }
        else if (c.Text.Length > 0) CommitText(c.Text);
        if (pressEnter)
        {
            CommitPreedit();
            ForwardKeyEvent(XK_Return, 28, 0);
            ForwardKeyEvent(XK_Return, 28, IBus.ReleaseMask);
        }
        Log.Info($"{c.Kind} via {how}: -{c.Backspaces} +{Log.Show(c.Text)} " +
                 $"{Languages.Symbol(c.FromLangId)}→{Languages.Symbol(c.TargetLangId)} margin {c.Margin:0.00} ({c.Reason}) in {sw.Elapsed.TotalMilliseconds:0.0} ms");
        SetLang(c.TargetLangId, null);
    }

    private void ShowPreedit() => UpdatePreeditText(_pre.ToString(), NoUnderline);

    /// <summary>Makes the preedit ordinary text: hide it, then commit the same string.</summary>
    private void CommitPreedit()
    {
        if (_pre.Length == 0) return;
        var text = _pre.ToString();
        _pre.Clear();
        UpdatePreeditText("", NoUnderline);
        CommitText(text);
    }

    /// <summary>The client committed or dropped the preedit itself (focus out, a click): forget ours without committing.</summary>
    private void DropPreedit() => _pre.Clear();

    private void SetLang(ushort lang, string? why)
    {
        if (lang == 0 || lang == typing.Lang || typing.Layouts.All(l => l.Table.LangId != lang)) return;
        typing.Lang = lang;
        if (why is not null) Log.Info($"language → {Languages.Symbol(lang)} ({why})");
        UpdateProperty(ModeProperty());
    }

    /// <summary>GNOME's top-bar indicator shows the symbol of an engine property keyed "InputMode".</summary>
    private IBus.Property ModeProperty()
    {
        var lang = typing.Lang;
        return new IBus.Property("InputMode", $"AutoLang: {Languages.DisplayName(lang)}", Languages.Symbol(lang), "AutoLang");
    }

    protected override void SetCapabilities(uint caps)
    {
        _caps = caps;
        Log.Info($"capabilities 0x{caps:X} (surrounding text: {(caps & IBus.CapSurroundingText) != 0})");
    }

    /// <summary>Password, PIN and terminal fields are typed in the current language but never corrected.</summary>
    protected override void SetContentType(uint purpose, uint hints)
    {
        _noCorrect = purpose is IBus.PurposePassword or IBus.PurposePin or IBus.PurposeTerminal;
        _purpose = purpose switch { IBus.PurposePassword => "password", IBus.PurposePin => "pin", IBus.PurposeTerminal => "terminal", _ => purpose.ToString() };
        Controller.Reset(ResetScope.All);
        Log.Info($"content purpose {_purpose}{(_noCorrect ? ": not corrected" : "")}");
    }

    /// <summary>Tests only: every focus starts in the first layout's language, so cases do not inherit each other's.</summary>
    private static readonly bool FocusResetsLang = Environment.GetEnvironmentVariable("AUTOLANG_FOCUS_RESETS_LANG") == "1";

    protected override void FocusIn()
    {
        Controller.Reset(ResetScope.All);
        if (FocusResetsLang) typing.Lang = typing.Layouts[0].Table.LangId;
        RegisterProperties([ModeProperty()]);
        RequireSurroundingText();
    }

    protected override void FocusOut()
    {
        DropPreedit();
        Controller.Reset(ResetScope.All);
        _noCorrect = false;
    }

    protected override void Reset() { DropPreedit(); Controller.Reset(ResetScope.All); }
    protected override void Enable() => RegisterProperties([ModeProperty()]);
    protected override void Disable() => Controller.Reset(ResetScope.All);
}
