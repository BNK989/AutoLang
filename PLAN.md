# Auto-Detect Language — Project Plan

A Windows tray utility that makes keyboard-layout switching unnecessary. The user
types; the tool decides from the *text itself* which installed layout the user
meant, fixes what was already typed, and switches the layout so the next words
come out right. The user can always undo, and undo teaches the tool.

Status: **M0, M1, M2 code done; any-keyboard support done (2026-10-12); M4 code done (2026-10-13) → M2 dogfooding,
Store submission pending (§8)**. Decisions below are locked unless marked *open*.
M0 findings: `spike/M0Spike/README.md`. Headline: mechanics work in Win32/Chromium/Electron; injection must move
off the hook thread; "word key" = letter on *any* installed layout.
M1 state (2026-10-12): engine library (`src/AutoLang.Engine`: KeyboardLayoutTable, Tokenizer, CharNGramModel,
WordDictionary, LanguageProfile + HebrewRules, PhraseDecider), 31 unit tests (`tests/AutoLang.Engine.Tests`, real
layout dumps in `data/layouts`), eval harness (`tools/AutoLang.Eval`: report, `--sweep`, `--explain`). Word lists
(hermitdave FrequencyWords 50k, CC-BY-SA 4.0) and corpus (Tatoeba, CC-BY 2.0 FR) are in `data/` (not shipped; see
`data/README.md`). **Harness results and chosen thresholds: §4.1.** Remaining M1 work: a harder corpus (chat-style
text, names, slang, mixed-script lines) — Tatoeba sentences do not separate the threshold settings.
M2 state (2026-10-12): two new projects. `src/AutoLang.Session` (pure, tested): `SessionController` owns the current
run, calls `PhraseDecider` on every word key / Enter, turns decisions into `Correction` records (backspaces, text,
re-emit Enter), keeps undo state and the in-memory exception set. `src/AutoLang.Host` (`AutoLang.exe`, tray):
**hook thread** (LL keyboard + mouse hooks, focus WinEvent, 5-min re-arm timer) resolves the char via `ToUnicodeEx`
on the foreground window's HKL, feeds the controller and only *enqueues* corrections → **corrector thread** does the
`SendInput` batch (backspaces + Unicode text + Enter, Shift+Enter preserved) and posts
`WM_INPUTLANGCHANGEREQUEST` → **focus thread** (STA) vets every focus change, default-deny: the run is blocked the
moment focus moves and unblocked only after the worker has checked paused / excluded / disabled / elevated /
fullscreen / UIA `IsPassword`, with sequence numbers so stale checks are dropped. Layouts and word lists are
discovered at start (`LayoutProbe` over the installed HKLs, `data/words/<iso>_50k.txt`, embedded in the exe since 0.4.2); a language
without a list is ignored. Hotkeys: `Pause` = undo + learn, `Shift+Pause` = force-convert the run. Tray menu:
pause, disable for the current app, skip fullscreen, open the log file. Persisted: `%LocalAppData%\AutoLang\
settings.json` (toggles, excluded/disabled apps, exception words) and `host.log` (counts and timings, never text).
Deviation from §2: the tray icon is a WinForms `NotifyIcon` (WPF has none); WPF stays referenced for UIA and the
M3 settings window. Known gaps, all planned for M3/M4: elevated windows are invisible to the hook and stay blocked;
the first keystrokes after a focus change are not tracked until the focus check returns (30–400 ms); exclusion is
by process name only, so VS Code's integrated terminal is not excluded; the fullscreen heuristic can block
borderless maximised apps (toggle in the menu).
Tester packages (M4): `packaging/build-msix.ps1` (MSIX, for the Store or signed with a test cert for sideloading)
and `packaging/build-portable.ps1` (portable single-file `AutoLang.exe`, word lists inside, unsigned; a zip before 0.4.2). Both self-contained, ~61-66 MB
because the .NET desktop runtime is inside. Store steps, certification notes and the clean-machine checklist:
`packaging/STORE.md`.
Multi-language state (2026-10-12): the host now works with whatever keyboards are installed, grouped by alphabet
(§2 "Alphabets"). `Scripts` + `KeyboardLayoutTable.Script` (majority script of the unshifted letters);
`SessionController.Configure` / `CanCorrect` / `neverSwitchTo`, with targets restricted to other scripts and the
last-used member of a script group winning ties. Host: `WordLists` (on-demand download from pinned FrequencyWords
into `%LocalAppData%\AutoLang\words`, size cap, validated before use), `LanguageManager` (installed languages, user
choices, reconfigures the controller live as lists arrive), tray **Languages…** window (`LanguagesForm`: per keyboard
alphabet, list state / download link, *Use*, *Switch to it*, auto-download toggle, attribution). Settings:
`InactiveLanguages`, `NeverSwitchTo`, `AutoDownloadWordLists`. Tests: `MultiLanguageTests` (54 total, all green);
he/en eval unchanged from §4.1. Not yet exercised at runtime: a live download and a non-Hebrew second alphabet.
M4 state (2026-10-13), version 0.4.0. **Per-app typing method** (§3.3) chosen from the tray for the app under the
menu: Unicode (default), Keys (`ToUnicodeEx`-mapped key presses on the target layout) or Paste (clipboard borrowed
and restored format for format, marked out of history / cloud / monitors); a method that cannot do a job declines
and Unicode types it. **Elevated windows**: detected (`Native.IsOutOfReach`, UIPI integrity check), orange icon with
reason, one balloon per session. **Hook watchdog**: 2 s timer on the hook thread; re-installs both hooks when input
arrived (`GetLastInputInfo`) but the hook saw none, or a callback ran late; at most every 30 s, and never for input
that went to an admin window. **Crash restart**: `RegisterApplicationRestart("--restarted")`; a restart that finds
another instance running exits quietly. **Packaging**: `packaging/msix/AppxManifest.xml` (full trust, StartupTask
`AutoLangStartup` enabled by default, `internetClient` for word lists), logos and `AutoLang.ico` generated by
`tools/AutoLang.Assets` from `IconArt`; packaged, start-at-login opens Settings → Startup instead of the Run key and
the log opens in an in-app viewer (AppData is redirected into the package). `PRIVACY.md` is the Store privacy policy.
Not exercised yet: installing the MSIX, a real crash restart, a watchdog re-arm, Keys/Paste in the field. The UIA
`TextPattern` replacement from §3.3 was not built: Keys and Paste cover the apps M0 flagged, and UIA replacement
cannot preserve the app's undo stack; revisit if dogfood finds an app none of the three handles.

