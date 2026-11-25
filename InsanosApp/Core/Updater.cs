// Atualização automática do .exe portátil (ideia do companion do GuildOS, que confere cada versão com minisign; o
// código é nosso). O app pergunta ao GitHub qual é o release mais novo de sklta/insanos-app, baixa o .exe e a
// assinatura (.sig), e só troca o arquivo se as duas conferências passarem:
//   1. o SHA-256 do arquivo baixado é o que está assinado;
//   2. a assinatura (ECDSA P-256) bate com a chave pública abaixo, que vem dentro do app.
// A chave secreta fica só nos segredos do repositório (UPDATE_SIGNING_KEY) e o workflow release.yml assina cada
// .exe que ele mesmo montou. Quem conseguisse pôr um .exe falso no GitHub não teria a chave: o app recusa.
// A assinatura cobre também a versão ("insanos-app", versão, hash), para ninguém reapresentar um .exe antigo e
// assinado como se fosse uma versão nova. Downloads só por HTTPS e só dos endereços do GitHub, com limite de tamanho.
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InsanosApp.Core;

public sealed record ReleaseInfo(Version Version, string Page, string ExeUrl, string SigUrl, long ExeSize);

public enum UpdateStep { Ready, NotWritable, DownloadFailed, BadSignature }

public sealed record UpdatePrepared(UpdateStep Step, string? File = null);

public static class Updater
{
    public const string Repo = "sklta/insanos-app";
    public const string LatestApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
    public const string ReleasesPage = "https://github.com/" + Repo + "/releases/latest";
    public const long MaxExeBytes = 150L * 1024 * 1024;
    private const int MaxSmallBytes = 512 * 1024;

    /// <summary>Chave pública da assinatura das atualizações (ECDSA P-256, SubjectPublicKeyInfo em base64).</summary>
    public const string PublicKey =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEs9ch/CJmBGGgp1hZj7xv8X8kOMH3tbAo1b51cKSCnpi7Melk/J1TIuk7RIzetmpoD3GFiqA1PUfMgzQ8wknKCA==";

