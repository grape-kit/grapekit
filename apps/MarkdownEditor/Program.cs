using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace MarkdownEditor;

internal static class Program
{
    // Ayrı ayar klasörüyle (GRAPEKIT_SETTINGS_DIR) açılan deneme kopyası kurulu kopyayla çakışmasın
    static readonly string Suffix = (Environment.GetEnvironmentVariable("GRAPEKIT_SETTINGS_DIR") is { Length: > 0 } ? ".Deneme" : "") + "." + Environment.UserName;
    static readonly string MutexName = @"Local\MarkdownEditor.Tekil" + Suffix;
    static readonly string PipeName = "Grapekit.MarkdownEditor" + Suffix;

    sealed record Request(string[] Files, string? PdfInput, string? PdfOutput, string Format = "pdf");

    [STAThread]
    static int Main(string[] args)
    {
        GrapekitApp.Init("markdown-editor", "MarkdownEditor", ["Markdown Editörü", "Markdown Studio", "Markdown Studio"], "markdown-editoru", "markdown-studio");
        GrapekitApp.Lang = LoadLang();

        if (args.Contains("--unregister")) { Shell.Unregister(); return 0; }
        // Dosya Gezgini önizleme bölmesi: COM bu exe'yi "-Embedding" ile başlatır (tek kopya kuralı dışında)
        if (args.Any(a => a.Equals("-Embedding", StringComparison.OrdinalIgnoreCase) || a.Equals("/Embedding", StringComparison.OrdinalIgnoreCase)))
            return PreviewServer.Run();

        // Komut satırından dönüştürme: --to-pdf | --to-html | --to-docx | --to-png  girdi.md [çıktı]
        string? pdfIn = null, pdfOut = null, format = "pdf";
        int toPdf = -1;
        foreach (var f in new[] { "pdf", "html", "docx", "png" }) { int i = Array.IndexOf(args, "--to-" + f); if (i >= 0) { toPdf = i; format = f; break; } }
        if (toPdf >= 0 && toPdf + 1 < args.Length) { pdfIn = Path.GetFullPath(args[toPdf + 1]); if (toPdf + 2 < args.Length && !args[toPdf + 2].StartsWith("--")) pdfOut = Path.GetFullPath(args[toPdf + 2]); }
        var files = toPdf >= 0 ? [] : args.Where(a => !a.StartsWith("--") && File.Exists(a)).Select(Path.GetFullPath).ToList();

        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // Zaten açık: isteği çalışan kopyaya ilet
            var reply = Send(new Request([.. files], pdfIn, pdfOut, format));
            if (reply == null && pdfIn == null) return 0;
            return reply == "ok" || reply == null ? 0 : 1;
        }

        ApplicationConfiguration.Initialize();
        ErrorLog.Install();
        // Deneme kopyası (ayrı ayar klasörü) kullanıcının dosya ilişkilendirmelerine dokunmaz
        if (!Suffix.StartsWith(".Deneme")) Shell.RegisterFileTypes();

        var form = new MainForm(files) { Headless = pdfIn != null };
        if (pdfIn != null)
        {
            // Pencere göstermeden dönüştür, bitince çık
            form.Opacity = 0; form.ShowInTaskbar = false;
            form.Shown += async (_, _) =>
            {
                var err = await form.ConvertToPdf(pdfIn, pdfOut, format);
                if (err != null) MessageBox.Show(err, GrapekitApp.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Environment.ExitCode = err == null ? 0 : 1;
                form.ForceClose();
            };
        }
        else form.Opacity = 0; // arayüz hazır olunca görünür (beyaz parlama olmasın)

        StartPipeServer(form);
        Application.Run(form);
        return Environment.ExitCode;
    }

    static int LoadLang()
    {
        var l = Store.Str("lang", "");
        return l == "en" ? 1 : l == "tr" ? 0 : CoreText.SystemLang();
    }

    static string? Send(Request r)
    {
        try
        {
            using var c = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            c.Connect(3000);
            Native.AllowSetForegroundWindow(-1);
            var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r) + "\n");
            c.Write(data);
            c.Flush();
            using var rd = new StreamReader(c, Encoding.UTF8);
            return rd.ReadLine();
        }
        catch { return null; }
    }

    static void StartPipeServer(MainForm form)
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var s = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
                    s.WaitForConnection();
                    using var rd = new StreamReader(s, Encoding.UTF8, leaveOpen: true);
                    var line = rd.ReadLine();
                    var r = line == null ? null : JsonSerializer.Deserialize<Request>(line);
                    string answer = "ok";
                    if (r != null)
                    {
                        if (r.PdfInput != null)
                        {
                            var task = (Task<string?>)form.Invoke(() => form.ConvertToPdf(r.PdfInput, r.PdfOutput, r.Format));
                            var err = task.GetAwaiter().GetResult();
                            answer = err == null ? "ok" : "error: " + err;
                        }
                        else form.BeginInvoke(() => form.OpenFromOtherInstance([.. r.Files]));
                    }
                    var bytes = Encoding.UTF8.GetBytes(answer + "\n");
                    s.Write(bytes); s.Flush();
                    try { s.WaitForPipeDrain(); } catch { }
                }
                catch (Exception ex) { ErrorLog.Write(ex); Thread.Sleep(500); }
            }
        }) { IsBackground = true, Name = "pipe" };
        t.Start();
    }
}
