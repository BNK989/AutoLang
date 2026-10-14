namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Bulgarian() => new()
    {
        PausedTitle = "AutoLang е на пауза",
        ReadyingTitle = "AutoLang се подготвя",
        IdleTitle = "AutoLang е неактивен",
        OnTitle = "AutoLang е включен",
        PausedHowToResume = "Щракнете иконата в областта за уведомяване, за да възобновите",
        DownloadingWordLists = "Изтегляне на списъци с думи…",
        NeedsTwoAlphabets = "Нужни са списъци с думи за две азбуки",
        OnWhatItDoes = "Поправя думи, въведени с грешна подредба",
        Close = "Затвори",

        WelcomeWhatExample = "Написахте нещо с грешна клавиатурна подредба? AutoLang поправя думата и сменя подредбата вместо вас:",
        WelcomeWhat = "Написахте нещо с грешна клавиатурна подредба? AutoLang поправя думата и сменя подредбата вместо вас.",
        WelcomePausedSubtitle = "Нищо не се поправя, докато не възобновите",
        WelcomeOnSubtitle = "Работи във фонов режим",
        WelcomeIdleBody = "Отворете „Езици…“ от менюто в областта за уведомяване, за да изберете с кои клавиатури да работи AutoLang.",
        Resume = "Възобнови",
        GotIt = "Разбрах",
        TipUndo = "Натиснете {0} два пъти, за да отмените поправка",
        TipPause = "Щракнете {0} в областта за уведомяване за пауза",
        TipHidden = "Иконата е скрита? Потърсете под {0} до часовника.",
        ShiftKey = "Shift",

        Starting = "Стартиране…",
        PauseAutoLang = "Постави AutoLang на пауза",
        ResumeAutoLang = "Възобнови AutoLang",
        ClickTrayIcon = "Щракване на иконата",
        DisableForThisApp = "Изключи за това приложение",
        DisableFor = "Изключи за {0}",
        CompatibilityForThisApp = "Съвместимост за това приложение",
        CompatibilityFor = "Съвместимост за {0}",
        TypingStandard = "Стандартно",
        TypingStandardNote = "Препоръчително",
        TypingKeys = "Натискания на клавиши",
        TypingKeysNote = "За игри и отдалечени работни плотове",
        TypingPaste = "Поставяне",
        TypingPasteNote = "Използва за момент клипборда",
        TypingHintThisApp = "Опитайте друга опция, ако поправките в това приложение излизат изкривени.",
        TypingHintFor = "Опитайте друга опция, ако поправките в {0} излизат изкривени.",
        SwitchTyped = "Превключи току-що написаното",
        ShiftTwice = "Shift два пъти",
        SwitchTypedHow =
            "Натиснете Shift два пъти в текстовото поле веднага след въвеждане, за да превключите текста към другата подредба. " +
            "Веднага след като AutoLang поправи дума, това отменя поправката. (Клавишът Pause също отменя, а Shift+Pause преобразува.)",
        LanguagesMenu = "Езици…",
        SkipFullscreen = "Пропускай прозорците на цял екран",
        StartWithWindows = "Стартирай с Windows",
        StartWithWindowsSettings = "Стартирай с Windows…",
        ShowLog = "Покажи дневника",
        OpenLogFile = "Отвори файла на дневника",
        Exit = "Изход",

        StatePaused = "На пауза",
        StateIdle = "Неактивен: нужни са две азбуки",
        StateBlocked = "Блокиран: {0}",
        StateWatching = "Следи въвеждането",
        CorrectionsThisSession = "Поправки в тази сесия: {0}",
        ClickToResume = "Щракнете, за да възобновите",
        ClickToPause = "Щракнете за пауза",
        NoLanguagesReady = "Няма готови езици",
        WordListsDownloading = "изтегляне на списъци с думи: {0}",

        ReasonStarting = "стартиране",
        ReasonNoWindow = "няма прозорец",
        ReasonPaused = "на пауза",
        ReasonAdmin = "прозорец с администраторски права (Windows не допуска AutoLang)",
        ReasonOutOfReach = "приложението е недостъпно",
        ReasonExcluded = "приложение в изключенията",
        ReasonDisabled = "изключено от вас",
        ReasonFullscreen = "прозорец на цял екран",
        ReasonPassword = "поле за парола",
        ReasonFailed = "неуспешна проверка на фокуса",

        FirstFixTitle = "Това беше AutoLang",
        FirstFixBody = "Той поправи дума, въведена с грешна клавиатурна подредба. За да отмените, веднага натиснете Shift два пъти.",
        AdminTitle = "AutoLang не може да помогне в този прозорец",
        AdminBody =
            "Приложението {0} се изпълнява като администратор и Windows не позволява на обикновените приложения да четат от него или да въвеждат текст в него. AutoLang продължава да работи навсякъде другаде.",
        PasteNotice =
            "Поправките в {0} вече минават през клипборда. Копираното от вас съдържание се връща веднага след всяка поправка и никога не се запазва и не се изпраща никъде.",
        StartupAppsNotice = "Включете или изключете AutoLang в списъка с приложения при стартиране.",
        StartupChangeFailed = "Настройката за стартиране не можа да бъде променена.",

        LanguagesTitle = "AutoLang — Езици",
        LanguagesIntro =
            "AutoLang поправя думи, въведени с грешна азбука, и превключва клавиатурата вместо вас. Между клавиатури с обща азбука " +
            "(например английска и немска) никога не се превключва автоматично; когато дадена дума е от тази азбука, AutoLang " +
            "избира клавиатурата, която сте използвали последно.",
        ColumnKeyboard = "Клавиатура",
        ColumnAlphabet = "Азбука",
        ColumnWordList = "Списък с думи",
        ColumnUse = "Използване",
        ColumnSwitchTo = "Превключване към нея",
        AutoDownload = "Автоматично изтегляне на липсващи списъци с думи (единственото, което AutoLang изтегля)",
        WarnTwoAlphabets = "AutoLang е неактивен: нужни са поне две различни използвани азбуки, всяка със списък с думи.",
        WarnNoTargets = "AutoLang е неактивен: за нито един език не е разрешено превключване към него.",
        ListReady = "Готов",
        ListReadyDownloaded = "Готов (изтеглен)",
        ListDownloading = "Изтегляне…",
        ListUnavailable = "Не е наличен",
        ListMissing = "Липсва",
        ListDownloadLink = "{0} — изтегляне",
        ListErrorOffline = "неуспешно изтегляне (няма връзка?)",
        ListErrorInvalid = "изтегленият файл не е валиден списък с думи",
        ListErrorUnreadable = "списъкът с думи не може да бъде прочетен",
        WordListsCredit = "Списъци с думи: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Латиница", ["Greek"] = "Гръцка", ["Cyrillic"] = "Кирилица", ["Hebrew"] = "Иврит",
            ["Arabic"] = "Арабска", ["Armenian"] = "Арменска", ["Georgian"] = "Грузинска", ["Thai"] = "Тайландска",
            ["Devanagari"] = "Деванагари", ["Han"] = "Китайски йероглифи", ["Hangul"] = "Хангъл",
        },

        LogTitle = "AutoLang — Дневник",
        CopyAll = "Копирай всичко",
        Refresh = "Обнови",
        LogEarlierOmitted = "… (по-ранните редове са пропуснати)",
        LogEmpty = "(дневникът е празен)",
        LogUnreadable = "(дневникът не можа да бъде прочетен: {0})",

        AlreadyRunning = "AutoLang вече работи (потърсете иконата в областта за уведомяване).",
        NeedsTwoAlphabetsLong =
            "AutoLang поправя текст, въведен с грешна азбука, затова се нуждае от клавиатури за поне две различни азбуки " +
            "(например английски и иврит или английски и гръцки).",
        InstalledKeyboards = "Инсталирани: {0}",
        HookFailed = "AutoLang не можа да инсталира своята кука за клавиатурата.",
    };
}
