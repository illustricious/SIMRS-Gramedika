using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Keuangan;

/// <summary>
/// Cek "kunjungan sudah ditagih" sebelum data kunjungan diubah, mengikuti Sequel.cariRegistrasi Khanza:
/// kunjungan dianggap sudah ditagih bila punya baris billing. status_bayar dan nota_inap tidak dilihat.
/// Bayi rawat gabung ikut dianggap ditagih bila ibunya punya billing, karena tagihannya digabung ke ibu.
/// Pemanggil mengunci baris reg_periksa lebih dulu, menangani registrasi batal, dan menulis pesan penolakannya.
/// Kasir menambah ceknya sendiri karena Kasir yang menulis tagihan.
/// </summary>
public static class TagihanKunjungan
{
    /// <param name="kunci">Baca billing dengan FOR UPDATE.</param>
    public static async Task<bool> SudahDitagihAsync(Db db, object? noRawat, bool kunci = false)
    {
        var akhiran = " LIMIT 1" + (kunci ? " FOR UPDATE" : "");
        return await db.FirstAsync("SELECT no_rawat FROM billing WHERE no_rawat=@p0" + akhiran, noRawat) is not null
            || await db.FirstAsync(
                "SELECT b.no_rawat FROM ranap_gabung g JOIN billing b ON b.no_rawat=g.no_rawat WHERE g.no_rawat2=@p0" + akhiran, noRawat) is not null;
    }

    /// <summary>Versi EF Core, memakai transaksi yang sedang berjalan pada context.</summary>
    internal static Task<bool> SudahDitagihAsync(SimrsGramedikaDbContext db, string noRawat, CancellationToken ct) =>
        db.Database.SqlQuery<bool>($"""
            SELECT (EXISTS(SELECT 1 FROM billing WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM ranap_gabung g JOIN billing b ON b.no_rawat=g.no_rawat WHERE g.no_rawat2={noRawat})) AS Value
            """).SingleAsync(ct);
}
