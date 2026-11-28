using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SimrsGramedika.Core.Entities;

namespace SimrsGramedika.Data;

public interface IPasienRepository
{
    Task<IReadOnlyList<Pasien>> SearchAsync(string keyword, CancellationToken ct = default);

    /// <summary>Generates a preview of the next no_rkm_medis using Khanza's numbering settings.</summary>
    Task<string> NextNoRekamMedisAsync(CancellationToken ct = default);

    /// <param name="sebelumSimpan">Langkah tambahan di dalam transaksi yang sama, misalnya kode wilayah.</param>
    /// <param name="sesudahSimpan">Langkah di dalam transaksi yang sama yang memerlukan baris pasien sudah ada.</param>
    Task SaveNewAsync(Pasien pasien, Func<CancellationToken, Task>? sebelumSimpan = null, CancellationToken ct = default,
        Func<CancellationToken, Task>? sesudahSimpan = null);
}

public class PasienRepository(SimrsGramedikaDbContext db) : IPasienRepository
{
    public async Task<IReadOnlyList<Pasien>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        var query = db.PasienSet.AsQueryable();

        var term = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(p => p.NamaPasien.Contains(term)
                || p.NoRekamMedis.Contains(term)
                || (p.NoKtp != null && p.NoKtp.Contains(term))
                || (p.NoTelepon != null && p.NoTelepon.Contains(term))
                || p.NamaIbu.Contains(term)
                || (p.Alamat != null && p.Alamat.Contains(term))
                || (p.Email != null && p.Email.Contains(term)));
        }

        return await query.OrderByDescending(p => p.TanggalDaftar).Take(100).ToListAsync(ct);
    }

    public async Task<string> NextNoRekamMedisAsync(CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(ct);
        try
        {
            var settings = await ReadNumberingSettingsAsync(connection, null, ct);
            var last = await ReadLastNumberAsync(connection, null, forUpdate: false, ct);
            return FormatNumber(DecodeSequence(last, settings) + 1, settings, DateTime.Today);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task SaveNewAsync(Pasien pasien, Func<CancellationToken, Task>? sebelumSimpan = null, CancellationToken ct = default,
        Func<CancellationToken, Task>? sesudahSimpan = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var connection = db.Database.GetDbConnection();
        var settings = await ReadNumberingSettingsAsync(connection, transaction.GetDbTransaction(), ct);
        var last = await ReadLastNumberAsync(connection, transaction.GetDbTransaction(), forUpdate: true, ct);
        var sequence = DecodeSequence(last, settings);
        string candidate;
        do
        {
            if (sequence >= 999_999) throw new InvalidOperationException("Urutan nomor RM sudah mencapai batas 999999.");
            candidate = FormatNumber(++sequence, settings, DateTime.Today);
        } while (await db.PasienSet.AnyAsync(p => p.NoRekamMedis == candidate, ct));

        pasien.NoRekamMedis = candidate;
        if (sebelumSimpan is not null) await sebelumSimpan(ct);
        db.PasienSet.Add(pasien);
        await db.SaveChangesAsync(ct);
        if (sesudahSimpan is not null) await sesudahSimpan(ct);

        await db.Database.ExecuteSqlRawAsync("DELETE FROM set_no_rkm_medis", ct);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO set_no_rkm_medis(no_rkm_medis) VALUES ({0})", [candidate], ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task<RmNumberingSettings> ReadNumberingSettingsAsync(DbConnection connection, DbTransaction? transaction, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT urutan, tahun, bulan, posisi_tahun_bulan FROM set_urut_no_rkm_medis LIMIT 1";
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Pengaturan nomor RM Khanza belum tersedia di set_urut_no_rkm_medis.");

        var settings = new RmNumberingSettings(reader.GetString(0), reader.GetString(1) == "Yes", reader.GetString(2) == "Yes",
            reader.IsDBNull(3) ? "" : reader.GetString(3));
        if (settings.Order is not ("Straight" or "Terminal" or "Middle") || settings.YearMonthPosition is not ("Depan" or "Belakang"))
            throw new InvalidOperationException("Pengaturan urutan nomor RM Khanza tidak valid.");
        return settings;
    }

    private static async Task<string?> ReadLastNumberAsync(DbConnection connection, DbTransaction? transaction, bool forUpdate, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT no_rkm_medis FROM set_no_rkm_medis LIMIT 1" + (forUpdate ? " FOR UPDATE" : "");
        var value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static int DecodeSequence(string? lastNumber, RmNumberingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(lastNumber)) return 0;
        var length = Math.Min(6, lastNumber.Length);
        var digits = settings.YearMonthPosition == "Depan"
            ? lastNumber.Substring(lastNumber.Length - length, length)
            : lastNumber[..length];
        if (digits.Length != 6 || !digits.All(char.IsDigit))
            throw new InvalidOperationException("Nomor terakhir di set_no_rkm_medis tidak sesuai format enam digit Khanza.");

        var plain = settings.Order switch
        {
            "Straight" => digits,
            "Terminal" => digits[4..6] + digits[2..4] + digits[0..2],
            "Middle" => digits[2..4] + digits[0..2] + digits[4..6],
            _ => throw new InvalidOperationException("Urutan nomor RM tidak dikenal.")
        };
        return int.Parse(plain, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(int sequence, RmNumberingSettings settings, DateTime date)
    {
        var plain = sequence.ToString("000000", System.Globalization.CultureInfo.InvariantCulture);
        var ordered = settings.Order switch
        {
            "Straight" => plain,
            "Terminal" => plain[4..6] + plain[2..4] + plain[0..2],
            "Middle" => plain[2..4] + plain[0..2] + plain[4..6],
            _ => throw new InvalidOperationException("Urutan nomor RM tidak dikenal.")
        };
        var yearMonth = (settings.IncludeYear ? date.ToString("yy", System.Globalization.CultureInfo.InvariantCulture) : "")
                      + (settings.IncludeMonth ? date.ToString("MM", System.Globalization.CultureInfo.InvariantCulture) : "");
        return settings.YearMonthPosition == "Depan"
            ? yearMonth + ordered
            : ordered + (yearMonth.Length == 0 ? "" : "-" +
                (settings.IncludeMonth ? date.ToString("MM", System.Globalization.CultureInfo.InvariantCulture) : "") +
                (settings.IncludeYear ? date.ToString("yy", System.Globalization.CultureInfo.InvariantCulture) : ""));
    }

    private sealed record RmNumberingSettings(string Order, bool IncludeYear, bool IncludeMonth, string YearMonthPosition);
}
