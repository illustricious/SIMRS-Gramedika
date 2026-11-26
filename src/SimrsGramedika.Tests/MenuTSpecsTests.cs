using SimrsGramedika.Data.Master;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public class MenuTSpecsTests
{
    [Fact]
    public void EveryMenuTTile_UsesItsOwnEditableReferenceTable()
    {
        var menuIndex = Array.FindIndex(LegacyMenuCatalog.Categories,
            name => name.StartsWith("[T]", StringComparison.Ordinal));
        Assert.True(menuIndex >= 0);
        var catalog = LegacyMenuCatalog.Tiles[menuIndex];
        var masters = MenuTSpecs.Masters;

        Assert.Equal(16, catalog.Length);
        Assert.Equal(catalog.Length, masters.Count);
        Assert.Equal(masters.Count, masters.Values.Select(spec => spec.Table).Distinct().Count());
        foreach (var (key, label) in catalog)
        {
            var spec = masters[key];
            Assert.Equal(label, spec.Title);
            Assert.True(spec.CanInsert && spec.CanEdit && spec.CanDelete);
            Assert.Equal("kode", Assert.Single(spec.Keys).Column);
        }

        Assert.Equal("zis_keterangan_kepemilikan_rumah_penerima_dankes",
            masters["btnZISKepemilikanRumahPenerimaDankes"].Table);
        Assert.Equal("zis_keterangan_atap_rumah_penerima_dankes",
            masters["btnZISAtapRumahPenerimaDankes"].Table);
    }
}
