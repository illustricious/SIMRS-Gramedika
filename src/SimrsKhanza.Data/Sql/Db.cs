using System.Globalization;
using MySqlConnector;
using SimrsKhanza.Config;

namespace SimrsKhanza.Data.Sql;

/// <summary>One result row; values by column name (case-insensitive).</summary>
public sealed class Row(Dictionary<string, object?> values)
{
    public object? this[string col] => values.TryGetValue(col, out var v) ? v : null;

    public string S(string col) => this[col] switch
    {
        null => "",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd") : d.ToString("yyyy-MM-dd HH:mm:ss"),
        double x => x.ToString(CultureInfo.InvariantCulture),
        var v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
    };

    public double D(string col) => this[col] switch
    {
        null => 0,
        string s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : 0,
        var v => Convert.ToDouble(v, CultureInfo.InvariantCulture),
    };

    public DateTime? Dt(string col) => this[col] is DateTime d ? d : null;
}

/// <summary>
/// Thin ADO.NET helper over MySqlConnector for the legacy `sik` tables that have no
/// EF mapping. Every value goes through parameters; identifiers are only ever
/// compile-time constants from the calling repository.
/// </summary>
public sealed class Db : IAsyncDisposable
{
    public MySqlConnection Conn { get; }
    public MySqlTransaction? Tx { get; private set; }

    private Db(MySqlConnection conn) => Conn = conn;

    public static async Task<Db> OpenAsync(DatabaseSettings settings, CancellationToken ct = default)
    {
        var conn = new MySqlConnection(settings.ToConnectionString());
        await conn.OpenAsync(ct);
        return new Db(conn);
    }

    public async Task BeginAsync(CancellationToken ct = default) => Tx = await Conn.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (Tx is null) return;
        await Tx.CommitAsync(ct);
        await Tx.DisposeAsync();
        Tx = null;
    }

    /// <summary>Rolls back and discards the current transaction (e.g. before retrying with a fresh number).</summary>
    public async Task DisposeTxAsync()
    {
        if (Tx is null) return;
        try { await Tx.RollbackAsync(); } catch { /* already gone */ }
        await Tx.DisposeAsync();
        Tx = null;
    }

    private MySqlCommand Cmd(string sql, object?[] args)
    {
        var cmd = new MySqlCommand(sql, Conn, Tx);
        for (var i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue("@p" + i, args[i] ?? DBNull.Value);
        return cmd;
    }

    /// <summary>Parameters are positional: @p0, @p1, ...</summary>
    public async Task<List<Row>> QueryAsync(string sql, params object?[] args)
    {
        await using var cmd = Cmd(sql, args);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Row>();
        while (await r.ReadAsync())
        {
            var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < r.FieldCount; i++)
                d[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(new Row(d));
        }
        return list;
    }

    public async Task<Row?> FirstAsync(string sql, params object?[] args) => (await QueryAsync(sql, args)).FirstOrDefault();

    public async Task<int> ExecAsync(string sql, params object?[] args)
    {
        await using var cmd = Cmd(sql, args);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql, params object?[] args)
    {
        await using var cmd = Cmd(sql, args);
        var v = await cmd.ExecuteScalarAsync();
        return v is DBNull ? null : v;
    }

    public async Task<long> CountAsync(string sql, params object?[] args) => Convert.ToInt64(await ScalarAsync(sql, args) ?? 0L);

    public async Task<string> StrAsync(string sql, params object?[] args) => Convert.ToString(await ScalarAsync(sql, args), CultureInfo.InvariantCulture) ?? "";

    /// <summary>Legacy Valid.autoNomer3: prefix + zero-padded (MAX(RIGHT(col,len))+1).</summary>
    public async Task<string> AutoNomorAsync(string maxSql, string prefix, int len, params object?[] args)
        => prefix + (await CountAsync(maxSql, args) + 1).ToString(new string('0', len));

    /// <summary>Serialize single-row settings writes across Gramedika clients, including legacy MyISAM tables.</summary>
    internal async Task WithSettingsLockAsync(string table, Func<Task> write)
    {
        var identity = $"simrs.setting.{Conn.Database}.{table}";
        var lockName = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
        if (await CountAsync("SELECT GET_LOCK(@p0,5)", lockName) != 1)
            throw new InvalidOperationException("Pengaturan sedang disimpan oleh pengguna lain.");
        try { await write(); }
        finally { await ScalarAsync("SELECT RELEASE_LOCK(@p0)", lockName); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Tx is not null) { try { await Tx.RollbackAsync(); } catch { /* connection gone */ } await Tx.DisposeAsync(); }
        await Conn.DisposeAsync();
    }
}
