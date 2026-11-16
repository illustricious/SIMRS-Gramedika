using SimrsKhanza.Config;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.Master;

public enum ReportPeriod { DateRange, Month, Year }

/// <summary>
/// A read-only legacy report (rekap presensi, K3 per tahun, ...). Sql gets @p0 = "%cari%";
/// DateRange/Month add @p1/@p2 = first day 00:00 / last day 23:59:59; Year adds @p1 = the year.
/// </summary>
public record ReportSpec(string Title, string Sql, (string Column, string Header)[] Columns, ReportPeriod Period, string? Hint = null);

public class ReportRepository(DatabaseSettings settings)
{
    public async Task<List<Row>> RunAsync(ReportSpec spec, string? cari, DateTime from, DateTime to, int year)
    {
        await using var db = await Db.OpenAsync(settings);
        var like = $"%{cari?.Trim()}%";
        return spec.Period == ReportPeriod.Year
            ? await db.QueryAsync(spec.Sql, like, year)
            : await db.QueryAsync(spec.Sql, like, from.Date, to.Date.AddDays(1).AddSeconds(-1));
    }
}
