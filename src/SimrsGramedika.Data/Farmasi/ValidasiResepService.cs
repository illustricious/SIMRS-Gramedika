using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Farmasi;

/// <summary>Satu resep_obat di daftar farmasi.</summary>
public sealed record ResepFarmasi(
    string NoResep, string NoRawat, string NoRm, string Pasien, string Dokter, string Poli, string CaraBayar,
    bool Ranap, DateTime? Diresepkan, DateTime? Divalidasi, DateTime? Diserahkan)
{
    public const string Menunggu = "Menunggu validasi";
    public const string Tervalidasi = "Sudah divalidasi";
    public const string Selesai = "Sudah diserahkan";

    public string Status => Diserahkan is not null ? Selesai : Divalidasi is not null ? Tervalidasi : Menunggu;
}

/// <summary>
/// Satu obat beserta harga, HPP, batch dan stok depo saat dimuat. Racik kosong berarti bukan racikan.
/// Tambahan berarti tidak ada pada resep dokter (ditambahkan atau pengganti).
/// </summary>
public sealed record BarisValidasi(
    string Kode, string Nama, string Satuan, double JumlahResep, string Aturan, double Harga, double Hpp, double Stok,
    string NoBatch, string NoFaktur, string Racik = "", bool Aktif = true, bool Tambahan = false);

/// <summary>Kelompok racikan pada resep dokter.</summary>
public sealed record RacikanResep(
    string NoRacik, string Nama, string KodeMetode, string Metode, int Jumlah, string Aturan, string Keterangan);

/// <summary>Obat yang tercatat diberikan saat resep divalidasi.</summary>
public sealed record ObatDiberikan(
    string Kode, string Nama, string Satuan, double Jumlah, double Harga, double Embalase, double Tuslah, double Total,
    string NoBatch, string Aturan);

/// <summary>Isi resep. Halangan berisi alasan resep tidak dapat divalidasi dari aplikasi ini.</summary>
public sealed record RincianResep(
    ResepFarmasi Resep, string KodeDepo, string NamaDepo, double Embalase, double Tuslah,
    IReadOnlyList<BarisValidasi> Baris, IReadOnlyList<string> Halangan)
{
    /// <summary>Kelas kamar penentu harga rawat inap.</summary>
    public string Kelas { get; init; } = "";
    public IReadOnlyList<RacikanResep> Racikan { get; init; } = [];
    public IReadOnlyList<ObatDiberikan> Diberikan { get; init; } = [];
}

/// <summary>
/// Keputusan farmasi per obat; harga dan batch adalah yang terlihat saat dikonfirmasi. Baris yang tidak ada
/// pada resep dianggap obat tambahan: Aturan wajib diisi bila bukan bagian racikan.
/// </summary>
public sealed record InputValidasi(
    string Kode, double Jumlah, double Embalase, double Tuslah, double Harga, string NoBatch, string NoFaktur,
    string Racik = "", string? Aturan = null);

/// <summary>
/// Validasi, pembatalan dan penyerahan resep, mengikuti DlgDaftarPermintaanResep, DlgCariObat, DlgCariObat2 dan
/// DlgPemberianObat Khanza: detail_pemberian_obat, aturan_pakai, obat_racikan, detail_obat_racikan, stok
/// (gudangbarang, data_batch, riwayat_barang_medis), waktu validasi di resep_obat dan jurnal obat, masing-masing
/// dalam satu transaksi. Penyerahan mengisi waktu penyerahan seperti halaman web penyerahanresep (tanpa foto).
/// </summary>
public sealed class ValidasiResepService(DatabaseSettings settings, AuthenticatedUser user)
{
    public const string Izin = "beri_obat";
    private const double Toleransi = 0.000001;

    private const string SqlResep = """
        SELECT o.no_resep,o.no_rawat,o.status,r.no_rkm_medis,r.kd_poli,r.kd_pj,r.stts,r.status_bayar,p.nm_pasien,d.nm_dokter,
               IFNULL(pl.nm_poli,'') AS nm_poli,IFNULL(pj.png_jawab,'') AS png_jawab,
               IF(o.tgl_peresepan>'0000-00-00',TIMESTAMP(o.tgl_peresepan,o.jam_peresepan),NULL) AS diresepkan,
               IF(o.tgl_perawatan>'0000-00-00',TIMESTAMP(o.tgl_perawatan,o.jam),NULL) AS divalidasi,
               IF(o.tgl_penyerahan>'0000-00-00',TIMESTAMP(o.tgl_penyerahan,o.jam_penyerahan),NULL) AS diserahkan
        FROM resep_obat o
        JOIN reg_periksa r ON r.no_rawat=o.no_rawat
        JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
        JOIN dokter d ON d.kd_dokter=o.kd_dokter
        LEFT JOIN poliklinik pl ON pl.kd_poli=r.kd_poli
        LEFT JOIN penjab pj ON pj.kd_pj=r.kd_pj
        """;

