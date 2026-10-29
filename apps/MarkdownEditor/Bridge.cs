using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MarkdownEditor;

/// <summary>Arayüzden gelen çağrılar (yöntem adı → işlem).</summary>
internal sealed partial class MainForm
{
    static readonly string[] MdExt = [".md", ".markdown", ".mdown", ".mkd", ".mkdn", ".mdx", ".txt", ".text"];
    static readonly string[] ImgExt = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".avif"];
    static readonly string[] SkipDirs = ["node_modules", ".git", ".svn", ".hg", "bin", "obj", ".vs", ".idea", "__pycache__"];

    FileSystemWatcher? _watcher;
    readonly Dictionary<string, FileSystemWatcher> _fileWatchers = new(StringComparer.OrdinalIgnoreCase);
    HashSet<string> _watched = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, System.Threading.Timer> _debounce = new(StringComparer.OrdinalIgnoreCase);
    UpdateManager? _updates;
    readonly Dictionary<string, TaskCompletionSource<string?>> _cli = [];

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
                    ["args"] = Arr(_startupFiles), ["dark"] = !ThemeInfo.AppsUseLight(), ["explorerMenu"] = Shell.ExplorerMenuEnabled(),
                    ["updateStatus"] = _updates?.StatusText ?? "", ["headless"] = Headless,
                };
            case "app.ready":
                _ready = true;
                _readyTcs.TrySetResult();
                if (!Headless) { Opacity = 1; if (_updates == null) StartUpdates(); }
                return true;
            case "app.lang":
                GrapekitApp.Lang = S(p, "lang") == "en" ? 1 : 0;
                Text = GrapekitApp.Name;
                return true;
            case "app.log": ErrorLog.Write(new Exception("Arayüz: " + S(p, "message") + Environment.NewLine + S(p, "stack"))); return true;
            case "app.exit": _allowClose = true; BeginInvoke(Close); return true;

            // ---------- Ayarlar, oturum, kurtarma ----------
            case "settings.load": return Store.LoadSettings();
            case "settings.save": Store.SaveSettings(p["settings"]?.DeepClone()); return true;
            case "session.load": return Store.LoadSession();
            case "session.save": Store.SaveSession(p.DeepClone()); return true;
            case "recovery.save": Store.SaveRecovery(p["items"]?.AsArray() ?? []); return true;
            case "recovery.list": return Store.ListRecovery();
            case "recovery.clear": Store.ClearRecovery(); return true;
            case "recent.list": return Store.RecentList();
            case "recent.add": Store.AddRecent(S(p, "path")); Emit("recent", Store.RecentList()); return true;
            case "recent.remove": Store.RemoveRecent(S(p, "path")); Emit("recent", Store.RecentList()); return true;
            case "recent.pin": Store.PinRecent(S(p, "path"), B(p, "pinned")); Emit("recent", Store.RecentList()); return true;

            // ---------- Dosyalar ----------
            case "file.read":
            {
                var r = await Task.Run(() => TextFiles.Read(S(p, "path")));
                return new JsonObject { ["text"] = r.Text, ["encoding"] = r.Encoding, ["bom"] = r.Bom, ["eol"] = r.Eol, ["mtime"] = r.Mtime, ["size"] = r.Size };
            }
            case "file.write":
            {
                var path = S(p, "path");
                var enc = S(p, "encoding", "utf-8");
                var bom = B(p, "bom") || enc == "utf-8-bom";
                var mtime = await Task.Run(() => TextFiles.Write(path, S(p, "text"), enc == "utf-8-bom" ? "utf-8" : enc, bom));
                return new JsonObject { ["mtime"] = mtime };
            }
            case "file.readBase64":
            {
                var path = S(p, "path");
                var fi = new FileInfo(path);
                if (!fi.Exists) throw new FileNotFoundException(path);
                if (fi.Length > 40L * 1024 * 1024) throw new BridgeException("too-big", "Dosya çok büyük");
                var bytes = await File.ReadAllBytesAsync(path);
                return new JsonObject { ["data"] = Convert.ToBase64String(bytes), ["mime"] = Mime(path) };
            }
            case "file.writeBase64":
            {
                var path = S(p, "path");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, Convert.FromBase64String(S(p, "data")));
                return true;
            }
            case "file.stat":
            {
                var path = S(p, "path");
                return new JsonObject { ["exists"] = File.Exists(path), ["mtime"] = File.Exists(path) ? TextFiles.Mtime(path) : 0 };
            }
            case "file.create":
            {
                var path = Path.Combine(S(p, "dir"), SafeName(S(p, "name")));
                if (File.Exists(path)) throw new BridgeException("exists", GrapekitApp.Lang == 0 ? "Bu adla bir dosya zaten var." : "A file with this name already exists.");
                await File.WriteAllTextAsync(path, "");
                return new JsonObject { ["path"] = path };
            }
            case "dir.create":
            {
                var path = Path.Combine(S(p, "dir"), SafeName(S(p, "name")));
                Directory.CreateDirectory(path);
                return new JsonObject { ["path"] = path };
            }
            case "dir.ensure": Directory.CreateDirectory(S(p, "path")); return true;
            case "fs.rename":
            {
                var src = S(p, "path");
                var dst = Path.Combine(Path.GetDirectoryName(src.TrimEnd('\\'))!, SafeName(S(p, "name")));
                if (Directory.Exists(src)) Directory.Move(src, dst); else File.Move(src, dst);
                return new JsonObject { ["path"] = dst };
            }
            case "fs.recycle": Native.Recycle(S(p, "path")); return true;
            case "file.openDialog": return OpenDialog(S(p, "kind", "md"), B(p, "multi"));
            case "file.saveDialog": return SaveDialog(S(p, "kind", "md"), S(p, "name"), S(p, "dir"));
            case "folder.dialog":
            {
                using var d = new FolderBrowserDialog { UseDescriptionForTitle = true, Description = GrapekitApp.Lang == 0 ? "Klasör seçin" : "Choose a folder", ShowNewFolderButton = true };
                return d.ShowDialog(this) == DialogResult.OK ? d.SelectedPath : null;
            }
            case "file.watch": WatchFiles(p["paths"]?.AsArray().Select(x => x!.GetValue<string>()) ?? []); return true;
            case "folder.watch": WatchFolder(S(p, "path")); return true;
            case "folder.list": return ListFolder(S(p, "path"));
            case "folder.files": return Arr(await Task.Run(() => MdFiles(S(p, "path"), B(p, "recursive", true)).ToList()));
            case "folder.search": return await Task.Run(() => Search(S(p, "path"), S(p, "query"), B(p, "regex"), B(p, "matchCase")));
            case "drop.paths": return Arr(dropped ?? []);
            case "temp.path":
            {
                var dir = Path.Combine(Path.GetTempPath(), "GrapekitMarkdown", "onizleme");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, SafeName(S(p, "name", "onizleme.pdf")));
            }

            // ---------- Yollar ve görseller ----------
            case "docbase": return DocHosts.BaseFor(S(p, "dir"));
            case "path.relative": return Path.GetRelativePath(S(p, "from"), S(p, "to")).Replace('\\', '/');
            case "path.resolve": return Path.GetFullPath(Path.Combine(S(p, "dir"), S(p, "rel").Replace('/', '\\')));
            case "image.save":
            {
                var folder = Path.Combine(S(p, "docDir"), S(p, "folder", "resimler"));
                Directory.CreateDirectory(folder);
                var ext = Regex.Replace(S(p, "ext", "png").ToLowerInvariant(), "[^a-z0-9]", "");
                var baseName = S(p, "name") is { Length: > 0 } n ? Path.GetFileNameWithoutExtension(SafeName(n)) : $"gorsel-{DateTime.Now:yyyyMMdd-HHmmss}";
                var path = Unique(Path.Combine(folder, baseName + "." + (ext.Length > 0 ? ext : "png")));
                await File.WriteAllBytesAsync(path, Convert.FromBase64String(S(p, "data")));
                return new JsonObject { ["rel"] = Path.GetRelativePath(S(p, "docDir"), path).Replace('\\', '/'), ["path"] = path };
            }
            case "image.import":
            {
                var src = S(p, "source"); var docDir = S(p, "docDir");
                // Zaten belgenin klasöründeyse kopyalama
                if (Path.GetFullPath(src).StartsWith(Path.GetFullPath(docDir) + "\\", StringComparison.OrdinalIgnoreCase))
                    return new JsonObject { ["rel"] = Path.GetRelativePath(docDir, src).Replace('\\', '/') };
                var folder = Path.Combine(docDir, S(p, "folder", "resimler"));
                Directory.CreateDirectory(folder);
                var dst = Unique(Path.Combine(folder, Path.GetFileName(src)));
                File.Copy(src, dst);
                return new JsonObject { ["rel"] = Path.GetRelativePath(docDir, dst).Replace('\\', '/') };
            }

            // ---------- Dışa aktarma ----------
            case "pdf.export":
                await _print!.Pdf(S(p, "html"), p["options"]?.AsObject() ?? [], S(p, "path"), DocDirs(p));
                return true;
            case "png.export":
                await _print!.Png(S(p, "html"), S(p, "path"), (int)N(p, "width", 900), N(p, "scale", 2), DocDirs(p));
                return true;
            case "render.capture": return await _print!.Capture(S(p, "html"), (int)N(p, "count"));
            case "print": await _print!.Print(S(p, "html"), DocDirs(p)); return true;
            case "pdf.view": new PdfViewer(_env!, S(p, "path"), S(p, "title"), this).Show(this); return true;

            // ---------- Kabuk ----------
            case "shell.open": Process.Start(new ProcessStartInfo(S(p, "path")) { UseShellExecute = true }); return true;
            case "shell.reveal": Process.Start("explorer.exe", $"/select,\"{S(p, "path")}\""); return true;
            case "shell.url":
            {
                var which = S(p, "which");
                var url = which switch
                {
                    "page" => GrapekitApp.PageUrl, "feedback" => GrapekitApp.FeedbackUrl, "coffee" => Brand.Support, "privacy" => Brand.PrivacyUrl,
                    _ => S(p, "url"),
                };
                if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                else Brand.Open(url.StartsWith("http://") ? "https://" + url[7..] : url);
                return true;
            }
            case "shell.defaultApps": Shell.OpenDefaultApps(); return true;
            case "shell.explorerMenu": Shell.SetExplorerMenu(B(p, "enabled")); return Shell.ExplorerMenuEnabled();

            // ---------- Pencere ----------
            case "window.min": WindowState = FormWindowState.Minimized; return true;
            case "window.max": WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; return true;
            case "window.fullscreen": SetFullscreen(p.ContainsKey("on") ? B(p, "on") : null); return true;
            case "window.theme":
                _edge = B(p, "dark") ? Color.FromArgb(0x12, 0x07, 0x26) : Color.FromArgb(0x1A, 0x0B, 0x33);
                BackColor = _edge; _web.DefaultBackgroundColor = _edge;
                return true;

            // ---------- Güncelleme ----------
            case "update.check":
                if (_updates != null) await _updates.CheckAsync(manual: true);
                return UpdateInfo(false);
            case "update.install": if (_updates != null) _ = _updates.InstallAsync(); return true;

            // ---------- Komut satırı ----------
            case "cli.done":
            {
                var cid = S(p, "id");
                if (_cli.Remove(cid, out var tcs)) tcs.TrySetResult(B(p, "ok") ? null : S(p, "error", "hata"));
                return true;
            }
            default: throw new BridgeException("unknown", "Bilinmeyen işlem: " + method);
        }
    }

    static IEnumerable<string> DocDirs(JsonObject p) => p["docDirs"]?.AsArray().Select(x => x!.GetValue<string>()).Where(Directory.Exists) ?? [];

    static string SafeName(string n)
    {
        n = string.Concat(n.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim().TrimEnd('.');
        if (n.Length == 0) throw new BridgeException("name", GrapekitApp.Lang == 0 ? "Geçersiz ad." : "Invalid name.");
        return n;
    }

    static string Unique(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!; var name = Path.GetFileNameWithoutExtension(path); var ext = Path.GetExtension(path);
        for (int i = 2; ; i++) { var p2 = Path.Combine(dir, $"{name}-{i}{ext}"); if (!File.Exists(p2)) return p2; }
    }

    static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".svg" => "image/svg+xml",
        ".bmp" => "image/bmp", ".avif" => "image/avif", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream",
    };

    // ===================== Diyaloglar =====================
    bool TR => GrapekitApp.Lang == 0;
    string MdFilter => TR ? "Markdown dosyaları|*.md;*.markdown;*.mdown;*.mkd;*.mkdn;*.mdx|Metin dosyaları|*.txt|Tüm dosyalar|*.*" : "Markdown files|*.md;*.markdown;*.mdown;*.mkd;*.mkdn;*.mdx|Text files|*.txt|All files|*.*";

    JsonNode? OpenDialog(string kind, bool multi)
    {
        using var d = new OpenFileDialog { Multiselect = multi, CheckFileExists = true, RestoreDirectory = true };
        d.Filter = kind switch
        {
            "image" => (TR ? "Görseller" : "Images") + "|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.svg;*.bmp|" + (TR ? "Tüm dosyalar" : "All files") + "|*.*",
            "import" => (TR ? "HTML ve Word" : "HTML and Word") + "|*.html;*.htm;*.docx|HTML|*.html;*.htm|Word|*.docx",
            _ => MdFilter,
        };
        return d.ShowDialog(this) == DialogResult.OK ? Arr(d.FileNames) : null;
    }

    JsonNode? SaveDialog(string kind, string name, string dir)
    {
        using var d = new SaveFileDialog { FileName = name, OverwritePrompt = true, AddExtension = true, RestoreDirectory = true };
        if (Directory.Exists(dir)) d.InitialDirectory = dir;
        (d.Filter, d.DefaultExt) = kind switch
        {
            "pdf" => ("PDF|*.pdf", "pdf"),
            "html" => ("HTML|*.html;*.htm", "html"),
            "docx" => ("Word|*.docx", "docx"),
            "png" => ("PNG|*.png", "png"),
            _ => (MdFilter, "md"),
        };
        return d.ShowDialog(this) == DialogResult.OK ? d.FileName : null;
    }

    // ===================== Klasör =====================
    static JsonObject ListFolder(string dir)
    {
        var dirs = new JsonArray(); var files = new JsonArray();
        try
        {
            var di = new DirectoryInfo(dir);
            foreach (var d in di.EnumerateDirectories().Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0 && !SkipDirs.Contains(d.Name, StringComparer.OrdinalIgnoreCase)).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
                dirs.Add(d.FullName);
            foreach (var f in di.EnumerateFiles().Where(f => (f.Attributes & FileAttributes.Hidden) == 0 && (MdExt.Contains(f.Extension, StringComparer.OrdinalIgnoreCase) || ImgExt.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
                files.Add(f.FullName);
        }
        catch { }
        return new JsonObject { ["dirs"] = dirs, ["files"] = files };
    }

    static IEnumerable<string> MdFiles(string dir, bool recursive)
    {
        var stack = new Stack<string>(); stack.Push(dir);
        int count = 0;
        while (stack.Count > 0 && count < 20000)
        {
            var d = stack.Pop();
            string[] fs = [], ds = [];
            try { fs = Directory.GetFiles(d); ds = recursive ? Directory.GetDirectories(d) : []; } catch { }
            foreach (var f in fs.Where(f => MdExt.Take(6).Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).Order(StringComparer.CurrentCultureIgnoreCase)) { count++; yield return f; }
            foreach (var sd in ds.Where(x => !SkipDirs.Contains(Path.GetFileName(x), StringComparer.OrdinalIgnoreCase) && !Path.GetFileName(x).StartsWith('.')).Reverse()) stack.Push(sd);
        }
    }

    static JsonArray Search(string dir, string query, bool regex, bool matchCase)
    {
        var result = new JsonArray();
        if (string.IsNullOrWhiteSpace(query) || !Directory.Exists(dir)) return result;
        Regex re;
        try { re = new Regex(regex ? query : Regex.Escape(query), (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), TimeSpan.FromMilliseconds(200)); }
        catch (ArgumentException) { return result; }
        int total = 0;
        foreach (var f in MdFiles(dir, true))
        {
            if (total >= 1000 || result.Count >= 300) break;
            string[] lines;
            try { if (new FileInfo(f).Length > 5_000_000) continue; lines = File.ReadAllLines(f); } catch { continue; }
            var hits = new JsonArray();
            for (int i = 0; i < lines.Length && hits.Count < 50; i++)
            {
                Match m;
                try { m = re.Match(lines[i]); } catch (RegexMatchTimeoutException) { break; }
                if (!m.Success || m.Length == 0) continue;
                var line = lines[i];
                int s0 = Math.Max(0, m.Index - 40);
                hits.Add(new JsonObject
                {
                    ["line"] = i + 1, ["col"] = m.Index + 1,
                    ["before"] = (s0 > 0 ? "…" : "") + line[s0..m.Index].TrimStart(), ["match"] = m.Value, ["after"] = line[(m.Index + m.Length)..Math.Min(line.Length, m.Index + m.Length + 80)],
                });
                total++;
            }
            if (hits.Count > 0) result.Add(new JsonObject { ["path"] = f, ["hits"] = hits });
        }
        return result;
    }

    // ===================== Dosya izleme =====================
    void WatchFolder(string dir)
    {
        _watcher?.Dispose(); _watcher = null;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        _watcher = new FileSystemWatcher(dir) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, EnableRaisingEvents = true };
        void changed(string? path)
        {
            if (path == null) return;
            var parent = Path.GetDirectoryName(path) ?? dir;
            Debounce("dir:" + parent, 400, () => Emit("folder-changed", new JsonObject { ["path"] = parent }));
        }
        _watcher.Created += (_, e) => changed(e.FullPath);
        _watcher.Deleted += (_, e) => changed(e.FullPath);
        _watcher.Renamed += (_, e) => { changed(e.OldFullPath); changed(e.FullPath); };
    }

    void WatchFiles(IEnumerable<string> paths)
    {
        _watched = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        var dirs = _watched.Select(p => Path.GetDirectoryName(p)!).Where(Directory.Exists).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var d in _fileWatchers.Keys.Where(k => !dirs.Contains(k)).ToList()) { _fileWatchers[d].Dispose(); _fileWatchers.Remove(d); }
        foreach (var d in dirs.Where(d => !_fileWatchers.ContainsKey(d)))
        {
            var w = new FileSystemWatcher(d) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size, EnableRaisingEvents = true };
            w.Changed += (_, e) => FileEvent(e.FullPath, "changed");
            w.Created += (_, e) => FileEvent(e.FullPath, "changed");
            w.Deleted += (_, e) => FileEvent(e.FullPath, "deleted");
            w.Renamed += (_, e) => { FileEvent(e.OldFullPath, File.Exists(e.OldFullPath) ? "changed" : "deleted"); FileEvent(e.FullPath, "changed"); };
            _fileWatchers[d] = w;
        }
    }

    void FileEvent(string path, string kind)
    {
        if (!_watched.Contains(path)) return;
        // Kaydetme "geçici dosya + değiştir" yaptığı için olaylar art arda gelir: biraz bekle, son durumu bildir
        Debounce("file:" + path, 350, () => Emit("file-changed", new JsonObject { ["path"] = path, ["kind"] = File.Exists(path) ? "changed" : kind }));
    }

    void Debounce(string key, int ms, Action action)
    {
        lock (_debounce)
        {
            if (_debounce.TryGetValue(key, out var t)) t.Dispose();
            _debounce[key] = new System.Threading.Timer(_ =>
            {
                lock (_debounce) { if (_debounce.Remove(key, out var x)) x.Dispose(); }
                try { BeginInvoke(action); } catch { }
            }, null, ms, Timeout.Infinite);
        }
    }

    // ===================== Güncelleme =====================
    static string UpdateStamp => Path.Combine(SettingsFile.Dir, "update-check.txt");

    void StartUpdates()
    {
        if (!Updater.Supported) return;
        _updates = new UpdateManager(
            () => { try { return DateTime.Parse(File.ReadAllText(UpdateStamp), null, System.Globalization.DateTimeStyles.RoundtripKind); } catch { return default; } },
            d => { try { Store.WriteAtomic(UpdateStamp, d.ToString("o")); } catch { } },
            () => Store.Bool("autoUpdate", true));
        _updates.Changed += () => Emit("update-status", UpdateInfo(false));
        _updates.Progress += () => Emit("update-status", UpdateInfo(false));
        _updates.Found += _ => Emit("update-status", UpdateInfo(true));
    }

    JsonObject UpdateInfo(bool notify) => new()
    {
        ["updateStatus"] = _updates?.StatusText ?? "", ["updateAvailable"] = _updates?.Available != null,
        ["updateLabel"] = _updates?.Available is { } a ? string.Format(CoreText.T("upd_install"), a.Version.ToString(3)) : "",
        ["notify"] = notify,
    };

    // ===================== Komut satırı: --to-pdf =====================
    public async Task<string?> ConvertToPdf(string input, string? output)
    {
        await WhenReady;
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<string?>();
        _cli[id] = tcs;
        Emit("cli-pdf", new JsonObject { ["input"] = input, ["output"] = output, ["id"] = id });
        return await tcs.Task.WaitAsync(TimeSpan.FromMinutes(3));
    }
}
