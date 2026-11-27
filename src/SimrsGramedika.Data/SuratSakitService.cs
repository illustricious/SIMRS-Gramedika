using System.Globalization;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>Satu baris suratsakit. NoSurat kosong berarti dibuatkan nomor saat disimpan.</summary>
public sealed record SuratSakitData(string NoSurat, DateTime TanggalAwal, DateTime TanggalAkhir, string LamaSakit);

/// <summary>Isi cetak surat cuti sakit (rptSuratSakit Khanza).</summary>
public sealed record SuratSakitCetak(
    SuratSakitData Surat, string Dokter, string Pasien, string HubunganKeluarga, string NamaKeluarga,
    DateTime? TanggalLahir, string JenisKelamin, string Pekerjaan, string Alamat)
{
    /// <summary>ID penanda tangan dan tanggal registrasi, keduanya dipakai pada QR.</summary>
    public string IdDokter { get; init; } = "";
    public DateTime? TanggalRegistrasi { get; init; }
}

public sealed class SuratSakitService(DatabaseSettings settings, AuthenticatedUser user)
{
    public const string Izin = "surat_sakit";

    private static readonly string[] Satuan =
        ["Nol", "Satu", "Dua", "Tiga", "Empat", "Lima", "Enam", "Tujuh", "Delapan", "Sembilan", "Sepuluh", "Sebelas"];

    /// <summary>Isian bawaan "lama sakit" seperti contoh Khanza "1 (Satu)", dihitung termasuk kedua tanggal.</summary>
    public static string LamaSakit(DateTime awal, DateTime akhir)
    {
        var hari = (akhir.Date - awal.Date).Days + 1;
        return hari is < 1 or > 99 ? "" : $"{hari} ({Terbilang(hari)})";
    }

    private static string Terbilang(int n) => n switch
    {
        < 12 => Satuan[n],
        < 20 => $"{Satuan[n - 10]} Belas",
        _ => $"{Satuan[n / 10]} Puluh" + (n % 10 == 0 ? "" : $" {Satuan[n % 10]}"),
    };

    public async Task<IReadOnlyList<SuratSakitData>> DaftarAsync(string noRawat)
    {
        Periksa(noRawat);
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT no_surat,tanggalawal,tanggalakhir,lamasakit FROM suratsakit
            WHERE no_rawat=@p0 ORDER BY tanggalawal DESC,no_surat DESC
            """, noRawat);
        return rows.Select(Baca).ToList();
    }

    /// <summary>
    /// Menambah surat, atau mengubah surat bernomor <paramref name="noSuratLama"/>. Mengembalikan nomor surat.
    /// Nomor otomatis seperti Khanza: SKS + tanggal awal + 3 angka urut per tanggal awal.
    /// </summary>
    public async Task<string> SimpanAsync(string noRawat, SuratSakitData data, string? noSuratLama = null)
    {
        Periksa(noRawat);
        var nomor = noSuratLama ?? data.NoSurat.Trim();
        var lama = data.LamaSakit.Trim();
        if (lama.Length == 0) throw new ArgumentException("Lama sakit wajib diisi.");
        if (lama.Length > 20) throw new ArgumentException("Lama sakit paling banyak 20 karakter.");
        if (data.TanggalAkhir.Date < data.TanggalAwal.Date)
            throw new ArgumentException("Tanggal akhir tidak boleh sebelum tanggal awal.");
        if (nomor.Length > 17) throw new ArgumentException("No. surat paling banyak 17 karakter.");

        await using var db = await Db.OpenAsync(settings);
        await db.WithNamedLockAsync($"simrs.suratsakit.{db.Conn.Database}", "Surat sakit sedang disimpan oleh pengguna lain.", async () =>
        {
            if (noSuratLama is not null)
            {
                var berubah = await db.ExecAsync("""
                    UPDATE suratsakit SET tanggalawal=@p2,tanggalakhir=@p3,lamasakit=@p4 WHERE no_surat=@p0 AND no_rawat=@p1
                    """, nomor, noRawat, data.TanggalAwal.Date, data.TanggalAkhir.Date, lama);
                if (berubah == 0) throw new InvalidOperationException("Surat ini sudah tidak ada. Muat ulang daftar.");
                return;
            }

            if (nomor.Length == 0)
                nomor = await db.AutoNomorAsync(
                    "SELECT IFNULL(MAX(CONVERT(RIGHT(no_surat,3),SIGNED)),0) FROM suratsakit WHERE tanggalawal=@p0",
                    "SKS" + data.TanggalAwal.ToString("yyyyMMdd", CultureInfo.InvariantCulture), 3, data.TanggalAwal.Date);
            if (await db.CountAsync("SELECT COUNT(*) FROM suratsakit WHERE no_surat=@p0", nomor) > 0)
                throw new InvalidOperationException($"No. surat {nomor} sudah dipakai.");
            await db.ExecAsync(
                "INSERT INTO suratsakit(no_surat,no_rawat,tanggalawal,tanggalakhir,lamasakit) VALUES(@p0,@p1,@p2,@p3,@p4)",
                nomor, noRawat, data.TanggalAwal.Date, data.TanggalAkhir.Date, lama);
        });
        return nomor;
    }

    public async Task<SuratSakitCetak> CetakAsync(string noRawat, string noSurat)
    {
        Periksa(noRawat);
        await using var db = await Db.OpenAsync(settings);
        var r = await db.FirstAsync("""
            SELECT s.no_surat,s.tanggalawal,s.tanggalakhir,s.lamasakit,r.kd_dokter,r.tgl_registrasi,d.nm_dokter,p.nm_pasien,p.keluarga,p.namakeluarga,
                   p.tgl_lahir,p.jk,p.pekerjaan,
                   CONCAT_WS(', ',p.alamat,kel.nm_kel,kec.nm_kec,kab.nm_kab) AS alamat_lengkap
            FROM suratsakit s
            JOIN reg_periksa r ON r.no_rawat=s.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN dokter d ON d.kd_dokter=r.kd_dokter
            LEFT JOIN kelurahan kel ON kel.kd_kel=p.kd_kel
            LEFT JOIN kecamatan kec ON kec.kd_kec=p.kd_kec
            LEFT JOIN kabupaten kab ON kab.kd_kab=p.kd_kab
            WHERE s.no_rawat=@p0 AND s.no_surat=@p1
            """, noRawat, noSurat) ?? throw new InvalidOperationException("Data surat sakit sudah tidak tersedia.");
        return new(Baca(r), r.S("nm_dokter"), r.S("nm_pasien"), r.S("keluarga"), r.S("namakeluarga"), r.Dt("tgl_lahir"),
            r.S("jk") == "L" ? "Laki-Laki" : "Perempuan", r.S("pekerjaan"), r.S("alamat_lengkap"))
        {
            IdDokter = await TandaTanganSurat.IdAsync(db, r.S("kd_dokter")),
            TanggalRegistrasi = r.Dt("tgl_registrasi"),
        };
    }

    private static SuratSakitData Baca(Row r) => new(r.S("no_surat"), r.Dt("tanggalawal") ?? DateTime.Today,
        r.Dt("tanggalakhir") ?? DateTime.Today, r.S("lamasakit"));

    private void Periksa(string noRawat)
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Hak akses Surat Sakit diperlukan.");
        if (string.IsNullOrWhiteSpace(noRawat)) throw new ArgumentException("Pilih kunjungan terlebih dahulu.");
    }
}
