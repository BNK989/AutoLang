using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Windows.Forms.Automation;

namespace AutoLang.Host;

/// <summary>What the status flyout says: the state and how to change it.</summary>
internal sealed record StatusModel(string Title, string Subtitle, Color Mark);

/// <summary>
/// The answer to a click on the tray icon: a small flyout in the welcome card's style that says whether AutoLang is now
/// on or paused, then goes away by itself. Replaces the notifications this used to raise, which piled up in the
/// notification center and were hidden by Do Not Disturb, though the user had just asked for the change.
/// One size for every state, and a crossfade when the state changes while it is up, so an accidental click and the
/// click that undoes it read as one flyout changing its mind. Never takes focus; hovering keeps it open.
/// </summary>
internal sealed class StatusFlyout : TrayFlyout
{
    private readonly Func<StatusModel> _model;
    private readonly System.Windows.Forms.Timer _hold = new() { Interval = 2600 };
    private readonly System.Windows.Forms.Timer _fade = new() { Interval = 10 };
    private readonly CardButton _close;
    private StatusModel _m;
    private Rectangle _markBox, _titleBox, _subtitleBox;

    // The previous state, painted once and faded out over the new one.
    private Bitmap? _old;
    private long _fadeStart;
    private const int FadeMs = 200;

    private const TextFormatFlags Line = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;

    public StatusFlyout(Func<StatusModel> model) : base(activate: false)
    {
        _model = model;
        _m = model();
        _hold.Tick += (_, _) => { _hold.Stop(); Dismiss(); };
        _fade.Tick += (_, _) => Fade();
        _close = new CardButton(this, accent: false) { Icon = "dismiss", AccessibleName = "Close", TabStop = false };
        _close.Click += (_, _) => Dismiss();
        _close.MouseLeave += (_, _) => HoldUnlessHovered();
        Controls.Add(_close);
    }

    /// <summary>The state changed again while the flyout is up (another click): show the new state for the full time.</summary>
    public void Restart()
    {
        if (IsDisposed || IsClosing) return;
        var next = _model();
        if (Visible && Animations() && next != _m)
        {
            _old?.Dispose();
            _old = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (var g = Graphics.FromImage(_old))
            {
                g.Clear(Theme.Back);
                PaintContent(g, _m);
            }
            _fadeStart = Environment.TickCount64;
            _fade.Start();
        }
        _m = next;
        Reflow();
        Announce();
        HoldUnlessHovered();
    }

    /// <summary>Rereads the model (word lists arriving) without extending the time on screen.</summary>
    public void Reload()
    {
        if (IsDisposed || IsClosing) return;
        _m = _model();
        Reflow();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        HoldUnlessHovered();
        Announce();
    }

    /// <summary>Screen readers do not follow a window that never takes focus, so read the new state out.</summary>
    private void Announce()
        => AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent, $"{_m.Title}. {_m.Subtitle}");

    /// <summary>(Re)starts the full time on screen, unless the pointer is on the flyout: reading it is a reason to keep it.</summary>
    private void HoldUnlessHovered()
    {
        _hold.Stop();
        if (!IsClosing && !ClientRectangle.Contains(PointToClient(Cursor.Position))) _hold.Start();
    }

    private void Fade()
    {
        if (Environment.TickCount64 - _fadeStart >= FadeMs)
        {
            _fade.Stop();
            _old?.Dispose();
            _old = null;
        }
        Invalidate(false);
    }

    protected override void Relayout()
    {
        var f = Fonts;
        // The welcome card's width, the same in every state: the flyout never jumps when the state changes.
        int w = S(368), pad = S(16), mark = S(32), gap = S(12);
        int titleH = TextHeight(f.Strong), subH = TextHeight(f.Body);
        int h = pad + Math.Max(mark, titleH + subH) + pad;

        _markBox = new Rectangle(pad, (h - mark) / 2, mark, mark);
        int tx = _markBox.Right + gap, ty = (h - titleH - subH) / 2;
        int textRight = w - S(8) - S(32) - S(4); // clear of the close button
        _titleBox = new Rectangle(tx, ty, textRight - tx, titleH);
        _subtitleBox = new Rectangle(tx, ty + titleH, textRight - tx, subH);
        _close.Bounds = new Rectangle(w - S(8) - S(32), S(8), S(32), S(32));

        ClientSize = new Size(w, h);
        AccessibleName = _m.Title;
        AccessibleDescription = _m.Subtitle;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintContent(g, _m);
        if (_old is not null)
        {
            float t = Math.Min(1f, (Environment.TickCount64 - _fadeStart) / (float)FadeMs);
            using var fade = new ImageAttributes();
            fade.SetColorMatrix(new ColorMatrix { Matrix33 = 1 - t });
            var r = new Rectangle(0, 0, _old.Width, _old.Height);
            g.DrawImage(_old, r, 0, 0, _old.Width, _old.Height, GraphicsUnit.Pixel, fade);
        }
        PaintBorder(g);
    }

    private void PaintContent(Graphics g, StatusModel m)
    {
        var f = Fonts;
        var t = Theme;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (var bmp = IconArt.Render(_markBox.Width, m.Mark)) g.DrawImage(bmp, _markBox);
        TextRenderer.DrawText(g, m.Title, f.Strong, _titleBox, t.Text, Line);
        TextRenderer.DrawText(g, m.Subtitle, f.Body, _subtitleBox, t.Secondary, Line);
    }

    protected override void OnMouseEnter(EventArgs e) { _hold.Stop(); base.OnMouseEnter(e); }
    // Moving onto the close button leaves the form but not the flyout.
    protected override void OnMouseLeave(EventArgs e) { HoldUnlessHovered(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) { Dismiss(); base.OnMouseClick(e); }

    /// <summary>Clicking the flyout or its close button must not take the keyboard from the window the user types in.</summary>
    protected override void WndProc(ref Message m)
    {
        const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
        if (m.Msg == WM_MOUSEACTIVATE) { m.Result = MA_NOACTIVATE; return; }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _hold.Stop();
        _fade.Stop();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _hold.Dispose(); _fade.Dispose(); _old?.Dispose(); }
        base.Dispose(disposing);
    }
}
