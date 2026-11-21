using SimrsKhanza.Data.Farmasi;
using SimrsKhanza.Data.Inventaris;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.Tests;

public class LogisticsSpecsTests
{
    [Theory]
    [InlineData("[E]", 33)]
    [InlineData("[F]", 27)]
    public void EveryLogisticsTile_HasOneScreen(string category, int count)
    {
        var menuIndex = Array.FindIndex(LegacyMenuCatalog.Categories, name => name.StartsWith(category, StringComparison.Ordinal));
        Assert.True(menuIndex >= 0);
        var catalog = LegacyMenuCatalog.Tiles[menuIndex].Select(tile => tile.Key).ToArray();
        var shared = catalog.Where(MenuDSpecs.Masters.ContainsKey);
        var keys = category == "[E]" ? MenuESpecs.Masters.Keys.Concat(MenuESpecs.Reports.Keys)
            : MenuFSpecs.Masters.Keys.Concat(MenuFSpecs.Reports.Keys);

        Assert.Equal(count, catalog.Length);
        Assert.Equal(catalog.Order(), keys.Concat(shared).Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BarangBaru_MulaiAktifDenganStokNol_DanEditTidakMenimpaSaldo(bool kitchen)
    {
        var spec = kitchen ? MenuFSpecs.Masters["btnBarangDapur"] : MenuESpecs.Masters["btnBarangIpsrs"];
        Assert.False(spec.CanDelete);
        Assert.DoesNotContain(spec.Fields, field => field.Column is "stok" or "status");
        var prepare = spec.BeforeSave;
        Assert.NotNull(prepare);

        var barangBaru = new Dictionary<string, object?> { ["harga"] = 4500d };
        await prepare(null!, barangBaru, null);
        Assert.Equal(0d, barangBaru["stok"]);
        Assert.Equal("1", barangBaru["status"]);

        var perubahan = new Dictionary<string, object?> { ["harga"] = 5000d };
        await prepare(null!, perubahan, new() { ["kode_brng"] = "UJI" });
        Assert.Single(perubahan);
        Assert.Equal(5000d, perubahan["harga"]);
    }
}
