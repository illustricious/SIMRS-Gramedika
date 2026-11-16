using SimrsKhanza.Config;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.Kepegawaian;

public sealed record SkpKriteria(string Kode, string Nama, string Kategori, int Sasaran);

public sealed record SkpPenilaian(string Nomor, DateTime Tanggal, string NikDinilai, string NikPenilai, string Keterangan, string Status,
    IReadOnlyDictionary<string, string> Jawaban);

public sealed record SkpRekap(string Nomor, DateTime Tanggal, string NikDinilai, string NikPenilai, string[] Skp, string Kesimpulan,
    string Rekomendasi, IReadOnlyList<string> Penilaian);

/// <summary>
/// Pengkajian SKP (skp_penilaian + skp_detail_penilaian) and its rekapitulasi
/// (skp_rekapitulasi_penilaian + skp_detail_rekapitulasi_penilaian), legacy SKPPenilaianPegawai /
/// SKPRekapitulasiPenilaianPegawai. Numbers: SKP + yyyyMMdd + 4 digits per day, like the Java app.
/// </summary>
public class SkpRepository(DatabaseSettings settings)
{
    public static readonly string[] StatusPenilaian = ["Proses Penilaian", "Keluar Hasil"];

    private const string PenilaianSkor =
        "ROUND(IFNULL(SUM(d.skala_penilaian='Ya')*100/NULLIF(COUNT(d.kode_kriteria),0),0),2)";

