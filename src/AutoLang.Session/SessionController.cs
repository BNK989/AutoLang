using System.Text;
using AutoLang.Engine;

namespace AutoLang.Session;

/// <summary>Auto: the engine's own. Force: the hotkey on a run or the last word. Undo: a correction taken back.
/// Selection: the hotkey on text the user selected (no backspaces: the typed text replaces the selection).</summary>
public enum CorrectionKind { Auto, Force, Undo, Selection }

/// <summary>
/// What the host must do to the focused control, in this order: delete <see cref="Backspaces"/> characters,
/// type <see cref="Text"/>, press Enter if <see cref="PressEnter"/>, and switch the window's input language to
/// <see cref="TargetLangId"/>. Everything the controller knows about the screen is encoded here; the host never
/// reads the control's text.
/// </summary>
public sealed record Correction(CorrectionKind Kind, int Backspaces, string Text, bool PressEnter,
    ushort FromLangId, ushort TargetLangId, double Margin, string Reason);

/// <summary>Why a finished run was left as typed. Codes only, so a host can log "it didn't switch" without logging text.</summary>
public enum SkipReason
{
    /// <summary>Judged and kept: the other reading did not beat the typed one by the margin a correction needs (it may
    /// not beat it at all), and the typed one did not win clearly enough to confirm its language.</summary>
    BelowThreshold,
    /// <summary>The typed reading clearly wins: the typed language is confirmed. The common case.</summary>
    Clear,
    /// <summary>Too few letters and too close to call: kept in the run and judged again with the next word.</summary>
    Short,
    /// <summary>Every word is on the user's exception list (undone, or erased and retyped earlier).</summary>
    Learnt,
    /// <summary>The user erased our correction and is typing the same keys again; once complete they are learnt.</summary>
    Retyping,
    Retyped,
    /// <summary>No layout of another alphabet to correct into (one script installed, or the rest excluded).</summary>
    NoTarget,
    /// <summary>Early exits of the decider: an e-mail or web address, nothing scorable, no word list loaded.</summary>
    Address, NoWords, NoProfile,
    /// <summary>The layout changed in the middle of the run, so it was dropped unjudged.</summary>
    LayoutChanged,
    /// <summary>Typed under a layout the controller was not given (no word list, or not an active language).</summary>
    Unmanaged,
}

/// <summary>
/// A run that reached a word boundary and was not corrected. Counts and scores only, never text: <see cref="Chars"/> is
/// the run's length on screen, <see cref="Words"/> / <see cref="Letters"/> what the decider scored of it, and
/// <see cref="TypedKnown"/> / <see cref="OtherKnown"/> how many of those words each language's dictionary knows.
/// <see cref="Margin"/> is the best target's margin and <see cref="Threshold"/> the margin a correction needed.
/// Those are zero where no decision was made, and <see cref="TargetLangId"/> too where no target was judged.
/// </summary>
public sealed record SkippedRun(SkipReason Reason, ushort FromLangId, ushort TargetLangId, int Chars,
    int Words = 0, int Letters = 0, int TypedKnown = 0, int OtherKnown = 0, double Margin = 0, double Threshold = 0);

/// <summary>Why the hotkey left a selection alone. Codes only: the host logs them, never the text.</summary>
public enum SelectionRefusal
{
    None,
    /// <summary>The selection is empty.</summary>
    Empty,
    /// <summary>Longer than <see cref="SessionController.MaxSelectionChars"/>.</summary>
    TooLong,
    /// <summary>It spans lines, or holds a tab or another control character: retyping those can send a message or move focus.</summary>
    LineBreak,
    /// <summary>No letter any active layout types.</summary>
    NoLetters,
    /// <summary>No layout to convert into.</summary>
    NoTarget,
    /// <summary>Converting changes nothing.</summary>
    Unchanged,
    /// <summary>Password field, excluded or paused app.</summary>
    Blocked,
    /// <summary>A key, click or focus change arrived after the hotkey: the selection may be gone.</summary>
    Stale,
}

/// <summary>Run = the text buffer; All = the buffer and the language context (new field, new window, mouse click).</summary>
public enum ResetScope { Run, All }

public sealed record SessionOptions
{
    /// <summary>Longest run kept in memory; older words are dropped from the front (they stay on screen untouched).</summary>
    public int MaxRunChars { get; init; } = 200;
    public int TrimToChars { get; init; } = 120;
    /// <summary>
    /// A Keep whose margin is at or below −this confirms the typed language: the run resets and the next words are
    /// judged on their own, with the context prior against switching. Without this a long correct sentence would
    /// swamp one wrong-layout word at its end. Null = the decider's THigh.
    /// </summary>
    public double? ConfirmMargin { get; init; }
    /// <summary>
    /// Within one alphabet group (e.g. English and German both type Latin), the member the user typed in most recently
    /// wins unless another member's margin beats it by more than this. Their readings of the same keys are nearly
    /// identical, so the score difference is mostly noise; switching to the wrong one would change y/z and umlauts.
    /// </summary>
    public double RecencyPreference { get; init; } = 4;
}

