using SimrsKhanza.Data;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.Tests;

public class RawatJalanServiceTests
{
    private static PemeriksaanRalanInput Empty() => new("2026/11/22/000001", new DateTime(2026, 11, 22), new TimeSpan(9, 0, 0),
        "", "", "", "", "", "", "", "", "Compos Mentis", "", "", "", "", "", "", "", "", "198001");

    [Fact]
    public async Task PemeriksaanDanRiwayat_MemeriksaIzinSebelumDatabase()
    {
        var service = new RawatJalanService(new DatabaseSettings { Host = "127.0.0.1", Port = 1 });
        var tanpaIzin = RawatJalanAkses.DariPengguna(new AuthenticatedUser { UserId = "198001", Permissions = new HashSet<string> { "catatan_perawatan" } });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PemeriksaanAsync("UJI", tanpaIzin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PegawaiAsync(tanpaIzin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LimaTerakhirAsync("UJI", "198001", "", tanpaIzin, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(Empty(), tanpaIzin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(Empty(), Empty(), tanpaIzin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(Empty(), tanpaIzin));
        Assert.True(RawatJalanAkses.DariPengguna(new AuthenticatedUser { Permissions = new HashSet<string> { "tindakan_ralan" } }).Diizinkan);
        Assert.True(RawatJalanAkses.DariPengguna(new AuthenticatedUser { IsAdminUtama = true }).Diizinkan);
        Assert.False(RawatJalanAkses.DariPengguna(null).Diizinkan);
    }

    [Fact]
    public async Task UbahPemeriksaan_MenolakKunjunganBerbeda()
    {
        var service = new RawatJalanService(new DatabaseSettings { Host = "127.0.0.1", Port = 1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(Empty() with { NoRawat = "LAIN" },
            Empty() with { Keluhan = "Uji" }, new(false, "198001", true)));
    }

    [Fact]
    public void AnyClinicalField_CountsAsContent()
    {
        Assert.False(RawatJalanService.AdaIsi(Empty()));
        Assert.True(RawatJalanService.AdaIsi(Empty() with { Gcs = "E4V5M6" }));
        Assert.True(RawatJalanService.AdaIsi(Empty() with { Penilaian = "ISPA" }));
        Assert.False(RawatJalanService.AdaIsi(Empty() with { Keluhan = "   " }));
    }

    [Fact]
    public void NonAdmin_CanOnlyActOnOwnRecords()
    {
        var staff = new RawatJalanAkses(false, "198001");
        RawatJalanService.PastikanMilikSendiri(staff, "198001");
        Assert.Throws<InvalidOperationException>(() => RawatJalanService.PastikanMilikSendiri(staff, "199002"));
        RawatJalanService.PastikanMilikSendiri(new RawatJalanAkses(true, "Admin Utama"), "199002");
    }

    [Fact]
    public void InputBeforeRegistration_IsRejected()
    {
        var registered = new DateTime(2026, 11, 22, 8, 0, 0);
        RawatJalanService.PastikanTidakSebelumRegistrasi(registered, registered);
        Assert.Throws<InvalidOperationException>(() => RawatJalanService.PastikanTidakSebelumRegistrasi(registered, registered.AddSeconds(-1)));
    }

    [Fact]
    public void ConsciousnessList_MatchesLegacyEnum()
    {
        Assert.Equal(12, RawatJalanService.Kesadaran.Length);
        Assert.Contains("Compos Mentis", RawatJalanService.Kesadaran);
    }
}
