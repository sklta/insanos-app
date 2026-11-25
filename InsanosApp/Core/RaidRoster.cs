// Quem está em cada raid, lido da própria chave !NILBAS (base64 de "inst=...&tanks=A,B&heals=...&dps=..."), e a
// diferença entre duas verificações: quem entrou e quem saiu. Serve para o aviso "Fulano entrou na raid" e para o
// app saber quando verificar mais rápido (raid perto de começar).
using System.Text;

namespace InsanosApp.Core;

public sealed record RosterChange(int Gid, string Inst, IReadOnlyList<string> Joined, IReadOnlyList<string> Left)
{
    public bool Any => Joined.Count > 0 || Left.Count > 0;

    /// <summary>"Molten Core: Ana e Bia entraram · Caio saiu".</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Joined.Count > 0) parts.Add(Names(Joined) + (Joined.Count == 1 ? " entrou" : " entraram"));
        if (Left.Count > 0) parts.Add(Names(Left) + (Left.Count == 1 ? " saiu" : " saíram"));
        return Inst + ": " + string.Join(" · ", parts);
    }

    private static string Names(IReadOnlyList<string> n) => n.Count switch
    {
        1 => n[0],
        2 => n[0] + " e " + n[1],
        3 => n[0] + ", " + n[1] + " e " + n[2],
        _ => n[0] + ", " + n[1] + " e mais " + (n.Count - 2)
    };
}

public static class RaidRoster
{
    /// <summary>Os campos da chave (inst, tanks, heals, dps...). Chave estragada devolve vazio.</summary>
    public static IReadOnlyDictionary<string, string> Decode(string? token)
    {
        var map = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(token) || !token.StartsWith("!NILBAS:")) return map;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token[8..]));
            foreach (var pair in raw.Split('&'))
            {
                var i = pair.IndexOf('=');
                if (i <= 0) continue;
                map[pair[..i]] = Uri.UnescapeDataString(pair[(i + 1)..]);
            }
        }
        catch (FormatException) { }
        return map;
    }

    /// <summary>Todos os inscritos da raid (tanks, heals e dps), na ordem da chave.</summary>
    public static IReadOnlyList<string> Members(string? token)
    {
        var d = Decode(token);
        var all = new List<string>();
        foreach (var role in new[] { "tanks", "heals", "dps" })
            if (d.TryGetValue(role, out var list))
                all.AddRange(list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return all;
    }

    /// <summary>Quem entrou e quem saiu de cada raid que estava nas duas verificações.</summary>
    public static IReadOnlyList<RosterChange> Diff(IEnumerable<InboxRaid> before, IEnumerable<InboxRaid> after)
    {
        var old = before.GroupBy(r => r.Gid).ToDictionary(g => g.Key, g => g.First());
        var changes = new List<RosterChange>();
        foreach (var now in after)
        {
            if (!old.TryGetValue(now.Gid, out var prev)) continue;
            var a = Members(prev.Token);
            var b = Members(now.Token);
            var joined = b.Where(n => !a.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
            var left = a.Where(n => !b.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
            var change = new RosterChange(now.Gid, now.Inst, joined, left);
            if (change.Any) changes.Add(change);
        }
        return changes;
    }

    /// <summary>
    /// Quando verificar de novo. Quem decide é o bot (ideia do companion do GuildOS): o ritmo que ele mandou, ou o
    /// tempo de espera quando disse "cedo demais". Sem nada do bot (bot antigo), o app calcula sozinho
    /// (PollInterval). Sem conexão, espera cada vez mais (30 s, 1 min, 2 min... até 5 min), para não insistir à toa.
    /// Tudo fica entre 10 s e 10 min.
    /// </summary>
    public static TimeSpan NextCheck(SyncOutcome r, DateTimeOffset now, TimeSpan normal, int failuresInARow)
    {
        static TimeSpan Clamp(TimeSpan t) =>
            t < TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) : t > TimeSpan.FromMinutes(10) ? TimeSpan.FromMinutes(10) : t;
        if (r.Status == InboxStatus.Ok)
            return Clamp(r.PollSeconds is int p ? TimeSpan.FromSeconds(p) : PollInterval(r.Raids, now, normal));
        if (r.Status == InboxStatus.RateLimited && r.RetryAfterSeconds is int w)
            return Clamp(TimeSpan.FromSeconds(w));
        var backoff = TimeSpan.FromSeconds(30 * Math.Pow(2, Math.Clamp(failuresInARow - 1, 0, 4)));
        return Clamp(backoff > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(5) : backoff);
    }

    /// <summary>
    /// De quanto em quanto tempo verificar: 15 segundos quando alguma raid começa nas próximas 3 horas (ou começou há
    /// menos de 1 hora); senão o intervalo normal. Raid sem horário marcado não acelera.
    /// </summary>
    public static TimeSpan PollInterval(IEnumerable<InboxRaid> raids, DateTimeOffset now, TimeSpan normal)
    {
        foreach (var r in raids)
        {
            if (r.Ts is not long ms) continue;
            var start = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            if (start > now - TimeSpan.FromHours(1) && start < now + TimeSpan.FromHours(3)) return TimeSpan.FromSeconds(15);
        }
        return normal;
    }
}
