// Testes do núcleo do App Insanos: o arquivo Inbox.lua (o que entra e o que não entra), a conversa com o bot (com um
// bot de mentira em memória, sem internet), a procura da pasta do addon e as configurações.
using System.Net;
using System.Text;
using InsanosApp.Core;

var pass = 0;
var fail = 0;
void Ok(bool cond, string msg)
{
    if (cond) { pass++; Console.WriteLine("PASS " + msg); }
    else { fail++; Console.WriteLine("FAIL " + msg); }
}

string Key(string payload) => "!NILBAS:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
var k1 = Key("inst=Molten%20Core&gid=7&rev=3&tanks=Tanque");
var k2 = Key("inst=Onyxia&gid=8&rev=1&tanks=Malf");
var tmp = Directory.CreateTempSubdirectory("insanosapp-");

try
{
    // ---------- Inbox.lua ----------
    Ok(InboxFile.IsValidKey(k1), "chave !NILBAS de verdade passa");
    Ok(!InboxFile.IsValidKey("!NILBAS:abc\"\nos.execute('x')--"), "chave com aspas e código não passa");
    Ok(!InboxFile.IsValidKey("NILBAS:abc") && !InboxFile.IsValidKey(null) && !InboxFile.IsValidKey(""), "sem o prefixo, nula ou vazia não passa");
    Ok(!InboxFile.IsValidKey("!NILBAS:" + new string('A', 12001)), "chave grande demais não passa");
    var many = Enumerable.Range(0, 15).Select(i => Key("gid=" + i)).ToList();
    Ok(InboxFile.Sanitize(many).Count == 10, "no máximo 10 chaves");
    Ok(InboxFile.Sanitize([k1, "lixo", k1, k2]).SequenceEqual([k1, k2]), "tira lixo e repetidas, mantém a ordem");

    var lua = InboxFile.Render([k1, "\"); os.exit()", k2], 1795600000);
    Ok(lua.Contains("NilbasForeverInbox = {") && lua.Contains("at = 1795600000,"), "o arquivo define a caixa e a hora");
    Ok(!lua.Contains("os.exit") && lua.Contains(k1) && lua.Contains(k2), "só as chaves válidas vão para o arquivo");

    var addonDir = Path.Combine(tmp.FullName, "Interface", "AddOns", "NilbasForever");
    Directory.CreateDirectory(addonDir);
    var inbox = Path.Combine(addonDir, "Inbox.lua");
    File.WriteAllText(inbox, "NilbasForeverInbox = {}\n");
    Ok(InboxFile.ReadKeys(inbox).Count == 0, "o Inbox.lua do pacote não tem chaves");
    Ok(InboxFile.WriteIfChanged(inbox, [k1, k2], 100), "lista nova: escreve");
    Ok(InboxFile.ReadKeys(inbox).SequenceEqual([k1, k2]), "o que foi escrito se lê de volta");
    Ok(!File.Exists(inbox + ".tmp"), "não sobra arquivo temporário");
    Ok(!InboxFile.WriteIfChanged(inbox, [k1, k2], 200), "mesma lista: não reescreve");
    Ok(File.ReadAllText(inbox).Contains("at = 100,"), "a hora continua a da última mudança");
    Ok(InboxFile.WriteIfChanged(inbox, [k2], 300) && InboxFile.ReadKeys(inbox).SequenceEqual([k2]), "lista mudou: escreve de novo");
    Ok(InboxFile.WriteIfChanged(inbox, [], 400) && InboxFile.ReadKeys(inbox).Count == 0, "sem raids: deixa a caixa vazia");

    // ---------- API do bot ----------
    Ok(InboxApi.LooksLikeKey("nfapp_" + new string('a', 43)), "formato da chave do /vincular-app");
    Ok(!InboxApi.LooksLikeKey("nfapp_curta") && !InboxApi.LooksLikeKey(null), "chave curta ou nula não serve");
    var json = $"{{\"v\":1,\"addon\":\"2.0.10\",\"raids\":[{{\"gid\":7,\"inst\":\"Molten Core\",\"when\":\"sáb 21:00\",\"ts\":1795600000000,\"rev\":3,\"players\":12,\"token\":\"{k1}\"}}]}}";
    var parsed = InboxApi.Parse(json);
    Ok(parsed is { Addon: "2.0.10" } && parsed.Raids.Count == 1 && parsed.Raids[0].Players == 12, "lê a resposta do bot");
    Ok(InboxApi.Parse("{nada") is null && InboxApi.Parse("{\"v\":1}") is null, "resposta estragada não quebra");
    var withRoles = InboxApi.Parse("{\"v\":1,\"raids\":[{\"gid\":1,\"inst\":\"BWL\",\"when\":\"dom 20:00\",\"rev\":1,\"players\":8,\"t\":1,\"h\":2,\"d\":5,\"token\":\"x\"}]}")!.Raids[0];
    Ok(withRoles.Details() == "dom 20:00 · 8 inscritos · 1 tank · 2 heals · 5 dps", "detalhes com as funções: " + withRoles.Details());
    Ok(withRoles.Short() == "dom 20:00 · 8 inscritos", "versão curta para a lista");
    Ok(parsed!.Raids[0].Details() == "sáb 21:00 · 12 inscritos", "bot antigo (sem funções): só horário e inscritos");

    var key = "nfapp_" + new string('b', 43);
    string? seenAuth = null;
    HttpClient Fake(HttpStatusCode code, string body = "", bool throws = false) => new(new FakeHandler(req =>
    {
        seenAuth = req.Headers.Authorization?.ToString();
        if (throws) throw new HttpRequestException("sem rede");
        return new HttpResponseMessage(code) { Content = new StringContent(body) };
    }));
    var ok = await InboxApi.FetchAsync(Fake(HttpStatusCode.OK, json), "https://exemplo.test/", key);
    Ok(ok.Status == InboxStatus.Ok && ok.Payload!.Raids[0].Inst == "Molten Core", "200: raids");
    Ok(seenAuth == "Bearer " + key, "manda a chave no Authorization");
    Ok((await InboxApi.FetchAsync(Fake(HttpStatusCode.Unauthorized), "https://x.test", key)).Status == InboxStatus.Unauthorized, "401: chave recusada");
    Ok((await InboxApi.FetchAsync(Fake((HttpStatusCode)429), "https://x.test", key)).Status == InboxStatus.RateLimited, "429: esperar");
    Ok((await InboxApi.FetchAsync(Fake(HttpStatusCode.InternalServerError), "https://x.test", key)).Status == InboxStatus.ServerError, "500: erro do bot");
    Ok((await InboxApi.FetchAsync(Fake(HttpStatusCode.OK, throws: true), "https://x.test", key)).Status == InboxStatus.Offline, "sem rede: offline");

    // ---------- uma rodada completa ----------
    var settings = new AppSettings { AddonsDir = Path.Combine(tmp.FullName, "Interface", "AddOns"), AddonFolder = "NilbasForever", ApiBase = "https://x.test" };
    File.WriteAllText(inbox, "NilbasForeverInbox = {}\n");
    var run = await InboxSync.RunAsync(Fake(HttpStatusCode.OK, json), settings, key, 500);
    Ok(run.Status == InboxStatus.Ok && run.Wrote && run.RaidCount == 1, "rodada: escreve a raid no addon");
    Ok(run.Message.Contains("Molten Core") && run.Message.Contains("12 inscritos"), "rodada: mensagem com a raid");
    var again = await InboxSync.RunAsync(Fake(HttpStatusCode.OK, json), settings, key, 600);
    Ok(again.Status == InboxStatus.Ok && !again.Wrote, "rodada repetida: não reescreve");
    var bad = await InboxSync.RunAsync(Fake(HttpStatusCode.Unauthorized), settings, key, 700);
    Ok(bad.Status == InboxStatus.Unauthorized && bad.Message.Contains("/vincular-app"), "rodada com chave recusada avisa do /vincular-app");
    Ok(InboxFile.ReadKeys(inbox).SequenceEqual([k1]), "chave recusada não apaga a raid que já estava");
    var missing = await InboxSync.RunAsync(Fake(HttpStatusCode.OK, json), new AppSettings { AddonsDir = settings.AddonsDir, AddonFolder = "NilbasForeverTeste" }, key, 800);
    Ok(missing.Status != InboxStatus.Ok && missing.Message.Contains("INSANOS (teste)"), "pasta do addon que não existe: avisa");

    // ---------- quem entrou e quem saiu ----------
    string Enc(string s) => Uri.EscapeDataString(s);
    InboxRaid Raid(int gid, string tanks, string heals, string dps, long? ts = null) =>
        new(gid, "Molten Core", "sáb 21:00", ts, 1, 0, Key($"inst={Enc("Molten Core")}&gid={gid}&tanks={Enc(tanks)}&heals={Enc(heals)}&dps={Enc(dps)}"));
    Ok(RaidRoster.Decode(k1)["inst"] == "Molten Core", "lê o nome da raid de dentro da chave");
    Ok(RaidRoster.Members(Key("tanks=Ana,Bia&heals=Caio&dps=")).SequenceEqual(["Ana", "Bia", "Caio"]), "lê os inscritos das três funções");
    Ok(RaidRoster.Members("!NILBAS:@@@").Count == 0 && RaidRoster.Members(null).Count == 0, "chave estragada: ninguém, sem quebrar");

    var antes = new[] { Raid(7, "Ana", "Bia", "Caio,Duda") };
    var depois = new[] { Raid(7, "Ana", "Bia,Edu", "Caio") };
    var diff = RaidRoster.Diff(antes, depois);
    Ok(diff.Count == 1 && diff[0].Joined.SequenceEqual(["Edu"]) && diff[0].Left.SequenceEqual(["Duda"]), "Edu entrou e Duda saiu");
    Ok(diff[0].Describe() == "Molten Core: Edu entrou · Duda saiu", "texto do aviso: " + diff[0].Describe());
    Ok(RaidRoster.Diff(antes, antes).Count == 0, "nada mudou: sem aviso");
    Ok(RaidRoster.Diff(antes, [Raid(8, "Zé", "", "")]).Count == 0, "raid nova não conta como entrou/saiu");
    var muitos = RaidRoster.Diff([Raid(7, "", "", "")], [Raid(7, "A,B,C,D", "", "")]);
    Ok(muitos[0].Describe() == "Molten Core: A, B e mais 2 entraram", "muita gente: resume");

    var agora = DateTimeOffset.FromUnixTimeSeconds(1795600000);
    var cinco = TimeSpan.FromSeconds(30);
    Ok(RaidRoster.PollInterval([Raid(7, "", "", "", agora.AddHours(2).ToUnixTimeMilliseconds())], agora, cinco) == TimeSpan.FromSeconds(15), "raid daqui a 2 h: verifica a cada 15 s");
    Ok(RaidRoster.PollInterval([Raid(7, "", "", "", agora.AddMinutes(-30).ToUnixTimeMilliseconds())], agora, cinco) == TimeSpan.FromSeconds(15), "raid começou há 30 min: continua 15 s");
    Ok(RaidRoster.PollInterval([Raid(7, "", "", "", agora.AddDays(1).ToUnixTimeMilliseconds())], agora, cinco) == cinco, "raid amanhã: intervalo normal");
    Ok(RaidRoster.PollInterval([Raid(7, "", "", "")], agora, cinco) == cinco, "raid sem horário: intervalo normal");

    // ---------- o bot decide o ritmo ----------
    HttpClient Respond(Func<HttpResponseMessage> make) => new(new FakeHandler(_ => make()));
    var comCabecalho = await InboxApi.FetchAsync(Respond(() =>
    {
        var m = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        m.Headers.Add("X-Insanos-Poll", "15");
        return m;
    }), "https://x.test", key);
    Ok(comCabecalho.PollSeconds == 15, "lê o ritmo do cabeçalho X-Insanos-Poll");
    var soNoCorpo = await InboxApi.FetchAsync(Fake(HttpStatusCode.OK, json.Replace("\"v\":1,", "\"v\":1,\"poll\":20,")), "https://x.test", key);
    Ok(soNoCorpo.PollSeconds == 20, "sem cabeçalho, lê o ritmo do campo poll");
    Ok(ok.PollSeconds is null, "bot antigo (sem ritmo): fica sem valor");
    var cedo = await InboxApi.FetchAsync(Respond(() =>
    {
        var m = new HttpResponseMessage((HttpStatusCode)429);
        m.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
        return m;
    }), "https://x.test", key);
    Ok(cedo.Status == InboxStatus.RateLimited && cedo.RetryAfterSeconds == 42, "429: lê quanto esperar (Retry-After)");

    var ts = DateTimeOffset.FromUnixTimeSeconds(1795600000);
    var trinta = TimeSpan.FromSeconds(30);
    SyncOutcome Out(InboxStatus st, int? poll = null, int? retry = null, IReadOnlyList<InboxRaid>? raids = null) =>
        new(st, false, null, 0, "") { PollSeconds = poll, RetryAfterSeconds = retry, Raids = raids ?? [] };
    Ok(RaidRoster.NextCheck(Out(InboxStatus.Ok, poll: 15), ts, trinta, 0) == TimeSpan.FromSeconds(15), "segue o ritmo do bot (15 s)");
    Ok(RaidRoster.NextCheck(Out(InboxStatus.Ok, poll: 2), ts, trinta, 0) == TimeSpan.FromSeconds(10), "ritmo baixo demais: no mínimo 10 s");
    Ok(RaidRoster.NextCheck(Out(InboxStatus.Ok, poll: 99999), ts, trinta, 0) == TimeSpan.FromMinutes(10), "ritmo alto demais: no máximo 10 min");
    Ok(RaidRoster.NextCheck(Out(InboxStatus.Ok, raids: [Raid(7, "", "", "", ts.AddHours(1).ToUnixTimeMilliseconds())]), ts, trinta, 0) == TimeSpan.FromSeconds(15), "bot antigo: o app calcula sozinho");
    Ok(RaidRoster.NextCheck(Out(InboxStatus.RateLimited, retry: 42), ts, trinta, 1) == TimeSpan.FromSeconds(42), "cedo demais: espera o que o bot pediu");
    Ok(RaidRoster.NextCheck(Out(InboxStatus.Offline), ts, trinta, 1) == TimeSpan.FromSeconds(30), "sem conexão: 30 s na primeira");
    Ok(RaidRoster.NextCheck(Out(InboxStatus.Offline), ts, trinta, 2) == TimeSpan.FromSeconds(60), "sem conexão de novo: 1 min");
    Ok(RaidRoster.NextCheck(Out(InboxStatus.Offline), ts, trinta, 9) == TimeSpan.FromMinutes(5), "sem conexão há muito tempo: no máximo 5 min");
    var rodada = await InboxSync.RunAsync(Respond(() =>
    {
        var m = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        m.Headers.Add("X-Insanos-Poll", "15");
        return m;
    }), settings, key, 900);
    Ok(rodada.PollSeconds == 15, "a rodada repassa o ritmo do bot para o ícone");

    // ---------- pasta do WoW ----------
    var forever = Path.Combine(tmp.FullName, "World of Warcraft", "_forever_");
    var addons = Path.Combine(forever, "Interface", "AddOns");
    Directory.CreateDirectory(Path.Combine(addons, "NilbasForeverTeste"));
    File.WriteAllText(Path.Combine(addons, "NilbasForeverTeste", "NilbasForeverTeste.toc"), "## Title: teste");
    Ok(WowLocator.NormalizeAddonsDir(forever) == addons, "aceita a pasta _forever_");
    Ok(WowLocator.NormalizeAddonsDir(Path.Combine(forever, "Interface")) == addons, "aceita a Interface");
    Ok(WowLocator.NormalizeAddonsDir(addons) == addons, "aceita a AddOns");
    Ok(WowLocator.NormalizeAddonsDir(tmp.FullName + "\\nada") is null, "pasta sem o addon: não serve");
    Ok(WowLocator.InstalledAddons(addons).SequenceEqual(["NilbasForeverTeste"]), "acha a cópia de teste");

    // ---------- configurações ----------
    var cfgDir = Path.Combine(tmp.FullName, "cfg");
    new AppSettings { AddonsDir = addons, AddonFolder = "NilbasForeverTeste", ProtectedKey = "abc", PollSeconds = 45, ShowWelcome = false }.Save(cfgDir);
    var back = AppSettings.Load(cfgDir);
    Ok(back.AddonsDir == addons && back.AddonFolder == "NilbasForeverTeste" && back.PollSeconds == 45 && !back.ShowWelcome && back.IsComplete, "configurações salvam e voltam");
    File.WriteAllText(Path.Combine(cfgDir, "config.json"), "{quebrado");
    Ok(!AppSettings.Load(cfgDir).IsComplete, "config.json estragado volta ao começo sem quebrar");
    Ok(new AppSettings().ShowWelcome, "as boas-vindas vêm ligadas na primeira vez");

    // ---------- o config.json não manda no endereço nem na pasta (qualquer programa do usuário mexe nele) ----------
    void Cfg(object o) => File.WriteAllText(Path.Combine(cfgDir, "config.json"), System.Text.Json.JsonSerializer.Serialize(o));
    Cfg(new { AddonsDir = addons, AddonFolder = @"..\..", ApiBase = "http://mal.test", PollSeconds = 1, ProtectedKey = "abc" });
    var odd = AppSettings.Load(cfgDir);
    Ok(odd.AddonsDir == addons && odd.IsComplete, "o arquivo foi lido (o resto continua valendo)");
    Ok(odd.ApiBase == InboxApi.DefaultBase, "endereço do bot vindo do arquivo é ignorado");
    Ok(odd.AddonFolder == "NilbasForever", "nome de addon fora da lista volta para NilbasForever");
    Ok(odd.PollSeconds == 15, "intervalo absurdo vira o mínimo");
    odd.Save(cfgDir);
    Ok(!File.ReadAllText(Path.Combine(cfgDir, "config.json")).Contains("ApiBase"), "o endereço nem é salvo");
    foreach (var badDir in new[] { @"C:\Windows", @"\\servidor\Interface\AddOns", @"C:\Jogo\Interface\AddOns\..\..\Windows", @"Interface\AddOns", @"C:\Jogo\AddOns", "" })
        Ok(!WowLocator.LooksLikeAddonsDir(badDir), "pasta recusada: " + badDir);
    Ok(WowLocator.LooksLikeAddonsDir(addons), "pasta AddOns de verdade aceita");
    Cfg(new { AddonsDir = @"C:\Windows", ProtectedKey = "abc" });
    Ok(!AppSettings.Load(cfgDir).IsComplete, "pasta que não é AddOns: pede para configurar de novo");
    var weird = new AppSettings { AddonsDir = addons, AddonFolder = "../../x" };
    var w = await InboxSync.RunAsync(Fake(HttpStatusCode.OK, json), weird, key, 900);
    Ok(w.Status != InboxStatus.Ok && !w.Wrote, "rodada com addon fora da lista não escreve nada");

    // ---------- resposta grande demais ou redirecionada ----------
    var huge = new string('a', 300_000);
    Ok((await InboxApi.FetchAsync(InboxApi.CreateClient("t", new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(huge) })), "https://x.test", key)).Status != InboxStatus.Ok,
        "resposta maior que o limite é recusada sem travar");
    var moved = new HttpResponseMessage(HttpStatusCode.Redirect);
    moved.Headers.Location = new Uri("https://outro.test/v1/inbox");
    Ok((await InboxApi.FetchAsync(InboxApi.CreateClient("t", new FakeHandler(_ => moved)), "https://x.test", key)).Status == InboxStatus.ServerError,
        "redirecionamento não é seguido");
}
finally
{
    try { tmp.Delete(recursive: true); } catch (IOException) { }
}

Console.WriteLine($"\n{pass} passaram, {fail} falharam");
return fail == 0 ? 0 : 1;

sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        Task.FromResult(respond(request));
}
