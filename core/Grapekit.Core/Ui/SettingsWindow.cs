using System.Drawing.Drawing2D;

namespace Grapekit.Ui;

/// <summary>
/// Tüm Grapekit uygulamalarının ayarlar penceresi: kendi başlık çubuğu, gece moru bant (durum + ana eylem),
/// bandın altında sekmeler, altta kaydırılabilir içerik. Uygulama sekmeleri <see cref="BuildTab"/> ile doldurur.
/// Değişiklikler hemen uygulanır (Kaydet düğmesi yoktur).
/// </summary>
public abstract class SettingsWindow : Form
{
    protected Kit K { get; private set; }
    protected readonly Fonts F = new();
    protected int Tab { get; private set; }
    public UpdateManager? Updates { get; }

    Page? _page;
    TabStrip? _tabs;
    PillButton? _action;
    CaptionButton? _min, _close;
    Font? _fName, _fVer, _fHero, _fHeroSub;
    (string Title, string Sub, string? Action) _hero;

    const int LogicalW = 880, LogicalH = 640;

    protected abstract string[] TabNames { get; }
    protected abstract void BuildTab(int tab, PageBuilder b);
    /// <summary>Banttaki durum: başlık, alt satır, eylem düğmesi (null: yok).</summary>
    protected abstract (string Title, string Sub, string? Action) HeroState();
    protected virtual void OnHeroAction() { }
    protected virtual int AboutTab => TabNames.Length - 1;

    protected SettingsWindow(Kit k, int tab, UpdateManager? updates)
    {
        K = k; Tab = tab; Updates = updates;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        DoubleBuffered = true;
        Text = GrapekitApp.Name;
        Icon = AppIcon.Get(32);
        if (updates != null) { updates.Changed += OnUpdatesChanged; updates.Progress += OnUpdateProgress; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000;            // CS_DROPSHADOW
            cp.Style |= 0x00020000;              // WS_MINIMIZEBOX: görev çubuğundan küçült/geri getir
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2;
        Win32.DwmSetWindowAttribute(Handle, 33, ref round, 4);
        Rebuild();
    }

    protected int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    int TitleH => S(44);
    int BandH => S(84);
    int TabsH => S(46);
    int ContentTop => TitleH + BandH + TabsH;

    /// <summary>Tüm pencereyi yeniden kurar (dil, tema veya DPI değişince).</summary>
    public void Rebuild(Kit? kit = null)
    {
        if (kit != null) K = kit;
        SuspendLayout();
        foreach (var c in Controls.Cast<Control>().ToArray()) c.Dispose();
        foreach (var f in new[] { _fName, _fVer, _fHero, _fHeroSub }) f?.Dispose();
        float k = DeviceDpi / 96f;
        F.CreateBrand(DeviceDpi);
        _fName = BrandFonts.Display(14 * k);
        _fVer = BrandFonts.Body(12 * k);
        _fHero = BrandFonts.Display(28 * k);
        _fHeroSub = BrandFonts.Body(13 * k);
        BackColor = K.P.Bg;
        Text = GrapekitApp.Name;
        Size = new Size(S(LogicalW), S(LogicalH)); // kenarlıksız: pencere = istemci alanı
        int w = ClientSize.Width;

        _close = new CaptionButton(K, "", true, CoreText.T("close")) { Bounds = new Rectangle(w - S(46), 0, S(46), TitleH) };
        _close.Click += (_, _) => Close();
        _min = new CaptionButton(K, "", false, CoreText.T("minimize")) { Bounds = new Rectangle(w - S(92), 0, S(46), TitleH) };
        _min.Click += (_, _) => WindowState = FormWindowState.Minimized;

        _action = PillButton.Primary(K, "", F.Ctl, K.Hero);
        _action.Click += (_, _) => OnHeroAction();

        _tabs = new TabStrip(K, TabNames, Tab, F.Nav) { Bounds = new Rectangle(S(20), TitleH + BandH, w - S(40), TabsH) };
        _tabs.SelectedChanged += SelectTab;

        _page = new Page(K.P, F) { Bounds = new Rectangle(1, ContentTop, w - 2, ClientSize.Height - ContentTop - 1), CardRadius = 16 };
        Controls.AddRange([_close, _min, _action, _tabs, _page]);
        ResumeLayout();
        RefreshHero(force: true);
        BuildPage();
        Invalidate();
    }

    public void SelectTab(int tab)
    {
        Tab = Math.Clamp(tab, 0, TabNames.Length - 1);
        if (_tabs != null) _tabs.Selected = Tab;
        BuildPage();
        if (_page != null) _page.AutoScrollPosition = Point.Empty;
    }

    void BuildPage()
    {
        if (_page == null) return;
        _page.SuspendLayout();
        _page.Clear();
        _page.AutoScrollPosition = Point.Empty;
        var b = new PageBuilder(_page, K, F);
        BuildTab(Tab, b);
        b.Finish();
        _page.ResumeLayout();
        _page.Invalidate();
    }

    /// <summary>Geçerli sekmeyi kaydırma konumunu koruyarak yeniden çizer (ayar dışarıdan değişince).</summary>
    public void RefreshPage()
    {
        if (_page == null) return;
        var pos = _page.AutoScrollPosition;
        BuildPage();
        _page.AutoScrollPosition = new Point(-pos.X, -pos.Y);
    }

    void OnUpdateProgress()
    {
        if (!IsDisposed && Tab == AboutTab) _page?.Invalidate();
    }

    void OnUpdatesChanged()
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (Tab == AboutTab) RefreshPage();
    }