    private static readonly string[] Hosts =
        ["github.com", "api.github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    /// <summary>"v0.2.0" vira 0.2.0. Pré-lançamento ("v0.2.0-beta.1") e texto estranho: null (não atualiza).</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var t = tag.Trim().TrimStart('v', 'V');
        if (t.Contains('-') || !Version.TryParse(t, out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }

    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    public static bool IsNewer(Version remote, Version current) => Normalize(remote) > Normalize(current);

    /// <summary>Endereço que o app aceita baixar: só HTTPS e só do GitHub.</summary>
    public static bool AllowedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
        && Hosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase);

    /// <summary>O release mais novo, se tiver o .exe e o .sig com os nomes esperados. Qualquer coisa fora disso: null.</summary>
    public static ReleaseInfo? ParseRelease(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True) return null;
            var v = ParseVersion(root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null);
            if (v is null || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
            var exeName = $"InsanosApp-{v.ToString(3)}.exe";
            string? exe = null, sig = null;
            long size = 0;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                if (!AllowedUrl(url)) continue;
                if (name == exeName) { exe = url; size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0; }
                else if (name == exeName + ".sig") sig = url;
            }
            if (exe is null || sig is null || size <= 0 || size > MaxExeBytes) return null;
            var page = root.TryGetProperty("html_url", out var h) && AllowedUrl(h.GetString()) ? h.GetString()! : ReleasesPage;
            return new ReleaseInfo(v, page, exe, sig, size);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    /// <summary>O texto que o workflow assina: "insanos-app", a versão e o SHA-256 do .exe, um por linha.</summary>
    public static byte[] SignedMessage(Version v, string sha256Hex) =>
        Encoding.UTF8.GetBytes("insanos-app\n" + Normalize(v).ToString(3) + "\n" + sha256Hex.Trim().ToLowerInvariant());

    public static bool VerifySignature(Version v, string sha256Hex, string? signatureBase64, string publicKeyBase64 = PublicKey)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64)) return false;
        try
        {
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return ec.VerifyData(SignedMessage(v, sha256Hex), Convert.FromBase64String(signatureBase64.Trim()), HashAlgorithmName.SHA256);
        }
        catch (Exception e) when (e is FormatException or CryptographicException) { return false; }
    }

    public static string Sha256Of(string file)
    {
        using var s = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    /// <summary>Pergunta ao GitHub pelo release mais novo. Sem rede ou resposta estranha: null.</summary>
    public static async Task<ReleaseInfo?> CheckAsync(HttpClient http, CancellationToken ct = default)
    {
        var bytes = await GetSmallAsync(http, LatestApi, ct);
        return bytes is null ? null : ParseRelease(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// Baixa o .exe novo para junto do atual (InsanosApp.exe.download) e confere hash e assinatura. Só devolve Ready
    /// com um arquivo que passou nas duas; senão apaga o que baixou.
    /// </summary>
    public static async Task<UpdatePrepared> PrepareAsync(HttpClient http, ReleaseInfo r, string exePath, CancellationToken ct = default, string publicKey = PublicKey)
    {
        var dir = Path.GetDirectoryName(exePath);
        if (dir is null || !CanWrite(dir)) return new(UpdateStep.NotWritable);
        var part = exePath + ".download";
        try
        {
            if (!await DownloadAsync(http, r.ExeUrl, part, Math.Min(MaxExeBytes, r.ExeSize), ct)) return Fail(UpdateStep.DownloadFailed);
            var sig = await GetSmallAsync(http, r.SigUrl, ct);
            if (sig is null) return Fail(UpdateStep.DownloadFailed);
            if (!VerifySignature(r.Version, Sha256Of(part), Encoding.ASCII.GetString(sig), publicKey)) return Fail(UpdateStep.BadSignature);
            return new(UpdateStep.Ready, part);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            return Fail(UpdateStep.DownloadFailed);
        }

        UpdatePrepared Fail(UpdateStep step)
        {
            try { File.Delete(part); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            return new(step);
        }
    }

    /// <summary>
    /// Põe o .exe novo no lugar do atual: o atual vira InsanosApp.exe.old (o Windows deixa renomear um programa
    /// aberto) e o novo entra com o nome dele. Se a segunda parte falhar, o antigo volta.
    /// </summary>
    public static bool Swap(string exePath, string newFile)
    {
        var old = exePath + ".old";
        try
        {
            if (File.Exists(old)) File.Delete(old);
            File.Move(exePath, old);
            try { File.Move(newFile, exePath); }
            catch (Exception) { File.Move(old, exePath); throw; }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Na abertura: apaga o .old de uma atualização anterior e um download que ficou pela metade.</summary>
    public static void CleanUp(string exePath)
    {
        foreach (var f in new[] { exePath + ".old", exePath + ".download" })
        {
            try { if (File.Exists(f)) File.Delete(f); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private static bool CanWrite(string dir)
    {
        var probe = Path.Combine(dir, ".insanos-write-test");
        try { File.WriteAllText(probe, ""); File.Delete(probe); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    private static HttpRequestMessage Get(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (url.StartsWith("https://api.github.com/", StringComparison.Ordinal))
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return req;
    }

    /// <summary>Segue até 5 redirecionamentos, cada um conferido (HTTPS e GitHub). Devolve a resposta final ou null.</summary>
    private static async Task<HttpResponseMessage?> SendFollowing(HttpClient http, string url, CancellationToken ct)
    {
        for (var hop = 0; hop < 6; hop++)
        {
            if (!AllowedUrl(url)) return null;
            using var req = Get(url);
            var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var code = (int)res.StatusCode;
            if (code is >= 300 and < 400 && res.Headers.Location is { } loc)
            {
                url = (loc.IsAbsoluteUri ? loc : new Uri(new Uri(url), loc)).ToString();
                res.Dispose();
                continue;
            }
            if (res.StatusCode == HttpStatusCode.OK) return res;
            res.Dispose();
            return null;
        }
        return null;
    }

    private static async Task<byte[]?> GetSmallAsync(HttpClient http, string url, CancellationToken ct)
    {
        try
        {
            using var res = await SendFollowing(http, url, ct);
            if (res is null || res.Content.Headers.ContentLength > MaxSmallBytes) return null;
            await using var s = await res.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            var buf = new byte[16 * 1024];
            int n;
            while ((n = await s.ReadAsync(buf, ct)) > 0)
            {
                if (ms.Length + n > MaxSmallBytes) return null;
                ms.Write(buf, 0, n);
            }
            return ms.ToArray();
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException) { return null; }
    }

    private static async Task<bool> DownloadAsync(HttpClient http, string url, string dest, long max, CancellationToken ct)
    {
        using var res = await SendFollowing(http, url, ct);
        if (res is null || res.Content.Headers.ContentLength > max) return false;
        await using var s = await res.Content.ReadAsStreamAsync(ct);
        await using (var f = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buf = new byte[81920];
            long total = 0;
            int n;
            while ((n = await s.ReadAsync(buf, ct)) > 0)
            {
                total += n;
                if (total > max) return false;
                await f.WriteAsync(buf.AsMemory(0, n), ct);
            }
            if (total != max) return false; // tamanho diferente do que o GitHub anunciou: arquivo incompleto ou outro
        }
        return true;
    }
}