/// <summary>
/// The session tracker of PLAN.md §3.2 as a pure state machine. Layouts are grouped by <see cref="KeyboardLayoutTable.Script"/>:
/// a run is only ever corrected into a layout of a <em>different</em> alphabet (PLAN.md §2), never between two layouts
/// that type the same script. Input: characters as they appeared on screen (already
/// resolved by the host against the window's layout), Backspace, Enter, resets, and the two hotkeys. Output: a
/// <see cref="Correction"/> when the host must rewrite the run, otherwise null. Holds only the current run, in memory.
/// All public members are thread-safe; the host calls them from its hook thread and its focus worker.
/// </summary>
public sealed class SessionController
{
    private readonly object _sync = new();
    private PhraseDecider _decider;
    private readonly Dictionary<ushort, KeyboardLayoutTable> _byLang = new();
    /// <summary>Per typed language: every other layout (for tokenizing: a key is a letter if any layout says so).</summary>
    private readonly Dictionary<ushort, KeyboardLayoutTable[]> _others = new();
    /// <summary>Per typed language: layouts a run may be corrected into (other script, not excluded by the user).</summary>
    private readonly Dictionary<ushort, KeyboardLayoutTable[]> _targets = new();
    /// <summary>Per typed language: same-script layouts the manual Force hotkey falls back to when there is no other script.</summary>
    private readonly Dictionary<ushort, KeyboardLayoutTable[]> _sameScript = new();
    /// <summary>Per script: the language the user last typed in or was switched to. Language ids only, never text.</summary>
    private readonly Dictionary<string, ushort> _recentInScript = new(StringComparer.Ordinal);
    private readonly SessionOptions _opt;
    private double _confirm;

    private readonly StringBuilder _run = new();
    private ushort _runLang;
    private ushort? _confirmedLang;
    private Undoable? _undo;
    /// <summary>
    /// Separators typed after <see cref="_undo"/>'s correction ("?", a space): undo outlives them and takes them along. A
    /// letter starts a new word and ends undo; from then on the hotkey flips that word instead.
    /// </summary>
    private int _undoTail;
    /// <summary>
    /// What is on screen just before the caret, as far back as we can vouch for it: every char that appeared since the
    /// caret last jumped (any reset, Enter, a layout we do not handle), each with the layout it was typed in, and our own
    /// corrections applied. The run is always its end. It lets the hotkey flip the last word after the run has moved on.
    /// In memory only, like the run, and never logged.
    /// </summary>
    private readonly StringBuilder _screen = new();
    private readonly List<ushort> _screenLang = new();
    /// <summary>The last correction while the user may still erase it: chars typed after it, and how much of it was erased.</summary>
    private Undoable? _fix;
    private int _afterFix, _fixDeleted;
    /// <summary>What the user had typed before they erased our whole correction; typing it again means "leave this alone".</summary>
    private (string Original, ushort Lang)? _rejected;
    /// <summary>Set when a retype added exceptions; the public entry point raises <see cref="ExceptionsAdded"/> outside the lock.</summary>
    private bool _learnt;
    /// <summary>Set when a boundary left the run uncorrected; the public entry point raises <see cref="Skipped"/> outside the lock.</summary>
    private SkippedRun? _skipped;
    /// <summary>Letters typed in a row under a layout the controller does not manage. A count, not the letters.</summary>
    private int _unmanaged;
    private readonly HashSet<string> _exceptions = new(StringComparer.Ordinal);
    private volatile bool _blocked;
    /// <summary>Bumped by every input and state change; see <see cref="InputStamp"/>.</summary>
    private long _stamp;

    /// <summary>Longest selection the hotkey converts.</summary>
    public const int MaxSelectionChars = 1000;

    /// <param name="OnScreen">What our correction put on screen (replacement incl. the boundary char).</param>
    /// <param name="Original">What the user had typed (incl. the boundary char).</param>
    /// <param name="Kind">Auto, or Force for one the user asked for (undoing that teaches the engine nothing).</param>
    private sealed record Undoable(string OnScreen, string Original, ushort OriginalLang, ushort TargetLang, CorrectionKind Kind);