    /// <summary>Banttaki durumu yeniler; değişmediyse hiçbir şey çizmez (saniyede bir çağrılabilir).</summary>
    public void RefreshHero(bool force = false)
    {
        var h = HeroState();
        if (!force && h == _hero) return;
        _hero = h;
        if (_action != null)
        {
            _action.Visible = h.Action != null;
            _action.Text = h.Action ?? "";
            int bw = Math.Max(S(120), _action.PreferredWidth);
            _action.SetBounds(ClientSize.Width - S(32) - bw, TitleH + (BandH - S(44)) / 2, bw, S(44));
        }
        Invalidate(new Rectangle(0, TitleH, ClientSize.Width, BandH));
    }

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        _page?.SetBounds(1, ContentTop, ClientSize.Width - 2, ClientSize.Height - ContentTop - 1);
        Invalidate();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Rebuild();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.Tab) { SelectTab((Tab + (e.Shift ? TabNames.Length - 1 : 1)) % TabNames.Length); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x84 && m.Result == 1) // WM_NCHITTEST → HTCLIENT
        {
            long lp = m.LParam.ToInt64();
            var pt = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            if (pt.Y < TitleH + BandH) m.Result = 2; // HTCAPTION: başlık ve banttan sürüklenir
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_fName == null) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(K.P.Bg);
        using (var hb = new SolidBrush(K.Hero)) g.FillRectangle(hb, 0, 0, ClientSize.Width, ContentTop);

        const TextFormatFlags one = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        int ic = S(22);
        AppIcon.Paint(g, new Rectangle(S(16), (TitleH - ic) / 2, ic, ic));
        int nx = S(16) + ic + S(10);
        int nw = TextRenderer.MeasureText(GrapekitApp.Name, _fName).Width;
        TextRenderer.DrawText(g, GrapekitApp.Name, _fName, new Rectangle(nx, 0, nw + S(4), TitleH), K.HeroText, one | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, GrapekitApp.VersionText, _fVer, new Rectangle(nx + nw + S(8), 0, S(80), TitleH), K.HeroSub, one | TextFormatFlags.VerticalCenter);

        int textW = (_action?.Visible == true ? _action.Left : ClientSize.Width) - S(32) - S(24);
        int hy = TitleH + S(12);
        TextRenderer.DrawText(g, _hero.Title, _fHero, new Rectangle(S(32), hy, textW, _fHero!.Height), K.HeroText, one);
        TextRenderer.DrawText(g, _hero.Sub, _fHeroSub, new Rectangle(S(32), hy + _fHero.Height + S(4), textW, _fHeroSub!.Height), K.HeroSub, one);

        using var pen = new Pen(K.Dark ? K.P.Border : ThemeInfo.Mix(K.P.Border, Brand.Night, 0.2));
        g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Updates != null) { Updates.Changed -= OnUpdatesChanged; Updates.Progress -= OnUpdateProgress; }
            F.Dispose();
            foreach (var f in new[] { _fName, _fVer, _fHero, _fHeroSub }) f?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Ayarlar sayfasını yukarıdan aşağı dolduran yardımcı: bölüm başlığı, anahtarlar, çipler, kartlar, düğmeler.</summary>