---

## 1. The pain point

Keyboard layout is invisible state. Bilingual users (here: Hebrew + English)
discover the active layout only *after* typing `akuo` instead of `שלום`, or
`יקךךם` instead of `hello`. Cost: delete, Alt+Shift, retype — dozens of times a
day — plus the messages that get sent wrong because nobody noticed.

Google search already solves this for one text box: it looks at the **whole
query** and asks "which reading is plausible?". We want that experience in
every text field in Windows.

### What "the Google feel" implies

1. **Decide on the phrase, not the keystroke.** Under the Hebrew↔QWERTY mapping,
   short words collide: `to`↔`אם`, `do`↔`גם`, `go`↔`עם`, `so`↔`דם`, `re`↔`רק`,
   `ha`↔`יש`, `tbh`↔`אני`, `eu`↔`קו`. Scoring the run of text since the input
   began resolves them (`to fi` → `אם כן`; `to be` → English).
2. **False positives are worse than misses.** A miss costs what the user pays
   today; a wrong correction is a *new* annoyance. Tune for precision, with
   one-key undo that also learns.
3. **Correction mechanics, not detection, are the hard part.** Detection is a
   pure function. Rewriting text reliably inside Chrome, Outlook, Word,
   WhatsApp, VS Code, Explorer — and *not* touching password fields, terminals,
   RDP — is where this lives or dies. (Punto Switcher / LangOver are disliked
   for exactly these rough edges.)

---

## 2. Locked decisions

