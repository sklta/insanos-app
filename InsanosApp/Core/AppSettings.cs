// Configurações do app em %APPDATA%\InsanosApp\config.json. A chave do /vincular-app fica protegida pelo Windows
// (KeyVault, DPAPI da conta do usuário): aqui só passa o texto já protegido.
using System.Text.Json;

namespace InsanosApp.Core;

public sealed class AppSettings
{
    public string? AddonsDir { get; set; }
    public string AddonFolder { get; set; } = "NilbasForever";
    public string? ProtectedKey { get; set; }
    public string ApiBase { get; set; } = InboxApi.DefaultBase;
    /// <summary>Intervalo normal entre as verificações, em segundos (perto da raid cai para 15 s).</summary>
    public int PollSeconds { get; set; } = 30;
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
            if (File.Exists(file)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file)) ?? new();
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

    public bool IsComplete => !string.IsNullOrEmpty(AddonsDir) && !string.IsNullOrEmpty(ProtectedKey);
}
