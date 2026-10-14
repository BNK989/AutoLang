namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Italian() => new()
    {
        PausedTitle = "AutoLang è in pausa",
        ReadyingTitle = "AutoLang si sta preparando",
        IdleTitle = "AutoLang è inattivo",
        OnTitle = "AutoLang è attivo",
        PausedHowToResume = "Fai clic sull'icona nell'area di notifica per riprendere",
        DownloadingWordLists = "Download degli elenchi di parole…",
        NeedsTwoAlphabets = "Servono elenchi di parole per due alfabeti",
        OnWhatItDoes = "Corregge le parole digitate con il layout sbagliato",
        Close = "Chiudi",

        WelcomeWhatExample = "Hai digitato con il layout di tastiera sbagliato? AutoLang corregge la parola e cambia il layout per te:",
        WelcomeWhat = "Hai digitato con il layout di tastiera sbagliato? AutoLang corregge la parola e cambia il layout per te.",
        WelcomePausedSubtitle = "Nessuna correzione finché non riprendi",
        WelcomeOnSubtitle = "In esecuzione in background",
        WelcomeIdleBody = "Apri Lingue… dal menu dell'area di notifica per scegliere con quali tastiere funziona AutoLang.",
        Resume = "Riprendi",
        GotIt = "Ho capito",
        TipUndo = "Premi {0} due volte per annullare una correzione",
        TipPause = "Fai clic su {0} nell'area di notifica per sospendere",
        TipHidden = "Icona nascosta? Cerca sotto {0} accanto all'orologio.",
        ShiftKey = "Maiusc",

        Starting = "Avvio…",
        PauseAutoLang = "Sospendi AutoLang",
        ResumeAutoLang = "Riprendi AutoLang",
        ClickTrayIcon = "Clic sull'icona",
        DisableForThisApp = "Disattiva per questa app",
        DisableFor = "Disattiva per {0}",
        CompatibilityForThisApp = "Compatibilità con questa app",
        CompatibilityFor = "Compatibilità con {0}",
        TypingStandard = "Standard",
        TypingStandardNote = "Consigliato",
        TypingKeys = "Pressioni dei tasti",
        TypingKeysNote = "Per giochi e desktop remoti",
        TypingPaste = "Incolla",
        TypingPasteNote = "Usa per un attimo gli Appunti",
        TypingHintThisApp = "Prova un'altra opzione se in questa app le correzioni risultano alterate.",
        TypingHintFor = "Prova un'altra opzione se in {0} le correzioni risultano alterate.",
        SwitchTyped = "Converti ciò che hai appena digitato",
        ShiftTwice = "Maiusc due volte",
        SwitchTypedHow =
            "Premi Maiusc due volte nel campo di testo, subito dopo aver digitato, per convertire il testo nell'altro layout. " +
            "Subito dopo una correzione di AutoLang, invece, la annulla. (Anche il tasto Pausa annulla, e Maiusc+Pausa converte.)",
        LanguagesMenu = "Lingue…",
        SkipFullscreen = "Ignora le finestre a schermo intero",
        StartWithWindows = "Avvia con Windows",
        StartWithWindowsSettings = "Avvia con Windows…",
        ShowLog = "Mostra registro",
        OpenLogFile = "Apri file di registro",
        Exit = "Esci",

        StatePaused = "In pausa",
        StateIdle = "Inattivo: servono due alfabeti",
        StateBlocked = "Bloccato: {0}",
        StateWatching = "Attivo",
        CorrectionsThisSession = "Correzioni in questa sessione: {0}",
        ClickToResume = "Fai clic per riprendere",
        ClickToPause = "Fai clic per sospendere",
        NoLanguagesReady = "Nessuna lingua pronta",
        WordListsDownloading = "download degli elenchi di parole: {0}",

        ReasonStarting = "avvio in corso",
        ReasonNoWindow = "nessuna finestra",
        ReasonPaused = "in pausa",
        ReasonAdmin = "finestra di amministratore (Windows esclude AutoLang)",
        ReasonOutOfReach = "app non raggiungibile",
        ReasonExcluded = "app esclusa",
        ReasonDisabled = "disattivato da te",
        ReasonFullscreen = "finestra a schermo intero",
        ReasonPassword = "campo password",
        ReasonFailed = "controllo dello stato attivo non riuscito",

        FirstFixTitle = "È stato AutoLang",
        FirstFixBody = "Ha corretto una parola digitata con il layout di tastiera sbagliato. Per annullare, premi subito Maiusc due volte.",
        AdminTitle = "AutoLang non può aiutarti in questa finestra",
        AdminBody =
            "{0} è in esecuzione come amministratore e Windows non consente alle normali app di leggerne il contenuto o di digitarvi. AutoLang continua a funzionare in tutte le altre finestre.",
        PasteNotice =
            "Le correzioni in {0} ora passano dagli Appunti. Il contenuto copiato viene ripristinato subito dopo ciascuna e non viene mai conservato né inviato altrove.",
        StartupAppsNotice = "Attiva o disattiva AutoLang nell'elenco delle app di avvio.",
        StartupChangeFailed = "Impossibile modificare l'impostazione di avvio.",

        LanguagesTitle = "AutoLang — Lingue",
        LanguagesIntro =
            "AutoLang corregge le parole digitate nell'alfabeto sbagliato e cambia la tastiera per te. Tra tastiere che condividono un alfabeto " +
            "(ad esempio inglese e tedesco) non si passa mai automaticamente; quando una parola appartiene a quell'alfabeto, AutoLang " +
            "sceglie l'ultima che hai usato.",
        ColumnKeyboard = "Tastiera",
        ColumnAlphabet = "Alfabeto",
        ColumnWordList = "Elenco di parole",
        ColumnUse = "Usa",
        ColumnSwitchTo = "Passa a questa",
        AutoDownload = "Scarica automaticamente gli elenchi di parole mancanti (l'unica cosa che AutoLang scarica)",
        WarnTwoAlphabets = "AutoLang è inattivo: servono almeno due alfabeti diversi in uso, ciascuno con un elenco di parole.",
        WarnNoTargets = "AutoLang è inattivo: tutte le lingue sono impostate per non passare mai a esse.",
        ListReady = "Pronto",
        ListReadyDownloaded = "Pronto (scaricato)",
        ListDownloading = "Download in corso…",
        ListUnavailable = "Non disponibile",
        ListMissing = "Mancante",
        ListDownloadLink = "{0} — scarica",
        ListErrorOffline = "download non riuscito (offline?)",
        ListErrorInvalid = "il file scaricato non è un elenco di parole valido",
        ListErrorUnreadable = "elenco di parole illeggibile",
        WordListsCredit = "Elenchi di parole: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Latino", ["Greek"] = "Greco", ["Cyrillic"] = "Cirillico", ["Hebrew"] = "Ebraico",
            ["Arabic"] = "Arabo", ["Armenian"] = "Armeno", ["Georgian"] = "Georgiano", ["Thai"] = "Thailandese",
            ["Devanagari"] = "Devanagari", ["Han"] = "Han", ["Hangul"] = "Hangul",
        },

        LogTitle = "AutoLang — Registro",
        CopyAll = "Copia tutto",
        Refresh = "Aggiorna",
        LogEarlierOmitted = "… (righe precedenti omesse)",
        LogEmpty = "(il registro è vuoto)",
        LogUnreadable = "(impossibile leggere il registro: {0})",

        AlreadyRunning = "AutoLang è già in esecuzione (cerca l'icona nell'area di notifica).",
        NeedsTwoAlphabetsLong =
            "AutoLang corregge il testo digitato nell'alfabeto sbagliato, quindi richiede tastiere per almeno due alfabeti diversi " +
            "(ad esempio inglese ed ebraico, oppure inglese e greco).",
        InstalledKeyboards = "Installate: {0}",
        HookFailed = "AutoLang non è riuscito a installare il suo hook della tastiera.",
    };
}
