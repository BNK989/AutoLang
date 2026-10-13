using System.Windows.Forms;
using AutoLang.Engine;
using AutoLang.Session;

namespace AutoLang.Host;

internal static class Program
{
    /// <summary>Passed back to us by Windows when it restarts AutoLang after a crash or hang.</summary>
    private const string RestartedArg = "--restarted";

    [STAThread]
    private static int Main(string[] args)
    {
        bool restarted = args.Contains(RestartedArg);
        using var single = new Mutex(true, @"Local\AutoLang.Host", out bool first);
        if (!first)
        {
            if (restarted) return 2; // the user (or start at login) beat Windows to it: nothing to say
            MessageBox.Show("AutoLang is already running (look for the icon in the tray).", "AutoLang", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 2;
        }

        Log.Init();
        if (restarted) Log.Warn("restarted by Windows after a crash or hang");
        // Crash resilience (PLAN.md §5, M4): Windows Error Reporting restarts us if we crash or hang after the first
        // 60 s. Not on reboot or update: start at login covers that.
        int hr = Native.RegisterApplicationRestart(RestartedArg, Native.RESTART_NO_REBOOT);
        if (hr != 0) Log.Warn($"RegisterApplicationRestart failed (0x{hr:X8})");
        if (Native.IsPackaged) Log.Info("running packaged (MSIX)");
        ApplicationConfiguration.Initialize();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Warn($"unhandled: {e.ExceptionObject}");
        Application.ThreadException += (_, e) => Log.Warn($"ui thread: {e.Exception}");

        var settings = Settings.Load();

        // 1. What does the user have installed? Everything downstream is derived from this.
        var installed = LayoutProbe.Installed();
        foreach (var l in installed) Log.Info($"installed layout: {l} ({l.Table.Script}, {l.Table.Letters.Count} letters, list {WordLists.SourceCode(l.LangId) ?? "none"})");

        // 2. Group by alphabet (PLAN.md §2). Switching only ever happens between alphabets, so with one alphabet
        //    (English + German, say) there is nothing for AutoLang to do.
        if (installed.Select(l => l.Table.Script).Where(s => s != Scripts.Unknown).Distinct().Count() < 2)
        {
            MessageBox.Show("AutoLang corrects text typed in the wrong alphabet, so it needs keyboards for at least two different alphabets " +
                            "(for example English and Hebrew, or English and Greek).\n\n" +
                            "Installed: " + string.Join(", ", installed.Select(l => $"{l.Table.Name} ({l.Table.Script})").Distinct()),
                "AutoLang", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }

        // 3. Word lists (bundled, downloaded earlier, or fetched now in the background) and the session built from
        //    them. Until two alphabets have a list the controller is idle; it is reconfigured as lists arrive and
        //    whenever the user changes the language settings.
        using var languages = new LanguageManager(installed, settings);
        var (decider, tables, never) = languages.Build();
        var controller = new SessionController(decider, tables, neverSwitchTo: never);
        controller.AddExceptions(settings.ExceptionWords);
        languages.Changed += () =>
        {
            var (d, t, n) = languages.Build();
            controller.Configure(d, t, n);
            Log.Info($"languages: {(t.Length == 0 ? "none ready" : string.Join(" / ", t.Select(x => x.ToString())))}{(n.Length > 0 ? $"; never switch to {string.Join(", ", n.Select(LanguageManager.CultureOf))}" : "")}");
        };
        languages.Start(); // loads what is on disk synchronously (raising Changed), then downloads in the background

        // Every installed layout is known to the hook, active or not: the controller decides what to ignore.
        var byHkl = installed.ToDictionary(l => l.Hkl);
        var hklByLang = installed.GroupBy(l => l.LangId).ToDictionary(g => g.Key, g => g.First().Hkl);
        InstalledLayout? LayoutOf(nint hkl)
        {
            if (byHkl.TryGetValue(hkl, out var l)) return l;
            var lang = LayoutProbe.LangIdOf(hkl);
            return installed.FirstOrDefault(x => x.LangId == lang); // same language, variant layout (e.g. Hebrew Standard vs. legacy)
        }

        // 4. Threads: corrector (injects), focus monitor (vets windows), hook (listens).
        // The typing method is read on the corrector thread and changed from the tray: both lock the settings.
        using var corrector = new Corrector(process => { lock (settings) return settings.TypingMethodFor(process); });
        using var focus = new FocusMonitor(controller, settings);
        using var hook = new KeyboardHook(controller, corrector, focus, LayoutOf, lang => hklByLang.TryGetValue(lang, out var h) ? h : 0);
        corrector.Start();
        focus.Start();
        focus.Poke();
        try { hook.Start(); }
        catch (Exception ex)
        {
            Log.Warn(ex.Message);
            MessageBox.Show("AutoLang could not install its keyboard hook.\n" + ex.Message, "AutoLang", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        // Persist words the user undid, or erased our correction of and typed again, as they happen: the only thing
        // learnt from typing that ever hits disk, and only because the user explicitly asked for that word to be left alone.
        void SaveExceptions()
        {
            var words = controller.Exceptions;
            lock (settings)
            {
                settings.ExceptionWords = words.ToList();
                settings.Save();
            }
        }
        corrector.Applied += job => { if (job.Correction.Kind == CorrectionKind.Undo) SaveExceptions(); };
        controller.ExceptionsAdded += () => ThreadPool.QueueUserWorkItem(_ => SaveExceptions()); // raised on the hook thread: no disk I/O there

        using var tray = new TrayApp(settings, controller, focus, corrector, languages, onExit: () => Log.Info("exit by user"));
        Application.Run(tray);
        return 0;
    }
}
