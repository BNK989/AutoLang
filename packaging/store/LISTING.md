# Microsoft Store listing

Copy-paste text for Partner Center, in the order the submission pages ask for it. Keep it in step with
the website (`site/strings/en.txt`, which builds `docs/`); every claim here is also made there.

## Pricing and availability

- Markets: all.
- Visibility: public.
- Pricing: **Free**. No trial, no in-app purchases.

## Properties

- Category: **Utilities & tools** (no subcategory).
- Privacy policy URL: https://bnk989.github.io/AutoLang/privacy.html
- Website: https://bnk989.github.io/AutoLang/
- Support contact info: https://github.com/BNK989/AutoLang/issues
- Product declarations: none apply. Leave the "depends on non-Microsoft drivers or NT services" and the
  accessibility boxes unchecked.
- System requirements: leave the hardware boxes unchecked. Minimum OS comes from the package (Windows 10 1809).

## Age ratings

The IARC questionnaire, category **Utility / productivity**. Answer **No** to everything: no violence, no user
content sharing or communication between users, no location sharing, no purchases, no unrestricted web access.
Result: 3+ / Everyone.

## Store listing (English)

**Product name:** AutoLang Keyboard Layout Fixer (the name reserved in Partner Center; "AutoLang" alone was taken)

**Description:**

```
Typed a whole sentence before noticing the keyboard was set to the wrong language? AutoLang fixes it as you type.

When a word comes out in the wrong alphabet ("akuo" instead of "שלום", or "יקךךם" instead of "hello"), AutoLang retypes it in the right one and switches the keyboard for you, so the rest of the sentence comes out right. It works in any app you type in: browsers, Office, chat apps, editors.

Made a correction you didn't want? Tap Shift twice and it is undone; AutoLang remembers that word and never touches it again. Tap Shift twice after a word it left alone and it converts it by hand. (Keyboards with a Pause key can use Pause and Shift+Pause too.)

Works with any pair of keyboards that use different alphabets: Hebrew, Arabic, Russian, Ukrainian, Greek and more, next to English. Keyboards that share an alphabet (English and German, say) are left alone, since most words are valid in both.

A tool that sees your keystrokes has to earn trust. These are design rules, not settings:
• Nothing you type leaves your PC. No account, no analytics, no telemetry.
• Only the current line is kept, in memory, and dropped on Enter, a click or a window switch.
• Password fields are skipped, along with password managers, terminals, remote desktops and virtual machines.
• The log never contains text, only counts and timings.
• The only thing AutoLang downloads is public word lists, and you can turn that off.

AutoLang is free and open source (MIT). The source code is on GitHub so anyone can check these claims.

Requires keyboards for at least two different alphabets (Settings → Time & language → Language & region).
```

**What's new in this version:** leave empty for the first submission. For later ones, a short list of changes.
The latest, for 0.5.0:

```
• Menus and windows now follow your Windows display language: English, Hebrew, Arabic, Persian, Russian, Ukrainian, Bulgarian, Greek, German, Spanish, French, Portuguese and Italian.
• A welcome card on first run shows how to undo a fix and how to pause. Launch AutoLang again to bring it back.
• Clicking the tray icon to pause or resume shows a small card by the tray instead of a notification.
```

**Product features** (one per box):

```
Fixes words typed in the wrong keyboard layout as you type
Switches the keyboard to the right language for you
Works in any app: browsers, Office, chat, editors
Tap Shift twice to undo a correction, and the word is never corrected again
Tap Shift twice to convert text by hand
Supports Hebrew, Arabic, Russian, Ukrainian, Greek and more, next to English
Lives in the tray; the icon shows when it is watching or paused
Menus and windows in 13 languages, following your Windows display language
Turn it off for any app, or change how it retypes there
Skips password fields, terminals, remote desktops and fullscreen games
Nothing you type is stored or sent anywhere
Free and open source
```

