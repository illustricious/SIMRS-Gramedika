using System.Globalization;
using SimrsKhanza.Config;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.Master;

public enum FieldKind { Text, Number, Integer, Choice, Lookup, Date, Time, DateTime, Memo }

/// <summary>One editable column of a master table. Column is a compile-time constant (used in SQL).</summary>
public record MasterField(string Column, string Label, FieldKind Kind = FieldKind.Text, int MaxLen = 0,
    string[]? Choices = null, string? LookupSql = null, bool Key = false, bool AutoKey = false, bool Required = false)
{
    /// <summary>Value a new record starts with (instead of empty/0/today).</summary>
    public object? Default { get; init; }
}

/// <summary>A per-row command (e.g. "Proses Pulang"); Sql gets the selected row's key values as @p0.. in key order.</summary>
public record MasterAction(string Label, string Sql, string Confirm);

/// <summary>How the list is filtered besides the search text.</summary>
public enum PeriodFilter { None, DateRange, Month }

/// <summary>
/// A legacy table edited by the one generic list + form screen. ListSql always gets the search
/// text as @p0 ("%cari%"); with a <see cref="Period"/> it also gets @p1/@p2 = first/last day.
/// </summary>
public record MasterSpec(
    string Title, string Table, MasterField[] Fields,
    string ListSql, (string Column, string Header)[] Display,
    string? AutoKeySql = null, int AutoKeyLen = 0,
    string? TotalColumn = null, string[]? TotalOf = null)
{
    public PeriodFilter Period { get; init; }

    /// <summary>Date-based numbering: prefix is a composite format over the date (e.g. "PC{0:yyyyMMdd}"); AutoKeySql gets the date as @p0.</summary>
    public string? AutoKeyPrefix { get; init; }
    public string? AutoKeyDateField { get; init; }

    public bool CanInsert { get; init; } = true;
    public bool CanEdit { get; init; } = true;
    public bool CanDelete { get; init; } = true;
    public string? Hint { get; init; }
    public MasterAction[] Actions { get; init; } = [];

    /// <summary>Choice fields that count toward a "% Ya" compliance score (audit screens).</summary>
    public string[]? ScoreOf { get; init; }

    public IEnumerable<MasterField> Keys => Fields.Where(f => f.Key);
}

/// <summary>Generic CRUD for a <see cref="MasterSpec"/>.</summary>
public class MasterRepository(DatabaseSettings settings)
{
    public async Task<List<Row>> ListAsync(MasterSpec spec, string? cari, DateTime? from = null, DateTime? to = null)
    {
        await using var db = await Db.OpenAsync(settings);
        return spec.Period == PeriodFilter.None
            ? await db.QueryAsync(spec.ListSql, $"%{cari?.Trim()}%")
            : await db.QueryAsync(spec.ListSql, $"%{cari?.Trim()}%",
                (from ?? DateTime.Today).Date, (to ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1));
    }

