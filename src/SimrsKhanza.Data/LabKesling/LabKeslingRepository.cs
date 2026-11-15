using MySqlConnector;
using SimrsKhanza.Config;
using SimrsKhanza.Data.Keuangan;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.LabKesling;

public record PermintaanInput(
    string KodePelanggan, string NipPetugas, DateTime WaktuSampling, DateTime WaktuDiterima,
    string LokasiSampling, string DeskripsiSampel, string JenisSampel, string JumlahSampel,
    string SamplingOleh, string VolumeSampel, string WadahSampel, string KondisiWadah,
    string KodeSampel, IReadOnlyList<string> KodeParameter);

public record HasilInput(string KodeParameter, string Hasil);
public record VerifikasiDetailInput(string NoPenugasan, string KodeParameter, string NilaiNormal, string Hasil, string Keterangan);
public record BayarInput(string NamaBayar, string KdRek, double PpnPersen, double Bayar);

/// <summary>
/// Lab Kesehatan Lingkungan workflow on the legacy `labkesling_*` tables, ported from
/// simrs-khanza viabarcode/LabKesling*.java. Status transitions, number formats and the
/// journal postings (validasi, pembayaran and their cancellations) match the original
/// so both apps can work on the same data:
///
///   Permintaan (Permintaan Baru) ─┬─ Dapat Dilayani (Belum/Sudah Ada Penugasan)
///                                 └─ Tidak Dapat Dilayani
///   Penugasan (Belum/Sudah Keluar Hasil) → Hasil (Belum/Sudah Diverifikasi)
///   → Verifikasi (Belum/Sudah Divalidasi) → Validasi (Belum/Sudah Bayar) + jurnal
///   → Pembayaran + jurnal + tagihan_sadewa
/// </summary>
public class LabKeslingRepository(DatabaseSettings settings)
{
    private Task<Db> OpenAsync() => Db.OpenAsync(settings);

    private const string PelangganNama = "(SELECT nama_pelanggan FROM labkesling_pelanggan pl WHERE pl.kode_pelanggan=pm.kode_pelanggan)";
    private const string SampelNama = "(SELECT nama_sampel FROM labkesling_master_sampel ms WHERE ms.kode_sampel=pm.kode_sampel)";

    private static string Nama(string col) => $"IFNULL((SELECT nama FROM petugas WHERE petugas.nip={col}),'')";

    // ------------------------------------------------------------------ lookups

    public async Task<List<(string Kode, string Nama)>> PetugasAsync()
    {
        await using var db = await OpenAsync();
        return (await db.QueryAsync("SELECT nip, nama FROM petugas WHERE status='1' ORDER BY nama")).Select(r => (r.S("nip"), r.S("nama"))).ToList();
    }

    public async Task<List<(string Kode, string Nama)>> PelangganAsync()
    {
        await using var db = await OpenAsync();
        return (await db.QueryAsync("SELECT kode_pelanggan, nama_pelanggan FROM labkesling_pelanggan ORDER BY nama_pelanggan"))
            .Select(r => (r.S("kode_pelanggan"), r.S("nama_pelanggan"))).ToList();
    }

    public async Task<List<(string Kode, string Nama)>> SampelAsync()
    {
        await using var db = await OpenAsync();
        return (await db.QueryAsync("SELECT kode_sampel, nama_sampel FROM labkesling_master_sampel ORDER BY kode_sampel"))
            .Select(r => (r.S("kode_sampel"), r.S("nama_sampel"))).ToList();
    }

