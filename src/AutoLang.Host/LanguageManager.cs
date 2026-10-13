using System.Globalization;
using System.Net.Http;
using AutoLang.Engine;

namespace AutoLang.Host;

internal enum WordListState
{
    /// <summary>Loaded; the language can be judged.</summary>
    Ready,
    Downloading,
    /// <summary>Not on disk and not being fetched (auto-download off, or it failed; see <see cref="LanguageInfo.Error"/>).</summary>
    Missing,
    /// <summary>No list exists for this language (IME languages, or FrequencyWords has none).</summary>
    Unavailable,
}

/// <summary>One installed keyboard language as the settings window shows it.</summary>
internal sealed record LanguageInfo(
    ushort LangId, string Culture, string Name, string Script, WordListState State, bool Downloaded,
    bool Active, bool NeverSwitchTo, string? Error);

/// <summary>
/// The installed keyboard languages and what the user chose for each (PLAN.md §2): which are active, which AutoLang may
/// never switch to, and whether its word list is loaded. Produces the engine configuration for the session controller
/// and raises <see cref="Changed"/> whenever that configuration changes (a setting, a finished download).
/// Thread-safe; <see cref="Changed"/> may fire on a background thread.
/// </summary>
internal sealed class LanguageManager : IDisposable
{
    private readonly Settings _settings;
    /// <summary>One table per language id: the first installed layout of each language.</summary>
    private readonly KeyboardLayoutTable[] _tables;
    private readonly object _sync = new();
    /// <summary>Profiles by word-list code; language variants (en-US, en-GB) share one and get a re-labelled copy.</summary>
    private readonly Dictionary<string, LanguageProfile> _byCode = new(StringComparer.Ordinal);
    private readonly HashSet<string> _downloading = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unavailable = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _downloaded = new(StringComparer.Ordinal);
    private readonly HttpClient _http;
    private readonly CancellationTokenSource _cts = new();

    public event Action? Changed;

    public LanguageManager(IEnumerable<InstalledLayout> installed, Settings settings)
    {
        _settings = settings;
        _tables = installed.GroupBy(l => l.LangId).Select(g => g.First().Table).ToArray();
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AutoLang");
    }

    /// <summary>Every distinct alphabet among the installed keyboards.</summary>
    public IReadOnlyList<string> InstalledScripts => _tables.Select(t => t.Script).Where(s => s != Scripts.Unknown).Distinct().ToArray();

    public static string CultureOf(ushort langId)
    {
        try { return CultureInfo.GetCultureInfo(langId).Name; }
        catch (CultureNotFoundException) { return $"{langId:x4}"; }
    }

    /// <summary>Load every list already on disk (bundled or downloaded earlier), then fetch the missing ones if allowed.</summary>
    public void Start()
    {
        foreach (var code in _tables.Select(t => WordLists.SourceCode(t.LangId)).OfType<string>().Distinct())
        {
            if (WordLists.Find(code) is { } found) TryLoad(code, found);
        }
        foreach (var t in _tables)
            if (WordLists.SourceCode(t.LangId) is null) Log.Info($"{CultureOf(t.LangId)}: no word list for this language (IME or unsupported)");
        Raise();
        DownloadMissing();
    }

    public IReadOnlyList<LanguageInfo> Languages
    {
        get
        {
            lock (_sync) lock (_settings)
                return _tables.Select(Describe).ToArray();
        }
    }

    private LanguageInfo Describe(KeyboardLayoutTable t)
    {
        var culture = CultureOf(t.LangId);
        var code = WordLists.SourceCode(t.LangId);
        var state = code is null || _unavailable.Contains(code) ? WordListState.Unavailable
            : _byCode.ContainsKey(code) ? WordListState.Ready
            : _downloading.Contains(code) ? WordListState.Downloading
            : WordListState.Missing;
        return new LanguageInfo(t.LangId, culture, t.Name, t.Script, state, code is not null && _downloaded.Contains(code),
            Active: !Contains(_settings.InactiveLanguages, culture),
            NeverSwitchTo: Contains(_settings.NeverSwitchTo, culture),
            Error: code is not null && _errors.TryGetValue(code, out var e) ? e : null);
    }

    /// <summary>
    /// What the controller should run with: the active languages that have a word list, their profiles, and the ones the
    /// user excluded as switch targets.
    /// </summary>
    public (PhraseDecider Decider, KeyboardLayoutTable[] Tables, ushort[] NeverSwitchTo) Build()
    {
        lock (_sync) lock (_settings)
        {
            var tables = new List<KeyboardLayoutTable>();
            var profiles = new List<LanguageProfile>();
            foreach (var t in _tables)
            {
                if (Contains(_settings.InactiveLanguages, CultureOf(t.LangId))) continue;
                if (WordLists.SourceCode(t.LangId) is not { } code || !_byCode.TryGetValue(code, out var p)) continue;
                tables.Add(t);
                profiles.Add(p.LangId == t.LangId ? p : new LanguageProfile(t.LangId, t.Name, p.Dictionary, p.Model,
                    LanguageProfile.DefaultRulesFor(t.LangId), p.Normalize));
            }
            var never = tables.Where(t => Contains(_settings.NeverSwitchTo, CultureOf(t.LangId))).Select(t => t.LangId).ToArray();
            return (new PhraseDecider(profiles), tables.ToArray(), never);
        }
    }

