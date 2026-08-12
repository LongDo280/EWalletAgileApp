using System.Security.Cryptography;
using System.Text;

namespace EWalletAgileApp.Helpers;

// US002 - Đăng nhập an toàn: mật khẩu không được lưu dạng plain-text.
// Băm bằng SHA-256 kèm salt ngẫu nhiên cho từng người dùng.
public static class PasswordHelper
{
    public static string GenerateSalt()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string HashPassword(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes(salt + password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool Verify(string password, string salt, string expectedHash)
    {
        var actualHash = HashPassword(password, salt);
        return actualHash == expectedHash;
    }
}