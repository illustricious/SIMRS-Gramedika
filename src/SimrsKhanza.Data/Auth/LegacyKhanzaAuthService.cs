using MySqlConnector;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.Data.Auth;

/// <summary>
/// Logs in with the SAME accounts as the original SIMRS Khanza (Java), straight
/// from the legacy `sik` database — a port of fungsi/akses.java setData():
///
///   select * from admin where usere=AES_ENCRYPT(?,'nur') and passworde=AES_ENCRYPT(?,'windi')
///   select * from user  where id_user=AES_ENCRYPT(?,'nur') and password=AES_ENCRYPT(?,'windi')
///
/// - A match in `admin` = "Admin Utama": every hak akses is granted (legacy sets
///   every akses.xxx=true), plus the admin-only menus (Setup Aplikasi, Admin, User…).
/// - Otherwise a match in `user` = pegawai login: each `user` column whose value
///   is 'true' becomes a permission key equal to the column name (e.g. "registrasi"),
///   which LegacyTilePermissions uses to filter the Menu Utama like frmUtama does.
///
/// Read-only: never writes to `admin`/`user`.
/// </summary>
public class LegacyKhanzaAuthService(DatabaseSettings settings) : IAuthService
{
    public async Task<LoginResult> LoginAsync(string userId, string password, CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(settings.ToConnectionString());
        await conn.OpenAsync(ct);

        if (await ExistsAsync(conn,
                "SELECT COUNT(*) FROM admin WHERE usere=AES_ENCRYPT(@u,'nur') AND passworde=AES_ENCRYPT(@p,'windi')",
                userId, password, ct))
        {
            var all = await GetAllUserColumnsAsync(conn, ct);
            all.Add(LegacyTilePermissions.AdminUtamaKey);
            all.Add("pengaturan.admin");
            foreach (var item in new DefaultMenuCatalog().AllItems) all.Add(item.RequiredPermission);
            await TrackAsync(conn, "Admin Utama", ct); // legacy frmUtama logs admin as 'Admin Utama'

            return LoginResult.Ok(new AuthenticatedUser
            {
                UserId = userId,
                DisplayName = "Admin Utama",
                IsAdminUtama = true,
                Permissions = all,
            });
        }

        var permissions = await GetUserPermissionsAsync(conn, userId, password, ct);
        if (permissions is null)
        {
            // Legacy shows one message for both cases; distinguish only when cheap & safe.
            var known = await ExistsAsync(conn,
                "SELECT (SELECT COUNT(*) FROM admin WHERE usere=AES_ENCRYPT(@u,'nur')) + (SELECT COUNT(*) FROM `user` WHERE id_user=AES_ENCRYPT(@u,'nur'))",
                userId, null, ct);
            return LoginResult.Fail(known ? LoginFailureReason.WrongPassword : LoginFailureReason.UserNotFound);
        }

        await TrackAsync(conn, userId, ct);
        var nama = await GetNamaAsync(conn, userId, ct);
        return LoginResult.Ok(new AuthenticatedUser
        {
            UserId = userId,
            DisplayName = string.IsNullOrWhiteSpace(nama) ? userId : $"{userId} - {nama}",
            IsAdminUtama = false,
            Permissions = permissions,
        });
    }

    /// <summary>Writes the `tracker` row (Tracker Login menu). Best effort — never blocks a login.</summary>
    private static async Task TrackAsync(MySqlConnection conn, string nip, CancellationToken ct)
    {
        try
        {
            await using var cmd = new MySqlCommand(
                "INSERT IGNORE INTO tracker (nip, tgl_login, jam_login) VALUES (@n, CURRENT_DATE(), CURRENT_TIME())", conn);
            cmd.Parameters.AddWithValue("@n", nip.Length > 20 ? nip[..20] : nip);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (MySqlException)
        {
            // tracker table missing/locked — ignore
        }
    }

    private static async Task<bool> ExistsAsync(MySqlConnection conn, string sql, string user, string? pass, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@u", user);
        if (pass is not null) cmd.Parameters.AddWithValue("@p", pass);
        var n = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct) ?? 0L);
        return n > 0;
    }

    private static async Task<HashSet<string>?> GetUserPermissionsAsync(
        MySqlConnection conn, string user, string pass, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand(
            "SELECT * FROM `user` WHERE id_user=AES_ENCRYPT(@u,'nur') AND password=AES_ENCRYPT(@p,'windi') LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@p", pass);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var col = reader.GetName(i);
            if (col is "id_user" or "password" || reader.IsDBNull(i)) continue;
            if (reader.GetValue(i) is string s && s.Equals("true", StringComparison.OrdinalIgnoreCase))
                permissions.Add(col);
        }
        return permissions;
    }

    private static async Task<HashSet<string>> GetAllUserColumnsAsync(MySqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand("SELECT * FROM `user` LIMIT 0", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var col = reader.GetName(i);
            if (col is not ("id_user" or "password")) cols.Add(col);
        }
        return cols;
    }

    /// <summary>id_user in Khanza is a pegawai NIK (or kd_dokter / petugas NIP) — find a display name.</summary>
    private static async Task<string?> GetNamaAsync(MySqlConnection conn, string userId, CancellationToken ct)
    {
        foreach (var sql in new[]
                 {
                     "SELECT nama FROM pegawai WHERE nik=@u LIMIT 1",
                     "SELECT nm_dokter FROM dokter WHERE kd_dokter=@u LIMIT 1",
                     "SELECT nama FROM petugas WHERE nip=@u LIMIT 1",
                 })
        {
            try
            {
                await using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@u", userId);
                if (await cmd.ExecuteScalarAsync(ct) is string nama && nama.Length > 0) return nama;
            }
            catch (MySqlException)
            {
                // table missing in this install — try the next source
            }
        }
        return null;
    }
}

/// <summary>
/// Uses the legacy Khanza database; only when the database cannot be reached at all
/// does it fall back to the dev in-memory accounts, so an admin can still open
/// Pengaturan to fix the connection settings. Remove the fallback for production.
/// </summary>
public class KhanzaLoginService(Func<DatabaseSettings> settings, IAuthService devFallback) : IAuthService
{
    public async Task<LoginResult> LoginAsync(string userId, string password, CancellationToken ct = default)
    {
        try
        {
            return await new LegacyKhanzaAuthService(settings()).LoginAsync(userId, password, ct);
        }
        catch (MySqlException)
        {
            var dev = await devFallback.LoginAsync(userId, password, ct);
            return dev.Success ? dev : LoginResult.Fail(LoginFailureReason.DatabaseUnavailable);
        }
    }
}
