using System.Drawing;
using System.Windows.Forms;

namespace AutoLang.Host;

/// <summary>
/// The language settings (PLAN.md §2): which installed keyboards AutoLang uses, which it may never switch to, and the
/// state of each word list. Built in code; rebuilt from <see cref="LanguageManager.Languages"/> on every change.
/// </summary>
internal sealed class LanguagesForm : Form
{
    private readonly LanguageManager _languages;
    private readonly TableLayoutPanel _grid;
    private readonly CheckBox _autoDownload;
    private readonly Label _warning;
    private readonly SynchronizationContext _ui;
    private readonly Font _bold;

    public LanguagesForm(LanguageManager languages)
    {
        _languages = languages;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        Text = "AutoLang — Languages";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = SystemFonts.MessageBoxFont ?? Font;
        Padding = new Padding(12);
        _bold = new Font(Font, FontStyle.Bold);

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        root.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(0, 0, 0, 10),
            Text = "AutoLang fixes words typed in the wrong alphabet and switches the keyboard for you. Keyboards that share an " +
                   "alphabet (English and German, say) are never switched between automatically; when a word belongs to that " +
                   "alphabet, AutoLang picks the one you used last.",
        });

        _grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 5, Margin = new Padding(0, 0, 0, 8), CellBorderStyle = TableLayoutPanelCellBorderStyle.None };
        for (int i = 0; i < 5; i++) _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.Controls.Add(_grid);

        _warning = new Label { AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.FromArgb(180, 90, 0), Margin = new Padding(0, 0, 0, 8) };
        root.Controls.Add(_warning);

        _autoDownload = new CheckBox { AutoSize = true, Text = "Download missing word lists automatically (the only thing AutoLang ever fetches)" };
        root.Controls.Add(_autoDownload);

        var attribution = new LinkLabel { AutoSize = true, Text = WordLists.Attribution, Margin = new Padding(0, 10, 0, 0) };
        attribution.LinkClicked += (_, _) => OpenUrl(WordLists.AttributionUrl);
        root.Controls.Add(attribution);

        var close = new Button { Text = "Close", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
        close.Click += (_, _) => Close();
        root.Controls.Add(close);
        CancelButton = close;

        Controls.Add(root);
        _languages.Changed += OnChanged;
        Rebuild();
    }

    private void OnChanged() => _ui.Post(_ => { if (!IsDisposed) Rebuild(); }, null);

    private void Rebuild()
    {
        var langs = _languages.Languages;
        _grid.SuspendLayout();
        _grid.Controls.Clear();
        _grid.RowStyles.Clear();
        _grid.RowCount = 0;

        AddRow(Header("Keyboard"), Header("Alphabet"), Header("Word list"), Header("Use"), Header("Switch to it"));
        foreach (var l in langs)
        {
            var use = new CheckBox { Checked = l.Active, AutoSize = true, Anchor = AnchorStyles.None };
            var to = new CheckBox { Checked = l.Active && !l.NeverSwitchTo, Enabled = l.Active, AutoSize = true, Anchor = AnchorStyles.None };
            var id = l.LangId;
            use.CheckedChanged += (_, _) => _languages.SetActive(id, use.Checked);
            to.CheckedChanged += (_, _) => _languages.SetNeverSwitchTo(id, !to.Checked);
            AddRow(Cell(l.Name), Cell(l.Script), Status(l), use, to);
        }
        _grid.ResumeLayout();

        // Event-free update so reopening doesn't write the setting back.
        _autoDownload.CheckedChanged -= AutoDownloadChanged;
        _autoDownload.Checked = _languages.AutoDownload;
        _autoDownload.CheckedChanged += AutoDownloadChanged;

        var ready = langs.Where(l => l.Active && l.State == WordListState.Ready).ToArray();
        int scripts = ready.Select(l => l.Script).Distinct().Count();
        int targets = ready.Where(l => !l.NeverSwitchTo).Select(l => l.Script).Distinct().Count();
        _warning.Text =
            scripts < 2 ? "AutoLang is idle: it needs at least two different alphabets in use, each with a word list." :
            targets == 0 ? "AutoLang is idle: every language is set to never switch to it." :
            "";
        _warning.Visible = _warning.Text.Length > 0;
    }

    private void AutoDownloadChanged(object? sender, EventArgs e) => _languages.SetAutoDownload(_autoDownload.Checked);

    private Control Status(LanguageInfo l)
    {
        string text = l.State switch
        {
            WordListState.Ready => l.Downloaded ? "Ready (downloaded)" : "Ready",
            WordListState.Downloading => "Downloading…",
            WordListState.Unavailable => "Not available",
            _ => l.Error ?? "Missing",
        };
        if (l.State != WordListState.Missing) return Cell(text);
        var link = new LinkLabel { AutoSize = true, Text = $"{text} — download", Margin = new Padding(3, 6, 12, 3) };
        link.LinkClicked += (_, _) => _languages.Download(l.LangId);
        return link;
    }

    private void AddRow(params Control[] cells)
    {
        int row = _grid.RowCount++;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (int c = 0; c < cells.Length; c++) _grid.Controls.Add(cells[c], c, row);
    }

    private static Label Cell(string text) => new() { AutoSize = true, Text = text, Margin = new Padding(3, 6, 12, 3) };

    private Label Header(string text) => new() { AutoSize = true, Text = text, Font = _bold, Margin = new Padding(3, 3, 12, 3) };

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { Log.Warn($"open url: {ex.Message}"); }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _languages.Changed -= OnChanged;
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bold.Dispose();
        base.Dispose(disposing);
    }
}
