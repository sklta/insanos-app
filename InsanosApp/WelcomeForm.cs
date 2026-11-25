// Tela de boas-vindas: aparece quando a pessoa abre o app pelo .exe (não quando ele abre junto com o Windows), antes
// de ele ir para perto do relógio. Mostra o brasão, o que o app faz, três garantias e os links da guilda. "Mostrar
// ao abrir o app" desligado faz ela não aparecer mais (o menu do ícone abre de novo).
using InsanosApp.Ui;

namespace InsanosApp;

internal sealed class WelcomeForm : ForgeWindow
{
    private readonly ToggleSwitch _show = new();

    /// <summary>Se a pessoa quer ver esta tela da próxima vez que abrir o app.</summary>
    public bool ShowNextTime => _show.Checked;

    /// <param name="configured">Já configurado: o botão principal leva à bandeja; senão, à configuração.</param>
    /// <param name="showNextTime">Como a opção "Mostrar ao abrir o app" começa.</param>
    public WelcomeForm(bool configured, bool showNextTime)
    {
        Text = "App Insanos";
        Icon = Theme.AppIcon();
        StartPosition = FormStartPosition.CenterScreen;
        Font = Paint2.Ui(9.5f);
        ClientSize = new Size(520, 604);

        const int W = 520, Pad = 28;
        var crest = new ArtImage(Theme.Crest(), 156) { Location = new Point((W - 156) / 2, 22) };
        var eyebrow = Centered("APP DA GUILDA  ·  WOW FOREVER", Paint2.Semi(8.5f), Theme.Gold, 188, 20);
        var about = Centered(
            "As raids marcadas no Discord chegam sozinhas no addon. Depois de configurar, o app fica perto do relógio "
            + "e trabalha sem você precisar abrir.", Paint2.Ui(10f), Theme.Ash, 212, 64);

        // três garantias, para quem desconfia de programa de fora
        string[][] promises =
        [
            ["\uE72E", "Código aberto"],
            ["\uE8A5", "Escreve só no addon"],
            ["\uE7FC", "Não mexe no jogo"]
        ];
        var cards = new List<Control>();
        var cw = (W - Pad * 2 - 16) / 3;
        for (var i = 0; i < promises.Length; i++)
        {
            var card = new Card { Location = new Point(Pad + i * (cw + 8), 294), Size = new Size(cw, 64) };
            var glyph = new Label
            {
                Text = promises[i][0], Font = Paint2.Icons(15f), ForeColor = Theme.Ok, BackColor = Color.Transparent,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(0, 8, cw, 24)
            };
            var text = new Label
            {
                Text = promises[i][1], Font = Paint2.Ui(9f), ForeColor = Theme.Bone, BackColor = Color.Transparent,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(0, 34, cw, 22)
            };
            card.Controls.AddRange([glyph, text]);
            MakeDraggable(card); MakeDraggable(glyph); MakeDraggable(text);
            cards.Add(card);
        }

        // links da guilda, dois por linha
        (string Glyph, string Text, string Url)[] links =
        [
            ("\uE774", "Site da guilda", Links.Site),
            ("\uE8BD", "Discord", Links.Discord),
            ("\uE896", "Addon no CurseForge", Links.Addon),
            ("\uE943", "Código no GitHub", Links.Code)
        ];
        var bw = (W - Pad * 2 - 8) / 2;
        for (var i = 0; i < links.Length; i++)
        {
            var (glyph, text, url) = links[i];
            var b = new ForgeButton(text) { Glyph = glyph, Location = new Point(Pad + i % 2 * (bw + 8), 376 + i / 2 * 44), Size = new Size(bw, 36) };
            b.Click += (_, _) => Links.Open(url);
            cards.Add(b);
        }

        var line = new Panel { BackColor = Theme.Seam, Bounds = new Rectangle(Pad, 486, W - Pad * 2, 1) };

        _show.Checked = showNextTime;
        _show.Location = new Point(Pad, 528);
        var showLabel = new Label
        {
            Text = "Mostrar ao abrir o app", ForeColor = Theme.Ash, AutoSize = true, BackColor = Color.Transparent,
            Location = new Point(Pad + 46, 528), Cursor = Cursors.Hand
        };
        showLabel.Click += (_, _) => _show.Checked = !_show.Checked;

        var go = new ForgeButton(configured ? "Ir para a bandeja" : "Começar", primary: true);
        go.Size = new Size(Math.Max(150, go.Width), 40);
        go.Location = new Point(W - Pad - go.Width, 516);
        go.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        AcceptButton = null;

        Controls.AddRange([crest, eyebrow, about, .. cards, line, _show, showLabel, go]);
        foreach (var c in new Control[] { crest, eyebrow, about }) MakeDraggable(c);
        Shown += (_, _) => go.Focus();
    }

    // o X e o Esc também seguem em frente (para a configuração ou para a bandeja); sair é pelo menu do ícone
    protected override void OnCloseClicked() { DialogResult = DialogResult.OK; Close(); }

    private static Label Centered(string text, Font font, Color color, int y, int height) => new()
    {
        Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent, AutoSize = false,
        TextAlign = ContentAlignment.TopCenter, Bounds = new Rectangle(40, y, 440, height)
    };
}
