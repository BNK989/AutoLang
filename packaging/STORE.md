# Shipping AutoLang (M4)

Two outputs, both built from this folder:

| Script | Output | For |
|---|---|---|
| `build-msix.ps1` | `dist/AutoLang-<version>-x64.msix` | Microsoft Store (unsigned upload) or sideload tests (signed with a test cert) |
| `build-zip.ps1` | `dist/AutoLang-win-x64.zip` | Portable build; unsigned, SmartScreen warns |

Both need the .NET 8 SDK. `build-msix.ps1` also needs `makeappx` / `makepri` (and `signtool` when signing). It
uses an installed Windows SDK if there is one, otherwise it fetches Microsoft's `Microsoft.Windows.SDK.BuildTools`
package from nuget.org once into `packaging/.tools` and checks that the tools are Microsoft-signed.

Logos and the exe icon are generated from `src/AutoLang.Host/IconArt.cs`. After changing the art, run:

```
dotnet run --project tools/AutoLang.Assets
```

The version comes from `<Version>` in `src/AutoLang.Host/AutoLang.Host.csproj` (`0.4.0` → package `0.4.0.0`). Bump
it for every Store submission; the Store reserves the 4th part.

Build output folders: `packaging/.build` (staging, recreated each run), `packaging/.tools` (cached SDK tools), `dist/`.
None of them is source.

## 1. Microsoft Store (one-time setup)

1. Open a developer account at https://partner.microsoft.com/dashboard (individual accounts are free).
2. **Apps and games → New product → MSIX or PWA app**, and reserve the name **AutoLang** (or the name you want).
3. In the new product: **Product management → Product identity**. Copy three values:
   - `Package/Identity/Name` (looks like `12345YourName.AutoLang`)
   - `Package/Identity/Publisher` (looks like `CN=1A2B3C4D-....`)
   - `Package/Properties/PublisherDisplayName`

## 2. Build the Store package

```
.\packaging\build-msix.ps1 -IdentityName "<Name>" -Publisher "<Publisher>" -PublisherDisplayName "<Display name>"
```

The result is unsigned on purpose; the Store signs it with Microsoft's certificate, so users get no SmartScreen or
"unknown publisher" warning.

## 3. Submission

**Packages:** upload `dist/AutoLang-<version>-x64.msix`. Device family: Desktop only.

**Properties:** category *Utilities & tools*. Privacy policy URL: required, because the app has `runFullTrust` and
reads input. Use https://bnk989.github.io/AutoLang/privacy.html (the GitHub Pages copy of `PRIVACY.md`, from
`docs/privacy.html`; keep the two in step). Website: https://bnk989.github.io/AutoLang/.

**Age ratings:** the questionnaire. No user-generated content, no communication, no purchases → rated 3+ / Everyone.

**Store listing:**
- Description (draft):
  > Typed a whole sentence in the wrong keyboard language? AutoLang notices "akuo" should have been "שלום" (or
  > "יקךךם" should have been "hello"), fixes it as you type, and switches the keyboard for you. Press Pause to undo;
  > AutoLang remembers. Works with any pair of keyboards that use different alphabets: Hebrew, Arabic, Russian, Greek
  > and more. Skips password fields, terminals and remote desktops. Nothing you type leaves your PC.
- Screenshots: at least one, 1366×768 or larger (PNG). Good ones: the tray menu open; the Languages… window;
  a before/after of a corrected sentence in Notepad.
- Store logos: `packaging/store-300x300.png` is the 1:1 app tile icon (300×300). The optional box art (1080×1080)
  and poster art (720×1080) are not generated; the listing works without them.

**Submission options → Notes for certification.** `runFullTrust` is a restricted capability, so reviewers ask why.
Paste this:

