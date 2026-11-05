using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;

namespace SimrsKhanza.Tests;

/// <summary>
/// Real MySQL/MariaDB integration test for PasienView's Simpan path — the
/// riskiest insert built so far (enum conversions for jk/gol_darah/
/// stts_nikah/keluarga, plus several NOT NULL FK columns to the lookup
/// tables seeded per docs/audit/HANDOVER.md). Skips itself when no server
/// is reachable.
/// </summary>
public class PasienRepositoryIntegrationTests
{
    [Fact]
    public async Task SaveNewAsync_WritesPatientThatSearchAsyncThenFinds()
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
            var repo = new PasienRepository(db);
            var noRm = await repo.NextNoRekamMedisAsync();
            var uniqueName = $"PASIEN TEST INTEGRASI {Guid.NewGuid():N}".Substring(0, 36);

            await repo.SaveNewAsync(new Core.Entities.Pasien
            {
                NoRekamMedis = noRm,
                NamaPasien = uniqueName,
                JenisKelamin = JenisKelamin.Perempuan,
                GolonganDarah = GolonganDarah.O,
                NamaIbu = "IBU TEST",
                StatusPernikahan = Core.Entities.StatusPernikahan.BelumMenikah,
                TanggalDaftar = DateOnly.FromDateTime(DateTime.Today),
                Email = "test@example.test",
                KodePenjab = "UMU",
                Keluarga = HubunganPenanggungJawab.DiriSendiri,
                NamaKeluarga = uniqueName,
            });

            var found = await repo.SearchAsync(uniqueName);
            Assert.Contains(found, p => p.NoRekamMedis == noRm && p.NamaPasien == uniqueName);
            Assert.Equal(JenisKelamin.Perempuan, found.Single(p => p.NoRekamMedis == noRm).JenisKelamin);
        }
    }
}
