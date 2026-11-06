using System.Text.Json;

namespace SimrsKhanza.Config;

/// <summary>
/// Persists DatabaseSettings locally so the Pengaturan (Settings) screen can
/// actually save what the admin types, instead of only reading environment
/// variables. Replaces legacy setting/database.ini's role for this app — but
/// stored under the user's profile (%APPDATA%), never inside the repo, so a
/// saved connection string/password is never at risk of being committed.
/// </summary>
public static class DatabaseSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimrsKhanza", "database.settings.json");

    public static DatabaseSettings? TryLoad()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<DatabaseSettings>(json);
        }
        catch
        {
            return null; // corrupt/unreadable file — caller falls back to environment variables
        }
    }

    public static void Save(DatabaseSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
