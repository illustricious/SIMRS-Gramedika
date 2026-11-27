using System.Globalization;
using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.Data.Keuangan;

namespace SimrsGramedika.Data;

public enum JenisOrderRalan { Resep, LabPk, LabPa, LabMb, Radiologi }
public sealed record BarisOrderRalan(string Kode, string Nama, decimal Jumlah = 0, string Aturan = "", IReadOnlyList<int>? Template = null);
public sealed record BahanPaRalan(DateTime? Diambil, string Cara, string Lokasi, string Pengawet, string TempatSebelumnya,
    DateTime? TanggalSebelumnya, string NomorSebelumnya, string DiagnosisSebelumnya);

/// <summary>Order tersimpan yang belum diproses unit tujuan; dipakai sebagai pembanding saat perubahan disimpan.</summary>
public sealed record DrafOrderRalan(string Nomor, string Dokter, string Diagnosa, string Informasi, BahanPaRalan? Bahan,
    IReadOnlyList<BarisOrderRalan> Baris)
{
    public IReadOnlyList<RacikanOrder> Racikan { get; init; } = [];

    internal string Sidik => System.Text.Json.JsonSerializer.Serialize(this);
}

public sealed class OrderRalanService(DatabaseSettings settings, string noRawat, string noRm, AuthenticatedUser user, JenisOrderRalan jenis)
{
    private sealed record FormatOrder(string Izin, string Header, string Detail, string Prefix, int Digit, string Kategori = "", string? Template = null);
    private readonly FormatOrder _format = jenis switch
    {
        JenisOrderRalan.Resep => new("resep_dokter", "resep_obat", "resep_dokter", "", 4),
        JenisOrderRalan.LabPk => new("permintaan_lab", "permintaan_lab", "permintaan_pemeriksaan_lab", "PK", 4, "PK", "permintaan_detail_permintaan_lab"),
        JenisOrderRalan.LabPa => new("permintaan_lab", "permintaan_labpa", "permintaan_pemeriksaan_labpa", "PA", 4, "PA"),
        JenisOrderRalan.LabMb => new("permintaan_lab", "permintaan_labmb", "permintaan_pemeriksaan_labmb", "MB", 4, "MB", "permintaan_detail_permintaan_labmb"),
        JenisOrderRalan.Radiologi => new("permintaan_radiologi", "permintaan_radiologi", "permintaan_pemeriksaan_radiologi", "PR", 3),
        _ => throw new ArgumentOutOfRangeException(nameof(jenis))
    };

    private bool Resep => jenis == JenisOrderRalan.Resep;
    private string SyaratTarif
    {
        get
        {
            var lab = jenis != JenisOrderRalan.Radiologi;
            var bayar = lab ? "cara_bayar_lab" : "cara_bayar_radiologi";
            var kelas = lab ? "kelas_lab" : "kelas_radiologi";
            var kategori = lab ? "m.kategori=@p2" : "@p2=''";
            return $"""
                m.status='1' AND {kategori}
                AND (COALESCE((SELECT {bayar} FROM set_tarif LIMIT 1),'Yes')<>'Yes' OR m.kd_pj IN ('-',r.kd_pj))
                AND (COALESCE((SELECT {kelas} FROM set_tarif LIMIT 1),'Yes')<>'Yes' OR m.kelas IN ('-','Rawat Jalan'))
                """;
        }
    }

