namespace Grapekit;

/// <summary>
/// Her Grapekit uygulamasının tepsi menüsünde aynı sırada duran öğeler:
/// [uygulamaya özel öğeler] — ☕ Destek · Diğer araçlar · Hakkında — [Ayarlar, Çıkış]
/// </summary>
public static class StandardMenu
{
    public static ToolStripItem[] Items(Action showAbout)
    {
        var support = new ToolStripMenuItem(CoreText.T("support"));
        support.Click += (_, _) => Brand.Open(Brand.Support);
        var more = new ToolStripMenuItem(CoreText.T("more_apps"));
        more.Click += (_, _) => Brand.Open(Brand.AllAppsUrl);
        var about = new ToolStripMenuItem(CoreText.T("about"));
        about.Click += (_, _) => showAbout();
        return [support, more, about];
    }
}
