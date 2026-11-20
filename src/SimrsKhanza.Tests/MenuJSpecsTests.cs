using SimrsKhanza.Data.Master;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.Tests;

public class MenuJSpecsTests
{
    [Fact]
    public void EveryMenuJTile_HasAScreen()
    {
        string[] explicitRoutes =
        [
            "btnKedatanganPasienPerJam", "btnSensusHarianPoli", "btnKunjunganRalan",
            "btnKunjunganRanap", "btnRekapKunjungan", "btnLaporanBulananIRJ",
        ];
        var menuIndex = Array.FindIndex(LegacyMenuCatalog.Categories, name => name.StartsWith("[J]", StringComparison.Ordinal));
        Assert.True(menuIndex >= 0);
        var catalog = LegacyMenuCatalog.Tiles[menuIndex].Select(tile => tile.Key).ToHashSet();
        var registered = MenuJSpecs.Masters.Keys.Concat(MenuJSpecs.Reports.Keys).Concat(explicitRoutes).ToArray();

        Assert.Equal(96, catalog.Count);
        Assert.Equal(registered.Length, registered.Distinct().Count());
        Assert.Empty(catalog.Except(registered));
        Assert.Empty(registered.Except(catalog));
    }
}
