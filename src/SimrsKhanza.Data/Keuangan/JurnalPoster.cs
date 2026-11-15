using MySqlConnector;

namespace SimrsKhanza.Data.Keuangan;

/// <summary>One posting line, same shape as legacy `tampjurnal` (kd_rek, nm_rek, debet, kredit).</summary>
public record JurnalLine(string KdRek, string NmRek, double Debet, double Kredit);

/// <summary>
/// Port of legacy keuangan/Jurnal.simpanJurnal(): writes one `jurnal` header + its
/// `detailjurnal` rows. Legacy stages lines in the shared `tampjurnal` table first
/// (rows merged per kd_rek); this does the same merge in memory so concurrent users
/// never clobber each other's staging rows. Must run inside the caller's transaction.
///
/// no_jurnal = "JR" + yyyyMMdd + 6-digit counter per tgl_jurnal (legacy autoNomer3).
/// </summary>
public static class JurnalPoster
{
    public static async Task PostAsync(MySqlConnection conn, MySqlTransaction tx, string noBukti, string keterangan,
        IEnumerable<JurnalLine> lines, CancellationToken ct = default)
    {
        var merged = lines
            .Where(l => l.Debet != 0 || l.Kredit != 0)
            .GroupBy(l => l.KdRek)
            .Select(g => new JurnalLine(g.Key, g.First().NmRek, g.Sum(x => x.Debet), g.Sum(x => x.Kredit)))
            .ToList();
        if (merged.Count == 0) return; // legacy: jml=0 → nothing posted, still success

        if (merged.Any(l => string.IsNullOrWhiteSpace(l.KdRek)))
            throw new InvalidOperationException(
                "Rekening akuntansi belum di-setting (set_akun / set_akun2). Atur dulu di Khanza: Keuangan → Pengaturan Rekening.");

        // Legacy compares getInt(debet)==getInt(kredit).
        var debet = merged.Sum(l => l.Debet);
        var kredit = merged.Sum(l => l.Kredit);
        if ((long)debet != (long)kredit)
            throw new InvalidOperationException($"Jurnal tidak seimbang (debet {debet:N0} ≠ kredit {kredit:N0}).");

        DateTime now;
        await using (var c = new MySqlCommand("SELECT NOW()", conn, tx))
            now = Convert.ToDateTime(await c.ExecuteScalarAsync(ct));
        var tgl = now.Date;
        var prefix = "JR" + tgl.ToString("yyyyMMdd");

        string noJurnal;
        await using (var c = new MySqlCommand(
                         "SELECT IFNULL(MAX(CONVERT(RIGHT(no_jurnal,6),SIGNED)),0) FROM jurnal WHERE tgl_jurnal=@t FOR UPDATE", conn, tx))
        {
            c.Parameters.AddWithValue("@t", tgl);
            noJurnal = prefix + (Convert.ToInt64(await c.ExecuteScalarAsync(ct)) + 1).ToString("D6");
        }

        await using (var c = new MySqlCommand("INSERT INTO jurnal VALUES (@no,@bukti,@tgl,@jam,'U',@ket)", conn, tx))
        {
            c.Parameters.AddWithValue("@no", noJurnal);
            c.Parameters.AddWithValue("@bukti", noBukti.Length > 30 ? noBukti[..30] : noBukti);
            c.Parameters.AddWithValue("@tgl", tgl);
            c.Parameters.AddWithValue("@jam", now.TimeOfDay);
            c.Parameters.AddWithValue("@ket", keterangan.Length > 350 ? keterangan[..350] : keterangan);
            await c.ExecuteNonQueryAsync(ct);
        }

        foreach (var l in merged)
        {
            await using var c = new MySqlCommand("INSERT INTO detailjurnal VALUES (@no,@rek,@d,@k)", conn, tx);
            c.Parameters.AddWithValue("@no", noJurnal);
            c.Parameters.AddWithValue("@rek", l.KdRek);
            c.Parameters.AddWithValue("@d", l.Debet);
            c.Parameters.AddWithValue("@k", l.Kredit);
            await c.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Reads one row of a set_akun* table into a column→kd_rek map.</summary>
    public static async Task<Dictionary<string, string>> ReadAkunAsync(MySqlConnection conn, MySqlTransaction? tx, string table, CancellationToken ct = default)
    {
        await using var c = new MySqlCommand($"SELECT * FROM `{table}` LIMIT 1", conn, tx);
        await using var r = await c.ExecuteReaderAsync(ct);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (await r.ReadAsync(ct))
            for (var i = 0; i < r.FieldCount; i++)
                map[r.GetName(i)] = r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)) ?? "";
        return map;
    }
}
