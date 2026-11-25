// Peças visuais do app no tema da guilda (as mesmas do site): cartão de ferro, número do passo, caixa de texto,
// botão forjado, chave liga/desliga, abas, bolinha de estado e o javali no círculo. Tudo desenhado aqui, sem as
// bordas brancas do Windows.
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace InsanosApp.Ui;

internal static class Paint2
{
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    public static Font Ui(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static Font Semi(float size) => new("Segoe UI Semibold", size);

    /// <summary>Ícones do próprio Windows (Segoe Fluent Icons no 11, Segoe MDL2 Assets no 10; os códigos são os mesmos).</summary>
    public static Font Icons(float size)
    {
        var f = new Font("Segoe Fluent Icons", size);
        if (f.Name == "Segoe Fluent Icons") return f;
        f.Dispose();
        return new Font("Segoe MDL2 Assets", size);
    }
}

/// <summary>Cartão de ferro com borda fina; Hot = borda em brasa (o destaque).</summary>
internal class Card : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Hot { get; set; }

    public Card()
    {
        DoubleBuffered = true;
        BackColor = Theme.Ink;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Ink);
        Paint2.Smooth(e.Graphics);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Paint2.Round(r, 8);
        using var bg = new LinearGradientBrush(new PointF(0, 0), new PointF(0, Height), Color.FromArgb(0x22, 0x17, 0x15), Color.FromArgb(0x17, 0x0f, 0x0e));
        e.Graphics.FillPath(bg, path);
        using var pen = new Pen(Hot ? Theme.Ember : Theme.Seam, Hot ? 1.4f : 1f);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Número do passo num círculo em brasa.</summary>
internal sealed class StepBadge : Control
{
    public StepBadge(string n)
    {
        Text = n;
        Size = new Size(22, 22);
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Paint2.Semi(9f);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Paint2.Smooth(e.Graphics);
        using var b = new LinearGradientBrush(ClientRectangle, Theme.Gold, Theme.Ember, 90f);
        e.Graphics.FillEllipse(b, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.FromArgb(0x2b, 0x09, 0x04),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Caixa de texto escura com cantos arredondados; a borda acende quando está em foco.</summary>
internal sealed class InputBox : Panel
{
    public TextBox Box { get; } = new()
    {
        BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(0x0d, 0x08, 0x07), ForeColor = Theme.Bone,
        Font = Paint2.Ui(10f)
    };

    public InputBox()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Iron;
        Padding = new Padding(10, 0, 10, 0);
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Click += (_, _) => Box.Focus();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Box.SetBounds(10, (Height - Box.PreferredHeight) / 2 + 1, Width - 20, Box.PreferredHeight);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Iron);
        Paint2.Smooth(e.Graphics);
        using var path = Paint2.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 6);
        using var bg = new SolidBrush(Box.BackColor);
        e.Graphics.FillPath(bg, path);
        using var pen = new Pen(Box.Focused ? Theme.Ember : Theme.Seam, 1f);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Botão forjado: Primary = metal derretido (dourado → brasa → vermelho), senão ferro quente.</summary>
internal sealed class ForgeButton : Control
{
    private bool _hover, _down;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Primary { get; set; }
    /// <summary>Ícone do Windows desenhado à esquerda do texto (opcional).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string? Glyph { get; set; }

    public ForgeButton(string text, bool primary = false)
    {
        Text = text;
        Primary = primary;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        BackColor = Color.Transparent;
        Font = Paint2.Semi(9.5f);
        TabStop = true;
        Size = new Size(TextRenderer.MeasureText(text, Font).Width + 32, 34);
    }

    public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Paint2.Smooth(g);
        var r = new RectangleF(1, _down ? 2 : 1, Width - 3, Height - 3);
        using var path = Paint2.Round(r, 6);
        if (Primary)
        {
            var top = Enabled ? (_hover ? Color.FromArgb(0xff, 0xd2, 0x7a) : Color.FromArgb(0xff, 0xc0, 0x61)) : Color.FromArgb(0x6a, 0x50, 0x3a);
            var bot = Enabled ? (_hover ? Color.FromArgb(0xff, 0x6a, 0x1f) : Theme.Lava) : Color.FromArgb(0x4a, 0x24, 0x1c);
            using var b = new LinearGradientBrush(new PointF(0, 0), new PointF(0, Height), top, bot);
            g.FillPath(b, path);
            using var pen = new Pen(Color.FromArgb(0x2a, 0x0c, 0x07), 1.5f);
            g.DrawPath(pen, path);
            using var shine = new Pen(Color.FromArgb(90, 255, 240, 200), 1f);
            g.DrawLine(shine, r.X + 6, r.Y + 2, r.Right - 6, r.Y + 2);
        }
        else
        {
            using var b = new LinearGradientBrush(new PointF(0, 0), new PointF(0, Height), Color.FromArgb(0x2b, 0x1c, 0x19), Color.FromArgb(0x16, 0x0e, 0x0d));
            g.FillPath(b, path);
            using var pen = new Pen(_hover || Focused ? Theme.Ember : Color.FromArgb(0x5a, 0x2a, 0x1c), 1.2f);
            g.DrawPath(pen, path);
        }
        var color = Primary ? Color.FromArgb(0x2b, 0x09, 0x04) : (Enabled ? (_hover ? Color.White : Theme.Bone) : Theme.Ash);
        if (string.IsNullOrEmpty(Glyph))
        {
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        // ícone + texto, os dois centralizados juntos
        using var icons = Paint2.Icons(Font.Size + 1.5f);
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
        var gw = TextRenderer.MeasureText(g, Glyph, icons, Size.Empty, flags).Width;
        var tw = TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags).Width;
        var x = (int)(r.X + (r.Width - gw - 8 - tw) / 2);
        var y = (int)r.Y;
        var h = (int)r.Height;
        TextRenderer.DrawText(g, Glyph, icons, new Rectangle(x, y, gw + 2, h), Primary ? color : Theme.Gold, flags);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x + gw + 8, y, tw + 4, h), color, flags);
    }
}

