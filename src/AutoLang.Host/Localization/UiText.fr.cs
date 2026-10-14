namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText French() => new()
    {
        PausedTitle = "AutoLang est en pause",
        ReadyingTitle = "AutoLang se prépare",
        IdleTitle = "AutoLang est inactif",
        OnTitle = "AutoLang est activé",
        PausedHowToResume = "Cliquez sur l'icône de la zone de notification pour reprendre",
        DownloadingWordLists = "Téléchargement des listes de mots…",
        NeedsTwoAlphabets = "Nécessite des listes de mots pour deux alphabets",
        OnWhatItDoes = "Corrige les mots tapés avec la mauvaise disposition",
        Close = "Fermer",

        WelcomeWhatExample = "Vous avez tapé avec la mauvaise disposition de clavier ? AutoLang corrige le mot et change de disposition pour vous :",
        WelcomeWhat = "Vous avez tapé avec la mauvaise disposition de clavier ? AutoLang corrige le mot et change de disposition pour vous.",
        WelcomePausedSubtitle = "Aucune correction tant que vous n'avez pas repris",
        WelcomeOnSubtitle = "Fonctionne en arrière-plan",
        WelcomeIdleBody = "Ouvrez Langues… dans le menu de la zone de notification pour choisir les claviers avec lesquels AutoLang fonctionne.",
        Resume = "Reprendre",
        GotIt = "J'ai compris",
        TipUndo = "Appuyez deux fois sur {0} pour annuler une correction",
        TipPause = "Cliquez sur {0} près de l'horloge pour mettre en pause",
        TipHidden = "Icône masquée ? Regardez sous {0} près de l'horloge.",
        ShiftKey = "Maj",

        Starting = "Démarrage…",
        PauseAutoLang = "Suspendre AutoLang",
        ResumeAutoLang = "Reprendre AutoLang",
        ClickTrayIcon = "Clic sur l'icône",
        DisableForThisApp = "Désactiver pour cette application",
        DisableFor = "Désactiver pour {0}",
        CompatibilityForThisApp = "Compatibilité avec cette application",
        CompatibilityFor = "Compatibilité avec {0}",
        TypingStandard = "Standard",
        TypingStandardNote = "Recommandé",
        TypingKeys = "Frappes de touches",
        TypingKeysNote = "Pour les jeux et les bureaux à distance",
        TypingPaste = "Coller",
        TypingPasteNote = "Utilise brièvement le Presse-papiers",
        TypingHintThisApp = "Essayez une autre option si les corrections apparaissent brouillées dans cette application.",
        TypingHintFor = "Essayez une autre option si les corrections apparaissent brouillées dans {0}.",
        SwitchTyped = "Convertir ce que vous venez de taper",
        ShiftTwice = "Maj deux fois",
        SwitchTypedHow =
            "Appuyez deux fois sur Maj dans le champ de texte, juste après la saisie, pour passer le texte dans l'autre disposition. " +
            "Juste après une correction d'AutoLang, cela l'annule. (La touche Pause annule aussi, et Maj+Pause convertit.)",
        LanguagesMenu = "Langues…",
        SkipFullscreen = "Ignorer les fenêtres en plein écran",
        StartWithWindows = "Démarrer avec Windows",
        StartWithWindowsSettings = "Démarrer avec Windows…",
        ShowLog = "Afficher le journal",
        OpenLogFile = "Ouvrir le fichier journal",
        Exit = "Quitter",

        StatePaused = "En pause",
        StateIdle = "Inactif : nécessite deux alphabets",
        StateBlocked = "Bloqué : {0}",
        StateWatching = "Actif",
        CorrectionsThisSession = "Corrections pendant cette session : {0}",
        ClickToResume = "Cliquez pour reprendre",
        ClickToPause = "Cliquez pour suspendre",
        NoLanguagesReady = "Aucune langue prête",
        WordListsDownloading = "téléchargement des listes de mots : {0}",

        ReasonStarting = "démarrage",
        ReasonNoWindow = "aucune fenêtre",
        ReasonPaused = "en pause",
        ReasonAdmin = "fenêtre d'administrateur (Windows en exclut AutoLang)",
        ReasonOutOfReach = "application inaccessible",
        ReasonExcluded = "application exclue",
        ReasonDisabled = "désactivé par vous",
        ReasonFullscreen = "fenêtre en plein écran",
        ReasonPassword = "champ de mot de passe",
        ReasonFailed = "échec de la vérification du focus",

        FirstFixTitle = "C'était AutoLang",
        FirstFixBody = "Il a corrigé un mot tapé avec la mauvaise disposition de clavier. Pour annuler, appuyez tout de suite deux fois sur Maj.",
        AdminTitle = "AutoLang ne peut pas vous aider dans cette fenêtre",
        AdminBody =
            "{0} s'exécute en tant qu'administrateur, et Windows ne permet pas aux applications ordinaires d'y lire ou d'y taper du texte. AutoLang continue de fonctionner partout ailleurs.",
        PasteNotice =
            "Les corrections dans {0} passent désormais par le Presse-papiers. Le contenu que vous avez copié est restauré juste après chacune d'elles et n'est jamais conservé ni envoyé où que ce soit.",
        StartupAppsNotice = "Activez ou désactivez AutoLang dans la liste des applications de démarrage.",
        StartupChangeFailed = "Impossible de modifier le paramètre de démarrage.",

        LanguagesTitle = "AutoLang — Langues",
        LanguagesIntro =
            "AutoLang corrige les mots tapés dans le mauvais alphabet et change de clavier pour vous. Les claviers qui partagent un alphabet " +
            "(l'anglais et l'allemand, par exemple) ne sont jamais permutés automatiquement ; quand un mot appartient à cet alphabet, AutoLang " +
            "choisit celui que vous avez utilisé en dernier.",
        ColumnKeyboard = "Clavier",
        ColumnAlphabet = "Alphabet",
        ColumnWordList = "Liste de mots",
        ColumnUse = "Utiliser",
        ColumnSwitchTo = "Y basculer",
        AutoDownload = "Télécharger automatiquement les listes de mots manquantes (la seule chose qu'AutoLang télécharge)",
        WarnTwoAlphabets = "AutoLang est inactif : il lui faut au moins deux alphabets différents utilisés, chacun avec une liste de mots.",
        WarnNoTargets = "AutoLang est inactif : toutes les langues sont configurées pour ne jamais y basculer.",
        ListReady = "Prête",
        ListReadyDownloaded = "Prête (téléchargée)",
        ListDownloading = "Téléchargement…",
        ListUnavailable = "Non disponible",
        ListMissing = "Manquante",
        ListDownloadLink = "{0} — télécharger",
        ListErrorOffline = "échec du téléchargement (hors connexion ?)",
        ListErrorInvalid = "le fichier téléchargé n'est pas une liste de mots valide",
        ListErrorUnreadable = "liste de mots illisible",
        WordListsCredit = "Listes de mots : {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Latin", ["Greek"] = "Grec", ["Cyrillic"] = "Cyrillique", ["Hebrew"] = "Hébreu",
            ["Arabic"] = "Arabe", ["Armenian"] = "Arménien", ["Georgian"] = "Géorgien", ["Thai"] = "Thaï",
            ["Devanagari"] = "Devanagari", ["Han"] = "Han", ["Hangul"] = "Hangeul",
        },

        LogTitle = "AutoLang — Journal",
        CopyAll = "Tout copier",
        Refresh = "Actualiser",
        LogEarlierOmitted = "… (lignes précédentes omises)",
        LogEmpty = "(le journal est vide)",
        LogUnreadable = "(impossible de lire le journal : {0})",

        AlreadyRunning = "AutoLang est déjà en cours d'exécution (cherchez l'icône dans la zone de notification).",
        NeedsTwoAlphabetsLong =
            "AutoLang corrige le texte tapé dans le mauvais alphabet ; il a donc besoin de claviers pour au moins deux alphabets différents " +
            "(par exemple anglais et hébreu, ou anglais et grec).",
        InstalledKeyboards = "Installés : {0}",
        HookFailed = "AutoLang n'a pas pu installer son hook clavier.",
    };
}
