using MySqlConnector;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data;

public sealed record RujukanMasukData(string Perujuk, string Alamat, string NoRujuk, double Jasa, string Dokter,
    string Diagnosa, string Kategori, string Keterangan, string NoBalasan);
public sealed record PerujukTerdaftar(string Nama, string Alamat);

public sealed class RujukanMasukService(DatabaseSettings settings, AuthenticatedUser user)
{
    public static bool BisaMengelola(AuthenticatedUser? user) =>
        user?.HasPermission("registrasi") == true && user.HasPermission("rujukan_masuk");

    private const string Select = """
        SELECT perujuk,alamat,no_rujuk,jm_perujuk,dokter_perujuk,kd_penyakit,kategori_rujuk,keterangan,no_balasan
        FROM rujuk_masuk WHERE no_rawat=@p0
        """;

    public async Task<RujukanMasukData?> BacaAsync(string noRawat)
    {
        PastikanAkses();
        await using var db = await Db.OpenAsync(settings);
        return Baca(await db.FirstAsync(Select, noRawat));
    }

    public async Task<IReadOnlyList<PerujukTerdaftar>> CariPerujukAsync(string keyword)
    {
        PastikanAkses();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT DISTINCT perujuk,alamat FROM rujuk_masuk
            WHERE perujuk <> '' AND (perujuk LIKE @p0 OR alamat LIKE @p0)
            ORDER BY perujuk,alamat LIMIT 100
            """, "%" + keyword.Trim() + "%");
        return rows.Select(row => new PerujukTerdaftar(row.S("perujuk"), row.S("alamat"))).ToList();
    }

    public async Task SimpanAsync(string noRawat, RujukanMasukData data, RujukanMasukData? sebelumnya)
    {
        PastikanAkses();
        Validasi(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(noRawat);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        if (sebelumnya is not null && Baca(await db.FirstAsync(Select + " FOR UPDATE", noRawat)) != sebelumnya)
            throw new InvalidOperationException("Data rujukan sudah berubah. Buka kembali formulir untuk memuat data terbaru.");
        await TulisAsync(db, noRawat, data, ubah: sebelumnya is not null);
        await db.CommitAsync();
    }

    public static void Validasi(RujukanMasukData data)
    {
        if (string.IsNullOrWhiteSpace(data.Perujuk) || string.IsNullOrWhiteSpace(data.Alamat)
            || string.IsNullOrWhiteSpace(data.Dokter) || string.IsNullOrWhiteSpace(data.Diagnosa)
            || string.IsNullOrWhiteSpace(data.Keterangan) || string.IsNullOrWhiteSpace(data.NoBalasan))
            throw new ArgumentException("Perujuk, alamat, dokter, kode diagnosis, keterangan dan nomor balasan wajib diisi.");
        if (!double.IsFinite(data.Jasa) || data.Jasa < 0 || data.Kategori is not ("-" or "Bedah" or "Non-Bedah" or "Kebidanan" or "Anak"))
            throw new ArgumentException("Jasa atau kategori rujukan tidak valid.");
        if (data.Perujuk.Length > 60 || data.Alamat.Length > 70 || data.NoRujuk.Length > 40 || data.Dokter.Length > 50
            || data.Diagnosa.Length > 15 || data.Keterangan.Length > 200 || data.NoBalasan.Length > 20)
            throw new ArgumentException("Panjang isian melebihi batas penyimpanan rujukan.");
    }

    internal static async Task TulisAsync(Db db, string noRawat, RujukanMasukData data, bool ubah)
    {
        const string insert = """
            INSERT INTO rujuk_masuk (no_rawat,perujuk,alamat,no_rujuk,jm_perujuk,dokter_perujuk,kd_penyakit,kategori_rujuk,keterangan,no_balasan)
            VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)
            """;
        const string update = """
            UPDATE rujuk_masuk SET perujuk=@p1,alamat=@p2,no_rujuk=@p3,jm_perujuk=@p4,dokter_perujuk=@p5,
                kd_penyakit=@p6,kategori_rujuk=@p7,keterangan=@p8,no_balasan=@p9 WHERE no_rawat=@p0
            """;
        try
        {
            await db.ExecAsync(ubah ? update : insert, noRawat, data.Perujuk, data.Alamat, data.NoRujuk,
                data.Jasa, data.Dokter, data.Diagnosa, data.Kategori, data.Keterangan, data.NoBalasan);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new InvalidOperationException("Rujukan masuk untuk kunjungan ini sudah ada. Buka kembali formulir.", ex);
        }
        catch (MySqlException ex) when (ex.Number == 1452)
        {
            throw new InvalidOperationException("Kunjungan atau kode diagnosis tidak ditemukan.", ex);
        }
    }

    private static RujukanMasukData? Baca(Row? row) => row is null ? null : new(row.S("perujuk"), row.S("alamat"),
        row.S("no_rujuk"), row.D("jm_perujuk"), row.S("dokter_perujuk"), row.S("kd_penyakit"), row.S("kategori_rujuk"),
        row.S("keterangan"), row.S("no_balasan"));

    private void PastikanAkses()
    {
        if (!BisaMengelola(user)) throw new InvalidOperationException("Akun tidak memiliki hak mengelola rujukan masuk.");
    }
}
