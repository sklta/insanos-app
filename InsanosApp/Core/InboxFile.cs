// O arquivo que o app escreve na pasta do addon (Interface\AddOns\NilbasForever\Inbox.lua). O jogo executa esse
// arquivo no login e no /reload, então nada vindo da internet entra sem conferir: só chaves !NILBAS com caracteres
// de base64, até 10 chaves e 12 000 caracteres cada (os mesmos limites que o addon confere de novo do lado dele).
using System.Text;
using System.Text.RegularExpressions;

namespace InsanosApp.Core;

public static partial class InboxFile
{
    public const int MaxKeys = 10;
    public const int MaxKeyLength = 12000;

    [GeneratedRegex("^!NILBAS:[A-Za-z0-9+/=]+$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("\"(!NILBAS:[A-Za-z0-9+/=]+)\"")]
    private static partial Regex KeyInFile();

    public static bool IsValidKey(string? key) =>
        key is not null && key.Length <= MaxKeyLength && KeyPattern().IsMatch(key);

    /// <summary>As chaves que podem ir para o arquivo, na ordem recebida (a raid mais próxima primeiro).</summary>
    public static IReadOnlyList<string> Sanitize(IEnumerable<string?> keys) =>
        keys.Where(IsValidKey).Select(k => k!).Distinct().Take(MaxKeys).ToList();

    /// <summary>O conteúdo de Inbox.lua para estas chaves; "at" é a hora (Unix) em que a lista mudou.</summary>
    public static string Render(IEnumerable<string?> keys, long at)
    {
        var clean = Sanitize(keys);
        var sb = new StringBuilder();
        sb.Append("-- Escrito pelo app Insanos no PC. Não edite: o app reescreve este arquivo sozinho.\n");
        sb.Append("-- Só dados: o addon confere cada chave antes de usar.\n");
        sb.Append("NilbasForeverInbox = {\n");
        sb.Append("    at = ").Append(at).Append(",\n");
        sb.Append("    raids = {\n");
        foreach (var k in clean) sb.Append("        \"").Append(k).Append("\",\n");
        sb.Append("    },\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>As chaves que já estão num Inbox.lua (vazio se o arquivo não existe ou é o do pacote).</summary>
    public static IReadOnlyList<string> ReadKeys(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            var text = File.ReadAllText(path, Encoding.UTF8);
            return KeyInFile().Matches(text).Select(m => m.Groups[1].Value).ToList();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    /// <summary>
    /// Escreve o arquivo só se a lista de chaves mudou, de forma atômica (arquivo temporário e troca), para o jogo
    /// nunca ler um arquivo pela metade. Devolve true quando escreveu.
    /// </summary>
    public static bool WriteIfChanged(string path, IEnumerable<string?> keys, long now)
    {
        var clean = Sanitize(keys);
        if (File.Exists(path) && ReadKeys(path).SequenceEqual(clean)) return false;
        var dir = Path.GetDirectoryName(path)!;
        var tmp = Path.Combine(dir, "Inbox.lua.tmp");
        File.WriteAllText(tmp, Render(clean, now), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
        return true;
    }
}
