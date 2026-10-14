namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Russian() => new()
    {
        PausedTitle = "AutoLang приостановлен",
        ReadyingTitle = "AutoLang готовится к работе",
        IdleTitle = "AutoLang в режиме ожидания",
        OnTitle = "AutoLang включен",
        PausedHowToResume = "Щелкните значок в области уведомлений, чтобы возобновить",
        DownloadingWordLists = "Скачивание списков слов…",
        NeedsTwoAlphabets = "Нужны списки слов для двух алфавитов",
        OnWhatItDoes = "Исправляет слова, набранные не в той раскладке",
        Close = "Закрыть",

        WelcomeWhatExample = "Набрали текст не в той раскладке клавиатуры? AutoLang исправит слово и сам переключит раскладку:",
        WelcomeWhat = "Набрали текст не в той раскладке клавиатуры? AutoLang исправит слово и сам переключит раскладку.",
        WelcomePausedSubtitle = "Ничего не исправляется, пока вы не возобновите работу",
        WelcomeOnSubtitle = "Работает в фоновом режиме",
        WelcomeIdleBody = "Откройте «Языки…» в меню значка в области уведомлений и выберите клавиатуры, с которыми будет работать AutoLang.",
        Resume = "Возобновить",
        GotIt = "Понятно",
        TipUndo = "Нажмите {0} дважды, чтобы отменить исправление",
        TipPause = "Щелкните {0} в области уведомлений для паузы",
        TipHidden = "Значок скрыт? Найдите его под {0} рядом с часами.",
        ShiftKey = "Shift",

        Starting = "Запуск…",
        PauseAutoLang = "Приостановить AutoLang",
        ResumeAutoLang = "Возобновить AutoLang",
        ClickTrayIcon = "Щелчок по значку",
        DisableForThisApp = "Отключить для этого приложения",
        DisableFor = "Отключить для {0}",
        CompatibilityForThisApp = "Совместимость с этим приложением",
        CompatibilityFor = "Совместимость с {0}",
        TypingStandard = "Стандартный",
        TypingStandardNote = "Рекомендуется",
        TypingKeys = "Нажатия клавиш",
        TypingKeysNote = "Для игр и удаленных рабочих столов",
        TypingPaste = "Вставка",
        TypingPasteNote = "Ненадолго использует буфер обмена",
        TypingHintThisApp = "Если исправления в этом приложении искажаются, попробуйте другой вариант.",
        TypingHintFor = "Если исправления в {0} искажаются, попробуйте другой вариант.",
        SwitchTyped = "Переключить только что набранное",
        ShiftTwice = "Shift дважды",
        SwitchTypedHow =
            "Дважды нажмите Shift в поле ввода сразу после набора, чтобы перевести текст в другую раскладку. " +
            "Сразу после того как AutoLang исправит слово, это отменяет исправление. (Клавиша Pause тоже отменяет, а Shift+Pause преобразует.)",
        LanguagesMenu = "Языки…",
        SkipFullscreen = "Пропускать полноэкранные окна",
        StartWithWindows = "Запускать вместе с Windows",
        StartWithWindowsSettings = "Запускать вместе с Windows…",
        ShowLog = "Показать журнал",
        OpenLogFile = "Открыть файл журнала",
        Exit = "Выход",

        StatePaused = "Приостановлен",
        StateIdle = "Ожидание: нужны два алфавита",
        StateBlocked = "Заблокирован: {0}",
        StateWatching = "Отслеживает ввод",
        CorrectionsThisSession = "Исправлений за сеанс: {0}",
        ClickToResume = "Щелкните, чтобы возобновить",
        ClickToPause = "Щелкните, чтобы приостановить",
        NoLanguagesReady = "Нет готовых языков",
        WordListsDownloading = "скачиваются списки слов: {0}",

        ReasonStarting = "запуск",
        ReasonNoWindow = "нет окна",
        ReasonPaused = "приостановлен",
        ReasonAdmin = "окно администратора (Windows не пускает в него AutoLang)",
        ReasonOutOfReach = "приложение недоступно",
        ReasonExcluded = "приложение в исключениях",
        ReasonDisabled = "отключено вами",
        ReasonFullscreen = "полноэкранное окно",
        ReasonPassword = "поле пароля",
        ReasonFailed = "ошибка проверки фокуса",

        FirstFixTitle = "Это был AutoLang",
        FirstFixBody = "Он исправил слово, набранное не в той раскладке клавиатуры. Чтобы отменить, сразу дважды нажмите Shift.",
        AdminTitle = "AutoLang не может помочь в этом окне",
        AdminBody =
            "Приложение {0} запущено от имени администратора, и Windows не позволяет обычным приложениям считывать из него текст или вводить его. AutoLang продолжает работать во всех остальных местах.",
        PasteNotice =
            "Исправления в {0} теперь выполняются через буфер обмена. Скопированное вами содержимое возвращается сразу после каждого исправления и никогда не сохраняется и никуда не отправляется.",
        StartupAppsNotice = "Включите или отключите AutoLang в списке приложений автозагрузки.",
        StartupChangeFailed = "Не удалось изменить параметр автозагрузки.",

        LanguagesTitle = "AutoLang — Языки",
        LanguagesIntro =
            "AutoLang исправляет слова, набранные не тем алфавитом, и сам переключает клавиатуру. Между клавиатурами с общим алфавитом " +
            "(например, английской и немецкой) автоматическое переключение не выполняется; если слово относится к этому алфавиту, AutoLang " +
            "выбирает ту клавиатуру, которой вы пользовались последней.",
        ColumnKeyboard = "Клавиатура",
        ColumnAlphabet = "Алфавит",
        ColumnWordList = "Список слов",
        ColumnUse = "Использовать",
        ColumnSwitchTo = "Переключать на нее",
        AutoDownload = "Автоматически скачивать недостающие списки слов (больше AutoLang ничего не скачивает)",
        WarnTwoAlphabets = "AutoLang в режиме ожидания: нужно использовать хотя бы два разных алфавита, каждый со списком слов.",
        WarnNoTargets = "AutoLang в режиме ожидания: ни для одного языка не разрешено переключение на него.",
        ListReady = "Готов",
        ListReadyDownloaded = "Готов (скачан)",
        ListDownloading = "Скачивание…",
        ListUnavailable = "Недоступен",
        ListMissing = "Отсутствует",
        ListDownloadLink = "{0} — скачать",
        ListErrorOffline = "ошибка скачивания (нет сети?)",
        ListErrorInvalid = "скачанный файл не является списком слов",
        ListErrorUnreadable = "не удается прочитать список слов",
        WordListsCredit = "Списки слов: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Латиница", ["Greek"] = "Греческий", ["Cyrillic"] = "Кириллица", ["Hebrew"] = "Иврит",
            ["Arabic"] = "Арабский", ["Armenian"] = "Армянский", ["Georgian"] = "Грузинский", ["Thai"] = "Тайский",
            ["Devanagari"] = "Деванагари", ["Han"] = "Китайские иероглифы", ["Hangul"] = "Хангыль",
        },

        LogTitle = "AutoLang — Журнал",
        CopyAll = "Копировать все",
        Refresh = "Обновить",
        LogEarlierOmitted = "… (более ранние строки пропущены)",
        LogEmpty = "(журнал пуст)",
        LogUnreadable = "(не удалось прочитать журнал: {0})",

        AlreadyRunning = "AutoLang уже запущен (найдите его значок в области уведомлений).",
        NeedsTwoAlphabetsLong =
            "AutoLang исправляет текст, набранный не тем алфавитом, поэтому ему нужны клавиатуры хотя бы для двух разных алфавитов " +
            "(например, для английского и иврита или для английского и греческого).",
        InstalledKeyboards = "Установлены: {0}",
        HookFailed = "AutoLang не удалось установить перехватчик клавиатуры.",
    };
}