    /// <param name="layouts">The active layouts, one per language id (later duplicates are ignored). Fewer than two
    /// scripts is allowed: the controller then never corrects, which is the right behaviour while word lists download.</param>
    /// <param name="neverSwitchTo">Languages the user may type in (and be corrected <em>from</em>) but that AutoLang must
    /// never correct <em>into</em>.</param>
    public SessionController(PhraseDecider decider, IEnumerable<KeyboardLayoutTable> layouts, SessionOptions? options = null,
        IEnumerable<ushort>? neverSwitchTo = null)
    {
        _decider = decider;
        _opt = options ?? new SessionOptions();
        Configure(decider, layouts, neverSwitchTo);
    }

    /// <summary>
    /// Replace the language set at runtime (settings changed, a word list finished downloading). Drops the current run;
    /// keeps the user's exceptions and which language of each script they used last.
    /// </summary>
    public void Configure(PhraseDecider decider, IEnumerable<KeyboardLayoutTable> layouts, IEnumerable<ushort>? neverSwitchTo = null)
    {
        lock (_sync)
        {
            _stamp++;
            _decider = decider;
            _confirm = _opt.ConfirmMargin ?? decider.Options.THigh;
            _byLang.Clear(); _others.Clear(); _targets.Clear(); _sameScript.Clear();
            foreach (var l in layouts) _byLang.TryAdd(l.LangId, l);
            var never = new HashSet<ushort>(neverSwitchTo ?? Array.Empty<ushort>());
            foreach (var l in _byLang.Values)
            {
                var others = _byLang.Values.Where(o => o.LangId != l.LangId).ToArray();
                _others[l.LangId] = others;
                _targets[l.LangId] = others.Where(o => o.Script != l.Script && !never.Contains(o.LangId)).ToArray();
                _sameScript[l.LangId] = others.Where(o => o.Script == l.Script && !never.Contains(o.LangId)).ToArray();
            }
            _run.Clear();
            ScreenClear();
            _undo = null;
            _fix = null;
            _rejected = null;
            if (_confirmedLang is { } c && !_byLang.ContainsKey(c)) _confirmedLang = null;
        }
    }

    public IReadOnlyCollection<KeyboardLayoutTable> Layouts { get { lock (_sync) return _byLang.Values.ToArray(); } }

    /// <summary>True if at least one active language has somewhere to be corrected into; false means the controller is idle.</summary>
    public bool CanCorrect { get { lock (_sync) return _targets.Values.Any(t => t.Length > 0); } }

    /// <summary>
    /// True while nothing may be buffered: password field, excluded app, paused. Setting it drops the current run.
    /// The host owns the policy; the controller just obeys.
    /// </summary>
    public bool Blocked
    {
        get => _blocked;
        set { lock (_sync) { _blocked = value; if (value) { _stamp++; _run.Clear(); ScreenClear(); _undo = null; _fix = null; _rejected = null; _unmanaged = 0; } } }
    }

    public int RunLength { get { lock (_sync) return _run.Length; } }

    /// <summary>
    /// Changes with every key, click, focus change and correction the controller sees. The host reads it when the hotkey
    /// fires and hands it back to <see cref="ConvertSelection"/>, which refuses if anything happened in between.
    /// </summary>
    public long InputStamp { get { lock (_sync) return _stamp; } }
    public bool CanUndo { get { lock (_sync) return _undo is not null; } }
    public int ExceptionCount { get { lock (_sync) return _exceptions.Count; } }

    /// <summary>Words that must never be auto-corrected (lower-cased). The host persists these as the user's own exception list.</summary>
    public IReadOnlyCollection<string> Exceptions { get { lock (_sync) return _exceptions.ToArray(); } }

    public void AddExceptions(IEnumerable<string> words)
    {
        lock (_sync) foreach (var w in words) if (!string.IsNullOrWhiteSpace(w)) _exceptions.Add(Norm(w.Trim()));
    }

    /// <summary>Forget exceptions the user removed from the list, so those words are corrected again.</summary>
    public void RemoveExceptions(IEnumerable<string> words)
    {
        lock (_sync) foreach (var w in words) if (!string.IsNullOrWhiteSpace(w)) _exceptions.Remove(Norm(w.Trim()));
    }

    public void ClearExceptions()
    {
        lock (_sync) _exceptions.Clear();
    }

    /// <summary>
    /// Raised (on the caller's thread, outside the lock) when the user rejected a correction by erasing it and typing the
    /// same keys again: those words joined <see cref="Exceptions"/>. Undo needs no event, the host sees its correction.
    /// </summary>
    public event Action? ExceptionsAdded;

