namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText German() => new()
    {
        PausedTitle = "AutoLang ist angehalten",
        ReadyingTitle = "AutoLang wird vorbereitet",
        IdleTitle = "AutoLang ist inaktiv",
        OnTitle = "AutoLang ist aktiv",
        PausedHowToResume = "Zum Fortsetzen auf das Symbol im Infobereich klicken",
        DownloadingWordLists = "Wortlisten werden heruntergeladen…",
        NeedsTwoAlphabets = "Benötigt Wortlisten für zwei Alphabete",
        OnWhatItDoes = "Korrigiert Wörter, die im falschen Layout getippt wurden",
        Close = "Schließen",

        WelcomeWhatExample = "Im falschen Tastaturlayout getippt? AutoLang korrigiert das Wort und wechselt das Layout für Sie:",
        WelcomeWhat = "Im falschen Tastaturlayout getippt? AutoLang korrigiert das Wort und wechselt das Layout für Sie.",
        WelcomePausedSubtitle = "Bis zum Fortsetzen wird nichts korrigiert",
        WelcomeOnSubtitle = "Wird im Hintergrund ausgeführt",
        WelcomeIdleBody = "Öffnen Sie im Infobereich-Menü „Sprachen…“, um auszuwählen, mit welchen Tastaturen AutoLang arbeitet.",
        Resume = "Fortsetzen",
        GotIt = "Verstanden",
        TipUndo = "Zum Rückgängigmachen zweimal {0} drücken",
        TipPause = "Zum Anhalten auf {0} im Infobereich klicken",
        TipHidden = "Symbol ausgeblendet? Unter {0} neben der Uhr suchen.",
        ShiftKey = "Umschalt",

        Starting = "Wird gestartet…",
        PauseAutoLang = "AutoLang anhalten",
        ResumeAutoLang = "AutoLang fortsetzen",
        ClickTrayIcon = "Symbol anklicken",
        DisableForThisApp = "Für diese App deaktivieren",
        DisableFor = "Für {0} deaktivieren",
        CompatibilityForThisApp = "Kompatibilität für diese App",
        CompatibilityFor = "Kompatibilität für {0}",
        TypingStandard = "Standard",
        TypingStandardNote = "Empfohlen",
        TypingKeys = "Tastenanschläge",
        TypingKeysNote = "Für Spiele und Remotedesktops",
        TypingPaste = "Einfügen",
        TypingPasteNote = "Verwendet kurz die Zwischenablage",
        TypingHintThisApp = "Probieren Sie eine andere Option aus, wenn Korrekturen in dieser App fehlerhaft erscheinen.",
        TypingHintFor = "Probieren Sie eine andere Option aus, wenn Korrekturen in {0} fehlerhaft erscheinen.",
        SwitchTyped = "Gerade Getipptes umwandeln",
        ShiftTwice = "Zweimal Umschalt",
        SwitchTypedHow =
            "Drücken Sie direkt nach dem Tippen im Textfeld zweimal die Umschalttaste, um den Text in das andere Layout umzuwandeln. " +
            "Direkt nach einer Korrektur durch AutoLang wird diese damit rückgängig gemacht. (Auch die Pause-Taste macht rückgängig, und Umschalt+Pause wandelt um.)",
        LanguagesMenu = "Sprachen…",
        SkipFullscreen = "Vollbildfenster überspringen",
        StartWithWindows = "Mit Windows starten",
        StartWithWindowsSettings = "Mit Windows starten…",
        ShowLog = "Protokoll anzeigen",
        OpenLogFile = "Protokolldatei öffnen",
        Exit = "Beenden",

        StatePaused = "Angehalten",
        StateIdle = "Inaktiv: benötigt zwei Alphabete",
        StateBlocked = "Blockiert: {0}",
        StateWatching = "Aktiv",
        CorrectionsThisSession = "Korrekturen in dieser Sitzung: {0}",
        ClickToResume = "Zum Fortsetzen klicken",
        ClickToPause = "Zum Anhalten klicken",
        NoLanguagesReady = "Keine Sprachen bereit",
        WordListsDownloading = "Wortlisten werden heruntergeladen: {0}",

        ReasonStarting = "wird gestartet",
        ReasonNoWindow = "kein Fenster",
        ReasonPaused = "angehalten",
        ReasonAdmin = "Administratorfenster (von Windows für AutoLang gesperrt)",
        ReasonOutOfReach = "App nicht erreichbar",
        ReasonExcluded = "ausgeschlossene App",
        ReasonDisabled = "von Ihnen deaktiviert",
        ReasonFullscreen = "Vollbildfenster",
        ReasonPassword = "Kennwortfeld",
        ReasonFailed = "Fokusprüfung fehlgeschlagen",

        FirstFixTitle = "Das war AutoLang",
        FirstFixBody = "Ein im falschen Tastaturlayout getipptes Wort wurde korrigiert. Drücken Sie zum Rückgängigmachen sofort zweimal die Umschalttaste.",
        AdminTitle = "AutoLang kann in diesem Fenster nicht helfen",
        AdminBody =
            "{0} wird als Administrator ausgeführt, und Windows lässt normale Apps darin weder lesen noch tippen. AutoLang funktioniert überall sonst weiter.",
        PasteNotice =
            "Korrekturen in {0} laufen jetzt über die Zwischenablage. Ihr kopierter Inhalt wird direkt nach jeder Korrektur wiederhergestellt und nie gespeichert oder irgendwohin gesendet.",
        StartupAppsNotice = "Aktivieren oder deaktivieren Sie AutoLang in der Liste der Autostart-Apps.",
        StartupChangeFailed = "Die Autostart-Einstellung konnte nicht geändert werden.",

        LanguagesTitle = "AutoLang — Sprachen",
        LanguagesIntro =
            "AutoLang korrigiert Wörter, die im falschen Alphabet getippt wurden, und wechselt die Tastatur für Sie. Zwischen Tastaturen mit demselben Alphabet " +
            "(etwa Englisch und Deutsch) wird nie automatisch gewechselt; gehört ein Wort zu diesem Alphabet, wählt AutoLang " +
            "die zuletzt verwendete.",
        ColumnKeyboard = "Tastatur",
        ColumnAlphabet = "Alphabet",
        ColumnWordList = "Wortliste",
        ColumnUse = "Verwenden",
        ColumnSwitchTo = "Dorthin wechseln",
        AutoDownload = "Fehlende Wortlisten automatisch herunterladen (das Einzige, was AutoLang jemals abruft)",
        WarnTwoAlphabets = "AutoLang ist inaktiv: Es werden mindestens zwei verschiedene Alphabete benötigt, jeweils mit einer Wortliste.",
        WarnNoTargets = "AutoLang ist inaktiv: Zu keiner Sprache darf gewechselt werden.",
        ListReady = "Bereit",
        ListReadyDownloaded = "Bereit (heruntergeladen)",
        ListDownloading = "Wird heruntergeladen…",
        ListUnavailable = "Nicht verfügbar",
        ListMissing = "Fehlt",
        ListDownloadLink = "{0} — herunterladen",
        ListErrorOffline = "Download fehlgeschlagen (offline?)",
        ListErrorInvalid = "Download war keine gültige Wortliste",
        ListErrorUnreadable = "Wortliste nicht lesbar",
        WordListsCredit = "Wortlisten: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Lateinisch", ["Greek"] = "Griechisch", ["Cyrillic"] = "Kyrillisch", ["Hebrew"] = "Hebräisch",
            ["Arabic"] = "Arabisch", ["Armenian"] = "Armenisch", ["Georgian"] = "Georgisch", ["Thai"] = "Thailändisch",
            ["Devanagari"] = "Devanagari", ["Han"] = "Han", ["Hangul"] = "Hangul",
        },

        LogTitle = "AutoLang — Protokoll",
        CopyAll = "Alles kopieren",
        Refresh = "Aktualisieren",
        LogEarlierOmitted = "… (frühere Zeilen ausgelassen)",
        LogEmpty = "(das Protokoll ist leer)",
        LogUnreadable = "(das Protokoll konnte nicht gelesen werden: {0})",

        AlreadyRunning = "AutoLang wird bereits ausgeführt (das Symbol befindet sich im Infobereich).",
        NeedsTwoAlphabetsLong =
            "AutoLang korrigiert Text, der im falschen Alphabet getippt wurde, und benötigt daher Tastaturen für mindestens zwei verschiedene Alphabete " +
            "(zum Beispiel Englisch und Hebräisch oder Englisch und Griechisch).",
        InstalledKeyboards = "Installiert: {0}",
        HookFailed = "AutoLang konnte seinen Tastatur-Hook nicht installieren.",
    };
}
