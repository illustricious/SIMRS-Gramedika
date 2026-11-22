using SimrsKhanza.Data.Master;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.Tests;

public class MenuOSpecsTests
{
    [Fact]
    public void EveryImplementedTile_IsInCatalogOnce()
    {
        var index = Array.FindIndex(LegacyMenuCatalog.Categories, name => name.StartsWith("[O]", StringComparison.Ordinal));
        Assert.True(index >= 0);
        var catalog = LegacyMenuCatalog.Tiles[index].Select(tile => tile.Key).ToHashSet();
        Assert.Equal(113, catalog.Count);
        Assert.Equal(MenuOSpecs.Reports.Count, MenuOSpecs.Reports.Keys.Count(catalog.Contains));
        Assert.True(MenuOSpecs.Reports.Count >= 50);
    }

    [Fact]
    public void CountReports_MatchPeriodParameters()
    {
        foreach (var (key, spec) in MenuOSpecs.Reports)
        {
            Assert.Equal(["jumlah"], spec.TotalColumns);
            Assert.Contains("@p0", spec.Sql);
            Assert.Equal(spec.Period is ReportPeriod.DateRange or ReportPeriod.Month, spec.Sql.Contains("@p2"));
            Assert.Equal(spec.Period == ReportPeriod.Year, spec.Sql.Contains("YEAR(") && spec.Sql.Contains("=@p1"));
        }
    }
}