    /// <summary>
    /// Raised (on the caller's thread, outside the lock) when a word boundary left the run as typed, with why. For
    /// diagnostics only: it carries counts and scores, never text. Not raised while <see cref="Blocked"/>.
    /// </summary>
    public event Action<SkippedRun>? Skipped;

    public ushort? ConfirmedLangId { get { lock (_sync) return _confirmedLang; } }

    /// <summary>
    /// A character appeared on screen under layout <paramref name="langId"/>. Word characters are buffered; any
    /// other character is a boundary that triggers the decision on the run so far. When a correction is returned the
    /// host must swallow the key that produced <paramref name="c"/>: the correction's text already contains it,
    /// re-read under the target layout.
    /// </summary>
    public Correction? OnChar(char c, ushort langId)
    {
        Correction? result;
        bool learnt;
        SkippedRun? skipped;
        lock (_sync)
        {
            _stamp++;
            result = OnCharLocked(c, langId);
            learnt = TakeLearnt();
            skipped = TakeSkipped();
        }
        if (learnt) ExceptionsAdded?.Invoke();
        if (skipped is not null) Skipped?.Invoke(skipped);
        return result;
    }

    private Correction? OnCharLocked(char c, ushort langId)
    {
        if (_blocked) { _run.Clear(); ScreenClear(); _undo = null; _fix = null; _unmanaged = 0; return null; }
        if (!_byLang.TryGetValue(langId, out var table))
        {
            // We cannot read these keys under another layout, so nothing before the caret can be flipped any more.
            _run.Clear(); ScreenClear(); _undo = null; _fix = null;
            // Count the letters only, to say "typed in a layout AutoLang does not handle" once per word.
            if (char.IsLetterOrDigit(c)) _unmanaged++;
            else if (_unmanaged > 0) { _skipped = new SkippedRun(SkipReason.Unmanaged, langId, 0, _unmanaged); _unmanaged = 0; }
            return null;
        }
        _unmanaged = 0;
        // Typing after erasing part of the correction edits it (the user keeps it); otherwise the char sits after it.
        if (_fix is not null && (_fixDeleted > 0 || ++_afterFix > _opt.MaxRunChars)) _fix = null;
        if (_run.Length > 0 && langId != _runLang)
        {
            // The layout changed under us: the screen is no longer one run.
            _skipped = new SkippedRun(SkipReason.LayoutChanged, _runLang, langId, _run.Length);
            _run.Clear();
        }
        _runLang = langId;
        _recentInScript[table.Script] = langId;

        bool word = Tokenizer.IsWordChar(c, table, _others[langId]);
        // Undo survives the separators typed after a correction (they go along with it), not the start of a new word.
        if (_undo is not null && (word || ++_undoTail > _opt.MaxRunChars)) _undo = null;
        if (word) { _run.Append(c); Trim(); ScreenAppend(c.ToString(), langId); return null; }
        // A separator with nothing before it: nothing to judge, but it is on screen, after the last word.
        if (_run.Length == 0) { ScreenAppend(c.ToString(), langId); return null; }

        var (corr, resetRun) = DecideAt(c.ToString(), enter: false);
        if (corr is not null) return corr; // DecideAt put the replacement, boundary included, on the screen copy
        ScreenAppend(c.ToString(), langId);
        if (resetRun) _run.Clear();
        else { _run.Append(c); Trim(); }
        return null;
    }

    /// <summary>Enter ends the run either way; if it corrects, the host presses Enter after the replacement. Not undoable.</summary>
    public Correction? OnEnter(ushort langId)
    {
        Correction? corr = null;
        bool learnt;
        SkippedRun? skipped;
        lock (_sync)
        {
            _stamp++;
            _undo = null;
            if (!_blocked && _run.Length > 0 && langId != _runLang)
                _skipped = new SkippedRun(SkipReason.LayoutChanged, _runLang, langId, _run.Length);
            else if (!_blocked && _run.Length > 0)
                (corr, _) = DecideAt("", enter: true);
            _run.Clear();
            // A new line, or a sent message: the last word is out of reach of Backspace.
            ScreenClear();
            _undo = null;
            _fix = null;
            _rejected = null;
            learnt = TakeLearnt();
            skipped = TakeSkipped();
        }
        if (learnt) ExceptionsAdded?.Invoke();
        if (skipped is not null) Skipped?.Invoke(skipped);
        return corr;
    }

