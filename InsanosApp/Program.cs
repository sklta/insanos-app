// App Insanos no PC: uma instância só. Aberto pelo .exe, mostra as boas-vindas (se a pessoa não desligou) e, na
// primeira vez, a configuração; depois fica perto do relógio. Aberto junto com o Windows (--tray), vai direto.
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
            if (args[1] is "status" or "status-espera")
            {
                var k = "!NILBAS:QUJD";
                var raids = new List<InboxRaid>
                {
                    new(7, "Molten Core", "sáb 21:00", null, 3, 12, k, 2, 3, 7),
                    new(8, "Onyxia", "dom 20:00", null, 1, 8, k, 1, 2, 5)
                };
                var view = new StatusView
                {
                    Configured = true, LastCheck = DateTime.Now, NextCheck = DateTime.Now.AddMinutes(4), WaitingForGame = args[1] == "status-espera",
                    Last = new SyncOutcome(InboxStatus.Ok, false, raids[0], 2, "Molten Core") { Raids = raids }
                };
                var sf = new StatusForm(() => view);
                sf.KeepOpen();
                f = sf;
            }
            else if (args[1] == "welcome") f = new WelcomeForm(configured: false, showNextTime: true);
            else if (args[1] is "toast-baixando" or "toast-pronto")
            {
                var t = new InsanosApp.Ui.UpdateToast();
                if (args[1] == "toast-baixando") t.Working("Atualizando o App Insanos", "Baixando a versão 0.2.2 · 64%", 0.64, "Depois confere a assinatura e reabre sozinho.");
                else t.Finish(InsanosApp.Ui.UpdateToast.Kind.Done, "App Insanos atualizado", "Agora na versão 0.2.2 · assinatura conferida", "Ver novidades", () => { });
                t.Hide();
                f = t;
            }
            else if (args[1] == "setup-guilda")
            {
                // como o pessoal da guilda vê: só a pasta NilbasForever instalada
                var fake = Path.Combine(Path.GetTempPath(), "insanos-preview", "_forever_", "Interface", "AddOns");
                Directory.CreateDirectory(Path.Combine(fake, "NilbasForever"));
                File.WriteAllText(Path.Combine(fake, "NilbasForever", "NilbasForever.toc"), "## Title: INSANOS");
                f = new SetupForm(new AppSettings { AddonsDir = fake }, h);
            }
            else if (args[1] == "setup-readme")
            {
                var sf2 = new SetupForm(new AppSettings(), h);
                sf2.ForPreview();
                f = sf2;
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
            if (f is StatusForm s3) s3.PaintWaitOnto(bmp);
            bmp.Save(args[2], System.Drawing.Imaging.ImageFormat.Png);
            f.Dispose();
            return;
        }

        using var single = new Mutex(true, @"Local\InsanosApp", out var first);
        var updated = args.Contains("--updated");
        // depois de uma atualização, a versão antiga ainda está fechando: espera ela soltar a vez
        if (!first && updated)
        {
            try { first = single.WaitOne(TimeSpan.FromSeconds(20)); }
            catch (AbandonedMutexException) { first = true; }
        }
        if (!first)
        {
            MessageBox.Show("O App Insanos já está aberto: procure o ícone perto do relógio.", "App Insanos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        if (Environment.ProcessPath is { } exe) Updater.CleanUp(exe);
        using var http = InboxApi.CreateClient(Version);
        var settings = AppSettings.Load();
        var fromWindows = args.Contains("--tray");
        if (!fromWindows && (settings.ShowWelcome || !settings.IsComplete))
        {
            using var hello = new WelcomeForm(settings.IsComplete, settings.ShowWelcome);
            hello.ShowDialog();
            if (hello.ShowNextTime != settings.ShowWelcome)
            {
                settings.ShowWelcome = hello.ShowNextTime;
                try { settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        if (!settings.IsComplete)
        {
            using var setup = new SetupForm(settings, http);
            if (setup.ShowDialog() != DialogResult.OK) return;
        }
        Application.Run(new TrayApp(settings, http, updated));
        try { single.ReleaseMutex(); } catch (ApplicationException) { }
    }
}
