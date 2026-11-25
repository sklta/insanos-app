// App Insanos no PC: uma instância só, abre a configuração na primeira vez e depois fica perto do relógio.
using InsanosApp.Core;

namespace InsanosApp;

internal static class Program
{
    public static string Version =>
        typeof(Program).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0";

    [STAThread]
    private static void Main(string[] args)
    {
        // --preview setup|status <arquivo.png>: salva uma imagem da janela e sai (para conferir o visual)
        if (args.Length == 3 && args[0] == "--preview")
        {
            ApplicationConfiguration.Initialize();
            using var h = InboxApi.CreateClient(Version);
            Form f;
            if (args[1] == "status")
            {
                var k = "!NILBAS:QUJD";
                var raids = new List<InboxRaid>
                {
                    new(7, "Molten Core", "sáb 21:00", null, 3, 12, k, 2, 3, 7),
                    new(8, "Onyxia", "dom 20:00", null, 1, 8, k, 1, 2, 5)
                };
                var view = new StatusView
                {
                    Configured = true, LastCheck = DateTime.Now, NextCheck = DateTime.Now.AddMinutes(4),
                    Last = new SyncOutcome(InboxStatus.Ok, false, raids[0], 2, "Molten Core") { Raids = raids }
                };
                var sf = new StatusForm(() => view);
                sf.KeepOpen();
                f = sf;
            }
            else f = new SetupForm(new AppSettings(), h);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-4000, -4000);
            f.ShowInTaskbar = false;
            f.Show();
            if (f is StatusForm s2) s2.Render();
            Application.DoEvents();
            using var bmp = new Bitmap(f.Width, f.Height);
            f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.Size));
            bmp.Save(args[2], System.Drawing.Imaging.ImageFormat.Png);
            f.Dispose();
            return;
        }

        using var single = new Mutex(true, @"Local\InsanosApp", out var first);
        if (!first)
        {
            MessageBox.Show("O App Insanos já está aberto: procure o ícone perto do relógio.", "App Insanos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        using var http = InboxApi.CreateClient(Version);
        var settings = AppSettings.Load();
        if (!settings.IsComplete)
        {
            using var setup = new SetupForm(settings, http);
            if (setup.ShowDialog() != DialogResult.OK) return;
        }
        Application.Run(new TrayApp(settings, http));
    }
}
