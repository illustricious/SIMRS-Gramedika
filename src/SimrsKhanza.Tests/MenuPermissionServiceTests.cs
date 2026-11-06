using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.Tests;

public class MenuPermissionServiceTests
{
    private static MenuPermissionService CreateSut() => new(new DefaultMenuCatalog());

    [Fact]
    public void GetVisibleMenu_ForAdmin_IncludesPengaturan()
    {
        var sut = CreateSut();
        var admin = new AuthenticatedUser
        {
            UserId = "admin",
            Permissions = new HashSet<string> { "rekammedis.view", "pengaturan.admin" }
        };

        var menu = sut.GetVisibleMenu(admin);

        Assert.Contains(menu, m => m.Key == "pengaturan");
    }

    [Fact]
    public void GetVisibleMenu_ForNonAdmin_HidesPengaturan()
    {
        var sut = CreateSut();
        var petugas = new AuthenticatedUser
        {
            UserId = "petugas",
            Permissions = new HashSet<string> { "rekammedis.view", "laporan.view" }
        };

        var menu = sut.GetVisibleMenu(petugas);

        Assert.DoesNotContain(menu, m => m.Key == "pengaturan");
    }
}
