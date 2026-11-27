using SimrsGramedika.Data;

namespace SimrsGramedika.Tests;

public sealed class HitungRacikanTests
{
    [Fact]
    public void KandunganDariPecahanDibulatkanSatuDesimal()
    {
        Assert.Equal(250m, HitungRacikan.Kandungan(500, 1, 2));
        Assert.Equal(166.7m, HitungRacikan.Kandungan(500, 1, 3));
        Assert.Null(HitungRacikan.Kandungan(0, 1, 2));
        Assert.Null(HitungRacikan.Kandungan(500, 1, 0));
    }

    [Fact]
    public void JumlahMengikutiKandunganLaluPersenDariBeratBahanLain()
    {
        var hasil = HitungRacikan.Ulang(10,
        [
            new("A", "Persen", 0, Kandungan: "10%", Kapasitas: 100),
            new("B", "Kandungan", 0, Kandungan: "250", Kapasitas: 500),
            new("C", "Koma", 0, Kandungan: "2,5", Kapasitas: 10),
            new("D", "Manual", 3, Kapasitas: 50),
            new("E", "Tanpa kapasitas", 4, Kandungan: "100"),
        ]);
        // B 5 tablet, C 2,5 tablet, D 3 tablet: berat 2500+25+150 = 2675, 10% dibagi 100 = 2,7.
        Assert.Equal([2.7m, 5m, 2.5m, 3m, 4m], hasil.Select(b => b.Jumlah));
    }
}