    /// <summary>Legacy permintaan form: parameters that have a nilai normal for this sampel.</summary>
    public async Task<List<Row>> ParameterUntukSampelAsync(string kodeSampel)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync("""
            SELECT p.kode_parameter, p.nama_parameter, p.metode_pengujian, p.satuan, p.kategori, n.nilai_normal, p.total
            FROM labkesling_parameter_pengujian p
            JOIN labkesling_nilai_normal_baku_mutu n ON n.kode_parameter=p.kode_parameter
            WHERE n.kode_sampel=@p0 ORDER BY p.kode_parameter
            """, kodeSampel);
    }

    // ------------------------------------------------------------------ permintaan

    public async Task<string> CreatePermintaanAsync(PermintaanInput x)
    {
        if (x.KodeParameter.Count == 0) throw new ArgumentException("Pilih minimal satu parameter pengujian.");
        if (string.IsNullOrWhiteSpace(x.KodePelanggan)) throw new ArgumentException("Pelanggan wajib dipilih.");
        if (string.IsNullOrWhiteSpace(x.KodeSampel)) throw new ArgumentException("Sampel wajib dipilih.");
        if (string.IsNullOrWhiteSpace(x.NipPetugas)) throw new ArgumentException("Petugas penerima wajib dipilih.");

        await using var db = await OpenAsync();
        for (var attempt = 0; ; attempt++)
        {
            await db.BeginAsync();
            try
            {
                var year = x.WaktuDiterima.Year.ToString();
                var no = await db.AutoNomorAsync(
                    "SELECT IFNULL(MAX(CONVERT(RIGHT(no_permintaan,5),SIGNED)),0) FROM labkesling_permintaan_pengujian_sampel WHERE DATE_FORMAT(waktu_diterima,'%Y')=@p0 AND kode_sampel=@p1",
                    $"{x.KodeSampel}/{year}/PPS/", 5, year, x.KodeSampel);
                await db.ExecAsync("""
                    INSERT INTO labkesling_permintaan_pengujian_sampel
                    (no_permintaan,kode_pelanggan,nip,waktu_sampling,waktu_diterima,lokasi_sampling,deskripsi_sampel,jenis_sampel,jumlah_sampel,
                     sampling_dilakukan_oleh,volume_sampel,wadah_sampel,kondisi_wadah_sampel,kode_sampel,status)
                    VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,'Permintaan Baru')
                    """, no, x.KodePelanggan, x.NipPetugas, x.WaktuSampling, x.WaktuDiterima, x.LokasiSampling, x.DeskripsiSampel, x.JenisSampel,
                    x.JumlahSampel, x.SamplingOleh, x.VolumeSampel, x.WadahSampel, x.KondisiWadah, x.KodeSampel);
                foreach (var p in x.KodeParameter.Distinct())
                    await db.ExecAsync("INSERT INTO labkesling_detail_permintaan_pengujian_sampel VALUES (@p0,@p1)", no, p);
                await db.CommitAsync();
                return no;
            }
            catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry && attempt < 2)
            {
                await db.DisposeTxAsync(); // number taken by another user — regenerate
            }
        }
    }

    /// <summary>status: null = semua.</summary>
    public async Task<List<Row>> ListPermintaanAsync(DateTime from, DateTime to, string? status, string? cari)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT pm.no_permintaan, pm.waktu_diterima, pm.waktu_sampling, pm.kode_pelanggan, {PelangganNama} AS nama_pelanggan,
                   pm.kode_sampel, {SampelNama} AS nama_sampel, pm.jenis_sampel, pm.lokasi_sampling, pm.deskripsi_sampel,
                   pm.jumlah_sampel, pm.volume_sampel, pm.wadah_sampel, pm.kondisi_wadah_sampel, pm.sampling_dilakukan_oleh,
                   pm.nip, {Nama("pm.nip")} AS nama_petugas, pm.status
            FROM labkesling_permintaan_pengujian_sampel pm
            WHERE DATE(pm.waktu_diterima) BETWEEN @p0 AND @p1 AND (@p2 IS NULL OR pm.status=@p2)
              AND CONCAT_WS(' ',pm.no_permintaan,pm.kode_pelanggan,{PelangganNama},pm.jenis_sampel,pm.lokasi_sampling) LIKE @p3
            ORDER BY pm.waktu_diterima DESC
            """, from.Date, to.Date, status, $"%{cari?.Trim()}%");
    }

    public async Task<List<Row>> DetailPermintaanAsync(string noPermintaan)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync("""
            SELECT d.kode_parameter, p.nama_parameter, p.metode_pengujian, p.satuan, p.kategori, p.total
            FROM labkesling_detail_permintaan_pengujian_sampel d
            JOIN labkesling_parameter_pengujian p ON p.kode_parameter=d.kode_parameter
            WHERE d.no_permintaan=@p0 ORDER BY d.kode_parameter
            """, noPermintaan);
    }

    private static async Task<string> StatusPermintaanAsync(Db db, string no)
        => await db.StrAsync("SELECT status FROM labkesling_permintaan_pengujian_sampel WHERE no_permintaan=@p0", no);

    public async Task DeletePermintaanAsync(string no)
    {
        await using var db = await OpenAsync();
        if (await StatusPermintaanAsync(db, no) != "Permintaan Baru")
            throw new InvalidOperationException("Hanya permintaan berstatus 'Permintaan Baru' yang bisa dihapus.");
        await db.BeginAsync();
        await db.ExecAsync("DELETE FROM labkesling_detail_permintaan_pengujian_sampel WHERE no_permintaan=@p0", no);
        await db.ExecAsync("DELETE FROM labkesling_permintaan_pengujian_sampel WHERE no_permintaan=@p0", no);
        await db.CommitAsync();
    }

    public async Task SetDapatDilayaniAsync(string no, bool dapat, string keterangan)
    {
        if (string.IsNullOrWhiteSpace(keterangan)) throw new ArgumentException("Keterangan wajib diisi.");
        await using var db = await OpenAsync();
        if (await StatusPermintaanAsync(db, no) != "Permintaan Baru")
            throw new InvalidOperationException("Permintaan ini sudah diproses.");
        await db.BeginAsync();
        if (dapat)
        {
            await db.ExecAsync("INSERT INTO labkesling_permintaan_pengujian_sampel_dilayani VALUES (@p0,@p1,'Belum Ada Penugasan')", no, keterangan);
            await db.ExecAsync("UPDATE labkesling_permintaan_pengujian_sampel SET status='Dapat Dilayani' WHERE no_permintaan=@p0", no);
        }
        else
        {
            await db.ExecAsync("INSERT INTO labkesling_permintaan_pengujian_sampel_tidak_dilayani VALUES (@p0,@p1)", no, keterangan);
            await db.ExecAsync("UPDATE labkesling_permintaan_pengujian_sampel SET status='Tidak Dapat Dilayani' WHERE no_permintaan=@p0", no);
        }
        await db.CommitAsync();
    }

    public async Task<List<Row>> ListDilayaniAsync(bool dapat, DateTime from, DateTime to, string? cari)
    {
        var table = dapat ? "labkesling_permintaan_pengujian_sampel_dilayani" : "labkesling_permintaan_pengujian_sampel_tidak_dilayani";
        var status = dapat ? "x.status" : "'Tidak Dapat Dilayani'";
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT pm.no_permintaan, pm.waktu_diterima, pm.kode_pelanggan, {PelangganNama} AS nama_pelanggan, pm.kode_sampel,
                   {SampelNama} AS nama_sampel, pm.jenis_sampel, pm.lokasi_sampling, x.keterangan, {status} AS status
            FROM {table} x JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=x.no_permintaan
            WHERE DATE(pm.waktu_diterima) BETWEEN @p0 AND @p1
              AND CONCAT_WS(' ',pm.no_permintaan,{PelangganNama},pm.jenis_sampel,x.keterangan) LIKE @p2
            ORDER BY pm.waktu_diterima DESC
            """, from.Date, to.Date, $"%{cari?.Trim()}%");
    }

    /// <summary>Legacy Cari...DapatDilayani / TidakDapatDilayani "hapus": permintaan back to 'Permintaan Baru'.</summary>
    public async Task DeleteDilayaniAsync(string no, bool dapat)
    {
        await using var db = await OpenAsync();
        if (dapat && await db.StrAsync("SELECT status FROM labkesling_permintaan_pengujian_sampel_dilayani WHERE no_permintaan=@p0", no) != "Belum Ada Penugasan")
            throw new InvalidOperationException("Sudah ada penugasan — hapus penugasannya dulu.");
        await db.BeginAsync();
        await db.ExecAsync(dapat
            ? "DELETE FROM labkesling_permintaan_pengujian_sampel_dilayani WHERE no_permintaan=@p0"
            : "DELETE FROM labkesling_permintaan_pengujian_sampel_tidak_dilayani WHERE no_permintaan=@p0", no);
        await db.ExecAsync("UPDATE labkesling_permintaan_pengujian_sampel SET status='Permintaan Baru' WHERE no_permintaan=@p0", no);
        await db.CommitAsync();
    }

    // ------------------------------------------------------------------ penugasan

    public async Task<string> CreatePenugasanAsync(string noPermintaan, string nipPj, string nipPelaksana, DateTime tanggal, string catatan, IReadOnlyList<string> kodeParameter)
    {
        if (kodeParameter.Count == 0) throw new ArgumentException("Pilih minimal satu parameter.");
        if (string.IsNullOrWhiteSpace(nipPj) || string.IsNullOrWhiteSpace(nipPelaksana)) throw new ArgumentException("PJ dan pelaksana wajib dipilih.");
        await using var db = await OpenAsync();
        if (await db.StrAsync("SELECT status FROM labkesling_permintaan_pengujian_sampel_dilayani WHERE no_permintaan=@p0", noPermintaan) != "Belum Ada Penugasan")
            throw new InvalidOperationException("Permintaan ini belum 'Dapat Dilayani' atau sudah ada penugasannya.");
        var kodeSampel = await db.StrAsync("SELECT kode_sampel FROM labkesling_permintaan_pengujian_sampel WHERE no_permintaan=@p0", noPermintaan);

        for (var attempt = 0; ; attempt++)
        {
            await db.BeginAsync();
            try
            {
                var year = tanggal.Year.ToString();
                var no = await db.AutoNomorAsync("""
                    SELECT IFNULL(MAX(CONVERT(RIGHT(t.no_penugasan,5),SIGNED)),0) FROM labkesling_penugasan_pengujian_sampel t
                    JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=t.no_permintaan
                    WHERE DATE_FORMAT(t.tanggal,'%Y')=@p0 AND pm.kode_sampel=@p1
                    """, $"{kodeSampel}/{year}/STP/", 5, year, kodeSampel);
                await db.ExecAsync("INSERT INTO labkesling_penugasan_pengujian_sampel VALUES (@p0,@p1,@p2,@p3,@p4,@p5,'Belum Keluar Hasil')",
                    noPermintaan, no, nipPj, nipPelaksana, tanggal, catatan ?? "");
                foreach (var p in kodeParameter.Distinct())
                    await db.ExecAsync("INSERT INTO labkesling_detail_penugasan_pengujian_sampel VALUES (@p0,@p1)", no, p);
                await db.ExecAsync("UPDATE labkesling_permintaan_pengujian_sampel_dilayani SET status='Sudah Ada Penugasan' WHERE no_permintaan=@p0", noPermintaan);
                await db.CommitAsync();
                return no;
            }
            catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry && attempt < 2)
            {
                await db.DisposeTxAsync();
            }
        }
    }

    public async Task<List<Row>> ListPenugasanAsync(DateTime from, DateTime to, string? status, string? cari)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT t.no_penugasan, t.no_permintaan, t.tanggal, {PelangganNama} AS nama_pelanggan, pm.kode_sampel, {SampelNama} AS nama_sampel,
                   t.nip_pj, {Nama("t.nip_pj")} AS nama_pj, t.nip_pelaksana, {Nama("t.nip_pelaksana")} AS nama_pelaksana, t.catatan, t.status
            FROM labkesling_penugasan_pengujian_sampel t JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=t.no_permintaan
            WHERE DATE(t.tanggal) BETWEEN @p0 AND @p1 AND (@p2 IS NULL OR t.status=@p2)
              AND CONCAT_WS(' ',t.no_penugasan,t.no_permintaan,{PelangganNama},t.catatan) LIKE @p3
            ORDER BY t.tanggal DESC
            """, from.Date, to.Date, status, $"%{cari?.Trim()}%");
    }

    public async Task<List<Row>> DetailPenugasanAsync(string noPenugasan)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync("""
            SELECT d.kode_parameter, p.nama_parameter, p.metode_pengujian, p.satuan, p.kategori,
                   IFNULL(n.nilai_normal,'') AS nilai_normal, IFNULL(h.hasil_pengujian,'') AS hasil_pengujian, IFNULL(h.status,'') AS status_hasil
            FROM labkesling_detail_penugasan_pengujian_sampel d
            JOIN labkesling_penugasan_pengujian_sampel t ON t.no_penugasan=d.no_penugasan
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=t.no_permintaan
            JOIN labkesling_parameter_pengujian p ON p.kode_parameter=d.kode_parameter
            LEFT JOIN labkesling_nilai_normal_baku_mutu n ON n.kode_sampel=pm.kode_sampel AND n.kode_parameter=d.kode_parameter
            LEFT JOIN labkesling_hasil_pengujian_sampel h ON h.no_penugasan=d.no_penugasan AND h.kode_parameter=d.kode_parameter
            WHERE d.no_penugasan=@p0 ORDER BY d.kode_parameter
            """, noPenugasan);
    }

    public async Task DeletePenugasanAsync(string noPenugasan)
    {
        await using var db = await OpenAsync();
        if (await db.CountAsync("SELECT COUNT(*) FROM labkesling_hasil_pengujian_sampel WHERE no_penugasan=@p0", noPenugasan) > 0)
            throw new InvalidOperationException("Penugasan sudah ada hasilnya — hapus data hasil dulu.");
        var noPermintaan = await db.StrAsync("SELECT no_permintaan FROM labkesling_penugasan_pengujian_sampel WHERE no_penugasan=@p0", noPenugasan);
        await db.BeginAsync();
        await db.ExecAsync("DELETE FROM labkesling_detail_penugasan_pengujian_sampel WHERE no_penugasan=@p0", noPenugasan);
        await db.ExecAsync("DELETE FROM labkesling_penugasan_pengujian_sampel WHERE no_penugasan=@p0", noPenugasan);
        if (await db.CountAsync("SELECT COUNT(*) FROM labkesling_penugasan_pengujian_sampel WHERE no_permintaan=@p0", noPermintaan) == 0)
            await db.ExecAsync("UPDATE labkesling_permintaan_pengujian_sampel_dilayani SET status='Belum Ada Penugasan' WHERE no_permintaan=@p0", noPermintaan);
        await db.CommitAsync();
    }

    // ------------------------------------------------------------------ hasil

    public async Task SaveHasilAsync(string noPenugasan, DateTime tanggal, IReadOnlyList<HasilInput> hasil)
    {
        var rows = hasil.Where(h => !string.IsNullOrWhiteSpace(h.Hasil)).ToList();
        if (rows.Count == 0) throw new ArgumentException("Isi minimal satu hasil pengujian.");
        await using var db = await OpenAsync();
        await db.BeginAsync();
        foreach (var h in rows)
        {
            if (await db.StrAsync("SELECT IFNULL(status,'') FROM labkesling_hasil_pengujian_sampel WHERE no_penugasan=@p0 AND kode_parameter=@p1", noPenugasan, h.KodeParameter) == "Sudah Diverifikasi")
                throw new InvalidOperationException($"Hasil parameter {h.KodeParameter} sudah diverifikasi, tidak bisa diubah.");
            await db.ExecAsync("""
                INSERT INTO labkesling_hasil_pengujian_sampel VALUES (@p0,@p1,@p2,@p3,'Belum Diverifikasi')
                ON DUPLICATE KEY UPDATE hasil_pengujian=VALUES(hasil_pengujian), tanggal=VALUES(tanggal)
                """, noPenugasan, h.KodeParameter, h.Hasil.Trim(), tanggal);
        }
        await db.ExecAsync("UPDATE labkesling_penugasan_pengujian_sampel SET status='Sudah Keluar Hasil' WHERE no_penugasan=@p0", noPenugasan);
        await db.CommitAsync();
    }

    public async Task<List<Row>> ListHasilAsync(DateTime from, DateTime to, string? status, string? cari)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT h.no_penugasan, t.no_permintaan, h.tanggal, {PelangganNama} AS nama_pelanggan, {SampelNama} AS nama_sampel,
                   h.kode_parameter, p.nama_parameter, p.satuan, IFNULL(n.nilai_normal,'') AS nilai_normal, h.hasil_pengujian,
                   {Nama("t.nip_pelaksana")} AS nama_pelaksana, h.status
            FROM labkesling_hasil_pengujian_sampel h
            JOIN labkesling_penugasan_pengujian_sampel t ON t.no_penugasan=h.no_penugasan
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=t.no_permintaan
            JOIN labkesling_parameter_pengujian p ON p.kode_parameter=h.kode_parameter
            LEFT JOIN labkesling_nilai_normal_baku_mutu n ON n.kode_sampel=pm.kode_sampel AND n.kode_parameter=h.kode_parameter
            WHERE DATE(h.tanggal) BETWEEN @p0 AND @p1 AND (@p2 IS NULL OR h.status=@p2)
              AND CONCAT_WS(' ',h.no_penugasan,t.no_permintaan,{PelangganNama},p.nama_parameter) LIKE @p3
            ORDER BY h.tanggal DESC, h.no_penugasan, h.kode_parameter
            """, from.Date, to.Date, status, $"%{cari?.Trim()}%");
    }

    public async Task DeleteHasilAsync(string noPenugasan, string kodeParameter)
    {
        await using var db = await OpenAsync();
        if (await db.StrAsync("SELECT status FROM labkesling_hasil_pengujian_sampel WHERE no_penugasan=@p0 AND kode_parameter=@p1", noPenugasan, kodeParameter) == "Sudah Diverifikasi")
            throw new InvalidOperationException("Hasil sudah diverifikasi — hapus verifikasinya dulu.");
        await db.BeginAsync();
        await db.ExecAsync("DELETE FROM labkesling_hasil_pengujian_sampel WHERE no_penugasan=@p0 AND kode_parameter=@p1", noPenugasan, kodeParameter);
        await db.ExecAsync("UPDATE labkesling_penugasan_pengujian_sampel SET status='Belum Keluar Hasil' WHERE no_penugasan=@p0", noPenugasan);
        await db.CommitAsync();
    }

    // ------------------------------------------------------------------ verifikasi

    /// <summary>Permintaan that have hasil 'Belum Diverifikasi' and no verifikasi yet.</summary>
    public async Task<List<Row>> SiapVerifikasiAsync()
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT pm.no_permintaan, pm.waktu_diterima, {PelangganNama} AS nama_pelanggan, pm.kode_sampel, {SampelNama} AS nama_sampel,
                   COUNT(*) AS jml_hasil
            FROM labkesling_hasil_pengujian_sampel h
            JOIN labkesling_penugasan_pengujian_sampel t ON t.no_penugasan=h.no_penugasan
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=t.no_permintaan
            WHERE h.status='Belum Diverifikasi'
              AND NOT EXISTS (SELECT 1 FROM labkesling_verifikasi_pengujian_sampel v WHERE v.no_permintaan=pm.no_permintaan)
            GROUP BY pm.no_permintaan ORDER BY pm.waktu_diterima
            """);
    }

    public async Task<List<Row>> HasilUntukVerifikasiAsync(string noPermintaan)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync("""
            SELECT h.no_penugasan, h.kode_parameter, p.nama_parameter, p.satuan, p.metode_pengujian,
                   IFNULL(n.nilai_normal,'') AS nilai_normal, h.hasil_pengujian, h.tanggal
            FROM labkesling_hasil_pengujian_sampel h
            JOIN labkesling_penugasan_pengujian_sampel t ON t.no_penugasan=h.no_penugasan
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=t.no_permintaan
            JOIN labkesling_parameter_pengujian p ON p.kode_parameter=h.kode_parameter
            LEFT JOIN labkesling_nilai_normal_baku_mutu n ON n.kode_sampel=pm.kode_sampel AND n.kode_parameter=h.kode_parameter
            WHERE t.no_permintaan=@p0 AND h.status='Belum Diverifikasi' ORDER BY h.no_penugasan, h.kode_parameter
            """, noPermintaan);
    }

    public async Task<string> CreateVerifikasiAsync(string noPermintaan, string nipPj, DateTime tanggal, string catatan,
        DateTime mulai, DateTime selesai, IReadOnlyList<VerifikasiDetailInput> detail)
    {
        if (detail.Count == 0) throw new ArgumentException("Tidak ada hasil yang diverifikasi.");
        if (string.IsNullOrWhiteSpace(nipPj)) throw new ArgumentException("PJ verifikasi wajib dipilih.");
        await using var db = await OpenAsync();
        if (await db.CountAsync("SELECT COUNT(*) FROM labkesling_verifikasi_pengujian_sampel WHERE no_permintaan=@p0", noPermintaan) > 0)
            throw new InvalidOperationException("Permintaan ini sudah diverifikasi.");
        var kodeSampel = await db.StrAsync("SELECT kode_sampel FROM labkesling_permintaan_pengujian_sampel WHERE no_permintaan=@p0", noPermintaan);
        for (var attempt = 0; ; attempt++)
        {
            await db.BeginAsync();
            try
            {
                var year = tanggal.Year.ToString();
                var no = await db.AutoNomorAsync("""
                    SELECT IFNULL(MAX(CONVERT(RIGHT(v.no_verifikasi,5),SIGNED)),0) FROM labkesling_verifikasi_pengujian_sampel v
                    JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=v.no_permintaan
                    WHERE DATE_FORMAT(v.tanggal,'%Y')=@p0 AND pm.kode_sampel=@p1
                    """, $"{kodeSampel}/{year}/LHUS/", 5, year, kodeSampel);
                await db.ExecAsync("INSERT INTO labkesling_verifikasi_pengujian_sampel VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,'Belum Divalidasi')",
                    noPermintaan, no, nipPj, tanggal, catatan ?? "", mulai, selesai);
                foreach (var d in detail)
                {
                    await db.ExecAsync("INSERT INTO labkesling_detail_verifikasi_pengujian_sampel VALUES (@p0,@p1,@p2,@p3,@p4,@p5)",
                        no, d.NoPenugasan, d.KodeParameter, d.NilaiNormal ?? "", d.Hasil ?? "", d.Keterangan ?? "");
                    await db.ExecAsync("UPDATE labkesling_hasil_pengujian_sampel SET status='Sudah Diverifikasi' WHERE no_penugasan=@p0 AND kode_parameter=@p1",
                        d.NoPenugasan, d.KodeParameter);
                }
                await db.CommitAsync();
                return no;
            }
            catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry && attempt < 2)
            {
                await db.DisposeTxAsync();
            }
        }
    }

    public async Task<List<Row>> ListVerifikasiAsync(DateTime from, DateTime to, string? status, string? cari)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT v.no_verifikasi, v.no_permintaan, v.tanggal, {PelangganNama} AS nama_pelanggan, pm.kode_sampel, {SampelNama} AS nama_sampel,
                   v.nip_pj, {Nama("v.nip_pj")} AS nama_pj, v.mulai_pengujian, v.selesai_pengujian, v.catatan, v.status
            FROM labkesling_verifikasi_pengujian_sampel v JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=v.no_permintaan
            WHERE DATE(v.tanggal) BETWEEN @p0 AND @p1 AND (@p2 IS NULL OR v.status=@p2)
              AND CONCAT_WS(' ',v.no_verifikasi,v.no_permintaan,{PelangganNama},v.catatan) LIKE @p3
            ORDER BY v.tanggal DESC
            """, from.Date, to.Date, status, $"%{cari?.Trim()}%");
    }

    public async Task<List<Row>> DetailVerifikasiAsync(string noVerifikasi)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync("""
            SELECT d.no_penugasan, d.kode_parameter, p.nama_parameter, p.satuan, p.metode_pengujian, d.nilai_normal, d.hasil_pengujian, d.keterangan
            FROM labkesling_detail_verifikasi_pengujian_sampel d JOIN labkesling_parameter_pengujian p ON p.kode_parameter=d.kode_parameter
            WHERE d.no_verifikasi=@p0 ORDER BY d.no_penugasan, d.kode_parameter
            """, noVerifikasi);
    }

    public async Task DeleteVerifikasiAsync(string noVerifikasi)
    {
        await using var db = await OpenAsync();
        if (await db.StrAsync("SELECT status FROM labkesling_verifikasi_pengujian_sampel WHERE no_verifikasi=@p0", noVerifikasi) != "Belum Divalidasi")
            throw new InvalidOperationException("Verifikasi sudah divalidasi — hapus validasinya dulu.");
        await db.BeginAsync();
        foreach (var d in await db.QueryAsync("SELECT no_penugasan, kode_parameter FROM labkesling_detail_verifikasi_pengujian_sampel WHERE no_verifikasi=@p0", noVerifikasi))
            await db.ExecAsync("UPDATE labkesling_hasil_pengujian_sampel SET status='Belum Diverifikasi' WHERE no_penugasan=@p0 AND kode_parameter=@p1",
                d.S("no_penugasan"), d.S("kode_parameter"));
        await db.ExecAsync("DELETE FROM labkesling_detail_verifikasi_pengujian_sampel WHERE no_verifikasi=@p0", noVerifikasi);
        await db.ExecAsync("DELETE FROM labkesling_verifikasi_pengujian_sampel WHERE no_verifikasi=@p0", noVerifikasi);
        await db.CommitAsync();
    }

    // ------------------------------------------------------------------ validasi (+ jurnal)

    private static readonly (string Col, string Debet, string DebetNama, string Kredit, string KreditNama)[] JurnalValidasi =
    [
        ("jasa_sarana", "Beban_Jasa_Sarana_Pelayanan_Lab_Kesling", "Beban Jasa Sarana Pelayanan Lab Kesling", "Utang_Jasa_sarana_Pelayanan_Lab_Kesling", "Utang Jasa Sarana Pelayanan Lab Kesling"),
        ("paket_bhp", "HPP_BHP_Pelayanan_Lab_Kesling", "HPP BHP Pelayanan Lab Kesling", "Persediaan_BHP_Pelayanan_Lab_Kesling", "Persediaan BHP Pelayanan Lab Kesling"),
        ("jasa_pj_lab", "Beban_Jasa_PJLab_Pelayanan_Lab_Kesling", "Beban Jasa PJ Lab Pelayanan Lab Kesling", "Utang_Jasa_PJLab_Pelayanan_Lab_Kesling", "Utang Jasa PJ Lab Pelayanan Lab Kesling"),
        ("jasa_pj_pengujian", "Beban_Jasa_PJPengujian_Pelayanan_Lab_Kesling", "Beban Jasa PJ Pengujian Pelayanan Lab Kesling", "Utang_Jasa_PJPengujian_Pelayanan_Lab_Kesling", "Utang Jasa PJ Pengujian Pelayanan Lab Kesling"),
        ("jasa_verifikator", "Beban_Jasa_PJVerifikasi_Pelayanan_Lab_Kesling", "Beban Jasa PJ Verifikasi Pelayanan Lab Kesling", "Utang_Jasa_PJVerifikasi_Pelayanan_Lab_Kesling", "Utang Jasa PJ Verifikasi Pelayanan Lab Kesling"),
        ("jasa_petugas", "Beban_Jasa_Analis_Pelayanan_Lab_Kesling", "Beban Jasa Analis Pelayanan Lab Kesling", "Utang_Jasa_Analis_Pelayanan_Lab_Kesling", "Utang Jasa Analis Pelayanan Lab Kesling"),
        ("kso", "Beban_KSO_Pelayanan_Lab_Kesling", "Beban KSO Pelayanan Lab Kesling", "Utang_KSO_Pelayanan_Lab_Kesling", "Utang KSO Pelayanan Lab Kesling"),
        ("jasa_menejemen", "Beban_Jasa_Menejemen_Pelayanan_Lab_Kesling", "Beban Jasa Menejemen Pelayanan Lab Kesling", "Utang_Jasa_Menejemen_Pelayanan_Lab_Kesling", "Utang Jasa Menejemen Pelayanan Lab Kesling"),
        ("total", "Suspen_Piutang_Pelayanan_Lab_Kesling", "Suspen Piutang Pelayanan Lab Kesling", "Pendapatan_Pelayanan_Lab_Kesling", "Pendapatan Pelayanan Lab Kesling"),
    ];

    private static IEnumerable<JurnalLine> ValidasiLines(Dictionary<string, string> akun, Func<string, double> sum, bool batal)
    {
        foreach (var j in JurnalValidasi)
        {
            var v = sum(j.Col);
            if (v <= 0) continue;
            var d = new JurnalLine(akun.GetValueOrDefault(j.Debet, ""), j.DebetNama, v, 0);
            var k = new JurnalLine(akun.GetValueOrDefault(j.Kredit, ""), j.KreditNama, 0, v);
            if (batal)
            {
                yield return d with { Debet = 0, Kredit = v };
                yield return k with { Debet = v, Kredit = 0 };
            }
            else
            {
                yield return d;
                yield return k;
            }
        }
    }

    public async Task<List<Row>> SiapValidasiAsync()
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT v.no_verifikasi, v.no_permintaan, v.tanggal, {PelangganNama} AS nama_pelanggan, pm.kode_sampel, {SampelNama} AS nama_sampel,
                   {Nama("v.nip_pj")} AS nama_verifikator
            FROM labkesling_verifikasi_pengujian_sampel v JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=v.no_permintaan
            WHERE v.status='Belum Divalidasi' ORDER BY v.tanggal
            """);
    }

    /// <summary>Rows the validasi will store: verifikasi detail + PJ/analis from the penugasan + tariff from the parameter master.</summary>
    public async Task<List<Row>> DetailUntukValidasiAsync(string noVerifikasi)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync("""
            SELECT d.no_penugasan, d.kode_parameter, p.nama_parameter, p.satuan, d.nilai_normal, d.hasil_pengujian, d.keterangan,
                   t.nip_pj AS nip_pjpengujian, t.nip_pelaksana AS nip_analis,
                   IFNULL(p.jasa_sarana,0) AS jasa_sarana, IFNULL(p.paket_bhp,0) AS paket_bhp, IFNULL(p.jasa_pj_lab,0) AS jasa_pj_lab,
                   p.jasa_pj_pengujian, p.jasa_verifikator, IFNULL(p.jasa_petugas,0) AS jasa_petugas, IFNULL(p.kso,0) AS kso,
                   IFNULL(p.jasa_menejemen,0) AS jasa_menejemen, IFNULL(p.total,0) AS total
            FROM labkesling_detail_verifikasi_pengujian_sampel d
            JOIN labkesling_penugasan_pengujian_sampel t ON t.no_penugasan=d.no_penugasan
            JOIN labkesling_parameter_pengujian p ON p.kode_parameter=d.kode_parameter
            WHERE d.no_verifikasi=@p0 ORDER BY d.no_penugasan, d.kode_parameter
            """, noVerifikasi);
    }

    public async Task<string> CreateValidasiAsync(string noVerifikasi, string nipPjLab, DateTime tanggal, string catatan, string petugasKode)
    {
        if (string.IsNullOrWhiteSpace(nipPjLab)) throw new ArgumentException("PJ Laboratorium wajib dipilih.");
        var detail = await DetailUntukValidasiAsync(noVerifikasi);
        if (detail.Count == 0) throw new InvalidOperationException("Detail verifikasi kosong.");

        await using var db = await OpenAsync();
        var v = await db.FirstAsync("""
            SELECT v.no_permintaan, v.nip_pj, v.status, pm.kode_sampel, pl.nama_pelanggan
            FROM labkesling_verifikasi_pengujian_sampel v
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=v.no_permintaan
            JOIN labkesling_pelanggan pl ON pl.kode_pelanggan=pm.kode_pelanggan
            WHERE v.no_verifikasi=@p0
            """, noVerifikasi) ?? throw new InvalidOperationException("Verifikasi tidak ditemukan.");
        if (v.S("status") != "Belum Divalidasi") throw new InvalidOperationException("Verifikasi ini sudah divalidasi.");
        var noPermintaan = v.S("no_permintaan");

        for (var attempt = 0; ; attempt++)
        {
            await db.BeginAsync();
            try
            {
                var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun2");
                var year = tanggal.Year.ToString();
                var no = await db.AutoNomorAsync("""
                    SELECT IFNULL(MAX(CONVERT(RIGHT(x.no_validasi,5),SIGNED)),0) FROM labkesling_validasi_pengujian_sampel x
                    JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=x.no_permintaan
                    WHERE DATE_FORMAT(x.tanggal,'%Y')=@p0 AND pm.kode_sampel=@p1
                    """, $"{v.S("kode_sampel")}/{year}/LHU/", 5, year, v.S("kode_sampel"));
                await db.ExecAsync("INSERT INTO labkesling_validasi_pengujian_sampel VALUES (@p0,@p1,@p2,@p3,@p4,@p5,'Belum Bayar')",
                    noPermintaan, no, nipPjLab, v.S("nip_pj"), tanggal, catatan ?? "");
                foreach (var d in detail)
                    await db.ExecAsync("INSERT INTO labkesling_detail_validasi_pengujian_sampel VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15)",
                        no, d.S("nip_pjpengujian"), d.S("nip_analis"), d.S("kode_parameter"), d.S("nilai_normal"), d.S("hasil_pengujian"), d.S("keterangan"),
                        d.D("jasa_sarana"), d.D("paket_bhp"), d.D("jasa_pj_lab"), d.D("jasa_pj_pengujian"), d.D("jasa_verifikator"),
                        d.D("jasa_petugas"), d.D("kso"), d.D("jasa_menejemen"), d.D("total"));

                await JurnalPoster.PostAsync(db.Conn, db.Tx!, no,
                    $"PELAYANAN LABORATORIUM KESEHATAN LINGKUNGAN {v.S("nama_pelanggan")} DIPOSTING OLEH {petugasKode}",
                    ValidasiLines(akun, c => detail.Sum(d => d.D(c)), batal: false));

                await db.ExecAsync("UPDATE labkesling_verifikasi_pengujian_sampel SET status='Sudah Divalidasi' WHERE no_verifikasi=@p0", noVerifikasi);
                await db.CommitAsync();
                return no;
            }
            catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry && attempt < 2)
            {
                await db.DisposeTxAsync();
            }
        }
    }

    public async Task<List<Row>> ListValidasiAsync(DateTime from, DateTime to, string? status, string? cari)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT x.no_validasi, x.no_permintaan, x.tanggal, pm.kode_pelanggan, {PelangganNama} AS nama_pelanggan, pm.kode_sampel, {SampelNama} AS nama_sampel,
                   x.nip_pj, {Nama("x.nip_pj")} AS nama_pj, x.nip_verifikator, {Nama("x.nip_verifikator")} AS nama_verifikator, x.catatan, x.status,
                   (SELECT IFNULL(SUM(total),0) FROM labkesling_detail_validasi_pengujian_sampel d WHERE d.no_validasi=x.no_validasi) AS total
            FROM labkesling_validasi_pengujian_sampel x JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=x.no_permintaan
            WHERE DATE(x.tanggal) BETWEEN @p0 AND @p1 AND (@p2 IS NULL OR x.status=@p2)
              AND CONCAT_WS(' ',x.no_validasi,x.no_permintaan,{PelangganNama},x.catatan) LIKE @p3
            ORDER BY x.tanggal DESC
            """, from.Date, to.Date, status, $"%{cari?.Trim()}%");
    }

    public async Task<List<Row>> DetailValidasiAsync(string noValidasi)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT d.kode_parameter, p.nama_parameter, p.satuan, d.nilai_normal, d.hasil_pengujian, d.keterangan,
                   {Nama("d.nip_pjpengujian")} AS nama_pjpengujian, {Nama("d.nip_analis")} AS nama_analis,
                   d.jasa_sarana, d.paket_bhp, d.jasa_pj_lab, d.jasa_pj_pengujian, d.jasa_verifikator, d.jasa_petugas, d.kso, d.jasa_menejemen, d.total
            FROM labkesling_detail_validasi_pengujian_sampel d JOIN labkesling_parameter_pengujian p ON p.kode_parameter=d.kode_parameter
            WHERE d.no_validasi=@p0 ORDER BY d.kode_parameter
            """, noValidasi);
    }

    /// <summary>Legacy ppHapusValidasiPengujian: only 'Belum Bayar'; posts the reversing journal.</summary>
    public async Task DeleteValidasiAsync(string noValidasi, string petugasKode)
    {
        await using var db = await OpenAsync();
        var x = await db.FirstAsync("""
            SELECT x.status, x.no_permintaan, pl.nama_pelanggan FROM labkesling_validasi_pengujian_sampel x
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=x.no_permintaan
            JOIN labkesling_pelanggan pl ON pl.kode_pelanggan=pm.kode_pelanggan WHERE x.no_validasi=@p0
            """, noValidasi) ?? throw new InvalidOperationException("Validasi tidak ditemukan.");
        if (x.S("status") != "Belum Bayar") throw new InvalidOperationException("Validasi sudah dibayar — hapus pembayarannya dulu (Rekap Pembayaran).");
        var detail = await db.QueryAsync("SELECT * FROM labkesling_detail_validasi_pengujian_sampel WHERE no_validasi=@p0", noValidasi);

        await db.BeginAsync();
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun2");
        await db.ExecAsync("DELETE FROM labkesling_detail_validasi_pengujian_sampel WHERE no_validasi=@p0", noValidasi);
        await db.ExecAsync("DELETE FROM labkesling_validasi_pengujian_sampel WHERE no_validasi=@p0", noValidasi);
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noValidasi,
            $"PEMBATALAN PELAYANAN LABORATORIUM KESEHATAN LINGKUNGAN {x.S("nama_pelanggan")} DIPOSTING OLEH {petugasKode}",
            ValidasiLines(akun, c => detail.Sum(d => d.D(c)), batal: true));
        await db.ExecAsync("UPDATE labkesling_verifikasi_pengujian_sampel SET status='Belum Divalidasi' WHERE no_permintaan=@p0", x.S("no_permintaan"));
        await db.CommitAsync();
    }

    // ------------------------------------------------------------------ pembayaran (+ jurnal, tagihan_sadewa)

    public async Task<List<(string Nama, string KdRek, double Ppn)>> AkunBayarAsync()
    {
        await using var db = await OpenAsync();
        return (await db.QueryAsync("SELECT nama_bayar, kd_rek, IFNULL(ppn,0) AS ppn FROM akun_bayar ORDER BY nama_bayar"))
            .Select(r => (r.S("nama_bayar"), r.S("kd_rek"), r.D("ppn"))).ToList();
    }

    /// <summary>Legacy Valid.roundUp(x,100).</summary>
    public static double RoundUp100(double x) => Math.Ceiling(Math.Round(x, 6) / 100) * 100;

    public async Task<string> CreatePembayaranAsync(string noPermintaan, DateTime tanggal, string dibayarOleh, IReadOnlyList<BayarInput> bayar, string petugasKode)
    {
        if (string.IsNullOrWhiteSpace(dibayarOleh)) throw new ArgumentException("Isi pihak pelanggan yang membayar.");
        var lines = bayar.Where(b => b.Bayar > 0).ToList();
        if (lines.Count == 0) throw new ArgumentException("Isi nominal pembayaran.");

        await using var db = await OpenAsync();
        var x = await db.FirstAsync("""
            SELECT x.no_validasi, x.status, pm.kode_pelanggan, pl.nama_pelanggan, IFNULL(pl.alamat,'') AS alamat,
                   (SELECT IFNULL(SUM(total),0) FROM labkesling_detail_validasi_pengujian_sampel d WHERE d.no_validasi=x.no_validasi) AS total
            FROM labkesling_validasi_pengujian_sampel x
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=x.no_permintaan
            JOIN labkesling_pelanggan pl ON pl.kode_pelanggan=pm.kode_pelanggan WHERE x.no_permintaan=@p0
            """, noPermintaan) ?? throw new InvalidOperationException("Validasi untuk permintaan ini tidak ditemukan.");
        if (x.S("status") != "Belum Bayar" || await db.CountAsync("SELECT COUNT(*) FROM labkesling_pembayaran_pengujian_sampel WHERE no_permintaan=@p0", noPermintaan) > 0)
            throw new InvalidOperationException("Maaf, tagihan pengujian dengan No.Permintaan tersebut sudah pernah dibayar.");

        var total = x.D("total");
        var dibayar = lines.Sum(l => l.Bayar);
        if (dibayar < total) throw new InvalidOperationException($"Pembayaran masih kurang {total - dibayar:N0}.");
        if (dibayar > total && lines.Count > 1) throw new InvalidOperationException("Kembalian harus 0 bila memakai lebih dari 1 cara bayar.");
        // Single cara bayar with kembalian: book only the tagihan (the change goes back to the customer) so the journal balances.
        if (dibayar > total) lines = [lines[0] with { Bayar = total }];

        for (var attempt = 0; ; attempt++)
        {
            await db.BeginAsync();
            try
            {
                var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun2");
                var no = await db.AutoNomorAsync(
                    "SELECT IFNULL(MAX(CONVERT(RIGHT(no_pembayaran,5),SIGNED)),0) FROM labkesling_pembayaran_pengujian_sampel WHERE DATE(tanggal)=@p0",
                    "LKL" + tanggal.ToString("yyyyMMdd"), 5, tanggal.Date);
                await db.ExecAsync("INSERT INTO labkesling_pembayaran_pengujian_sampel VALUES (@p0,@p1,@p2,@p3)", noPermintaan, no, tanggal, dibayarOleh);
                var jurnal = new List<JurnalLine>();
                foreach (var l in lines)
                {
                    var ppn = RoundUp100(l.PpnPersen / 100 * l.Bayar);
                    await db.ExecAsync("INSERT INTO labkesling_detail_pembayaran_pengujian_sampel VALUES (@p0,@p1,@p2,@p3)", no, l.NamaBayar, ppn, l.Bayar);
                    jurnal.Add(new JurnalLine(l.KdRek, l.NamaBayar, l.Bayar, 0));
                }
                jurnal.Add(new JurnalLine(akun.GetValueOrDefault("Suspen_Piutang_Pelayanan_Lab_Kesling", ""), "Suspen Piutang Pelayanan Lab Kesling", 0, total));
                await JurnalPoster.PostAsync(db.Conn, db.Tx!, no,
                    $"PEMBAYARAN PELAYANAN LABORATORIUM KESEHATAN LINGKUNGAN {x.S("nama_pelanggan")} DIPOSTING OLEH {petugasKode}", jurnal);
                await db.ExecAsync("INSERT INTO tagihan_sadewa VALUES (@p0,@p1,@p2,@p3,@p4,'Pelunasan',@p5,@p5,'Sudah',@p6)",
                    no, x.S("kode_pelanggan"), x.S("nama_pelanggan"), x.S("alamat"), tanggal, total, petugasKode.Length > 20 ? petugasKode[..20] : petugasKode);
                await db.ExecAsync("UPDATE labkesling_validasi_pengujian_sampel SET status='Sudah Bayar' WHERE no_permintaan=@p0", noPermintaan);
                await db.CommitAsync();
                return no;
            }
            catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry && attempt < 2)
            {
                await db.DisposeTxAsync();
            }
        }
    }

    public async Task<List<Row>> ListPembayaranAsync(DateTime from, DateTime to, string? cari)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT b.no_pembayaran, b.tanggal, b.no_permintaan, pm.kode_pelanggan, {PelangganNama} AS nama_pelanggan, {SampelNama} AS nama_sampel,
                   b.dibayar_oleh,
                   (SELECT IFNULL(SUM(besar_bayar),0) FROM labkesling_detail_pembayaran_pengujian_sampel d WHERE d.no_pembayaran=b.no_pembayaran) AS total,
                   (SELECT IFNULL(SUM(besarppn),0) FROM labkesling_detail_pembayaran_pengujian_sampel d WHERE d.no_pembayaran=b.no_pembayaran) AS ppn
            FROM labkesling_pembayaran_pengujian_sampel b JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=b.no_permintaan
            WHERE DATE(b.tanggal) BETWEEN @p0 AND @p1
              AND CONCAT_WS(' ',b.no_pembayaran,b.no_permintaan,{PelangganNama},b.dibayar_oleh) LIKE @p2
            ORDER BY b.tanggal DESC
            """, from.Date, to.Date, $"%{cari?.Trim()}%");
    }

    public async Task<List<Row>> DetailPembayaranAsync(string noPembayaran)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync("""
            SELECT d.nama_bayar, d.besar_bayar, d.besarppn, IFNULL(a.kd_rek,'') AS kd_rek
            FROM labkesling_detail_pembayaran_pengujian_sampel d LEFT JOIN akun_bayar a ON a.nama_bayar=d.nama_bayar
            WHERE d.no_pembayaran=@p0
            """, noPembayaran);
    }

    /// <summary>Legacy LabKeslingRekapPembayaran BtnHapus: reversing journal, remove tagihan_sadewa, validasi back to 'Belum Bayar'.</summary>
    public async Task DeletePembayaranAsync(string noPembayaran, string petugasKode)
    {
        await using var db = await OpenAsync();
        var b = await db.FirstAsync($"""
            SELECT b.no_permintaan, {PelangganNama} AS nama_pelanggan FROM labkesling_pembayaran_pengujian_sampel b
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=b.no_permintaan WHERE b.no_pembayaran=@p0
            """, noPembayaran) ?? throw new InvalidOperationException("Pembayaran tidak ditemukan.");
        var detail = await DetailPembayaranAsync(noPembayaran);

        await db.BeginAsync();
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun2");
        await db.ExecAsync("DELETE FROM labkesling_detail_pembayaran_pengujian_sampel WHERE no_pembayaran=@p0", noPembayaran);
        await db.ExecAsync("DELETE FROM labkesling_pembayaran_pengujian_sampel WHERE no_pembayaran=@p0", noPembayaran);
        var lines = new List<JurnalLine>
        {
            new(akun.GetValueOrDefault("Suspen_Piutang_Pelayanan_Lab_Kesling", ""), "Suspen Piutang Pelayanan Lab Kesling", detail.Sum(d => d.D("besar_bayar")), 0),
        };
        lines.AddRange(detail.Select(d => new JurnalLine(d.S("kd_rek"), d.S("nama_bayar"), 0, d.D("besar_bayar"))));
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noPembayaran,
            $"PEMBATALAN PEMBAYARAN PELAYANAN LABORATORIUM KESEHATAN LINGKUNGAN {b.S("nama_pelanggan")} DIPOSTING OLEH {petugasKode}", lines);
        await db.ExecAsync("DELETE FROM tagihan_sadewa WHERE no_nota=@p0", noPembayaran);
        await db.ExecAsync("UPDATE labkesling_validasi_pengujian_sampel SET status='Belum Bayar' WHERE no_permintaan=@p0", b.S("no_permintaan"));
        await db.CommitAsync();
    }

    // ------------------------------------------------------------------ rekap pelayanan

    public async Task<List<Row>> RekapPelayananAsync(DateTime from, DateTime to, string? cari)
    {
        await using var db = await OpenAsync();
        return await db.QueryAsync($"""
            SELECT x.no_validasi, x.tanggal, x.no_permintaan, {PelangganNama} AS nama_pelanggan, {SampelNama} AS nama_sampel,
                   d.kode_parameter, p.nama_parameter, {Nama("d.nip_pjpengujian")} AS nama_pjpengujian, {Nama("d.nip_analis")} AS nama_analis,
                   d.jasa_sarana, d.paket_bhp, d.jasa_pj_lab, d.jasa_pj_pengujian, d.jasa_verifikator, d.jasa_petugas, d.kso, d.jasa_menejemen, d.total, x.status
            FROM labkesling_detail_validasi_pengujian_sampel d
            JOIN labkesling_validasi_pengujian_sampel x ON x.no_validasi=d.no_validasi
            JOIN labkesling_permintaan_pengujian_sampel pm ON pm.no_permintaan=x.no_permintaan
            JOIN labkesling_parameter_pengujian p ON p.kode_parameter=d.kode_parameter
            WHERE DATE(x.tanggal) BETWEEN @p0 AND @p1
              AND CONCAT_WS(' ',x.no_validasi,x.no_permintaan,{PelangganNama},p.nama_parameter) LIKE @p2
            ORDER BY x.tanggal, x.no_validasi, d.kode_parameter
            """, from.Date, to.Date, $"%{cari?.Trim()}%");
    }
}
