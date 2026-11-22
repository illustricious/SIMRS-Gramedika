using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;

namespace SimrsKhanza.Tests;

/// <summary>
/// Real MySQL/MariaDB integration test for the first non-read-only feature in
/// the scaffold: saving an outpatient examination. Needs SIMRS_DB_* pointing
/// at a reachable server with the seed data from docs/handover/04-HISTORY.md
/// (specifically registration "2026/11/03/000002" and pegawai "D0000099").
/// Skips itself when no server is reachable.
/// </summary>
public class RawatJalanRepositoryIntegrationTests
{
    private const string SeedNoRawat = "2026/11/03/000002";

    [Fact]
    public async Task SaveExaminationAsync_WritesRowAndRemovesPatientFromQueue()
    {
        var settings = DatabaseSettings.FromEnvironment();

        SimrsKhanzaDbContext db;
        try
        {
            db = SimrsKhanzaDbContextFactory.Create(settings);
            await db.Database.CanConnectAsync();
        }
        catch
        {
            return; // no reachable server — skip rather than fail
        }

        using (db)
        {
            var repo = new RawatJalanRepository(db);
            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
            var to = DateOnly.FromDateTime(DateTime.Today);

            var beforeQueue = await repo.GetQueueAsync(from, to);
            if (!beforeQueue.Any(q => q.NoRawat == SeedNoRawat))
            {
                // Seed data not present in this database — nothing to verify.
                return;
            }

            await repo.SaveExaminationAsync(new PemeriksaanRalan
            {
                NoRawat = SeedNoRawat,
                TanggalPerawatan = DateOnly.FromDateTime(DateTime.Now),
                JamRawat = TimeOnly.FromDateTime(DateTime.Now),
                Tensi = "120/80",
                Spo2 = "98",
                Kesadaran = TingkatKesadaran.ComposMentis,
                Keluhan = "(integration test row)",
                Rtl = "-",
                Penilaian = "-",
                Instruksi = "-",
                Evaluasi = "-",
                Nip = "D0000099",
            });

            var afterQueue = await repo.GetQueueAsync(from, to);
            Assert.DoesNotContain(afterQueue, q => q.NoRawat == SeedNoRawat);
        }
    }
}
