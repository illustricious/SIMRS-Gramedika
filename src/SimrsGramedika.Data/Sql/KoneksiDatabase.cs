using MySqlConnector;
using SimrsGramedika.Config;

namespace SimrsGramedika.Data.Sql;

/// <summary>Connection checks for the "Koneksi Database" screen.</summary>
public static class KoneksiDatabase
{
    public sealed record HasilTes(bool Ok, string Pesan);

    /// <summary>Connects and checks that the SIMRS Khanza login tables exist in the chosen database.</summary>
    public static async Task<HasilTes> TesAsync(DatabaseSettings s, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new MySqlConnection(s.ToConnectionString());
            await conn.OpenAsync(ct);
            await using var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('admin','user','pasien','reg_periksa')",
                conn);
            var n = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            var versi = conn.ServerVersion;
            return n == 4
                ? new HasilTes(true, $"Berhasil terhubung ke {s.Summary} (server {versi}).")
                : new HasilTes(false, $"Terhubung ke server {versi}, tapi database '{s.Database}' bukan database SIMRS Khanza (tabel admin/user/pasien/reg_periksa tidak lengkap).");
        }
        catch (MySqlException ex)
        {
            return new HasilTes(false, Jelaskan(ex));
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or InvalidOperationException or ArgumentException)
        {
            return new HasilTes(false, $"Gagal konek: {ex.Message}");
        }
    }

    /// <summary>Databases visible to this user on the server (system schemas excluded).</summary>
    public static async Task<List<string>> DaftarDatabaseAsync(DatabaseSettings s, CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(s.ToServerConnectionString());
        await conn.OpenAsync(ct);
        await using var cmd = new MySqlCommand("SHOW DATABASES", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<string>();
        while (await r.ReadAsync(ct))
        {
            var name = r.GetString(0);
            if (name is "information_schema" or "performance_schema" or "mysql" or "sys") continue;
            list.Add(name);
        }
        return list;
    }

    public static string Jelaskan(MySqlException ex) => ex.ErrorCode switch
    {
        MySqlErrorCode.UnableToConnectToHost => "Server tidak bisa dihubungi. Cek alamat host, port, dan pastikan MySQL/MariaDB menyala serta tidak diblokir firewall.",
        MySqlErrorCode.AccessDenied => "User atau password database salah (atau user ini tidak boleh konek dari komputer ini).",
        MySqlErrorCode.UnknownDatabase => "Nama database tidak ditemukan di server. Klik 'Ambil daftar' untuk memilih.",
        MySqlErrorCode.DatabaseAccessDenied => "User ini tidak punya akses ke database tersebut.",
        _ => $"Gagal konek: {ex.Message}",
    };
}
