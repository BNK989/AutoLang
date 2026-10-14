namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Persian() => new()
    {
        PausedTitle = "AutoLang موقتاً متوقف شده",
        ReadyingTitle = "AutoLang در حال آماده‌سازی است",
        IdleTitle = "AutoLang غیرفعال است",
        OnTitle = "AutoLang فعال است",
        PausedHowToResume = "برای ازسرگیری، روی نماد در سینی سیستم کلیک کنید",
        DownloadingWordLists = "در حال بارگیری فهرست‌های واژه…",
        NeedsTwoAlphabets = "به فهرست واژه برای دو الفبا نیاز دارد",
        OnWhatItDoes = "اصلاح واژه‌هایی که با طرح‌بندی اشتباه تایپ شده‌اند",
        Close = "بستن",

        WelcomeWhatExample = "با طرح‌بندی اشتباه صفحه‌کلید تایپ کردید؟ AutoLang واژه را اصلاح و طرح‌بندی را برایتان عوض می‌کند:",
        WelcomeWhat = "با طرح‌بندی اشتباه صفحه‌کلید تایپ کردید؟ AutoLang واژه را اصلاح و طرح‌بندی را برایتان عوض می‌کند.",
        WelcomePausedSubtitle = "تا ازسرگیری، چیزی اصلاح نمی‌شود",
        WelcomeOnSubtitle = "در پس‌زمینه اجرا می‌شود",
        WelcomeIdleBody = "برای انتخاب صفحه‌کلیدهایی که AutoLang با آن‌ها کار می‌کند، «زبان‌ها…» را از منوی سینی سیستم باز کنید.",
        Resume = "ازسرگیری",
        GotIt = "متوجه شدم",
        TipUndo = "برای لغو اصلاح، {0} را دو بار فشار دهید",
        TipPause = "برای توقف، روی {0} در سینی سیستم کلیک کنید",
        TipHidden = "نماد پنهان است؟ زیر {0} کنار ساعت را ببینید.",
        ShiftKey = "Shift",

        Starting = "در حال شروع…",
        PauseAutoLang = "توقف موقت AutoLang",
        ResumeAutoLang = "ازسرگیری AutoLang",
        ClickTrayIcon = "کلیک روی نماد",
        DisableForThisApp = "غیرفعال‌سازی برای این برنامه",
        DisableFor = "غیرفعال‌سازی برای {0}",
        CompatibilityForThisApp = "سازگاری برای این برنامه",
        CompatibilityFor = "سازگاری برای {0}",
        TypingStandard = "استاندارد",
        TypingStandardNote = "توصیه‌شده",
        TypingKeys = "فشردن کلیدها",
        TypingKeysNote = "برای بازی‌ها و میز کار راه دور",
        TypingPaste = "چسباندن",
        TypingPasteNote = "لحظه‌ای از کلیپ‌بورد استفاده می‌کند",
        TypingHintThisApp = "اگر اصلاح‌ها در این برنامه به‌هم‌ریخته درمی‌آیند، گزینه دیگری را امتحان کنید.",
        TypingHintFor = "اگر اصلاح‌ها در {0} به‌هم‌ریخته درمی‌آیند، گزینه دیگری را امتحان کنید.",
        SwitchTyped = "تبدیل آنچه همین حالا تایپ کردید",
        ShiftTwice = "دو بار Shift",
        SwitchTypedHow =
            "بلافاصله پس از تایپ، در کادر متن دو بار Shift را فشار دهید تا متن به طرح‌بندی دیگر تبدیل شود. " +
            "اگر بلافاصله پس از اصلاح یک واژه توسط AutoLang این کار را کنید، اصلاح لغو می‌شود. (کلید Pause هم لغو می‌کند و Shift+Pause تبدیل می‌کند.)",
        LanguagesMenu = "زبان‌ها…",
        SkipFullscreen = "نادیده گرفتن پنجره‌های تمام‌صفحه",
        StartWithWindows = "اجرا همراه با Windows",
        StartWithWindowsSettings = "اجرا همراه با Windows…",
        ShowLog = "نمایش گزارش",
        OpenLogFile = "باز کردن فایل گزارش",
        Exit = "خروج",

        StatePaused = "متوقف",
        StateIdle = "غیرفعال: به دو الفبا نیاز دارد",
        StateBlocked = "مسدود: {0}",
        StateWatching = "در حال پایش",
        CorrectionsThisSession = "اصلاح‌ها در این جلسه: {0}",
        ClickToResume = "برای ازسرگیری کلیک کنید",
        ClickToPause = "برای توقف کلیک کنید",
        NoLanguagesReady = "هیچ زبانی آماده نیست",
        WordListsDownloading = "در حال بارگیری فهرست‌های واژه: {0}",

        ReasonStarting = "در حال شروع",
        ReasonNoWindow = "بدون پنجره",
        ReasonPaused = "متوقف",
        ReasonAdmin = "پنجره سرپرست (Windows به AutoLang اجازه دسترسی نمی‌دهد)",
        ReasonOutOfReach = "برنامه در دسترس نیست",
        ReasonExcluded = "برنامه مستثنا",
        ReasonDisabled = "غیرفعال‌شده توسط شما",
        ReasonFullscreen = "پنجره تمام‌صفحه",
        ReasonPassword = "کادر گذرواژه",
        ReasonFailed = "بررسی فوکوس ناموفق بود",

        FirstFixTitle = "این کار AutoLang بود",
        FirstFixBody = "واژه‌ای را که با طرح‌بندی اشتباه صفحه‌کلید تایپ شده بود اصلاح کرد. برای لغو، بلافاصله دو بار Shift را فشار دهید.",
        AdminTitle = "AutoLang در این پنجره نمی‌تواند کمک کند",
        AdminBody =
            "{0} با دسترسی سرپرست اجرا می‌شود و Windows به برنامه‌های معمولی اجازه نمی‌دهد از آن بخوانند یا در آن تایپ کنند. AutoLang در همه جای دیگر به کارش ادامه می‌دهد.",
        PasteNotice =
            "اصلاح‌ها در {0} اکنون از طریق کلیپ‌بورد انجام می‌شوند. محتوای کپی‌شده شما بلافاصله پس از هر اصلاح بازگردانده می‌شود و هرگز نگه داشته یا به جایی فرستاده نمی‌شود.",
        StartupAppsNotice = "AutoLang را در فهرست برنامه‌های راه‌اندازی روشن یا خاموش کنید.",
        StartupChangeFailed = "تغییر تنظیم راه‌اندازی ممکن نشد.",

        LanguagesTitle = "AutoLang — زبان‌ها",
        LanguagesIntro =
            "AutoLang واژه‌هایی را که با الفبای اشتباه تایپ شده‌اند اصلاح و صفحه‌کلید را برایتان عوض می‌کند. بین صفحه‌کلیدهایی که الفبای مشترک دارند " +
            "(مثلاً انگلیسی و آلمانی) هرگز به‌طور خودکار جابه‌جا نمی‌شود؛ وقتی واژه‌ای به آن الفبا تعلق دارد، AutoLang " +
            "صفحه‌کلیدی را انتخاب می‌کند که آخرین بار از آن استفاده کردید.",
        ColumnKeyboard = "صفحه‌کلید",
        ColumnAlphabet = "الفبا",
        ColumnWordList = "فهرست واژه",
        ColumnUse = "استفاده",
        ColumnSwitchTo = "تغییر به آن",
        AutoDownload = "بارگیری خودکار فهرست‌های واژه ناموجود (تنها چیزی که AutoLang از اینترنت دریافت می‌کند)",
        WarnTwoAlphabets = "AutoLang غیرفعال است: دست‌کم به دو الفبای متفاوت در حال استفاده نیاز دارد که هر کدام فهرست واژه داشته باشد.",
        WarnNoTargets = "AutoLang غیرفعال است: همه زبان‌ها طوری تنظیم شده‌اند که هرگز به آن‌ها تغییر داده نشود.",
        ListReady = "آماده",
        ListReadyDownloaded = "آماده (بارگیری‌شده)",
        ListDownloading = "در حال بارگیری…",
        ListUnavailable = "در دسترس نیست",
        ListMissing = "ناموجود",
        ListDownloadLink = "{0} — بارگیری",
        ListErrorOffline = "بارگیری ناموفق بود (آفلاین هستید؟)",
        ListErrorInvalid = "فایل بارگیری‌شده فهرست واژه معتبری نبود",
        ListErrorUnreadable = "فهرست واژه خوانا نیست",
        WordListsCredit = "فهرست‌های واژه: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "لاتین", ["Greek"] = "یونانی", ["Cyrillic"] = "سیریلیک", ["Hebrew"] = "عبری",
            ["Arabic"] = "عربی", ["Armenian"] = "ارمنی", ["Georgian"] = "گرجی", ["Thai"] = "تایلندی",
            ["Devanagari"] = "دوناگری", ["Han"] = "چینی (هان)", ["Hangul"] = "کره‌ای (هانگول)",
        },

        LogTitle = "AutoLang — گزارش",
        CopyAll = "کپی همه",
        Refresh = "تازه‌سازی",
        LogEarlierOmitted = "… (سطرهای قبلی حذف شده‌اند)",
        LogEmpty = "(گزارش خالی است)",
        LogUnreadable = "(گزارش خوانده نشد: {0})",

        AlreadyRunning = "AutoLang از قبل در حال اجراست (نماد آن را در سینی سیستم پیدا کنید).",
        NeedsTwoAlphabetsLong =
            "AutoLang متنی را که با الفبای اشتباه تایپ شده اصلاح می‌کند، بنابراین به صفحه‌کلیدهایی برای دست‌کم دو الفبای متفاوت نیاز دارد " +
            "(مثلاً انگلیسی و عبری، یا انگلیسی و یونانی).",
        InstalledKeyboards = "نصب‌شده: {0}",
        HookFailed = "AutoLang نتوانست قلاب صفحه‌کلید خود را نصب کند.",
    };
}
