using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Config;

namespace SimrsGramedika.Data;

public static class SimrsGramedikaDbContextFactory
{
    public static SimrsGramedikaDbContext Create(DatabaseSettings settings)
    {
        var options = new DbContextOptionsBuilder<SimrsGramedikaDbContext>()
            .UseMySql(settings.ToConnectionString(), ServerVersion.AutoDetect(settings.ToConnectionString()))
            .Options;
        return new SimrsGramedikaDbContext(options);
    }

    /// <summary>
    /// Builds the context without contacting a server (skips ServerVersion.AutoDetect,
    /// which needs a live connection) — used to validate the EF Core model/mapping
    /// in environments with no MySQL server available, e.g. this scaffold's CI/dev box.
    /// </summary>
    public static SimrsGramedikaDbContext CreateWithoutServerProbe(DatabaseSettings settings)
    {
        var options = new DbContextOptionsBuilder<SimrsGramedikaDbContext>()
            .UseMySql(settings.ToConnectionString(), new MySqlServerVersion(new Version(8, 0, 34)))
            .Options;
        return new SimrsGramedikaDbContext(options);
    }
}
