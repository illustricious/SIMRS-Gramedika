namespace SimrsKhanza.Core.Auth;

/// <summary>
/// Replaces legacy tables `admin` (plaintext usere/passworde) and `user`
/// (one boolean column PER feature — ~1225 columns, confirmed in schema_sik.sql).
/// Here, permissions are a normalized set (see Permission/UserPermission) instead
/// of a wide flat table, so adding a feature never requires a schema change.
/// </summary>
public class UserAccount
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True for accounts sourced from the legacy `user`/`admin` tables, where
    /// <see cref="PasswordHash"/> holds the raw plaintext password instead of a
    /// PBKDF2 hash — AuthService compares it directly rather than calling
    /// PasswordHasher.Verify. See LegacyUserRepository for the full rationale
    /// and the recommended rehash-on-login migration path.
    /// </summary>
    public bool IsLegacyPlaintext { get; set; }

    /// <summary>Links back to Pegawai.Nik when this account belongs to an employee.</summary>
    public string? PegawaiNik { get; set; }
}

/// <summary>One entry in the permission catalog, e.g. "rekammedis.pasien.edit".</summary>
public class Permission
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
}

/// <summary>Grants a single permission to a user — replaces one boolean column of legacy `user`.</summary>
public class UserPermission
{
    public string UserId { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public bool Allowed { get; set; }
}

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
