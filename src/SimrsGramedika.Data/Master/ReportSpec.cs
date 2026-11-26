using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Master;

public enum ReportPeriod { DateRange, Month, Year, None }

/// <summary>
/// A read-only legacy report (rekap presensi, K3 per tahun, stok farmasi, ...). Sql gets @p0 = "%cari%";
/// DateRange/Month add @p1/@p2 = first day 00:00 / last day 23:59:59; Year adds @p1 = the year.
/// "{hpp}" in Sql is replaced by the profile's HPP column (legacy HPPFARMASI: dasar | h_beli).
/// </summary>
public record ReportSpec(string Title, string Sql, (string Column, string Header)[] Columns, ReportPeriod Period, string? Hint = null)
{
    public string? VisitSql { get; init; }
    public string SearchWatermark { get; init; } = "Cari...";
    public string[] TotalColumns { get; init; } = [];
}

public class ReportRepository(DatabaseSettings settings)
{
    public static string Resolve(ReportSpec spec, DatabaseSettings settings) => spec.Sql.Replace("{hpp}", settings.HppColumn);

    public async Task<List<Row>> RunForVisitAsync(ReportSpec spec, string noRawat, string? search)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noRawat);
        var sql = spec.VisitSql ?? throw new InvalidOperationException("Laporan tidak mendukung filter kunjungan.");
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync(sql, $"%{search?.Trim()}%", noRawat);
    }

    public async Task<List<Row>> RunAsync(ReportSpec spec, string? cari, DateTime from, DateTime to, int year)
    {
        await using var db = await Db.OpenAsync(settings);
        var like = $"%{cari?.Trim()}%";
        var sql = Resolve(spec, settings);
        return spec.Period switch
        {
            ReportPeriod.Year => await db.QueryAsync(sql, like, year),
            ReportPeriod.None => await db.QueryAsync(sql, like),
            _ => await db.QueryAsync(sql, like, from.Date, to.Date.AddDays(1).AddSeconds(-1)),
        };
    }
}
