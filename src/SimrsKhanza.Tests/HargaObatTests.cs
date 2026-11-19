using SimrsKhanza.Data.Farmasi;

namespace SimrsKhanza.Tests;

public class HargaObatTests
{
    private static readonly double[] Persen = [20, 25, 25, 25, 20, 30, 35, 20, 30, 10];

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(1001, 1100)]
    [InlineData(1199.99, 1200)]
    [InlineData(0, 0)]
    public void RoundUp_ToNext100(double v, double expected) => Assert.Equal(expected, HargaObat.RoundUp(v));

    [Fact]
    public void Hitung_SmallUnit_NoPpn_BasisHargaBeli()
    {
        // 10 box @ 50.000, isi 10 tablet, diskon 10% => total 450.000 for 100 tablet
        var (hBeli, dasar, jual) = HargaObat.Hitung(50000, 10, 450000, 100, 11, new HargaSetting("Umum", false, false), Persen);
        Assert.Equal(5000, hBeli);
        Assert.Equal(4500, dasar);
        Assert.Equal(6000, jual[0]);   // 5000 + 20%
        Assert.Equal(6800, jual[6]);   // 5000 + 35% = 6750 -> 6800
    }

    [Fact]
    public void Hitung_WithPpn_AndDiscountBasis()
    {
        var (hBeli, dasar, jual) = HargaObat.Hitung(1000, 1, 900, 1, 11, new HargaSetting("Per Jenis", true, true), Persen);
        Assert.Equal(1110, hBeli);
        Assert.Equal(999, dasar);
        Assert.Equal(1200, jual[0]);   // 999 + 20% = 1198.8 -> 1200
    }

    [Fact]
    public void Line_ComputesConversionAndTotals()
    {
        var l = new PenerimaanLine
        {
            Kode = "B1", Nama = "X", KdJns = "J", SatKecil = "TAB", NamaSatKecil = "Tablet", SatBesar = "BOX", NamaSatBesar = "Box",
            Isi = 10, HargaLama = new double[12], Jumlah = 3, Harga = 20000, DiskonPersen = 5, PakaiSatuanBesar = true, Kadaluarsa = "31/12/2027",
        };
        Assert.Equal(30, l.JumlahKecil);
        Assert.Equal(60000, l.Subtotal);
        Assert.Equal(3000, l.BesarDiskon);
        Assert.Equal(57000, l.Total);
        Assert.Equal("BOX", l.KodeSatBeli);
        Assert.Equal(new DateTime(2027, 12, 31), l.TanggalKadaluarsa);

        var t = PenerimaanRepository.Totals([l], 11, 10000);
        Assert.Equal(57000 * 0.11, t.Ppn, 6);
        Assert.Equal(57000 + 57000 * 0.11 + 10000, t.Tagihan, 6);
    }
}
