// Janela de status: abre com um clique no ícone perto do relógio, encostada no canto da tela, e some quando perde o
// foco. Mostra se está ligado ao bot, a próxima raid (já no addon ou não), as outras raids abertas, quando é a
// próxima verificação e os atalhos.
using InsanosApp.Core;
using InsanosApp.Ui;

namespace InsanosApp;

/// <summary>O que a janela precisa saber do app (o TrayApp preenche).</summary>
internal sealed class StatusView
{
    public bool Configured { get; init; }
    public SyncOutcome? Last { get; init; }
    public DateTime? LastCheck { get; init; }
    public DateTime NextCheck { get; init; }
    public bool Busy { get; init; }
}

internal sealed class StatusForm : ForgeWindow
{
    private readonly Func<StatusView> _view;
    private readonly Dot _dot = new();
    private readonly Label _conn = new() { AutoSize = true, BackColor = Color.Transparent, Font = Paint2.Semi(10f) };
    private readonly Label _checked = new() { AutoSize = false, BackColor = Color.Transparent, ForeColor = Theme.Ash, TextAlign = ContentAlignment.MiddleRight };
    private readonly Card _next = new() { Hot = true };
    private readonly Label _nextCap = Caption("Próxima raid");
    private readonly Label _nextName = new() { AutoSize = false, BackColor = Color.Transparent, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Theme.Bone, AutoEllipsis = true };
    private readonly Label _nextInfo = new() { AutoSize = false, BackColor = Color.Transparent, ForeColor = Theme.Ash, AutoEllipsis = true };
    private readonly Label _nextState = new() { AutoSize = false, BackColor = Color.Transparent };
    private readonly Card _others = new();
    private readonly Label _othersCap = Caption("Outras raids abertas");
    private readonly Label _othersList = new() { AutoSize = false, BackColor = Color.Transparent, ForeColor = Theme.Bone };
    private readonly Label _countdown = new() { AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.Ash };
    private readonly ForgeButton _refresh = new("Atualizar agora", primary: true);
    private readonly ForgeButton _folder = new("Pasta do addon");
    private readonly ForgeButton _site = new("Site");
    private readonly ForgeButton _gear = new("⚙") { Font = new Font("Segoe UI Symbol", 11f) };
    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 1000 };

    public event EventHandler? RefreshRequested, FolderRequested, SiteRequested, SetupRequested;

    public StatusForm(Func<StatusView> view)
    {
        _view = view;
        Text = "App Insanos";
        Icon = Theme.AppIcon();
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Theme.Ink;
        ForeColor = Theme.Bone;
        Font = Paint2.Ui(9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        DoubleBuffered = true;
        Width = 380;

        var title = new ArtImage(Theme.Wordmark(), 92) { Location = new Point(14, 6) };

        _dot.Location = new Point(18, 62);
        _conn.Location = new Point(34, 58);
        _checked.SetBounds(200, 58, 162, 22);

        _next.Controls.AddRange([_nextCap, _nextName, _nextInfo, _nextState]);
        _others.Controls.AddRange([_othersCap, _othersList]);

        _refresh.Size = new Size(126, 34);
        _folder.Size = new Size(110, 34);
        _site.Size = new Size(52, 34);
        _gear.Size = new Size(40, 34);
        _refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _folder.Click += (_, _) => FolderRequested?.Invoke(this, EventArgs.Empty);
        _site.Click += (_, _) => SiteRequested?.Invoke(this, EventArgs.Empty);
        _gear.Click += (_, _) => SetupRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange([title, _dot, _conn, _checked, _next, _others, _countdown, _refresh, _folder, _site, _gear]);
        _tick.Tick += (_, _) => UpdateCountdown();
        Deactivate += (_, _) => { if (!_keepOpen) Hide(); };
        VisibleChanged += (_, _) => { if (Visible) _tick.Start(); else _tick.Stop(); };
        Render();
    }

    private bool _keepOpen;

    protected override void OnCloseClicked() => Hide();
    /// <summary>Para o --preview: não some ao perder o foco.</summary>
    public void KeepOpen() => _keepOpen = true;

    private static Label Caption(string text) => new()
    {
        Text = text.ToUpperInvariant(), AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.Gold,
        Font = Paint2.Semi(8f), Location = new Point(14, 12)
    };



    /// <summary>Encosta no canto de baixo à direita, acima da barra de tarefas.</summary>
    public void ShowNearTray()
    {
        Render();
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
        Show();
        Activate();
    }

    public void Render()
    {
        var v = _view();
        var last = v.Last;
        // linha de conexão
        (Color dot, string conn) = !v.Configured ? (Theme.Gold, "Falta configurar")
            : v.Busy ? (Theme.Gold, "Verificando…")
            : last is null ? (Theme.Gold, "Ainda não verificou")
            : last.Status switch
            {
                InboxStatus.Ok => (Theme.Ok, "Ligado ao bot"),
                InboxStatus.Unauthorized => (Theme.Bad, "Chave recusada"),
                InboxStatus.Offline => (Theme.Bad, "Sem conexão com o bot"),
                InboxStatus.RateLimited => (Theme.Gold, "O bot pediu para esperar"),
                _ => (Theme.Bad, "Algo deu errado")
            };
        _dot.Color = dot;
        _conn.Text = conn;
        _conn.ForeColor = Theme.Bone;
        _checked.Text = v.LastCheck is { } lc ? "verificado " + lc.ToString("HH:mm") : "";

        var y = 94;
        // próxima raid
        _next.SetBounds(16, y, 348, 118);
        _nextCap.Text = "PRÓXIMA RAID";
        _nextName.SetBounds(14, 30, 320, 32);
        _nextInfo.SetBounds(14, 62, 320, 22);
        _nextState.SetBounds(14, 86, 320, 22);
        if (!v.Configured)
        {
            _nextName.Text = "Configure o app";
            _nextInfo.Text = "Clique na engrenagem e cole a chave do /vincular-app.";
            _nextState.Text = "";
        }
        else if (last?.First is { } r)
        {
            _nextName.Text = r.Inst;
            _nextInfo.Text = r.Details();
            _nextState.Text = "✓  Já está no addon: entre no jogo ou dê /reload";
            _nextState.ForeColor = Theme.Ok;
        }
        else if (last is { Status: InboxStatus.Ok })
        {
            _nextName.Text = "Nenhuma raid aberta";
            _nextInfo.Text = "Quando você liderar uma raid no Discord, ela aparece aqui.";
            _nextState.Text = "";
        }
        else
        {
            _nextName.Text = last is null ? "…" : "Sem novidade";
            _nextInfo.Text = last?.Message ?? "Buscando as raids no bot.";
            _nextState.Text = "";
        }
        _next.Invalidate();
        y += 118 + 10;

        // outras raids
        var others = last?.Raids.Skip(1).Take(4).ToList() ?? [];
        _others.Visible = others.Count > 0;
        if (others.Count > 0)
        {
            var h = 36 + others.Count * 22 + 8;
            _others.SetBounds(16, y, 348, h);
            _othersList.SetBounds(14, 34, 320, others.Count * 22);
            _othersList.Text = string.Join("\n", others.Select(o => o.Inst + " · " + o.Short()));
            y += h + 10;
        }

        _countdown.Location = new Point(18, y + 2);
        UpdateCountdown();
        y += 30;
        // da esquerda para a direita, 8 px entre os botões
        _refresh.Location = new Point(16, y);
        _folder.Location = new Point(_refresh.Right + 8, y);
        _site.Location = new Point(_folder.Right + 8, y);
        _gear.Location = new Point(_site.Right + 8, y);
        _refresh.Enabled = v.Configured && !v.Busy;
        Height = y + 34 + 16;
    }

    private void UpdateCountdown()
    {
        var v = _view();
        if (!v.Configured) { _countdown.Text = ""; return; }
        var left = v.NextCheck - DateTime.Now;
        _countdown.Text = v.Busy ? "Verificando agora…"
            : left.TotalSeconds <= 0 ? "Verificando em instantes…"
            : left.TotalMinutes >= 1 ? $"Próxima verificação em {(int)Math.Ceiling(left.TotalMinutes)} min"
            : $"Próxima verificação em {(int)left.TotalSeconds} s";
    }
}
