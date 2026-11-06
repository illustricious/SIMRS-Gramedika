namespace SimrsKhanza.Config;

/// <summary>
/// Replaces legacy setting/database.xml (AES-encrypted flat properties file,
/// re-read from disk on every getter call — see docs/audit/PLANNING_APLIKASI.md §2).
/// No credentials are hardcoded here: values come from environment variables
/// or appsettings.Local.json (gitignored), never committed to source control.
/// </summary>
public class DatabaseSettings
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 3306;
    public string Database { get; init; } = "sik";
    public string User { get; init; } = "root";
    public string Password { get; init; } = "";

    public string ToConnectionString()
        => $"Server={Host};Port={Port};Database={Database};User={User};Password={Password};";

    /// <summary>
    /// Reads from environment variables (SIMRS_DB_HOST, SIMRS_DB_PORT, SIMRS_DB_NAME,
    /// SIMRS_DB_USER, SIMRS_DB_PASSWORD) so no secret ever needs to live in a file
    /// that gets committed.
    /// </summary>
    public static DatabaseSettings FromEnvironment()
    {
        return new DatabaseSettings
        {
            Host = Environment.GetEnvironmentVariable("SIMRS_DB_HOST") ?? "localhost",
            Port = int.TryParse(Environment.GetEnvironmentVariable("SIMRS_DB_PORT"), out var p) ? p : 3306,
            Database = Environment.GetEnvironmentVariable("SIMRS_DB_NAME") ?? "sik",
            User = Environment.GetEnvironmentVariable("SIMRS_DB_USER") ?? "root",
            Password = Environment.GetEnvironmentVariable("SIMRS_DB_PASSWORD") ?? "",
        };
    }

    /// <summary>
    /// Canonical way the app should read its DB connection: prefers whatever
    /// was saved from the Pengaturan screen (DatabaseSettingsStore), falls
    /// back to environment variables when nothing has been saved yet. Call
    /// sites that previously called FromEnvironment() directly should use
    /// this instead so a change saved in Pengaturan actually takes effect.
    /// </summary>
    public static DatabaseSettings Load() => DatabaseSettingsStore.TryLoad() ?? FromEnvironment();
}
