# AutoLang – tester notes (preview build)

AutoLang fixes text typed in the wrong keyboard layout. Type `akuo` and it becomes `שלום`,
type `יקךךם` and it becomes `hello`, and the keyboard switches to the right language for the
next words. It works in every normal text field (Chrome, Outlook, Word, WhatsApp, VS Code, …).

This is an early build for friends. It comes two ways: an installer package (`.msix`, preferred)
and a single portable `AutoLang.exe` (no install).

## Requirements

- Windows 10 (1809 or later) or Windows 11, 64-bit.
- Keyboards for at least two different alphabets (for example English + Hebrew, or English + Greek),
  installed in Settings → Time & language → Language & region. Keyboards that share an alphabet
  (English + German) are never switched between automatically.
- English and Hebrew word lists ship with the build. For any other keyboard language AutoLang downloads
  its word list once, in the background (a public file from FrequencyWords on GitHub; nothing about you
  is sent). Turn that off, or see each language's status, in the tray menu → **Languages…**.
- No .NET install needed.

## Install

### From the Microsoft Store (once it is listed)

Install it like any Store app. No warnings, and it updates itself.

### From the `.msix` file (test builds)

A test build is signed with a test certificate that your PC has to trust once:

1. Double-click `dev-signing.cer` → **Install Certificate…** → **Local Machine** →
   **Place all certificates in the following store** → **Browse…** → **Trusted People** → Finish.
2. Double-click `AutoLang-<version>-x64.msix` → **Install**.

AutoLang appears in the Start menu, starts with Windows by default, and uninstalls cleanly from
Settings → Apps.

### Portable `AutoLang.exe` (no install)

1. Put `AutoLang.exe` anywhere (for example `C:\Users\<you>\AutoLang`). It is a single file with the
   word lists inside; nothing to unzip.
2. Double-click `AutoLang.exe`.
   Windows SmartScreen will warn because the exe is unsigned: click **More info → Run anyway**.
   If Defender quarantines it, restore it and add an exclusion for the folder (it hooks the
   keyboard, which looks suspicious to antivirus).

Either way, a green **A** icon appears in the tray (click the `^` next to the clock if it is hidden).

**Start at login:** right-click the icon → **Start with Windows**. In the installed version this opens
Settings → Apps → Startup, where you switch AutoLang on or off.

To stop: right-click the icon → **Exit**. Nothing runs in the background after that. If AutoLang ever
crashes, Windows restarts it by itself.

## Using it

- Just type. When a word is clearly in the wrong layout it is replaced on the next space or Enter.
- **Pause key** = undo the last correction. The words you undid are remembered and never corrected again.
- **No Pause key?** Delete the whole correction (AutoLang switches the layout back for you) and type the word again. AutoLang leaves it alone and remembers it, just like Pause.
- **Shift+Pause** = force the current text into the other layout (when it did not correct and you wanted it to).
- Tray icon colours: green = watching, grey = paused, orange = this window is skipped
  (hover for the reason).
- Right-click the icon: pause, disable for the current app, typing method for the current app,
  skip fullscreen windows, show the log.
- Double-click the icon to pause/resume.

### If corrections come out garbled in one app

Some apps handle the way AutoLang retypes text badly (letters missing, doubled, or in the wrong
language). Click into that app, then right-click the tray icon → **Typing method for <app>** and try:

- **Simulated keys** – retypes with ordinary key presses on the right keyboard layout.
- **Paste via clipboard** – pastes the corrected word. Whatever you had copied is put back right
  after, and the pasted word is kept out of clipboard history.

The choice is remembered for that app only. **Unicode (default)** is right for almost everything.

### Apps running as administrator

Windows does not let ordinary apps see or type into windows that run as administrator. The icon
turns orange there, and the first time it happens AutoLang tells you why. Everything else keeps
working.

## Privacy

- Only the current line of typing is kept, in memory, and it is dropped when you click, press Enter,
  or change window.
- Nothing you type is ever sent anywhere. The only network use is downloading a missing word list
  (a public file; the request names the language and nothing else). You can turn it off in **Languages…**.
- The log never contains what you typed, only counts and timings.
- Password fields, terminals, remote desktop, VMs and password managers are skipped automatically.
- The full policy is in `PRIVACY.md`.

## What to report

Please send me:

1. What you typed (roughly), in which app, and what happened instead of what you expected:
   - a wrong correction (worst case, tell me the words),
   - a missed correction,
   - text that came out garbled or doubled (and whether another typing method fixed it),
   - a layout switch that happened when it should not have.
2. The log: right-click the icon → **Show log** (installed) or **Open log file** (portable) → copy it.
   It has no typed text in it, only timings and counts.
3. Anything that felt slow or made a key feel "sticky".

Known gaps in this build:

- Apps running as administrator are not corrected (see above).
- The first one or two letters right after switching windows are not tracked.
- The terminal inside VS Code is not skipped (only standalone terminals are).
- Some borderless fullscreen apps are treated as games and skipped; untick
  "Skip fullscreen windows" in the menu if that bothers you.
