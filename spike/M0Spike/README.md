# M0 spike — mechanics only

Throwaway code. Its job is to answer four questions before we build anything real:

1. Can we read the installed layouts' key tables at runtime? (`dump`)
2. Can we resolve what the user typed against the *foreground window's* layout?
3. Can we delete-and-retype a word, and switch the window's input language, before the next key lands?
4. How long does that take, and in which of the target apps does it break?

## Run

```bash
dotnet run --project spike/M0Spike -- dump
```

```bash
dotnet run --project spike/M0Spike -- translit 0409 akuo
```

```bash
dotnet run --project spike/M0Spike -- run
```

In `run` mode the console logs every word boundary. Two triggers exist:

- **Space/Enter** after a word: fires only on a hard rule (Hebrew final letter mid-word) or a tiny demo list
  (`שלום תודה הי יום טוב בוקר hello hi thanks ok good morning`). Type `akuo ` in English layout → `שלום `;
  type `vh ` → `הי `. Type `יקךךם ` in Hebrew layout → `hello `.
- **F8**: convert the current (unfinished) word unconditionally. The manual fallback.

Ctrl+C in the console removes the hook.

## Acceptance checklist

For each app: does `akuo ` → `שלום ` render correctly, does the layout switch stick for the next word,
does F8 work mid-word, and what does the log say the replace took?

| App | Replace OK | Layout switch OK | F8 OK | Notes |
|---|---|---|---|---|
| Notepad | ✅ | ✅ next word arrived under the new layout | not pressed | replace 20–22 ms incl. SendInput; callback over the 5 ms budget → M2 must inject off the hook thread |
| Chrome (page textarea) | ✅ | ✅ | ✅ 6.5 ms (`aku` → `שלו`) | replace 7.5–8.7 ms |
| Chrome (address bar) | ✅ | ✅ | — | user-reported OK; autocomplete did not interfere with 4-letter words |
| Outlook (new mail body) | ⏳ | | | not tested in M0 — covered by M2 dogfood |
| Outlook (subject / To) | ⏳ | | | not tested in M0 — covered by M2 dogfood |
| Word | ⏳ | | | not tested in M0 — covered by M2 dogfood |
| WhatsApp Desktop | ✅ | ✅ | ✅ both directions, 2.7–3.8 ms | replace 4–9 ms (`WhatsApp.Root.exe`); `n,h` buffered as one word thanks to the cross-layout punctuation fix |
| VS Code | ⏳ | | | not tested in M0 — covered by M2 dogfood |
| Explorer rename (F2) | ⏳ | | | not tested in M0 — covered by M2 dogfood |
| Explorer address bar | ⏳ | | | not tested in M0 — covered by M2 dogfood |

## M0 findings (2026-10-12)

1. **Mechanics work.** Backspace + `KEYEVENTF_UNICODE` retype + `WM_INPUTLANGCHANGEREQUEST` behaved identically in a
   Win32 control (Notepad), Chromium (Chrome page + omnibox) and Electron (WhatsApp). The layout switch stuck in
   every case — the next word was logged under the new HKL.
2. **Latency is fine, but it's in the wrong place.** `SendInput` inside the hook callback cost 4–22 ms, which is
   over the 5 ms hook budget on every correction. The host must enqueue from the callback and inject from a worker
   thread (as PLAN.md §3.3 already says); the callback still swallows the boundary key so ordering stays
   deterministic.
3. **Cross-layout punctuation matters.** `,` `.` `;` `'` `/` are Hebrew letters; a tokenizer that breaks on them
   loses words like `תודה` (`,usv`) and `מתי` (`n,h`). The engine must define "word key" as *letter on any
   installed layout*.
4. **Modifier key-down must reset the run** (Alt before Alt+Tab showed up as an unresolved VK and dropped the buffer
   late). Fixed in `Hook.cs`.
5. **The 12-word demo list was the limit, not the mechanics.** Everything the user typed that wasn't on it
   (`מילה`, `שלכם`, `אחרת`, `מתי`) was correctly left alone — M1's job is to make those decisions for real.
6. **Unplanned live catch:** `םי` → `oh` via the final-letter rule while the user was typing to Claude. The hard
   rule alone already earns its keep.
7. Untested in M0: Outlook, Word, VS Code, Explorer. They move to M2 dogfood; M4 owns any fallbacks they need.

## Known spike limitations (by design)

- Buffer is the current word only; mouse clicks don't reset it (no mouse hook yet).
- Digits and punctuation end the word without triggering.
- Elevated windows won't be seen unless the spike runs elevated.
- No password-field detection — don't type secrets while it runs.