    /// <summary>Keadaan yang menentukan depo, harga dan rekening untuk satu resep.</summary>
    private sealed record Konteks(bool Ranap, string KodeDepo, string Kelas, double Kenaikan, string KolomHarga);

    public bool BolehValidasi => user.HasPermission(Izin);

    /// <summary>Resep yang diresepkan atau divalidasi dalam rentang tanggal, terbaru di atas.</summary>
    public async Task<IReadOnlyList<ResepFarmasi>> DaftarAsync(DateTime dari, DateTime sampai, string cari = "", string? status = null)
    {
        PeriksaLihat();
        await using var db = await Db.OpenAsync(settings);
        // Disaring di SQL sebelum LIMIT supaya resep yang menunggu tidak terpotong oleh resep yang sudah selesai.
        var syarat = status switch
        {
            ResepFarmasi.Menunggu => "AND (o.tgl_perawatan IS NULL OR o.tgl_perawatan<='0000-00-00') AND o.tgl_penyerahan<='0000-00-00'",
            ResepFarmasi.Tervalidasi => "AND o.tgl_perawatan>'0000-00-00' AND o.tgl_penyerahan<='0000-00-00'",
            ResepFarmasi.Selesai => "AND o.tgl_penyerahan>'0000-00-00'",
            _ => "",
        };
        var rows = await db.QueryAsync($"""
            {SqlResep}
            WHERE (o.tgl_peresepan BETWEEN @p0 AND @p1 OR o.tgl_perawatan BETWEEN @p0 AND @p1) {syarat}
              AND CONCAT_WS(' ',o.no_resep,o.no_rawat,r.no_rkm_medis,p.nm_pasien,d.nm_dokter,pl.nm_poli,pj.png_jawab) LIKE @p2
            ORDER BY o.no_resep DESC LIMIT 500
            """, dari.Date, sampai.Date, $"%{cari.Trim()}%");
        return rows.Select(Baca).ToList();
    }

    public async Task<RincianResep> RincianAsync(string noResep)
    {
        PeriksaLihat();
        await using var db = await Db.OpenAsync(settings);
        return (await MuatAsync(db, noResep)).Rincian;
    }

    /// <summary>
    /// Obat aktif yang dapat ditambahkan atau dipakai mengganti, dengan harga dan stok depo resep itu.
    /// Hanya yang berstok.
    /// </summary>
    public async Task<IReadOnlyList<BarisValidasi>> CariObatAsync(string noResep, string cari)
    {
        PeriksaUbah();
        await using var db = await Db.OpenAsync(settings);
        var (rincian, konteks) = await MuatAsync(db, noResep);
        if (konteks.KodeDepo.Length == 0) throw new InvalidOperationException(string.Join(" ", rincian.Halangan));
        var rows = await db.QueryAsync("""
            SELECT b.kode_brng,b.nama_brng,IFNULL(s.satuan,b.kode_sat) AS satuan
            FROM databarang b LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            WHERE b.status='1' AND CONCAT_WS(' ',b.kode_brng,b.nama_brng) LIKE @p0
              AND EXISTS(SELECT 1 FROM gudangbarang g WHERE g.kode_brng=b.kode_brng AND g.kd_bangsal=@p1 AND g.stok>0)
            ORDER BY b.nama_brng,b.kode_brng LIMIT 50
            """, $"%{cari.Trim()}%", konteks.KodeDepo);
        var hasil = new List<BarisValidasi>();
        foreach (var r in rows)
        {
            var baris = await BarisAsync(db, konteks, r.S("kode_brng"), r.S("nama_brng"), r.S("satuan"));
            if (baris.Stok > 0) hasil.Add(baris with { Tambahan = true });
        }
        return hasil;
    }

    /// <summary>Nilai satu baris seperti Khanza: embalase + tuslah + harga × jumlah, dibulatkan.</summary>
    public static double Total(double harga, double jumlah, double embalase, double tuslah) =>
        Bulat(embalase + tuslah + harga * jumlah);

