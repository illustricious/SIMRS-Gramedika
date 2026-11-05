using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Auth;

namespace SimrsKhanza.Tests;

public class AuthServiceTests
{
    private static AuthService CreateSut() => new(new InMemoryUserRepository());

    [Fact]
    public async Task Login_WithCorrectDevSeedCredentials_Succeeds()
    {
        var sut = CreateSut();

        var result = await sut.LoginAsync("admin", "admin123");

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("admin", result.User!.UserId);
        Assert.True(result.User.HasPermission("rekammedis.view"));
    }

    [Fact]
    public async Task Login_WithWrongPassword_FailsWithWrongPasswordReason()
    {
        var sut = CreateSut();

        var result = await sut.LoginAsync("admin", "wrong-password");

        Assert.False(result.Success);
        Assert.Equal(LoginFailureReason.WrongPassword, result.FailureReason);
    }

    [Fact]
    public async Task Login_WithUnknownUserId_FailsWithUserNotFoundReason()
    {
        var sut = CreateSut();

        var result = await sut.LoginAsync("no-such-user", "whatever");

        Assert.False(result.Success);
        Assert.Equal(LoginFailureReason.UserNotFound, result.FailureReason);
    }

    [Fact]
    public void PasswordHasher_VerifiesItsOwnHash()
    {
        var (hash, salt) = PasswordHasher.Hash("secret123");

        Assert.True(PasswordHasher.Verify("secret123", hash, salt));
        Assert.False(PasswordHasher.Verify("not-secret123", hash, salt));
    }

    /// <summary>
    /// Covers the branch LegacyUserRepository relies on: an account flagged
    /// IsLegacyPlaintext is compared by raw string equality, not PasswordHasher.
    /// </summary>
    private sealed class FakeLegacyUserRepository : IUserRepository
    {
        public Task<UserAccount?> FindByIdAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<UserAccount?>(userId == "nip001"
                ? new UserAccount { UserId = "nip001", DisplayName = "Petugas Lama", PasswordHash = "rahasia123", IsLegacyPlaintext = true }
                : null);

        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "registrasi" });
    }

    [Fact]
    public async Task Login_WithLegacyPlaintextAccount_ComparesRawStringNotHash()
    {
        var sut = new AuthService(new FakeLegacyUserRepository());

        var ok = await sut.LoginAsync("nip001", "rahasia123");
        var wrong = await sut.LoginAsync("nip001", "salah");

        Assert.True(ok.Success);
        Assert.True(ok.User!.HasPermission("registrasi"));
        Assert.False(wrong.Success);
        Assert.Equal(LoginFailureReason.WrongPassword, wrong.FailureReason);
    }
}
