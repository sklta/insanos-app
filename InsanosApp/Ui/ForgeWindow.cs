// Janela sem a barra de título do Windows: o app desenha a própria borda (ferro com um fio de brasa em cima) e um
// X de fechar. Arrasta clicando em qualquer parte vazia, tem sombra e, no Windows 11, cantos arredondados.
using System.Runtime.InteropServices;

namespace InsanosApp.Ui;

internal class ForgeWindow : Form
{
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int WmNcLButtonDown = 0xA1;
    private const int HtCaption = 2;
    private const int CsDropShadow = 0x00020000;
    private const int CornerPreference = 33;   // DWMWA_WINDOW_CORNER_PREFERENCE
    private const int Round = 2;               // DWMWCP_ROUND

    protected readonly Label CloseButton = new()
    {
        Text = "✕", AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.Ash, Cursor = Cursors.Hand,
        Font = new Font("Segoe UI", 11f)
    };

    public ForgeWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Ink;
        ForeColor = Theme.Bone;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        KeyPreview = true;
        CloseButton.MouseEnter += (_, _) => CloseButton.ForeColor = Theme.Bone;
        CloseButton.MouseLeave += (_, _) => CloseButton.ForeColor = Theme.Ash;
        CloseButton.Click += (_, _) => OnCloseClicked();
        Controls.Add(CloseButton);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) OnCloseClicked(); };
    }

    /// <summary>O que o X (e o Esc) fazem; por padrão fecha a janela.</summary>
    protected virtual void OnCloseClicked() => Close();

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CsDropShadow;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { var v = Round; DwmSetWindowAttribute(Handle, CornerPreference, ref v, sizeof(int)); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        CloseButton.Location = new Point(ClientSize.Width - CloseButton.Width - 12, 10);
        CloseButton.BringToFront();
    }

    /// <summary>Arrastar a janela por qualquer parte vazia (a borda dela, os rótulos, os cartões).</summary>
    protected void MakeDraggable(Control c)
    {
        c.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WmNcLButtonDown, HtCaption, IntPtr.Zero);
        };
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WmNcLButtonDown, HtCaption, IntPtr.Zero);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Seam);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        using var top = new Pen(Theme.Ember, 2f);
        e.Graphics.DrawLine(top, 0, 1, Width, 1);
    }
}