/// <summary>Chave liga/desliga em brasa.</summary>
internal sealed class ToggleSwitch : Control
{
    private bool _checked;
    public event EventHandler? CheckedChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Checked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    public ToggleSwitch()
    {
        Size = new Size(38, 20);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        TabStop = true;
    }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Paint2.Smooth(e.Graphics);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Paint2.Round(r, r.Height / 2);
        using var bg = new SolidBrush(Checked ? Theme.Ember : Color.FromArgb(0x2a, 0x1c, 0x19));
        e.Graphics.FillPath(bg, path);
        using var pen = new Pen(Checked ? Theme.Gold : Theme.Seam);
        e.Graphics.DrawPath(pen, path);
        var d = Height - 6f;
        var x = Checked ? Width - d - 4 : 3;
        using var knob = new SolidBrush(Checked ? Color.FromArgb(0x2b, 0x09, 0x04) : Theme.Ash);
        e.Graphics.FillEllipse(knob, x, 3, d, d);
    }
}

/// <summary>Abas lado a lado (escolher a pasta do addon); itens que não existem ficam apagados.</summary>
internal sealed class Segmented : Control
{
    private readonly List<(string Value, string Label)> _items = [];
    private readonly HashSet<string> _enabled = [];
    private int _selected = -1;
    public event EventHandler? SelectedChanged;

    public Segmented()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Paint2.Semi(9f);
        Height = 28;
        Cursor = Cursors.Hand;
    }

    public void SetItems(IEnumerable<(string Value, string Label)> items)
    {
        _items.Clear();
        _items.AddRange(items);
        Invalidate();
    }

    public void SetEnabledValues(IEnumerable<string> values)
    {
        _enabled.Clear();
        foreach (var v in values) _enabled.Add(v);
        if (_selected < 0 || !_enabled.Contains(_items[_selected].Value))
            _selected = _items.FindIndex(i => _enabled.Contains(i.Value));
        Invalidate();
        SelectedChanged?.Invoke(this, EventArgs.Empty);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string? SelectedValue
    {
        get => _selected >= 0 && _selected < _items.Count ? _items[_selected].Value : null;
        set
        {
            var i = _items.FindIndex(x => x.Value == value);
            if (i >= 0 && _enabled.Contains(value!)) { _selected = i; Invalidate(); SelectedChanged?.Invoke(this, EventArgs.Empty); }
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (_items.Count == 0) return;
        var i = Math.Clamp(e.X * _items.Count / Math.Max(1, Width), 0, _items.Count - 1);
        if (_enabled.Contains(_items[i].Value)) SelectedValue = _items[i].Value;
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_items.Count == 0) return;
        var g = e.Graphics;
        Paint2.Smooth(g);
        var outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Paint2.Round(outer, 6);
        using var bg = new SolidBrush(Color.FromArgb(0x0d, 0x08, 0x07));
        g.FillPath(bg, path);
        var w = outer.Width / _items.Count;
        for (var i = 0; i < _items.Count; i++)
        {
            var cell = new RectangleF(outer.X + i * w + 2, outer.Y + 2, w - 4, outer.Height - 4);
            var on = i == _selected;
            var enabled = _enabled.Contains(_items[i].Value);
            if (on)
            {
                using var cp = Paint2.Round(cell, 4);
                using var b = new LinearGradientBrush(new PointF(0, cell.Top), new PointF(0, cell.Bottom), Theme.Gold, Theme.Ember);
                g.FillPath(b, cp);
            }
            var color = on ? Color.FromArgb(0x2b, 0x09, 0x04) : enabled ? Theme.Bone : Color.FromArgb(0x6a, 0x5a, 0x50);
            TextRenderer.DrawText(g, _items[i].Label, Font, Rectangle.Round(cell), color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        using var pen = new Pen(Theme.Seam);
        g.DrawPath(pen, path);
    }
}

/// <summary>Bolinha de estado (verde, âmbar ou vermelha) com brilho.</summary>
internal sealed class Dot : Control
{
    private Color _color = Theme.Ok;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color Color { get => _color; set { _color = value; Invalidate(); } }

    public Dot()
    {
        Size = new Size(12, 12);
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Paint2.Smooth(e.Graphics);
        using var glow = new SolidBrush(Color.FromArgb(70, _color));
        e.Graphics.FillEllipse(glow, 0, 0, Width - 1, Height - 1);
        using var b = new SolidBrush(_color);
        e.Graphics.FillEllipse(b, 3, 3, Width - 7, Height - 7);
    }
}

/// <summary>Uma imagem do app (o nome em fogo) desenhada sem distorcer, com fundo transparente.</summary>
internal sealed class ArtImage : Control
{
    private readonly Image? _img;

    public ArtImage(Image? img, int width)
    {
        _img = img;
        var ratio = img is null ? 0.5 : (double)img.Height / img.Width;
        Size = new Size(width, (int)Math.Round(width * ratio));
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_img is null) return;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_img, ClientRectangle);
    }
}
