namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Ukrainian() => new()
    {
        PausedTitle = "AutoLang призупинено",
        ReadyingTitle = "AutoLang готується до роботи",
        IdleTitle = "AutoLang у режимі очікування",
        OnTitle = "AutoLang увімкнуто",
        PausedHowToResume = "Клацніть значок в області сповіщень, щоб відновити",
        DownloadingWordLists = "Завантаження списків слів…",
        NeedsTwoAlphabets = "Потрібні списки слів для двох алфавітів",
        OnWhatItDoes = "Виправляє слова, набрані не в тій розкладці",
        Close = "Закрити",

        WelcomeWhatExample = "Набрали текст не в тій розкладці клавіатури? AutoLang виправить слово й сам перемкне розкладку:",
        WelcomeWhat = "Набрали текст не в тій розкладці клавіатури? AutoLang виправить слово й сам перемкне розкладку.",
        WelcomePausedSubtitle = "Нічого не виправляється, доки ви не відновите роботу",
        WelcomeOnSubtitle = "Працює у фоновому режимі",
        WelcomeIdleBody = "Відкрийте «Мови…» в меню значка в області сповіщень, щоб вибрати клавіатури, з якими працюватиме AutoLang.",
        Resume = "Відновити",
        GotIt = "Зрозуміло",
        TipUndo = "Натисніть {0} двічі, щоб скасувати виправлення",
        TipPause = "Клацніть {0} в області сповіщень, щоб призупинити",
        TipHidden = "Значок приховано? Шукайте під {0} біля годинника.",
        ShiftKey = "Shift",

        Starting = "Запуск…",
        PauseAutoLang = "Призупинити AutoLang",
        ResumeAutoLang = "Відновити AutoLang",
        ClickTrayIcon = "Клацання значка",
        DisableForThisApp = "Вимкнути для цієї програми",
        DisableFor = "Вимкнути для {0}",
        CompatibilityForThisApp = "Сумісність із цією програмою",
        CompatibilityFor = "Сумісність із {0}",
        TypingStandard = "Стандартний",
        TypingStandardNote = "Рекомендовано",
        TypingKeys = "Натискання клавіш",
        TypingKeysNote = "Для ігор і віддалених робочих столів",
        TypingPaste = "Вставлення",
        TypingPasteNote = "Ненадовго використовує буфер обміну",
        TypingHintThisApp = "Якщо виправлення в цій програмі спотворюються, спробуйте інший варіант.",
        TypingHintFor = "Якщо виправлення в {0} спотворюються, спробуйте інший варіант.",
        SwitchTyped = "Перемкнути щойно набране",
        ShiftTwice = "Shift двічі",
        SwitchTypedHow =
            "Двічі натисніть Shift у текстовому полі одразу після введення, щоб перевести текст в іншу розкладку. " +
            "Одразу після того, як AutoLang виправить слово, це скасовує виправлення. (Клавіша Pause теж скасовує, а Shift+Pause перетворює.)",
        LanguagesMenu = "Мови…",
        LearntWordsMenu = "Вивчені слова",
        LearntWordsHint = "Натисніть слово, щоб воно знову виправлялося.",
        ClearAll = "Очистити все",
        SkipFullscreen = "Пропускати повноекранні вікна",
        StartWithWindows = "Запускати разом із Windows",
        StartWithWindowsSettings = "Запускати разом із Windows…",
        ShowLog = "Показати журнал",
        OpenLogFile = "Відкрити файл журналу",
        Exit = "Вихід",

        StatePaused = "Призупинено",
        StateIdle = "Очікування: потрібні два алфавіти",
        StateBlocked = "Заблоковано: {0}",
        StateWatching = "Стежить за введенням",
        CorrectionsThisSession = "Виправлень за сеанс: {0}",
        ClickToResume = "Клацніть, щоб відновити",
        ClickToPause = "Клацніть, щоб призупинити",
        NoLanguagesReady = "Немає готових мов",
        WordListsDownloading = "завантажуються списки слів: {0}",

        ReasonStarting = "запуск",
        ReasonNoWindow = "немає вікна",
        ReasonPaused = "призупинено",
        ReasonAdmin = "вікно адміністратора (Windows не пускає в нього AutoLang)",
        ReasonOutOfReach = "програма недоступна",
        ReasonExcluded = "програма у винятках",
        ReasonDisabled = "вимкнуто вами",
        ReasonFullscreen = "повноекранне вікно",
        ReasonPassword = "поле пароля",
        ReasonFailed = "помилка перевірки фокуса",

        FirstFixTitle = "Це був AutoLang",
        FirstFixBody = "Він виправив слово, набране не в тій розкладці клавіатури. Щоб скасувати, одразу двічі натисніть Shift.",
        AdminTitle = "AutoLang не може допомогти в цьому вікні",
        AdminBody =
            "Програму {0} запущено від імені адміністратора, і Windows не дозволяє звичайним програмам читати з неї або вводити в неї текст. AutoLang і далі працює всюди інде.",
        PasteNotice =
            "Виправлення в {0} тепер виконуються через буфер обміну. Скопійований вами вміст повертається одразу після кожного виправлення й ніколи не зберігається та нікуди не надсилається.",
        StartupAppsNotice = "Увімкніть або вимкніть AutoLang у списку програм автозавантаження.",
        StartupChangeFailed = "Не вдалося змінити параметр автозавантаження.",

        LanguagesTitle = "AutoLang — Мови",
        LanguagesIntro =
            "AutoLang виправляє слова, набрані не тим алфавітом, і сам перемикає клавіатуру. Між клавіатурами зі спільним алфавітом " +
            "(наприклад, англійською та німецькою) автоматичне перемикання не виконується; коли слово належить до цього алфавіту, AutoLang " +
            "вибирає ту клавіатуру, якою ви користувалися останньою.",
        ColumnKeyboard = "Клавіатура",
        ColumnAlphabet = "Алфавіт",
        ColumnWordList = "Список слів",
        ColumnUse = "Використовувати",
        ColumnSwitchTo = "Перемикати на неї",
        AutoDownload = "Автоматично завантажувати відсутні списки слів (більше AutoLang нічого не завантажує)",
        WarnTwoAlphabets = "AutoLang у режимі очікування: потрібно використовувати щонайменше два різні алфавіти, кожен зі списком слів.",
        WarnNoTargets = "AutoLang у режимі очікування: для жодної мови не дозволено перемикання на неї.",
        ListReady = "Готово",
        ListReadyDownloaded = "Готово (завантажено)",
        ListDownloading = "Завантаження…",
        ListUnavailable = "Недоступно",
        ListMissing = "Немає",
        ListDownloadLink = "{0} — завантажити",
        ListErrorOffline = "помилка завантаження (немає мережі?)",
        ListErrorInvalid = "завантажений файл не є списком слів",
        ListErrorUnreadable = "не вдається прочитати список слів",
        WordListsCredit = "Списки слів: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Латиниця", ["Greek"] = "Грецький", ["Cyrillic"] = "Кирилиця", ["Hebrew"] = "Іврит",
            ["Arabic"] = "Арабський", ["Armenian"] = "Вірменський", ["Georgian"] = "Грузинський", ["Thai"] = "Тайський",
            ["Devanagari"] = "Деванагарі", ["Han"] = "Китайські ієрогліфи", ["Hangul"] = "Хангиль",
        },

        LogTitle = "AutoLang — Журнал",
        CopyAll = "Копіювати все",
        Refresh = "Оновити",
        LogEarlierOmitted = "… (попередні рядки пропущено)",
        LogEmpty = "(журнал порожній)",
        LogUnreadable = "(не вдалося прочитати журнал: {0})",

        AlreadyRunning = "AutoLang уже запущено (шукайте значок в області сповіщень).",
        NeedsTwoAlphabetsLong =
            "AutoLang виправляє текст, набраний не тим алфавітом, тому йому потрібні клавіатури щонайменше для двох різних алфавітів " +
            "(наприклад, для англійської та івриту або для англійської та грецької).",
        InstalledKeyboards = "Установлено: {0}",
        HookFailed = "AutoLang не вдалося встановити перехоплювач клавіатури.",
    };
}
