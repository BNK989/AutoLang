namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Arabic() => new()
    {
        PausedTitle = "AutoLang متوقف مؤقتاً",
        ReadyingTitle = "جارٍ تجهيز AutoLang",
        IdleTitle = "AutoLang غير نشط",
        OnTitle = "AutoLang قيد التشغيل",
        PausedHowToResume = "انقر فوق الأيقونة في علبة النظام للاستئناف",
        DownloadingWordLists = "جارٍ تنزيل قوائم الكلمات…",
        NeedsTwoAlphabets = "يحتاج إلى قوائم كلمات لأبجديتين",
        OnWhatItDoes = "يصحح الكلمات المكتوبة بالتخطيط الخطأ",
        Close = "إغلاق",

        WelcomeWhatExample = "هل كتبت بتخطيط لوحة المفاتيح الخطأ؟ يصحح AutoLang الكلمة ويبدّل التخطيط نيابةً عنك:",
        WelcomeWhat = "هل كتبت بتخطيط لوحة المفاتيح الخطأ؟ يصحح AutoLang الكلمة ويبدّل التخطيط نيابةً عنك.",
        WelcomePausedSubtitle = "لن يتم تصحيح أي شيء حتى تستأنف",
        WelcomeOnSubtitle = "يعمل في الخلفية",
        WelcomeIdleBody = "افتح \"اللغات…\" من قائمة علبة النظام لاختيار لوحات المفاتيح التي يعمل معها AutoLang.",
        Resume = "استئناف",
        GotIt = "حسناً",
        TipUndo = "اضغط {0} مرتين للتراجع عن التصحيح",
        TipPause = "انقر فوق {0} في علبة النظام للإيقاف المؤقت",
        TipHidden = "الأيقونة مخفية؟ ابحث تحت {0} بجوار الساعة.",
        ShiftKey = "Shift",

        Starting = "جارٍ البدء…",
        PauseAutoLang = "إيقاف AutoLang مؤقتاً",
        ResumeAutoLang = "استئناف AutoLang",
        ClickTrayIcon = "النقر فوق الأيقونة",
        DisableForThisApp = "تعطيل في هذا التطبيق",
        DisableFor = "تعطيل في {0}",
        CompatibilityForThisApp = "التوافق مع هذا التطبيق",
        CompatibilityFor = "التوافق مع {0}",
        TypingStandard = "قياسي",
        TypingStandardNote = "موصى به",
        TypingKeys = "ضغطات المفاتيح",
        TypingKeysNote = "للألعاب وسطح المكتب البعيد",
        TypingPaste = "لصق",
        TypingPasteNote = "يستخدم الحافظة للحظة",
        TypingHintThisApp = "جرّب خياراً آخر إذا ظهرت التصحيحات مشوّهة في هذا التطبيق.",
        TypingHintFor = "جرّب خياراً آخر إذا ظهرت التصحيحات مشوّهة في {0}.",
        SwitchTyped = "تبديل ما كتبته للتو",
        ShiftTwice = "Shift مرتين",
        SwitchTypedHow =
            "اضغط Shift مرتين في حقل النص، مباشرةً بعد الكتابة، لتحويل النص إلى التخطيط الآخر. " +
            "وإذا ضغطته مباشرةً بعد أن يصحح AutoLang كلمة، فإنه يتراجع عن التصحيح. (يتراجع مفتاح Pause أيضاً، ويقوم Shift+Pause بالتحويل.)",
        LanguagesMenu = "اللغات…",
        LearntWordsMenu = "الكلمات المتعلَّمة",
        LearntWordsHint = "انقر على كلمة لتُصحَّح مجددًا.",
        ClearAll = "مسح الكل",
        SkipFullscreen = "تخطي نوافذ ملء الشاشة",
        StartWithWindows = "بدء التشغيل مع Windows",
        StartWithWindowsSettings = "بدء التشغيل مع Windows…",
        ShowLog = "إظهار السجل",
        OpenLogFile = "فتح ملف السجل",
        Exit = "خروج",

        StatePaused = "متوقف مؤقتاً",
        StateIdle = "غير نشط: يحتاج إلى أبجديتين",
        StateBlocked = "محظور: {0}",
        StateWatching = "يراقب",
        CorrectionsThisSession = "التصحيحات في هذه الجلسة: {0}",
        ClickToResume = "انقر للاستئناف",
        ClickToPause = "انقر للإيقاف المؤقت",
        NoLanguagesReady = "لا توجد لغات جاهزة",
        WordListsDownloading = "جارٍ تنزيل قوائم الكلمات: {0}",

        ReasonStarting = "جارٍ البدء",
        ReasonNoWindow = "لا توجد نافذة",
        ReasonPaused = "متوقف مؤقتاً",
        ReasonAdmin = "نافذة مسؤول (يمنع Windows وصول AutoLang إليها)",
        ReasonOutOfReach = "تعذر الوصول إلى التطبيق",
        ReasonExcluded = "تطبيق مستثنى",
        ReasonDisabled = "معطّل من قبلك",
        ReasonFullscreen = "نافذة ملء الشاشة",
        ReasonPassword = "حقل كلمة مرور",
        ReasonFailed = "فشل فحص التركيز",

        FirstFixTitle = "كان ذلك AutoLang",
        FirstFixBody = "لقد صحح كلمة مكتوبة بتخطيط لوحة المفاتيح الخطأ. للتراجع، اضغط Shift مرتين على الفور.",
        AdminTitle = "لا يمكن لـ AutoLang المساعدة في هذه النافذة",
        AdminBody =
            "يعمل {0} كمسؤول، ولا يسمح Windows للتطبيقات العادية بالقراءة منه أو الكتابة فيه. يستمر AutoLang في العمل في كل مكان آخر.",
        PasteNotice =
            "تتم التصحيحات في {0} الآن عبر الحافظة. تتم إعادة المحتوى الذي نسخته مباشرةً بعد كل تصحيح، ولا يتم الاحتفاظ به أو إرساله إلى أي مكان أبداً.",
        StartupAppsNotice = "شغّل AutoLang أو أوقف تشغيله في قائمة تطبيقات بدء التشغيل.",
        StartupChangeFailed = "تعذر تغيير إعداد بدء التشغيل.",

        LanguagesTitle = "AutoLang — اللغات",
        LanguagesIntro =
            "يصحح AutoLang الكلمات المكتوبة بالأبجدية الخطأ ويبدّل لوحة المفاتيح نيابةً عنك. لا يتم أبداً التبديل تلقائياً بين لوحات المفاتيح " +
            "التي تشترك في أبجدية واحدة (مثل الإنجليزية والألمانية)؛ وعندما تنتمي الكلمة إلى تلك الأبجدية، يختار AutoLang " +
            "اللوحة التي استخدمتها آخر مرة.",
        ColumnKeyboard = "لوحة المفاتيح",
        ColumnAlphabet = "الأبجدية",
        ColumnWordList = "قائمة الكلمات",
        ColumnUse = "استخدام",
        ColumnSwitchTo = "التبديل إليها",
        AutoDownload = "تنزيل قوائم الكلمات الناقصة تلقائياً (الشيء الوحيد الذي يجلبه AutoLang من الإنترنت)",
        WarnTwoAlphabets = "AutoLang غير نشط: يحتاج إلى أبجديتين مختلفتين على الأقل قيد الاستخدام، لكل منهما قائمة كلمات.",
        WarnNoTargets = "AutoLang غير نشط: تم ضبط كل اللغات على عدم التبديل إليها أبداً.",
        ListReady = "جاهزة",
        ListReadyDownloaded = "جاهزة (تم تنزيلها)",
        ListDownloading = "جارٍ التنزيل…",
        ListUnavailable = "غير متوفرة",
        ListMissing = "مفقودة",
        ListDownloadLink = "{0} — تنزيل",
        ListErrorOffline = "فشل التنزيل (غير متصل؟)",
        ListErrorInvalid = "الملف الذي تم تنزيله ليس قائمة كلمات صالحة",
        ListErrorUnreadable = "تتعذر قراءة قائمة الكلمات",
        WordListsCredit = "قوائم الكلمات: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "اللاتينية", ["Greek"] = "اليونانية", ["Cyrillic"] = "السيريلية", ["Hebrew"] = "العبرية",
            ["Arabic"] = "العربية", ["Armenian"] = "الأرمنية", ["Georgian"] = "الجورجية", ["Thai"] = "التايلاندية",
            ["Devanagari"] = "الديفاناغارية", ["Han"] = "الصينية (هان)", ["Hangul"] = "الكورية (هانغول)",
        },

        LogTitle = "AutoLang — السجل",
        CopyAll = "نسخ الكل",
        Refresh = "تحديث",
        LogEarlierOmitted = "… (تم حذف الأسطر السابقة)",
        LogEmpty = "(السجل فارغ)",
        LogUnreadable = "(تعذرت قراءة السجل: {0})",

        AlreadyRunning = "AutoLang قيد التشغيل بالفعل (ابحث عن الأيقونة في علبة النظام).",
        NeedsTwoAlphabetsLong =
            "يصحح AutoLang النص المكتوب بالأبجدية الخطأ، لذا يحتاج إلى لوحات مفاتيح لأبجديتين مختلفتين على الأقل " +
            "(مثل الإنجليزية والعبرية، أو الإنجليزية واليونانية).",
        InstalledKeyboards = "المثبّتة: {0}",
        HookFailed = "تعذر على AutoLang تثبيت خطاف لوحة المفاتيح الخاص به.",
    };
}
