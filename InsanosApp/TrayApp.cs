// O app fica perto do relógio: a cada poucos minutos pergunta ao bot pelas raids e escreve o Inbox.lua do addon.
// Clique no ícone: janela de status. Botão direito: menu (atualizar, configurar, pasta do addon, site, sair).
using System.Diagnostics;
using InsanosApp.Core;
using InsanosApp.Ui;

namespace InsanosApp;

internal sealed class TrayApp : ApplicationContext
{
    public const string Site = "https://insanos.discloud.app";
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

    public TrayApp(AppSettings settings, HttpClient http)
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
        menu.Items.Add("Abrir o site da guilda", null, (_, _) => Open(Site));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitThread());

        _icon = new NotifyIcon { Icon = Theme.AppIcon(), Text = "App Insanos", ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleStatus(); };
        _icon.BalloonTipClicked += (_, _) => ShowStatus();

        _status = new StatusForm(View);
        _status.RefreshRequested += async (_, _) => await SyncAsync(manual: true);
        _status.FolderRequested += (_, _) => OpenAddonFolder();
        _status.SiteRequested += (_, _) => Open(Site);
        _status.SetupRequested += (_, _) => { _status.Hide(); OpenSetup(); };

        _timer.Interval = Math.Max(15, _settings.PollSeconds) * 1000;
        _timer.Tick += async (_, _) => await SyncAsync(manual: false);
        _timer.Start();
        _ = SyncAsync(manual: false);
    }

    private StatusView View() => new()
    {
        Configured = _settings.IsComplete && KeyVault.Unprotect(_settings.ProtectedKey) is not null,
        Last = _last, LastCheck = _lastCheck, NextCheck = _nextCheck, Busy = _busy
    };

    private void ToggleStatus()
    {
        if (_status.Visible) _status.Hide(); else ShowStatus();
    }

    private void ShowStatus() => _status.ShowNearTray();

    private void Changed()
    {
        var tip = "App Insanos · " + (_last?.Message ?? "iniciando");
        _icon.Text = tip.Length > 63 ? tip[..62] + "…" : tip;
        if (_status.Visible) _status.Render();
    }

    private async Task SyncAsync(bool manual)
    {
        if (_busy) return;
        var key = KeyVault.Unprotect(_settings.ProtectedKey);
        if (!_settings.IsComplete || key is null) { Changed(); return; }
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
            Open(Path.Combine(d, _settings.AddonFolder));
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch (Exception) { }
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
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
