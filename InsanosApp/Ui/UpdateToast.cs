// Janelinha da atualização, em cima do relógio (no lugar do balão do Windows): mostra o download com a barra de
// progresso, a conferência da assinatura e, depois de reabrir, "atualizado" com o botão das novidades. Não rouba o
// foco (o jogo e o que estiver aberto continuam com o teclado) e some sozinha quando termina.
namespace InsanosApp.Ui;

internal sealed class UpdateToast : ForgeWindow
{
    public enum Kind { Working, Done, Warning }

    private const int W = 340;
    private readonly System.Windows.Forms.Timer _autoClose = new();
    private readonly ForgeButton _action = new("Ver novidades") { Glyph = "\uE8A7" };
    private Action? _onAction;
    private Kind _kind = Kind.Working;
    private string _title = "", _text = "", _foot = "";
    private double? _progress;

    public UpdateToast()
    {
        Text = "App Insanos";
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Font = Paint2.Ui(9.5f);
        ClientSize = new Size(W, 96);
        _action.Size = new Size(140, 32);
        _action.Visible = false;
        _action.Click += (_, _) => { var a = _onAction; HideToast(); a?.Invoke(); };
        Controls.Add(_action);
        _autoClose.Tick += (_, _) => HideToast();
    }

    // aparece sem tirar o foco de quem estiver usando o PC
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 /* WS_EX_NOACTIVATE */ | 0x00000080 /* WS_EX_TOOLWINDOW */;
            return cp;
        }
    }

    protected override void OnCloseClicked() => HideToast();

    private void HideToast()
    {
        _autoClose.Stop();
        Hide();
    }

    /// <summary>Enquanto trabalha: título, texto e, se houver, a barra (0 a 1). Não some sozinha.</summary>
    public void Working(string title, string text, double? progress, string foot = "")
    {
        _kind = Kind.Working;
        _title = title; _text = text; _progress = progress; _foot = foot;
        _onAction = null;
        _autoClose.Stop();
        Present();
    }

    /// <summary>Fim: verde (deu certo) ou amarelo (aviso), com um botão opcional. Some depois de alguns segundos.</summary>
    public void Finish(Kind kind, string title, string text, string? actionLabel = null, Action? action = null, int seconds = 10)
    {
        _kind = kind;
        _title = title; _text = text; _progress = null; _foot = "";
        _onAction = action;
        _action.Text = actionLabel ?? "";
        _autoClose.Stop();
        _autoClose.Interval = seconds * 1000;
        _autoClose.Start();
        Present();
    }

    private void Present()
    {
        var hasAction = _kind != Kind.Working && _onAction is not null;
        _action.Visible = hasAction;
        var h = 74;
        if (_progress is not null) h += 18;
        if (_foot.Length > 0) h += 20;
        if (hasAction) h += 44;
        ClientSize = new Size(W, h);
        _action.Location = new Point(W - 16 - _action.Width, h - 16 - _action.Height);
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
        Invalidate();
        if (!Visible) Show();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        Paint2.Smooth(g);
        var accent = _kind switch { Kind.Done => Theme.Ok, Kind.Warning => Theme.Gold, _ => Theme.Ember };
        using (var top = new Pen(accent, 2f)) g.DrawLine(top, 0, 1, Width, 1);

        // ícone num círculo
        var c = new Rectangle(16, 18, 40, 40);
        using (var disc = new SolidBrush(Theme.Iron)) g.FillEllipse(disc, c);
        using (var ring = new Pen(accent, 1.4f)) g.DrawEllipse(ring, c);
        var glyph = _kind switch { Kind.Done => "\uE73E", Kind.Warning => "\uE7BA", _ => "\uE896" };
        using (var icons = Paint2.Icons(14f))
            TextRenderer.DrawText(g, glyph, icons, c, _kind == Kind.Working ? Theme.Gold : accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        using (var title = Paint2.Semi(10.5f))
            TextRenderer.DrawText(g, _title, title, new Rectangle(68, 18, W - 68 - 36, 20), Theme.Bone, flags);
        using (var body = Paint2.Ui(9f))
            TextRenderer.DrawText(g, _text, body, new Rectangle(68, 40, W - 68 - 16, 20), Theme.Ash, flags);

        var y = 74;
        if (_progress is double p)
        {
            var bar = new RectangleF(16, y, W - 32, 6);
            using (var track = Paint2.Round(bar, 3)) using (var b = new SolidBrush(Theme.Iron2)) g.FillPath(b, track);
            var fill = new RectangleF(bar.X, bar.Y, Math.Max(6f, (float)(bar.Width * Math.Clamp(p, 0, 1))), bar.Height);
            using (var fp = Paint2.Round(fill, 3)) using (var b = new SolidBrush(Theme.Ember)) g.FillPath(b, fp);
            y += 18;
        }
        if (_foot.Length > 0)
        {
            using var small = Paint2.Ui(8.5f);
            TextRenderer.DrawText(g, _foot, small, new Rectangle(16, y, W - 32, 18), Theme.Ash, flags);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _autoClose.Dispose();
        base.Dispose(disposing);
    }
}
