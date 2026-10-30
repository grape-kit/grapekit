using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Formaty;

internal static class Program
{
    // Ayrı ayar klasörüyle (GRAPEKIT_SETTINGS_DIR) açılan deneme kopyası kurulu kopyayla çakışmasın
    static readonly string Suffix = (Environment.GetEnvironmentVariable("GRAPEKIT_SETTINGS_DIR") is { Length: > 0 } ? ".Deneme" : "") + "." + Environment.UserName;
    static readonly string MutexName = @"Local\Formaty.Tekil" + Suffix;
    static readonly string PipeName = "Grapekit.Formaty" + Suffix;
    public static bool TestCopy => Suffix.StartsWith(".Deneme");

    sealed record Request(string[] Files, string? Action);

    [STAThread]
    static int Main(string[] args)
    {
        GrapekitApp.Init("formaty", "Formaty", ["Formaty", "Formaty", "Formaty"], "formaty", "formaty");
        GrapekitApp.Lang = LoadLang();

        if (args.Contains("--unregister")) { Shell.Unregister(); return 0; }
        if (args.Length >= 2 && args[0] == "--selftest") return SelfTest.Run(args[1]);

        // Gezgin sağ tık hızlı işlemi: --action <ad> dosyalar…
        string? action = null;
        int ai = Array.IndexOf(args, "--action");
        if (ai >= 0 && ai + 1 < args.Length) action = args[ai + 1];
        var files = args.Where((a, i) => !a.StartsWith("--") && (ai < 0 || i != ai + 1) && (File.Exists(a) || Directory.Exists(a))).Select(Path.GetFullPath).ToList();

        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // Zaten açık: dosyaları çalışan kopyaya ilet (Gezgin çoklu seçimde her dosya için ayrı başlatabilir)
            Send(new Request([.. files], action));
            return 0;
        }

        ApplicationConfiguration.Initialize();
        ErrorLog.Install();
        if (!TestCopy) Shell.Register();

        var form = new MainForm(files, action) { Opacity = 0 }; // arayüz hazır olunca görünür (beyaz parlama olmasın)
        StartPipeServer(form);
        Application.Run(form);
        return Environment.ExitCode;
    }

    static int LoadLang()
    {
        var l = Store.Str("lang", "");
        return l == "en" ? 1 : l == "tr" ? 0 : CoreText.SystemLang();
    }

    static void Send(Request r)
    {
        try
        {
            using var c = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            c.Connect(5000);
            Native.AllowSetForegroundWindow(-1);
            var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r) + "\n");
            c.Write(data);
            c.Flush();
            using var rd = new StreamReader(c, Encoding.UTF8);
            rd.ReadLine();
        }
        catch { }
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
                    if (r != null) form.BeginInvoke(() => form.OpenFromOtherInstance([.. r.Files], r.Action));
                    var bytes = Encoding.UTF8.GetBytes("ok\n");
                    s.Write(bytes); s.Flush();
                    try { s.WaitForPipeDrain(); } catch { }
                }
                catch (Exception ex) { ErrorLog.Write(ex); Thread.Sleep(500); }
            }
        }) { IsBackground = true, Name = "pipe" };
        t.Start();
    }
}
