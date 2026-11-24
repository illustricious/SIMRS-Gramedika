using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;

namespace SimrsKhanza.Tests;

public class CatatanDokterTests
{
    [Fact]
    public async Task IzinCatatanDanKunjungan_DiperiksaSebelumAksesDatabase()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Port = 1 };
        var wrong = new CatatanDokter("UJI-LAIN", new DateTime(2026, 11, 24), "D01", "Dokter Uji", "Catatan Uji");
        var noPermission = new CatatanDokterService(settings, "UJI", new AuthenticatedUser { UserId = "D01" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => noPermission.DaftarAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => noPermission.DokterAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => noPermission.SimpanAsync(wrong.Waktu!.Value, "D01", "Isi"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => noPermission.HapusAsync(wrong));

        var admin = new CatatanDokterService(settings, "UJI", new AuthenticatedUser { IsAdminUtama = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.HapusAsync(wrong));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SimpanAsync(wrong.Waktu!.Value, "D01", "Isi", wrong));
        await Assert.ThrowsAsync<ArgumentException>(() => admin.SimpanAsync(wrong.Waktu!.Value, "D01", new string('a', 701)));
    }

    [Fact]
    public async Task RiwayatSoap_MenolakNipLainSebelumAksesDatabase()
    {
        var service = new RawatJalanService(new DatabaseSettings { Host = "127.0.0.1", Port = 1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LimaTerakhirAsync("UJI", "D02", "", new(false, "D01", true)));
    }
}
