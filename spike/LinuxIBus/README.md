# Linux spike: AutoLang as an IBus input method

Throwaway code. Question: can AutoLang run on Ubuntu (Wayland) as an IBus engine, with no root daemon, no `input`
group and no keyboard-layout switching, on the shared 0.6 engine and session? **Yes.**

## How it works

- `ibus-daemon` starts `autolang-ibus --ibus` (from `/usr/share/ibus/component/autolang-spike.xml`) and talks to it over
  its private D-Bus. [IBus.cs](IBus.cs) speaks that protocol directly with Tmds.DBus.Protocol; no libibus, no Python.
- AutoLang **is** the keyboard source. It translates every physical key itself on the current language's XKB layout
  ([Xkb.cs](Xkb.cs), libxkbcommon) and commits the character, so GNOME never has to switch layouts.
- `SessionController` is used unchanged; [AutoLangEngine.cs](AutoLangEngine.cs) only maps IBus key events to
  `OnChar` / `OnEnter` / `OnBackspace` / `Force` / `Undo` and applies the `Correction` it returns.

## Results (WSL Ubuntu 24.04, IBus 1.5.29, X11 clients through WSLg, keys typed with xdotool)

Ten cases per run: plain English, one word to Hebrew, there and back, two words, Enter (plain and correcting),
Backspace inside a word, arrow key mid-word, Double-Shift undo, Double-Shift convert.

| How a correction reaches the screen | GTK 3 | Chrome 158 | VS Code (Electron) |
|---|---|---|---|
| **preedit**: undecided text stays uncommitted, corrections edit it | all pass | all pass | all pass |
| surrounding: commit every key, then `DeleteSurroundingText` | all pass | fails: delete ignored (`akuoשלום`) | fails, same |
| forward: commit every key, then BackSpace key events | fails: BackSpaces land after the text | fails, same | not run |
| forward with a 40 ms pause before the commit | 1 flaky failure (undo) | fails (runs differ) | not run |

- Chrome and Electron on X11 report no surrounding-text support (capabilities `0x9`) and ignore deletes. GTK reports
  `0x29` and honours them.
- Corrections take about 0.1 ms in the engine. Word lists load in 0.55 to 1.2 s after the engine starts; until then keys
  are typed but not corrected.
- `autolang-ibus try "akuo kfuko hello "` gives the same decisions offline as the Windows build (margins identical).

**Decision: preedit is the method.** It is the one path every client implements, and it needs neither deletes nor
key-event ordering. The rule that makes it safe: everything since the last word start stays in preedit; when a new word
starts, the controller can no longer rewrite what came before (Undo and "last word" both end there), so it is committed.
Enter, Tab, arrows, shortcuts and unknown keys commit first. A correction that reaches past the preedit (not seen in any
test) falls back to surrounding or forward.

## Not covered here (needs a real Ubuntu desktop)

- **GNOME Wayland**: Mutter's text-input-v3 bridge to IBus, Chrome and Electron on native Wayland, the top-bar indicator
  showing the `InputMode` symbol (EN/HE), Firefox (a Snap on Ubuntu, so not testable in WSL).
- **How preedit looks**: the spike sends an "underline: none" attribute so preedit should look like plain text; nobody
  has looked at it yet.
- Qt apps, LibreOffice, terminals (purpose `terminal` is typed but not corrected), password fields (same), selection
  conversion, per-app exclusions, settings and the language list UI.

## Run

```bash
dotnet publish spike/LinuxIBus -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o spike/LinuxIBus/publish/linux-x64
sudo sh spike/LinuxIBus/linux/install-dev.sh spike/LinuxIBus/publish/linux-x64   # --remove undoes it
ibus write-cache && ibus restart
```

- `autolang-ibus layouts`: the configured layouts and what each key types.
- `autolang-ibus try "akuo vh "`: types those keys (named by their US-layout characters) through the engine, offline.
- `linux/run-tests.sh [gtk-textview|gtk-entry|chrome|vscode] [preedit|surrounding|forward|auto]`: the end-to-end tests
  above. Needs `ibus ibus-gtk3 python3-gi gir1.2-gtk-3.0 xdotool x11-xkb-utils`. Engine settings for a run go in
  `~/.config/autolang/spike.env`. The log is `~/.local/state/autolang/spike.log`; typed text appears there only with
  `AUTOLANG_LOG_TEXT=1`, which the test script sets.