public sealed class PageBuilder
{
    readonly Page _p;
    readonly Kit _k;
    readonly Fonts _f;
    readonly int _x0, _w0;
    int _x, _w, _y, _softStart = -1;
    bool _first = true;
    Color _surface;

    public Kit Kit => _k;
    public Fonts Fonts => _f;
    public Color Surface => _surface;
    public int Width => _w;

    internal PageBuilder(Page p, Kit k, Fonts f)
    {
        _p = p; _k = k; _f = f;
        int cw = p.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
        _w0 = _w = Math.Min(S(760), cw - S(64));
        _x0 = _x = (cw - _w) / 2;
        _y = S(28);
        _surface = k.P.Bg;
    }

    int S(int v) => _p.LogicalToDeviceUnits(v);

    public void Section(string title, string? desc = null)
    {
        if (!_first) _y += S(30);
        _first = false;
        _p.Headings.Add((title, new Point(_x, _y), false));
        _y += _f.Heading.Height + S(2);
        if (desc != null) Note(desc);
        _y += S(8);
    }

    public void Note(string text, Color? color = null)
    {
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
        int h = TextRenderer.MeasureText(text, _f.Desc, new Size(_w, 0), flags).Height;
        _p.Paragraphs.Add((text, new Rectangle(_x, _y, _w, h + S(2)), color ?? _k.P.SubText));
        _y += h + S(4);
    }

    public void Gap(int logical) => _y += S(logical);

    public T Add<T>(T c, int logicalHeight, int? width = null) where T : Control
    {
        _first = false;
        c.SetBounds(_x, _y, width ?? _w, S(logicalHeight));
        _p.Controls.Add(c);
        _y += c.Height;
        return c;
    }

    /// <summary>Hafif mor zeminli kutu başlatır; içindeki kontroller kutunun yüzeyini kullanır.</summary>
    public void BeginSoft()
    {
        _softStart = _y;
        _x = _x0 + S(18); _w = _w0 - S(36);
        _y += S(18);
        _surface = _k.P.Card;
    }

    public void EndSoft()
    {
        if (_softStart < 0) return;
        _y += S(18);
        _p.Cards.Add(new Rectangle(_x0, _softStart, _w0, _y - _softStart));
        _x = _x0; _w = _w0; _softStart = -1;
        _surface = _k.P.Bg;
    }

    public ToggleRow Toggle(string title, string desc, bool value, Action<bool> changed)
    {
        var t = new ToggleRow(_k.P, title, desc, value, _f.Row, _f.Desc, _surface) { Inset = 0 };
        t.CheckedChanged += changed;
        Add(t, 64);
        _p.Dividers.Add((_x, _x + _w, _y - 1));
        return t;
    }

    public ChipGroup Chips(string[] options, int selected, Action<int> picked, bool uniform = false)
    {
        var c = new ChipGroup(_k.P, options, selected, _f.Ctl, uniform, _surface);
        c.SelectedChanged += picked;
        Add(c, 44);
        _y += S(4);
        return c;
    }

    public ChoiceCards Cards((string Icon, string Title, string Desc)[] items, int selected, Action<int> picked)
    {
        var c = new ChoiceCards(_k, items, selected, _f.Row, _f.Desc, _surface);
        c.SelectedChanged += picked;
        return Add(c, 104);
    }

    public PillButton Primary(string text, Action click) => Button(PillButton.Primary(_k, text, _f.Ctl, _surface), click);
    public PillButton Secondary(string text, Action click) => Button(PillButton.Secondary(_k, text, _f.Ctl, _surface), click);
    public PillButton Link(string text, Action click) => Button(PillButton.Link(_k, text, _f.Ctl, _surface), click);

    static PillButton Button(PillButton b, Action click) { b.Click += (_, _) => click(); return b; }

