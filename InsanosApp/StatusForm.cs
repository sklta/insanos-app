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
    /// <summary>O WoW Forever está fechado: o app espera o jogo abrir para verificar.</summary>
    public bool WaitingForGame { get; init; }
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
    // rodapé: a próxima verificação à esquerda e quatro botões quadrados iguais, só com ícone, à direita
    private readonly Panel _rule = new() { BackColor = Theme.Seam };
    private readonly Label _clock = new() { Text = "", AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.Gold, Font = Paint2.Icons(10f) };
    private readonly Label _countdown = new() { AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.Ash, Font = Paint2.Ui(9f) };
    private readonly ForgeButton _refresh = new("") { Glyph = "" };
    private readonly ForgeButton _folder = new("") { Glyph = "" };
    private readonly ForgeButton _site = new("") { Glyph = "" };
    private readonly ForgeButton _gear = new("") { Glyph = "" };
    private readonly ToolTip _tips = new() { InitialDelay = 300 };
    // trava: com o WoW Forever fechado, uma camada cobre a janela inteira até o jogo abrir
    private readonly WaitOverlay _wait = new();
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

        foreach (var (b, tip) in new[] { (_refresh, "Atualizar agora"), (_folder, "Abrir a pasta do addon"), (_site, "Abrir o site da guilda"), (_gear, "Configurar") })
        {
            b.Size = new Size(34, 34);
            b.AccessibleName = tip;
            _tips.SetToolTip(b, tip);
        }
        _refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _folder.Click += (_, _) => FolderRequested?.Invoke(this, EventArgs.Empty);
        _site.Click += (_, _) => SiteRequested?.Invoke(this, EventArgs.Empty);
        _gear.Click += (_, _) => SetupRequested?.Invoke(this, EventArgs.Empty);
        _wait.Setup.Click += (_, _) => SetupRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange([title, _dot, _conn, _checked, _next, _others, _rule, _clock, _countdown, _refresh, _folder, _site, _gear, _wait]);
        _tick.Tick += (_, _) => UpdateCountdown();
        Deactivate += (_, _) => { if (!_keepOpen) Hide(); };
        VisibleChanged += (_, _) =>
        {
            if (Visible) { _tick.Start(); if (_waiting) BeginInvoke(ApplyWait); }
            else _tick.Stop();
        };
        Render();
    }

    private bool _keepOpen;
    private bool _waiting;

    /// <summary>Para o --preview: o DrawToBitmap do Windows Forms desenha os filhos fora de ordem, então a camada vai por cima à mão.</summary>
    public void PaintWaitOnto(Bitmap bmp)
    {
        if (!_wait.Visible) return;
        using var layer = new Bitmap(_wait.Width, _wait.Height);
        _wait.DrawToBitmap(layer, new Rectangle(Point.Empty, _wait.Size));
        using var g = Graphics.FromImage(bmp);
        var origin = PointToScreen(Point.Empty);
        g.DrawImage(layer, _wait.Left + (origin.X - Left), _wait.Top + (origin.Y - Top));
    }

    // Liga ou desliga a trava. Ligando: tira uma foto da janela sem a camada, para ela ficar esmaecida atrás.
    private void ApplyWait()
    {
        if (!_waiting) { _wait.Visible = false; return; }
        _wait.SetBounds(1, 2, ClientSize.Width - 2, ClientSize.Height - 3);
        if (IsHandleCreated && Visible)
        {
            _wait.Visible = false;
            var shot = new Bitmap(ClientSize.Width, ClientSize.Height);
            DrawToBitmap(shot, new Rectangle(Point.Empty, ClientSize));
            var part = shot.Clone(_wait.Bounds, shot.PixelFormat);
            shot.Dispose();
            _wait.SetBackdrop(part);
        }
        _wait.Visible = true;
        _wait.BringToFront();
        CloseButton.BringToFront();
    }

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
            : v.WaitingForGame ? (Theme.Gold, "Esperando o WoW Forever")
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
        _checked.Text = !v.WaitingForGame && v.LastCheck is { } lc ? "verificado " + lc.ToString("HH:mm") : "";
        _checked.Visible = _checked.Text.Length > 0; // vazio, ele ficaria por cima do texto da conexão

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

        // linha fina e o rodapé
        _rule.SetBounds(16, y + 2, 348, 1);
        y += 14;
        _gear.Location = new Point(364 - 34, y);
        _site.Location = new Point(_gear.Left - 6 - 34, y);
        _folder.Location = new Point(_site.Left - 6 - 34, y);
        _refresh.Location = new Point(_folder.Left - 6 - 34, y);
        _clock.Location = new Point(16, y + 10);
        _countdown.Location = new Point(34, y + 8);
        UpdateCountdown();
        _refresh.Enabled = v.Configured && !v.Busy;
        Height = y + 34 + 16;

        var wasWaiting = _waiting;
        _waiting = v.Configured && v.WaitingForGame;
        if (_waiting != wasWaiting || _waiting) ApplyWait();
    }

    private void UpdateCountdown()
    {
        var v = _view();
        if (!v.Configured) { _countdown.Text = ""; return; }
        var left = v.NextCheck - DateTime.Now;
        _countdown.Text = v.Busy ? "Verificando agora…"
            : v.WaitingForGame ? "Começa quando o jogo abrir"
            : left.TotalSeconds <= 0 ? "Verificando em instantes…"
            : left.TotalMinutes >= 1 ? $"Próxima verificação em {(int)Math.Ceiling(left.TotalMinutes)} min"
            : $"Próxima verificação em {(int)left.TotalSeconds} s";
    }
}
