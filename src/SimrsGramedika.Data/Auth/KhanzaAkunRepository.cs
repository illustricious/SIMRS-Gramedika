using MySqlConnector;
using SimrsGramedika.Config;

namespace SimrsGramedika.Data.Auth;

public record KhanzaUserRow(string IdUser, string Nama, int JumlahAkses);
public record KhanzaTrackerRow(string Nip, string Nama, DateTime TglLogin, TimeSpan JamLogin)
{
    public string Tanggal => TglLogin.ToString("dd/MM/yyyy");
    public string Jam => JamLogin.ToString(@"hh\:mm\:ss");
}

/// <summary>
/// Account administration on the legacy SIMRS Khanza tables, compatible with the
/// original Java app (setting/DlgUser.java, DlgAdmin.java, MnGantiPassword, tracker):
/// `user.id_user` = AES_ENCRYPT(id,'nur'), `user.password` = AES_ENCRYPT(pw,'windi'),
/// `admin.usere`/`passworde` likewise, one enum('true','false') column per hak akses.
/// Hak-akses column names are always whitelisted against SHOW COLUMNS before
/// being placed in SQL, so no user input ever reaches an identifier.
/// </summary>
public class KhanzaAkunRepository(DatabaseSettings settings)
{
    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new MySqlConnection(settings.ToConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    private static MySqlCommand Cmd(MySqlConnection conn, string sql, params (string Name, object? Value)[] ps)
    {
        var cmd = new MySqlCommand(sql, conn);
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd;
    }

    // ---------------------------------------------------------------- hak akses columns

    /// <summary>All hak-akses columns of `user` (enum true/false), in table order.</summary>
    public async Task<IReadOnlyList<string>> GetAksesColumnsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        return await GetAksesColumnsAsync(conn, ct);
    }

    private static async Task<List<string>> GetAksesColumnsAsync(MySqlConnection conn, CancellationToken ct)
    {
        await using var cmd = Cmd(conn, "SHOW COLUMNS FROM `user`");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var cols = new List<string>();
        while (await r.ReadAsync(ct))
        {
            var name = r.GetString(0);
            var type = r.GetString(1);
            if (name is "id_user" or "password") continue;
            if (type.StartsWith("enum", StringComparison.OrdinalIgnoreCase)) cols.Add(name);
        }
        return cols;
    }

    // ---------------------------------------------------------------- user (pegawai)

