// Uma rodada: pergunta ao bot, escreve o Inbox.lua se mudou e diz o que aconteceu, para o ícone mostrar.
namespace InsanosApp.Core;

public sealed record SyncOutcome(InboxStatus Status, bool Wrote, InboxRaid? First, int RaidCount, string Message)
{
    public IReadOnlyList<InboxRaid> Raids { get; init; } = [];
    /// <summary>O ritmo que o bot mandou (segundos), quando mandou.</summary>
    public int? PollSeconds { get; init; }
    /// <summary>Quanto esperar quando o bot disse "cedo demais" (segundos).</summary>
    public int? RetryAfterSeconds { get; init; }
    /// <summary>A versão mais nova do app que o bot avisou (texto como veio; o app confere antes de usar).</summary>
    public string? LatestApp { get; init; }
}

public static class InboxSync
{
    public static async Task<SyncOutcome> RunAsync(HttpClient http, AppSettings s, string key, long now, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(s.AddonsDir)) return new(InboxStatus.ServerError, false, null, 0, "Escolha a pasta do WoW Forever.");
        var result = await InboxApi.FetchAsync(http, s.ApiBase, key, ct);
        switch (result.Status)
        {
            case InboxStatus.Unauthorized:
                return new(result.Status, false, null, 0, "Chave recusada: gere outra com /vincular-app no Discord.");
            case InboxStatus.RateLimited:
                return new(result.Status, false, null, 0, "O bot pediu para esperar um pouco. Tento de novo depois.") { RetryAfterSeconds = result.RetryAfterSeconds };
            case InboxStatus.Offline:
                return new(result.Status, false, null, 0, "Sem conexão com o bot. Tento de novo depois.");
            case InboxStatus.ServerError:
                return new(result.Status, false, null, 0, "O bot não respondeu direito (" + result.Detail + "). Tento de novo depois.");
        }

        var raids = result.Payload!.Raids.Where(r => InboxFile.IsValidKey(r.Token)).ToList();
        if (!WowLocator.IsKnownAddon(s.AddonFolder) || !WowLocator.LooksLikeAddonsDir(s.AddonsDir))
            return new(InboxStatus.ServerError, false, null, raids.Count, "Configuração estranha: escolha a pasta do WoW Forever de novo em Configurar.");
        var path = WowLocator.InboxPath(s.AddonsDir, s.AddonFolder);
        if (!Directory.Exists(Path.GetDirectoryName(path)))
            return new(InboxStatus.ServerError, false, null, raids.Count, "Não achei o addon " + (s.AddonFolder == "NilbasForeverTeste" ? "INSANOS (teste)" : "INSANOS") + " na pasta do jogo. Ele está instalado?");
        bool wrote;
        try { wrote = InboxFile.WriteIfChanged(path, raids.Select(r => r.Token), now); }
        catch (UnauthorizedAccessException)
        {
            return new(InboxStatus.ServerError, false, null, raids.Count, "O Windows não deixou escrever na pasta do addon. Abra o app como o mesmo usuário do jogo.");
        }
        catch (IOException e)
        {
            return new(InboxStatus.ServerError, false, null, raids.Count, "Não deu para escrever o arquivo do addon: " + e.Message);
        }
        var first = raids.FirstOrDefault();
        var msg = first is null
            ? "Nenhuma raid aberta sua agora."
            : $"{first.Inst}{(string.IsNullOrEmpty(first.When) ? "" : " · " + first.When)} ({first.Players} inscritos)";
        return new(InboxStatus.Ok, wrote, first, raids.Count, msg) { Raids = raids, PollSeconds = result.PollSeconds, LatestApp = result.Payload.App };
    }
}
