// Configurações do app em %APPDATA%\InsanosApp\config.json. A chave do /vincular-app fica protegida pelo Windows
// (KeyVault, DPAPI da conta do usuário): aqui só passa o texto já protegido.
//
// O arquivo fica na pasta do usuário e qualquer programa dele pode mexer, então o app não confia no que lê (26 nov
// 2026): o endereço do bot é fixo no código (não vem do arquivo, para ninguém desviar a chave para outro site), o
// addon só pode ser NilbasForever ou NilbasForeverTeste (um nome como "..\.." faria o app escrever fora da pasta
// do addon), e a pasta tem que ser uma Interface\AddOns de verdade.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InsanosApp.Core;

public sealed class AppSettings
{
    public string? AddonsDir { get; set; }
    public string AddonFolder { get; set; } = "NilbasForever";
    public string? ProtectedKey { get; set; }
    /// <summary>Sempre o bot da guilda; não sai nem entra no config.json (os testes trocam pelo código).</summary>
    [JsonIgnore]
    public string ApiBase { get; init; } = InboxApi.DefaultBase;
    /// <summary>Intervalo normal entre as verificações, em segundos (perto da raid cai para 15 s).</summary>
    public int PollSeconds { get; set; } = 30;
    /// <summary>Mostrar a tela de boas-vindas quando a pessoa abre o app pelo .exe.</summary>
    public bool ShowWelcome { get; set; } = true;
    public long LastChangeAt { get; set; }
    public string? LastRaidLabel { get; set; }

    public static string DefaultDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InsanosApp");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static AppSettings Load(string? dir = null)
    {
        var file = Path.Combine(dir ?? DefaultDir, "config.json");
        try
        {
            if (File.Exists(file)) return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file)) ?? new()).Sanitized();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
        return new();
    }

    public void Save(string? dir = null)
    {
        var d = dir ?? DefaultDir;
        Directory.CreateDirectory(d);
        var file = Path.Combine(d, "config.json");
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, file, overwrite: true);
    }

    /// <summary>Desfaz o que não pode vir do arquivo: addon fora da lista e pasta que não é uma AddOns.</summary>
    public AppSettings Sanitized()
    {
        if (!WowLocator.IsKnownAddon(AddonFolder)) AddonFolder = WowLocator.AddonFolders[0];
        if (!WowLocator.LooksLikeAddonsDir(AddonsDir)) AddonsDir = null;
        PollSeconds = Math.Clamp(PollSeconds, 15, 600);
        return this;
    }

    public bool IsComplete => !string.IsNullOrEmpty(AddonsDir) && !string.IsNullOrEmpty(ProtectedKey);
}