    public async Task<IReadOnlyList<KhanzaUserRow>> ListUsersAsync(string? cari, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        // Count 'true' columns client-side: a 1,200-term SUM expression overflows mysqld's thread stack.
        // Then look up names separately (keeps SQL portable across Khanza versions).
        var rows = new List<(string Id, int Jml)>();
        await using (var cmd = Cmd(conn, "SELECT CAST(AES_DECRYPT(u.id_user,'nur') AS CHAR) AS __id, u.* FROM `user` u"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                if (r.IsDBNull(0)) continue;
                var n = 0;
                for (var i = 1; i < r.FieldCount; i++)
                    if (!r.IsDBNull(i) && r.GetValue(i) is string s && s == "true") n++;
                rows.Add((r.GetString(0), n));
            }
        }

        var names = await LookupNamesAsync(conn, rows.Select(x => x.Id).ToList(), ct);
        return rows
            .Select(x => new KhanzaUserRow(x.Id, names.GetValueOrDefault(x.Id, ""), x.Jml))
            .Where(x => string.IsNullOrWhiteSpace(cari)
                        || x.IdUser.Contains(cari, StringComparison.OrdinalIgnoreCase)
                        || x.Nama.Contains(cari, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.IdUser, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<Dictionary<string, string>> LookupNamesAsync(MySqlConnection conn, List<string> ids, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sql in new[]
                 {
                     "SELECT nik, nama FROM pegawai",
                     "SELECT kd_dokter, nm_dokter FROM dokter",
                     "SELECT nip, nama FROM petugas",
                 })
        {
            try
            {
                await using var cmd = Cmd(conn, sql);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    if (r.IsDBNull(0) || r.IsDBNull(1)) continue;
                    result.TryAdd(r.GetString(0), r.GetString(1));
                }
            }
            catch (MySqlException) { /* table missing in this install */ }
        }
        return ids.Where(result.ContainsKey).ToDictionary(i => i, i => result[i], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Names of the hak-akses columns set to 'true' for this user.</summary>
    public async Task<HashSet<string>> GetUserAksesAsync(string idUser, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, "SELECT * FROM `user` WHERE id_user=AES_ENCRYPT(@u,'nur') LIMIT 1", ("@u", idUser));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!await r.ReadAsync(ct)) return set;
        for (var i = 0; i < r.FieldCount; i++)
        {
            var c = r.GetName(i);
            if (c is "id_user" or "password" || r.IsDBNull(i)) continue;
            if (r.GetValue(i) is string s && s == "true") set.Add(c);
        }
        return set;
    }

    public async Task<bool> UserExistsAsync(string idUser, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, "SELECT COUNT(*) FROM `user` WHERE id_user=AES_ENCRYPT(@u,'nur')", ("@u", idUser));
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    /// <summary>
    /// Creates (originalId == null) or updates a user. Password null/empty on update = unchanged.
    /// Every hak-akses column is written: 'true' if in <paramref name="akses"/>, else 'false'.
    /// </summary>
    public async Task SaveUserAsync(string? originalId, string idUser, string? password, IReadOnlySet<string> akses, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idUser)) throw new ArgumentException("ID User wajib diisi.");
        await using var conn = await OpenAsync(ct);
        var cols = await GetAksesColumnsAsync(conn, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var sets = string.Join(",", cols.Select(c => $"`{c}`='{(akses.Contains(c) ? "true" : "false")}'"));

        if (originalId is null)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password wajib diisi untuk user baru.");
            await using (var chk = Cmd(conn, "SELECT COUNT(*) FROM `user` WHERE id_user=AES_ENCRYPT(@u,'nur')", ("@u", idUser)))
            {
                chk.Transaction = tx;
                if (Convert.ToInt64(await chk.ExecuteScalarAsync(ct)) > 0)
                    throw new InvalidOperationException($"User '{idUser}' sudah ada.");
            }
            var sql = $"INSERT INTO `user` SET id_user=AES_ENCRYPT(@u,'nur'), password=AES_ENCRYPT(@p,'windi'){(sets.Length > 0 ? "," + sets : "")}";
            await using var ins = Cmd(conn, sql, ("@u", idUser), ("@p", password));
            ins.Transaction = tx;
            await ins.ExecuteNonQueryAsync(ct);
        }
        else
        {
            if (!string.Equals(originalId, idUser, StringComparison.Ordinal))
            {
                await using var chk = Cmd(conn, "SELECT COUNT(*) FROM `user` WHERE id_user=AES_ENCRYPT(@u,'nur')", ("@u", idUser));
                chk.Transaction = tx;
                if (Convert.ToInt64(await chk.ExecuteScalarAsync(ct)) > 0)
                    throw new InvalidOperationException($"User '{idUser}' sudah ada.");
            }
            var pwSet = string.IsNullOrEmpty(password) ? "" : ", password=AES_ENCRYPT(@p,'windi')";
            var sql = $"UPDATE `user` SET id_user=AES_ENCRYPT(@u,'nur'){pwSet}{(sets.Length > 0 ? "," + sets : "")} WHERE id_user=AES_ENCRYPT(@orig,'nur')";
            await using var upd = Cmd(conn, sql, ("@u", idUser), ("@p", password), ("@orig", originalId));
            upd.Transaction = tx;
            if (await upd.ExecuteNonQueryAsync(ct) == 0)
                throw new InvalidOperationException($"User '{originalId}' tidak ditemukan.");
        }

