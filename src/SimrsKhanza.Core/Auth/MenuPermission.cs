namespace SimrsKhanza.Core.Auth;

/// <summary>
/// One dashboard tile. Legacy equivalent: frmUtama generates icon-grid buttons
/// at runtime from the `akses`/`user` permission row — here the catalog itself
/// is data (not hardcoded XAML), filtered per logged-in user's permissions.
/// </summary>
public record MenuItemDefinition(string Key, string Label, string Module, string RequiredPermission);

public interface IMenuCatalog
{
    IReadOnlyList<MenuItemDefinition> AllItems { get; }
}

/// <summary>
/// Static catalog for now — mirrors the Modules/ folders under SimrsKhanza.UI.
/// Later this can be backed by the `Permission` table instead of hardcoded C#.
/// </summary>
public class DefaultMenuCatalog : IMenuCatalog
{
    public IReadOnlyList<MenuItemDefinition> AllItems { get; } = new List<MenuItemDefinition>
    {
        new("rekammedis", "Rekam Medis", "RekamMedis", "rekammedis.view"),
        new("keuangan", "Keuangan", "Keuangan", "keuangan.view"),
        new("kepegawaian", "Kepegawaian", "Kepegawaian", "kepegawaian.view"),
        new("inventaris", "Inventaris", "Inventaris", "inventaris.view"),
        new("laporan", "Laporan", "Laporan", "laporan.view"),
        new("bridging", "Bridging BPJS/SatuSehat", "Bridging", "bridging.view"),
    };
}

public interface IMenuPermissionService
{
    IReadOnlyList<MenuItemDefinition> GetVisibleMenu(AuthenticatedUser user);
}

public class MenuPermissionService(IMenuCatalog catalog) : IMenuPermissionService
{
    public IReadOnlyList<MenuItemDefinition> GetVisibleMenu(AuthenticatedUser user)
        => catalog.AllItems.Where(item => user.HasPermission(item.RequiredPermission)).ToList();
}
