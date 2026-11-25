// O app fica perto do relógio: a cada poucos minutos pergunta ao bot pelas raids e escreve o Inbox.lua do addon.
// Clique no ícone: janela de status. Botão direito: menu (atualizar, configurar, pasta do addon, site, sair).
using System.Diagnostics;
using InsanosApp.Core;
using InsanosApp.Ui;

namespace InsanosApp;

internal sealed class TrayApp : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly HttpClient _http;
    private readonly NotifyIcon _icon;
    private readonly StatusForm _status;
    private readonly System.Windows.Forms.Timer _timer = new();
    private SyncOutcome? _last;
    private DateTime? _lastCheck;
    private DateTime _nextCheck = DateTime.Now;
    private bool _busy;
    private int _failures;
    private bool _waitingForGame;
    private const int GameCheckMs = 10 * 1000;   // com o jogo fechado, olha a cada 10 s se ele abriu
    private readonly System.Windows.Forms.Timer _updateTimer = new();
    private bool _updating;
    private Version? _announced;        // versão nova já avisada (para não repetir o balão)
    private Action? _balloonClick;      // o que o clique no balão faz (padrão: abrir o status)

    public TrayApp(AppSettings settings, HttpClient http, bool justUpdated = false)
    {
        _settings = settings;
        _http = http;

        var menu = new ContextMenuStrip { ShowImageMargin = false, Renderer = new ForgeMenuRenderer(), BackColor = Theme.Iron, ForeColor = Theme.Bone, Font = Paint2.Ui(9.5f) };
        menu.Items.Add(new ToolStripMenuItem("App Insanos v" + Program.Version) { Enabled = false, Font = Paint2.Semi(9.5f) });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Abrir o status", null, (_, _) => ShowStatus());
        menu.Items.Add("Atualizar agora", null, async (_, _) => await SyncAsync(manual: true));
        menu.Items.Add("Configurar…", null, (_, _) => OpenSetup());
        menu.Items.Add("Abrir a pasta do addon", null, (_, _) => OpenAddonFolder());
        menu.Items.Add("Abrir o site da guilda", null, (_, _) => Links.Open(Links.Site));
        menu.Items.Add("Boas-vindas e links", null, (_, _) => ShowWelcome());
        menu.Items.Add("Procurar atualização", null, async (_, _) => await CheckUpdateAsync(manual: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitThread());

        _icon = new NotifyIcon { Icon = Theme.AppIcon(), Text = "App Insanos", ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleStatus(); };
        _icon.BalloonTipClicked += (_, _) => { var a = _balloonClick ?? ShowStatus; _balloonClick = null; a(); };

        _status = new StatusForm(View);
        _status.RefreshRequested += async (_, _) => await SyncAsync(manual: true);
        _status.FolderRequested += (_, _) => OpenAddonFolder();
        _status.SiteRequested += (_, _) => Links.Open(Links.Site);
        _status.SetupRequested += (_, _) => { _status.Hide(); OpenSetup(); };

        _timer.Interval = Math.Max(15, _settings.PollSeconds) * 1000;
        _timer.Tick += async (_, _) => await SyncAsync(manual: false);
        _timer.Start();
        _ = SyncAsync(manual: false);

        // atualização: a primeira olhada 1 minuto depois de abrir, depois a cada 6 horas
        _updateTimer.Interval = 60 * 1000;
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Interval = 6 * 60 * 60 * 1000;
            await CheckUpdateAsync(manual: false);
        };
        _updateTimer.Start();
        if (justUpdated)
            Balloon(6000, "App Insanos atualizado", "Agora na versão " + Program.Version + ".", ToolTipIcon.Info);
    }

    private void Balloon(int ms, string title, string text, ToolTipIcon icon, Action? onClick = null)
    {
        _balloonClick = onClick;
        _icon.ShowBalloonTip(ms, title, text, icon);
    }

    // Procura versão nova no GitHub. Com "Atualizar sozinho" ligado, baixa, confere a assinatura, troca o .exe e
    // reabre; desligado (ou numa pasta sem permissão), só avisa com o link do download.
    private async Task CheckUpdateAsync(bool manual)
    {
        if (_updating) return;
        _updating = true;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var current = Version.Parse(Program.Version);
            var r = await Updater.CheckAsync(_http, cts.Token);
            if (r is null)
            {
                if (manual) Balloon(5000, "App Insanos", "Não consegui ver se há versão nova agora. Tente mais tarde.", ToolTipIcon.Warning);
                return;
            }
            if (!Updater.IsNewer(r.Version, current))
            {
                if (manual) Balloon(4000, "App Insanos", "Você já está na versão mais nova (" + Program.Version + ").", ToolTipIcon.Info);
                return;
            }
            var exe = Environment.ProcessPath;
            if ((!_settings.AutoUpdate && !manual) || exe is null)
            {
                if (manual || _announced != r.Version)
                    Balloon(8000, "Versão nova do App Insanos", r.Version.ToString(3) + " disponível. Clique para baixar.", ToolTipIcon.Info, () => Links.Open(r.Page));
                _announced = r.Version;
                return;
            }

            var prep = await Updater.PrepareAsync(_http, r, exe, cts.Token);
            switch (prep.Step)
            {
                case UpdateStep.NotWritable:
                    Balloon(8000, "Versão nova do App Insanos", r.Version.ToString(3) + " disponível, mas não posso trocar o arquivo nesta pasta. Clique para baixar.", ToolTipIcon.Info, () => Links.Open(r.Page));
                    return;
                case UpdateStep.BadSignature:
                    Balloon(8000, "Atualização recusada", "O arquivo da versão " + r.Version.ToString(3) + " não passou na conferência da assinatura e foi apagado. Avise um oficial.", ToolTipIcon.Warning);
                    return;
                case UpdateStep.DownloadFailed:
                    if (manual) Balloon(5000, "App Insanos", "Não consegui baixar a versão nova agora. Tento de novo depois.", ToolTipIcon.Warning);
                    return;
            }

            // não troca no meio de uma tela aberta (configuração, boas-vindas): tenta de novo daqui a 5 minutos
            if (_busy || Application.OpenForms.OfType<Form>().Any(f => f.Modal))
            {
                Updater.CleanUp(exe);
                _updateTimer.Interval = 5 * 60 * 1000;
                return;
            }
            if (!Updater.Swap(exe, prep.File!))
            {
                Updater.CleanUp(exe);
                Balloon(8000, "Versão nova do App Insanos", "Não consegui trocar o arquivo. Clique para baixar a " + r.Version.ToString(3) + ".", ToolTipIcon.Warning, () => Links.Open(r.Page));
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, ArgumentList = { "--tray", "--updated" } });
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Balloon(8000, "App Insanos", "A versão nova foi instalada. Abra o app de novo para usar.", ToolTipIcon.Info);
                return;
            }
            ExitThread();
        }
        finally { _updating = false; }
    }

    private StatusView View() => new()
    {
        Configured = _settings.IsComplete && KeyVault.Unprotect(_settings.ProtectedKey) is not null,
        Last = _last, LastCheck = _lastCheck, NextCheck = _nextCheck, Busy = _busy, WaitingForGame = _waitingForGame
    };

    private void ToggleStatus()
    {
        if (_status.Visible) _status.Hide(); else ShowStatus();
    }

    private void ShowStatus() => _status.ShowNearTray();

    private void Changed()
    {
        var tip = "App Insanos · " + (_waitingForGame ? "esperando o WoW Forever abrir" : _last?.Message ?? "iniciando");
        _icon.Text = tip.Length > 63 ? tip[..62] + "…" : tip;
        if (_status.Visible) _status.Render();
    }

    private async Task SyncAsync(bool manual)
    {
        if (_busy) return;
        var key = KeyVault.Unprotect(_settings.ProtectedKey);
        if (!_settings.IsComplete || key is null) { Changed(); return; }
        // trava: com o WoW Forever fechado o app fica parado, sem chamar o bot; o clique em "Atualizar agora" ainda vale
        _waitingForGame = !manual && !GameWatch.IsRunning(_settings.AddonsDir);
        if (_waitingForGame)
        {
            _timer.Stop();
            _timer.Interval = GameCheckMs;
            _timer.Start();
            _nextCheck = DateTime.Now.AddMilliseconds(_timer.Interval);
            Changed();
            return;
        }
        _busy = true;
        Changed();
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var r = await InboxSync.RunAsync(_http, _settings, key, now);
            var prev = _last;
            _last = r;
            _lastCheck = DateTime.Now;
            // quando verificar de novo: o ritmo que o bot mandou, a espera que ele pediu, ou, sem conexão, cada vez mais devagar
            _failures = r.Status == InboxStatus.Ok ? 0 : _failures + 1;
            var normal = TimeSpan.FromSeconds(Math.Max(15, _settings.PollSeconds));
            _timer.Interval = (int)RaidRoster.NextCheck(r, DateTimeOffset.UtcNow, normal, _failures).TotalMilliseconds;
            if (r.Status != InboxStatus.Ok)
            {
                if (manual || r.Status == InboxStatus.Unauthorized) _icon.ShowBalloonTip(6000, "App Insanos", r.Message, ToolTipIcon.Warning);
                return;
            }

            var changes = prev is { Status: InboxStatus.Ok } ? RaidRoster.Diff(prev.Raids, r.Raids) : [];
            if (r.Wrote)
            {
                _settings.LastChangeAt = now;
                _settings.LastRaidLabel = r.First is null ? null : r.Message;
                _settings.Save();
            }
            if (changes.Count > 0)
            {
                var text = string.Join("\n", changes.Take(3).Select(c => c.Describe()));
                _icon.ShowBalloonTip(8000, "A raid mudou", text + "\nNo jogo: clique em Puxar do app na aba Raid.", ToolTipIcon.Info);
            }
            else if (r.Wrote && r.First is not null && prev?.First?.Gid != r.First.Gid)
            {
                _icon.ShowBalloonTip(8000, "Raid pronta no addon", r.First.Inst + " · " + r.First.Details() + "\nEntre no jogo ou clique em Puxar do app.", ToolTipIcon.Info);
            }
            else if (manual && !_status.Visible)
            {
                _icon.ShowBalloonTip(4000, "App Insanos", "Tudo em dia: " + r.Message, ToolTipIcon.Info);
            }
        }
        finally
        {
            _busy = false;
            // recomeça a contagem: depois de uma verificação (ou de o intervalo mudar), conta do zero
            _timer.Stop();
            _timer.Start();
            _nextCheck = DateTime.Now.AddMilliseconds(_timer.Interval);
            Changed();
        }
    }

    private void OpenSetup()
    {
        using var f = new SetupForm(_settings, _http);
        if (f.ShowDialog() == DialogResult.OK) _ = SyncAsync(manual: true);
        Changed();
    }

    private void OpenAddonFolder()
    {
        if (_settings.AddonsDir is { } d && Directory.Exists(Path.Combine(d, _settings.AddonFolder)))
            Links.Open(Path.Combine(d, _settings.AddonFolder));
    }

    private bool _welcomeOpen;
    private void ShowWelcome()
    {
        if (_welcomeOpen) return;
        _welcomeOpen = true;
        try
        {
            using var f = new WelcomeForm(configured: true, _settings.ShowWelcome);
            f.ShowDialog();
            if (f.ShowNextTime != _settings.ShowWelcome) { _settings.ShowWelcome = f.ShowNextTime; _settings.Save(); }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        finally { _welcomeOpen = false; }
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _updateTimer.Stop();
        _status.Close();
        _icon.Visible = false;
        _icon.Dispose();
        base.ExitThreadCore();
    }
}

/// <summary>Menu do botão direito no tema escuro da guilda.</summary>
internal sealed class ForgeMenuRenderer() : ToolStripProfessionalRenderer(new ForgeColors())
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? (e.Item.Selected ? Color.White : Theme.Bone) : Theme.Gold;
        base.OnRenderItemText(e);
    }

    private sealed class ForgeColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Iron;
        public override Color MenuBorder => Theme.Seam;
        public override Color MenuItemBorder => Theme.Ember;
        public override Color MenuItemSelected => Color.FromArgb(0x3a, 0x1d, 0x15);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(0x3a, 0x1d, 0x15);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(0x3a, 0x1d, 0x15);
        public override Color SeparatorDark => Theme.Seam;
        public override Color SeparatorLight => Theme.Iron;
        public override Color ImageMarginGradientBegin => Theme.Iron;
        public override Color ImageMarginGradientMiddle => Theme.Iron;
        public override Color ImageMarginGradientEnd => Theme.Iron;
    }
}