    public async Task<List<Row>> TemplateAsync(string kode)
    {
        PastikanIzin();
        if (_format.Template is null) return [];
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT id_template,pemeriksaan FROM template_laboratorium WHERE kd_jenis_prw=@p0 ORDER BY urut,id_template
            """, kode);
    }

    public async Task<IReadOnlyList<BarisOrderRalan>> CariAsync(string kata)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var keyword = "%" + kata.Trim() + "%";
        var rows = Resep
            ? await db.QueryAsync("""
                SELECT kode_brng kode,nama_brng nama FROM databarang
                WHERE status='1' AND (kode_brng LIKE @p0 OR nama_brng LIKE @p0) ORDER BY nama_brng,kode_brng LIMIT 100
                """, keyword)
            : await db.QueryAsync($"""
                SELECT m.kd_jenis_prw kode,m.nm_perawatan nama FROM {MasterPemeriksaan} m
                JOIN reg_periksa r ON r.no_rawat=@p0 AND r.no_rkm_medis=@p1
                WHERE {SyaratTarif} AND (m.kd_jenis_prw LIKE @p3 OR m.nm_perawatan LIKE @p3)
                ORDER BY m.nm_perawatan,m.kd_jenis_prw LIMIT 100
                """, noRawat, noRm, _format.Kategori, keyword);
        return rows.Select(r => new BarisOrderRalan(r.S("kode"), r.S("nama"))).ToList();
    }

    public async Task<List<MetodeRacik>> MetodeRacikAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("SELECT kd_racik,nm_racik FROM metode_racik ORDER BY nm_racik,kd_racik"))
            .Select(r => new MetodeRacik(r.S("kd_racik"), r.S("nm_racik"))).ToList();
    }

    public async Task<IReadOnlyList<BahanRacikan>> CariBahanAsync(string kata)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("""
                SELECT kode_brng,nama_brng,IFNULL(kapasitas,0) kapasitas FROM databarang
                WHERE status='1' AND (kode_brng LIKE @p0 OR nama_brng LIKE @p0) ORDER BY nama_brng,kode_brng LIMIT 100
                """, "%" + kata.Trim() + "%"))
            .Select(r => new BahanRacikan(r.S("kode_brng"), r.S("nama_brng"), 0, Kapasitas: (decimal)r.D("kapasitas"))).ToList();
    }

    public async Task<List<Row>> RiwayatAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        if (Resep)
            return await db.QueryAsync("""
                SELECT h.no_resep nomor,h.tgl_peresepan tanggal,m.nama_brng nama,d.jml jumlah,d.aturan_pakai aturan,
                    IF(h.tgl_penyerahan>'0000-00-00','Diserahkan',IF(h.tgl_perawatan>'0000-00-00','Diverifikasi','Menunggu farmasi')) status
                FROM resep_obat h JOIN reg_periksa r ON r.no_rawat=h.no_rawat
                LEFT JOIN resep_dokter d ON d.no_resep=h.no_resep LEFT JOIN databarang m ON m.kode_brng=d.kode_brng
                WHERE h.no_rawat=@p0 AND r.no_rkm_medis=@p1 AND h.status='ralan' AND h.tgl_peresepan>'0000-00-00'
                    AND (d.no_resep IS NOT NULL OR NOT EXISTS(SELECT 1 FROM resep_dokter_racikan x WHERE x.no_resep=h.no_resep))
                UNION ALL
                SELECT h.no_resep,h.tgl_peresepan,
                    CONCAT('Racikan ',x.no_racik,': ',x.nama_racik,' (',IFNULL(c.nm_racik,x.kd_racik),')'),x.jml_dr,x.aturan_pakai,
                    IF(h.tgl_penyerahan>'0000-00-00','Diserahkan',IF(h.tgl_perawatan>'0000-00-00','Diverifikasi','Menunggu farmasi'))
                FROM resep_obat h JOIN reg_periksa r ON r.no_rawat=h.no_rawat
                JOIN resep_dokter_racikan x ON x.no_resep=h.no_resep LEFT JOIN metode_racik c ON c.kd_racik=x.kd_racik
                WHERE h.no_rawat=@p0 AND r.no_rkm_medis=@p1 AND h.status='ralan' AND h.tgl_peresepan>'0000-00-00'
                ORDER BY 1 DESC,3
                """, noRawat, noRm);
        return await db.QueryAsync($"""
            SELECT h.noorder nomor,h.tgl_permintaan tanggal,m.nm_perawatan nama,h.diagnosa_klinis diagnosa,
                h.informasi_tambahan informasi,IF(h.tgl_hasil>'0000-00-00','Hasil dicatat',IF(h.tgl_sampel>'0000-00-00','Diproses','Menunggu')) status
            FROM {_format.Header} h JOIN reg_periksa r ON r.no_rawat=h.no_rawat
            LEFT JOIN {_format.Detail} d ON d.noorder=h.noorder LEFT JOIN {MasterPemeriksaan} m ON m.kd_jenis_prw=d.kd_jenis_prw
            WHERE h.no_rawat=@p0 AND r.no_rkm_medis=@p1 AND h.status='ralan' ORDER BY h.noorder DESC,m.nm_perawatan
            """, noRawat, noRm);
    }

    private void Periksa(IReadOnlyList<BarisOrderRalan> baris, string diagnosa, string informasi, BahanPaRalan? bahan,
        IReadOnlyList<RacikanOrder> racikan)
    {
        if (!Resep && racikan.Count > 0) throw new ArgumentException("Racikan hanya ada pada resep.");
        PeriksaRacikan(racikan);
        if (baris.Count == 0 && racikan.Count == 0 || baris.Any(r => string.IsNullOrWhiteSpace(r.Kode)) ||
            baris.Select(r => r.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != baris.Count)
            throw new ArgumentException("Pilih paling sedikit satu item dan hindari kode ganda.");
        if (Resep && baris.Any(r => r.Jumlah <= 0 || string.IsNullOrWhiteSpace(r.Aturan) || r.Aturan.Length > 150))
            throw new ArgumentException("Isi jumlah positif dan aturan pakai untuk setiap obat.");
        if (!Resep && (diagnosa.Trim().Length is 0 or > 80 || informasi.Length > 60))
            throw new ArgumentException("Diagnosis klinis wajib diisi (maksimum 80 karakter). Informasi tambahan maksimum 60 karakter.");
        if (jenis == JenisOrderRalan.LabPa && (bahan is null || bahan.Cara.Length > 40 || bahan.Lokasi.Length > 40 ||
            bahan.Pengawet.Length > 40 || bahan.TempatSebelumnya.Length > 100 || bahan.NomorSebelumnya.Length > 20 || bahan.DiagnosisSebelumnya.Length > 100))
            throw new ArgumentException("Lengkapi informasi bahan PA sesuai panjang kolom.");
    }

    private static void PeriksaRacikan(IReadOnlyList<RacikanOrder> racikan)
    {
        if (racikan.Count > 98) throw new ArgumentException("Satu resep paling banyak 98 racikan.");
        foreach (var r in racikan)
        {
            var nama = r.Nama.Trim();
            if (nama.Length is 0 or > 100 || string.IsNullOrWhiteSpace(r.KodeMetode))
                throw new ArgumentException("Isi nama racikan (maksimum 100 karakter) dan metode racik.");
            if (r.Jumlah <= 0 || r.Aturan.Trim().Length is 0 or > 150 || r.Keterangan.Trim().Length > 50)
                throw new ArgumentException($"Racikan {nama}: isi jumlah positif dan aturan pakai. Keterangan maksimum 50 karakter.");
            if (r.Bahan.Count == 0 || r.Bahan.Any(b => string.IsNullOrWhiteSpace(b.Kode)) ||
                r.Bahan.Select(b => b.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != r.Bahan.Count)
                throw new ArgumentException($"Racikan {nama}: pilih paling sedikit satu obat dan hindari obat ganda.");
            if (r.Bahan.Any(b => b.Jumlah <= 0 || b.P1 < 0 || b.P2 < 0 || b.Kandungan.Trim().Length > 10))
                throw new ArgumentException($"Racikan {nama}: jumlah tiap obat harus positif dan kandungan maksimum 10 karakter.");
        }
    }

    private static async Task SimpanRacikanAsync(Db db, string nomor, IReadOnlyList<RacikanOrder> racikan)
    {
        for (var i = 0; i < racikan.Count; i++)
        {
            var r = racikan[i];
            var urut = (i + 1).ToString(CultureInfo.InvariantCulture);
            if (await db.ExecAsync("""
                    INSERT INTO resep_dokter_racikan(no_resep,no_racik,nama_racik,kd_racik,jml_dr,aturan_pakai,keterangan)
                    SELECT @p0,@p1,@p2,kd_racik,@p4,@p5,@p6 FROM metode_racik WHERE kd_racik=@p3
                    """, nomor, urut, r.Nama.Trim(), r.KodeMetode, r.Jumlah, r.Aturan.Trim(), r.Keterangan.Trim()) != 1)
                throw new InvalidOperationException($"Metode racik {r.KodeMetode} sudah tidak tersedia.");
            foreach (var b in r.Bahan)
                if (await db.ExecAsync("""
                        INSERT INTO resep_dokter_racikan_detail(no_resep,no_racik,kode_brng,p1,p2,kandungan,jml)
                        SELECT @p0,@p1,kode_brng,@p3,@p4,@p5,@p6 FROM databarang WHERE kode_brng=@p2 AND status='1'
                        """, nomor, urut, b.Kode, b.P1, b.P2, b.Kandungan.Trim(), b.Jumlah) != 1)
                    throw new InvalidOperationException($"Item {b.Kode} sudah tidak tersedia untuk kunjungan ini.");
        }
    }

    // Kolom waktu layanan Khanza memakai tanggal nol sampai unit penunjang memproses order.
    private static Task IzinkanTanggalNolAsync(Db db) => db.ExecAsync("""
        SET SESSION sql_mode=TRIM(BOTH ',' FROM REPLACE(REPLACE(CONCAT(',',@@SESSION.sql_mode,','),
            ',NO_ZERO_DATE,',','),',NO_ZERO_IN_DATE,',','))
        """);

    /// <summary>Kunci kunjungan; order hanya boleh dibuat atau diubah selama kunjungan rawat jalan belum ditagih.</summary>
    private async Task<Row> KunciKunjunganAsync(Db db)
    {
        var visit = await db.FirstAsync("""
            SELECT kd_dokter,status_bayar,NOW() waktu FROM reg_periksa
            WHERE no_rawat=@p0 AND no_rkm_medis=@p1 AND status_lanjut='Ralan' AND stts<>'Batal' FOR UPDATE
            """, noRawat, noRm) ?? throw new InvalidOperationException("Kunjungan rawat jalan tidak aktif.");
        if (await TagihanKunjungan.SudahDitagihAsync(db, noRawat) ||
            await db.CountAsync("SELECT COUNT(*) FROM kamar_inap WHERE no_rawat=@p0", noRawat) > 0)
            throw new InvalidOperationException("Kunjungan telah masuk billing atau rawat inap.");
        return visit;
    }

    public async Task<DrafOrderRalan> MuatOrderAsync(string nomor)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunciKunjunganAsync(db);
        var draf = await BacaOrderAsync(db, nomor);
        await db.CommitAsync();
        return draf;
    }

    /// <summary>Mengganti isi order yang belum diproses. Nomor, waktu dan dokter pengirim tetap.</summary>
    public async Task UbahAsync(DrafOrderRalan awal, IReadOnlyList<BarisOrderRalan> baris, string diagnosa, string informasi,
        BahanPaRalan? bahan = null, IReadOnlyList<RacikanOrder>? racikan = null)
    {
        PastikanIzin();
        racikan ??= [];
        Periksa(baris, diagnosa, informasi, bahan, racikan);
        await using var db = await Db.OpenAsync(settings);
        await IzinkanTanggalNolAsync(db);
        await db.BeginAsync();
        await KunciKunjunganAsync(db);
        var kini = await BacaOrderAsync(db, awal.Nomor);
        if (kini.Sidik != awal.Sidik)
            throw new InvalidOperationException("Order sudah diubah. Buka ulang untuk melihat isi terbaru.");
        if (Resep)
            foreach (var tabel in new[] { "resep_dokter_racikan_detail", "resep_dokter_racikan", "resep_dokter" })
                await db.ExecAsync($"DELETE FROM {tabel} WHERE no_resep=@p0", awal.Nomor);
        else
        {
            await db.ExecAsync($"UPDATE {_format.Header} SET diagnosa_klinis=@p1,informasi_tambahan=@p2 WHERE noorder=@p0",
                awal.Nomor, diagnosa.Trim(), informasi.Trim());
            if (jenis == JenisOrderRalan.LabPa && bahan is not null) await SimpanBahanAsync(db, awal.Nomor, bahan);
            if (_format.Template is { } template)
                await db.ExecAsync($"DELETE FROM {template} WHERE noorder=@p0", awal.Nomor);
            await db.ExecAsync($"DELETE FROM {_format.Detail} WHERE noorder=@p0", awal.Nomor);
        }
        foreach (var row in baris)
            await SimpanBarisAsync(db, awal.Nomor, row);
        await SimpanRacikanAsync(db, awal.Nomor, racikan);
        await db.CommitAsync();
    }

    /// <summary>
    /// Menghapus order yang belum diproses unit tujuan, seperti tombol Hapus pada daftar permintaan Khanza.
    /// Syaratnya sama dengan perubahan order: kunjungan belum ditagih, belum dibayar, dan oleh dokter pengirim atau Admin Utama.
    /// </summary>
    public async Task BatalkanAsync(string nomor)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunciKunjunganAsync(db);
        await BacaOrderAsync(db, nomor, untukUbah: false);
        // Baris anak dihapus eksplisit supaya tidak bergantung pada ON DELETE CASCADE database setempat.
        string[] anak = Resep ? ["resep_dokter_racikan_detail", "resep_dokter_racikan", "resep_dokter"]
            : _format.Template is { } template ? [template, _format.Detail] : [_format.Detail];
        var kunci = Resep ? "no_resep" : "noorder";
        try
        {
            foreach (var tabel in anak)
                await db.ExecAsync($"DELETE FROM {tabel} WHERE {kunci}=@p0", nomor);
            await db.ExecAsync($"DELETE FROM {_format.Header} WHERE {kunci}=@p0", nomor);
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            throw new InvalidOperationException("Order sudah dipakai data lain, misalnya bridging, sehingga tidak bisa dibatalkan.");
        }
        await db.CommitAsync();
    }

    private async Task<DrafOrderRalan> BacaOrderAsync(Db db, string nomor, bool untukUbah = true)
    {
        DrafOrderRalan draf;
        if (Resep)
        {
            var h = await db.FirstAsync("""
                SELECT kd_dokter,IF(tgl_perawatan>'0000-00-00' OR tgl_penyerahan>'0000-00-00',1,0) proses
                FROM resep_obat WHERE no_resep=@p0 AND no_rawat=@p1 AND status='ralan' AND tgl_peresepan>'0000-00-00' FOR UPDATE
                """, nomor, noRawat) ?? throw new InvalidOperationException("Resep tidak ditemukan pada kunjungan ini.");
            if (h.D("proses") != 0) throw new InvalidOperationException("Resep sudah divalidasi Farmasi dan tidak bisa diubah atau dibatalkan.");
            var obat = await db.QueryAsync("""
                SELECT d.kode_brng,d.jml,d.aturan_pakai,m.nama_brng FROM resep_dokter d
                LEFT JOIN databarang m ON m.kode_brng=d.kode_brng WHERE d.no_resep=@p0
                ORDER BY d.kode_brng,d.jml,d.aturan_pakai FOR UPDATE
                """, nomor);
            if (untukUbah && obat.Select(r => r.S("kode_brng")).Distinct(StringComparer.OrdinalIgnoreCase).Count() != obat.Count)
                throw new InvalidOperationException("Resep memuat obat ganda. Ubah lewat Khanza.");
            draf = new(nomor, h.S("kd_dokter"), "", "", null, obat.Select(r =>
                new BarisOrderRalan(r.S("kode_brng"), r.S("nama_brng"), (decimal)r.D("jml"), r.S("aturan_pakai"))).ToList())
            {
                Racikan = await BacaRacikanAsync(db, nomor, untukUbah),
            };
        }
        else
        {
            var h = await db.FirstAsync($"""
                SELECT dokter_perujuk,diagnosa_klinis,informasi_tambahan,IF(tgl_sampel>'0000-00-00' OR tgl_hasil>'0000-00-00',1,0) proses
                FROM {_format.Header} WHERE noorder=@p0 AND no_rawat=@p1 AND status='ralan' FOR UPDATE
                """, nomor, noRawat) ?? throw new InvalidOperationException("Permintaan tidak ditemukan pada kunjungan ini.");
            if (h.D("proses") != 0)
                throw new InvalidOperationException("Permintaan sudah diproses unit penunjang dan tidak bisa diubah atau dibatalkan.");
            var items = await db.QueryAsync($"""
                SELECT d.kd_jenis_prw,d.stts_bayar,m.nm_perawatan FROM {_format.Detail} d
                LEFT JOIN {MasterPemeriksaan} m ON m.kd_jenis_prw=d.kd_jenis_prw WHERE d.noorder=@p0
                ORDER BY d.kd_jenis_prw FOR UPDATE
                """, nomor);
            var rincian = _format.Template is { } template
                ? await db.QueryAsync($"SELECT kd_jenis_prw,id_template,stts_bayar FROM {template} WHERE noorder=@p0 ORDER BY kd_jenis_prw,id_template FOR UPDATE", nomor)
                : [];
            if (items.Concat(rincian).Any(r => r.S("stts_bayar") == "Sudah"))
                throw new InvalidOperationException("Sebagian pemeriksaan sudah dibayar. Hubungi kasir.");
            BahanPaRalan? bahan = null;
            if (jenis == JenisOrderRalan.LabPa)
            {
                var b = (await db.FirstAsync("""
                    SELECT CAST(pengambilan_bahan AS CHAR) ambil,diperoleh_dengan,lokasi_jaringan,diawetkan_dengan,pernah_dilakukan_di,
                           CAST(tanggal_pa_sebelumnya AS CHAR) sebelum,nomor_pa_sebelumnya,diagnosa_pa_sebelumnya
                    FROM permintaan_labpa WHERE noorder=@p0
                    """, nomor))!;
                static DateTime? Tanggal(string s) => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var t) ? t : null;
                bahan = new(Tanggal(b.S("ambil")), b.S("diperoleh_dengan"), b.S("lokasi_jaringan"), b.S("diawetkan_dengan"),
                    b.S("pernah_dilakukan_di"), Tanggal(b.S("sebelum")), b.S("nomor_pa_sebelumnya"), b.S("diagnosa_pa_sebelumnya"));
            }
            draf = new(nomor, h.S("dokter_perujuk"), h.S("diagnosa_klinis"), h.S("informasi_tambahan"), bahan, items.Select(r =>
                new BarisOrderRalan(r.S("kd_jenis_prw"), r.S("nm_perawatan"), Template: _format.Template is null ? null
                    : rincian.Where(d => d.S("kd_jenis_prw") == r.S("kd_jenis_prw")).Select(d => (int)d.D("id_template")).ToArray())).ToList());
        }
        if (!user.IsAdminUtama && !user.UserId.Equals(draf.Dokter, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Order hanya dapat diubah atau dibatalkan oleh dokter pengirimnya atau Admin Utama.");
        return draf;
    }

    private static async Task<List<RacikanOrder>> BacaRacikanAsync(Db db, string nomor, bool untukUbah)
    {
        var kepala = await db.QueryAsync("""
            SELECT x.no_racik,x.nama_racik,x.kd_racik,IFNULL(m.nm_racik,x.kd_racik) metode,x.jml_dr,x.aturan_pakai,x.keterangan
            FROM resep_dokter_racikan x LEFT JOIN metode_racik m ON m.kd_racik=x.kd_racik
            WHERE x.no_resep=@p0 ORDER BY CAST(x.no_racik AS UNSIGNED),x.no_racik FOR UPDATE
            """, nomor);
        var bahan = await db.QueryAsync("""
            SELECT d.no_racik,d.kode_brng,b.nama_brng,IFNULL(b.kapasitas,0) kapasitas,IFNULL(d.jml,0) jml,
                   IFNULL(d.p1,1) p1,IFNULL(d.p2,1) p2,IFNULL(d.kandungan,'') kandungan
            FROM resep_dokter_racikan_detail d LEFT JOIN databarang b ON b.kode_brng=d.kode_brng
            WHERE d.no_resep=@p0 ORDER BY d.no_racik,d.kode_brng FOR UPDATE
            """, nomor);
        if (untukUbah && bahan.Any(b => kepala.All(k => k.S("no_racik") != b.S("no_racik"))))
            throw new InvalidOperationException("Data racikan pada resep tidak lengkap. Ubah lewat Khanza.");
        return kepala.Select(k => new RacikanOrder(k.S("nama_racik"), k.S("kd_racik"), k.S("metode"), (int)k.D("jml_dr"),
            k.S("aturan_pakai"), k.S("keterangan"), bahan.Where(b => b.S("no_racik") == k.S("no_racik")).Select(b =>
                new BahanRacikan(b.S("kode_brng"), b.S("nama_brng"), (decimal)b.D("jml"), (decimal)b.D("p1"), (decimal)b.D("p2"),
                    b.S("kandungan"), (decimal)b.D("kapasitas"))).ToList())).ToList();
    }

    private static Task<int> SimpanBahanAsync(Db db, string nomor, BahanPaRalan bahan) => db.ExecAsync("""
        UPDATE permintaan_labpa SET pengambilan_bahan=COALESCE(@p1,'0000-00-00'),diperoleh_dengan=@p2,
        lokasi_jaringan=@p3,diawetkan_dengan=@p4,pernah_dilakukan_di=@p5,tanggal_pa_sebelumnya=COALESCE(@p6,'0000-00-00'),
        nomor_pa_sebelumnya=@p7,diagnosa_pa_sebelumnya=@p8 WHERE noorder=@p0
        """, nomor, bahan.Diambil?.Date, bahan.Cara, bahan.Lokasi, bahan.Pengawet, bahan.TempatSebelumnya,
        bahan.TanggalSebelumnya?.Date, bahan.NomorSebelumnya, bahan.DiagnosisSebelumnya);

    public async Task<string> SimpanAsync(IReadOnlyList<BarisOrderRalan> baris, string diagnosa, string informasi,
        BahanPaRalan? bahan = null, IReadOnlyList<RacikanOrder>? racikan = null)
    {
        PastikanIzin();
        racikan ??= [];
        Periksa(baris, diagnosa, informasi, bahan, racikan);
        await using var db = await Db.OpenAsync(settings);
        await IzinkanTanggalNolAsync(db);
        for (int percobaan = 1; ; percobaan++)
        {
            await db.BeginAsync();
            try
            {
                var visit = await KunciKunjunganAsync(db);
                var dokter = user.IsAdminUtama ? visit.S("kd_dokter") : user.UserId;
                if (await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0", dokter) != 1)
                    throw new InvalidOperationException("Akun harus terhubung ke dokter yang terdaftar.");
                var waktu = visit.Dt("waktu") ?? throw new InvalidOperationException("Waktu server tidak tersedia.");
                var nomor = await NomorAsync(db, waktu);
                await SimpanHeaderAsync(db, nomor, waktu, dokter, diagnosa.Trim(), informasi.Trim());
                if (jenis == JenisOrderRalan.LabPa && bahan is not null)
                    await SimpanBahanAsync(db, nomor, bahan);
                foreach (var row in baris)
                    await SimpanBarisAsync(db, nomor, row);
                await SimpanRacikanAsync(db, nomor, racikan);
                await db.CommitAsync();
                return nomor;
            }
            catch (MySqlException ex) when (percobaan < 5 && ex.Number is 1062 or 1213 or 1205)
            {
                await db.DisposeTxAsync();
            }
        }
    }

    private string MasterPemeriksaan => jenis == JenisOrderRalan.Radiologi ? "jns_perawatan_radiologi" : "jns_perawatan_lab";

    private async Task<string> NomorAsync(Db db, DateTime waktu)
    {
        var prefix = _format.Prefix + waktu.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var key = Resep ? "no_resep" : "noorder";
        var last = await db.CountAsync($"""
            SELECT COALESCE(MAX(CAST(RIGHT({key},{_format.Digit}) AS UNSIGNED)),0)
            FROM {_format.Header} WHERE {key} LIKE @p0
            """, prefix + "%");
        if (last >= (_format.Digit == 3 ? 999 : 9999))
            throw new InvalidOperationException("Nomor order untuk tanggal ini sudah penuh.");
        return prefix + (last + 1).ToString($"D{_format.Digit}", CultureInfo.InvariantCulture);
    }

    private Task<int> SimpanHeaderAsync(Db db, string nomor, DateTime waktu, string dokter, string diagnosa, string informasi)
    {
        var jam = waktu.TimeOfDay;
        return Resep
            ? db.ExecAsync("""
                INSERT INTO resep_obat(no_resep,tgl_perawatan,jam,no_rawat,kd_dokter,tgl_peresepan,jam_peresepan,status,tgl_penyerahan,jam_penyerahan)
                VALUES(@p0,'0000-00-00','00:00:00',@p1,@p2,@p3,@p4,'ralan','0000-00-00','00:00:00')
                """, nomor, noRawat, dokter, waktu.Date, jam)
            : db.ExecAsync($"""
                INSERT INTO {_format.Header}(noorder,no_rawat,tgl_permintaan,jam_permintaan,tgl_sampel,jam_sampel,tgl_hasil,jam_hasil,
                    dokter_perujuk,status,informasi_tambahan,diagnosa_klinis)
                VALUES(@p0,@p1,@p3,@p4,'0000-00-00','00:00:00','0000-00-00','00:00:00',@p2,'ralan',@p5,@p6)
                """, nomor, noRawat, dokter, waktu.Date, jam, informasi, diagnosa);
    }

    private async Task SimpanBarisAsync(Db db, string nomor, BarisOrderRalan row)
    {
        var affected = Resep
            ? await db.ExecAsync("""
                INSERT INTO resep_dokter(no_resep,kode_brng,jml,aturan_pakai)
                SELECT @p0,kode_brng,@p2,@p3 FROM databarang WHERE kode_brng=@p1 AND status='1'
                """, nomor, row.Kode, row.Jumlah, row.Aturan.Trim())
            : await db.ExecAsync($"""
                INSERT INTO {_format.Detail}(noorder,kd_jenis_prw,stts_bayar)
                SELECT @p3,m.kd_jenis_prw,'Belum' FROM {MasterPemeriksaan} m
                JOIN reg_periksa r ON r.no_rawat=@p0 AND r.no_rkm_medis=@p1
                WHERE {SyaratTarif} AND m.kd_jenis_prw=@p4
                """, noRawat, noRm, _format.Kategori, nomor, row.Kode);
        if (affected != 1) throw new InvalidOperationException($"Item {row.Kode} sudah tidak tersedia untuk kunjungan ini.");
        if (_format.Template is { } template && row.Template is not null)
        {
            foreach (var id in row.Template.Distinct())
            {
                var inserted = await db.ExecAsync($"""
                    INSERT INTO {template}(noorder,kd_jenis_prw,id_template,stts_bayar)
                    SELECT @p0,kd_jenis_prw,id_template,'Belum' FROM template_laboratorium WHERE kd_jenis_prw=@p1 AND id_template=@p2
                    """, nomor, row.Kode, id);
                if (inserted != 1) throw new InvalidOperationException("Rincian pemeriksaan lab sudah berubah. Pilih kembali item.");
            }
        }
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(_format.Izin)) throw new InvalidOperationException("Akun tidak memiliki hak mengakses alur ini.");
    }
}
