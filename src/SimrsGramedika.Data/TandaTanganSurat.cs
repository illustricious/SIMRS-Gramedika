using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>
/// Teks QR tanda tangan pada surat, sama dengan parameter "finger" Khanza. Ini hanya identitas penanda tangan,
/// bukan tanda tangan elektronik tersertifikasi.
/// </summary>
public static class TandaTanganSurat
{
    public static string Teks(string namaRs, string kabupaten, string dokter, string id, string tanggal) =>
        $"Dikeluarkan di {namaRs}, Kabupaten/Kota {kabupaten}\nDitandatangani secara elektronik oleh {dokter}\nID {id}\n{tanggal}";

    /// <summary>ID pada QR: SHA-1 sidik jari pegawai dokter bila ada, selain itu kode dokter.</summary>
    internal static async Task<string> IdAsync(Db db, string kodeDokter)
    {
        var sidik = await db.StrAsync("""
            SELECT SHA1(sidikjari.sidikjari) FROM sidikjari
            JOIN pegawai ON pegawai.id=sidikjari.id WHERE pegawai.nik=@p0
            """, kodeDokter);
        return sidik.Length > 0 ? sidik : kodeDokter;
    }
}