    /// <summary>
    /// Returns the language the host must switch the window back to when this Backspace erased the last of our
    /// correction: the user is taking it back, and will most likely type their own text again in their own layout.
    /// </summary>
    public ushort? OnBackspace()
    {
        lock (_sync)
        {
            _stamp++;
            // Erasing a separator typed after a correction keeps it undoable; erasing into the correction does not.
            if (_undo is not null && _undoTail-- == 0) _undo = null;
            if (_run.Length > 0) _run.Length--;
            ScreenErase(1);
            if (_fix is not { } f) return null;
            if (_afterFix > 0) { _afterFix--; return null; }
            if (++_fixDeleted < f.OnScreen.Length) return null;
            // All of our correction is gone. If the user now types what they had typed before, they meant it.
            _rejected = (f.Original, f.OriginalLang);
            _fix = null;
            return f.OriginalLang;
        }
    }

    /// <summary>Returns how many buffered characters were dropped, so the host can count (never log) lost runs.</summary>
    public int Reset(ResetScope scope)
    {
        lock (_sync)
        {
            _stamp++;
            int dropped = _run.Length;
            _run.Clear();
            ScreenClear(); // the caret may have moved, or the text under it changed: we can no longer vouch for it
            _undo = null;
            // The correction being watched survives a Run reset: switching the layout back (Alt+Shift) is one, and so is
            // Ctrl+Backspace. After an arrow key the count of erased chars is only approximate, but nothing is learnt
            // until the user types the very keys we corrected again, in the layout we corrected them from.
            if (scope == ResetScope.All) { _confirmedLang = null; _fix = null; _rejected = null; }
            return dropped;
        }
    }

    /// <summary>Revert the last correction: the host deletes what we typed and retypes the original. The original words become exceptions for this session.</summary>
    public Correction? Undo()
    {
        lock (_sync)
        {
            if (_undo is not { } u) return null;
            _stamp++;
            _undo = null;
            _fix = null;
            if (!_byLang.TryGetValue(u.OriginalLang, out var table)) return null;
            // An automatic correction the user took back is a word the engine must leave alone from now on. A conversion
            // they asked for and then took back says nothing about the engine.
            if (u.Kind == CorrectionKind.Auto)
                foreach (var t in Tokenizer.Split(u.Original, table, _others[u.OriginalLang])) _exceptions.Add(Norm(t.Text));
            // Separators typed after the correction go along with it, re-read under the layout it returns to.
            var restored = u.Original + ReRead(_screen.Length - _undoTail, u.OriginalLang);
            int erase = u.OnScreen.Length + _undoTail;
            ScreenReplace(erase, restored, u.OriginalLang);
            // The restored text is not tracked any more: it must never be corrected again, and the user's next words are judged alone.
            _run.Clear();
            _runLang = u.OriginalLang;
            _confirmedLang = u.OriginalLang;
            return new Correction(CorrectionKind.Undo, erase, restored, false, u.TargetLang, u.OriginalLang, 0, "undo");
        }
    }

    /// <summary>
    /// Convert the current run regardless of score (the manual fallback); with no run, the last word before the caret and
    /// the separators after it (<see cref="FlipLastWord"/>). Undoable.
    /// </summary>
    public Correction? Force(ushort langId)
    {
        lock (_sync)
        {
            _undo = null;
            if (_blocked) return null;
            if (_run.Length == 0) return FlipLastWord() is { } flip ? Bump(flip) : null;
            _stamp++;
            if (langId != _runLang || !_byLang.TryGetValue(langId, out var typedTable)) { _run.Clear(); ScreenClear(); return null; }
            var onScreen = _run.ToString();
            // The manual hotkey converts across alphabets like the automatic path; only a user whose layouts all share
            // one alphabet (English + German) gets a same-script conversion, because they asked for it explicitly.
            var candidates = _targets[langId].Length > 0 ? _targets[langId] : _sameScript[langId];
            var picked = Pick(candidates.Select(o => (o, _decider.Decide(onScreen, typedTable, o))), byRank: false);
            if (picked is not { } p) return null;
            var (target, best) = p;
            double bestMargin = best.Margin;
            var replacement = typedTable.TransliterateTo(target, onScreen);
            _undo = new Undoable(replacement, onScreen, langId, target.LangId, CorrectionKind.Force);
            _undoTail = 0;
            _fix = null; // asked for explicitly: erasing it is not a rejection of ours
            ScreenReplace(onScreen.Length, replacement, target.LangId);
            _run.Clear();
            _confirmedLang = target.LangId;
            _recentInScript[target.Script] = target.LangId;
            return new Correction(CorrectionKind.Force, onScreen.Length, replacement, false, langId, target.LangId, bestMargin, "force");
        }
    }

    private Correction Bump(Correction c) { _stamp++; return c; }

