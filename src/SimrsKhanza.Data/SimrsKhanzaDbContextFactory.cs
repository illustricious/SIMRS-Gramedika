using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Config;

namespace SimrsKhanza.Data;

public static class SimrsKhanzaDbContextFactory
{
    public static SimrsKhanzaDbContext Create(DatabaseSettings settings)
    {
        var options = new DbContextOptionsBuilder<SimrsKhanzaDbContext>()
            .UseMySql(settings.ToConnectionString(), ServerVersion.AutoDetect(settings.ToConnectionString()))
            .Options;
        return new SimrsKhanzaDbContext(options);
    }

    /// <summary>
    /// Builds the context without contacting a server (skips ServerVersion.AutoDetect,
    /// which needs a live connection) — used to validate the EF Core model/mapping
    /// in environments with no MySQL server available, e.g. this scaffold's CI/dev box.
    /// </summary>
    public static SimrsKhanzaDbContext CreateWithoutServerProbe(DatabaseSettings settings)
    {
        var options = new DbContextOptionsBuilder<SimrsKhanzaDbContext>()
            .UseMySql(settings.ToConnectionString(), new MySqlServerVersion(new Version(8, 0, 34)))
            .Options;
        return new SimrsKhanzaDbContext(options);
    }
}
