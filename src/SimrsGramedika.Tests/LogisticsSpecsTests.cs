using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Inventaris;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public class LogisticsSpecsTests
{
    [Theory]
    [InlineData("[E]", 33)]
    [InlineData("[F]", 27)]
    [InlineData("[S]", 25)]
    public void EveryLogisticsTile_HasOneScreen(string category, int count)
    {
        var menuIndex = Array.FindIndex(LegacyMenuCatalog.Categories, name => name.StartsWith(category, StringComparison.Ordinal));
        Assert.True(menuIndex >= 0);
        var catalog = LegacyMenuCatalog.Tiles[menuIndex].Select(tile => tile.Key).ToArray();
        var shared = catalog.Where(MenuDSpecs.Masters.ContainsKey);
        var keys = category switch
        {
            "[E]" => MenuESpecs.Masters.Keys.Concat(MenuESpecs.Reports.Keys),
            "[F]" => MenuFSpecs.Masters.Keys.Concat(MenuFSpecs.Reports.Keys),
            "[S]" => MenuSSpecs.Masters.Keys.Concat(MenuSSpecs.Reports.Keys),
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

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

    [Fact]
    public async Task BarangToko_EditHargaMempertahankanStokDanStatus()
    {
        var spec = MenuSSpecs.Masters["btnBarangToko"];
        Assert.False(spec.CanDelete);
        Assert.DoesNotContain(spec.Fields, field => field.Column is "stok" or "status");
        var prepare = Assert.IsType<Func<SimrsGramedika.Data.Sql.Db, Dictionary<string, object?>,
            Dictionary<string, object?>?, Task>>(spec.BeforeSave);

        var prices = new Dictionary<string, object?>
        {
            ["dasar"] = 4000d, ["h_beli"] = 5000d, ["distributor"] = 6000d,
            ["grosir"] = 6500d, ["retail"] = 7000d,
        };
        await prepare(null!, prices, new() { ["kode_brng"] = "UJI" });
        Assert.Equal(5, prices.Count);

        await prepare(null!, prices, null);
        Assert.Equal(0d, prices["stok"]);
        Assert.Equal("1", prices["status"]);
        prices["retail"] = -1d;
        await Assert.ThrowsAsync<ArgumentException>(async () => await prepare(null!, prices, null));
    }
}
