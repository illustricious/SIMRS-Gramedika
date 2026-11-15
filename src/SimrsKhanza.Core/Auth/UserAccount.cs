namespace SimrsKhanza.Core.Auth;

/// <summary>
/// A logged-in account from the legacy `admin` / `user` tables. Permissions are the
/// `user` columns whose value is 'true' (see LegacyKhanzaAuthService).
/// </summary>
public class AuthenticatedUser
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();

    /// <summary>True when logged in from the legacy `admin` table ("Admin Utama") — has every hak akses.</summary>
    public bool IsAdminUtama { get; set; }

    public bool HasPermission(string key) => IsAdminUtama || Permissions.Contains(key);

    /// <summary>Legacy akses.getkode(): "Admin Utama" for admins, otherwise the user id (NIK).</summary>
    public string KodeLegacy => IsAdminUtama ? "Admin Utama" : UserId;
}

/// <summary>The account logged into this window session (set by MainWindow).</summary>
public static class AppSession
{
    public static AuthenticatedUser? CurrentUser { get; set; }
    public static string Kode => CurrentUser?.KodeLegacy ?? "-";
}

public enum LoginFailureReason
{
    None,
    UserNotFound,
    WrongPassword,
    AccountInactive,
    DatabaseUnavailable
}

public class LoginResult
{
    public bool Success { get; init; }
    public AuthenticatedUser? User { get; init; }
    public LoginFailureReason FailureReason { get; init; } = LoginFailureReason.None;

    public static LoginResult Ok(AuthenticatedUser user) => new() { Success = true, User = user };
    public static LoginResult Fail(LoginFailureReason reason) => new() { Success = false, FailureReason = reason };
}
