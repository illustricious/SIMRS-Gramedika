using System.Security.Cryptography;

namespace SimrsKhanza.Core.Auth;

/// <summary>
/// PBKDF2-based password hashing (built into .NET, no extra package).
/// Replaces legacy plaintext storage found in tables `admin` (usere/passworde)
/// and `user` (password text NOT NULL) — confirmed plaintext-shaped columns.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 100_000;

    public static (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string storedHash, string storedSalt)
    {
        var salt = Convert.FromBase64String(storedSalt);
        var expected = Convert.FromBase64String(storedHash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
