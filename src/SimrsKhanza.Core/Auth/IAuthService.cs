namespace SimrsKhanza.Core.Auth;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string userId, string password, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<UserAccount?> FindByIdAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetPermissionKeysAsync(string userId, CancellationToken ct = default);
}