| Topic | Decision |
|---|---|
| Languages | **Derived from the user's installed keyboard layouts at runtime.** Nothing Hebrew-specific is hard-coded in the host; the key→char mapping comes from `ToUnicodeEx` on each installed HKL. Hebrew/English is the first *supported* pair (dictionaries, LMs, test corpus). |
| Aggressiveness | **Hybrid.** Correct immediately when a word is unambiguous (`hello`, `שלום`); defer short/ambiguous words until the phrase is decisive; retroactively correct the whole run. |
| Stack | **C# / .NET 8**, Win32 via P/Invoke (or CsWin32). Tray/settings UI: WPF (POC) — small, fast to build. Decision engine is a dependency-free class library. |
| Scope | **POC first**, built so it can be published later (clean layering, no shortcuts in privacy/compat). Publishing decisions (signing, installer, PowerToys upstreaming) deferred to after POC proves itself. |
| PowerToys | **Not a PowerToys module for the POC.** PowerToys has no third-party module API — only PowerToys Run plugins (launcher) and Command Palette extensions (preview), neither of which can host a keyboard hook. A core module means forking the monorepo and getting a PR accepted upstream. We *do* use PowerToys (MIT) as a reference: **Quick Accent** (LL hook + `SendInput` character replacement) and **Keyboard Manager** (hook thread, injected-key tagging) solve the same mechanics we need. The engine + host split keeps a later "contribute as a PowerToy" option open. |
| Target apps (acceptance set) | Outlook, Chrome, WhatsApp Desktop, Word, VS Code, Explorer (rename / address bar). |
| Model | Dictionaries + char n-gram language models (the "lightweight AI"; microseconds, no runtime). Tiny ONNX classifier is a **later, eval-gated** option. |
| Alphabets | **Switching follows the alphabet, not the language.** Installed layouts are grouped by script (majority script of their unshifted letters). Within a group (English + German) AutoLang never switches or corrects automatically — the words are mostly valid in both and the user switches deliberately. Between groups it corrects and switches; the target member of a group is chosen by score, ties (variants sharing a list) go to the member the user used last. Language variants (en-US / en-GB) share one word list. The hotkey force-convert still works within a group. |
| Language settings | Per installed language: *Use* (inactive languages are ignored entirely) and *Switch to it* (never a target, but text typed in it is still corrected out of it). With fewer than two active alphabets that have a list, AutoLang is idle and says so. |
| Word lists | Bundled lists (next to the exe) plus **on-demand download** of missing ones from hermitdave/FrequencyWords `content/2018/<code>/<code>_50k.txt`, pinned to commit `525f9b5`, on by default, toggle in Languages…. Downloaded lists win over bundled ones, must have ≥ 1,000 entries and be in the keyboard's alphabet. IME languages (ja/zh/ko) have none. |
| Privacy | Hard constraint, see §6. |

---

## 3. Architecture

```
┌────────────────────────────────────────────────────────────────┐
│ Host (tray app, .NET 8)                                        │
│                                                                │
│  Hook thread ──► event queue ──► Session tracker ──► Engine    │
│  (WH_KEYBOARD_LL,                 (phrase buffer,     (pure)   │
│   enqueue only)                    focus/caret state)    │     │
│                                                          ▼     │
│                                                    Decision    │
│                                                          │     │
│  Corrector ◄─────────────────────────────────────────────┘     │
│  (backspaces → ActivateKeyboardLayout → SendInput Unicode)     │
│                                                                │
│  Context providers: foreground process, UIA focused element    │
│  (IsPassword, control type), layout list                       │
│                                                                │
│  UI: tray icon, caret toast (undo hint), settings, hotkeys     │
└────────────────────────────────────────────────────────────────┘

Engine (class library, no Windows deps, fully unit-testable):
  LayoutMap        key→char tables for each layout, transliteration both ways
  Validators       hard rules (final-letter positions, punctuation-in-word, digits, @ / ://)
  Scorers          dictionary lookup, char n-gram LM log-prob, context prior
  PhraseDecider    scores as-typed vs remapped for the whole run → {Keep | Correct(run) | Defer}
  Personalization  exception words, accepted corrections, per-app rules
```

### 3.1 Engine — scoring

For a run of words `w1..wn` (as typed) and its remap `w1'..wn'`:

- **Hard rejects** (either side): Hebrew final form (ך ם ן ף ץ) not at word end
  → that reading is impossible; a word containing `@`, `://`, or mostly digits
  → never touch; a token with a comma/period *inside* it after remap is a
  strong signal for the other reading (`,`→`ת`, `.`→`ץ`, `'`→`,`, `/`→`.`).
