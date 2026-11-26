using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>Hak akses pengguna untuk registrasi (legacy akses.getkode()=="Admin Utama" dan akses.getedit_registrasi()).</summary>
public record RegistrasiAkses(bool IsAdminUtama, bool BolehEdit);

public record RegistrasiBaru(
    string NoRekamMedis, string KodeDokter, string KodePoli, string KodePenjab,
    string PenanggungJawab, string AlamatPj, string HubunganPj, DateTime Waktu);

public record RegistrasiPasien(
    string NoRekamMedis, string Nama, string Alamat, string NamaKeluarga, string Keluarga, string KodePenjab, string StatusDaftar, string Umur);

/// <summary>
/// Aturan registrasi rawat jalan dari DlgReg.java: pendaftaran baru (BtnSimpan/isRegistrasi), ubah (BtnEdit/ganti)
/// dan hapus (BtnHapus). Memakai SQL langsung agar nomor rawat/reg, status Baru/Lama, biaya, dan umur
/// dihitung seperti Khanza.
/// </summary>
public class RegistrasiService(DatabaseSettings settings)
{
    public const int BatasEditJam = 48;

    /// <summary>Sequel.cekTanggal48jam: perubahan/penghapusan lewat 2x24 jam ditolak untuk non-Admin Utama.</summary>
    public static bool MelewatiBatasEdit(DateTime registrasi, DateTime sekarang) => (sekarang - registrasi).TotalSeconds > BatasEditJam * 3600;

    /// <summary>Umur saat pendaftaran seperti isCekPasien: tahun (Th), jika 0 bulan (Bl), jika 0 hari (Hr).</summary>
    public static (int Nilai, string Satuan) HitungUmur(DateOnly lahir, DateOnly hariIni)
    {
        var tahun = hariIni.Year - lahir.Year;
        if (hariIni < lahir.AddYears(tahun)) tahun--;
        if (tahun > 0) return (tahun, "Th");
        var bulan = (hariIni.Year - lahir.Year) * 12 + hariIni.Month - lahir.Month;
        if (hariIni < lahir.AddMonths(bulan)) bulan--;
        if (bulan > 0) return (bulan, "Bl");
        return (Math.Max(0, hariIni.DayNumber - lahir.DayNumber), "Hr");
    }

    /// <summary>Biaya registrasi: pasien daftar hari ini ("Baru") memakai poliklinik.registrasi, selain itu registrasilama.</summary>
    public static string KolomBiaya(string statusDaftar) => statusDaftar == "Lama" ? "registrasilama" : "registrasi";

    public static string HariJadwal(DateTime tanggal) => tanggal.DayOfWeek switch
    {
        DayOfWeek.Monday => "SENIN",
        DayOfWeek.Tuesday => "SELASA",
        DayOfWeek.Wednesday => "RABU",
        DayOfWeek.Thursday => "KAMIS",
        DayOfWeek.Friday => "JUMAT",
        DayOfWeek.Saturday => "SABTU",
        _ => "AKHAD",
    };

    public async Task<RegistrasiPasien?> CariPasienAsync(string noRekamMedis, DateTime? hariIni = null)
    {
        if (string.IsNullOrWhiteSpace(noRekamMedis)) return null;
        var today = (hariIni ?? DateTime.Today).Date;
        await using var db = await Db.OpenAsync(settings);
        var row = await db.FirstAsync("""
            SELECT p.no_rkm_medis,p.nm_pasien,CONCAT_WS(', ',p.alamat,kl.nm_kel,kc.nm_kec,kb.nm_kab) AS asal,p.namakeluarga,p.keluarga,p.kd_pj,
                   p.tgl_daftar,p.tgl_lahir
            FROM pasien p LEFT JOIN kelurahan kl ON kl.kd_kel=p.kd_kel LEFT JOIN kecamatan kc ON kc.kd_kec=p.kd_kec
                 LEFT JOIN kabupaten kb ON kb.kd_kab=p.kd_kab
            WHERE p.no_rkm_medis=@p0
            """, noRekamMedis.Trim());
        if (row is null) return null;
        var daftar = row.Dt("tgl_daftar")?.Date == today ? "Baru" : "Lama";
        var umur = row.Dt("tgl_lahir") is { } lahir ? HitungUmur(DateOnly.FromDateTime(lahir), DateOnly.FromDateTime(today)) : (0, "Th");
        return new(row.S("no_rkm_medis"), row.S("nm_pasien"), row.S("asal"), row.S("namakeluarga"), row.S("keluarga"), row.S("kd_pj"),
            daftar, $"{umur.Item1} {umur.Item2}");
    }

