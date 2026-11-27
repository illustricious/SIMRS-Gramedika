using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>
/// Pengaturan TANGGALMUNDUR=no Khanza: selain Admin Utama tidak bisa memilih tanggal dan jam. Catatan baru
/// memakai waktu server, catatan yang diubah mempertahankan waktu tersimpannya.
/// </summary>
public static class KunciTanggal
{
    public static bool Aktif(DatabaseSettings settings, bool adminUtama) => !settings.TanggalMundur && !adminUtama;

    /// <summary>Waktu yang boleh disimpan: pilihan pengguna, kecuali bila tanggal dikunci.</summary>
    /// <param name="tersimpan">Waktu catatan yang sedang diubah; null untuk catatan baru.</param>
    internal static async Task<DateTime> WaktuAsync(Db db, bool terkunci, DateTime diminta, DateTime? tersimpan = null)
    {
        if (!terkunci) return diminta;
        if (tersimpan is { } lama) return lama;
        var sekarang = (DateTime)(await db.ScalarAsync("SELECT NOW()"))!;
        return sekarang.AddTicks(-(sekarang.Ticks % TimeSpan.TicksPerSecond));
    }
}
