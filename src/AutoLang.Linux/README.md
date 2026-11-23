# AutoLang for Linux: an IBus input method

The Linux host, a tester build since 0.6.0. Same engine and session as Windows and Mac. It runs as an IBus engine on
Ubuntu (GNOME, Wayland or X11): no root process, no `input` group, no keyboard-layout switching. Tester install page:
https://bnk989.github.io/AutoLang/linux-test-7htmr/ (unlisted).

## How it works

- `ibus-daemon` starts `/usr/lib/autolang/autolang --ibus` (from `/usr/share/ibus/component/autolang.xml`) as the
  user, and talks to it over its private D-Bus. [IBus.cs](IBus.cs) speaks that protocol directly with
  Tmds.DBus.Protocol; no libibus, no Python.
- AutoLang **is** the keyboard source. It translates every physical key itself on the current language's XKB layout
  ([Xkb.cs](Xkb.cs), libxkbcommon), so GNOME never has to switch layouts.
- `SessionController` is used unchanged; [AutoLangEngine.cs](AutoLangEngine.cs) maps IBus key events to
  `OnChar` / `OnEnter` / `OnBackspace` / `Force` / `Undo` and applies the `Correction` it returns.
- **Corrections edit preedit.** Everything since the last word start stays uncommitted; when a new word starts, the
  controller can no longer rewrite what came before (Undo and "last word" both end there), so it is committed. Enter,
  Tab, arrows, shortcuts and unknown keys commit first. A correction that reaches past the preedit falls back to
  `DeleteSurroundingText` or BackSpace key events.
- Password, PIN and terminal fields (IBus content purpose) are typed in the current language and never corrected.
- The top-bar menu has the language indicator (`InputMode` property), "Type in …" and "Pause corrections".
- Settings are `~/.config/autolang/settings.json` ([Settings.cs](Settings.cs)): layouts, paused, learnt words. The log is
  `~/.local/state/autolang/autolang.log`; it never contains typed text unless `AUTOLANG_LOG_TEXT=1`.

## Command line

| | |
|---|---|
| `autolang enable` / `disable` | Add AutoLang to GNOME's input sources and select it, or take it out ([Gnome.cs](Gnome.cs)). |
| `autolang doctor` | Checks IBus, the component, the input source, the engine process, layouts and word lists ([Doctor.cs](Doctor.cs)). |
| `autolang layouts` | The configured layouts and what each key types. |
| `autolang try "akuo vh "` | Types those keys (named by their US-layout characters) through the engine, offline. |

## Why preedit (results from the spike, WSL Ubuntu 24.04, IBus 1.5.29, X11 clients, keys typed with xdotool)

| How a correction reaches the screen | GTK 3 | Chrome 158 | VS Code (Electron) |
|---|---|---|---|
| **preedit**: undecided text stays uncommitted, corrections edit it | all pass | all pass | all pass |
| surrounding: commit every key, then `DeleteSurroundingText` | all pass | fails: delete ignored (`akuoשלום`) | fails, same |
| forward: commit every key, then BackSpace key events | fails: BackSpaces land after the text | fails, same | not run |
| forward with a 40 ms pause before the commit | 1 flaky failure (undo) | fails (runs differ) | not run |

Chrome and Electron on X11 report no surrounding-text support (capabilities `0x9`) and ignore deletes; GTK reports
`0x29` and honours them. Corrections take about 0.1 ms in the engine; word lists load in 0.5 to 1.2 s after start.

## Not verified yet (needs a real Ubuntu desktop)

- GNOME on Wayland: Mutter's text-input-v3 bridge to IBus, Chrome and Electron on native Wayland, Firefox (a Snap).
- The top-bar indicator and menu, and how preedit looks (it is sent with "underline: none").
- Qt apps and LibreOffice.

## Build and test

```powershell
.\packaging\linux\build-deb.ps1        # dist\autolang_0.6.0-tester1_amd64.deb, via WSL for dpkg-deb
```

On Linux with the .NET 8 SDK: `dotnet publish src/AutoLang.Linux -c Release -r linux-x64 --self-contained
-p:PublishSingleFile=true -o dist/linux-x64`, then `packaging/linux/build-deb.sh dist/linux-x64 0.6.0~tester1`.

[tests/linux/run-tests.sh](../../tests/linux/run-tests.sh) `[gtk-textview|gtk-entry|chrome|vscode] [preedit|surrounding|forward|auto]`
types eleven cases into a real app through IBus and the installed package and reads the text back. Needs an X display
(WSLg is enough), `ibus ibus-gtk3 python3-gi gir1.2-gtk-3.0 xdotool x11-xkb-utils`, and the package installed. It runs
the engine with its own settings folder in /tmp.
