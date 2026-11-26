using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Pengaturan;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public class DatabaseBackupTests
{
    [Fact]
    public void BackupTile_IsVisibleInMenuUForEmployeesAndAdmins()
    {
        var key = LegacyTilePermissions.DatabaseBackupTile;
        var menuIndex = Array.FindIndex(LegacyMenuCatalog.Categories, label => label.StartsWith("[U]"));
        Assert.Contains(LegacyMenuCatalog.Tiles[menuIndex], tile => tile.Key == key);
        Assert.True(LegacyTilePermissions.CanSeeTile(new AuthenticatedUser { IsAdminUtama = true }, key));
        Assert.True(LegacyTilePermissions.CanSeeTile(new AuthenticatedUser(), key));
    }

    [Fact]
    public async Task Backup_RejectsMissingSessionBeforePreparingOutput()
    {
        var settings = new DatabaseSettings();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            DatabaseBackup.SaveAsync(null, settings, ""));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            DatabaseBackup.SaveAsync(new AuthenticatedUser(), settings, ""));
    }
}