- **Soft score** per word: `log P_lang(word)` from a char 5-gram Witten-Bell LM
  trained on the frequency list with damped weights `ln(1+count)` (raw counts
  swallow the unseen-event mass: an unseen character then costs ~15 nats and
  every rare real word looks like gibberish), plus a dictionary bonus
  `2 + 0.25·(ln relfreq + 10)` when the word is in that language's 50k list,
  with a per-user exception list override. Apostrophes are stripped before
  scoring (`don't`, `ג'ים`); trailing punctuation is stripped; punctuation
  *inside* a word is penalised.
- **Layout-neutral tokens** (identical under both layouts: digits, shifted
  Latin capitals under Hebrew, `XXX`) are skipped entirely — they say nothing
  about the intended layout and must not tilt the margin.
- **Context prior**: previous *confirmed* language in this input (strong),
  current active layout (weak), app hint (URL bar / code editor → Latin bias).
- **Decision**: `margin = score(remapped) − score(typed)`.
  - `margin > T_high` → correct now.
  - `|margin| < T_low` and run is short → **defer** (wait for the next word).
  - else keep.
- Thresholds are set by the eval harness (§4), not by hand.

### 3.2 Session tracker — what is "a run"

A run starts on: focus change (UIA `FocusChanged`), mouse click, Enter, any
navigation key (arrows/Home/End/Tab), any Ctrl/Alt/Win chord, a pause longer
than N seconds, or a completed correction. The buffer holds *only* the current
run; it is cleared in memory on run end and never persisted.

Backspace within a run edits the buffer. Shift/Caps are tracked for case.
Typing with a modifier chord is ignored (shortcut).

### 3.3 Corrector

Default strategy (works in most Win32/WPF/Chromium/Electron edit controls):

1. Tag injected input with a private `dwExtraInfo` signature so the hook
   ignores our own keys.
2. Send `n` Backspaces (n = chars in the run incl. trailing boundary char).
3. `ActivateKeyboardLayout` + `WM_INPUTLANGCHANGEREQUEST` to the foreground
   window so subsequent typing is natively correct.
4. `SendInput` with `KEYEVENTF_UNICODE` for the corrected text + boundary char.
5. Verify (optional, UIA `TextPattern`): if the control exposes text and the
   result doesn't match, roll back.

Per-app typing method (M4), set from the tray for the app under the menu:

- **Unicode** (default): step 4 above.
- **Keys**: switch the layout first, then type each character as the virtual
  key + shift state that produces it on the target layout (`ToUnicodeEx`
  reverse map; a character with no plain/Shift key goes as a Unicode packet).
  For apps that drop or reorder `VK_PACKET` input. Declines when the layout
  switch is not confirmed within 150 ms, or CapsLock / a held Shift would change
  what the keys type.
- **Paste**: borrow the clipboard, Ctrl+V the replacement, restore the user's
  clipboard format for format after ~250 ms. The replacement carries the
  "exclude from history / cloud / monitors" formats. Declines (Unicode types
  instead) when the clipboard cannot be snapshotted faithfully or exceeds 32 MB;
  if another app writes the clipboard meanwhile, the newer content wins.

