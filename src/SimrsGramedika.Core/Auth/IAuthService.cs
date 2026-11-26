namespace SimrsGramedika.Core.Auth;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string userId, string password, CancellationToken ct = default);
}
