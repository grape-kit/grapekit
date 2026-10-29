using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KeepAwake;

/// <summary>Süreli oturum bitince yapılacak işlem (1 ekranı kapat, 2 kilitle, 3 uyku, 4 bilgisayarı kapat).</summary>
internal static class EndAction
{
    [DllImport("user32.dll")] static extern bool LockWorkStation();
    [DllImport("user32.dll")] static extern IntPtr PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static void Run(int action)
    {
        try
        {
            switch (action)
            {
                case 1: PostMessage(new IntPtr(0xFFFF), 0x0112, new IntPtr(0xF170), new IntPtr(2)); break; // SC_MONITORPOWER: kapat
                case 2: LockWorkStation(); break;
                case 3: Application.SetSuspendState(PowerState.Suspend, false, false); break;
                case 4: Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false }); break;
            }
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }
}

/// <summary>İşlemden önceki 60 saniyelik uyarı: tepsinin yanında, "Şimdi" ve "Vazgeç" düğmeleriyle.</summary>
internal sealed class EndWarning : Form
{
    readonly Kit _k;
    readonly int _action;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    readonly PillButton _now, _cancel;
    readonly Font _title, _text;
    int _left = 60;

    public EndWarning(Kit k, int action)
    {
        _k = k; _action = action;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        BackColor = k.P.Bg;
        Text = GrapekitApp.Name;
        float s = DeviceDpi / 96f;
        _title = BrandFonts.Display(17 * s);
        _text = BrandFonts.Body(13 * s);
        var btn = BrandFonts.BodySemi(13 * s);
        _now = PillButton.Secondary(k, L.T("warn_now"), btn, k.P.Bg);
        _now.Click += (_, _) => Finish(run: true);
        _cancel = PillButton.Primary(k, L.T("warn_cancel"), btn, k.P.Bg);
        _cancel.Click += (_, _) => Finish(run: false);
        Controls.AddRange([_now, _cancel]);

        int w = S(380);
        ClientSize = new Size(w, S(172));
        int bh = S(40);
        int cw = Math.Max(S(110), _cancel.PreferredWidth), nw = Math.Max(S(90), _now.PreferredWidth);
        _cancel.SetBounds(w - S(20) - cw, ClientSize.Height - S(20) - bh, cw, bh);
        _now.SetBounds(_cancel.Left - S(10) - nw, _cancel.Top, nw, bh);
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(wa.Right - Width - S(12), wa.Bottom - Height - S(12));

        _timer.Tick += (_, _) =>
        {
            if (--_left <= 0) Finish(run: true);
            else Invalidate();
        };
        _timer.Start();
        System.Media.SystemSounds.Exclamation.Play();
    }

    int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; cp.ExStyle |= 0x80; return cp; }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2;
        Win32.DwmSetWindowAttribute(Handle, 33, ref round, 4);
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); _cancel.Focus(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Finish(run: false);
        base.OnKeyDown(e);
    }

    void Finish(bool run)
    {
        if (!_timer.Enabled) return;
        _timer.Stop();
        Close();
        if (run) EndAction.Run(_action);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(_k.P.Bg);
        int band = S(64);
        using (var hb = new SolidBrush(_k.Hero)) g.FillRectangle(hb, 0, 0, Width, band);
        AppIcon.Paint(g, new Rectangle(S(20), (band - S(28)) / 2, S(28), S(28)));
        const TextFormatFlags one = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(g, L.T("warn_title" + _action), _title, new Rectangle(S(58), 0, Width - S(130), band), _k.HeroText, one);

        // Geri sayım rozeti
        var badge = new Rectangle(Width - S(20) - S(44), (band - S(36)) / 2, S(44), S(36));
        Draw.Fill(g, badge, S(12), _k.Ring);
        TextRenderer.DrawText(g, _left.ToString(), _title, badge, Brand.Night, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        TextRenderer.DrawText(g, string.Format(L.T("warn_text"), _left), _text, new Rectangle(S(20), band + S(14), Width - S(40), _text.Height * 2),
            _k.P.SubText, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);

        // Kalan süre çizgisi
        float k = _left / 60f;
        Draw.Fill(g, new RectangleF(0, band, Width * k, S(3)), 0, _k.P.Accent);

        using var pen = new Pen(_k.Dark ? _k.P.Border : ThemeInfo.Mix(_k.P.Border, Brand.Night, 0.15));
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _title.Dispose(); _text.Dispose(); }
        base.Dispose(disposing);
    }
}
