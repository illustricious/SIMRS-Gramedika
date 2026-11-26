using SimrsGramedika.Config;

namespace SimrsGramedika.Tests;

[Collection("DatabaseSettingsStore")]
public sealed class DatabaseSettingsStoreTests : IDisposable
{
    private readonly string _original = DatabaseSettingsStore.FilePath;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "simrs-store-" + Guid.NewGuid().ToString("N"));

    public DatabaseSettingsStoreTests()
    {
        DatabaseSettingsStore.FilePath = Path.Combine(_dir, "database.settings.json");
    }

    public void Dispose()
    {
        DatabaseSettingsStore.FilePath = _original;
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    [Fact]
    public void NothingSaved_ReturnsNull()
    {
        Assert.False(DatabaseSettingsStore.HasSaved);
        Assert.Null(DatabaseSettingsStore.TryLoad());
    }

    [Fact]
    public void LegacySingleObjectFile_IsReadAsDefaultProfile()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(DatabaseSettingsStore.FilePath,
            """{ "Host": "db-server", "Port": 3307, "Database": "sik", "User": "root", "Password": "x", "AktifkanBatchObat": true, "HppFarmasi": "h_beli" }""");

        var s = DatabaseSettingsStore.TryLoad();

        Assert.NotNull(s);
        Assert.Equal("Default", s!.Name);
        Assert.Equal("db-server", s.Host);
        Assert.Equal(3307, s.Port);
        Assert.True(s.AktifkanBatchObat);
        Assert.Equal("h_beli", s.HppColumn);
    }

    [Fact]
    public void SaveAndSetActive_SwitchesProfile()
    {
        DatabaseSettingsStore.Save(new DatabaseSettings { Name = "Utama", Host = "10.0.0.1" });
        DatabaseSettingsStore.Save(new DatabaseSettings { Name = "Uji", Host = "10.0.0.2" });
        Assert.Equal("Uji", DatabaseSettingsStore.TryLoad()!.Name);

        DatabaseSettingsStore.SetActive("Utama");
        Assert.Equal("10.0.0.1", DatabaseSettingsStore.TryLoad()!.Host);
        Assert.Equal(2, DatabaseSettingsStore.LoadAll().Items.Count);

        // Saving an existing name replaces it instead of adding a duplicate.
        DatabaseSettingsStore.Save(new DatabaseSettings { Name = "Uji", Host = "10.0.0.3" });
        var all = DatabaseSettingsStore.LoadAll();
        Assert.Equal(2, all.Items.Count);
        Assert.Equal("10.0.0.3", all.Items.Single(i => i.Name == "Uji").Host);
    }

    [Fact]
    public void ConnectionString_QuotesSpecialCharacters()
    {
        var cs = new DatabaseSettings { Host = "h", Password = "a;b=c\"d" }.ToConnectionString();
        var b = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = cs };
        Assert.Equal("a;b=c\"d", b["Password"]);
        Assert.Equal("h", b["Server"]);
    }
}
