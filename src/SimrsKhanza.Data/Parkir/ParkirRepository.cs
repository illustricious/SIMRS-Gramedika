using SimrsKhanza.Config;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.Parkir;

public sealed class ParkirRepository(DatabaseSettings settings)
{
    private const string BarcodeSql = "SELECT nomer_kartu FROM parkir_barcode WHERE kode_barcode=@p0";
    private const string MasihParkir = "(p.tgl_keluar IS NULL OR CAST(p.tgl_keluar AS CHAR)='0000-00-00')";

    public async Task<List<Row>> JenisAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("SELECT kd_parkir, jns_parkir, biaya, jenis FROM parkir_jenis ORDER BY jns_parkir, kd_parkir");
    }

    public async Task<List<Row>> PetugasAsync(string? nip)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync(
            "SELECT nip, COALESCE(nama, nip) AS nama FROM petugas WHERE (@p0 IS NULL OR nip=@p0) ORDER BY nama, nip", nip);
    }

    public async Task<string> CariKartuAsync(string? barcode)
    {
        var kode = Required(barcode, "Barcode", 15);
        await using var db = await Db.OpenAsync(settings);
        var kartu = await db.StrAsync(BarcodeSql, kode);
        return kartu.Length > 0 ? kartu : throw new InvalidOperationException("Barcode belum terdaftar di Barcode Parkir.");
    }

    public async Task<List<Row>> ListAsync(DateTime from, DateTime to, string? search)
    {
        if (from.Date > to.Date) throw new ArgumentException("Tanggal awal harus sebelum atau sama dengan tanggal akhir.");
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync($"""
            SELECT p.no_kendaraan, p.nomer_kartu, p.tgl_masuk, p.jam_masuk,
                   COALESCE(j.jns_parkir, p.kd_parkir) AS jenis_parkir,
                   COALESCE(t.nama, p.nip) AS petugas,
                   CASE WHEN {MasihParkir} THEN 'Masih Parkir' ELSE 'Keluar' END AS status
            FROM parkir p
            LEFT JOIN parkir_jenis j ON j.kd_parkir=p.kd_parkir
            LEFT JOIN petugas t ON t.nip=p.nip
            WHERE p.tgl_masuk BETWEEN @p0 AND @p1 AND
                  (p.no_kendaraan LIKE @p2 OR p.nomer_kartu LIKE @p2 OR j.jns_parkir LIKE @p2
                   OR p.kd_parkir LIKE @p2 OR t.nama LIKE @p2 OR p.nip LIKE @p2)
            ORDER BY p.tgl_masuk DESC, p.jam_masuk DESC, p.no_kendaraan
            """, from.Date, to.Date, $"%{search?.Trim()}%");
    }

    public async Task<string> MasukAsync(string? barcode, string? kendaraan, string? jenis, string? petugas)
    {
        var kodeBarcode = Required(barcode, "Barcode", 15);
        var plat = Required(string.Join(" ", (kendaraan ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant(), "Nomor kendaraan", 15);
        var kodeJenis = Required(jenis, "Jenis parkir", 5);
        var nip = Required(petugas, "Petugas", 20);
        await using var db = await Db.OpenAsync(settings);
        var kartu = "";

        // The legacy schema has no unique key for active vehicle/card assignments.
        await db.WithNamedLockAsync($"simrs.parkir.{db.Conn.Database.ToUpperInvariant()}",
            "Parkir sedang diproses di loket lain. Silakan coba lagi.", async () =>
        {
            await db.BeginAsync();
            try
            {
                if (await db.ScalarAsync("SELECT kd_parkir FROM parkir_jenis WHERE kd_parkir=@p0 FOR UPDATE", kodeJenis) is null)
                    throw new InvalidOperationException("Jenis parkir tidak ditemukan. Muat ulang pilihan.");
                kartu = await db.StrAsync(BarcodeSql + " FOR UPDATE", kodeBarcode);
                if (kartu.Length == 0) throw new InvalidOperationException("Barcode belum terdaftar di Barcode Parkir.");
                if (await db.ScalarAsync("SELECT nip FROM petugas WHERE nip=@p0 FOR UPDATE", nip) is null)
                    throw new InvalidOperationException("Petugas belum terdaftar di master Petugas.");
                if (await db.ScalarAsync($"""
                    SELECT p.no_kendaraan
                    FROM parkir p
                    WHERE {MasihParkir} AND (REPLACE(p.no_kendaraan,' ','')=@p0 OR p.nomer_kartu=@p1)
                    LIMIT 1
                    """, plat.Replace(" ", ""), kartu) is not null)
                    throw new InvalidOperationException("Kendaraan atau kartu masih tercatat aktif.");

                await db.ExecAsync("""
                    INSERT INTO parkir (no_kendaraan, tgl_masuk, jam_masuk, kd_parkir, nomer_kartu, nip)
                    VALUES (@p0, CURRENT_DATE, CURRENT_TIME, @p1, @p2, @p3)
                    """, plat, kodeJenis, kartu, nip);
                await db.CommitAsync();
            }
            catch
            {
                await db.DisposeTxAsync();
                throw;
            }
        });
        return kartu;
    }

    private static string Required(string? value, string label, int maxLength)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0) throw new ArgumentException($"{label} wajib diisi.");
        if (text.Length > maxLength) throw new ArgumentException($"{label} maksimal {maxLength} karakter.");
        return text;
    }
}
