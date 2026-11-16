using SimrsKhanza.Config;
using SimrsKhanza.Data.Kepegawaian;
using SimrsKhanza.Data.LabKesling;
using SimrsKhanza.Data.Master;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Tests;

public class MenuCSpecsTests
{
    public static IEnumerable<object[]> MasterKeys() => MenuCSpecs.Masters.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(MasterKeys))]
    public void MasterSpec_IsConsistent(string key)
    {
        var s = MenuCSpecs.Masters[key];
        Assert.NotEmpty(s.Keys);
        Assert.NotEmpty(s.Display);
        Assert.Equal(s.Fields.Length, s.Fields.Select(f => f.Column).Distinct().Count());
        Assert.All(s.Fields.Where(f => f.Kind == FieldKind.Choice), f => Assert.NotEmpty(f.Choices!));
        Assert.All(s.Fields.Where(f => f.Kind == FieldKind.Lookup), f => Assert.False(string.IsNullOrWhiteSpace(f.LookupSql)));
        if (s.AutoKeyPrefix is not null)
        {
            Assert.Contains(s.Fields, f => f.AutoKey);
            Assert.Contains(s.Fields, f => f.Column == s.AutoKeyDateField && f.Kind == FieldKind.Date);
        }
        if (s.ScoreOf is not null) Assert.All(s.ScoreOf, c => Assert.Contains(s.Fields, f => f.Column == c));
    }

    [Fact]
    public void EveryMenuCTile_HasAScreen()
    {
        string[] custom = ["btnPenggajian", "btnSKPPenilaianPegawai", "btnSKPRekapitulasiPenilaian"];
        var tiles = SimrsKhanza.UI.Modules.Shared.LegacyMenuCatalog.Tiles[2].Select(t => t.Key);
        var missing = tiles.Where(k => !MenuCSpecs.Masters.ContainsKey(k) && !MenuCSpecs.Reports.ContainsKey(k) && !custom.Contains(k)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void AuditScore_IgnoresNa()
    {
        var sql = MasterLookups.ScoreSql("a", ["x", "y"]);
        Assert.Contains("IN ('Ya','Tidak')", sql);
    }
}

/// <summary>
/// Runs every menu [C] (and Lab Kesling) list/report query and checks every form column against the
/// configured database. Opt-in: set SIMRS_DB_SMOKE=1 (uses the saved connection profile).
/// </summary>
[Collection("DatabaseSettingsStore")] // DatabaseSettingsStoreTests swap the settings file path; don't overlap
public class MenuCSpecsDbSmokeTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("SIMRS_DB_SMOKE") == "1";

    [Fact]
    public async Task AllSpecs_RunAgainstDatabase()
    {
        if (!Enabled) return;
        var settings = DatabaseSettings.Load();
        var repo = new MasterRepository(settings);
        var reports = new ReportRepository(settings);
        var errors = new List<string>();

        await using var db = await Db.OpenAsync(settings);
        var columns = (await db.QueryAsync("SELECT table_name AS t, column_name AS c FROM information_schema.columns WHERE table_schema=DATABASE()"))
            .Select(r => (r.S("t").ToLowerInvariant(), r.S("c").ToLowerInvariant())).ToHashSet();

        MasterSpec[] lab = [LabKeslingMasters.Pelanggan, LabKeslingMasters.Sampel, LabKeslingMasters.Parameter, LabKeslingMasters.NilaiNormal];
        foreach (var (key, s) in MenuCSpecs.Masters.Select(kv => (kv.Key, kv.Value)).Concat(lab.Select(l => (l.Table, l))))
        {
            foreach (var f in s.Fields.Where(f => !columns.Contains((s.Table, f.Column.ToLowerInvariant()))))
                errors.Add($"{key}: kolom {s.Table}.{f.Column} tidak ada");
            try
            {
                var from = DateTime.Today.AddYears(-1);
                await repo.ListAsync(s, "", from, DateTime.Today);
                foreach (var f in s.Fields.Where(f => f.LookupSql is not null)) await repo.LookupAsync(f.LookupSql!);
                if (s.AutoKeySql is not null) Assert.NotEmpty(await repo.NextKeyAsync(s, DateTime.Today));
            }
            catch (Exception ex) { errors.Add($"{key}: {ex.Message}"); }
        }
        foreach (var (key, r) in MenuCSpecs.Reports)
        {
            try { await reports.RunAsync(r, "", DateTime.Today.AddMonths(-1), DateTime.Today, DateTime.Today.Year); }
            catch (Exception ex) { errors.Add($"{key}: {ex.Message}"); }
        }

        var skp = new SkpRepository(settings);
        try
        {
            await skp.KriteriaAsync();
            await skp.ListPenilaianAsync("", DateTime.Today.AddYears(-1), DateTime.Today);
            await skp.ListRekapAsync("", DateTime.Today.AddYears(-1), DateTime.Today);
            await skp.HitungAsync("-", DateTime.Today.AddYears(-1), DateTime.Today);
            await skp.NextNomorPenilaianAsync(DateTime.Today);
            await skp.NextNomorRekapAsync(DateTime.Today);
        }
        catch (Exception ex) { errors.Add($"SKP: {ex.Message}"); }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}