    /// <summary>
    /// The hotkey with nothing tracked and text selected in the focused control: re-read the selection under the other
    /// layout. The host read <paramref name="text"/> from the control after the hotkey (never from the clipboard), and
    /// <paramref name="stamp"/> is <see cref="InputStamp"/> as it was then; anything since makes this a no-op. The source
    /// layout is one of the alphabet most of the letters belong to (the window's, else the one used last); the target is
    /// chosen as <see cref="Force"/> chooses it. The whole selection is re-read, punctuation included: every key of it was
    /// pressed under the wrong layout. The host types the result over the selection (no backspaces), and
    /// <see cref="Undo"/> puts the original back while nothing else happens. Text is held in memory only.
    /// </summary>
    public Correction? ConvertSelection(string text, ushort windowLang, long stamp, out SelectionRefusal why)
    {
        lock (_sync)
        {
            why = Refuse(text, stamp);
            if (why != SelectionRefusal.None) return null;
            if (SelectionSource(text, windowLang) is not { } source) { why = SelectionRefusal.NoLetters; return null; }
            var candidates = _targets[source.LangId].Length > 0 ? _targets[source.LangId] : _sameScript[source.LangId];
            var picked = Pick(candidates.Select(o => (o, _decider.Decide(text, source, o))), byRank: false);
            if (picked is not { } p) { why = SelectionRefusal.NoTarget; return null; }
            var (target, best) = p;
            var replacement = source.TransliterateTo(target, text);
            if (replacement == text) { why = SelectionRefusal.Unchanged; return null; }

            _stamp++;
            _run.Clear();
            ScreenClear();
            // The converted text is all we can vouch for before the caret: typing it replaced the selection. Not trimmed
            // like typing is, so undo reaches all of it.
            _screen.Append(replacement);
            for (int i = 0; i < replacement.Length; i++) _screenLang.Add(target.LangId);
            _undo = new Undoable(replacement, text, source.LangId, target.LangId, CorrectionKind.Force);
            _undoTail = 0;
            _fix = null; // asked for explicitly: erasing it is not a rejection of ours
            _rejected = null;
            _runLang = target.LangId;
            _confirmedLang = target.LangId;
            _recentInScript[target.Script] = target.LangId;
            return new Correction(CorrectionKind.Selection, 0, replacement, false, source.LangId, target.LangId, best.Margin, "selection");
        }
    }

    private SelectionRefusal Refuse(string text, long stamp)
    {
        if (_blocked) return SelectionRefusal.Blocked;
        if (stamp != _stamp) return SelectionRefusal.Stale;
        if (text.Length == 0) return SelectionRefusal.Empty;
        if (text.Length > MaxSelectionChars) return SelectionRefusal.TooLong;
        foreach (var c in text)
            if (char.IsControl(c) || c is '\u2028' or '\u2029') return SelectionRefusal.LineBreak;
        return SelectionRefusal.None;
    }

