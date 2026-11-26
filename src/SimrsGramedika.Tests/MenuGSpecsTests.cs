using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Inventaris;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public class MenuGSpecsTests
{
    [Fact]
    public void EveryMenuGTile_HasOneScreen()
    {
        var menuIndex = Array.FindIndex(LegacyMenuCatalog.Categories, category => category.StartsWith("[G]", StringComparison.Ordinal));
        Assert.True(menuIndex >= 0);
        var catalog = LegacyMenuCatalog.Tiles[menuIndex].Select(tile => tile.Key).ToArray();
        var shared = catalog.Where(MenuDSpecs.Masters.ContainsKey);
        var registered = MenuGSpecs.Masters.Keys.Concat(MenuGSpecs.Reports.Keys).Concat(shared).ToArray();

        Assert.Equal(28, catalog.Length);
        Assert.Equal(catalog.Order(), registered.Order());
    }
}
