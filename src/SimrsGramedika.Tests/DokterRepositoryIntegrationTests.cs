using SimrsGramedika.Config;
using SimrsGramedika.Data;

namespace SimrsGramedika.Tests;

/// <summary>
/// Real MySQL/MariaDB integration test — needs SIMRS_DB_* environment variables
/// pointing at a reachable server (see docs/handover/04-HISTORY.md for local setup).
/// Skips itself (does not fail the suite) when no server is reachable, so CI/dev
/// machines without a database still get a clean test run.
/// </summary>
public class DokterRepositoryIntegrationTests
{
    [Fact]
    public async Task SearchAsync_AgainstRealDatabase_ReturnsSeededDummyDoctors()
    {
        var settings = DatabaseSettings.FromEnvironment();

        SimrsGramedikaDbContext db;
        try
        {
            db = SimrsGramedikaDbContextFactory.Create(settings);
            await db.Database.CanConnectAsync();
        }
        catch
        {
            // No reachable MySQL/MariaDB server configured — skip rather than fail.
            return;
        }

        using (db)
        {
            var repo = new DokterRepository(db);
            var results = await repo.SearchAsync("Contoh");

            Assert.NotEmpty(results);
            Assert.All(results, d => Assert.Contains("seed dev", d.NamaDokter));
        }
    }
}
