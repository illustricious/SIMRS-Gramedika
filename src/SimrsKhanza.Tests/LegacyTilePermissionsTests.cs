using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.Tests;

public class LegacyTilePermissionsTests
{
    [Fact]
    public void AdminUtama_SeesEveryTile_IncludingAdminOnly()
    {
        var admin = new AuthenticatedUser { UserId = "spv", IsAdminUtama = true };

        Assert.True(LegacyTilePermissions.CanSeeTile(admin, "btnRegistrasi"));
        Assert.True(LegacyTilePermissions.CanSeeTile(admin, "btnSetupAplikasi"));
    }

    [Fact]
    public void Pegawai_SeesOnlyTilesForTrueUserColumns()
    {
        var user = new AuthenticatedUser
        {
            UserId = "12345",
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "registrasi" },
        };

        Assert.True(LegacyTilePermissions.CanSeeTile(user, "btnRegistrasi"));
        Assert.False(LegacyTilePermissions.CanSeeTile(user, "btnKamarInap"));
        Assert.False(LegacyTilePermissions.CanSeeTile(user, "btnSetupAplikasi"));
        Assert.False(LegacyTilePermissions.CanSeeTile(user, "btnUser"));
    }
}
