// Janela de configuração (abre sozinha na primeira vez), em dois passos: 1) a pasta do WoW Forever e qual pasta do
// addon usar; 2) a chave do /vincular-app. Mais "Abrir junto com o Windows". Ao salvar, testa a chave no bot.
using InsanosApp.Core;
using InsanosApp.Ui;

namespace InsanosApp;

internal sealed class SetupForm : ForgeWindow
{
    private readonly AppSettings _settings;
    private readonly HttpClient _http;
    private readonly InputBox _dir = new();
    private readonly Label _found = new() { AutoSize = true, BackColor = Color.Transparent };
    private readonly Segmented _addon = new();
    private readonly InputBox _key = new();
    private readonly ToggleSwitch _startup = new();
    private readonly Label _status = new() { AutoSize = false, BackColor = Color.Transparent };
    private readonly ForgeButton _save = new("Salvar e conectar", primary: true);

    public SetupForm(AppSettings settings, HttpClient http)
    {
        _settings = settings;
        _http = http;
        Text = "App Insanos";
        Icon = Theme.AppIcon();
        StartPosition = FormStartPosition.CenterScreen;
        Font = Paint2.Ui(9.5f);
        ClientSize = new Size(520, 512);

        // cabeçalho: só o nome INSANOS em fogo
        var title = new ArtImage(Theme.Wordmark(), 172) { Location = new Point(18, 6) };
        var sub = new Label
        {
            Text = "As raids do Discord direto no addon, sem colar código.", ForeColor = Theme.Ash, AutoSize = false,
            BackColor = Color.Transparent, Location = new Point(206, 34), Size = new Size(296, 40)
        };

        // passo 1: a pasta do jogo e do addon
        var step1 = new Card { Location = new Point(20, 96), Size = new Size(480, 136) };
        var t1 = StepTitle(step1, "1", "Pasta do WoW Forever");
        _dir.SetBounds(14, 44, 360, 34);
        var browse = new ForgeButton("Procurar") { Location = new Point(382, 44), Size = new Size(84, 34) };
        browse.Click += (_, _) => Browse();
        _dir.Box.TextChanged += (_, _) => RefreshAddons();
        _found.Location = new Point(14, 96);
        _addon.SetItems([("NilbasForever", "INSANOS"), ("NilbasForeverTeste", "Teste")]);
        _addon.SetBounds(232, 90, 234, 30);
        step1.Controls.AddRange([.. t1, _dir, browse, _found, _addon]);

        // passo 2: a chave do /vincular-app
        var step2 = new Card { Location = new Point(20, 244), Size = new Size(480, 116) };
        var t2 = StepTitle(step2, "2", "Chave do app");
        _key.SetBounds(14, 44, 404, 34);
        _key.Box.UseSystemPasswordChar = true;
        var eye = new ForgeButton("👁") { Location = new Point(424, 44), Size = new Size(42, 34), Font = new Font("Segoe UI Emoji", 10f) };
        eye.Click += (_, _) => _key.Box.UseSystemPasswordChar = !_key.Box.UseSystemPasswordChar;
        var hint = new Label { Text = "No Discord da guilda, use /vincular-app e cole a chave aqui.", ForeColor = Theme.Ash, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 86) };
        step2.Controls.AddRange([.. t2, _key, eye, hint]);

        // iniciar com o Windows
        _startup.Location = new Point(24, 378);
        _startup.Checked = Startup.IsEnabled();
        var startupLabel = new Label { Text = "Abrir junto com o Windows", ForeColor = Theme.Bone, AutoSize = true, BackColor = Color.Transparent, Location = new Point(70, 378), Cursor = Cursors.Hand };
        startupLabel.Click += (_, _) => _startup.Checked = !_startup.Checked;

        _status.SetBounds(24, 410, 476, 40);

        _save.Location = new Point(20, 458);
        _save.Size = new Size(184, 38);
        _save.Click += async (_, _) => await SaveAsync();
        var cancel = new ForgeButton("Cancelar") { Location = new Point(214, 458), Size = new Size(110, 38) };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange([title, sub, step1, step2, _startup, startupLabel, _status, _save, cancel]);
        foreach (var c in new Control[] { title, sub, step1, step2 }) MakeDraggable(c);

        _dir.Box.Text = settings.AddonsDir ?? WowLocator.Detect() ?? "";
        RefreshAddons();
        _addon.SelectedValue = settings.AddonFolder;
        if (KeyVault.Unprotect(settings.ProtectedKey) is { } k) _key.Box.Text = k;
    }

    protected override void OnCloseClicked()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private static Control[] StepTitle(Control card, string n, string text) =>
    [
        new StepBadge(n) { Location = new Point(14, 13) },
        new Label { Text = text, Font = Paint2.Semi(10.5f), ForeColor = Theme.Bone, AutoSize = true, BackColor = Color.Transparent, Location = new Point(44, 13) }
    ];

    private void Browse()
    {
        using var dlg = new FolderBrowserDialog { Description = "Escolha a pasta do WoW Forever (a _forever_) ou a Interface\\AddOns", UseDescriptionForTitle = true };
        if (Directory.Exists(_dir.Box.Text)) dlg.InitialDirectory = _dir.Box.Text;
        if (dlg.ShowDialog(this) == DialogResult.OK) _dir.Box.Text = dlg.SelectedPath;
    }

    private void RefreshAddons()
    {
        var addons = WowLocator.NormalizeAddonsDir(_dir.Box.Text) is { } d ? WowLocator.InstalledAddons(d) : [];
        _addon.SetEnabledValues(addons);
        _addon.Visible = addons.Count > 0;
        _found.Text = addons.Count > 0 ? "✓  Addon encontrado" : "✕  Não achei o addon nessa pasta";
        _found.ForeColor = addons.Count > 0 ? Theme.Ok : Theme.Bad;
    }

    private void SetStatus(string text, bool ok)
    {
        _status.Text = text;
        _status.ForeColor = ok ? Theme.Ok : Theme.Bad;
    }

    private async Task SaveAsync()
    {
        var dir = WowLocator.NormalizeAddonsDir(_dir.Box.Text);
        if (dir is null || _addon.SelectedValue is not string addon) { SetStatus("Escolha a pasta do WoW Forever com o addon instalado.", false); return; }
        var key = _key.Box.Text.Trim();
        if (!InboxApi.LooksLikeKey(key)) { SetStatus("A chave começa com nfapp_ e vem do /vincular-app no Discord.", false); return; }

        _save.Enabled = false;
        SetStatus("Testando a chave no bot…", true);
        var res = await InboxApi.FetchAsync(_http, _settings.ApiBase, key);
        _save.Enabled = true;
        if (res.Status == InboxStatus.Unauthorized) { SetStatus("O bot recusou a chave. Gere outra com /vincular-app.", false); return; }
        if (res.Status != InboxStatus.Ok) { SetStatus("Não consegui falar com o bot agora. Confira a internet e tente de novo.", false); return; }

        _settings.AddonsDir = dir;
        _settings.AddonFolder = addon;
        _settings.ProtectedKey = KeyVault.Protect(key);
        _settings.Save();
        try { Startup.Set(_startup.Checked); } catch (UnauthorizedAccessException) { }
        DialogResult = DialogResult.OK;
        Close();
    }
}