    public async Task<List<SkpKriteria>> KriteriaAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("""
            SELECT k.kode_kriteria, k.nama_kriteria, c.nama_kategori, c.sasaran FROM skp_kriteria_penilaian k
            JOIN skp_kategori_penilaian c ON c.kode_kategori=k.kode_kategori ORDER BY c.sasaran, k.kode_kategori, k.kode_kriteria
            """)).Select(r => new SkpKriteria(r.S("kode_kriteria"), r.S("nama_kriteria"), r.S("nama_kategori"), (int)r.D("sasaran"))).ToList();
    }

    public async Task<List<Row>> ListPenilaianAsync(string? cari, DateTime from, DateTime to)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync($"""
            SELECT s.nomor_penilaian, s.tanggal, s.nik_dinilai, a.nama AS dinilai, s.nik_penilai, b.nama AS penilai, s.keterangan, s.status,
            {PenilaianSkor} AS nilai
            FROM skp_penilaian s LEFT JOIN pegawai a ON a.nik=s.nik_dinilai LEFT JOIN pegawai b ON b.nik=s.nik_penilai
            LEFT JOIN skp_detail_penilaian d ON d.nomor_penilaian=s.nomor_penilaian
            WHERE s.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',s.nomor_penilaian,a.nama,b.nama,s.keterangan,s.status) LIKE @p0
            GROUP BY s.nomor_penilaian, s.tanggal, s.nik_dinilai, a.nama, s.nik_penilai, b.nama, s.keterangan, s.status
            ORDER BY s.tanggal DESC
            """, $"%{cari?.Trim()}%", from.Date, to.Date.AddDays(1).AddSeconds(-1));
    }

    public async Task<SkpPenilaian?> GetPenilaianAsync(string nomor)
    {
        await using var db = await Db.OpenAsync(settings);
        var h = await db.FirstAsync("SELECT * FROM skp_penilaian WHERE nomor_penilaian=@p0", nomor);
        if (h is null) return null;
        var d = (await db.QueryAsync("SELECT kode_kriteria, skala_penilaian FROM skp_detail_penilaian WHERE nomor_penilaian=@p0", nomor))
            .ToDictionary(r => r.S("kode_kriteria"), r => r.S("skala_penilaian"));
        return new SkpPenilaian(nomor, h.Dt("tanggal") ?? DateTime.Now, h.S("nik_dinilai"), h.S("nik_penilai"), h.S("keterangan"), h.S("status"), d);
    }

    private static Task<string> NomorAsync(Db db, string table, string col, DateTime tanggal) => db.AutoNomorAsync(
        $"SELECT IFNULL(MAX(CONVERT(RIGHT({col},4),SIGNED)),0) FROM {table} WHERE DATE(tanggal)=@p0", $"SKP{tanggal:yyyyMMdd}", 4, tanggal.Date);

    public async Task<string> NextNomorPenilaianAsync(DateTime tanggal)
    {
        await using var db = await Db.OpenAsync(settings);
        return await NomorAsync(db, "skp_penilaian", "nomor_penilaian", tanggal);
    }

    public async Task<string> NextNomorRekapAsync(DateTime tanggal)
    {
        await using var db = await Db.OpenAsync(settings);
        return await NomorAsync(db, "skp_rekapitulasi_penilaian", "nomor_rekapitulasi", tanggal);
    }

    /// <summary>Insert (Nomor empty) or replace a penilaian with its answers; returns the number.</summary>
    public async Task<string> SavePenilaianAsync(SkpPenilaian p)
    {
        if (string.IsNullOrWhiteSpace(p.NikDinilai)) throw new ArgumentException("Pegawai yang dinilai wajib diisi.");
        if (string.IsNullOrWhiteSpace(p.NikPenilai)) throw new ArgumentException("Penilai wajib diisi.");
        if (p.Jawaban.Count == 0) throw new ArgumentException("Isi minimal satu kriteria (Ya/Tidak).");

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var nomor = p.Nomor;
        if (nomor.Length == 0)
        {
            nomor = await NomorAsync(db, "skp_penilaian", "nomor_penilaian", p.Tanggal);
            await db.ExecAsync("INSERT INTO skp_penilaian (nomor_penilaian,nik_dinilai,nik_penilai,tanggal,keterangan,status) VALUES (@p0,@p1,@p2,@p3,@p4,@p5)",
                nomor, p.NikDinilai, p.NikPenilai, p.Tanggal, p.Keterangan, p.Status);
        }
        else
        {
            await db.ExecAsync("UPDATE skp_penilaian SET nik_dinilai=@p1,nik_penilai=@p2,tanggal=@p3,keterangan=@p4,status=@p5 WHERE nomor_penilaian=@p0",
                nomor, p.NikDinilai, p.NikPenilai, p.Tanggal, p.Keterangan, p.Status);
            await db.ExecAsync("DELETE FROM skp_detail_penilaian WHERE nomor_penilaian=@p0", nomor);
        }
        foreach (var (kode, jawab) in p.Jawaban)
            await db.ExecAsync("INSERT INTO skp_detail_penilaian (nomor_penilaian,kode_kriteria,skala_penilaian) VALUES (@p0,@p1,@p2)", nomor, kode, jawab);
        await db.CommitAsync();
        return nomor;
    }

    public async Task DeletePenilaianAsync(string nomor)
    {
        await using var db = await Db.OpenAsync(settings);
        if (await db.CountAsync("SELECT COUNT(*) FROM skp_detail_rekapitulasi_penilaian WHERE nomor_penilaian=@p0", nomor) > 0)
            throw new InvalidOperationException("Pengkajian ini sudah dipakai di rekapitulasi; hapus rekapitulasinya dulu.");
        await db.BeginAsync();
        await db.ExecAsync("DELETE FROM skp_detail_penilaian WHERE nomor_penilaian=@p0", nomor);
        await db.ExecAsync("DELETE FROM skp_penilaian WHERE nomor_penilaian=@p0", nomor);
        await db.CommitAsync();
    }

    // ------------------------------------------------------------ rekapitulasi

    public async Task<List<Row>> ListRekapAsync(string? cari, DateTime from, DateTime to)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT r.*, a.nama AS dinilai, b.nama AS penilai FROM skp_rekapitulasi_penilaian r
            LEFT JOIN pegawai a ON a.nik=r.nik_dinilai LEFT JOIN pegawai b ON b.nik=r.nik_penilai
            WHERE r.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',r.nomor_rekapitulasi,a.nama,b.nama,r.kesimpulan) LIKE @p0
            ORDER BY r.tanggal DESC
            """, $"%{cari?.Trim()}%", from.Date, to.Date.AddDays(1).AddSeconds(-1));
    }

    public async Task<SkpRekap?> GetRekapAsync(string nomor)
    {
        await using var db = await Db.OpenAsync(settings);
        var h = await db.FirstAsync("SELECT * FROM skp_rekapitulasi_penilaian WHERE nomor_rekapitulasi=@p0", nomor);
        if (h is null) return null;
        var d = (await db.QueryAsync("SELECT nomor_penilaian FROM skp_detail_rekapitulasi_penilaian WHERE nomor_rekapitulasi=@p0", nomor))
            .Select(r => r.S("nomor_penilaian")).ToList();
        string[] cols = ["skp_pertama", "skp_kedua", "skp_ketiga", "skp_keempat", "skp_kelima", "skp_keenam"];
        return new SkpRekap(nomor, h.Dt("tanggal") ?? DateTime.Now, h.S("nik_dinilai"), h.S("nik_penilai"),
            cols.Select(h.S).ToArray(), h.S("kesimpulan"), h.S("rekomendasi"), d);
    }

    /// <summary>Penilaian of one pegawai in a period, with "% Ya" per sasaran 1–6 across all of them.</summary>
    public async Task<(List<Row> Penilaian, string[] Skp)> HitungAsync(string nikDinilai, DateTime from, DateTime to)
    {
        await using var db = await Db.OpenAsync(settings);
        var args = new object?[] { nikDinilai, from.Date, to.Date.AddDays(1).AddSeconds(-1) };
        var list = await db.QueryAsync($"""
            SELECT s.nomor_penilaian, s.tanggal, b.nama AS penilai, s.keterangan, {PenilaianSkor} AS nilai
            FROM skp_penilaian s LEFT JOIN pegawai b ON b.nik=s.nik_penilai LEFT JOIN skp_detail_penilaian d ON d.nomor_penilaian=s.nomor_penilaian
            WHERE s.nik_dinilai=@p0 AND s.tanggal BETWEEN @p1 AND @p2
            GROUP BY s.nomor_penilaian, s.tanggal, b.nama, s.keterangan ORDER BY s.tanggal
            """, args);
        var per = (await db.QueryAsync("""
            SELECT c.sasaran, SUM(d.skala_penilaian='Ya') AS ya, COUNT(*) AS n
            FROM skp_penilaian s JOIN skp_detail_penilaian d ON d.nomor_penilaian=s.nomor_penilaian
            JOIN skp_kriteria_penilaian k ON k.kode_kriteria=d.kode_kriteria JOIN skp_kategori_penilaian c ON c.kode_kategori=k.kode_kategori
            WHERE s.nik_dinilai=@p0 AND s.tanggal BETWEEN @p1 AND @p2 GROUP BY c.sasaran
            """, args)).ToDictionary(r => (int)r.D("sasaran"), r => r.D("n") == 0 ? 0 : r.D("ya") * 100 / r.D("n"));
        var skp = Enumerable.Range(1, 6).Select(i => per.TryGetValue(i, out var v) ? $"{Math.Round(v):0}%" : "").ToArray();
        return (list, skp);
    }

    public async Task<string> SaveRekapAsync(SkpRekap r)
    {
        if (string.IsNullOrWhiteSpace(r.NikDinilai)) throw new ArgumentException("Pegawai yang dinilai wajib diisi.");
        if (string.IsNullOrWhiteSpace(r.NikPenilai)) throw new ArgumentException("Penilai wajib diisi.");
        if (r.Skp.Any(s => s.Length > 5)) throw new ArgumentException("Nilai SKP maksimal 5 karakter (mis. 100%).");

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var nomor = r.Nomor;
        object?[] Args(string no) => [no, .. r.Skp, r.Kesimpulan, r.Rekomendasi, r.Tanggal, r.NikPenilai, r.NikDinilai];
        if (nomor.Length == 0)
        {
            nomor = await NomorAsync(db, "skp_rekapitulasi_penilaian", "nomor_rekapitulasi", r.Tanggal);
            await db.ExecAsync("""
                INSERT INTO skp_rekapitulasi_penilaian (nomor_rekapitulasi,skp_pertama,skp_kedua,skp_ketiga,skp_keempat,skp_kelima,skp_keenam,
                kesimpulan,rekomendasi,tanggal,nik_penilai,nik_dinilai) VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11)
                """, Args(nomor));
        }
        else
        {
            await db.ExecAsync("""
                UPDATE skp_rekapitulasi_penilaian SET skp_pertama=@p1,skp_kedua=@p2,skp_ketiga=@p3,skp_keempat=@p4,skp_kelima=@p5,skp_keenam=@p6,
                kesimpulan=@p7,rekomendasi=@p8,tanggal=@p9,nik_penilai=@p10,nik_dinilai=@p11 WHERE nomor_rekapitulasi=@p0
                """, Args(nomor));
            await db.ExecAsync("DELETE FROM skp_detail_rekapitulasi_penilaian WHERE nomor_rekapitulasi=@p0", nomor);
        }
        foreach (var p in r.Penilaian.Distinct())
            await db.ExecAsync("INSERT INTO skp_detail_rekapitulasi_penilaian (nomor_rekapitulasi,nomor_penilaian) VALUES (@p0,@p1)", nomor, p);
        await db.CommitAsync();
        return nomor;
    }

    public async Task DeleteRekapAsync(string nomor)
    {
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await db.ExecAsync("DELETE FROM skp_detail_rekapitulasi_penilaian WHERE nomor_rekapitulasi=@p0", nomor);
        await db.ExecAsync("DELETE FROM skp_rekapitulasi_penilaian WHERE nomor_rekapitulasi=@p0", nomor);
        await db.CommitAsync();
    }
}
