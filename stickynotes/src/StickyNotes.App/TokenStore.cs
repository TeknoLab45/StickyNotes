using System;
using System.Security.Cryptography;
using System.Text;

namespace StickyNotes.App;

/// <summary>
/// Memos token'ı DPAPI (CurrentUser) ile şifrelenir; yalnızca şifreli metin settings tablosuna yazılır.
/// Düz token ne diske ne log'a yazılır.
/// </summary>
public static class TokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("StickyNotes.Memos.v1");

    public static string Protect(string token)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Şifreli metni çözer. Boş, bozuk ya da başka kullanıcıya ait ise null.</summary>
    public static string? Unprotect(string enc)
    {
        if (string.IsNullOrEmpty(enc)) return null;
        try
        {
            var plain = ProtectedData.Unprotect(Convert.FromBase64String(enc), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
