using SimrsKhanza.Data.Master;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.Tests;

public class MenuQSpecsTests
{
    [Fact]
    public void EveryLibraryTile_HasOneScreen()
    {
        var index = Array.FindIndex(LegacyMenuCatalog.Categories, name => name.StartsWith("[Q]", StringComparison.Ordinal));
        Assert.True(index >= 0);
        var catalog = LegacyMenuCatalog.Tiles[index].Select(tile => tile.Key).ToArray();
        Assert.Equal(16, catalog.Length);
        Assert.Equal(catalog.Order(), MenuQSpecs.Masters.Keys.Concat(MenuQSpecs.Reports.Keys).Order());
    }

    [Theory]
    [InlineData(0d, 7d, 500d)]
    [InlineData(3d, 0d, 500d)]
    [InlineData(3d, 7d, -1d)]
    public async Task LoanSettings_RejectInvalidValues(double max, double days, double fine)
    {
        var values = new Dictionary<string, object?> { ["max_pinjam"] = max, ["lama_pinjam"] = days, ["denda_perhari"] = fine };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MenuQSpecs.Masters["btnPengaturanPeminjamanPerpustakaan"].BeforeSave!(null!, values, null));
    }

    [Fact]
    public async Task LoanSettings_AcceptsValidValues()
    {
        var values = new Dictionary<string, object?> { ["max_pinjam"] = 3d, ["lama_pinjam"] = 7d, ["denda_perhari"] = 500d };
        await MenuQSpecs.Masters["btnPengaturanPeminjamanPerpustakaan"].BeforeSave!(null!, values, null);
    }
}
