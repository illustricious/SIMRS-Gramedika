namespace SimrsKhanza.Core.Auth;

public class AuthService(IUserRepository users) : IAuthService
{
    public async Task<LoginResult> LoginAsync(string userId, string password, CancellationToken ct = default)
    {
        var account = await users.FindByIdAsync(userId, ct);
        if (account is null)
            return LoginResult.Fail(LoginFailureReason.UserNotFound);

        if (!account.IsActive)
            return LoginResult.Fail(LoginFailureReason.AccountInactive);

        // Legacy `user`/`admin` rows store a plaintext password (no hash/salt
        // columns exist) — compared directly. Everything else goes through
        // PBKDF2. See UserAccount.IsLegacyPlaintext and LegacyUserRepository.
        var passwordOk = account.IsLegacyPlaintext
            ? string.Equals(password, account.PasswordHash, StringComparison.Ordinal)
            : PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt);

        if (!passwordOk)
            return LoginResult.Fail(LoginFailureReason.WrongPassword);

        var permissions = await users.GetPermissionKeysAsync(userId, ct);
        return LoginResult.Ok(new AuthenticatedUser
        {
            UserId = account.UserId,
            DisplayName = account.DisplayName,
            Permissions = permissions
        });
    }
}
