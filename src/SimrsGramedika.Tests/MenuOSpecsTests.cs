using SimrsGramedika.Data.Master;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public class MenuOSpecsTests
{
    [Fact]
    public void EveryImplementedTile_IsInCatalogOnce()
    {
        var index = Array.FindIndex(LegacyMenuCatalog.Categories, name => name.StartsWith("[O]", StringComparison.Ordinal));
        Assert.True(index >= 0);
        var catalog = LegacyMenuCatalog.Tiles[index].Select(tile => tile.Key).ToHashSet();
        Assert.Equal(113, catalog.Count);
        Assert.Equal(catalog.Order(), MenuOSpecs.Reports.Keys.Order());
    }

    [Fact]
    public void CountReports_MatchPeriodParameters()
    {
        foreach (var (key, spec) in MenuOSpecs.Reports)
        {
            Assert.NotEmpty(spec.TotalColumns);
            Assert.Contains("@p0", spec.Sql);
            Assert.Equal(spec.Period is ReportPeriod.DateRange or ReportPeriod.Month, spec.Sql.Contains("@p2"));
            Assert.Equal(spec.Period == ReportPeriod.Year, spec.Sql.Contains("YEAR(") && spec.Sql.Contains("=@p1"));
        }
    }
}
