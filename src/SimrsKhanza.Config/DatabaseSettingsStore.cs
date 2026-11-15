using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimrsKhanza.Config;

/// <summary>
/// Saved connection profiles + which one is active. Stored under the user's profile
/// (%APPDATA%\SimrsKhanza on Windows, ~/.config/SimrsKhanza on Linux), never in the
/// install folder or the repo. Files written by older builds (a single settings
/// object) are read as one profile named "Default".
/// </summary>
public static class DatabaseSettingsStore
{
    /// <summary>Settings file; SIMRS_CONFIG_DIR overrides the folder (portable installs, testing).</summary>
    public static string FilePath { get; set; } = Path.Combine(
        Environment.GetEnvironmentVariable("SIMRS_CONFIG_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimrsKhanza"),
        "database.settings.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public sealed class Profiles
    {
        public string? Active { get; set; }
        public List<DatabaseSettings> Items { get; set; } = [];
    }

    /// <summary>True once at least one profile has been saved on this computer.</summary>
    public static bool HasSaved => LoadAll().Items.Count > 0;

    public static Profiles LoadAll()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Profiles();
            var node = JsonNode.Parse(File.ReadAllText(FilePath));
            if (node is JsonObject obj && obj.ContainsKey("Host"))
            {
                var single = node.Deserialize<DatabaseSettings>();
                return single is null ? new Profiles() : new Profiles { Active = single.Name, Items = [single] };
            }
            var p = node.Deserialize<Profiles>() ?? new Profiles();
            p.Items = p.Items.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
            return p;
        }
        catch
        {
            return new Profiles(); // corrupt/unreadable file — caller falls back to environment variables
        }
    }

    /// <summary>The active profile (or the first one), null when nothing is saved.</summary>
    public static DatabaseSettings? TryLoad()
    {
        var all = LoadAll();
        return all.Items.FirstOrDefault(i => i.Name == all.Active) ?? all.Items.FirstOrDefault();
    }

    public static void SaveAll(Profiles profiles)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(profiles, Json));
        // The file holds a database password: keep it readable by this user only on Linux/macOS.
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { /* filesystem without unix permissions */ }
        }
    }

    /// <summary>Adds or replaces the profile with the same name and makes it active.</summary>
    public static void Save(DatabaseSettings settings)
    {
        var all = LoadAll();
        var i = all.Items.FindIndex(p => p.Name == settings.Name);
        if (i >= 0) all.Items[i] = settings;
        else all.Items.Add(settings);
        all.Active = settings.Name;
        SaveAll(all);
    }

    public static void SetActive(string name)
    {
        var all = LoadAll();
        if (all.Items.All(p => p.Name != name) || all.Active == name) return;
        all.Active = name;
        SaveAll(all);
    }
}