> AutoLang is a desktop (Win32) utility that corrects text typed in the wrong keyboard layout, e.g. Hebrew letters
> typed while the English layout was active. It needs runFullTrust because it installs a low-level keyboard hook
> (SetWindowsHookEx WH_KEYBOARD_LL) to see which word is being typed, uses SendInput to replace the mistyped word,
> and asks the foreground window to switch keyboard layout. None of this is possible from a sandboxed app.
> Keystrokes are held in memory for the current word only and never stored or transmitted; see the privacy policy.
> The only network use is downloading public word lists from GitHub. To test: install an English and a Hebrew
> keyboard, open Notepad with the English layout active, type "akuo " — it becomes "שלום " and the layout switches
> to Hebrew. Press Pause to undo.

**Pricing:** free; markets as you like.

Certification takes from a few hours to a few days. Later updates: bump `<Version>`, rebuild with the same three
values, upload as a new submission.

## 4. Sideload test builds (before or alongside the Store)

A signed `.msix` installs without the Store, provided the PC trusts the signing certificate. The certificate subject
must equal `-Publisher`; the defaults (`CN=AutoLang Dev`) match the test certificate.

The test certificate (created 2026-10-13, `CN=AutoLang Dev`, valid until 2029-10-13) lives in the build PC's
`Cert:\CurrentUser\My` as "AutoLang dev signing". Its private key is non-exportable, so it never leaves that PC and
there is no `.pfx` or password. Every build with the default `-Publisher` signs with it automatically:

```
.\packaging\build-msix.ps1
```

and puts `dist/dev-signing.cer` (the public part, safe to share) next to the `.msix`. It also packs both, plus
the install steps from `packaging/INSTALL-testers.txt`, into `dist/AutoLang-<version>-x64-testers.zip`: send new
testers that one file. Testers who already trust the certificate only need the new `.msix`.
`-Unsigned` skips signing. On a new build PC, or once the certificate expires, run
`.\packaging\build-msix.ps1 -NewDevCertificate` to make a new one. Testers then have to trust the new `.cer`
before they can install updates.

On the test PC (once, as administrator), trust the certificate, then install:

```
Import-Certificate dist\dev-signing.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage dist\AutoLang-0.4.0.0-x64.msix
```

Testers can do the same by double-clicking: `.cer` → Install Certificate → Local Machine → Trusted People, then
the `.msix` → Install. Uninstall: Settings → Apps, or `Get-AppxPackage AutoLang* | Remove-AppxPackage`.

A Store-identity build and a dev-identity build are different apps to Windows and can be installed side by side;
uninstall one before testing the other so only one hook runs.

## 5. Clean-machine checklist

Run on a PC (or Windows Sandbox / a fresh VM) that has never had AutoLang. English + Hebrew keyboards installed.

Install and start
- [ ] `.msix` installs (Store or sideload); AutoLang is in the Start menu with the right icon.
- [ ] Launching it shows the green tray icon; hovering shows the layouts.
- [ ] Sign out and in: AutoLang starts by itself. **Start with Windows…** opens Settings → Startup; switching it
      off there stops the next auto start.

Core
- [ ] Notepad, Chrome, Word/Outlook, WhatsApp desktop: `akuo ` → `שלום `, and `יקךךם ` → `hello `; layout follows.
- [ ] Pause undoes and the word is not corrected again; Shift+Pause forces a conversion.
- [ ] Password field (any login page): nothing is corrected, icon orange.
- [ ] Windows Terminal / cmd: nothing is corrected.

M4 features
- [ ] Notepad run as administrator: icon orange, one notice balloon per session, nothing typed there is touched.
- [ ] Typing method: in some app switch to **Simulated keys**, then **Paste via clipboard**; corrections still
      work. With Paste: copy an image first, trigger a correction, paste. The image comes back, and the corrected
      word is not in Win+V history.
- [ ] Crash restart cannot be forced from outside: `taskkill` is not a crash, and Windows only restarts an app that
      crashed or hung after running at least 60 s. If a real crash happens, the icon comes back by itself and the
      log says `restarted by Windows`.
- [ ] **Show log** opens the in-app log viewer; Copy all works; no typed text in it.
- [ ] Languages…: add a third keyboard (e.g. Russian); its word list downloads, then corrections to it work.

Uninstall
- [ ] Settings → Apps → AutoLang → Uninstall: tray icon gone, no AutoLang process, startup entry gone.
