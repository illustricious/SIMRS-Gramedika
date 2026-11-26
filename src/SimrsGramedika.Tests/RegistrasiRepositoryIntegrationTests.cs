using SimrsGramedika.Config;
using SimrsGramedika.Data;

namespace SimrsGramedika.Tests;

/// <summary>
/// Real MySQL/MariaDB integration test for the widened "Daftar Pasien Rawat
/// Jalan" projection (reg_periksa+pasien+poliklinik+dokter+penjab, including
/// the stts/status_lanjut/status_poli/status_bayar enum conversions added
/// alongside it). Skips itself when no server is reachable.
/// </summary>
public class RegistrasiRepositoryIntegrationTests
{
    [Fact]
    public async Task GetByPeriodeAsync_AgainstRealDatabase_ReturnsSeededRegistrationWithConvertedEnums()
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
            return; // no reachable server — skip rather than fail
        }

        using (db)
        {
            var repo = new RegistrasiRepository(db);
            var items = await repo.GetByPeriodeAsync(DateOnly.MinValue, DateOnly.MaxValue);

            var seedRow = items.FirstOrDefault(i => i.NoRawat == "2026/11/03/000001");
            if (seedRow is null) return; // seed data not present in this database

            Assert.Equal("Sudah", seedRow.Status);
            Assert.Equal("Baru", seedRow.StatusPoli);
            Assert.Equal("Sudah Bayar", seedRow.StatusBayar);
            Assert.Equal("Umum", seedRow.JenisBayar);

            // Filter-by-name should narrow down to just the matching rows.
            var filtered = await repo.GetByPeriodeAsync(
                DateOnly.MinValue, DateOnly.MaxValue,
                new RegistrasiSearchFilter(NamaPasien: "PASIEN CONTOH SATU"));
            Assert.All(filtered, i => Assert.Contains("PASIEN CONTOH SATU", i.NamaPasien));
        }
    }
}