    /// <summary>Memvalidasi resep dan mengembalikan total biaya obat yang dibebankan.</summary>
    public async Task<double> ValidasiAsync(string noResep, IReadOnlyList<InputValidasi> input)
    {
        PeriksaUbah();
        if (input.Any(i => !double.IsFinite(i.Jumlah + i.Embalase + i.Tuslah) || i.Jumlah < 0 || i.Embalase < 0 || i.Tuslah < 0))
            throw new ArgumentException("Jumlah, embalase dan tuslah harus berupa angka 0 atau lebih.");
        var diberikan = input.Where(i => i.Jumlah > 0).ToList();
        if (diberikan.Count == 0) throw new ArgumentException("Isi jumlah paling sedikit satu obat.");
        if (diberikan.Select(i => (i.Racik, Kode: i.Kode.ToUpperInvariant())).Distinct().Count() != diberikan.Count)
            throw new ArgumentException("Satu obat hanya boleh diisi sekali dalam kelompok yang sama.");

        await using var db = await Db.OpenAsync(settings);
        await IzinkanTanggalNolAsync(db);
        await KunciAsync(db, noResep, """
            SELECT kode_brng FROM resep_dokter WHERE no_resep=@p0
            UNION SELECT kode_brng FROM resep_dokter_racikan_detail WHERE no_resep=@p0
            """, diberikan.Select(i => i.Kode));

        var (rincian, konteks) = await MuatAsync(db, noResep);
        if (rincian.Halangan.Count > 0) throw new InvalidOperationException(string.Join(" ", rincian.Halangan));
        var resep = rincian.Resep;

        var pilihan = new List<(InputValidasi Isi, BarisValidasi Obat)>();
        foreach (var i in diberikan)
        {
            var obat = rincian.Baris.FirstOrDefault(x => x.Racik == i.Racik && x.Kode.Equals(i.Kode, StringComparison.OrdinalIgnoreCase));
            if (obat is null)
            {
                if (i.Racik.Length > 0 && rincian.Racikan.All(r => r.NoRacik != i.Racik))
                    throw new InvalidOperationException($"Racikan {i.Racik} tidak ada pada resep ini.");
                var barang = await db.FirstAsync("""
                    SELECT b.kode_brng,b.nama_brng,IFNULL(s.satuan,b.kode_sat) AS satuan
                    FROM databarang b LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
                    WHERE b.kode_brng=@p0 AND b.status='1'
                    """, i.Kode) ?? throw new InvalidOperationException($"Obat {i.Kode} tidak ditemukan atau tidak aktif.");
                var aturan = (i.Aturan ?? "").Trim();
                if (i.Racik.Length == 0 && aturan.Length == 0)
                    throw new InvalidOperationException($"Isi aturan pakai untuk obat tambahan {barang.S("nama_brng")}.");
                if (aturan.Length > 150) throw new InvalidOperationException("Aturan pakai paling banyak 150 karakter.");
                obat = await BarisAsync(db, konteks, barang.S("kode_brng"), barang.S("nama_brng"), barang.S("satuan"))
                    with { Aturan = aturan, Racik = i.Racik, Tambahan = true };
            }
            else
            {
                if (!obat.Aktif) throw new InvalidOperationException($"Obat {obat.Nama} tidak aktif, ganti dengan obat lain.");
                if (i.Jumlah > obat.JumlahResep + Toleransi)
                    throw new InvalidOperationException($"Jumlah {obat.Nama} melebihi resep ({obat.JumlahResep:0.##}).");
            }
            if (Math.Abs(obat.Harga - i.Harga) > Toleransi || obat.NoBatch != i.NoBatch || obat.NoFaktur != i.NoFaktur)
                throw new InvalidOperationException($"Harga atau batch {obat.Nama} berubah. Muat ulang resep lalu periksa lagi.");
            pilihan.Add((i, obat));
        }

        // Kunci utama detail_pemberian_obat tidak memuat racikan, jadi obat yang sama digabung dalam satu baris.
        var gabungan = pilihan
            .GroupBy(x => (x.Obat.Kode, x.Obat.NoBatch, x.Obat.NoFaktur))
            .Select(g => (g.First().Obat, Jumlah: g.Sum(x => x.Isi.Jumlah), Embalase: g.Sum(x => x.Isi.Embalase),
                Tuslah: g.Sum(x => x.Isi.Tuslah), Total: g.Sum(x => Total(x.Obat.Harga, x.Isi.Jumlah, x.Isi.Embalase, x.Isi.Tuslah))))
            .OrderBy(x => x.Obat.Kode, StringComparer.Ordinal)
            .ToList();
        foreach (var g in gabungan.Where(g => g.Jumlah > g.Obat.Stok + Toleransi))
            throw new InvalidOperationException($"Stok {g.Obat.Nama} di {rincian.NamaDepo} tidak cukup (sisa {g.Obat.Stok:0.##}).");

        var waktu = Convert.ToDateTime(await db.ScalarAsync("SELECT NOW()"));
        var petugas = user.KodeLegacy;
        var status = konteks.Ranap ? "Ranap" : "Ralan";
        double jual = 0, hpp = 0;
        foreach (var g in gabungan)
        {
            var b = g.Obat;
            await db.ExecAsync("""
                INSERT INTO detail_pemberian_obat(tgl_perawatan,jam,no_rawat,kode_brng,h_beli,biaya_obat,jml,embalase,tuslah,total,
                    status,kd_bangsal,no_batch,no_faktur)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p13,@p10,@p11,@p12)
                """, waktu.Date, waktu.TimeOfDay, resep.NoRawat, b.Kode, b.Hpp, b.Harga, g.Jumlah, g.Embalase, g.Tuslah, g.Total,
                konteks.KodeDepo, b.NoBatch, b.NoFaktur, status);

            var stok = Stok(b.Kode, b.Nama, b.NoBatch, b.NoFaktur);
            await FarmasiStokRepository.CatatAsync(db, stok, 0, g.Jumlah, "Pemberian Obat", petugas, konteks.KodeDepo,
                $"{resep.NoRawat} {resep.NoRm} {resep.Pasien}");
            if (settings.AktifkanBatchObat)
                await db.ExecAsync("UPDATE data_batch SET sisa=sisa-@p0 WHERE no_batch=@p1 AND kode_brng=@p2 AND no_faktur=@p3",
                    g.Jumlah, b.NoBatch, b.Kode, b.NoFaktur);
            await FarmasiStokRepository.TambahStokAsync(db, stok, konteks.KodeDepo, -g.Jumlah);

            jual += g.Total;
            hpp += Bulat(b.Hpp * g.Jumlah);
        }

        foreach (var (_, obat) in pilihan.Where(x => x.Obat.Racik.Length == 0 && x.Obat.Aturan.Length > 0))
            await db.ExecAsync("INSERT INTO aturan_pakai(tgl_perawatan,jam,no_rawat,kode_brng,aturan) VALUES(@p0,@p1,@p2,@p3,@p4)",
                waktu.Date, waktu.TimeOfDay, resep.NoRawat, obat.Kode, obat.Aturan);

        foreach (var r in rincian.Racikan)
        {
            var isi = pilihan.Where(x => x.Obat.Racik == r.NoRacik).ToList();
            if (isi.Count == 0) continue;
            await db.ExecAsync("""
                INSERT INTO obat_racikan(tgl_perawatan,jam,no_rawat,no_racik,nama_racik,kd_racik,jml_dr,aturan_pakai,keterangan)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)
                """, waktu.Date, waktu.TimeOfDay, resep.NoRawat, r.NoRacik, r.Nama, r.KodeMetode, r.Jumlah, r.Aturan, r.Keterangan);
            foreach (var (_, obat) in isi)
                await db.ExecAsync(
                    "INSERT INTO detail_obat_racikan(tgl_perawatan,jam,no_rawat,no_racik,kode_brng) VALUES(@p0,@p1,@p2,@p3,@p4)",
                    waktu.Date, waktu.TimeOfDay, resep.NoRawat, r.NoRacik, obat.Kode);
        }

        await db.ExecAsync("UPDATE resep_obat SET tgl_perawatan=@p1,jam=@p2 WHERE no_resep=@p0", noResep, waktu.Date, waktu.TimeOfDay);
        await JurnalAsync(db, resep, jual, hpp, false);
        await db.CommitAsync();
        return jual;
    }

