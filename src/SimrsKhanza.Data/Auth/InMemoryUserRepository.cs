using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.Data.Auth;

/// <summary>
/// Dummy in-memory user store for development only — NOT connected to the
/// legacy `sik` database or any real credentials. Seeds one dev account
/// ("admin" / "admin123") so the login screen is testable before
/// SimrsKhanza.Data gets a real MySQL/EF Core backing store.
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<string, UserAccount> _accounts = new();
    private readonly Dictionary<string, HashSet<string>> _permissions = new();

    public InMemoryUserRepository()
    {
        var (hash, salt) = PasswordHasher.Hash("admin123");
        _accounts["admin"] = new UserAccount
        {
            UserId = "admin",
            DisplayName = "Administrator (dev seed)",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true
        };
        _permissions["admin"] = new HashSet<string>
        {
            "rekammedis.view", "keuangan.view", "kepegawaian.view",
            "inventaris.view", "laporan.view", "bridging.view",
            "pengaturan.admin", // only this account has it — demonstrates the admin-only gate
        };

        // Second dev account WITHOUT pengaturan.admin, so the admin-only gate
        // on the Pengaturan tile/screen is actually exercisable during dev
        // (login as "petugas" and confirm the tile + screen are both hidden).
        var (hash2, salt2) = PasswordHasher.Hash("petugas123");
        _accounts["petugas"] = new UserAccount
        {
            UserId = "petugas",
            DisplayName = "Petugas (dev seed, bukan admin)",
            PasswordHash = hash2,
            PasswordSalt = salt2,
            IsActive = true
        };
        _permissions["petugas"] = new HashSet<string>
        {
            "rekammedis.view", "laporan.view",
        };
    }

    public Task<UserAccount?> FindByIdAsync(string userId, CancellationToken ct = default)
        => Task.FromResult(_accounts.GetValueOrDefault(userId));

    public Task<IReadOnlySet<string>> GetPermissionKeysAsync(string userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<string>>(_permissions.GetValueOrDefault(userId) ?? new HashSet<string>());
}
