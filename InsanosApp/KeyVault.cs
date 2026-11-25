// A chave do /vincular-app protegida pelo Windows (DPAPI, só a conta do usuário consegue abrir). Nunca fica em
// texto puro no config.json.
using System.Security.Cryptography;
using System.Text;

namespace InsanosApp;

internal static class KeyVault
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("InsanosApp/vincular-app/v1");

    public static string Protect(string key) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), Entropy, DataProtectionScope.CurrentUser));

    public static string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return null;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception e) when (e is CryptographicException or FormatException) { return null; }
    }
}
