// Trava da janela de status com o WoW Forever fechado: uma camada por cima de tudo, com o conteúdo de trás
// esmaecido e desfocado (uma foto da própria janela, reduzida e escurecida), um ícone de controle pulsando e o aviso
// no meio. Some sozinha quando o jogo abre. O botão Configurar fica, para quem escolheu a pasta errada.
using System.Drawing.Drawing2D;

namespace InsanosApp.Ui;

internal sealed class WaitOverlay : Control
{
    private Bitmap? _backdrop;
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 50 };
    private double _t;

    public ForgeButton Setup { get; } = new("Configurar") { Glyph = "\uE713" };

    public WaitOverlay()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Ink;
        Cursor = Cursors.Default;
        Visible = false;
        Setup.Size = new Size(132, 34);
        Controls.Add(Setup);
        _anim.Tick += (_, _) => { _t += 0.06; Invalidate(IconArea()); };
    }

    private Point Center => new(Width / 2, Height / 2 - 46);
    private Rectangle IconArea() => new(Center.X - 60, Center.Y - 60, 120, 120);

    /// <summary>A foto da janela que fica atrás (esmaecida aqui). Null: só o fundo escuro.</summary>
    public void SetBackdrop(Bitmap? shot)
    {
        _backdrop?.Dispose();
        _backdrop = shot is null ? null : Soften(shot);
        shot?.Dispose();
        Invalidate();
    }

    // desfoque barato: reduz a 1/8 e amplia de volta suavizando
    private static Bitmap Soften(Bitmap src)
    {
        var small = new Bitmap(Math.Max(1, src.Width / 8), Math.Max(1, src.Height / 8));
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(src, new Rectangle(Point.Empty, small.Size));
        }
        var big = new Bitmap(src.Width, src.Height);
        using (var g = Graphics.FromImage(big))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(small, new Rectangle(Point.Empty, big.Size));
        }
        small.Dispose();
        return big;
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (Visible) _anim.Start(); else _anim.Stop();
        base.OnVisibleChanged(e);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Setup.Location = new Point((Width - Setup.Width) / 2, Center.Y + 104);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (_backdrop is not null) g.DrawImage(_backdrop, ClientRectangle);
        else g.Clear(Theme.Ink);
        using (var dim = new SolidBrush(Color.FromArgb(185, 11, 6, 6))) g.FillRectangle(dim, ClientRectangle);

        Paint2.Smooth(g);
        var c = Center;
        // anel que pulsa em volta do ícone
        var pulse = (Math.Sin(_t * 2.2) + 1) / 2;
        var ring = 36 + (float)(pulse * 10);
        using (var halo = new Pen(Color.FromArgb((int)(40 + 90 * (1 - pulse)), Theme.Ember), 2f))
            g.DrawEllipse(halo, c.X - ring, c.Y - ring, ring * 2, ring * 2);
        using (var disc = new SolidBrush(Theme.Iron)) g.FillEllipse(disc, c.X - 32, c.Y - 32, 64, 64);
        using (var edge = new Pen(Theme.Ember, 1.6f)) g.DrawEllipse(edge, c.X - 32, c.Y - 32, 64, 64);
        using (var icons = Paint2.Icons(22f))
            TextRenderer.DrawText(g, "\uE7FC", icons, new Rectangle(c.X - 32, c.Y - 32, 64, 64), Theme.Gold,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        using var title = Paint2.Semi(12.5f);
        using var note = Paint2.Ui(9.5f);
        var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak;
        TextRenderer.DrawText(g, "Esperando o WoW Forever", title, new Rectangle(20, c.Y + 50, Width - 40, 28), Theme.Bone, center);
        TextRenderer.DrawText(g, "Abra o jogo: o app começa a trazer as raids sozinho.", note,
            new Rectangle(28, c.Y + 78, Width - 56, 22), Theme.Ash, center);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _anim.Dispose(); _backdrop?.Dispose(); }
        base.Dispose(disposing);
    }
}