    /// <summary>
    /// The layout a selection was typed in: the alphabet most of its letters belong to, among the active layouts; within
    /// it the window's layout, else the one the user typed in last, else the one that types most of the letters.
    /// </summary>
    private KeyboardLayoutTable? SelectionSource(string text, ushort windowLang)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in text)
        {
            if (!char.IsLetter(c) || Scripts.Of(c) is not { } script) continue;
            if (!_byLang.Values.Any(l => l.Script == script && l.Letters.Contains(c))) continue;
            counts[script] = counts.GetValueOrDefault(script) + 1;
        }
        if (counts.Count == 0) return null;
        _byLang.TryGetValue(windowLang, out var window);
        // Most letters; on a tie, the window's alphabet.
        var top = counts.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key == window?.Script).First().Key;
        if (window is not null && window.Script == top) return window;
        if (_recentInScript.TryGetValue(top, out var recent) && _byLang.TryGetValue(recent, out var r)) return r;
        return _byLang.Values.Where(l => l.Script == top).MaxBy(l => text.Count(l.Letters.Contains));
    }

    /// <summary>
    /// The hotkey with no run: the user noticed a wrong-layout word after typing past it ("rkuubyh?"). Converts the last
    /// word before the caret, and re-reads the separators after it under the same target layout, since they were typed
    /// in the same belief about the layout. Only as far back as <see cref="_screen"/> vouches for, so the backspaces
    /// never reach text we did not see typed.
    /// </summary>
    private Correction? FlipLastWord()
    {
        int end = _screen.Length, i = end;
        while (i > 0 && !IsWordAt(i - 1)) i--;
        int wordEnd = i;
        if (wordEnd == 0) return null;
        ushort lang = _screenLang[wordEnd - 1];
        while (i > 0 && _screenLang[i - 1] == lang && IsWordAt(i - 1)) i--;
        if (!_byLang.TryGetValue(lang, out var typedTable)) return null;

        var word = _screen.ToString(i, wordEnd - i);
        var candidates = _targets[lang].Length > 0 ? _targets[lang] : _sameScript[lang];
        var picked = Pick(candidates.Select(o => (o, _decider.Decide(word, typedTable, o))), byRank: false);
        if (picked is not { } p) return null;
        var (target, best) = p;

        var original = _screen.ToString(i, end - i);
        var replacement = typedTable.TransliterateTo(target, word) + ReRead(wordEnd, target.LangId);
        _undo = new Undoable(replacement, original, lang, target.LangId, CorrectionKind.Force);
        _undoTail = 0;
        _fix = null; // asked for explicitly: erasing it is not a rejection of ours
        ScreenReplace(original.Length, replacement, target.LangId);
        _runLang = target.LangId;
        _confirmedLang = target.LangId;
        _recentInScript[target.Script] = target.LangId;
        return new Correction(CorrectionKind.Force, original.Length, replacement, false, lang, target.LangId, best.Margin, "force last word");
    }

    private bool IsWordAt(int i)
    {
        ushort lang = _screenLang[i];
        return _byLang.TryGetValue(lang, out var table) && Tokenizer.IsWordChar(_screen[i], table, _others[lang]);
    }

    /// <summary><see cref="_screen"/> from <paramref name="from"/> to its end, each char re-read from the layout it was typed in to <paramref name="toLang"/>.</summary>
    private string ReRead(int from, ushort toLang)
    {
        if (from >= _screen.Length || !_byLang.TryGetValue(toLang, out var to)) return "";
        var sb = new StringBuilder(_screen.Length - Math.Max(from, 0));
        for (int i = Math.Max(from, 0); i < _screen.Length; i++)
        {
            ushort lang = _screenLang[i];
            sb.Append(lang != toLang && _byLang.TryGetValue(lang, out var t) ? t.TransliterateTo(to, _screen[i].ToString()) : _screen[i].ToString());
        }
        return sb.ToString();
    }

    private void ScreenAppend(string text, ushort lang)
    {
        _screen.Append(text);
        for (int i = 0; i < text.Length; i++) _screenLang.Add(lang);
        // Keep twice the longest run: room for a run, a correction of it and the separators after it.
        int over = _screen.Length - 2 * _opt.MaxRunChars;
        if (over <= 0) return;
        _screen.Remove(0, over);
        _screenLang.RemoveRange(0, over);
        if (_undo is { } u && _screen.Length < u.OnScreen.Length + _undoTail) _undo = null;
    }

    private void ScreenErase(int count)
    {
        count = Math.Min(count, _screen.Length);
        _screen.Remove(_screen.Length - count, count);
        _screenLang.RemoveRange(_screenLang.Count - count, count);
    }

    /// <summary>Mirror a correction: <paramref name="erase"/> backspaces, then <paramref name="text"/> typed in <paramref name="lang"/>.</summary>
    private void ScreenReplace(int erase, string text, ushort lang)
    {
        ScreenErase(erase);
        ScreenAppend(text, lang);
    }

    private void ScreenClear()
    {
        _screen.Clear();
        _screenLang.Clear();
    }

    private (Correction? corr, bool resetRun) DecideAt(string boundary, bool enter)
    {
        var typedTable = _byLang[_runLang];
        var onScreen = _run.ToString();
        var typed = onScreen + boundary;
        var ctx = new DecisionContext(_confirmedLang);

        if (_rejected is { } rj)
        {
            if (rj.Lang == _runLang && Retypes(typed, typedTable, rj.Original) is { } complete)
            {
                if (!complete) { Skip(SkipReason.Retyping); return (null, false); } // the first words of it so far: wait for the rest
                foreach (var t in Tokenizer.Split(rj.Original, typedTable, _others[_runLang])) _exceptions.Add(Norm(t.Text));
                _rejected = null;
                _learnt = true;
                _confirmedLang = _runLang;
                Skip(SkipReason.Retyped);
                return (null, true);
            }
            _rejected = null; // they typed something else
        }
        // Words the user insisted on stay as typed and confirm their language, so the next words are judged on their own
        // and cannot drag them into a correction.
        if (IsException(typed, typedTable)) { _confirmedLang = _runLang; Skip(SkipReason.Learnt); return (null, true); }

        var picked = Pick(_targets[_runLang].Select(o => (o, _decider.Decide(typed, typedTable, o, ctx))), byRank: true);
        if (picked is not { } p) { Skip(SkipReason.NoTarget); return (null, false); }
        var (target, best) = p;

        if (best.Kind == DecisionKind.Correct && best.Replacement is { } replacement)
        {
            var corr = new Correction(CorrectionKind.Auto, onScreen.Length, replacement, enter, _runLang, target.LangId, best.Margin, best.Reason);
            _undo = enter ? null : new Undoable(replacement, typed, _runLang, target.LangId, CorrectionKind.Auto);
            _undoTail = 0;
            _fix = _undo;
            _afterFix = _fixDeleted = 0;
            ScreenReplace(onScreen.Length, replacement, target.LangId);
            _run.Clear();
            _confirmedLang = target.LangId;
            _recentInScript[target.Script] = target.LangId;
            return (corr, true);
        }
        bool clear = best.Kind == DecisionKind.Keep && best.Margin <= -_confirm;
        Skip(best.Cause switch
        {
            DecisionCause.Address => SkipReason.Address,
            DecisionCause.NoProfile => SkipReason.NoProfile,
            DecisionCause.Empty or DecisionCause.NoWords => SkipReason.NoWords,
            _ when best.Kind == DecisionKind.Defer => SkipReason.Short,
            _ => clear ? SkipReason.Clear : SkipReason.BelowThreshold,
        }, target.LangId, best);
        if (clear)
        {
            _confirmedLang = _runLang;
            return (null, true);
        }
        return (null, false);
    }

    /// <summary>Note why the run at this boundary stays as typed. Lengths and scores only.</summary>
    private void Skip(SkipReason reason, ushort target = 0, Decision? d = null) =>
        _skipped = new SkippedRun(reason, _runLang, target, _run.Length, d?.Words ?? 0, d?.Letters ?? 0,
            d?.TypedKnown ?? 0, d?.OtherKnown ?? 0, d?.Margin ?? 0, d?.Threshold ?? 0);

    private SkippedRun? TakeSkipped() { var s = _skipped; _skipped = null; return s; }

    /// <summary>
    /// Choose the target among per-layout decisions. First one member per script: the most recently used member of
    /// that script unless another beats its margin by more than <see cref="SessionOptions.RecencyPreference"/>. The
    /// members' readings of the same keys barely differ, so this is a tie-break, not a vote. Then across scripts, the
    /// strongest decision (by kind when <paramref name="byRank"/>, then by margin): different alphabets read very
    /// differently, so there the score alone decides.
    /// </summary>
    private (KeyboardLayoutTable Target, Decision Decision)? Pick(IEnumerable<(KeyboardLayoutTable Target, Decision Decision)> decisions, bool byRank)
    {
        (KeyboardLayoutTable Target, Decision Decision)? best = null;
        foreach (var group in decisions.GroupBy(x => x.Target.Script))
        {
            ushort? recent = _recentInScript.TryGetValue(group.Key, out var r) ? r : null;
            var member = group.MaxBy(x => x.Decision.Margin + (x.Target.LangId == recent ? _opt.RecencyPreference : 0));
            if (best is not { } b) { best = member; continue; }
            int cmp = byRank ? Rank(member.Decision).CompareTo(Rank(b.Decision)) : 0;
            if (cmp > 0 || (cmp == 0 && member.Decision.Margin > b.Decision.Margin)) best = member;
        }
        return best;
    }

    private static int Rank(Decision d) => d.Kind switch { DecisionKind.Correct => 2, DecisionKind.Defer => 1, _ => 0 };

    private bool IsException(string typed, KeyboardLayoutTable table)
    {
        if (_exceptions.Count == 0) return false;
        var tokens = Tokenizer.Split(typed, table, _others[table.LangId]);
        return tokens.Count > 0 && tokens.All(t => _exceptions.Contains(Norm(t.Text)));
    }

    /// <summary>
    /// Null if <paramref name="typed"/> is not <paramref name="original"/> being typed again; false while it is its first
    /// words, true once it has all of them.
    /// </summary>
    private bool? Retypes(string typed, KeyboardLayoutTable table, string original)
    {
        var now = Tokenizer.Split(typed, table, _others[table.LangId]);
        var was = Tokenizer.Split(original, table, _others[table.LangId]);
        if (now.Count == 0 || now.Count > was.Count) return null;
        for (int i = 0; i < now.Count; i++) if (Norm(now[i].Text) != Norm(was[i].Text)) return null;
        return now.Count == was.Count;
    }

    private bool TakeLearnt() { var l = _learnt; _learnt = false; return l; }

    private static string Norm(string s) => s.ToLowerInvariant();

    private void Trim()
    {
        if (_run.Length <= _opt.MaxRunChars) return;
        int cut = -1;
        for (int i = _run.Length - _opt.TrimToChars; i < _run.Length; i++)
            if (_run[i] == ' ') { cut = i + 1; break; }
        if (cut < 0) _run.Clear(); else _run.Remove(0, cut);
    }
}