    /// <summary>
    /// Membatalkan validasi seluruh resep: obat kembali ke stok, catatan pemberian dihapus, jurnal dibalik dan
    /// resep kembali menunggu. Seperti Khanza, lewat 48 jam hanya Admin Utama.
    /// </summary>
    public async Task BatalAsync(string noResep)
    {
        PeriksaUbah();
        await using var db = await Db.OpenAsync(settings);
        await IzinkanTanggalNolAsync(db);
        await KunciAsync(db, noResep, """
            SELECT d.kode_brng FROM resep_obat o
            JOIN detail_pemberian_obat d ON d.no_rawat=o.no_rawat AND d.tgl_perawatan=o.tgl_perawatan AND d.jam=o.jam
            WHERE o.no_resep=@p0
            """, []);

        var r = await db.FirstAsync($"{SqlResep} WHERE o.no_resep=@p0", noResep)
                ?? throw new InvalidOperationException("Resep sudah tidak ada. Muat ulang daftar.");
        var resep = Baca(r);
        if (resep.Divalidasi is not { } divalidasi) throw new InvalidOperationException("Resep belum divalidasi.");
        if (resep.Diserahkan is not null) throw new InvalidOperationException("Obat sudah diserahkan, validasi tidak dapat dibatalkan.");
        if (r.S("stts") == "Batal") throw new InvalidOperationException("Registrasi kunjungan ini batal.");
        if (await SudahBillingAsync(db, r))
            throw new InvalidOperationException("Billing sudah terverifikasi, hubungi bagian kasir.");
        var sekarang = Convert.ToDateTime(await db.ScalarAsync("SELECT NOW()"));
        if (!user.IsAdminUtama && sekarang - divalidasi > TimeSpan.FromHours(48))
            throw new InvalidOperationException("Validasi lebih dari 2 x 24 jam hanya dapat dibatalkan Admin Utama.");

        var tanggal = divalidasi.Date;
        var jam = divalidasi.TimeOfDay;
        if (await db.CountAsync("SELECT COUNT(*) FROM resep_obat WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam=@p2",
                resep.NoRawat, tanggal, jam) != 1)
            throw new InvalidOperationException("Ada resep lain pada waktu validasi yang sama. Batalkan lewat Data Pemberian Obat Khanza.");

        var baris = await db.QueryAsync("""
            SELECT d.kode_brng,d.jml,d.h_beli,d.total,d.kd_bangsal,d.no_batch,d.no_faktur,d.status,IFNULL(b.nama_brng,'') AS nama_brng
            FROM detail_pemberian_obat d LEFT JOIN databarang b ON b.kode_brng=d.kode_brng
            WHERE d.no_rawat=@p0 AND d.tgl_perawatan=@p1 AND d.jam=@p2 ORDER BY d.kode_brng FOR UPDATE
            """, resep.NoRawat, tanggal, jam);
        // Tanpa catatan pada waktu itu, stok yang sudah dipotong tidak dapat ditelusuri; jangan buka ulang resepnya.
        if (baris.Count == 0)
            throw new InvalidOperationException("Tidak ada catatan pemberian obat pada waktu validasi ini. Periksa lewat Khanza.");
        var status = resep.Ranap ? "Ranap" : "Ralan";
        if (baris.Any(b => b.S("status") != status))
            throw new InvalidOperationException("Jenis pemberian obat tidak sama dengan resepnya. Periksa lewat Khanza.");
        foreach (var b in baris)
            if (await db.CountAsync("SELECT COUNT(*) FROM returpasien WHERE no_rawat=@p0 AND kode_brng=@p1",
                    resep.NoRawat, b.S("kode_brng")) > 0)
                throw new InvalidOperationException($"{b.S("nama_brng")} sudah punya retur obat pada kunjungan ini. Selesaikan lewat Khanza.");

        var petugas = user.KodeLegacy;
        double jual = 0, hpp = 0;
        foreach (var b in baris)
        {
            var jumlah = b.D("jml");
            var stok = Stok(b.S("kode_brng"), b.S("nama_brng"), b.S("no_batch"), b.S("no_faktur"));
            await FarmasiStokRepository.CatatAsync(db, stok, jumlah, 0, "Pemberian Obat", petugas, b.S("kd_bangsal"),
                $"{resep.NoRawat} {resep.NoRm} {resep.Pasien}", "Hapus");
            if (settings.AktifkanBatchObat)
                await db.ExecAsync("UPDATE data_batch SET sisa=sisa+@p0 WHERE no_batch=@p1 AND kode_brng=@p2 AND no_faktur=@p3",
                    jumlah, stok.NoBatch, stok.Kode, stok.NoFaktur);
            await FarmasiStokRepository.TambahStokAsync(db, stok, b.S("kd_bangsal"), jumlah);
            await db.ExecAsync("DELETE FROM aturan_pakai WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam=@p2 AND kode_brng=@p3",
                resep.NoRawat, tanggal, jam, stok.Kode);
            jual += b.D("total");
            hpp += Bulat(b.D("h_beli") * jumlah);
        }

        foreach (var tabel in new[] { "detail_obat_racikan", "obat_racikan", "detail_pemberian_obat" })
            await db.ExecAsync($"DELETE FROM {tabel} WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam=@p2", resep.NoRawat, tanggal, jam);
        await db.ExecAsync("UPDATE resep_obat SET tgl_perawatan='0000-00-00',jam='00:00:00' WHERE no_resep=@p0", noResep);
        await JurnalAsync(db, resep, jual, hpp, true);
        await db.CommitAsync();
    }

