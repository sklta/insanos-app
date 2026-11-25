// Cores da INSANOS para as janelas do app (as mesmas do site e do addon: ferro escuro, brasa e pergaminho).
namespace InsanosApp;

internal static class Theme
{
    public static readonly Color Ink = Color.FromArgb(0x0b, 0x06, 0x06);
    public static readonly Color Iron = Color.FromArgb(0x1d, 0x15, 0x14);
    public static readonly Color Iron2 = Color.FromArgb(0x2a, 0x1c, 0x19);
    public static readonly Color Seam = Color.FromArgb(0x3b, 0x23, 0x20);
    public static readonly Color Ember = Color.FromArgb(0xff, 0x5a, 0x1f);
    public static readonly Color Lava = Color.FromArgb(0xe0, 0x30, 0x2a);
    public static readonly Color Gold = Color.FromArgb(0xff, 0xb3, 0x47);
    public static readonly Color Bone = Color.FromArgb(0xf3, 0xe6, 0xd2);
    public static readonly Color Ash = Color.FromArgb(0xc2, 0xad, 0x96);
    public static readonly Color Ok = Color.FromArgb(0x7c, 0xd9, 0x6b);
    public static readonly Color Bad = Color.FromArgb(0xff, 0x6b, 0x5a);

    public static Icon AppIcon()
    {
        using var s = typeof(Theme).Assembly.GetManifestResourceStream("InsanosApp.app.ico");
        return s is null ? SystemIcons.Application : new Icon(s);
    }

    /// <summary>O nome INSANOS em fogo, do kit visual (o mesmo do addon e do site).</summary>
    public static Image? Wordmark() => Resource("InsanosApp.wordmark.png");

    private static Image? Resource(string name)
    {
        using var s = typeof(Theme).Assembly.GetManifestResourceStream(name);
        return s is null ? null : new Bitmap(Image.FromStream(s));
    }
}
