namespace SimrsGramedika.Core.Auth;

/// <summary>
/// Decides which Menu Utama tiles a user sees, the same way legacy frmUtama.java
/// does (`if(akses.getXxx()==true){ Panelmenu.add(btnYyy); }`). The tile → `user`
/// column map is generated into LegacyTilePermissions.g.cs.
/// </summary>
public static partial class LegacyTilePermissions
{
    /// <summary>Permission key granted only to accounts from the legacy `admin` table.</summary>
    public const string AdminUtamaKey = "admin_utama";
    public const string DatabaseBackupTile = "btnDatabaseBackup";

    /// <summary>
    /// Tiles whose akses flag has no `user` column — legacy sets them true only for
    /// Admin Utama (akses.admin/user/vakum/aplikasi/e_eksekutif).
    /// </summary>
    private static readonly HashSet<string> AdminOnlyTiles = new(StringComparer.Ordinal)
    {
        "btnAdmin", "btnUser", "btnVakum", "btnSetupAplikasi", "btnEEksekutif",
    };

    /// <summary>Getters the generator couldn't resolve automatically.</summary>
    private static readonly Dictionary<string, string[]> ManualTileColumns = new(StringComparer.Ordinal)
    {
        ["btnCekReferensiCaraKeluarBPJS"] = ["bpjs_cek_carakeluar"],
        ["btnLamaPelayananLabPA"] = ["lama_pelayanan_lab"],
        ["btnLamaPelayananLabMB"] = ["lama_pelayanan_lab"],
    };

    public static bool CanSeeTile(AuthenticatedUser user, string tileKey)
    {
        if (user.IsAdminUtama) return true;
        if (AdminOnlyTiles.Contains(tileKey)) return false;

        if (TileColumns.TryGetValue(tileKey, out var cols) || ManualTileColumns.TryGetValue(tileKey, out cols))
            return cols.Any(user.HasPermission);

        // No akses check found in frmUtama for this tile — legacy shows it to everyone logged in.
        return true;
    }
}