UIA `TextPattern`/`ValuePattern` replacement was not built (it bypasses the
app's own undo); revisit if dogfood finds an app none of the three handles.

Hook callback must return fast (Windows drops hooks that exceed the LL hook
timeout) — it only enqueues.

### 3.4 User control

- **Undo** hotkey (default `Pause`, configurable): reverts the last correction,
  restores the previous layout, and adds the word(s) to the personal exception
  list. Shown as a hint in the caret toast for ~1.5 s.
- **Force convert** hotkey (default `Shift+Pause`): remap the last word or
  current selection regardless of score — the manual fallback.
- **Double-tap Shift** does both, for keyboards without Pause (most laptops):
  undo when a correction was just made, force convert otherwise.
- **Pause/resume** from tray; **per-app disable** from tray menu.
- Settings: aggressiveness (conservative / balanced / aggressive), excluded
  apps, exception words, start at login.
- The user's own Alt+Shift keeps working and counts as a strong context signal.

---

## 4. Evaluation harness (built before the host)

- Corpus: Hebrew (Wikipedia dump sample, OpenSubtitles-style dialogue, chat-like
  short messages) + English (same categories). Keep it local, deduplicated.
- Simulate wrong-layout typing by transliterating through the real layout map.
- Report precision / recall / deferral rate, sliced by word length, word
  position in run (first word is the critical case), and category.
- Gate: **precision ≥ 99.5% on corrections**, recall as high as that allows.
  Deferred decisions must resolve within ≤ 2 words on ≥ 95% of cases.
- Unit tests for every collision pair in §1 and every hard rule.

### 4.1 Results (2026-10-12, Tatoeba he/en)

**How it is measured.** Each sentence yields two cases: typed in its own layout
(right answer: Keep) and typed in the other layout (right answer: Correct with
the original text). The host acts at every word boundary, so *precision counts
every point it would act*: the 1-, 2-, 3-word prefixes and the full run. A
correction that produces anything other than the intended text — on good text
or on wrong-layout text — is a mistake. Recall is measured on the full run only
(by the end of the run, was it fixed?). First-word outcomes are reported
separately because that is where the user still has most of the text to type.

**Corpus.** 5,000 Hebrew + 5,000 English Tatoeba sentences every character of
which the layout can type, deduplicated → 20,000 cases, ~36k decision points.

**Sweep.** 216 settings: `DictBonus ∈ {0,2,4,8}` × `THigh ∈ {2,4,6,8,10,14}` ×
`TLow ∈ {1,2,4}` × `ShortRunPerLetter ∈ {0,2,4}`. Chooser (per §1, a wrong
correction costs more than a miss): fewest mistakes → highest recall → lowest
first-word deferral → most conservative `THigh`.

**Chosen defaults** (`EngineOptions`): `THigh=10`, `TLow=4`, `DictBonus=2`,
`ShortRunPerLetter=0`, `MinDecisiveLetters=5`, `ImpossiblePenalty=30`,
`InnerPunctPenalty=6`, `ContextPrior=3`.

| Measure | Result |
|---|---|
| Corrections at any prefix | 36,392 — 0 on good text, 0 with wrong replacement |
| Precision | **100.00%** (gate ≥ 99.5%) |
| Recall (full run) | **99.9%** — 10,000 wrong-layout runs: 9,991 fixed, 6 missed, 3 still deferred |
| Good text mangled (full run) | 0 of 10,000 |
| First word | TP 8,432 · FP 0 · missed 87 · deferred 2,962 / 20,000 (14.8%) |
| Defers resolved within 2 more words | **100%** (gate ≥ 95%) |

Misses are short, punctuation-heavy runs (`tz???` → `אז???` margin 8.6,
`eky,?` 9.4, `huo burt/` 4.1). The first-word deferral rate is inflated by
Tatoeba's many "I …" sentences: `I` is layout-neutral, so the first word carries
no signal and the decision waits for the second.

**What the sweep taught.**
- Every one of the 216 settings clears the 99.5% precision gate (worst 99.94%).
  Tatoeba sentences are too clean to separate the settings; the zero-mistake
  frontier is `THigh ≥ 8` with `DictBonus=0` or `THigh ≥ 10` with `DictBonus=2`.
  **The next corpus must be chat-like** (names, slang, abbreviations, numbers,
  mixed-script lines) before the thresholds are trusted beyond the POC.
- The dictionary bonus on *tail* entries is the main false-positive source: the
  50k lists contain junk like `rj` (from `רחץ` typed as `rj.`) and `kang` (from
  `לשמע`) at ln relfreq ≈ −13. Hence the small bonus, frequency-weighted.
- `ShortRunPerLetter` (extra margin for short runs) only adds misses on this
  corpus; kept at 0 but left as a knob for the harder corpus.
- `to fi` is two English dictionary words (sci-fi) as well as `אם כן`: it
  defers (margin 1.6), and `to fi tbh` corrects (margin 23.1). That is the
  intended behaviour — the unit test pins "not Keep" at two words and Correct
  at three.

Reproduce: `dotnet tools/AutoLang.Eval/bin/Release/net8.0/AutoLang.Eval.dll data --max 5000 [--sweep]`;
`--explain "<text>" --layout he|en` prints the per-token score breakdown.

---

## 5. Milestones

| M | Deliverable | Proves |
|---|---|---|
| **M0** | Spike (throwaway): hook + Backspace/SendInput replacement in the 6 target apps; layout map via `ToUnicodeEx` for all installed layouts; measure correction latency and hook timing. | Mechanics viable; which apps need fallbacks |
| **M1** | Engine library + corpus + eval harness + report. | Detection quality, thresholds |
| **M2** (code done, dogfooding) | Tray host: hook, session tracker, phrase correction, undo/force hotkeys, default exclusions, password-field skip, no persistence. | Daily dogfood |
| **M3** | Focus-aware run detection, context priors, learning from undo, caret toast, settings UI, aggressiveness levels. | The Google feel |
| **M4** (code done; Store submission and clean-machine test pending) | Per-app strategies (typing method; UIA fallback dropped), elevated-window handling, installer (MSIX), start at login, crash resilience (hook re-arm, restart). | Shippable |
| **M5** | Eval-gated ONNX classifier; third language (Russian/Arabic) using the same runtime-derived mapping. Possibly package as a PowerToys contribution. | Generality |

POC = M0–M2. Go/no-go after M2 dogfooding.

---

## 6. Privacy & safety constraints (non-negotiable)

- The only keystroke data held is the current run, in memory, cleared on run end.
- Nothing is written to disk except user-authored settings and exception words.
- Nothing leaves the machine. No telemetry in POC; any later telemetry is
  opt-in and contains counts only, never text.
- The only network use is fetching public word lists (a plain GET naming the
  language code, nothing else); it can be turned off in Languages…. The log
  records the code and the outcome only.
- Password fields: skip when UIA reports `IsPassword`, when the control type is
  a password edit, or when the foreground process is on the exclusion list.
- Default exclusions: terminals (Windows Terminal, conhost), RDP / VM / remote
  clients (mstsc, VMware, VirtualBox, Parsec, Citrix), password managers,
  fullscreen exclusive games, credential dialogs.
- Visible tray icon at all times; pause is one click; uninstall removes the
  hook immediately.

---

## 7. Known risks

| Risk | Mitigation |
|---|---|
| App mangles backspace/retype (autocomplete URL bars, rich editors) | M0 measures; UIA fallback; per-app rule |
| Elevated windows don't receive the non-elevated hook (UIPI) | Detected; orange icon + one notice per session; documented. Run-elevated option not built (a Store app cannot self-elevate at login) |
| Hook removed by Windows if callback is slow | Enqueue-only callback; watchdog re-arms both hooks (late callback or input seen by `GetLastInputInfo` but not by the hook; ≤ once per 30 s); `RegisterApplicationRestart` after a crash or hang |
| Short-word ambiguity → flip-flop | Phrase-level decision + defer + exception learning |
| User typing mid-text (caret not at end) | Reset run on click/navigation; never correct unless run is contiguous |
| Shift in Hebrew layout yields Latin capitals (layout-dependent) | Mapping is read from the real layout, incl. shift states |
| Layouts with dead keys / AltGr | Treat those keys as run-breakers in POC |

---

## 8. Open items

- Hotkey defaults (`Pause` / `Shift+Pause`) — implemented in M2; confirm during dogfood that nothing else claims them (the host swallows the key only when a correction actually happened).
- Dictionary licence for shipping: *resolved for now* — we use hermitdave/FrequencyWords (CC-BY-SA 4.0, OpenSubtitles-derived) unmodified as data files, attributed in the Languages… window and `data/README.md`; share-alike covers the lists, not the app. Revisit only if we start shipping modified lists.
- Same-alphabet pairs (English + German): confirm in dogfood that "never switch within a group" is right, and whether the user wants an opt-in per pair.
- Eval covers he/en only; extend `tools/AutoLang.Eval` to a second pair (e.g. en/el or en/ru) once a Tatoeba corpus and layout dump are in `data/`.
- Chat-like eval corpus (see §4.1): source still to pick — needs Hebrew + English short messages with names, slang and numbers; must stay local.
- Whether M0 should also test Teams and Slack.
- **Store (M4, user):** Partner Center account → reserve the name → copy the three Product identity values →
  `packaging/build-msix.ps1 -IdentityName … -Publisher … -PublisherDisplayName …` → submit (`packaging/STORE.md`).
  Needs a public URL for `PRIVACY.md` and its contact email filled in.
- **Clean-machine test (M4, user):** the checklist in `packaging/STORE.md` §5, on a PC or VM that never had AutoLang.
- Optional: an OV code-signing certificate for the zip build, if friends keep using it outside the Store
  (SmartScreen still warns until the certificate builds reputation).
