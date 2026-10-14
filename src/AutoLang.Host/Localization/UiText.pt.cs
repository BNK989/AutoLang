namespace AutoLang.Host;

internal sealed partial record UiText
{
    private static UiText Portuguese() => new()
    {
        PausedTitle = "O AutoLang está pausado",
        ReadyingTitle = "O AutoLang está se preparando",
        IdleTitle = "O AutoLang está inativo",
        OnTitle = "O AutoLang está ativado",
        PausedHowToResume = "Clique no ícone na área de notificação para retomar",
        DownloadingWordLists = "Baixando listas de palavras…",
        NeedsTwoAlphabets = "Precisa de listas de palavras para dois alfabetos",
        OnWhatItDoes = "Corrigindo palavras digitadas no layout errado",
        Close = "Fechar",

        WelcomeWhatExample = "Digitou no layout de teclado errado? O AutoLang corrige a palavra e muda o layout para você:",
        WelcomeWhat = "Digitou no layout de teclado errado? O AutoLang corrige a palavra e muda o layout para você.",
        WelcomePausedSubtitle = "Nada é corrigido até você retomar",
        WelcomeOnSubtitle = "Executando em segundo plano",
        WelcomeIdleBody = "Abra Idiomas… no menu da área de notificação para escolher com quais teclados o AutoLang funciona.",
        Resume = "Retomar",
        GotIt = "Entendi",
        TipUndo = "Pressione {0} duas vezes para desfazer uma correção",
        TipPause = "Clique em {0} na área de notificação para pausar",
        TipHidden = "Ícone oculto? Procure em {0} ao lado do relógio.",
        ShiftKey = "Shift",

        Starting = "Iniciando…",
        PauseAutoLang = "Pausar o AutoLang",
        ResumeAutoLang = "Retomar o AutoLang",
        ClickTrayIcon = "Clique no ícone",
        DisableForThisApp = "Desativar para este aplicativo",
        DisableFor = "Desativar para {0}",
        CompatibilityForThisApp = "Compatibilidade com este aplicativo",
        CompatibilityFor = "Compatibilidade com {0}",
        TypingStandard = "Padrão",
        TypingStandardNote = "Recomendado",
        TypingKeys = "Pressionamentos de tecla",
        TypingKeysNote = "Para jogos e áreas de trabalho remotas",
        TypingPaste = "Colar",
        TypingPasteNote = "Usa a Área de Transferência por um instante",
        TypingHintThisApp = "Tente outra opção se as correções saírem embaralhadas neste aplicativo.",
        TypingHintFor = "Tente outra opção se as correções saírem embaralhadas em {0}.",
        SwitchTyped = "Converter o que você acabou de digitar",
        ShiftTwice = "Shift duas vezes",
        SwitchTypedHow =
            "Pressione Shift duas vezes no campo de texto, logo após digitar, para passar o texto para o outro layout. " +
            "Logo depois que o AutoLang corrige uma palavra, isso desfaz a correção. (A tecla Pause também desfaz, e Shift+Pause converte.)",
        LanguagesMenu = "Idiomas…",
        LearntWordsMenu = "Palavras aprendidas",
        LearntWordsHint = "Clique em uma palavra para que ela volte a ser corrigida.",
        ClearAll = "Limpar tudo",
        SkipFullscreen = "Ignorar janelas em tela cheia",
        StartWithWindows = "Iniciar com o Windows",
        StartWithWindowsSettings = "Iniciar com o Windows…",
        ShowLog = "Mostrar log",
        OpenLogFile = "Abrir arquivo de log",
        Exit = "Sair",

        StatePaused = "Pausado",
        StateIdle = "Inativo: precisa de dois alfabetos",
        StateBlocked = "Bloqueado: {0}",
        StateWatching = "Ativo",
        CorrectionsThisSession = "Correções nesta sessão: {0}",
        ClickToResume = "Clique para retomar",
        ClickToPause = "Clique para pausar",
        NoLanguagesReady = "Nenhum idioma pronto",
        WordListsDownloading = "baixando listas de palavras: {0}",

        ReasonStarting = "iniciando",
        ReasonNoWindow = "nenhuma janela",
        ReasonPaused = "pausado",
        ReasonAdmin = "janela de administrador (o Windows bloqueia o AutoLang)",
        ReasonOutOfReach = "aplicativo fora de alcance",
        ReasonExcluded = "aplicativo excluído",
        ReasonDisabled = "desativado por você",
        ReasonFullscreen = "janela em tela cheia",
        ReasonPassword = "campo de senha",
        ReasonFailed = "falha na verificação de foco",

        FirstFixTitle = "Isso foi o AutoLang",
        FirstFixBody = "Ele corrigiu uma palavra digitada no layout de teclado errado. Para desfazer, pressione Shift duas vezes imediatamente.",
        AdminTitle = "O AutoLang não pode ajudar nesta janela",
        AdminBody =
            "{0} está sendo executado como administrador, e o Windows não permite que aplicativos comuns leiam ou digitem nele. O AutoLang continua funcionando em todos os outros lugares.",
        PasteNotice =
            "As correções em {0} agora passam pela Área de Transferência. O conteúdo que você copiou é restaurado logo após cada uma e nunca é guardado nem enviado a lugar nenhum.",
        StartupAppsNotice = "Ative ou desative o AutoLang na lista de aplicativos de inicialização.",
        StartupChangeFailed = "Não foi possível alterar a configuração de inicialização.",

        LanguagesTitle = "AutoLang — Idiomas",
        LanguagesIntro =
            "O AutoLang corrige palavras digitadas no alfabeto errado e troca o teclado para você. Teclados que compartilham um alfabeto " +
            "(inglês e alemão, por exemplo) nunca são trocados entre si automaticamente; quando uma palavra pertence a esse alfabeto, o AutoLang " +
            "escolhe o último que você usou.",
        ColumnKeyboard = "Teclado",
        ColumnAlphabet = "Alfabeto",
        ColumnWordList = "Lista de palavras",
        ColumnUse = "Usar",
        ColumnSwitchTo = "Mudar para ele",
        AutoDownload = "Baixar automaticamente as listas de palavras que faltam (a única coisa que o AutoLang baixa)",
        WarnTwoAlphabets = "O AutoLang está inativo: ele precisa de pelo menos dois alfabetos diferentes em uso, cada um com uma lista de palavras.",
        WarnNoTargets = "O AutoLang está inativo: todos os idiomas estão configurados para nunca mudar para eles.",
        ListReady = "Pronta",
        ListReadyDownloaded = "Pronta (baixada)",
        ListDownloading = "Baixando…",
        ListUnavailable = "Indisponível",
        ListMissing = "Ausente",
        ListDownloadLink = "{0} — baixar",
        ListErrorOffline = "falha no download (sem conexão?)",
        ListErrorInvalid = "o download não é uma lista de palavras válida",
        ListErrorUnreadable = "lista de palavras ilegível",
        WordListsCredit = "Listas de palavras: {0}",

        Scripts = new Dictionary<string, string>
        {
            ["Latin"] = "Latino", ["Greek"] = "Grego", ["Cyrillic"] = "Cirílico", ["Hebrew"] = "Hebraico",
            ["Arabic"] = "Árabe", ["Armenian"] = "Armênio", ["Georgian"] = "Georgiano", ["Thai"] = "Tailandês",
            ["Devanagari"] = "Devanágari", ["Han"] = "Han", ["Hangul"] = "Hangul",
        },

        LogTitle = "AutoLang — Log",
        CopyAll = "Copiar tudo",
        Refresh = "Atualizar",
        LogEarlierOmitted = "… (linhas anteriores omitidas)",
        LogEmpty = "(o log está vazio)",
        LogUnreadable = "(não foi possível ler o log: {0})",

        AlreadyRunning = "O AutoLang já está em execução (procure o ícone na área de notificação).",
        NeedsTwoAlphabetsLong =
            "O AutoLang corrige texto digitado no alfabeto errado, por isso precisa de teclados para pelo menos dois alfabetos diferentes " +
            "(por exemplo, inglês e hebraico, ou inglês e grego).",
        InstalledKeyboards = "Instalados: {0}",
        HookFailed = "O AutoLang não conseguiu instalar seu gancho de teclado.",
    };
}
