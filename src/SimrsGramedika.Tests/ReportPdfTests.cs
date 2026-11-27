using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public sealed class ReportPdfTests
{
    private static readonly KopInstansi Kop = new("RS UJI", "Jalan Uji", "000", "", null);

    private static Row Baris(int kolom, string isi) => new(Enumerable.Range(0, kolom)
        .ToDictionary(i => $"k{i}", _ => (object?)isi, StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData(1, "pendek")]
    [InlineData(12, "Keterangan sintetis yang cukup panjang untuk dilipat")]
    [InlineData(24, "KODETANPASPASIYANGSANGATPANJANGSEKALIUNTUKSATUKOLOMLAPORAN")]
    public void KolomSedikitMaupunBanyak_BisaDicetak(int kolom, string isi)
    {
        var cols = Enumerable.Range(0, kolom).Select(i => new Col($"k{i}", $"Kolom {i}")).ToArray();
        var path = ReportPdf.GeneratePdf("Laporan Uji", "Periode uji", cols,
            Enumerable.Range(0, 80).Select(_ => Baris(kolom, isi)).ToList(), ["Total: 1"], Kop);
        Assert.True(new FileInfo(path).Length > 1000);
        File.Delete(path);
    }

    [Fact]
    public void TanpaBarisAtauTerlaluBanyak_Ditolak()
    {
        Col[] cols = [new("k0", "Kolom")];
        Assert.Throws<InvalidOperationException>(() => ReportPdf.GeneratePdf("Uji", "", cols, [], [], Kop));
        var banyak = Enumerable.Repeat(Baris(1, "x"), ReportPdf.BatasBaris + 1).ToList();
        Assert.Throws<InvalidOperationException>(() => ReportPdf.GeneratePdf("Uji", "", cols, banyak, [], Kop));
    }
}
