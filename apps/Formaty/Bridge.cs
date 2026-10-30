using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Formaty.Engines;

namespace Formaty;

/// <summary>Arayüzden gelen çağrılar (yöntem adı → işlem).</summary>
internal sealed partial class MainForm
{
    UpdateManager? _updates;

    static string S(JsonObject p, string k, string def = "") => p[k]?.GetValue<string>() ?? def;
    static bool B(JsonObject p, string k, bool def = false) => p[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : def;
    static double N(JsonObject p, string k, double def = 0) => p[k] is JsonValue v && v.TryGetValue<double>(out var d) ? d : def;
    static JsonArray Arr(IEnumerable<string> xs) => new(xs.Select(x => (JsonNode)x).ToArray());

    async Task<JsonNode?> HandleAsync(string method, JsonObject p, List<string>? dropped)
    {
        switch (method)
        {
            // ---------- Uygulama ----------
            case "app.info":
                return new JsonObject
                {
                    ["version"] = GrapekitApp.VersionText, ["lang"] = GrapekitApp.Lang == 0 ? "tr" : "en", ["packaged"] = GrapekitApp.IsPackaged,
                    ["args"] = Arr(_startupFiles), ["action"] = _startupAction, ["dark"] = !ThemeInfo.AppsUseLight(), ["explorerMenu"] = Shell.MenuEnabled(),
                    ["updateStatus"] = _updates?.StatusText ?? "", ["engines"] = Ffmpeg.Available, ["cpu"] = Environment.ProcessorCount,
                    ["downloads"] = KnownFolders.Downloads, ["pictures"] = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
#if DEBUG
                    ["debug"] = true,
#endif
                };
            case "app.ready":
                _ready = true;
                _readyTcs.TrySetResult();
                Opacity = 1;
                if (_updates == null) StartUpdates();
                return true;
            case "app.lang":
                GrapekitApp.Lang = S(p, "lang") == "en" ? 1 : 0;
                Text = GrapekitApp.Name;
                if (Shell.MenuEnabled()) Shell.SetMenu(true, remember: false); // menü dili
                return true;
            case "app.log": ErrorLog.Write(new Exception("Arayüz: " + S(p, "message") + Environment.NewLine + S(p, "stack"))); return true;
            case "app.exit": _allowClose = true; BeginInvoke(Close); return true;
            case "engines.hw": return await Ffmpeg.HardwareListAsync();

            // ---------- Ayarlar ----------
            case "settings.load": return Store.LoadSettings();
            case "settings.save": Store.SaveSettings(p["settings"]?.DeepClone()); return true;

            // ---------- Dosyalar ----------
            case "files.expand": return await Task.Run(() => Expand(p["paths"]?.AsArray().Select(x => x!.GetValue<string>()) ?? [], B(p, "recursive", true)));
            case "drop.paths": return Arr(dropped ?? []);
            case "file.info": return await InfoAsync(S(p, "path"));
            case "file.thumb": return await ThumbAsync(S(p, "path"), (int)N(p, "size", 160));
            case "dialog.files":
            {
                using var d = new OpenFileDialog { Multiselect = true, CheckFileExists = true, RestoreDirectory = true, Filter = FileFilter(S(p, "kind")) };
                return d.ShowDialog(this) == DialogResult.OK ? Arr(d.FileNames) : null;
            }
            case "dialog.folder":
            {
                using var d = new FolderBrowserDialog { UseDescriptionForTitle = true, Description = S(p, "title", GrapekitApp.Lang == 0 ? "Klasör seçin" : "Choose a folder"), ShowNewFolderButton = true };
                if (S(p, "dir") is { Length: > 0 } dir && Directory.Exists(dir)) d.InitialDirectory = dir;
                return d.ShowDialog(this) == DialogResult.OK ? d.SelectedPath : null;
            }

            // ---------- İşler ----------
            case "job.start": return Jobs.Start(p);
            case "job.cancel": Jobs.Cancel(S(p, "id")); return true;
            case "job.pause": Jobs.Pause(S(p, "id"), B(p, "paused")); return true;
            case "job.busy": return Jobs.Busy;

            // ---------- Kabuk ----------
            case "shell.open": Process.Start(new ProcessStartInfo(S(p, "path")) { UseShellExecute = true }); return true;
            case "shell.reveal":
            {
                var path = S(p, "path");
                if (Directory.Exists(path) && !B(p, "select")) Process.Start("explorer.exe", $"\"{path}\"");
                else Process.Start("explorer.exe", $"/select,\"{path}\"");
                return true;
            }
            case "shell.url":
            {
                var url = S(p, "which") switch
                {
                    "page" => GrapekitApp.PageUrl, "feedback" => GrapekitApp.FeedbackUrl, "coffee" => Brand.Support, "privacy" => Brand.PrivacyUrl,
                    _ => S(p, "url"),
                };
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) Brand.Open(url);
                return true;
            }
            case "shell.explorerMenu": Shell.SetMenu(B(p, "enabled")); return Shell.MenuEnabled();
            case "shell.licenses":
            {
                var f = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
                if (File.Exists(f)) Process.Start(new ProcessStartInfo(f) { UseShellExecute = true });
                return true;
            }

            // ---------- Pencere ----------
            case "window.min": WindowState = FormWindowState.Minimized; return true;
            case "window.max": WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; return true;
            case "window.fullscreen": SetFullscreen(p.ContainsKey("on") ? B(p, "on") : null); return true;
            case "window.theme":
                _edge = B(p, "dark") ? Color.FromArgb(0x1B, 0x10, 0x14) : Color.FromArgb(0xFF, 0xF7, 0xF3);
                BackColor = _edge; _web.DefaultBackgroundColor = _edge;
                return true;
            case "window.flash": FlashWindow(); return true;
            case "notify": ShowNotification(S(p, "title"), S(p, "text"), S(p, "path")); return true;

            // ---------- Güncelleme ----------
            case "update.check":
                if (_updates != null) await _updates.CheckAsync(manual: true);
                return UpdateInfo(false);
            case "update.install": if (_updates != null) _ = _updates.InstallAsync(); return true;

            default: throw new BridgeException("unknown", "Bilinmeyen işlem: " + method);
        }
    }

