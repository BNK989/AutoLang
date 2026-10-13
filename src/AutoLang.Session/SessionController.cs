using System.Text;
using AutoLang.Engine;

namespace AutoLang.Session;

public enum CorrectionKind { Auto, Force, Undo }

/// <summary>
/// What the host must do to the focused control, in this order: delete <see cref="Backspaces"/> characters,
/// type <see cref="Text"/>, press Enter if <see cref="PressEnter"/>, and switch the window's input language to
/// <see cref="TargetLangId"/>. Everything the controller knows about the screen is encoded here; the host never
/// reads the control's text.
/// </summary>
public sealed record Correction(CorrectionKind Kind, int Backspaces, string Text, bool PressEnter,
    ushort FromLangId, ushort TargetLangId, double Margin, string Reason);

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
    /// <summary>The last correction while the user may still erase it: chars typed after it, and how much of it was erased.</summary>
    private Undoable? _fix;
    private int _afterFix, _fixDeleted;
    /// <summary>What the user had typed before they erased our whole correction; typing it again means "leave this alone".</summary>
    private (string Original, ushort Lang)? _rejected;
    /// <summary>Set when a retype added exceptions; the public entry point raises <see cref="ExceptionsAdded"/> outside the lock.</summary>
    private bool _learnt;
    private readonly HashSet<string> _exceptions = new(StringComparer.Ordinal);
    private volatile bool _blocked;

    /// <param name="OnScreen">What our correction put on screen (replacement incl. the boundary char).</param>
    /// <param name="Original">What the user had typed (incl. the boundary char).</param>
    private sealed record Undoable(string OnScreen, string Original, ushort OriginalLang, ushort TargetLang);

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
        set { lock (_sync) { _blocked = value; if (value) { _run.Clear(); _undo = null; _fix = null; _rejected = null; } } }
    }

    public int RunLength { get { lock (_sync) return _run.Length; } }
    public bool CanUndo { get { lock (_sync) return _undo is not null; } }
    public int ExceptionCount { get { lock (_sync) return _exceptions.Count; } }

    /// <summary>Words that must never be auto-corrected (lower-cased). The host persists these as the user's own exception list.</summary>
    public IReadOnlyCollection<string> Exceptions { get { lock (_sync) return _exceptions.ToArray(); } }

    public void AddExceptions(IEnumerable<string> words)
    {
        lock (_sync) foreach (var w in words) if (!string.IsNullOrWhiteSpace(w)) _exceptions.Add(Norm(w.Trim()));
    }

    /// <summary>
    /// Raised (on the caller's thread, outside the lock) when the user rejected a correction by erasing it and typing the
    /// same keys again: those words joined <see cref="Exceptions"/>. Undo needs no event, the host sees its correction.
    /// </summary>
    public event Action? ExceptionsAdded;
    public ushort? ConfirmedLangId { get { lock (_sync) return _confirmedLang; } }

    /// <summary>
    /// A character appeared on screen under layout <paramref name="langId"/>. Word characters are buffered; any
    /// other character is a boundary that triggers the decision on the run so far. When a correction is returned the
    /// host must swallow the key that produced <paramref name="c"/>: the correction's text already contains it,
    /// re-read under the target layout.
    /// </summary>
    public Correction? OnChar(char c, ushort langId)
    {
        Correction? result = null;
        bool learnt;
        lock (_sync)
        {
            _undo = null;
            if (_blocked || !_byLang.TryGetValue(langId, out var table)) { _run.Clear(); _fix = null; return null; }
            // Typing after erasing part of the correction edits it (the user keeps it); otherwise the char sits after it.
            if (_fix is not null && (_fixDeleted > 0 || ++_afterFix > _opt.MaxRunChars)) _fix = null;
            if (_run.Length > 0 && langId != _runLang) _run.Clear(); // the layout changed under us: the screen is no longer one run
            _runLang = langId;
            _recentInScript[table.Script] = langId;

            if (Tokenizer.IsWordChar(c, table, _others[langId])) { _run.Append(c); Trim(); return null; }
            if (_run.Length == 0) return null; // a separator with nothing before it: nothing to judge, nothing to track

            var (corr, resetRun) = DecideAt(c.ToString(), enter: false);
            if (corr is not null) result = corr;
            else if (resetRun) _run.Clear();
            else { _run.Append(c); Trim(); }
            learnt = TakeLearnt();
        }
        if (learnt) ExceptionsAdded?.Invoke();
        return result;
    }

    /// <summary>Enter ends the run either way; if it corrects, the host presses Enter after the replacement. Not undoable.</summary>
    public Correction? OnEnter(ushort langId)
    {
        Correction? corr;
        bool learnt;
        lock (_sync)
        {
            _undo = null;
            if (_blocked || _run.Length == 0 || langId != _runLang) { _run.Clear(); _fix = null; _rejected = null; return null; }
            (corr, _) = DecideAt("", enter: true);
            _run.Clear();
            _undo = null;
            _fix = null;
            _rejected = null;
            learnt = TakeLearnt();
        }
        if (learnt) ExceptionsAdded?.Invoke();
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
            _undo = null;
            if (_run.Length > 0) _run.Length--;
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
            int dropped = _run.Length;
            _run.Clear();
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
            if (_undo is null) return null;
            var u = _undo;
            _undo = null;
            _fix = null;
            var table = _byLang[u.OriginalLang];
            foreach (var t in Tokenizer.Split(u.Original, table, _others[u.OriginalLang])) _exceptions.Add(Norm(t.Text));
            // The restored text is not tracked any more: it must never be corrected again, and the user's next words are judged alone.
            _run.Clear();
            _runLang = u.OriginalLang;
            _confirmedLang = u.OriginalLang;
            return new Correction(CorrectionKind.Undo, u.OnScreen.Length, u.Original, false, u.TargetLang, u.OriginalLang, 0, "undo");
        }
    }

    /// <summary>Convert the current run regardless of score (the manual fallback). Undoable.</summary>
    public Correction? Force(ushort langId)
    {
        lock (_sync)
        {
            _undo = null;
            if (_blocked || _run.Length == 0 || langId != _runLang || !_byLang.TryGetValue(langId, out var typedTable)) { _run.Clear(); return null; }
            var onScreen = _run.ToString();
            // The manual hotkey converts across alphabets like the automatic path; only a user whose layouts all share
            // one alphabet (English + German) gets a same-script conversion, because they asked for it explicitly.
            var candidates = _targets[langId].Length > 0 ? _targets[langId] : _sameScript[langId];
            var picked = Pick(candidates.Select(o => (o, _decider.Decide(onScreen, typedTable, o))), byRank: false);
            if (picked is not { } p) return null;
            var (target, best) = p;
            double bestMargin = best.Margin;
            var replacement = typedTable.TransliterateTo(target, onScreen);
            _undo = new Undoable(replacement, onScreen, langId, target.LangId);
            _fix = null; // asked for explicitly: erasing it is not a rejection of ours
            _run.Clear();
            _confirmedLang = target.LangId;
            _recentInScript[target.Script] = target.LangId;
            return new Correction(CorrectionKind.Force, onScreen.Length, replacement, false, langId, target.LangId, bestMargin, "force");
        }
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
                if (!complete) return (null, false); // the first words of it so far: wait for the rest
                foreach (var t in Tokenizer.Split(rj.Original, typedTable, _others[_runLang])) _exceptions.Add(Norm(t.Text));
                _rejected = null;
                _learnt = true;
                _confirmedLang = _runLang;
                return (null, true);
            }
            _rejected = null; // they typed something else
        }
        // Words the user insisted on stay as typed and confirm their language, so the next words are judged on their own
        // and cannot drag them into a correction.
        if (IsException(typed, typedTable)) { _confirmedLang = _runLang; return (null, true); }

        var picked = Pick(_targets[_runLang].Select(o => (o, _decider.Decide(typed, typedTable, o, ctx))), byRank: true);
        if (picked is not { } p) return (null, false);
        var (target, best) = p;

        if (best.Kind == DecisionKind.Correct && best.Replacement is { } replacement)
        {
            var corr = new Correction(CorrectionKind.Auto, onScreen.Length, replacement, enter, _runLang, target.LangId, best.Margin, best.Reason);
            _undo = enter ? null : new Undoable(replacement, typed, _runLang, target.LangId);
            _fix = _undo;
            _afterFix = _fixDeleted = 0;
            _run.Clear();
            _confirmedLang = target.LangId;
            _recentInScript[target.Script] = target.LangId;
            return (corr, true);
        }
        if (best.Kind == DecisionKind.Keep && best.Margin <= -_confirm)
        {
            _confirmedLang = _runLang;
            return (null, true);
        }
        return (null, false);
    }

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