**Screenshots** (Desktop; upload in this order, 1366×768). One set per UI language, in
`packaging/store/screenshots/<code>/screenshot-1.png` .. `screenshot-4.png`, with `<code>` one of `en he ar fa ru uk bg el
de es fr pt it`. In Partner Center add each language's listing (Store listings → Add/remove languages) and upload that
language's folder to it; languages without their own listing show the English set. The slide headline is the caption;
English captions:

1. `Typed in the wrong layout? Already fixed.`
2. `Lives in the tray, out of your way`
3. `Works with the keyboards you have`
4. `Nothing you type leaves your PC`

Each language folder also has a `captions.txt` with its four captions, one per line in upload order (UTF-8). The menu and window in each set are
AutoLang's real UI in that language (right to left for he, ar, fa). The listing text itself is still English; the
screenshots are the only localized part so far.

**Store logos:** 1:1 app tile icon `packaging/store-300x300.png`. Box art and poster art: none.

**Search terms** (max 7):

```
keyboard layout
wrong language
Hebrew keyboard
language switcher
typing correction
keyboard switcher
transliteration
```

**Copyright and trademark info:** `© 2026 Ben Klinovsky`

**Additional license terms:**

```
AutoLang is open source under the MIT License: https://github.com/BNK989/AutoLang/blob/main/LICENSE
Word lists are derived from FrequencyWords by Hermit Dave (OpenSubtitles 2018), licensed CC BY-SA 4.0.
```

**Developed by:** Ben Klinovsky

Leave the short description, short title, sort title, voice title, trailers and additional system requirements
empty; they are optional.

## Notes for certification (Supplemental info → Additional Testing Information)

Submission options no longer has a notes box; it links to the Additional Testing Information page. `runFullTrust` is
a restricted capability, so reviewers ask why. Paste this:

```
AutoLang is a desktop (Win32) utility that corrects text typed in the wrong keyboard layout, e.g. Hebrew letters typed while the English layout was active. It needs runFullTrust because it installs a low-level keyboard hook (SetWindowsHookEx WH_KEYBOARD_LL) to see which word is being typed, uses SendInput to replace the mistyped word, and asks the foreground window to switch keyboard layout. None of this is possible from a sandboxed app. Keystrokes are held in memory for the current line only and are never stored or transmitted; see the privacy policy. The only network use is downloading public word lists from GitHub.

To test: add the English (US) and Hebrew keyboards in Settings > Time & language > Language & region. Open Notepad with the English layout active and type "akuo " (with the space). It becomes "שלום " and the layout switches to Hebrew. Tap Shift twice to undo. The tray icon (green "A") has the settings.
```

**Restricted capability justification** (Submission options → "Why do you need the runFullTrust capability"):

```
Required for a low-level keyboard hook (WH_KEYBOARD_LL), SendInput and keyboard layout switching in other apps' windows. AutoLang is a Win32 desktop app packaged with the Desktop Bridge.
```

## Regenerating the screenshots

```powershell
.\packaging\store\render-screenshots.ps1                  # all 13 languages, 52 PNGs
.\packaging\store\render-screenshots.ps1 -Languages he,ru # just those
```

For each language the script runs `tools/AutoLang.Screens`, which starts AutoLang's own tray menu and Languages window in
that UI language (`AUTOLANG_UI`) and draws them off-screen with `Control.DrawToBitmap` into `ui/<code>-tray-menu.png`
and `ui/<code>-languages.png` (client area only; the page draws the title bar). It then renders the four slides of
`screenshots.html` with headless Edge into `screenshots/<code>/`. Nothing appears on screen, and a running AutoLang.exe
does not get in the way (the tool builds into `%TEMP%`). The words on the slides come from `screenshot-strings.js`; a
new UI language needs an entry there, and the script picks it up. Rerun after any change to the menu, the Languages
window or the strings. The translations other than he, ar, ru, el and uk were written for this file and want a native
reader.