    // ===================== Dosya listesi =====================
    static readonly string[] SkipDirs = ["node_modules", ".git", "$RECYCLE.BIN", "System Volume Information"];

    /// <summary>Dosya ve klasörleri desteklenen dosyalara açar. rel: klasörün içindeki göreli yol (yapıyı korumak için).</summary>
    static JsonArray Expand(IEnumerable<string> paths, bool recursive)
    {
        var list = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void add(string f, string rel)
        {
            if (list.Count >= 20000 || !seen.Add(f) || !Formats.Supported(f)) return;
            long size = 0; try { size = new FileInfo(f).Length; } catch { }
            list.Add(new JsonObject { ["path"] = f, ["rel"] = rel, ["size"] = size, ["kind"] = Formats.KindOf(f).ToString().ToLowerInvariant(), ["ext"] = Formats.Ext(f) });
        }
        foreach (var p in paths)
        {
            if (File.Exists(p)) { add(p, Path.GetFileName(p)); continue; }
            if (!Directory.Exists(p)) continue;
            var root = Path.GetFullPath(p).TrimEnd('\\');
            var rootName = Path.GetFileName(root);
            var stack = new Stack<string>(); stack.Push(root);
            while (stack.Count > 0 && list.Count < 20000)
            {
                var d = stack.Pop();
                string[] fs = [], ds = [];
                try { fs = Directory.GetFiles(d); ds = recursive ? Directory.GetDirectories(d) : []; } catch { }
                foreach (var f in fs.Order(StringComparer.CurrentCultureIgnoreCase))
                {
                    try { if ((File.GetAttributes(f) & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue; } catch { continue; }
                    if (Path.GetFileName(f).Contains(".formaty-")) continue; // yarım kalmış geçici dosya
                    add(f, Path.Combine(rootName, Path.GetRelativePath(root, f)));
                }
                foreach (var sd in ds.Where(x => !SkipDirs.Contains(Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)).OrderDescending(StringComparer.CurrentCultureIgnoreCase))
                {
                    try { if ((File.GetAttributes(sd) & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue; } catch { continue; }
                    stack.Push(sd);
                }
            }
        }
        return list;
    }

    static async Task<JsonObject> InfoAsync(string path)
    {
        var r = new JsonObject { ["path"] = path };
        try
        {
            switch (Formats.KindOf(path))
            {
                case Kind.Image:
                {
                    var (w, h, pages) = await Task.Run(() => Images.Header(path));
                    r["width"] = w; r["height"] = h; r["frames"] = pages;
                    if (w == 0 && Ffmpeg.Available) { var mi = await Ffmpeg.InfoAsync(path); r["width"] = mi.Width; r["height"] = mi.Height; }
                    break;
                }
                case Kind.Video: case Kind.Audio:
                {
                    if (!Ffmpeg.Available) break;
                    var mi = await Ffmpeg.InfoAsync(path);
                    r["duration"] = Math.Round(mi.Duration, 3); r["width"] = mi.Width; r["height"] = mi.Height; r["fps"] = Math.Round(mi.Fps, 3);
                    r["hasVideo"] = mi.HasVideo; r["hasAudio"] = mi.HasAudio; r["vcodec"] = mi.VideoCodec; r["acodec"] = mi.AudioCodec;
                    r["channels"] = mi.Channels; r["sampleRate"] = mi.SampleRate; r["bitrate"] = mi.Bitrate; r["rotation"] = mi.Rotation;
                    break;
                }
                case Kind.Pdf:
                {
                    var (pages, enc) = await Task.Run(() => PdfOps.Info(path));
                    r["pages"] = pages; r["encrypted"] = enc;
                    break;
                }
            }
        }
        catch (Exception ex) { r["error"] = ex.Message; }
        return r;
    }

    // ===================== Küçük resimler =====================
    static readonly SemaphoreSlim ThumbGate = new(Math.Max(2, Environment.ProcessorCount / 2));

    async Task<string?> ThumbAsync(string path, int size)
    {
        if (!File.Exists(path)) return null;
        var fi = new FileInfo(path);
        var key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{path.ToLowerInvariant()}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}|{size}")))[..20].ToLowerInvariant();
        var name = key + ".jpg";
        var file = Path.Combine(ThumbDir, name);
        if (File.Exists(file)) return "https://thumb.grapekit.local/" + name;
        await ThumbGate.WaitAsync();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var tmp = Path.Combine(ThumbDir, key + ".tmp.jpg");
            switch (Formats.KindOf(path))
            {
                case Kind.Image: await Images.ThumbAsync(path, tmp, size, ThumbDir, cts.Token); break;
                case Kind.Video:
                {
                    if (!Ffmpeg.Available) return null;
                    var mi = await Ffmpeg.InfoAsync(path, cts.Token);
                    if (!mi.HasVideo) return null;
                    await Media.ThumbAsync(path, tmp, size, Math.Min(1.5, mi.Duration / 3), cts.Token);
                    break;
                }
                case Kind.Pdf:
                {
                    await Task.Run(() =>
                    {
                        using var doc = Pdfium.Open(path);
                        var (bgra, w, h, _) = doc.Render(0, 150, size * 2);
                        using var raw = NetVips.Image.NewFromMemory(bgra, w, h, 4, NetVips.Enums.BandFormat.Uchar);
                        using var rgb = raw[2].Bandjoin(raw[1], raw[0]).Copy(interpretation: NetVips.Enums.Interpretation.Srgb);
                        using var t = rgb.ThumbnailImage(size, height: size);
                        t.Jpegsave(tmp, q: 80);
                    }, cts.Token);
                    break;
                }
                default: return null;
            }
            if (!File.Exists(tmp)) return null;
            File.Move(tmp, file, true);
            return "https://thumb.grapekit.local/" + name;
        }
        catch { return null; }
        finally { ThumbGate.Release(); }
    }

    // ===================== Yardımcılar =====================
    bool TR => GrapekitApp.Lang == 0;

    string FileFilter(string kind)
    {
        string all = TR ? "Tüm dosyalar" : "All files";
        string img = "*.jpg;*.jpeg;*.png;*.webp;*.avif;*.heic;*.heif;*.gif;*.tif;*.tiff;*.bmp;*.ico;*.svg;*.jxl;*.jp2;*.tga;*.psd;*.dds;*.qoi";
        string vid = "*.mp4;*.m4v;*.mkv;*.mov;*.avi;*.webm;*.wmv;*.flv;*.mts;*.m2ts;*.ts;*.3gp;*.mpg;*.mpeg;*.vob;*.ogv";
        string aud = "*.mp3;*.aac;*.m4a;*.wav;*.flac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.amr;*.ac3;*.ape;*.wv";
        return kind switch
        {
            "image" => $"{(TR ? "Fotoğraflar" : "Images")}|{img}|{all}|*.*",
            "video" => $"{(TR ? "Videolar" : "Videos")}|{vid}|{all}|*.*",
            "audio" => $"{(TR ? "Ses dosyaları" : "Audio")}|{aud}|{all}|*.*",
            "pdf" => $"PDF|*.pdf|{all}|*.*",
            _ => $"{(TR ? "Desteklenen dosyalar" : "Supported files")}|{img};{vid};{aud};*.pdf|{(TR ? "Fotoğraflar" : "Images")}|{img}|{(TR ? "Videolar" : "Videos")}|{vid}|{(TR ? "Ses" : "Audio")}|{aud}|PDF|*.pdf|{all}|*.*",
        };
    }

    // Bitiş bildirimi: pencere öndeyse gösterilmez. Tıklanınca çıktıyı Gezgin'de gösterir.
    NotifyIcon? _notify;
    void ShowNotification(string title, string text, string path)
    {
        if (ContainsFocus && WindowState != FormWindowState.Minimized) return;
        _notify?.Dispose();
        var n = new NotifyIcon { Icon = AppIcon.Get(16), Text = GrapekitApp.Name, Visible = true };
        n.BalloonTipClicked += (_, _) =>
        {
            if (path.Length > 0 && (File.Exists(path) || Directory.Exists(path))) Process.Start("explorer.exe", File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"");
            else { if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); }
            n.Visible = false;
        };
        n.BalloonTipClosed += (_, _) => n.Visible = false;
        n.ShowBalloonTip(8000, title, text, ToolTipIcon.Info);
        _notify = n;
        // Bildirim alanında simge kalmasın
        var t = new System.Windows.Forms.Timer { Interval = 15000 };
        t.Tick += (_, _) => { t.Dispose(); if (_notify == n) { n.Visible = false; n.Dispose(); _notify = null; } };
        t.Start();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool FlashWindow(IntPtr h, bool invert);
    void FlashWindow() { if (!ContainsFocus) FlashWindow(Handle, true); }

    // ===================== Güncelleme =====================
    static string UpdateStamp => Path.Combine(SettingsFile.Dir, "update-check.txt");

    void StartUpdates()
    {
        if (!Updater.Supported) return;
        static DateTime Last() { try { return DateTime.Parse(File.ReadAllText(UpdateStamp), null, System.Globalization.DateTimeStyles.RoundtripKind); } catch { return default; } }
        _updates = new UpdateManager(
            Last,
            d => { try { Store.WriteAtomic(UpdateStamp, d.ToString("o")); } catch { } },
            () => Store.Bool("autoUpdate", true));
        _updates.Changed += () => Emit("update-status", UpdateInfo(false));
        _updates.Progress += () => Emit("update-status", UpdateInfo(false));
        _updates.Found += _ => Emit("update-status", UpdateInfo(true));
        // Açılışta da denetle (son denetim 6 saatten eskiyse): yeni sürüm ayarlar simgesindeki rozetle hemen görünsün
        if (Store.Bool("autoUpdate", true) && (DateTime.UtcNow - Last()).TotalHours >= 6) _ = _updates.CheckAsync(manual: false);
    }

    JsonObject UpdateInfo(bool notify) => new()
    {
        ["updateStatus"] = _updates?.StatusText ?? "", ["updateAvailable"] = _updates?.Available != null,
        ["updateLabel"] = _updates?.Available is { } a ? string.Format(CoreText.T("upd_install"), a.Version.ToString(3)) : "",
        ["notify"] = notify,
    };
}

internal static class KnownFolders
{
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out string path);

    public static string Downloads
    {
        get
        {
            var g = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            try { return SHGetKnownFolderPath(ref g, 0, IntPtr.Zero, out var p) == 0 ? p : ""; } catch { return ""; }
        }
    }
}
