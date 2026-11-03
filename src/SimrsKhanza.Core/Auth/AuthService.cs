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

        if (!PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt))
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