    /// <summary>Menandai resep rawat jalan tervalidasi sudah diterima pasien.</summary>
    public async Task SerahkanAsync(string noResep)
    {
        PeriksaUbah();
        await using var db = await Db.OpenAsync(settings);
        await IzinkanTanggalNolAsync(db);
        var berubah = await db.ExecAsync("""
            UPDATE resep_obat SET tgl_penyerahan=CURRENT_DATE(),jam_penyerahan=CURRENT_TIME()
            WHERE no_resep=@p0 AND status='ralan' AND tgl_perawatan>'0000-00-00' AND tgl_penyerahan<='0000-00-00'
            """, noResep);
        if (berubah == 1) return;
        var r = await db.FirstAsync($"{SqlResep} WHERE o.no_resep=@p0", noResep)
                ?? throw new InvalidOperationException("Resep sudah tidak ada. Muat ulang daftar.");
        var resep = Baca(r);
        throw new InvalidOperationException(
            resep.Ranap ? "Penyerahan resep hanya untuk rawat jalan."
            : resep.Diserahkan is not null ? "Resep ini sudah diserahkan."
            : "Resep belum divalidasi.");
    }

    /// <summary>
    /// Memulai transaksi dengan urutan kunci yang sama dengan Kasir: kunjungan, resep, lalu stok menurut kode barang.
    /// </summary>
    private static async Task KunciAsync(Db db, string noResep, string sqlKode, IEnumerable<string> tambahan)
    {
        // Dibaca sebelum transaksi: pembacaan tanpa kunci di dalam transaksi (REPEATABLE READ) membekukan
        // potret data, sehingga perubahan klien lain selama menunggu kunci tidak akan terlihat.
        var noRawat = await db.StrAsync("SELECT no_rawat FROM resep_obat WHERE no_resep=@p0", noResep);
        var kodeStok = (await db.QueryAsync(sqlKode, noResep)).Select(r => r.S("kode_brng"))
            .Concat(tambahan).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();

        await db.BeginAsync();
        await db.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", noRawat);
        await db.FirstAsync("SELECT no_resep FROM resep_obat WHERE no_resep=@p0 FOR UPDATE", noResep);
        foreach (var kode in kodeStok)
            await db.QueryAsync("SELECT stok FROM gudangbarang WHERE kode_brng=@p0 FOR UPDATE", kode);
    }

