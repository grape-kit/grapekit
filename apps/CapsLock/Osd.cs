namespace CapsGosterge;

/// <summary>Odak çalmayan, tıklamaların içinden geçtiği, yarı saydam katmanlı pencere.</summary>
internal sealed class LayeredWindow : Form
{
    IntPtr _hBmp;
    Size _size;

    public LayeredWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "";
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW
                        | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public bool IsShowing { get; private set; }

    public void SetContent(Bitmap bmp)
    {
        FreeBitmap();
        _hBmp = bmp.GetHbitmap(Color.FromArgb(0));
        _size = bmp.Size;
    }

    public void Apply(byte alpha, Point pos)
    {
        if (_hBmp == IntPtr.Zero) return;
        if (!IsHandleCreated) CreateHandle();

        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr old = Native.SelectObject(memDc, _hBmp);
        try
        {
            var dst = new Native.POINT(pos.X, pos.Y);
            var src = new Native.POINT(0, 0);
            var size = new Native.SIZE(_size.Width, _size.Height);
            var blend = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER,
                SourceConstantAlpha = alpha,
                AlphaFormat = Native.AC_SRC_ALPHA,
            };
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
        }
        finally
        {
            Native.SelectObject(memDc, old);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public void ShowNoActivate()
    {
        if (!IsHandleCreated) CreateHandle();
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        IsShowing = true;
    }

    public void HideWindow()
    {
        if (IsHandleCreated) Native.ShowWindow(Handle, Native.SW_HIDE);
        IsShowing = false;
        FreeBitmap(); // gizliyken bellek tutma
    }

    void FreeBitmap()
    {
        if (_hBmp != IntPtr.Zero) Native.DeleteObject(_hBmp);
        _hBmp = IntPtr.Zero;
    }

    protected override void Dispose(bool disposing)
    {
        FreeBitmap();
        base.Dispose(disposing);
    }
}

/// <summary>Ekran bildirimi (beliren/kaybolan kart), kalıcı rozet ve şifre uyarısı balonu.</summary>
internal sealed class Osd : IDisposable
{
    readonly LayeredWindow _popup = new();
    readonly LayeredWindow _badge = new();
    readonly LayeredWindow _warning = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };

    enum Phase { Hidden, In, Hold, Out }
    Phase _phase = Phase.Hidden;
    long _t0;
    double _alpha, _from, _maxAlpha = 1;
    Point _pos;
    int _rise, _holdMs;

    const int InMs = 150, OutMs = 380;

    public static readonly float[] SizeFactors = [0.8f, 1f, 1.3f, 1.7f];

    public Osd() => _timer.Tick += (_, _) => Tick();

    static Point Place(Rectangle wa, Size bmp, int margin, int position, float s)
    {
        int edge = (int)Math.Round(28 * s) - margin;
        int col = position % 3, row = position / 3;
        int x = col switch
        {
            0 => wa.Left + edge,
            2 => wa.Right - bmp.Width - edge,
            _ => wa.Left + (wa.Width - bmp.Width) / 2,
        };
        int y = row switch
        {
            0 => wa.Top + edge,
            2 => wa.Bottom - bmp.Height - edge,
            _ => wa.Top + (wa.Height - bmp.Height) / 2,
        };
        return new Point(x, y);
    }

    public void ShowCard(CardSpec card, Palette p, int position, int holdMs, int sizeIndex, int opacity)
    {
        var cur = Cursor.Position;
        float s = Native.ScaleAt(cur) * SizeFactors[Math.Clamp(sizeIndex, 0, 3)];
        var wa = Screen.FromPoint(cur).WorkingArea;

        using var bmp = Render.Card(card, p, s, out int margin);
        _pos = Place(wa, bmp.Size, margin, position, s);
        _rise = (int)Math.Round(12 * s) * (position / 3 == 0 ? -1 : 1);
        _holdMs = holdMs;
        _maxAlpha = Math.Clamp(opacity, 30, 100) / 100.0;

        _popup.SetContent(bmp);
        _from = _phase == Phase.Hidden ? 0 : _alpha;
        _phase = Phase.In;
        _t0 = Environment.TickCount64;
        Apply();
        _popup.ShowNoActivate();
        _timer.Interval = 15;
        _timer.Start();
    }