    /// <summary>"English, Deutsch ⇄ עברית": the active, ready languages grouped by alphabet.</summary>
    public string Summary()
    {
        var (_, tables, _) = Build();
        if (tables.Length == 0) return "No languages ready";
        return string.Join("  ⇄  ", tables.GroupBy(t => t.Script).Select(g => string.Join(", ", g.Select(t => t.Name))));
    }

    public void SetActive(ushort langId, bool active)
    {
        if (!Toggle(s => s.InactiveLanguages, langId, !active)) return;
        if (active) DownloadMissing();
        Raise();
    }

    public void SetNeverSwitchTo(ushort langId, bool never)
    {
        if (Toggle(s => s.NeverSwitchTo, langId, never)) Raise();
    }

    public bool AutoDownload { get { lock (_settings) return _settings.AutoDownloadWordLists; } }

    public void SetAutoDownload(bool on)
    {
        lock (_settings)
        {
            if (_settings.AutoDownloadWordLists == on) return;
            _settings.AutoDownloadWordLists = on;
            _settings.Save();
        }
        if (on) DownloadMissing();
        Raise();
    }

    /// <summary>Fetch one language's list on the user's request, regardless of the auto-download setting.</summary>
    public void Download(ushort langId)
    {
        if (WordLists.SourceCode(langId) is { } code && _tables.FirstOrDefault(t => t.LangId == langId) is { } t) StartDownload(code, t.Script);
    }

    private bool Toggle(Func<Settings, List<string>> list, ushort langId, bool present)
    {
        var culture = CultureOf(langId);
        lock (_settings)
        {
            var l = list(_settings);
            if (Contains(l, culture) == present) return false;
            if (present) l.Add(culture);
            else l.RemoveAll(x => x.Equals(culture, StringComparison.OrdinalIgnoreCase));
            _settings.Save();
        }
        Log.Info($"{culture}: {(present ? "added to" : "removed from")} a language setting by user");
        return true;
    }

    private void DownloadMissing()
    {
        bool auto;
        lock (_settings) auto = _settings.AutoDownloadWordLists;
        if (!auto) return;
        foreach (var info in Languages)
            if (info.Active && info.State == WordListState.Missing && info.Error is null)
                StartDownload(WordLists.SourceCode(info.LangId)!, info.Script);
    }

    private void StartDownload(string code, string script)
    {
        lock (_sync)
        {
            if (_byCode.ContainsKey(code) || _unavailable.Contains(code) || !_downloading.Add(code)) return;
            _errors.Remove(code);
        }
        Raise();
        _ = Task.Run(async () =>
        {
            try
            {
                Log.Info($"word list {code}: downloading");
                var path = await WordLists.DownloadAsync(_http, code, script, _cts.Token).ConfigureAwait(false);
                Log.Info($"word list {code}: downloaded");
                TryLoad(code, WordLists.FromFile(path));
            }
            catch (WordListUnavailableException)
            {
                Log.Info($"word list {code}: none available");
                lock (_sync) _unavailable.Add(code);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Message only: an exception here describes the network or the file, never anything typed.
                Log.Warn($"word list {code}: download failed: {ex.Message}");
                lock (_sync) _errors[code] = ex is HttpRequestException or TaskCanceledException ? "download failed (offline?)" : "download was not a valid word list";
            }
            finally
            {
                lock (_sync) _downloading.Remove(code);
            }
            Raise();
        });
    }

    private void TryLoad(string code, WordListSource source)
    {
        var table = _tables.First(t => WordLists.SourceCode(t.LangId) == code);
        var bundled = source.Bundled;
        try
        {
            var p = LanguageProfile.FromFrequencyLines(table.LangId, table.Name, source.ReadLines(),
                LanguageProfile.DefaultRulesFor(table.LangId), LanguageProfile.DefaultNormalizeFor(table.LangId));
            lock (_sync)
            {
                _byCode[code] = p;
                if (!bundled) _downloaded.Add(code);
            }
            Log.Info($"word list {code}: loaded ({p.Dictionary.Count} words, {(bundled ? "bundled" : "downloaded")})");
        }
        catch (Exception ex)
        {
            Log.Warn($"word list {code}: unreadable: {ex.Message}");
            lock (_sync) _errors[code] = "word list unreadable";
        }
    }

    private void Raise()
    {
        try { Changed?.Invoke(); }
        catch (Exception ex) { Log.Warn($"language change handler: {ex}"); }
    }

    private static bool Contains(List<string> list, string culture) => list.Any(x => x.Equals(culture, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        _cts.Cancel();
        _http.Dispose();
        _cts.Dispose();
    }
}