    /// <summary>Registrasi baru. Menolak data kosong, pasien yang sedang dirawat inap, dan (bila diaktifkan) kuota dokter penuh.</summary>
    public async Task<string> DaftarAsync(RegistrasiBaru baru, RegistrasiAkses akses, bool terapkanKuota = false,
        RujukanMasukData? rujukan = null, AuthenticatedUser? pengguna = null)
    {
        if (string.IsNullOrWhiteSpace(baru.NoRekamMedis)) throw new ArgumentException("Pilih pasien terlebih dahulu.");
        if (string.IsNullOrWhiteSpace(baru.KodeDokter)) throw new ArgumentException("Pilih dokter.");
        if (string.IsNullOrWhiteSpace(baru.KodePoli)) throw new ArgumentException("Pilih poliklinik.");
        if (string.IsNullOrWhiteSpace(baru.KodePenjab)) throw new ArgumentException("Pilih jenis bayar.");
        if (rujukan is not null)
        {
            if (!RujukanMasukService.BisaMengelola(pengguna))
                throw new InvalidOperationException("Akun tidak memiliki hak mengelola rujukan masuk.");
            RujukanMasukService.Validasi(rujukan);
        }

        var tanggal = baru.Waktu.Date;
        await using var db = await Db.OpenAsync(settings);
        var pasien = await db.FirstAsync("SELECT tgl_daftar,tgl_lahir FROM pasien WHERE no_rkm_medis=@p0", baru.NoRekamMedis)
            ?? throw new InvalidOperationException($"Pasien {baru.NoRekamMedis} tidak ditemukan.");

        if (await db.CountAsync("""
                SELECT COUNT(p.no_rkm_medis) FROM pasien p INNER JOIN reg_periksa r ON r.no_rkm_medis=p.no_rkm_medis
                INNER JOIN kamar_inap k ON k.no_rawat=r.no_rawat WHERE k.stts_pulang='-' AND p.no_rkm_medis=@p0
                """, baru.NoRekamMedis) > 0)
            throw new InvalidOperationException("Pasien sedang dalam masa perawatan di kamar inap..!!");

        if (await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0", baru.KodeDokter) == 0)
            throw new InvalidOperationException($"Dokter {baru.KodeDokter} tidak ditemukan.");
        if (await db.CountAsync("SELECT COUNT(*) FROM penjab WHERE kd_pj=@p0", baru.KodePenjab) == 0)
            throw new InvalidOperationException($"Jenis bayar {baru.KodePenjab} tidak ditemukan.");
        var poli = await db.FirstAsync("SELECT registrasi,registrasilama FROM poliklinik WHERE kd_poli=@p0", baru.KodePoli)
            ?? throw new InvalidOperationException($"Poliklinik {baru.KodePoli} tidak ditemukan.");

        if (terapkanKuota && !akses.IsAdminUtama)
        {
            var kuota = await db.ScalarAsync(
                "SELECT MAX(kuota) FROM jadwal WHERE kd_dokter=@p0 AND kd_poli=@p1 AND hari_kerja=@p2",
                baru.KodeDokter, baru.KodePoli, HariJadwal(tanggal));
            if (kuota is not null && await db.CountAsync(
                    "SELECT COUNT(no_rawat) FROM reg_periksa WHERE kd_dokter=@p0 AND tgl_registrasi=@p1", baru.KodeDokter, tanggal) >= Convert.ToInt64(kuota))
                throw new InvalidOperationException("Eiiits, Kuota registrasi penuh..!!!");
        }

        var statusDaftar = pasien.Dt("tgl_daftar")?.Date == tanggal ? "Baru" : "Lama";
        var statusPoli = await db.CountAsync("SELECT COUNT(no_rawat) FROM reg_periksa WHERE no_rkm_medis=@p0 AND kd_poli=@p1",
            baru.NoRekamMedis, baru.KodePoli) > 0 ? "Lama" : "Baru";
        var biaya = poli.D(KolomBiaya(statusDaftar));
        var (umur, satuan) = pasien.Dt("tgl_lahir") is { } lahir
            ? HitungUmur(DateOnly.FromDateTime(lahir), DateOnly.FromDateTime(tanggal)) : (0, "Th");

        for (var percobaan = 0; ; percobaan++)
        {
            var noReg = (await db.CountAsync(
                "SELECT IFNULL(MAX(CONVERT(no_reg,SIGNED)),0) FROM reg_periksa WHERE kd_dokter=@p0 AND tgl_registrasi=@p1",
                baru.KodeDokter, tanggal) + 1).ToString("000");
            var noRawat = await db.AutoNomorAsync(
                "SELECT IFNULL(MAX(CONVERT(RIGHT(no_rawat,6),SIGNED)),0) FROM reg_periksa WHERE tgl_registrasi=@p0",
                $"{tanggal:yyyy/MM/dd}/", 6, tanggal);
            try
            {
                if (rujukan is not null) await db.BeginAsync();
                await db.ExecAsync("""
                    INSERT INTO reg_periksa(no_reg,no_rawat,tgl_registrasi,jam_reg,kd_dokter,no_rkm_medis,kd_poli,p_jawab,almt_pj,hubunganpj,
                        biaya_reg,stts,stts_daftar,status_lanjut,kd_pj,umurdaftar,sttsumur,status_bayar,status_poli)
                    VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,'Belum',@p11,'Ralan',@p12,@p13,@p14,'Belum Bayar',@p15)
                    """, noReg, noRawat, tanggal, baru.Waktu.ToString("HH:mm:ss"), baru.KodeDokter, baru.NoRekamMedis, baru.KodePoli,
                    baru.PenanggungJawab.Trim(), baru.AlamatPj.Trim(), baru.HubunganPj.Trim(), biaya, statusDaftar, baru.KodePenjab, umur, satuan, statusPoli);
                if (rujukan is not null)
                {
                    await RujukanMasukService.TulisAsync(db, noRawat, rujukan, ubah: false);
                    await db.CommitAsync();
                }
                return noRawat;
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry && percobaan < 4)
            {
                // Dua loket mendaftar bersamaan: hitung ulang nomor seperti loop isNumber() di Java.
                await db.DisposeTxAsync();
            }
        }
    }

    /// <summary>Ubah registrasi (ganti()). Ditolak bila sudah ada billing atau batal; non-Admin Utama perlu hak edit dan paling lama 2x24 jam.</summary>
    public async Task UbahAsync(string noRawat, string noRekamMedis, string kodeDokter, string kodePoli, string kodePenjab,
        string penanggungJawab, string alamatPj, string hubunganPj, RegistrasiAkses akses, DateTime? sekarang = null)
    {
        await using var db = await Db.OpenAsync(settings);
        var row = await AmbilAsync(db, noRawat);
        if (row.S("no_rkm_medis") != noRekamMedis)
            throw new InvalidOperationException("Data pasien berubah. Muat ulang daftar sebelum menyimpan.");
        await PastikanBolehUbahAsync(db, noRawat, row, akses, sekarang ?? DateTime.Now);
        if (!akses.IsAdminUtama && !akses.BolehEdit)
            throw new InvalidOperationException("Maaf hak akses anda dibatasi, silahkan konfirmasi kepada admin..!!!");

        var poliBerubah = row.S("kd_poli") != kodePoli;
        var biaya = row.D("biaya_reg");
        if (poliBerubah)
        {
            var poli = await db.FirstAsync("SELECT registrasi,registrasilama FROM poliklinik WHERE kd_poli=@p0", kodePoli)
                ?? throw new InvalidOperationException($"Poliklinik {kodePoli} tidak ditemukan.");
            biaya = poli.D(KolomBiaya(row.S("stts_daftar")));
        }

        await db.ExecAsync("""
            UPDATE reg_periksa SET kd_dokter=@p0,kd_poli=@p1,kd_pj=@p2,p_jawab=@p3,almt_pj=@p4,hubunganpj=@p5,biaya_reg=@p6 WHERE no_rawat=@p7
            """, kodeDokter, kodePoli, kodePenjab, penanggungJawab.Trim(), alamatPj.Trim(), hubunganPj.Trim(), biaya, noRawat);
    }

    /// <summary>Hapus registrasi (BtnHapus). Non-Admin Utama paling lama 2x24 jam. Tambahan dari Gramedika: ditolak bila sudah ada billing agar pembukuan tidak terhapus berantai.</summary>
    public async Task HapusAsync(string noRawat, RegistrasiAkses akses, DateTime? sekarang = null)
    {
        await using var db = await Db.OpenAsync(settings);
        var row = await AmbilAsync(db, noRawat);
        if (!akses.IsAdminUtama && MelewatiBatasEdit(Waktu(row), sekarang ?? DateTime.Now))
            throw new InvalidOperationException("Maaf, perubahan data / penghapusan data tidak boleh lebih dari 2 x 24 jam !");
        if (await db.CountAsync("SELECT COUNT(no_rawat) FROM billing WHERE no_rawat=@p0", noRawat) > 0)
            throw new InvalidOperationException("Registrasi sudah memiliki billing, tidak bisa dihapus. Hubungi bagian kasir/keuangan..!!");
        await db.ExecAsync("DELETE FROM reg_periksa WHERE no_rawat=@p0", noRawat);
    }

    private static async Task<Row> AmbilAsync(Db db, string noRawat) =>
        await db.FirstAsync("SELECT no_rkm_medis,kd_poli,biaya_reg,stts,stts_daftar,tgl_registrasi,jam_reg FROM reg_periksa WHERE no_rawat=@p0", noRawat)
        ?? throw new InvalidOperationException("Registrasi tidak ditemukan lagi. Muat ulang daftar.");

    private static DateTime Waktu(Row row)
    {
        var tanggal = row.Dt("tgl_registrasi") ?? DateTime.Today;
        var jam = row["jam_reg"] is TimeSpan t ? t : TimeSpan.Zero;
        return tanggal.Date + jam;
    }

    private static async Task PastikanBolehUbahAsync(Db db, string noRawat, Row row, RegistrasiAkses akses, DateTime sekarang)
    {
        // Sequel.cariRegistrasi: ada billing atau registrasi dibatalkan.
        if (await db.CountAsync("SELECT COUNT(no_rawat) FROM billing WHERE no_rawat=@p0", noRawat) > 0 || row.S("stts") == "Batal")
            throw new InvalidOperationException("Data billing sudah terverifikasi (atau registrasi dibatalkan), data tidak boleh diubah.\nSilahkan hubungi bagian kasir/keuangan ..!!");
        if (!akses.IsAdminUtama && akses.BolehEdit && MelewatiBatasEdit(Waktu(row), sekarang))
            throw new InvalidOperationException("Maaf, perubahan data / penghapusan data tidak boleh lebih dari 2 x 24 jam !");
    }
}
