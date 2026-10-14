# AutoLang for Mac: tester build

AutoLang fixes text you typed in the wrong keyboard layout. For example, if you type `akuo` with the English layout
active, it becomes `שלום` and macOS switches to Hebrew. It lives in the menu bar and has no Dock icon.

This is an early, **unsigned** build. macOS will not open it until you sign it yourself. That takes one command, below.
It needs macOS 12 (Monterey) or later.

## 1. Pick the right zip

Apple menu → **About This Mac**:

- **Chip: Apple M1 / M2 / M3 / M4…** → `AutoLang-0.5.0-mac-arm64.zip`
- **Processor: Intel…** → `AutoLang-0.5.0-mac-x64.zip`

## 2. Install

1. Double-click the zip. You get `AutoLang.app`.
2. Drag `AutoLang.app` into **Applications**.
3. Open **Terminal** (Applications → Utilities) and paste these two lines:

   ```
   xattr -cr /Applications/AutoLang.app
   codesign --force --deep --sign - /Applications/AutoLang.app
   ```

   The first line removes the "downloaded from the internet" flag. The second line signs the app for this Mac only;
   Apple silicon Macs refuse to run unsigned apps at all. Neither needs a password.

4. Open AutoLang from Applications. A keyboard icon appears in the menu bar, near the clock.

## 3. Give it permission

AutoLang has to see your keystrokes and retype words, so macOS asks for **Accessibility** permission:

1. On the prompt, click **Open System Settings**. You can also use the menu bar icon → **Grant Accessibility permission…**
2. Turn on **AutoLang** in Privacy & Security → Accessibility.
3. Within a couple of seconds, the menu bar icon changes from ⚠︎ to a keyboard. If it doesn't, quit AutoLang from its
   menu and open it again.

Some macOS versions also ask for **Input Monitoring**. If they do, allow that as well.

> **When you install a newer build**, macOS treats it as a different app. Before you open the new build, go to
> Accessibility, select AutoLang, remove it with **−**, then add it again with **+** (or let the prompt add it).

## 4. Try it

You need both an **English** and a **Hebrew** keyboard in System Settings → Keyboard → Input Sources. Use the plain
"Hebrew" layout, not "Hebrew – QWERTY". The menu shows `English ⇄ עברית` when both are there.

1. Open TextEdit or Notes.
2. With the **English** layout active, type `akuo` and then a space. It should become `שלום ` and the layout should
   switch to Hebrew.
3. With the **Hebrew** layout active, press the keys h-e-l-l-o and then a space (`יקךךם`). It should become `hello `.
4. Right after a correction, **tap Shift twice quickly** to undo it. AutoLang remembers that word and leaves it alone
   from then on.
5. To convert a word that was not corrected, tap Shift twice before you type the space.

Also try a chat app (Messages, WhatsApp, Slack), a browser text field, and pressing Enter right after a mistyped
word.

AutoLang does nothing in password fields.

## 5. Report back

Please send:

- Your Mac model, chip, and macOS version.
- What worked, what didn't, and in which apps.
- The log. Use the menu bar icon → **Open log folder** and send `host.log`. It records timing and which app was in
  front. It never records what you typed.

## Known rough edges

- Its own menu items are in English only. The Windows version follows the system language.
- If the Mac is very busy, macOS may switch AutoLang's keyboard watcher off for a moment. AutoLang turns it straight
  back on, but a word typed during that moment is not corrected. If that happens, `host.log` shows "timed out".
- **Start at login** in the menu works on macOS 13 and later. macOS may ask you to approve it in System Settings →
  General → Login Items.

## Uninstall

Quit AutoLang from its menu, then drag it from Applications to the Trash. To remove its settings and logs too, delete
`~/Library/Application Support/AutoLang` and `~/Library/Logs/AutoLang`.
