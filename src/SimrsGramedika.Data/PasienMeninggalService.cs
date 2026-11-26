using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>
/// Satu baris pasien_mati (DlgPasienMati Khanza). Di layar Khanza: Icd1 dasar, Icd2 antara #1,
/// Icd3 antara #2, Icd4 langsung.
/// </summary>
public sealed record PasienMeninggalData(
    DateTime Waktu, string Keterangan, string TempatMeninggal, string KodeDokter,
    string Icd1 = "", string Icd2 = "", string Icd3 = "", string Icd4 = "");

public sealed class PasienMeninggalService(DatabaseSettings settings, AuthenticatedUser user)
{
    public const string Izin = "pasien_meninggal";

    /// <summary>Nilai enum temp_meninggal.</summary>
    public static readonly IReadOnlyList<string> Tempat =
        ["-", "Rumah Sakit", "Puskesmas", "Rumah Bersalin", "Rumah Tempat Tinggal", "Lain-lain (Termasuk Doa)", "Tidak tahu"];

    /// <summary>Data yang sudah tersimpan, atau null bila pasien belum tercatat meninggal.</summary>
    public async Task<PasienMeninggalData?> MuatAsync(string noRekamMedis)
    {
        Periksa(noRekamMedis);
        await using var db = await Db.OpenAsync(settings);
        var r = await db.FirstAsync(
            "SELECT tanggal,jam,keterangan,temp_meninggal,kd_dokter,icd1,icd2,icd3,icd4 FROM pasien_mati WHERE no_rkm_medis=@p0",
            noRekamMedis);
        if (r is null) return null;
        var jam = r["jam"] is TimeSpan t ? t : TimeSpan.Zero;
        return new((r.Dt("tanggal") ?? DateTime.Today).Date + jam, r.S("keterangan"), r.S("temp_meninggal"), r.S("kd_dokter"),
            r.S("icd1"), r.S("icd2"), r.S("icd3"), r.S("icd4"));
    }

    /// <summary>Menyimpan data kematian; pasien yang sudah tercatat diperbarui, bukan diduplikasi.</summary>
    public async Task SimpanAsync(string noRekamMedis, PasienMeninggalData data, DateTime? sekarang = null)
    {
        Periksa(noRekamMedis);
        var keterangan = data.Keterangan.Trim();
        if (keterangan.Length == 0) throw new ArgumentException("Penyebab kematian (keterangan) wajib diisi.");
        if (keterangan.Length > 100) throw new ArgumentException("Keterangan paling banyak 100 karakter.");
        if (string.IsNullOrWhiteSpace(data.KodeDokter)) throw new ArgumentException("Dokter DPJP wajib dipilih.");
        if (!Tempat.Contains(data.TempatMeninggal)) throw new ArgumentException("Tempat meninggal tidak dikenal.");
        var icd = new[] { data.Icd1, data.Icd2, data.Icd3, data.Icd4 }.Select(k => k.Trim()).ToArray();
        if (icd.Any(k => k.Length > 20)) throw new ArgumentException("Kode ICD-X paling banyak 20 karakter.");
        if (data.Waktu > (sekarang ?? DateTime.Now)) throw new ArgumentException("Waktu meninggal tidak boleh di masa depan.");

        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("""
            INSERT INTO pasien_mati(tanggal,jam,no_rkm_medis,keterangan,temp_meninggal,icd1,icd2,icd3,icd4,kd_dokter)
            VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)
            ON DUPLICATE KEY UPDATE tanggal=VALUES(tanggal),jam=VALUES(jam),keterangan=VALUES(keterangan),
                temp_meninggal=VALUES(temp_meninggal),icd1=VALUES(icd1),icd2=VALUES(icd2),icd3=VALUES(icd3),
                icd4=VALUES(icd4),kd_dokter=VALUES(kd_dokter)
            """, data.Waktu.Date, data.Waktu.TimeOfDay, noRekamMedis, keterangan, data.TempatMeninggal,
            icd[0], icd[1], icd[2], icd[3], data.KodeDokter);
    }

    private void Periksa(string noRekamMedis)
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Hak akses Pasien Meninggal diperlukan.");
        if (string.IsNullOrWhiteSpace(noRekamMedis)) throw new ArgumentException("Pilih pasien terlebih dahulu.");
    }
}
