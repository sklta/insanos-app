// Endereços da guilda que o app abre no navegador (os mesmos do site). Só estes: nada vem da internet.
using System.Diagnostics;

namespace InsanosApp;

internal static class Links
{
    public const string Site = "https://insanos.discloud.app";
    public const string Discord = "https://discord.gg/BszvVez4H";
    public const string Addon = "https://www.curseforge.com/wow/addons/insanos-guild-sync";
    public const string Code = "https://github.com/sklta/insanos-app";

    /// <summary>Abre um endereço ou uma pasta com o programa padrão do Windows.</summary>
    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}
