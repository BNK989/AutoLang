# AutoLang privacy policy

Last updated: 2026-10-13

AutoLang is a Windows utility that notices when you type in the wrong keyboard layout, fixes the
word, and switches the layout. To do that it has to see your keystrokes. This page says exactly
what it does with them.

## What AutoLang reads

- **Keystrokes**, through a standard Windows keyboard hook, so it can tell which word you are typing.
- **The active window**: its program name, whether it is fullscreen, whether it runs as administrator,
  and whether the focused field is a password field. This decides whether AutoLang works there at all.
- **Your installed keyboard layouts**, to know which languages to consider.

## What AutoLang keeps

- **In memory only:** the word or line you are typing right now. It is dropped when you press Enter,
  click, or switch windows, and never written anywhere.
- **On disk, in your user profile:**
  - your settings (pause, apps you disabled, the typing method per app, language choices),
  - words you told AutoLang not to correct (by tapping Shift twice to undo a correction, or by deleting a correction and typing the word again),
  - downloaded word lists (public files, see below),
  - a log of counts and timings for troubleshooting (for example "corrected 1 word in chrome.exe
    in 12 ms"). The log never contains anything you typed.

Uninstalling the Store / installed version removes all of it. The portable version keeps it in
`%LocalAppData%\AutoLang`; delete that folder to remove it.

## What leaves your computer

Nothing you type, ever. AutoLang has no account, no analytics, no telemetry, and no crash reporting.

The only network request it makes is downloading a **word list** for a keyboard language that does
not ship with it: a plain download of a public file from the FrequencyWords project on GitHub. The
request names the language (for example `ru`) and contains nothing about you or your typing. GitHub
sees your IP address, as with any download. You can switch this off in the tray menu → **Languages…**.

## The clipboard

By default AutoLang never touches the clipboard. If you switch a particular app to the
**Paste via clipboard** typing method, a correction in that app briefly puts the corrected word on
the clipboard, pastes it, and puts back what you had copied. The corrected word is marked so that
Windows clipboard history, cloud clipboard and clipboard managers skip it. Your copied content is
held in memory for a fraction of a second and is not read, stored or sent.

## Where AutoLang stays out

Password fields, terminals, remote-desktop and virtual-machine windows, password managers,
fullscreen games, and any app you disable from the tray menu. Windows itself keeps AutoLang out of
apps running as administrator.

## Contact

Questions about this policy, or anything else: open an issue at
https://github.com/BNK989/AutoLang/issues.

The source code is public at https://github.com/BNK989/AutoLang, so all of the above can be checked.
This policy is also published at https://bnk989.github.io/AutoLang/privacy.html.
