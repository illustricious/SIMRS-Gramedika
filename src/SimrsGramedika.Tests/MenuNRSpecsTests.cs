using SimrsGramedika.Data.Master;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public class MenuNRSpecsTests
{
    private static IEnumerable<string> Catalog(string menu)
    {
        var index = Array.FindIndex(LegacyMenuCatalog.Categories, name => name.StartsWith(menu, StringComparison.Ordinal));
        Assert.True(index >= 0);
        return LegacyMenuCatalog.Tiles[index].Select(tile => tile.Key);
    }

    [Theory]
    [InlineData("[N]", 11)]
    [InlineData("[R]", 10)]
    public void EveryTile_HasOneScreen(string menu, int count)
    {
        var catalog = Catalog(menu).ToArray();
        var keys = MenuNRSpecs.Masters.Keys.Concat(MenuNRSpecs.Reports.Keys).Where(catalog.Contains);
        Assert.Equal(count, catalog.Length);
        Assert.Equal(catalog.Order(), keys.Order());
    }

    [Fact]
    public async Task KomponenDarah_TotalExcludesCancellationFee()
    {
        var spec = MenuNRSpecs.Masters["btnUTDKomponenDarah"];
        var values = new Dictionary<string, object?>
        {
            ["jasa_sarana"] = 100d, ["paket_bhp"] = 50d, ["kso"] = 10d, ["manajemen"] = 5d, ["pembatalan"] = 999d, ["lama"] = 35d,
        };
        await spec.BeforeSave!(null!, values, null);
        Assert.Equal(165d, values["total"]);
    }

    [Fact]
    public async Task KomponenDarah_RejectsNegativeComponent()
    {
        var values = new Dictionary<string, object?> { ["jasa_sarana"] = -1d, ["lama"] = 1d };
        await Assert.ThrowsAsync<ArgumentException>(() => MenuNRSpecs.Masters["btnUTDKomponenDarah"].BeforeSave!(null!, values, null));
    }
}