    public void HidePopup()
    {
        _timer.Stop();
        _phase = Phase.Hidden;
        _alpha = 0;
        _popup.HideWindow();
    }

    void Tick()
    {
        long now = Environment.TickCount64;
        long el = now - _t0;
        switch (_phase)
        {
            case Phase.In:
            {
                double k = Math.Min(1.0, el / (double)InMs);
                _alpha = _from + (1 - _from) * (1 - Math.Pow(1 - k, 3));
                if (k >= 1) { _phase = Phase.Hold; _t0 = now; _timer.Interval = Math.Max(15, _holdMs); }
                break;
            }
            case Phase.Hold:
                // Bekleme süresince 15 ms'lik zamanlayıcı yerine tek uzun aralık: işlemciyi boşuna uyandırma
                _timer.Interval = 15;
                _phase = Phase.Out; _t0 = now;
                return;
            case Phase.Out:
            {
                double k = Math.Min(1.0, el / (double)OutMs);
                _alpha = 1 - k * k;
                if (k >= 1) { HidePopup(); return; }
                break;
            }
            default:
                _timer.Stop();
                return;
        }
        Apply();
    }

    void Apply()
    {
        int dy = _phase == Phase.In ? (int)Math.Round((1 - _alpha) * _rise) : 0;
        _popup.Apply((byte)Math.Round(Math.Clamp(_alpha * _maxAlpha, 0, 1) * 255), new Point(_pos.X, _pos.Y + dy));
    }

    /// <summary>Kalıcı rozet: tam orta seçiliyse ekranı kapatmasın diye üst ortaya konur.</summary>
    public void SetBadge(bool visible, Palette p, int position, int opacity)
    {
        if (!visible) { if (_badge.IsShowing) _badge.HideWindow(); return; }
        var scr = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var wa = scr.WorkingArea;
        float s = Native.ScaleAt(new Point(wa.Left + wa.Width / 2, wa.Top + wa.Height / 2));
        using var bmp = Render.Badge(p, s, out int margin);
        int pos = position == 4 ? 1 : position;
        _badge.SetContent(bmp);
        _badge.Apply((byte)(Math.Clamp(opacity, 30, 100) * 245 / 100), Place(wa, bmp.Size, margin, pos, s));
        _badge.ShowNoActivate();
    }

    /// <summary>Şifre alanının hemen altında uyarı balonu.</summary>
    public void ShowWarning(Rectangle field, CardSpec card, Palette p, int sizeIndex, int opacity)
    {
        var center = new Point(field.Left + field.Width / 2, field.Bottom);
        float s = Native.ScaleAt(center) * Math.Min(1f, SizeFactors[Math.Clamp(sizeIndex, 0, 3)]) * 0.85f;
        using var bmp = Render.Card(card, p, s, out int margin, arrowUp: true);
        var wa = Screen.FromPoint(center).WorkingArea;
        int x = Math.Clamp(field.Left - margin, wa.Left - margin, wa.Right - bmp.Width + margin);
        int y = field.Bottom + (int)(4 * s) - margin;
        if (y + bmp.Height - margin > wa.Bottom) y = field.Top - bmp.Height + margin; // altta yer yoksa üstte
        _warning.SetContent(bmp);
        _warning.Apply((byte)(Math.Clamp(opacity, 30, 100) * 255 / 100), new Point(x, y));
        _warning.ShowNoActivate();
    }

    public void HideWarning()
    {
        if (_warning.IsShowing) _warning.HideWindow();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _popup.Dispose();
        _badge.Dispose();
        _warning.Dispose();
    }
}
