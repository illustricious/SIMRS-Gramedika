using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

public sealed record PasienCalonBayi(string NoRekamMedis, string Nama, string TanggalLahir);
/// <param name="JumlahTransaksi">Tindakan, obat, pemeriksaan dan resep pulang yang tercatat atas nama bayi.</param>
public sealed record BayiGabung(string NoRawat, string NoRekamMedis, string Nama, int JumlahTransaksi);

/// <summary>
/// Rawat gabung ibu dan bayi (DlgKamarInap, tabel ranap_gabung). Bayi mendapat registrasi rawat inap sendiri tanpa baris
/// kamar_inap: ia memakai kamar ibunya dan tagihannya digabung ke No. Rawat ibu.
/// </summary>
public sealed class RawatGabungService(DatabaseSettings settings, AuthenticatedUser user)
{
    public const string Izin = "kamar_inap";

    /// <summary>
    /// Ekspresi SQL untuk No. Rawat pemilik baris kamar_inap suatu kunjungan: ibunya bila kunjungan itu bayi rawat gabung.
    /// Argumen harus nama kolom atau parameter dari kode, bukan isian pengguna.
    /// </summary>
    internal static string PemilikKamar(string noRawat) =>
        $"COALESCE((SELECT rg.no_rawat FROM ranap_gabung rg WHERE rg.no_rawat2={noRawat} LIMIT 1),{noRawat})";

