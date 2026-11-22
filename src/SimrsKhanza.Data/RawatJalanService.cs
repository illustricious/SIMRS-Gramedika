using SimrsKhanza.Config;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data;

/// <summary>Pengguna yang menyimpan: <paramref name="Kode"/> = "Admin Utama" atau NIK pegawai (akses.getkode()).</summary>
public record RawatJalanAkses(bool IsAdminUtama, string Kode);

public record RawatJalanKunjungan(
    string NoRawat, string NoRekamMedis, string Pasien, DateTime? Tanggal, string Jam, string Poli, string Dokter, long JumlahPemeriksaan);

public record PegawaiPilihan(string Nik, string Nama)
{
    public override string ToString() => $"{Nik} - {Nama}";
}

/// <summary>Satu baris pemeriksaan_ralan (SOAP dan tanda vital). Nilai kosong disimpan sebagai string kosong seperti Java.</summary>
public record PemeriksaanRalanInput(
    string NoRawat, DateTime Tanggal, TimeSpan Jam, string Suhu, string Tensi, string Nadi, string Respirasi, string Tinggi, string Berat,
    string Spo2, string Gcs, string Kesadaran, string Keluhan, string Pemeriksaan, string Alergi, string LingkarPerut,
    string Rtl, string Penilaian, string Instruksi, string Evaluasi, string Nip);

/// <summary>
/// Pemeriksaan rawat jalan dari DlgRawatJalan.java tab Pemeriksaan: antrian kunjungan Ralan, banyak SOAP per kunjungan,
/// tambah/ubah/hapus dengan aturan akses Khanza (non-Admin Utama hanya miliknya sendiri, ubah/hapus 48 jam,
/// waktu input tidak boleh sebelum waktu registrasi).
/// </summary>
public class RawatJalanService(DatabaseSettings settings)
{
    public const string KodePoliIgd = "IGDK";

    public static readonly string[] Kesadaran =
    [
        "Compos Mentis", "Somnolence", "Sopor", "Coma", "Alert", "Confusion", "Voice", "Pain", "Unresponsive", "Apatis", "Delirium", "Meninggal",
    ];

