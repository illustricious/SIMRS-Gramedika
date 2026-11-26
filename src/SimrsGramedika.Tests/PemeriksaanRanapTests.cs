using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;

namespace SimrsGramedika.Tests;

public class PemeriksaanRanapTests
{
    [Fact]
    public void Validasi_MenolakKosongDanNilaiMelebihiSkema()
    {
        var note = Catatan("UJI");
        var fields = note.Isian.ToArray();
        Array.Fill(fields, "");
        Assert.Throws<ArgumentException>(() => PemeriksaanRanapService.ValidasiIsian(note with { Isian = fields }));
        fields[0] = "123456";
        Assert.Throws<ArgumentException>(() => PemeriksaanRanapService.ValidasiIsian(note with { Isian = fields }));
        PemeriksaanRanapService.ValidasiIsian(note);
    }

    [Fact]
    public async Task Perubahan_MenolakKunjunganLainSebelumAksesDatabase()
    {
        var service = new PemeriksaanRanapService(new DatabaseSettings(), "UJI-A",
            new AuthenticatedUser { IsAdminUtama = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(Catatan("UJI-B")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(Catatan("UJI-A"), Catatan("UJI-B")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(Catatan("UJI-B")));
    }

    [Fact]
    public async Task AkunTanpaIzin_DitolakSebelumAksesDatabase()
    {
        var service = new PemeriksaanRanapService(new DatabaseSettings(), "UJI", new AuthenticatedUser());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DaftarAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LimaTerakhirAsync("UJI", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PegawaiAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(Catatan("UJI")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(Catatan("UJI")));
    }

    private static SoapRanap Catatan(string noRawat) =>
        new(noRawat, new DateTime(2026, 11, 23, 10, 0, 0), "UJI", "Compos Mentis",
            PemeriksaanRanapService.Isian.Select(f => f.Kolom == "keluhan" ? "Data sintetis" : "").ToArray());
}
