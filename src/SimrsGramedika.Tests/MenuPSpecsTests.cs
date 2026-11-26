using SimrsGramedika.Data.Master;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public class MenuPSpecsTests
{
    [Fact]
    public void EveryLetterTile_HasOneScreen()
    {
        var index = Array.FindIndex(LegacyMenuCatalog.Categories, name => name.StartsWith("[P]", StringComparison.Ordinal));
        Assert.True(index >= 0);
        var catalog = LegacyMenuCatalog.Tiles[index].Select(tile => tile.Key).ToArray();
        Assert.Equal(45, catalog.Length);
        Assert.Equal(catalog.Order(), MenuPSpecs.Masters.Keys.Concat(MenuPSpecs.Reports.Keys).Order());
    }

    [Fact]
    public void PatientLetters_AreDateRangeReportsKeyedByVisit()
    {
        foreach (var (key, spec) in MenuPSpecs.Reports.Where(pair => pair.Key != "btnSKDPBPJS"))
        {
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.Contains("no_rawat", spec.Sql);
            Assert.Contains("nm_pasien", spec.Sql);
        }
    }

    [Fact]
    public async Task ConsentTemplate_RejectsNegativeFee()
    {
        var values = new Dictionary<string, object?> { ["biaya"] = -5d };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MenuPSpecs.Masters["btnTemplatePersetujuanPenolakanTindakan"].BeforeSave!(null!, values, null));
    }
}