    /// <summary>Java menyimpan bila minimal satu isian klinis terisi.</summary>
    public static bool AdaIsi(PemeriksaanRalanInput x) =>
        new[] { x.Keluhan, x.Pemeriksaan, x.Suhu, x.Tensi, x.Alergi, x.Tinggi, x.Berat, x.Respirasi, x.Nadi, x.Gcs, x.Rtl, x.Penilaian, x.Instruksi, x.Spo2, x.Evaluasi }
            .Any(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Non-Admin Utama hanya boleh menyimpan, mengubah, atau menghapus data atas NIK-nya sendiri.</summary>
    public static void PastikanMilikSendiri(RawatJalanAkses akses, string nip)
    {
        if (!akses.IsAdminUtama && akses.Kode != nip)
            throw new InvalidOperationException("Hanya bisa diproses oleh dokter/petugas yang bersangkutan..!!");
    }

    /// <summary>Sequel.cekTanggalRegistrasi: waktu input data minimal waktu registrasi (non-Admin Utama).</summary>
    public static void PastikanTidakSebelumRegistrasi(DateTime registrasi, DateTime input)
    {
        if (input < registrasi)
            throw new InvalidOperationException($"Maaf, jam input data / perubahan data minimal di jam {registrasi:yyyy-MM-dd HH:mm:ss} !");
    }

    private static PemeriksaanRalanInput Bersih(PemeriksaanRalanInput x) => x with
    {
        Suhu = x.Suhu.Trim(), Tensi = x.Tensi.Trim(), Nadi = x.Nadi.Trim(), Respirasi = x.Respirasi.Trim(), Tinggi = x.Tinggi.Trim(), Berat = x.Berat.Trim(),
        Spo2 = x.Spo2.Trim(), Gcs = x.Gcs.Trim(), Alergi = x.Alergi.Trim(), LingkarPerut = x.LingkarPerut.Trim(), Keluhan = x.Keluhan.Trim(),
        Pemeriksaan = x.Pemeriksaan.Trim(), Rtl = x.Rtl.Trim(), Penilaian = x.Penilaian.Trim(), Instruksi = x.Instruksi.Trim(), Evaluasi = x.Evaluasi.Trim(),
    };

    /// <summary>Antrian kunjungan rawat jalan (status_lanjut Ralan, bukan IGD) dalam rentang tanggal registrasi, termasuk yang sudah diperiksa.</summary>
    public async Task<List<RawatJalanKunjungan>> AntrianAsync(DateTime dari, DateTime sampai, string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT r.no_rawat,r.no_rkm_medis,p.nm_pasien,r.tgl_registrasi,r.jam_reg,pl.nm_poli,d.nm_dokter,
                   (SELECT COUNT(*) FROM pemeriksaan_ralan e WHERE e.no_rawat=r.no_rawat) AS jumlah
            FROM reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
                 LEFT JOIN poliklinik pl ON pl.kd_poli=r.kd_poli LEFT JOIN dokter d ON d.kd_dokter=r.kd_dokter
            WHERE r.status_lanjut='Ralan' AND r.kd_poli<>@p3 AND r.tgl_registrasi BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',r.no_rawat,r.no_reg,r.no_rkm_medis,p.nm_pasien,pl.nm_poli,d.nm_dokter) LIKE @p0
            ORDER BY r.tgl_registrasi DESC,r.jam_reg DESC,r.no_rawat DESC
            LIMIT 1000
            """, $"%{cari?.Trim()}%", dari.Date, sampai.Date, KodePoliIgd);
        return rows.Select(Kunjungan).ToList();
    }

    public async Task<RawatJalanKunjungan?> KunjunganAsync(string noRawat)
    {
        await using var db = await Db.OpenAsync(settings);
        var row = await db.FirstAsync("""
            SELECT r.no_rawat,r.no_rkm_medis,p.nm_pasien,r.tgl_registrasi,r.jam_reg,pl.nm_poli,d.nm_dokter,
                   (SELECT COUNT(*) FROM pemeriksaan_ralan e WHERE e.no_rawat=r.no_rawat) AS jumlah
            FROM reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
                 LEFT JOIN poliklinik pl ON pl.kd_poli=r.kd_poli LEFT JOIN dokter d ON d.kd_dokter=r.kd_dokter
            WHERE r.no_rawat=@p0 AND r.status_lanjut='Ralan'
            """, noRawat);
        return row is null ? null : Kunjungan(row);
    }

    private static RawatJalanKunjungan Kunjungan(Row r) => new(r.S("no_rawat"), r.S("no_rkm_medis"), r.S("nm_pasien"), r.Dt("tgl_registrasi"),
        r["jam_reg"] is TimeSpan t ? t.ToString(@"hh\:mm\:ss") : r.S("jam_reg"), r.S("nm_poli"), r.S("nm_dokter"), Convert.ToInt64(r["jumlah"] ?? 0));

    public async Task<List<Row>> PemeriksaanAsync(string noRawat)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT e.*,g.nama AS nama_pegawai FROM pemeriksaan_ralan e LEFT JOIN pegawai g ON g.nik=e.nip
            WHERE e.no_rawat=@p0 ORDER BY e.tgl_perawatan DESC,e.jam_rawat DESC
            """, noRawat);
    }