        await tx.CommitAsync(ct);
    }

    public async Task DeleteUserAsync(string idUser, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, "DELETE FROM `user` WHERE id_user=AES_ENCRYPT(@u,'nur')", ("@u", idUser));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------------------------------------------------------------- admin

    public async Task<IReadOnlyList<string>> ListAdminsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, "SELECT CAST(AES_DECRYPT(usere,'nur') AS CHAR) FROM admin");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<string>();
        while (await r.ReadAsync(ct)) if (!r.IsDBNull(0)) list.Add(r.GetString(0));
        return list.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task AddAdminAsync(string usere, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(usere) || string.IsNullOrEmpty(password))
            throw new ArgumentException("ID admin dan password wajib diisi.");
        await using var conn = await OpenAsync(ct);
        await using (var chk = Cmd(conn, "SELECT COUNT(*) FROM admin WHERE usere=AES_ENCRYPT(@u,'nur')", ("@u", usere)))
            if (Convert.ToInt64(await chk.ExecuteScalarAsync(ct)) > 0)
                throw new InvalidOperationException($"Admin '{usere}' sudah ada.");
        await using var cmd = Cmd(conn, "INSERT INTO admin (usere, passworde) VALUES (AES_ENCRYPT(@u,'nur'), AES_ENCRYPT(@p,'windi'))",
            ("@u", usere), ("@p", password));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ResetAdminPasswordAsync(string usere, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password baru wajib diisi.");
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, "UPDATE admin SET passworde=AES_ENCRYPT(@p,'windi') WHERE usere=AES_ENCRYPT(@u,'nur')",
            ("@u", usere), ("@p", password));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAdminAsync(string usere, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using (var cnt = Cmd(conn, "SELECT COUNT(*) FROM admin"))
            if (Convert.ToInt64(await cnt.ExecuteScalarAsync(ct)) <= 1)
                throw new InvalidOperationException("Tidak bisa menghapus admin terakhir.");
        await using var cmd = Cmd(conn, "DELETE FROM admin WHERE usere=AES_ENCRYPT(@u,'nur')", ("@u", usere));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------------------------------------------------------------- ganti password (self)

    /// <summary>Changes the logged-in account's own password; the old password must match. Returns false if it doesn't.</summary>
    public async Task<bool> ChangeOwnPasswordAsync(bool isAdmin, string id, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newPassword)) throw new ArgumentException("Password baru wajib diisi.");
        await using var conn = await OpenAsync(ct);
        var sql = isAdmin
            ? "UPDATE admin SET passworde=AES_ENCRYPT(@n,'windi') WHERE usere=AES_ENCRYPT(@u,'nur') AND passworde=AES_ENCRYPT(@o,'windi')"
            : "UPDATE `user` SET password=AES_ENCRYPT(@n,'windi') WHERE id_user=AES_ENCRYPT(@u,'nur') AND password=AES_ENCRYPT(@o,'windi')";
        await using var cmd = Cmd(conn, sql, ("@u", id), ("@o", oldPassword), ("@n", newPassword));
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ---------------------------------------------------------------- tracker

    // Rows are written at login by LegacyKhanzaAuthService.TrackAsync.
    public async Task<IReadOnlyList<KhanzaTrackerRow>> ListTrackerAsync(DateOnly from, DateOnly to, string? cari, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, """
            SELECT t.nip, COALESCE(pg.nama, d.nm_dokter, ''), t.tgl_login, t.jam_login
            FROM tracker t
            LEFT JOIN pegawai pg ON pg.nik = t.nip
            LEFT JOIN dokter d ON d.kd_dokter = t.nip
            WHERE t.tgl_login BETWEEN @f AND @t
              AND (@c = '' OR t.nip LIKE CONCAT('%',@c,'%') OR pg.nama LIKE CONCAT('%',@c,'%') OR d.nm_dokter LIKE CONCAT('%',@c,'%'))
            ORDER BY t.tgl_login DESC, t.jam_login DESC
            LIMIT 5000
            """,
            ("@f", from.ToDateTime(TimeOnly.MinValue)), ("@t", to.ToDateTime(TimeOnly.MinValue)), ("@c", cari ?? ""));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<KhanzaTrackerRow>();
        while (await r.ReadAsync(ct))
            list.Add(new KhanzaTrackerRow(r.GetString(0), r.GetString(1), r.GetDateTime(2), r.GetTimeSpan(3)));
        return list;
    }
}
