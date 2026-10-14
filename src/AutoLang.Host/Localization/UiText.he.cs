namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Hebrew() => new()
    {
        PausedTitle = "AutoLang מושהה",
        ReadyingTitle = "AutoLang מתכונן",
        IdleTitle = "AutoLang לא פעיל",
        OnTitle = "AutoLang פועל",
        PausedHowToResume = "כדי להמשיך, יש ללחוץ על הסמל במגש",
        DownloadingWordLists = "רשימות מילים בהורדה…",
        NeedsTwoAlphabets = "נדרשות רשימות מילים לשני אלפביתים",
        OnWhatItDoes = "מתקן מילים שהוקלדו בפריסה הלא נכונה",
        Close = "סגירה",

        WelcomeWhatExample = "הקלדת בפריסת מקלדת לא נכונה? AutoLang מתקן את המילה ומחליף את הפריסה בשבילך:",
        WelcomeWhat = "הקלדת בפריסת מקלדת לא נכונה? AutoLang מתקן את המילה ומחליף את הפריסה בשבילך.",
        WelcomePausedSubtitle = "אין תיקונים עד לחידוש הפעולה",
        WelcomeOnSubtitle = "פועל ברקע",
        WelcomeIdleBody = "כדי לבחור עם אילו מקלדות AutoLang עובד, יש לפתוח את \"שפות…\" מתפריט המגש.",
        Resume = "המשך",
        GotIt = "הבנתי",
        TipUndo = "לחיצה פעמיים על {0} מבטלת תיקון",
        TipPause = "לחיצה על {0} במגש משהה את AutoLang",
        TipHidden = "הסמל מוסתר? הוא נמצא מתחת ל-{0} ליד השעון.",
        ShiftKey = "Shift",

        Starting = "בהפעלה…",
        PauseAutoLang = "השהיית AutoLang",
        ResumeAutoLang = "חידוש פעולת AutoLang",
        ClickTrayIcon = "לחיצה על הסמל",
        DisableForThisApp = "השבתה עבור אפליקציה זו",
        DisableFor = "השבתה עבור {0}",
        CompatibilityForThisApp = "תאימות עבור אפליקציה זו",
        CompatibilityFor = "תאימות עבור {0}",
        TypingStandard = "רגיל",
        TypingStandardNote = "מומלץ",
        TypingKeys = "הקשות מקשים",
        TypingKeysNote = "למשחקים ולשולחן עבודה מרוחק",
        TypingPaste = "הדבקה",
        TypingPasteNote = "משתמש לרגע בלוח",
        TypingHintThisApp = "אם התיקונים יוצאים משובשים באפליקציה זו, כדאי לנסות אפשרות אחרת.",
        TypingHintFor = "אם התיקונים יוצאים משובשים ב-{0}, כדאי לנסות אפשרות אחרת.",
        SwitchTyped = "החלפת מה שהוקלד זה עתה",
        ShiftTwice = "Shift פעמיים",
        SwitchTypedHow =
            "לחיצה פעמיים על Shift בשדה הטקסט, מיד אחרי ההקלדה, מעבירה את הטקסט לפריסה האחרת. " +
            "אם AutoLang תיקן מילה רגע לפני כן, היא מבטלת את התיקון. (גם מקש Pause מבטל, ו-Shift+Pause ממיר.)",
        LanguagesMenu = "שפות…",
        SkipFullscreen = "דילוג על חלונות במסך מלא",
        StartWithWindows = "הפעלה עם Windows",
        StartWithWindowsSettings = "הפעלה עם Windows…",
        ShowLog = "הצגת היומן",
        OpenLogFile = "פתיחת קובץ היומן",
        Exit = "יציאה",

        StatePaused = "מושהה",
        StateIdle = "לא פעיל: נדרשים שני אלפביתים",
        StateBlocked = "חסום: {0}",
        StateWatching = "פעיל",
        CorrectionsThisSession = "תיקונים בהפעלה זו: {0}",
        ClickToResume = "לחיצה להמשך",
        ClickToPause = "לחיצה להשהיה",
        NoLanguagesReady = "אין שפות מוכנות",
        WordListsDownloading = "רשימות מילים בהורדה: {0}",

        ReasonStarting = "בהפעלה",
        ReasonNoWindow = "אין חלון",
        ReasonPaused = "מושהה",
        ReasonAdmin = "חלון של מנהל מערכת (Windows חוסם את AutoLang)",
        ReasonOutOfReach = "אין גישה לאפליקציה",
        ReasonExcluded = "אפליקציה מוחרגת",
        ReasonDisabled = "הושבת על ידך",
        ReasonFullscreen = "חלון במסך מלא",
        ReasonPassword = "שדה סיסמה",
        ReasonFailed = "בדיקת המיקוד נכשלה",

        FirstFixTitle = "זה היה AutoLang",
        FirstFixBody = "הוא תיקן מילה שהוקלדה בפריסת מקלדת לא נכונה. כדי לבטל, יש ללחוץ פעמיים על Shift מיד.",
        AdminTitle = "AutoLang לא יכול לעזור בחלון הזה",
        AdminBody =
            "{0} פועל כמנהל מערכת, ו-Windows לא מאפשר לאפליקציות רגילות לקרוא ממנו או להקליד בו. AutoLang ממשיך לפעול בכל מקום אחר.",
        PasteNotice =
            "תיקונים ב-{0} עוברים עכשיו דרך הלוח. התוכן שהעתקת מוחזר מיד אחרי כל תיקון, ולעולם לא נשמר או נשלח לשום מקום.",
        StartupAppsNotice = "אפשר להפעיל או לכבות את AutoLang ברשימת אפליקציות ההפעלה.",
        StartupChangeFailed = "לא ניתן היה לשנות את הגדרת ההפעלה.",

        LanguagesTitle = "AutoLang — שפות",
        LanguagesIntro =
            "AutoLang מתקן מילים שהוקלדו באלפבית הלא נכון ומחליף את המקלדת בשבילך. בין מקלדות שחולקות אלפבית " +
            "(אנגלית וגרמנית, למשל) אין אף פעם מעבר אוטומטי; כשמילה שייכת לאלפבית הזה, AutoLang " +
            "בוחר במקלדת שבה השתמשת לאחרונה.",
        ColumnKeyboard = "מקלדת",
        ColumnAlphabet = "אלפבית",
        ColumnWordList = "רשימת מילים",
        ColumnUse = "שימוש",
        ColumnSwitchTo = "מעבר אליה",
        AutoDownload = "הורדה אוטומטית של רשימות מילים חסרות (הדבר היחיד ש-AutoLang מוריד מהרשת)",
        WarnTwoAlphabets = "AutoLang לא פעיל: נדרשים לפחות שני אלפביתים שונים בשימוש, ולכל אחד רשימת מילים.",
        WarnNoTargets = "AutoLang לא פעיל: כל השפות מוגדרות כך שלעולם לא יעברו אליהן.",
        ListReady = "מוכנה",
        ListReadyDownloaded = "מוכנה (הורדה)",
        ListDownloading = "בהורדה…",
        ListUnavailable = "לא זמינה",
        ListMissing = "חסרה",
        ListDownloadLink = "{0} — הורדה",
        ListErrorOffline = "ההורדה נכשלה (אין חיבור?)",
        ListErrorInvalid = "הקובץ שהורד אינו רשימת מילים תקינה",
        ListErrorUnreadable = "לא ניתן לקרוא את רשימת המילים",
        WordListsCredit = "רשימות מילים: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "לטיני", ["Greek"] = "יווני", ["Cyrillic"] = "קירילי", ["Hebrew"] = "עברי",
            ["Arabic"] = "ערבי", ["Armenian"] = "ארמני", ["Georgian"] = "גאורגי", ["Thai"] = "תאי",
            ["Devanagari"] = "דוונאגרי", ["Han"] = "סיני", ["Hangul"] = "קוריאני (הנגול)",
        },

        LogTitle = "AutoLang — יומן",
        CopyAll = "העתקת הכול",
        Refresh = "רענון",
        LogEarlierOmitted = "… (שורות קודמות הושמטו)",
        LogEmpty = "(היומן ריק)",
        LogUnreadable = "(לא ניתן היה לקרוא את היומן: {0})",

        AlreadyRunning = "AutoLang כבר פועל (הסמל נמצא במגש המערכת).",
        NeedsTwoAlphabetsLong =
            "AutoLang מתקן טקסט שהוקלד באלפבית הלא נכון, ולכן הוא צריך מקלדות של לפחות שני אלפביתים שונים " +
            "(למשל אנגלית ועברית, או אנגלית ויוונית).",
        InstalledKeyboards = "מותקנות: {0}",
        HookFailed = "AutoLang לא הצליח להתקין את מאזין המקלדת שלו.",
    };
}
