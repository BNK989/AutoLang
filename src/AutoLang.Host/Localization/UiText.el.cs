namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Greek() => new()
    {
        PausedTitle = "Το AutoLang είναι σε παύση",
        ReadyingTitle = "Το AutoLang προετοιμάζεται",
        IdleTitle = "Το AutoLang είναι ανενεργό",
        OnTitle = "Το AutoLang είναι ενεργό",
        PausedHowToResume = "Κάντε κλικ στο εικονίδιο στην περιοχή ειδοποιήσεων για συνέχιση",
        DownloadingWordLists = "Λήψη λιστών λέξεων…",
        NeedsTwoAlphabets = "Απαιτούνται λίστες λέξεων για δύο αλφάβητα",
        OnWhatItDoes = "Διορθώνει λέξεις που γράφτηκαν σε λάθος διάταξη",
        Close = "Κλείσιμο",

        WelcomeWhatExample = "Πληκτρολογήσατε με λάθος διάταξη πληκτρολογίου; Το AutoLang διορθώνει τη λέξη και αλλάζει τη διάταξη για εσάς:",
        WelcomeWhat = "Πληκτρολογήσατε με λάθος διάταξη πληκτρολογίου; Το AutoLang διορθώνει τη λέξη και αλλάζει τη διάταξη για εσάς.",
        WelcomePausedSubtitle = "Δεν γίνονται διορθώσεις μέχρι να συνεχίσετε",
        WelcomeOnSubtitle = "Εκτελείται στο παρασκήνιο",
        WelcomeIdleBody = "Ανοίξτε το «Γλώσσες…» από το μενού της περιοχής ειδοποιήσεων για να επιλέξετε με ποια πληκτρολόγια λειτουργεί το AutoLang.",
        Resume = "Συνέχιση",
        GotIt = "Το κατάλαβα",
        TipUndo = "Πατήστε {0} δύο φορές για αναίρεση μιας διόρθωσης",
        TipPause = "Κάντε κλικ στο {0} στην περιοχή ειδοποιήσεων για παύση",
        TipHidden = "Κρυφό εικονίδιο; Δείτε στο {0} δίπλα στο ρολόι.",
        ShiftKey = "Shift",

        Starting = "Εκκίνηση…",
        PauseAutoLang = "Παύση του AutoLang",
        ResumeAutoLang = "Συνέχιση του AutoLang",
        ClickTrayIcon = "Κλικ στο εικονίδιο",
        DisableForThisApp = "Απενεργοποίηση για αυτήν την εφαρμογή",
        DisableFor = "Απενεργοποίηση για {0}",
        CompatibilityForThisApp = "Συμβατότητα για αυτήν την εφαρμογή",
        CompatibilityFor = "Συμβατότητα για {0}",
        TypingStandard = "Τυπική",
        TypingStandardNote = "Συνιστάται",
        TypingKeys = "Πατήματα πλήκτρων",
        TypingKeysNote = "Για παιχνίδια και απομακρυσμένες επιφάνειες εργασίας",
        TypingPaste = "Επικόλληση",
        TypingPasteNote = "Χρησιμοποιεί προσωρινά το Πρόχειρο",
        TypingHintThisApp = "Δοκιμάστε άλλη επιλογή αν οι διορθώσεις εμφανίζονται αλλοιωμένες σε αυτήν την εφαρμογή.",
        TypingHintFor = "Δοκιμάστε άλλη επιλογή αν οι διορθώσεις εμφανίζονται αλλοιωμένες στο {0}.",
        SwitchTyped = "Αλλαγή όσων μόλις πληκτρολογήσατε",
        ShiftTwice = "Shift δύο φορές",
        SwitchTypedHow =
            "Πατήστε Shift δύο φορές στο πεδίο κειμένου, αμέσως μετά την πληκτρολόγηση, για να μετατρέψετε το κείμενο στην άλλη διάταξη. " +
            "Αμέσως μετά από μια διόρθωση του AutoLang, την αναιρεί. (Το πλήκτρο Pause επίσης αναιρεί και το Shift+Pause μετατρέπει.)",
        LanguagesMenu = "Γλώσσες…",
        SkipFullscreen = "Παράβλεψη παραθύρων πλήρους οθόνης",
        StartWithWindows = "Εκκίνηση με τα Windows",
        StartWithWindowsSettings = "Εκκίνηση με τα Windows…",
        ShowLog = "Εμφάνιση καταγραφής",
        OpenLogFile = "Άνοιγμα αρχείου καταγραφής",
        Exit = "Έξοδος",

        StatePaused = "Σε παύση",
        StateIdle = "Ανενεργό: απαιτούνται δύο αλφάβητα",
        StateBlocked = "Αποκλεισμένο: {0}",
        StateWatching = "Παρακολουθεί",
        CorrectionsThisSession = "Διορθώσεις σε αυτήν τη συνεδρία: {0}",
        ClickToResume = "Κάντε κλικ για συνέχιση",
        ClickToPause = "Κάντε κλικ για παύση",
        NoLanguagesReady = "Καμία γλώσσα έτοιμη",
        WordListsDownloading = "λήψη λιστών λέξεων: {0}",

        ReasonStarting = "εκκίνηση",
        ReasonNoWindow = "κανένα παράθυρο",
        ReasonPaused = "σε παύση",
        ReasonAdmin = "παράθυρο διαχειριστή (τα Windows δεν επιτρέπουν την πρόσβαση στο AutoLang)",
        ReasonOutOfReach = "μη προσβάσιμη εφαρμογή",
        ReasonExcluded = "εξαιρούμενη εφαρμογή",
        ReasonDisabled = "απενεργοποιήθηκε από εσάς",
        ReasonFullscreen = "παράθυρο πλήρους οθόνης",
        ReasonPassword = "πεδίο κωδικού πρόσβασης",
        ReasonFailed = "αποτυχία ελέγχου εστίασης",

        FirstFixTitle = "Αυτό ήταν το AutoLang",
        FirstFixBody = "Διόρθωσε μια λέξη που πληκτρολογήθηκε με λάθος διάταξη πληκτρολογίου. Για αναίρεση, πατήστε αμέσως Shift δύο φορές.",
        AdminTitle = "Το AutoLang δεν μπορεί να βοηθήσει σε αυτό το παράθυρο",
        AdminBody =
            "Η εφαρμογή {0} εκτελείται ως διαχειριστής και τα Windows δεν επιτρέπουν σε απλές εφαρμογές να διαβάζουν ή να πληκτρολογούν σε αυτήν. Το AutoLang συνεχίζει να λειτουργεί παντού αλλού.",
        PasteNotice =
            "Οι διορθώσεις στο {0} γίνονται πλέον μέσω του Προχείρου. Το περιεχόμενο που έχετε αντιγράψει επανέρχεται αμέσως μετά από κάθε διόρθωση και δεν διατηρείται ούτε αποστέλλεται πουθενά.",
        StartupAppsNotice = "Ενεργοποιήστε ή απενεργοποιήστε το AutoLang στη λίστα εφαρμογών εκκίνησης.",
        StartupChangeFailed = "Δεν ήταν δυνατή η αλλαγή της ρύθμισης εκκίνησης.",

        LanguagesTitle = "AutoLang — Γλώσσες",
        LanguagesIntro =
            "Το AutoLang διορθώνει λέξεις που πληκτρολογήθηκαν σε λάθος αλφάβητο και αλλάζει το πληκτρολόγιο για εσάς. Μεταξύ πληκτρολογίων με κοινό αλφάβητο " +
            "(π.χ. Αγγλικά και Γερμανικά) δεν γίνεται ποτέ αυτόματη εναλλαγή. Όταν μια λέξη ανήκει σε αυτό το αλφάβητο, το AutoLang " +
            "επιλέγει το πληκτρολόγιο που χρησιμοποιήσατε τελευταίο.",
        ColumnKeyboard = "Πληκτρολόγιο",
        ColumnAlphabet = "Αλφάβητο",
        ColumnWordList = "Λίστα λέξεων",
        ColumnUse = "Χρήση",
        ColumnSwitchTo = "Εναλλαγή σε αυτό",
        AutoDownload = "Αυτόματη λήψη λιστών λέξεων που λείπουν (το μόνο που κατεβάζει ποτέ το AutoLang)",
        WarnTwoAlphabets = "Το AutoLang είναι ανενεργό: χρειάζεται τουλάχιστον δύο διαφορετικά αλφάβητα σε χρήση, το καθένα με λίστα λέξεων.",
        WarnNoTargets = "Το AutoLang είναι ανενεργό: δεν επιτρέπεται η εναλλαγή σε καμία γλώσσα.",
        ListReady = "Έτοιμη",
        ListReadyDownloaded = "Έτοιμη (από λήψη)",
        ListDownloading = "Λήψη…",
        ListUnavailable = "Μη διαθέσιμη",
        ListMissing = "Λείπει",
        ListDownloadLink = "{0} — λήψη",
        ListErrorOffline = "η λήψη απέτυχε (εκτός σύνδεσης;)",
        ListErrorInvalid = "το αρχείο που λήφθηκε δεν είναι έγκυρη λίστα λέξεων",
        ListErrorUnreadable = "η λίστα λέξεων δεν είναι αναγνώσιμη",
        WordListsCredit = "Λίστες λέξεων: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Λατινικό", ["Greek"] = "Ελληνικό", ["Cyrillic"] = "Κυριλλικό", ["Hebrew"] = "Εβραϊκό",
            ["Arabic"] = "Αραβικό", ["Armenian"] = "Αρμενικό", ["Georgian"] = "Γεωργιανό", ["Thai"] = "Ταϊλανδικό",
            ["Devanagari"] = "Ντεβαναγκάρι", ["Han"] = "Κινεζικό (Χαν)", ["Hangul"] = "Χανγκούλ",
        },

        LogTitle = "AutoLang — Καταγραφή",
        CopyAll = "Αντιγραφή όλων",
        Refresh = "Ανανέωση",
        LogEarlierOmitted = "… (οι προηγούμενες γραμμές παραλείφθηκαν)",
        LogEmpty = "(η καταγραφή είναι κενή)",
        LogUnreadable = "(δεν ήταν δυνατή η ανάγνωση της καταγραφής: {0})",

        AlreadyRunning = "Το AutoLang εκτελείται ήδη (αναζητήστε το εικονίδιο στην περιοχή ειδοποιήσεων).",
        NeedsTwoAlphabetsLong =
            "Το AutoLang διορθώνει κείμενο που πληκτρολογήθηκε σε λάθος αλφάβητο, οπότε χρειάζεται πληκτρολόγια για τουλάχιστον δύο διαφορετικά αλφάβητα " +
            "(για παράδειγμα Αγγλικά και Εβραϊκά ή Αγγλικά και Ελληνικά).",
        InstalledKeyboards = "Εγκατεστημένα: {0}",
        HookFailed = "Το AutoLang δεν μπόρεσε να εγκαταστήσει το άγκιστρο πληκτρολογίου του.",
    };
}
