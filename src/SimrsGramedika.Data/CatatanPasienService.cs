using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

public sealed class CatatanPasienService(DatabaseSettings settings)
{
    public async Task<string> BacaAsync(string noRekamMedis)
    {
        ValidasiNoRm(noRekamMedis);
        await using var db = await Db.OpenAsync(settings);
        return await db.StrAsync("SELECT catatan FROM catatan_pasien WHERE no_rkm_medis=@p0", noRekamMedis);
    }

    /// <summary>
    /// Catatan untuk dimunculkan saat nama pasien diklik: kosong bila "Tampilkan Catatan"
    /// (set_validasi_catatan) tidak diaktifkan, seperti DlgReg.
    /// </summary>
    public async Task<string> UntukDitampilkanAsync(string noRekamMedis)
    {
        ValidasiNoRm(noRekamMedis);
        await using var db = await Db.OpenAsync(settings);
        return await db.StrAsync("""
            SELECT catatan FROM catatan_pasien WHERE no_rkm_medis=@p0
            AND (SELECT tampilkan_catatan FROM set_validasi_catatan LIMIT 1)='Yes'
            """, noRekamMedis);
    }

    public async Task SimpanAsync(string noRekamMedis, string catatan)
    {
        ValidasiNoRm(noRekamMedis);
        if (string.IsNullOrWhiteSpace(catatan)) throw new ArgumentException("Isi catatan tidak boleh kosong.", nameof(catatan));

        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("""
            INSERT INTO catatan_pasien(no_rkm_medis,catatan) VALUES(@p0,@p1)
            ON DUPLICATE KEY UPDATE catatan=VALUES(catatan)
            """, noRekamMedis, catatan.Trim());
    }

    public async Task HapusAsync(string noRekamMedis)
    {
        ValidasiNoRm(noRekamMedis);
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("DELETE FROM catatan_pasien WHERE no_rkm_medis=@p0", noRekamMedis);
    }

    private static void ValidasiNoRm(string noRekamMedis)
    {
        if (string.IsNullOrWhiteSpace(noRekamMedis)) throw new ArgumentException("Pilih pasien terlebih dahulu.", nameof(noRekamMedis));
    }
}
