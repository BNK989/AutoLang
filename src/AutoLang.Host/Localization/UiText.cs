using System.Globalization;

namespace AutoLang.Host;

/// <summary>
/// Every word AutoLang shows, in the Windows display language. English is the default for each string, so a
/// translation that lacks one shows the English one rather than nothing. One file per language
/// (<c>UiText.he.cs</c>, ...) fills in its own; <see cref="Current"/> is picked once at startup.
/// "{0}" marks where a name, a number or (in the tips) a key is put in; translations keep it, anywhere in the sentence.
/// </summary>
internal sealed partial record UiText
{
    /// <summary>The language the UI is shown in. Set before any window is made; never changes while running.</summary>
    public static UiText Current { get; private set; } = new();

    /// <summary>ISO 639-1 code of this translation.</summary>
    public string Code { get; private init; } = "en";

    /// <summary>Hebrew, Arabic, Persian: the flyouts, menu and windows are mirrored.</summary>
    public bool RightToLeft { get; private init; }

    private static readonly Dictionary<string, Func<UiText>> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["he"] = Hebrew, ["ar"] = Arabic, ["fa"] = Persian,
        ["ru"] = Russian, ["uk"] = Ukrainian, ["bg"] = Bulgarian, ["el"] = Greek,
        ["de"] = German, ["es"] = Spanish, ["fr"] = French, ["pt"] = Portuguese, ["it"] = Italian,
    };

    /// <summary>
    /// The Windows display language, or English when there is no translation for it. AUTOLANG_UI=he (say) overrides
    /// it, for trying a translation without changing Windows (keyboard names, which come from Windows, follow it too).
    /// Runs first thing in Main, before the log is open: the "already running" message is already translated.
    /// </summary>
    public static void Init()
    {
        var code = Environment.GetEnvironmentVariable("AUTOLANG_UI")?.Trim();
        if (!string.IsNullOrEmpty(code))
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(code);
                CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
            catch (CultureNotFoundException) { }
        }
        else code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (Translations.TryGetValue(code, out var make))
            Current = make() with { Code = code.ToLowerInvariant(), RightToLeft = IsRightToLeft(code) };
    }

    private static bool IsRightToLeft(string code)
    {
        try { return CultureInfo.GetCultureInfo(code).TextInfo.IsRightToLeft; }
        catch (CultureNotFoundException) { return false; }
    }

    public static string F(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>An alphabet's name ("Latin", "Hebrew", ... as the engine names them), in this language if it has one.</summary>
    public string ScriptName(string script) => Scripts.TryGetValue(script, out var name) ? name : script;

    // ---- states: the status flyout, the welcome card and the tray menu's header ----

    public string PausedTitle { get; init; } = "AutoLang is paused";
    public string ReadyingTitle { get; init; } = "AutoLang is getting ready";
    public string IdleTitle { get; init; } = "AutoLang is idle";
    public string OnTitle { get; init; } = "AutoLang is on";
    public string PausedHowToResume { get; init; } = "Click the tray icon to resume";
    public string DownloadingWordLists { get; init; } = "Downloading word lists…";
    public string NeedsTwoAlphabets { get; init; } = "Needs word lists for two alphabets";
    public string OnWhatItDoes { get; init; } = "Fixing words typed in the wrong layout";
    public string Close { get; init; } = "Close";

    // ---- welcome card ----

    /// <summary>Followed by an example ("akuo → שלום").</summary>
    public string WelcomeWhatExample { get; init; } = "Typed in the wrong keyboard layout? AutoLang fixes the word and switches the layout for you:";
    /// <summary>The same, when there is no example to show.</summary>
    public string WelcomeWhat { get; init; } = "Typed in the wrong keyboard layout? AutoLang fixes the word and switches the layout for you.";
    public string WelcomePausedSubtitle { get; init; } = "Nothing is corrected until you resume";
    public string WelcomeOnSubtitle { get; init; } = "Running in the background";
    public string WelcomeIdleBody { get; init; } = "Open Languages… from the tray menu to choose which keyboards AutoLang works with.";
    public string Resume { get; init; } = "Resume";
    public string GotIt { get; init; } = "Got it";
    /// <summary>{0} is drawn as the Shift key.</summary>
    public string TipUndo { get; init; } = "Tap {0} twice to undo a fix";
    /// <summary>{0} is drawn as the AutoLang tray icon.</summary>
    public string TipPause { get; init; } = "Click {0} in the tray to pause";
    /// <summary>{0} is drawn as the ^ button that opens the hidden tray icons.</summary>
    public string TipHidden { get; init; } = "Icon hidden? Look under {0} next to the clock.";
    /// <summary>The label on the Shift key, as printed on keyboards in this language.</summary>
    public string ShiftKey { get; init; } = "Shift";

    // ---- tray menu ----

    public string Starting { get; init; } = "Starting…";
    public string PauseAutoLang { get; init; } = "Pause AutoLang";
    public string ResumeAutoLang { get; init; } = "Resume AutoLang";
    /// <summary>Beside Pause, in the menu's shortcut column.</summary>
    public string ClickTrayIcon { get; init; } = "Click tray icon";
    public string DisableForThisApp { get; init; } = "Disable for this app";
    /// <summary>{0}: the app, "chrome" say.</summary>
    public string DisableFor { get; init; } = "Disable for {0}";
    public string CompatibilityForThisApp { get; init; } = "Compatibility for this app";
    public string CompatibilityFor { get; init; } = "Compatibility for {0}";
    public string TypingStandard { get; init; } = "Standard";
    public string TypingStandardNote { get; init; } = "Recommended";
    public string TypingKeys { get; init; } = "Key presses";
    public string TypingKeysNote { get; init; } = "For games and remote desktops";
    public string TypingPaste { get; init; } = "Paste";
    public string TypingPasteNote { get; init; } = "Uses the clipboard for a moment";
    public string TypingHintThisApp { get; init; } = "Try another option if fixes come out garbled in this app.";
    public string TypingHintFor { get; init; } = "Try another option if fixes come out garbled in {0}.";
    public string SwitchTyped { get; init; } = "Switch what you just typed";
    /// <summary>Beside "Switch what you just typed", in the shortcut column.</summary>
    public string ShiftTwice { get; init; } = "Shift twice";
    public string SwitchTypedHow { get; init; } =
        "Tap Shift twice in the text field, right after typing, to switch the text to the other layout. " +
        "Right after AutoLang fixes a word, it undoes the fix. (The Pause key also undoes, and Shift+Pause converts.)";
    public string LanguagesMenu { get; init; } = "Languages…";
    public string LearntWordsMenu { get; init; } = "Learnt words";
    public string LearntWordsHint { get; init; } = "Click a word to have it fixed again.";
    public string ClearAll { get; init; } = "Clear all";
    public string SkipFullscreen { get; init; } = "Skip fullscreen windows";
    public string StartWithWindows { get; init; } = "Start with Windows";
    /// <summary>Packaged: opens the startup apps page in Windows Settings.</summary>
    public string StartWithWindowsSettings { get; init; } = "Start with Windows…";
    public string ShowLog { get; init; } = "Show log";
    public string OpenLogFile { get; init; } = "Open log file";
    public string Exit { get; init; } = "Exit";

    // ---- header and tray icon tooltip ----

    public string StatePaused { get; init; } = "Paused";
    public string StateIdle { get; init; } = "Idle: needs two alphabets";
    /// <summary>{0}: why, one of the Reason… strings.</summary>
    public string StateBlocked { get; init; } = "Blocked: {0}";
    public string StateWatching { get; init; } = "Watching";
    public string CorrectionsThisSession { get; init; } = "Corrections this session: {0}";
    public string ClickToResume { get; init; } = "Click to resume";
    public string ClickToPause { get; init; } = "Click to pause";
    public string NoLanguagesReady { get; init; } = "No languages ready";
    /// <summary>After the active languages, in brackets.</summary>
    public string WordListsDownloading { get; init; } = "downloading word lists: {0}";

    public string ReasonStarting { get; init; } = "starting";
    public string ReasonNoWindow { get; init; } = "no window";
    public string ReasonPaused { get; init; } = "paused";
    public string ReasonAdmin { get; init; } = "admin window (Windows keeps AutoLang out)";
    public string ReasonOutOfReach { get; init; } = "app out of reach";
    public string ReasonExcluded { get; init; } = "excluded app";
    public string ReasonDisabled { get; init; } = "disabled by you";
    public string ReasonFullscreen { get; init; } = "fullscreen window";
    public string ReasonPassword { get; init; } = "password field";
    public string ReasonFailed { get; init; } = "focus check failed";

    /// <summary>Why AutoLang is blocked, from the reason <see cref="FocusMonitor"/> logs (always English).</summary>
    public string Reason(string reason) => reason switch
    {
        "starting" => ReasonStarting,
        "no foreground window" => ReasonNoWindow,
        "paused" => ReasonPaused,
        FocusStatus.AdminReason => ReasonAdmin,
        "inaccessible process" => ReasonOutOfReach,
        "excluded app" => ReasonExcluded,
        "disabled by you" => ReasonDisabled,
        "fullscreen window" => ReasonFullscreen,
        "password field" => ReasonPassword,
        "focus check failed" => ReasonFailed,
        _ => reason,
    };

    // ---- notifications ----

    public string FirstFixTitle { get; init; } = "That was AutoLang";
    public string FirstFixBody { get; init; } = "It fixed a word typed in the wrong keyboard layout. To undo, tap Shift twice right away.";
    public string AdminTitle { get; init; } = "AutoLang can't help in this window";
    /// <summary>{0}: the app.</summary>
    public string AdminBody { get; init; } =
        "{0} is running as administrator, and Windows does not let ordinary apps read or type into it. AutoLang keeps working everywhere else.";
    /// <summary>{0}: the app.</summary>
    public string PasteNotice { get; init; } =
        "Corrections in {0} now go through the clipboard. Your copied content is put back right after each one and is never kept or sent anywhere.";
    public string StartupAppsNotice { get; init; } = "Turn AutoLang on or off in the list of startup apps.";
    public string StartupChangeFailed { get; init; } = "Could not change the startup setting.";

    // ---- Languages window ----

    public string LanguagesTitle { get; init; } = "AutoLang — Languages";
    public string LanguagesIntro { get; init; } =
        "AutoLang fixes words typed in the wrong alphabet and switches the keyboard for you. Keyboards that share an alphabet " +
        "(English and German, say) are never switched between automatically; when a word belongs to that alphabet, AutoLang " +
        "picks the one you used last.";
    public string ColumnKeyboard { get; init; } = "Keyboard";
    public string ColumnAlphabet { get; init; } = "Alphabet";
    public string ColumnWordList { get; init; } = "Word list";
    public string ColumnUse { get; init; } = "Use";
    public string ColumnSwitchTo { get; init; } = "Switch to it";
    public string AutoDownload { get; init; } = "Download missing word lists automatically (the only thing AutoLang ever fetches)";
    public string WarnTwoAlphabets { get; init; } = "AutoLang is idle: it needs at least two different alphabets in use, each with a word list.";
    public string WarnNoTargets { get; init; } = "AutoLang is idle: every language is set to never switch to it.";
    public string ListReady { get; init; } = "Ready";
    public string ListReadyDownloaded { get; init; } = "Ready (downloaded)";
    public string ListDownloading { get; init; } = "Downloading…";
    public string ListUnavailable { get; init; } = "Not available";
    public string ListMissing { get; init; } = "Missing";
    /// <summary>{0}: the state ("Missing", or why the download failed); the whole line is a link that downloads.</summary>
    public string ListDownloadLink { get; init; } = "{0} — download";
    public string ListErrorOffline { get; init; } = "download failed (offline?)";
    public string ListErrorInvalid { get; init; } = "download was not a valid word list";
    public string ListErrorUnreadable { get; init; } = "word list unreadable";
    /// <summary>{0}: the credit, which stays in English ("FrequencyWords by Hermit Dave ...").</summary>
    public string WordListsCredit { get; init; } = "Word lists: {0}";

    /// <summary>The alphabets of the keyboards AutoLang is most used with; others keep their English name.</summary>
    public IReadOnlyDictionary<string, string> Scripts { get; init; } = new Dictionary<string, string>();

    // ---- log window ----

    public string LogTitle { get; init; } = "AutoLang — Log";
    public string CopyAll { get; init; } = "Copy all";
    public string Refresh { get; init; } = "Refresh";
    public string LogEarlierOmitted { get; init; } = "… (earlier lines omitted)";
    public string LogEmpty { get; init; } = "(the log is empty)";
    /// <summary>{0}: the error, from Windows.</summary>
    public string LogUnreadable { get; init; } = "(the log could not be read: {0})";

    // ---- startup messages ----

    public string AlreadyRunning { get; init; } = "AutoLang is already running (look for the icon in the tray).";
    public string NeedsTwoAlphabetsLong { get; init; } =
        "AutoLang corrects text typed in the wrong alphabet, so it needs keyboards for at least two different alphabets " +
        "(for example English and Hebrew, or English and Greek).";
    /// <summary>{0}: the installed keyboards.</summary>
    public string InstalledKeyboards { get; init; } = "Installed: {0}";
    public string HookFailed { get; init; } = "AutoLang could not install its keyboard hook.";
}