    public async Task<List<PegawaiPilihan>> PegawaiAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("SELECT nik,nama FROM pegawai WHERE stts_aktif<>'KELUAR' ORDER BY nama,nik");
        return rows.Select(r => new PegawaiPilihan(r.S("nik"), r.S("nama"))).ToList();
    }

    public async Task SimpanAsync(PemeriksaanRalanInput input, RawatJalanAkses akses, DateTime? sekarang = null)
    {
        input = Bersih(input);
        ValidasiDasar(input);
        PastikanMilikSendiri(akses, input.Nip);
        await using var db = await Db.OpenAsync(settings);
        var registrasi = await WaktuRegistrasiAsync(db, input.NoRawat);
        if (!akses.IsAdminUtama) PastikanTidakSebelumRegistrasi(registrasi, input.Tanggal.Date + input.Jam);
        await PastikanPegawaiAsync(db, input.Nip);
        await db.ExecAsync("""
            INSERT INTO pemeriksaan_ralan(no_rawat,tgl_perawatan,jam_rawat,suhu_tubuh,tensi,nadi,respirasi,tinggi,berat,spo2,gcs,kesadaran,keluhan,pemeriksaan,
                alergi,lingkar_perut,rtl,penilaian,instruksi,evaluasi,nip)
            VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16,@p17,@p18,@p19,@p20)
            """, Args(input));
    }

    /// <summary>Ubah satu baris (kunci lama: no_rawat + tanggal + jam). Non-Admin Utama: milik sendiri, 48 jam, tidak sebelum registrasi.</summary>
    public async Task UbahAsync(string noRawat, DateTime tanggalLama, TimeSpan jamLama, PemeriksaanRalanInput input, RawatJalanAkses akses, DateTime? sekarang = null)
    {
        input = Bersih(input);
        ValidasiDasar(input);
        await using var db = await Db.OpenAsync(settings);
        var lama = await db.FirstAsync("SELECT nip FROM pemeriksaan_ralan WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam_rawat=@p2",
            noRawat, tanggalLama.Date, jamLama.ToString(@"hh\:mm\:ss")) ?? throw new InvalidOperationException("Pemeriksaan tidak ditemukan lagi. Muat ulang daftar.");
        if (!akses.IsAdminUtama)
        {
            PastikanMilikSendiri(akses, lama.S("nip"));
            PastikanMilikSendiri(akses, input.Nip);
            if (RegistrasiService.MelewatiBatasEdit(tanggalLama.Date + jamLama, sekarang ?? DateTime.Now))
                throw new InvalidOperationException("Maaf, perubahan data / penghapusan data tidak boleh lebih dari 2 x 24 jam !");
            PastikanTidakSebelumRegistrasi(await WaktuRegistrasiAsync(db, noRawat), input.Tanggal.Date + input.Jam);
        }
        await PastikanPegawaiAsync(db, input.Nip);
        await db.ExecAsync("""
            UPDATE pemeriksaan_ralan SET tgl_perawatan=@p1,jam_rawat=@p2,suhu_tubuh=@p3,tensi=@p4,nadi=@p5,respirasi=@p6,tinggi=@p7,berat=@p8,spo2=@p9,gcs=@p10,
                kesadaran=@p11,keluhan=@p12,pemeriksaan=@p13,alergi=@p14,lingkar_perut=@p15,rtl=@p16,penilaian=@p17,instruksi=@p18,evaluasi=@p19,nip=@p20
            WHERE no_rawat=@p0 AND tgl_perawatan=@p21 AND jam_rawat=@p22
            """, [.. Args(input), tanggalLama.Date, jamLama.ToString(@"hh\:mm\:ss")]);
    }

    public async Task HapusAsync(string noRawat, DateTime tanggal, TimeSpan jam, RawatJalanAkses akses, DateTime? sekarang = null)
    {
        await using var db = await Db.OpenAsync(settings);
        var row = await db.FirstAsync("SELECT nip FROM pemeriksaan_ralan WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam_rawat=@p2",
            noRawat, tanggal.Date, jam.ToString(@"hh\:mm\:ss")) ?? throw new InvalidOperationException("Pemeriksaan tidak ditemukan lagi. Muat ulang daftar.");
        if (!akses.IsAdminUtama)
        {
            if (RegistrasiService.MelewatiBatasEdit(tanggal.Date + jam, sekarang ?? DateTime.Now))
                throw new InvalidOperationException("Maaf, perubahan data / penghapusan data tidak boleh lebih dari 2 x 24 jam !");
            PastikanMilikSendiri(akses, row.S("nip"));
        }
        await db.ExecAsync("DELETE FROM pemeriksaan_ralan WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam_rawat=@p2",
            noRawat, tanggal.Date, jam.ToString(@"hh\:mm\:ss"));
    }

    private static void ValidasiDasar(PemeriksaanRalanInput x)
    {
        if (string.IsNullOrWhiteSpace(x.NoRawat)) throw new ArgumentException("Pilih kunjungan pasien terlebih dahulu.");
        if (string.IsNullOrWhiteSpace(x.Nip)) throw new ArgumentException("Dokter/Paramedis masih kosong...!!");
        if (!Kesadaran.Contains(x.Kesadaran)) throw new ArgumentException("Pilih tingkat kesadaran.");
        if (!AdaIsi(x)) throw new ArgumentException("Isi minimal satu data pemeriksaan.");
    }

    private static object?[] Args(PemeriksaanRalanInput x) =>
    [
        x.NoRawat, x.Tanggal.Date, x.Jam.ToString(@"hh\:mm\:ss"), x.Suhu, x.Tensi, x.Nadi, x.Respirasi, x.Tinggi, x.Berat, x.Spo2, x.Gcs, x.Kesadaran,
        x.Keluhan, x.Pemeriksaan, x.Alergi, x.LingkarPerut, x.Rtl, x.Penilaian, x.Instruksi, x.Evaluasi, x.Nip,
    ];

    private static async Task<DateTime> WaktuRegistrasiAsync(Db db, string noRawat)
    {
        var row = await db.FirstAsync("SELECT tgl_registrasi,jam_reg FROM reg_periksa WHERE no_rawat=@p0", noRawat)
            ?? throw new InvalidOperationException("Kunjungan tidak ditemukan.");
        return (row.Dt("tgl_registrasi") ?? DateTime.Today).Date + (row["jam_reg"] is TimeSpan t ? t : TimeSpan.Zero);
    }

    private static async Task PastikanPegawaiAsync(Db db, string nip)
    {
        if (await db.CountAsync("SELECT COUNT(*) FROM pegawai WHERE nik=@p0", nip) == 0)
            throw new InvalidOperationException($"Pegawai dengan NIK {nip} tidak ditemukan di data pegawai.");
    }
}
