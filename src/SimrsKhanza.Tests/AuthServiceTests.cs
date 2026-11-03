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
}
