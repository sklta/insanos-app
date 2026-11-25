// Conversa com o bot (services/appApi.js na Discloud): GET /v1/inbox com a chave da pessoa (Authorization: Bearer).
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace InsanosApp.Core;

public enum InboxStatus { Ok, Unauthorized, RateLimited, Offline, ServerError }

public sealed record InboxRaid(
    [property: JsonPropertyName("gid")] int Gid,
    [property: JsonPropertyName("inst")] string Inst,
    [property: JsonPropertyName("when")] string When,
    [property: JsonPropertyName("ts")] long? Ts,
    [property: JsonPropertyName("rev")] int Rev,
    [property: JsonPropertyName("players")] int Players,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("t")] int? Tanks = null,
    [property: JsonPropertyName("h")] int? Heals = null,
    [property: JsonPropertyName("d")] int? Dps = null)
{
    /// <summary>"sáb 21:00 · 12 inscritos · 2 tanks · 3 heals · 7 dps" (as funções só quando o bot manda).</summary>
    public string Details()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(When)) parts.Add(When);
        parts.Add(Players == 1 ? "1 inscrito" : Players + " inscritos");
        if (Tanks is int t) parts.Add(t == 1 ? "1 tank" : t + " tanks");
        if (Heals is int h) parts.Add(h == 1 ? "1 heal" : h + " heals");
        if (Dps is int d) parts.Add(d + " dps");
        return string.Join(" · ", parts);
    }

    /// <summary>Versão curta para listas: "dom 20:00 · 8 inscritos".</summary>
    public string Short() =>
        (string.IsNullOrEmpty(When) ? "" : When + " · ") + (Players == 1 ? "1 inscrito" : Players + " inscritos");
}

public sealed record InboxPayload(
    [property: JsonPropertyName("v")] int V,
    [property: JsonPropertyName("addon")] string? Addon,
    [property: JsonPropertyName("raids")] List<InboxRaid> Raids,
    [property: JsonPropertyName("poll")] int? Poll = null);

/// <summary>
/// O que o bot respondeu. PollSeconds: em quanto tempo perguntar de novo (o bot decide o ritmo; cabeçalho
/// X-Insanos-Poll ou campo poll). RetryAfterSeconds: quanto esperar quando o bot disse "cedo demais" (429).
/// </summary>
public sealed record InboxResult(InboxStatus Status, InboxPayload? Payload = null, string? Detail = null,
    int? PollSeconds = null, int? RetryAfterSeconds = null);

public static partial class InboxApi
{
    public const string DefaultBase = "https://insanos.discloud.app";

    [GeneratedRegex("^nfapp_[A-Za-z0-9_-]{43}$")]
    private static partial Regex KeyPattern();

    /// <summary>Formato da chave do /vincular-app, antes de gastar uma chamada.</summary>
    public static bool LooksLikeKey(string? key) => key is not null && KeyPattern().IsMatch(key.Trim());

    public static InboxPayload? Parse(string json)
    {
        try
        {
            var p = JsonSerializer.Deserialize<InboxPayload>(json);
            if (p is null || p.Raids is null) return null;
            return p;
        }
        catch (JsonException) { return null; }
    }

    public static async Task<InboxResult> FetchAsync(HttpClient http, string baseUrl, string key, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl.TrimEnd('/') + "/v1/inbox");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        try
        {
            using var res = await http.SendAsync(req, ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return new(InboxStatus.Unauthorized);
            if (res.StatusCode == (HttpStatusCode)429)
                return new(InboxStatus.RateLimited, RetryAfterSeconds: RetryAfter(res));
            if (!res.IsSuccessStatusCode) return new(InboxStatus.ServerError, Detail: ((int)res.StatusCode).ToString());
            var payload = Parse(await res.Content.ReadAsStringAsync(ct));
            if (payload is null) return new(InboxStatus.ServerError, Detail: "resposta inválida");
            return new(InboxStatus.Ok, payload, PollSeconds: HeaderSeconds(res, "X-Insanos-Poll") ?? payload.Poll);
        }
        catch (HttpRequestException e) when (e.Message.Contains("buffer", StringComparison.OrdinalIgnoreCase))
        {
            return new(InboxStatus.ServerError, Detail: "resposta grande demais");
        }
        catch (HttpRequestException e) { return new(InboxStatus.Offline, Detail: e.Message); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new(InboxStatus.Offline, Detail: "tempo esgotado"); }
    }

    private static int? HeaderSeconds(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var s) && s > 0 ? s : null;

    private static int? RetryAfter(HttpResponseMessage res)
    {
        if (res.Headers.RetryAfter?.Delta is TimeSpan d && d > TimeSpan.Zero) return (int)Math.Ceiling(d.TotalSeconds);
        if (res.Headers.RetryAfter?.Date is DateTimeOffset at)
        {
            var left = (int)Math.Ceiling((at - DateTimeOffset.UtcNow).TotalSeconds);
            return left > 0 ? left : null;
        }
        return null;
    }

    /// <summary>A resposta do bot tem poucos KB (até 10 raids); acima disto o app recusa.</summary>
    public const int MaxResponseBytes = 256 * 1024;

    /// <summary>
    /// O cliente do app: 20 s de espera, resposta de no máximo 256 KB e sem seguir redirecionamento (a chave só vai
    /// para o endereço do bot; um 3xx vira erro em vez de mandar o pedido para outro lugar).
    /// </summary>
    public static HttpClient CreateClient(string version, HttpMessageHandler? inner = null)
    {
        var handler = inner ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxResponseHeadersLength = 64
        };
        var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20),
            MaxResponseContentBufferSize = MaxResponseBytes
        };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InsanosApp", version));
        return http;
    }
}
