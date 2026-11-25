// "Iniciar com o Windows": um valor em HKCU\...\Run, só da conta do usuário (não pede administrador). Desligado
// até a pessoa marcar.
using Microsoft.Win32;

namespace InsanosApp;

internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "InsanosApp";

    public static bool IsEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(Name) is string v && v.Contains(Environment.ProcessPath ?? "", StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on && Environment.ProcessPath is { } exe) k.SetValue(Name, "\"" + exe + "\" --tray");
        else k.DeleteValue(Name, throwOnMissingValue: false);
    }
}
