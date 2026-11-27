using System.Text.Json.Serialization;

namespace SimrsGramedika.Config;

/// <summary>
/// One database connection profile (replaces legacy setting/database.xml).
/// Profiles are saved per Windows/Linux user by <see cref="DatabaseSettingsStore"/>
/// and picked on the login screen; nothing is hardcoded or committed.
/// </summary>
public class DatabaseSettings
{
    /// <summary>Profile name shown in the server picker (e.g. "Server Utama", "Server Uji").</summary>
    public string Name { get; init; } = "Default";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 3306;
    public string Database { get; init; } = "sik";
    public string User { get; init; } = "root";
    public string Password { get; init; } = "";

    /// <summary>Legacy database.xml AKTIFKANBATCHOBAT: obat/BHP dipotong per no_batch/no_faktur.</summary>
    public bool AktifkanBatchObat { get; init; }

    /// <summary>Legacy database.xml TANGGALMUNDUR: false mengunci tanggal dan jam bagi selain Admin Utama.</summary>
    public bool TanggalMundur { get; init; } = true;

    /// <summary>Legacy database.xml HPPFARMASI: databarang/data_batch column used as HPP (dasar | h_beli).</summary>
    public string HppFarmasi { get; init; } = "dasar";

    /// <summary>Legacy database.xml PEMBULATANHARGAOBAT: harga obat dibulatkan ke atas per Rp100.</summary>
    public bool PembulatanHargaObat { get; init; }

    /// <summary>Legacy HOSTHYBRIDWEB/PORTWEB/HYBRIDWEB: base URL of the Khanza web apps (penggajian, berkas). Empty = http://{Host}/webapps.</summary>
    public string WebUrl { get; init; } = "";

    /// <summary>Legacy URUTNOREG: nomor antrean berurut per "dokter", "poli", atau "dokter + poli".</summary>
    public string UrutNoReg { get; init; } = "dokter";

    /// <summary>Legacy BASENOREG: "booking" ikut menghitung nomor yang sudah dipesan di booking_registrasi.</summary>
    public string BaseNoReg { get; init; } = "registrasi";

    /// <summary>Legacy JADWALDOKTERDIREGISTRASI=aktif: kuota jadwal dokter membatasi registrasi selain Admin Utama.</summary>
    public bool JadwalDokterDiRegistrasi { get; init; }

    [JsonIgnore]
    public string WebBase => (WebUrl is { Length: > 0 } w ? w : $"http://{Host}/webapps").TrimEnd('/');

    /// <summary>Whitelisted HPP column (it is interpolated into SQL).</summary>
    [JsonIgnore]
    public string HppColumn => HppFarmasi is "h_beli" ? "h_beli" : "dasar";

    /// <summary>"host:port/database" — for status bars and pickers.</summary>
    [JsonIgnore]
    public string Summary => $"{Host}:{Port}/{Database}";

    public string ToConnectionString()
        => $"Server={Q(Host)};Port={Port};Database={Q(Database)};User={Q(User)};Password={Q(Password)};ConvertZeroDateTime=True;Connection Timeout=10;";

    /// <summary>Server-only connection (no database selected) — used to list databases.</summary>
    public string ToServerConnectionString()
        => $"Server={Q(Host)};Port={Port};User={Q(User)};Password={Q(Password)};Connection Timeout=10;";

    /// <summary>Quotes a connection-string value when it contains separators or quotes.</summary>
    private static string Q(string v)
    {
        if (v.IndexOfAny([';', '=', '"', '\'']) < 0 && v.Trim() == v) return v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }

    public DatabaseSettings With(string? name = null, bool? batch = null, string? hpp = null, string? web = null) => new()
    {
        Name = name ?? Name, Host = Host, Port = Port, Database = Database, User = User, Password = Password,
        AktifkanBatchObat = batch ?? AktifkanBatchObat, HppFarmasi = hpp ?? HppFarmasi, WebUrl = web ?? WebUrl,
        UrutNoReg = UrutNoReg, BaseNoReg = BaseNoReg, JadwalDokterDiRegistrasi = JadwalDokterDiRegistrasi,
        PembulatanHargaObat = PembulatanHargaObat, TanggalMundur = TanggalMundur,
    };

    /// <summary>
    /// Environment variables (SIMRS_DB_HOST, SIMRS_DB_PORT, SIMRS_DB_NAME, SIMRS_DB_USER,
    /// SIMRS_DB_PASSWORD) — used when no profile has been saved yet.
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

    /// <summary>True when the connection was configured through environment variables.</summary>
    public static bool HasEnvironment => Environment.GetEnvironmentVariable("SIMRS_DB_HOST") is { Length: > 0 };

    /// <summary>The active profile, or environment variables when nothing has been saved yet.</summary>
    public static DatabaseSettings Load() => DatabaseSettingsStore.TryLoad() ?? FromEnvironment();
}
