// Acha a pasta de addons do WoW Forever (…\World of Warcraft\_forever_\Interface\AddOns) e o addon dentro dela.
// Só lê pastas: o app nunca mexe no jogo enquanto ele roda.
namespace InsanosApp.Core;

public static class WowLocator
{
    /// <summary>Pastas do addon que o app sabe usar: a da guilda e a cópia de teste.</summary>
    public static readonly string[] AddonFolders = ["NilbasForever", "NilbasForeverTeste"];

    public static IEnumerable<string> Candidates()
    {
        var roots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
            roots.Add(d.RootDirectory.FullName);
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Games"));
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Jogos"));
        }
        foreach (var r in roots.Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return Path.Combine(r, "World of Warcraft", "_forever_", "Interface", "AddOns");
    }

    /// <summary>Aceita a pasta do WoW, a _forever_, a Interface ou a própria AddOns, e devolve a AddOns.</summary>
    public static string? NormalizeAddonsDir(string? picked)
    {
        if (string.IsNullOrWhiteSpace(picked)) return null;
        var p = picked.Trim().TrimEnd('\\', '/');
        string[] tries =
        [
            p,
            Path.Combine(p, "AddOns"),
            Path.Combine(p, "Interface", "AddOns"),
            Path.Combine(p, "_forever_", "Interface", "AddOns")
        ];
        return tries.FirstOrDefault(t => InstalledAddons(t).Count > 0);
    }

    /// <summary>Quais das pastas do addon existem (com o .toc) dentro desta AddOns.</summary>
    public static IReadOnlyList<string> InstalledAddons(string addonsDir)
    {
        try
        {
            return AddonFolders.Where(a => File.Exists(Path.Combine(addonsDir, a, a + ".toc"))).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return []; }
    }

    public static string? Detect() => Candidates().FirstOrDefault(c => InstalledAddons(c).Count > 0);

    public static string InboxPath(string addonsDir, string addonFolder) => Path.Combine(addonsDir, addonFolder, "Inbox.lua");
}