    /// <summary>Düğmeleri yan yana dizer; satıra sığmayan alta geçer.</summary>
    public void Buttons(params PillButton[] buttons)
    {
        int x = _x, h = S(44), gap = S(10);
        foreach (var b in buttons)
        {
            int bw = b.PreferredWidth;
            if (x > _x && x + bw > _x + _w) { x = _x; _y += h + gap; }
            b.SetBounds(x, _y, bw, h);
            _p.Controls.Add(b);
            x += bw + gap;
        }
        _y += h;
    }

    public void Painter(int logicalHeight, Action<Graphics, Rectangle> paint)
    {
        _first = false;
        int y = _y, h = S(logicalHeight), x = _x, w = _w;
        _p.Painters.Add((g, o) => paint(g, new Rectangle(x + o.X, y + o.Y, w, h)));
        _y += h;
    }

    /// <summary>Standart Hakkında bölümü: işaret, sürüm, güncelleme, bağlantılar, gizlilik.</summary>
    public void About(UpdateManager? upd, Action<bool>? autoUpdateChanged = null, bool autoUpdate = true)
    {
        var k = _k;
        var big = _f.PageTitle;
        var desc = _f.Desc;
        Painter(96, (g, r) =>
        {
            var tile = new RectangleF(r.X, r.Y + S(4), S(88), S(88));
            Draw.Fill(g, tile, S(26), k.Hero);
            Brand.PaintMark(g, RectangleF.Inflate(tile, -S(18), -S(18)), k.Ring);
            int tx = r.X + S(88) + S(24);
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, $"{GrapekitApp.Name} {GrapekitApp.VersionText}", big, new Rectangle(tx, r.Y + S(10), r.Right - tx, big.Height), k.P.Text, flags);
            TextRenderer.DrawText(g, CoreText.T("by") + " · " + CoreText.T("tagline"), desc, new Rectangle(tx, r.Y + S(14) + big.Height, r.Right - tx, desc.Height), k.P.SubText, flags);
            TextRenderer.DrawText(g, CoreText.T("free_note"), desc, new Rectangle(tx, r.Y + S(18) + big.Height + desc.Height, r.Right - tx, desc.Height), k.P.SubText, flags);
        });

        Section(CoreText.T("upd_check"));
        // Durum yazısı her çizimde güncel değerden okunur (indirme yüzdesi sayfayı yeniden kurmadan ilerler)
        var statusColor = upd?.Available != null ? k.P.AccentText : k.P.SubText;
        Painter(22, (g, r) => TextRenderer.DrawText(g, upd?.StatusText ?? CoreText.T("upd_store"), desc, r, statusColor,
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter));
        Gap(6);
        if (upd != null && Updater.Supported)
        {
            var main = upd.Available is { } a
                ? Primary(string.Format(CoreText.T("upd_install"), a.Version.ToString(3)), () => _ = upd.InstallAsync())
                : Primary(CoreText.T("upd_check"), () => _ = upd.CheckAsync(manual: true));
            main.Enabled = !upd.Busy;
            Buttons(main, Secondary(CoreText.T("website"), () => Brand.Open(GrapekitApp.PageUrl)));
            if (autoUpdateChanged != null)
            {
                Gap(8);
                Toggle(CoreText.T("upd_auto"), CoreText.T("upd_auto_desc"), autoUpdate, autoUpdateChanged);
            }
        }
        else Buttons(Secondary(CoreText.T("website"), () => Brand.Open(GrapekitApp.PageUrl)));

        Section(CoreText.T("support").TrimStart('☕', ' '), CoreText.T("support_desc"));
        Buttons(Primary(CoreText.T("support").TrimStart('☕', ' '), () => Brand.Open(Brand.Support)),
                Secondary(CoreText.T("more_apps"), () => Brand.Open(Brand.AllAppsUrl)));

        Section(CoreText.T("about"));
        Buttons(Link(CoreText.T("source"), () => Brand.Open(GrapekitApp.SourceUrl)),
                Link(CoreText.T("report"), () => Brand.Open(GrapekitApp.FeedbackUrl)),
                Link(CoreText.T("privacy"), () => Brand.Open(Brand.PrivacyUrl)));
        Gap(8);
        Note(CoreText.T("privacy_note"));
        Note($"{Brand.Copyright} · {Brand.License}");
    }

    internal void Finish()
    {
        EndSoft();
        _p.AutoScrollMinSize = new Size(0, _y + S(32));
    }
}
