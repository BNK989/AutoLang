using System.Drawing;
using System.Windows.Forms;

namespace AutoLang.Host;

/// <summary>
/// The log, shown in-app. Packaged (MSIX), %LocalAppData% is redirected into the package's private folder, so the file
/// Notepad would be handed is not where the user can find it; this reads it from inside the package instead.
/// The log never holds typed text (PLAN.md §6), so showing and copying it is safe.
/// </summary>
internal sealed class LogViewerForm : Form
{
    /// <summary>Only the tail: the log is capped at 2 MB, and the recent part is what a bug report needs.</summary>
    private const int TailBytes = 256 * 1024;

    private readonly TextBox _text;

    public LogViewerForm()
    {
        Text = "AutoLang — Log";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(900, 560);
        MinimumSize = new Size(480, 300);
        Font = SystemFonts.MessageBoxFont ?? Font;

        _text = new TextBox
        {
            Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9f), BackColor = SystemColors.Window,
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        var close = new Button { Text = "Close", AutoSize = true };
        close.Click += (_, _) => Close();
        var copy = new Button { Text = "Copy all", AutoSize = true };
        copy.Click += (_, _) => { if (_text.TextLength > 0) Clipboard.SetText(_text.Text); };
        var refresh = new Button { Text = "Refresh", AutoSize = true };
        refresh.Click += (_, _) => Reload();
        buttons.Controls.AddRange(new Control[] { close, copy, refresh });
        CancelButton = close;

        Controls.Add(_text);
        Controls.Add(buttons);
        Load += (_, _) => Reload();
    }

    private void Reload()
    {
        string body;
        try
        {
            // The log stays open for writing; share both ways so neither side blocks.
            using var fs = new FileStream(Log.Path_, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            bool cut = fs.Length > TailBytes;
            if (cut) fs.Seek(-TailBytes, SeekOrigin.End);
            using var reader = new StreamReader(fs);
            body = reader.ReadToEnd();
            if (cut)
            {
                int nl = body.IndexOf('\n');
                body = "… (earlier lines omitted)" + Environment.NewLine + (nl >= 0 ? body[(nl + 1)..] : body);
            }
        }
        catch (FileNotFoundException) { body = "(the log is empty)"; }
        catch (Exception ex) { body = $"(the log could not be read: {ex.Message})"; }

        _text.Text = body.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        _text.SelectionStart = _text.TextLength;
        _text.ScrollToCaret();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _text.Font.Dispose();
        base.Dispose(disposing);
    }
}