    public async Task<IReadOnlyList<BayiGabung>> DaftarAsync(string noRawatIbu)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT g.no_rawat2,r.no_rkm_medis,p.nm_pasien,
                (SELECT COUNT(*) FROM rawat_inap_dr WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM rawat_inap_pr WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM rawat_inap_drpr WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM rawat_jl_dr WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM rawat_jl_pr WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM rawat_jl_drpr WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM detail_pemberian_obat WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM periksa_lab WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM periksa_radiologi WHERE no_rawat=g.no_rawat2)+
                (SELECT COUNT(*) FROM resep_pulang WHERE no_rawat=g.no_rawat2) AS transaksi
            FROM ranap_gabung g JOIN reg_periksa r ON r.no_rawat=g.no_rawat2
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            WHERE g.no_rawat=@p0 ORDER BY g.no_rawat2
            """, noRawatIbu);
        return rows.Select(r => new BayiGabung(r.S("no_rawat2"), r.S("no_rkm_medis"), r.S("nm_pasien"), (int)r.D("transaksi"))).ToList();
    }

    public async Task<IReadOnlyList<PasienCalonBayi>> CariPasienAsync(string pencarian)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT no_rkm_medis,nm_pasien,tgl_lahir FROM pasien
            WHERE no_rkm_medis LIKE @p0 OR nm_pasien LIKE @p0
            ORDER BY tgl_lahir DESC,no_rkm_medis DESC LIMIT 100
            """, "%" + pencarian.Trim() + "%");
        return rows.Select(r => new PasienCalonBayi(r.S("no_rkm_medis"), r.S("nm_pasien"),
            r.Dt("tgl_lahir")?.ToString("yyyy-MM-dd") ?? "")).ToList();
    }

    /// <summary>Buat registrasi rawat inap bayi dari data registrasi ibu dan hubungkan keduanya. Mengembalikan No. Rawat bayi.</summary>
    public async Task<string> GabungAsync(string noRawatIbu, string noRekamMedisBayi)
    {
        PastikanIzin();
        if (string.IsNullOrWhiteSpace(noRekamMedisBayi)) throw new ArgumentException("Pilih pasien bayi.");
        for (var percobaan = 0; ; percobaan++)
        {
            await using var db = await Db.OpenAsync(settings);
            await db.BeginAsync();
            var ibu = await KunciIbuAsync(db, noRawatIbu);
            if (await db.FirstAsync("SELECT no_rawat FROM ranap_gabung WHERE no_rawat=@p0 OR no_rawat2=@p0 LIMIT 1 FOR UPDATE", noRawatIbu) is not null)
                throw new InvalidOperationException("No. Rawat ini sudah terhubung rawat gabung. Lepas dulu sebelum menggabungkan lagi.");
            if (await db.FirstAsync("SELECT no_rawat FROM kamar_inap WHERE no_rawat=@p0 AND stts_pulang='-' LIMIT 1 FOR UPDATE", noRawatIbu) is null)
                throw new InvalidOperationException("Ibu tidak sedang menempati kamar.");
            if (ibu.S("no_rkm_medis") == noRekamMedisBayi) throw new InvalidOperationException("Pasien bayi tidak boleh sama dengan ibunya.");
            var bayi = await db.FirstAsync("SELECT tgl_lahir,NOW() sekarang FROM pasien WHERE no_rkm_medis=@p0 FOR UPDATE", noRekamMedisBayi)
                ?? throw new InvalidOperationException("Pasien bayi tidak ditemukan.");
            if (await db.CountAsync("""
                    SELECT COUNT(*) FROM reg_periksa r JOIN kamar_inap k
                        ON k.no_rawat=COALESCE((SELECT g.no_rawat FROM ranap_gabung g WHERE g.no_rawat2=r.no_rawat LIMIT 1),r.no_rawat)
                    WHERE r.no_rkm_medis=@p0 AND r.status_lanjut='Ranap' AND r.stts<>'Batal' AND k.stts_pulang='-'
                    """, noRekamMedisBayi) > 0)
                throw new InvalidOperationException("Pasien ini masih tercatat sedang dirawat inap.");
            var (umur, satuan) = bayi.Dt("tgl_lahir") is { } lahir
                ? RegistrasiService.HitungUmur(DateOnly.FromDateTime(lahir), DateOnly.FromDateTime(bayi.Dt("sekarang")!.Value))
                : (0, "Th");
            var tanggal = ibu.Dt("tgl_registrasi")?.Date ?? throw new InvalidOperationException("Tanggal registrasi ibu tidak valid.");
            var noRawat = await db.AutoNomorAsync(
                "SELECT IFNULL(MAX(CONVERT(RIGHT(no_rawat,6),SIGNED)),0) FROM reg_periksa WHERE tgl_registrasi=@p0",
                $"{tanggal:yyyy/MM/dd}/", 6, tanggal);
            try
            {
                // Seperti Khanza, registrasi bayi ditandai "Sudah Bayar" agar tidak ditagih terpisah.
                await db.ExecAsync("""
                    INSERT INTO reg_periksa(no_reg,no_rawat,tgl_registrasi,jam_reg,kd_dokter,no_rkm_medis,kd_poli,p_jawab,almt_pj,hubunganpj,
                        biaya_reg,stts,stts_daftar,status_lanjut,kd_pj,umurdaftar,sttsumur,status_bayar,status_poli)
                    SELECT no_reg,@p1,tgl_registrasi,jam_reg,kd_dokter,@p2,kd_poli,p_jawab,almt_pj,hubunganpj,
                        biaya_reg,'Belum','Baru','Ranap',kd_pj,@p3,@p4,'Sudah Bayar','Baru'
                    FROM reg_periksa WHERE no_rawat=@p0
                    """, noRawatIbu, noRawat, noRekamMedisBayi, umur, satuan);
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry && percobaan < 4)
            {
                continue; // Nomor dipakai pendaftaran lain; ulangi transaksi dari awal.
            }
            await db.ExecAsync("INSERT INTO ranap_gabung(no_rawat,no_rawat2) VALUES(@p0,@p1)", noRawatIbu, noRawat);
            await db.CommitAsync();
            return noRawat;
        }
    }

    /// <summary>Lepas rawat gabung: registrasi bayi menjadi rawat jalan dan hubungan dihapus. Transaksi bayi tidak dihapus.</summary>
    public async Task LepasAsync(string noRawatIbu, string noRawatBayi)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunciIbuAsync(db, noRawatIbu);
        await db.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", noRawatBayi);
        if (await db.ExecAsync("DELETE FROM ranap_gabung WHERE no_rawat=@p0 AND no_rawat2=@p1", noRawatIbu, noRawatBayi) != 1)
            throw new InvalidOperationException("Rawat gabung sudah berubah. Muat ulang daftar.");
        await db.ExecAsync("UPDATE reg_periksa SET status_lanjut='Ralan' WHERE no_rawat=@p0", noRawatBayi);
        await db.CommitAsync();
    }

    private static async Task<Row> KunciIbuAsync(Db db, string noRawat)
    {
        var ibu = await db.FirstAsync(
                "SELECT no_rkm_medis,tgl_registrasi,stts,status_lanjut FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", noRawat)
            ?? throw new InvalidOperationException("Kunjungan ibu tidak ditemukan.");
        if (ibu.S("status_lanjut") != "Ranap" || ibu.S("stts") == "Batal")
            throw new InvalidOperationException("Kunjungan ibu bukan rawat inap yang aktif.");
        if (await TagihanKunjungan.SudahDitagihAsync(db, noRawat, kunci: true))
            throw new InvalidOperationException("Data billing sudah terverifikasi. Hubungi bagian kasir/keuangan.");
        return ibu;
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak Kamar Inap.");
    }
}
