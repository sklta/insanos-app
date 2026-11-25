// O WoW Forever está aberto? O app só pergunta ao bot e mexe no Inbox.lua com o jogo rodando (26 nov 2026): com o
// jogo fechado ele fica parado, sem gastar internet nem chamar o bot à toa.
//
// Como confere: procura processos chamados "Wow" e pergunta ao Windows só o caminho do .exe de cada um
// (QueryFullProcessImageName com PROCESS_QUERY_LIMITED_INFORMATION, a permissão mais baixa que existe). Isso não lê a
// memória do jogo, não abre o processo para escrita e não injeta nada: é o mesmo que o Gerenciador de Tarefas mostra
// na coluna "Caminho". O jogo certo é o Wow.exe da pasta _forever_ que a pessoa escolheu na configuração.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace InsanosApp.Core;

public static class GameWatch
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>O Wow.exe esperado para esta pasta AddOns (…\_forever_\Interface\AddOns → …\_forever_\Wow.exe).</summary>
    public static string? ExpectedExe(string? addonsDir)
    {
        if (string.IsNullOrWhiteSpace(addonsDir)) return null;
        try
        {
            var root = Directory.GetParent(Path.GetFullPath(addonsDir).TrimEnd('\\', '/'))?.Parent;
            return root is null ? null : Path.Combine(root.FullName, "Wow.exe");
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    /// <summary>Este caminho de processo é o WoW Forever da configuração?</summary>
    public static bool IsForever(string? processPath, string? addonsDir)
    {
        var expected = ExpectedExe(addonsDir);
        if (expected is null || string.IsNullOrWhiteSpace(processPath)) return false;
        try { return string.Equals(Path.GetFullPath(processPath), expected, StringComparison.OrdinalIgnoreCase); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>
    /// O WoW Forever está rodando? Um "Wow" cujo caminho o Windows não informa (jogo aberto como administrador, por
    /// exemplo) conta como aberto: na dúvida o app trabalha, em vez de ficar parado com o jogo na tela.
    /// </summary>
    public static bool IsRunning(string? addonsDir)
    {
        Process[] procs;
        try { procs = Process.GetProcessesByName("Wow"); }
        catch (InvalidOperationException) { return true; }
        try
        {
            foreach (var p in procs)
            {
                var path = ImagePath(p.Id);
                if (path is null || IsForever(path, addonsDir)) return true;
            }
            return false;
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
        }
    }

    private static string? ImagePath(int pid)
    {
        var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            var size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally { CloseHandle(h); }
    }
}