    public async Task<List<(string Kode, string Nama)>> LookupAsync(string sql)
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync(sql)).Select(r => (r.S("kode"), r.S("nama"))).ToList();
    }

    public async Task<string> NextKeyAsync(MasterSpec spec, DateTime? date = null)
    {
        if (spec.AutoKeySql is null) return "";
        await using var db = await Db.OpenAsync(settings);
        return await NextKeyAsync(db, spec, date);
    }

    private static async Task<string> NextKeyAsync(Db db, MasterSpec spec, DateTime? date)
    {
        if (spec.AutoKeyPrefix is null) return await db.AutoNomorAsync(spec.AutoKeySql!, "", spec.AutoKeyLen);
        var d = (date ?? DateTime.Today).Date;
        return await db.AutoNomorAsync(spec.AutoKeySql!, string.Format(CultureInfo.InvariantCulture, spec.AutoKeyPrefix, d), spec.AutoKeyLen, d);
    }

    private static Dictionary<string, object?> WithTotal(MasterSpec spec, Dictionary<string, object?> values)
    {
        var v = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        if (spec.TotalColumn is not null && spec.TotalOf is not null)
            v[spec.TotalColumn] = spec.TotalOf.Sum(c => v.TryGetValue(c, out var x) && x is double d ? d : 0);
        return v;
    }

    private static bool Blank(object? v) => v is null || (v is string s && string.IsNullOrWhiteSpace(s));

    /// <summary>Insert when <paramref name="originalKeys"/> is null, otherwise update the row having those keys.</summary>
    public async Task<string?> SaveAsync(MasterSpec spec, Dictionary<string, object?> values, Dictionary<string, object?>? originalKeys)
    {
        var v = WithTotal(spec, values);
        await using var db = await Db.OpenAsync(settings);

        // Date-based numbers are re-taken at save time so two users saving at once don't collide.
        if (originalKeys is null && spec.AutoKeyPrefix is not null)
            foreach (var f in spec.Fields.Where(f => f.AutoKey))
                v[f.Column] = await NextKeyAsync(db, spec, v.GetValueOrDefault(spec.AutoKeyDateField ?? "") as DateTime?);

        foreach (var f in spec.Fields.Where(f => f.Key || f.Required))
            if (Blank(v.GetValueOrDefault(f.Column)))
                throw new ArgumentException($"{f.Label} wajib diisi.");

        var cols = v.Keys.Where(k => spec.Fields.Any(f => f.Column.Equals(k, StringComparison.OrdinalIgnoreCase))
                                     || k.Equals(spec.TotalColumn, StringComparison.OrdinalIgnoreCase)).ToList();
        var args = cols.Select(c => v[c]).ToList();

        if (originalKeys is null)
        {
            var keyCols = spec.Keys.Select(f => f.Column).ToList();
            var where = string.Join(" AND ", keyCols.Select((c, i) => $"`{c}`=@p{i}"));
            if (await db.CountAsync($"SELECT COUNT(*) FROM `{spec.Table}` WHERE {where}", keyCols.Select(c => v[c]).ToArray()) > 0)
                throw new InvalidOperationException("Data dengan kode/kunci tersebut sudah ada.");
            var sql = $"INSERT INTO `{spec.Table}` ({string.Join(",", cols.Select(c => $"`{c}`"))}) VALUES ({string.Join(",", cols.Select((_, i) => $"@p{i}"))})";
            await db.ExecAsync(sql, args.ToArray());
        }
        else
        {
            var set = string.Join(",", cols.Select((c, i) => $"`{c}`=@p{i}"));
            var keys = originalKeys.Keys.ToList();
            var where = string.Join(" AND ", keys.Select((c, i) => $"`{c}`=@p{cols.Count + i}"));
            args.AddRange(keys.Select(k => originalKeys[k]));
            if (await db.ExecAsync($"UPDATE `{spec.Table}` SET {set} WHERE {where} LIMIT 1", args.ToArray()) == 0
                && await db.CountAsync($"SELECT COUNT(*) FROM `{spec.Table}` WHERE {string.Join(" AND ", keys.Select((c, i) => $"`{c}`=@p{i}"))}",
                    keys.Select(k => originalKeys[k]).ToArray()) == 0)
                throw new InvalidOperationException("Data yang diubah tidak ditemukan.");
        }
        return spec.Fields.FirstOrDefault(f => f.AutoKey) is { } auto ? Convert.ToString(v[auto.Column]) : null;
    }

    public async Task DeleteAsync(MasterSpec spec, Dictionary<string, object?> keys)
    {
        var k = keys.Keys.ToList();
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync($"DELETE FROM `{spec.Table}` WHERE {string.Join(" AND ", k.Select((c, i) => $"`{c}`=@p{i}"))} LIMIT 1",
            k.Select(c => keys[c]).ToArray());
    }

    public async Task RunActionAsync(MasterAction action, Dictionary<string, object?> keys)
    {
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await db.ExecAsync(action.Sql, keys.Values.ToArray());
        await db.CommitAsync();
    }
}

/// <summary>Builders shared by the menu spec catalogs.</summary>
public static class MasterLookups
{
    // Not filtered on stts_aktif: old records may point at staff who have since left.
    public const string PegawaiNik = "SELECT nik AS kode, nama FROM pegawai ORDER BY nama";
    public const string PegawaiId = "SELECT id AS kode, CONCAT(nama,' (',nik,')') AS nama FROM pegawai ORDER BY nama";
    public const string RuangAudit = "SELECT id_ruang AS kode, nama_ruang AS nama FROM ruang_audit_kepatuhan ORDER BY nama_ruang";

    public static readonly string[] YaTidakNa = ["Ya", "Tidak", "NA"];
    public static readonly string[] YaTidak = ["Ya", "Tidak"];

    public static readonly string[] Shift =
    [
        .. Enumerable.Range(1, 10).Select(i => i == 1 ? "Pagi" : $"Pagi{i}"),
        .. Enumerable.Range(1, 10).Select(i => i == 1 ? "Siang" : $"Siang{i}"),
        .. Enumerable.Range(1, 10).Select(i => i == 1 ? "Malam" : $"Malam{i}"),
        .. Enumerable.Range(1, 10).Select(i => $"Midle Pagi{i}"),
        .. Enumerable.Range(1, 10).Select(i => $"Midle Siang{i}"),
        .. Enumerable.Range(1, 10).Select(i => $"Midle Malam{i}"),
    ];

    public static readonly string[] StatusPresensi =
    [
        "Tepat Waktu", "Terlambat Toleransi", "Terlambat I", "Terlambat II",
        "Tepat Waktu & PSW", "Terlambat Toleransi & PSW", "Terlambat I & PSW", "Terlambat II & PSW",
    ];

    /// <summary>SQL fragment: percentage of "Ya" among the non-NA answers of <paramref name="cols"/> (legacy "Ttl.Nilai(%)").</summary>
    public static string ScoreSql(string alias, IEnumerable<string> cols)
    {
        var c = cols.ToList();
        var ya = string.Join("+", c.Select(x => $"({alias}.`{x}`='Ya')"));
        var n = string.Join("+", c.Select(x => $"({alias}.`{x}` IN ('Ya','Tidak'))"));
        return $"ROUND(IFNULL(({ya})*100/NULLIF({n},0),0),2)";
    }
}
