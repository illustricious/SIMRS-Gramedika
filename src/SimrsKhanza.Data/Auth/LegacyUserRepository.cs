using MySqlConnector;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.Data.Auth;

/// <summary>
/// Answers the question "can the legacy `user`/`petugas` tables log people into
/// the rewrite?" — yes, this is that integration. It reads straight from the
/// legacy `sik` database, so no data migration is required to start using it.
///
/// Two things make this NOT a drop-in replacement for <see cref="InMemoryUserRepository"/>:
///
/// 1. Legacy `user.password` is plaintext (confirmed in docs/audit/DB_SCHEMA.md —
///    `admin.passworde` and `user.password` are both `text`, no hash/salt
///    columns anywhere). <see cref="UserAccount.IsLegacyPlaintext"/> tells
///    AuthService to compare the raw string instead of running
///    PasswordHasher.Verify (which expects a PBKDF2 hash+salt this table
///    doesn't have). The recommended next step — NOT yet implemented here —
///    is "rehash on first successful login": once AuthService confirms a
///    plaintext match, hash the password with PasswordHasher and store it in
///    a NEW table the rewrite owns (never write back into the legacy `user`
///    table), then prefer that hash on subsequent logins. See
///    docs/audit/HANDOVER.md for the full writeup.
///
/// 2. Legacy `user` has ~1,225 columns, one `enum('true','false')` per
///    feature (confirmed by counting CREATE TABLE `user` in schema_sik.sql),
///    instead of a normalized permissions table. Modeling that as an EF Core
///    entity isn't practical, so this reads it with plain ADO.NET
///    (MySqlConnector) and turns every column that is 'true' into a
///    permission key equal to its column name — e.g. `registrasi` becomes
///    the permission key "registrasi". A real migration should flatten this
///    into Core.Auth.UserPermission rows once, rather than re-scanning 1,225
///    columns on every login — this method is a correct but unoptimized
///    starting point, not the final design.
/// </summary>
public class LegacyUserRepository(DatabaseSettings settings) : IUserRepository
{
    public async Task<UserAccount?> FindByIdAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(settings.ToConnectionString());
        await conn.OpenAsync(ct);

        // id_user is assumed to match petugas.nip (the common Khanza pattern per
        // the top-level-module audit) — NOT verified against a real `sik`
        // database in this session; confirm against your actual install before
        // relying on the display-name join.
        const string sql = """
            SELECT u.id_user, u.password, p.nama AS petugas_nama
            FROM `user` u
            LEFT JOIN `petugas` p ON p.nip = u.id_user
            WHERE u.id_user = @id
            """;

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct)) return null;

        var password = reader.GetString(reader.GetOrdinal("password"));
        var namaOrdinal = reader.GetOrdinal("petugas_nama");
        var displayName = reader.IsDBNull(namaOrdinal) ? userId : reader.GetString(namaOrdinal);

        return new UserAccount
        {
            UserId = userId,
            DisplayName = displayName,
            PasswordHash = password, // raw legacy value — see IsLegacyPlaintext
            PasswordSalt = string.Empty,
            IsLegacyPlaintext = true,
            IsActive = true,
        };
    }

    public async Task<IReadOnlySet<string>> GetPermissionKeysAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(settings.ToConnectionString());
        await conn.OpenAsync(ct);

        await using var cmd = new MySqlCommand("SELECT * FROM `user` WHERE id_user = @id", conn);
        cmd.Parameters.AddWithValue("@id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var permissions = new HashSet<string>();
        if (!await reader.ReadAsync(ct)) return permissions;

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            if (columnName is "id_user" or "password") continue;

            if (!reader.IsDBNull(i) && string.Equals(reader.GetString(i), "true", StringComparison.OrdinalIgnoreCase))
            {
                permissions.Add(columnName);
            }
        }

        return permissions;
    }
}