    private static Task<bool> SudahBillingAsync(Db db, Row r) =>
        TagihanKunjungan.SudahDitagihAsync(db, r.S("no_rawat"));

    private async Task JurnalAsync(Db db, ResepFarmasi resep, double jual, double hpp, bool batal)
    {
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, resep.Ranap ? "set_akun_ranap" : "set_akun_ralan");
        string A(string kolom) => akun.GetValueOrDefault(kolom, "");
        var sfx = resep.Ranap ? "Ranap" : "Ralan";
        var rawat = resep.Ranap ? "Rawat_Inap" : "Rawat_Jalan";
        var nama = resep.Ranap ? "Rawat Inap" : "Rawat Jalan";
        var baris = new List<JurnalLine>();
        // Pembatalan menukar sisi debet dan kredit.
        void Pasang(double nilai, string debet, string namaDebet, string kredit, string namaKredit)
        {
            if (nilai <= 0) return;
            baris.Add(new(A(debet), namaDebet, batal ? 0 : nilai, batal ? nilai : 0));
            baris.Add(new(A(kredit), namaKredit, batal ? nilai : 0, batal ? 0 : nilai));
        }
        Pasang(jual, $"Suspen_Piutang_Obat_{sfx}", $"Suspen Piutang Obat {sfx}", $"Obat_{sfx}", $"Pendapatan Obat {nama}");
        Pasang(hpp, $"HPP_Obat_{rawat}", $"HPP Persediaan Obat {nama}", $"Persediaan_Obat_{rawat}", $"Persediaan Obat {nama}");
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, resep.NoRawat,
            batal
                ? $"PEMBATALAN PEMBERIAN OBAT {nama.ToUpperInvariant()} PASIEN {resep.NoRm} {resep.Pasien} OLEH {user.KodeLegacy}"
                : $"PEMBERIAN OBAT {nama.ToUpperInvariant()} PASIEN {resep.NoRm} {resep.Pasien}, DIPOSTING OLEH {user.KodeLegacy}",
            baris);
    }

    private async Task<(RincianResep Rincian, Konteks Konteks)> MuatAsync(Db db, string noResep)
    {
        var r = await db.FirstAsync($"{SqlResep} WHERE o.no_resep=@p0", noResep)
                ?? throw new InvalidOperationException("Resep sudah tidak ada. Muat ulang daftar.");
        var resep = Baca(r);
        var halangan = new List<string>();
        if (resep.Divalidasi is not null || resep.Diserahkan is not null) halangan.Add("Resep ini sudah divalidasi.");
        if (r.S("stts") == "Batal") halangan.Add("Registrasi kunjungan ini batal.");
        if (await SudahBillingAsync(db, r)) halangan.Add("Billing sudah terverifikasi, hubungi bagian kasir.");

        var konteks = await KonteksAsync(db, resep, r, halangan);
        var namaDepo = await db.StrAsync("SELECT nm_bangsal FROM bangsal WHERE kd_bangsal=@p0", konteks.KodeDepo);
        var tambahan = await db.FirstAsync("SELECT embalase_per_obat,tuslah_per_obat FROM set_embalase LIMIT 1");

        var racikan = (await db.QueryAsync("""
                SELECT x.no_racik,x.nama_racik,x.kd_racik,IFNULL(m.nm_racik,x.kd_racik) AS metode,x.jml_dr,x.aturan_pakai,x.keterangan
                FROM resep_dokter_racikan x LEFT JOIN metode_racik m ON m.kd_racik=x.kd_racik
                WHERE x.no_resep=@p0 ORDER BY x.no_racik
                """, noResep))
            .Select(x => new RacikanResep(x.S("no_racik"), x.S("nama_racik"), x.S("kd_racik"), x.S("metode"),
                (int)x.D("jml_dr"), x.S("aturan_pakai"), x.S("keterangan"))).ToList();

        var obat = await db.QueryAsync("""
            SELECT x.racik,x.kode_brng,x.jml,x.aturan_pakai,b.nama_brng,b.status,IFNULL(s.satuan,b.kode_sat) AS satuan
            FROM (
                SELECT '' AS racik,kode_brng,IFNULL(jml,0) AS jml,IFNULL(aturan_pakai,'') AS aturan_pakai
                FROM resep_dokter WHERE no_resep=@p0
                UNION ALL
                SELECT no_racik,kode_brng,IFNULL(jml,0),'' FROM resep_dokter_racikan_detail WHERE no_resep=@p0
            ) x
            JOIN databarang b ON b.kode_brng=x.kode_brng
            LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            ORDER BY x.racik,b.nama_brng,x.kode_brng
            """, noResep);
        if (obat.Count == 0) halangan.Add("Resep tidak berisi obat.");
        if (obat.Select(o => (o.S("racik"), o.S("kode_brng"))).Distinct().Count() != obat.Count)
            halangan.Add("Resep memuat obat yang sama lebih dari sekali, validasi lewat Khanza.");
        if (obat.Any(o => o.S("racik").Length > 0 && racikan.All(x => x.NoRacik != o.S("racik"))))
            halangan.Add("Data racikan pada resep tidak lengkap.");

        var baris = new List<BarisValidasi>();
        foreach (var o in obat)
            baris.Add(await BarisAsync(db, konteks, o.S("kode_brng"), o.S("nama_brng"), o.S("satuan")) with
            {
                JumlahResep = o.D("jml"), Aturan = o.S("aturan_pakai"), Racik = o.S("racik"), Aktif = o.S("status") == "1",
            });

        IReadOnlyList<ObatDiberikan> diberikan = [];
        if (resep.Divalidasi is { } waktu)
            diberikan = (await db.QueryAsync("""
                    SELECT d.kode_brng,IFNULL(b.nama_brng,d.kode_brng) AS nama_brng,IFNULL(s.satuan,b.kode_sat) AS satuan,
                           d.jml,d.biaya_obat,d.embalase,d.tuslah,d.total,d.no_batch,IFNULL(a.aturan,'') AS aturan
                    FROM detail_pemberian_obat d
                    LEFT JOIN databarang b ON b.kode_brng=d.kode_brng
                    LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
                    LEFT JOIN aturan_pakai a ON a.no_rawat=d.no_rawat AND a.tgl_perawatan=d.tgl_perawatan AND a.jam=d.jam
                         AND a.kode_brng=d.kode_brng
                    WHERE d.no_rawat=@p0 AND d.tgl_perawatan=@p1 AND d.jam=@p2 ORDER BY b.nama_brng,d.kode_brng
                    """, resep.NoRawat, waktu.Date, waktu.TimeOfDay))
                .Select(x => new ObatDiberikan(x.S("kode_brng"), x.S("nama_brng"), x.S("satuan"), x.D("jml"), x.D("biaya_obat"),
                    x.D("embalase"), x.D("tuslah"), x.D("total"), x.S("no_batch"), x.S("aturan"))).ToList();

        var rincian = new RincianResep(resep, konteks.KodeDepo, namaDepo.Length > 0 ? namaDepo : konteks.KodeDepo,
            tambahan?.D("embalase_per_obat") ?? 0, tambahan?.D("tuslah_per_obat") ?? 0, baris, halangan)
        {
            Kelas = konteks.Kelas, Racikan = racikan, Diberikan = diberikan,
        };
        return (rincian, konteks);
    }

    /// <summary>Depo dan dasar harga: poli untuk rawat jalan, kamar terakhir dan kelasnya untuk rawat inap.</summary>
    private static async Task<Konteks> KonteksAsync(Db db, ResepFarmasi resep, Row r, List<string> halangan)
    {
        var lokasi = await db.FirstAsync("SELECT kd_bangsal,asal_stok FROM set_lokasi LIMIT 1");
        string depo, kelas = "", kolom = "ralan";
        object? naik;
        if (!resep.Ranap)
        {
            depo = await db.StrAsync("SELECT kd_bangsal FROM set_depo_ralan WHERE kd_poli=@p0", r.S("kd_poli"));
            if (depo.Length == 0) depo = lokasi?.S("kd_bangsal") ?? "";
            naik = await db.ScalarAsync("SELECT hargajual/100 FROM set_harga_obat_ralan WHERE kd_pj=@p0", r.S("kd_pj"));
        }
        else
        {
            var kamar = await db.FirstAsync($"""
                SELECT k.kd_bangsal,k.kelas FROM kamar_inap i JOIN kamar k ON k.kd_kamar=i.kd_kamar
                WHERE i.no_rawat={RawatGabungService.PemilikKamar("@p0")} ORDER BY (i.stts_pulang='-') DESC,i.tgl_masuk DESC,i.jam_masuk DESC LIMIT 1
                """, resep.NoRawat);
            if (kamar is null)
            {
                halangan.Add("Pasien belum punya kamar rawat inap.");
                return new(true, "", "", 0, "kelas1");
            }
            kelas = kamar.S("kelas");
            kolom = kelas switch
            {
                "Kelas 1" => "kelas1", "Kelas 2" => "kelas2", "Kelas 3" => "kelas3",
                "Kelas Utama" => "utama", "Kelas VIP" => "vip", "Kelas VVIP" => "vvip",
                _ => "",
            };
            if (kolom.Length == 0)
            {
                halangan.Add($"Kelas kamar '{kelas}' tidak dikenali, harga obat tidak dapat ditentukan.");
                kolom = "kelas1";
            }
            var bangsal = kamar.S("kd_bangsal");
            depo = await db.StrAsync("SELECT kd_depo FROM set_depo_ranap WHERE kd_bangsal=@p0 ORDER BY kd_depo LIMIT 1", bangsal);
            if (depo.Length == 0)
                depo = lokasi?.S("asal_stok") == "Gunakan Stok Bangsal" ? bangsal : lokasi?.S("kd_bangsal") ?? "";
            naik = await db.ScalarAsync("SELECT hargajual/100 FROM set_harga_obat_ranap WHERE kd_pj=@p0 AND kelas=@p1",
                r.S("kd_pj"), kelas);
        }
        if (depo.Length == 0) halangan.Add("Depo obat belum diatur.");
        return new(resep.Ranap, depo, kelas, Convert.ToDouble(naik ?? 0d), kolom);
    }

    /// <summary>Harga, HPP, batch (kadaluarsa terdekat) dan stok satu obat di depo resep.</summary>
    private async Task<BarisValidasi> BarisAsync(Db db, Konteks k, string kode, string nama, string satuan)
    {
        // Nama kolom berasal dari daftar tetap (KolomHarga dan HppColumn dibatasi), bukan dari masukan pengguna.
        var tabel = settings.AktifkanBatchObat ? "x" : "b";
        var harga = k.Kenaikan > 0 ? $"({tabel}.h_beli+({tabel}.h_beli*@p2))" : $"{tabel}.{k.KolomHarga}";
        var sql = settings.AktifkanBatchObat
            ? $"""
              SELECT {harga} AS harga,x.{settings.HppColumn} AS hpp,g.stok,x.no_batch,x.no_faktur
              FROM data_batch x
              JOIN gudangbarang g ON g.kode_brng=x.kode_brng AND g.no_batch=x.no_batch AND g.no_faktur=x.no_faktur
              WHERE x.kode_brng=@p0 AND g.kd_bangsal=@p1 AND g.stok>0
              ORDER BY x.tgl_kadaluarsa,g.stok DESC LIMIT 1
              """
            : $"""
              SELECT {harga} AS harga,b.{settings.HppColumn} AS hpp,IFNULL(g.stok,0) AS stok,'' AS no_batch,'' AS no_faktur
              FROM databarang b
              LEFT JOIN gudangbarang g ON g.kode_brng=b.kode_brng AND g.kd_bangsal=@p1 AND g.no_batch='' AND g.no_faktur=''
              WHERE b.kode_brng=@p0
              """;
        var s = await db.FirstAsync(sql, kode, k.KodeDepo, k.Kenaikan);
        return new BarisValidasi(kode, nama, satuan, 0, "", BulatHarga(s?.D("harga") ?? 0), s?.D("hpp") ?? 0,
            Math.Max(0, s?.D("stok") ?? 0), s?.S("no_batch") ?? "", s?.S("no_faktur") ?? "");
    }

    private static StokItem Stok(string kode, string nama, string batch, string faktur) =>
        new(kode, nama, "", "", batch, faktur, 0, 0, null);

    /// <summary>resep_obat memakai tanggal nol untuk resep yang belum diproses.</summary>
    private static Task<int> IzinkanTanggalNolAsync(Db db) => db.ExecAsync("""
        SET SESSION sql_mode=TRIM(BOTH ',' FROM REPLACE(REPLACE(CONCAT(',',@@SESSION.sql_mode,','),
            ',NO_ZERO_DATE,',','),',NO_ZERO_IN_DATE,',','))
        """);

    /// <summary>Math.round Java: setengah dibulatkan ke atas.</summary>
    private static double Bulat(double nilai) => Math.Floor(nilai + 0.5);

    /// <summary>Valid.roundUp Khanza: ke atas per Rp100 bila PEMBULATANHARGAOBAT aktif.</summary>
    private double BulatHarga(double harga) =>
        settings.PembulatanHargaObat ? Math.Ceiling(harga / 100 - Toleransi) * 100 : Bulat(harga);

    private static ResepFarmasi Baca(Row r) => new(r.S("no_resep"), r.S("no_rawat"), r.S("no_rkm_medis"), r.S("nm_pasien"),
        r.S("nm_dokter"), r.S("nm_poli"), r.S("png_jawab"), r.S("status") == "ranap",
        r.Dt("diresepkan"), r.Dt("divalidasi"), r.Dt("diserahkan"));

    private void PeriksaLihat()
    {
        if (!user.HasPermission(Izin) && !user.HasPermission("resep_dokter"))
            throw new InvalidOperationException("Hak akses Resep Dokter atau Pemberian Obat diperlukan.");
    }

    private void PeriksaUbah()
    {
        if (!BolehValidasi) throw new InvalidOperationException("Hak akses Pemberian Obat diperlukan.");
    }
}
