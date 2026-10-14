namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Spanish() => new()
    {
        PausedTitle = "AutoLang está en pausa",
        ReadyingTitle = "AutoLang se está preparando",
        IdleTitle = "AutoLang está inactivo",
        OnTitle = "AutoLang está activado",
        PausedHowToResume = "Haz clic en el icono del área de notificación para reanudar",
        DownloadingWordLists = "Descargando listas de palabras…",
        NeedsTwoAlphabets = "Necesita listas de palabras de dos alfabetos",
        OnWhatItDoes = "Corrige palabras escritas con la distribución equivocada",
        Close = "Cerrar",

        WelcomeWhatExample = "¿Has escrito con la distribución de teclado equivocada? AutoLang corrige la palabra y cambia la distribución por ti:",
        WelcomeWhat = "¿Has escrito con la distribución de teclado equivocada? AutoLang corrige la palabra y cambia la distribución por ti.",
        WelcomePausedSubtitle = "No se corrige nada hasta que reanudes",
        WelcomeOnSubtitle = "Se ejecuta en segundo plano",
        WelcomeIdleBody = "Abre Idiomas… desde el menú del área de notificación para elegir con qué teclados funciona AutoLang.",
        Resume = "Reanudar",
        GotIt = "Entendido",
        TipUndo = "Pulsa {0} dos veces para deshacer una corrección",
        TipPause = "Haz clic en {0} en el área de notificación para pausar",
        TipHidden = "¿Icono oculto? Búscalo bajo {0} junto al reloj.",
        ShiftKey = "Mayús",

        Starting = "Iniciando…",
        PauseAutoLang = "Pausar AutoLang",
        ResumeAutoLang = "Reanudar AutoLang",
        ClickTrayIcon = "Clic en el icono",
        DisableForThisApp = "Desactivar para esta aplicación",
        DisableFor = "Desactivar para {0}",
        CompatibilityForThisApp = "Compatibilidad con esta aplicación",
        CompatibilityFor = "Compatibilidad con {0}",
        TypingStandard = "Estándar",
        TypingStandardNote = "Recomendado",
        TypingKeys = "Pulsaciones de teclas",
        TypingKeysNote = "Para juegos y escritorios remotos",
        TypingPaste = "Pegar",
        TypingPasteNote = "Usa el portapapeles un momento",
        TypingHintThisApp = "Prueba otra opción si las correcciones salen con caracteres extraños en esta aplicación.",
        TypingHintFor = "Prueba otra opción si las correcciones salen con caracteres extraños en {0}.",
        SwitchTyped = "Cambiar lo que acabas de escribir",
        ShiftTwice = "Mayús dos veces",
        SwitchTypedHow =
            "Pulsa Mayús dos veces en el campo de texto, justo después de escribir, para pasar el texto a la otra distribución. " +
            "Justo después de que AutoLang corrija una palabra, deshace la corrección. (La tecla Pausa también deshace, y Mayús+Pausa convierte).",
        LanguagesMenu = "Idiomas…",
        LearntWordsMenu = "Palabras aprendidas",
        LearntWordsHint = "Haga clic en una palabra para que vuelva a corregirse.",
        ClearAll = "Borrar todo",
        SkipFullscreen = "Omitir ventanas en pantalla completa",
        StartWithWindows = "Iniciar con Windows",
        StartWithWindowsSettings = "Iniciar con Windows…",
        ShowLog = "Mostrar registro",
        OpenLogFile = "Abrir archivo de registro",
        Exit = "Salir",

        StatePaused = "En pausa",
        StateIdle = "Inactivo: necesita dos alfabetos",
        StateBlocked = "Bloqueado: {0}",
        StateWatching = "Activo",
        CorrectionsThisSession = "Correcciones en esta sesión: {0}",
        ClickToResume = "Haz clic para reanudar",
        ClickToPause = "Haz clic para pausar",
        NoLanguagesReady = "Ningún idioma listo",
        WordListsDownloading = "descargando listas de palabras: {0}",

        ReasonStarting = "iniciando",
        ReasonNoWindow = "sin ventana",
        ReasonPaused = "en pausa",
        ReasonAdmin = "ventana de administrador (Windows no deja entrar a AutoLang)",
        ReasonOutOfReach = "aplicación fuera de alcance",
        ReasonExcluded = "aplicación excluida",
        ReasonDisabled = "desactivado por ti",
        ReasonFullscreen = "ventana en pantalla completa",
        ReasonPassword = "campo de contraseña",
        ReasonFailed = "error al comprobar el foco",

        FirstFixTitle = "Ha sido AutoLang",
        FirstFixBody = "Ha corregido una palabra escrita con la distribución de teclado equivocada. Para deshacerlo, pulsa Mayús dos veces enseguida.",
        AdminTitle = "AutoLang no puede ayudar en esta ventana",
        AdminBody =
            "{0} se ejecuta como administrador y Windows no permite que las aplicaciones normales lean ni escriban en esa ventana. AutoLang sigue funcionando en todas las demás.",
        PasteNotice =
            "Las correcciones en {0} ahora pasan por el portapapeles. Lo que hayas copiado se restaura justo después de cada una y nunca se guarda ni se envía a ningún sitio.",
        StartupAppsNotice = "Activa o desactiva AutoLang en la lista de aplicaciones de arranque.",
        StartupChangeFailed = "No se pudo cambiar la configuración de inicio.",

        LanguagesTitle = "AutoLang — Idiomas",
        LanguagesIntro =
            "AutoLang corrige palabras escritas en el alfabeto equivocado y cambia el teclado por ti. Entre teclados que comparten alfabeto " +
            "(por ejemplo, inglés y alemán) nunca se cambia automáticamente; cuando una palabra pertenece a ese alfabeto, AutoLang " +
            "elige el último que usaste.",
        ColumnKeyboard = "Teclado",
        ColumnAlphabet = "Alfabeto",
        ColumnWordList = "Lista de palabras",
        ColumnUse = "Usar",
        ColumnSwitchTo = "Cambiar a este",
        AutoDownload = "Descargar automáticamente las listas de palabras que falten (lo único que AutoLang descarga)",
        WarnTwoAlphabets = "AutoLang está inactivo: necesita al menos dos alfabetos distintos en uso, cada uno con una lista de palabras.",
        WarnNoTargets = "AutoLang está inactivo: todos los idiomas están configurados para no cambiar nunca a ellos.",
        ListReady = "Lista",
        ListReadyDownloaded = "Lista (descargada)",
        ListDownloading = "Descargando…",
        ListUnavailable = "No disponible",
        ListMissing = "Falta",
        ListDownloadLink = "{0} — descargar",
        ListErrorOffline = "error en la descarga (¿sin conexión?)",
        ListErrorInvalid = "lo descargado no es una lista de palabras válida",
        ListErrorUnreadable = "lista de palabras ilegible",
        WordListsCredit = "Listas de palabras: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Latino", ["Greek"] = "Griego", ["Cyrillic"] = "Cirílico", ["Hebrew"] = "Hebreo",
            ["Arabic"] = "Árabe", ["Armenian"] = "Armenio", ["Georgian"] = "Georgiano", ["Thai"] = "Tailandés",
            ["Devanagari"] = "Devanagari", ["Han"] = "Han", ["Hangul"] = "Hangul",
        },

        LogTitle = "AutoLang — Registro",
        CopyAll = "Copiar todo",
        Refresh = "Actualizar",
        LogEarlierOmitted = "… (líneas anteriores omitidas)",
        LogEmpty = "(el registro está vacío)",
        LogUnreadable = "(no se pudo leer el registro: {0})",

        AlreadyRunning = "AutoLang ya se está ejecutando (busca el icono en el área de notificación).",
        NeedsTwoAlphabetsLong =
            "AutoLang corrige texto escrito en el alfabeto equivocado, así que necesita teclados de al menos dos alfabetos distintos " +
            "(por ejemplo, inglés y hebreo, o inglés y griego).",
        InstalledKeyboards = "Instalados: {0}",
        HookFailed = "AutoLang no pudo instalar su enlace de teclado.",
    };
}
