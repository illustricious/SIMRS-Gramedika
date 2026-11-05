using Microsoft.EntityFrameworkCore;

namespace SimrsKhanza.Data;

/// <summary>
/// Read-side row for the Registrasi list screen (legacy: a plain
/// reg_periksa+pasien+poliklinik+dokter join). Not an EF entity — this is a
/// projection shaped for the grid, not a mapped table.
/// </summary>
public record RegistrasiListItem(
    string NoReg,
    string NoRawat,
    string NoRekamMedis,
    string NamaPasien,
    DateOnly? TanggalRegistrasi,
    TimeOnly? JamRegistrasi,
    string? NamaDokter,
    string? NamaPoli,
    string Status,
    string StatusBayar);

public interface IRegistrasiRepository
{
    /// <summary>Convenience wrapper — kept for callers that only care about "today".</summary>
    Task<IReadOnlyList<RegistrasiListItem>> GetTodayAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists registrations in [from, to] inclusive. Backs the "Periode" date
    /// range filter in RegistrasiView — the legacy screen defaults to today
    /// but lets the user widen the range, which is exactly what tripped up
    /// the first cut of this screen (seed data dated 2026-11-03, queried on
    /// 2026-11-05 with a hardcoded "today" filter returned nothing).
    /// </summary>
    Task<IReadOnlyList<RegistrasiListItem>> GetByPeriodeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public class RegistrasiRepository(SimrsKhanzaDbContext db) : IRegistrasiRepository
{
    public Task<IReadOnlyList<RegistrasiListItem>> GetTodayAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return GetByPeriodeAsync(today, today, ct);
    }

    public async Task<IReadOnlyList<RegistrasiListItem>> GetByPeriodeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var query =
            from r in db.RegPeriksaSet
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join poli in db.PoliklinikSet on r.KodePoli equals poli.KodePoli into poliJoin
            from poli in poliJoin.DefaultIfEmpty()
            join dok in db.DokterSet on r.KodeDokter equals dok.KodeDokter into dokJoin
            from dok in dokJoin.DefaultIfEmpty()
            where r.TanggalRegistrasi >= fromDate && r.TanggalRegistrasi <= toDate
            orderby r.NoRawat
            select new
            {
                r.NoRawat,
                r.NoRekamMedis,
                p.NamaPasien,
                r.TanggalRegistrasi,
                r.JamRegistrasi,
                DokterNama = dok != null ? dok.NamaDokter : null,
                PoliNama = poli != null ? poli.NamaPoli : null,
            };

        var rows = await query.ToListAsync(ct);

        return rows.Select((row, i) => new RegistrasiListItem(
            NoReg: (i + 1).ToString("000"),
            NoRawat: row.NoRawat,
            NoRekamMedis: row.NoRekamMedis,
            NamaPasien: row.NamaPasien,
            TanggalRegistrasi: row.TanggalRegistrasi,
            JamRegistrasi: row.JamRegistrasi,
            NamaDokter: row.DokterNama,
            NamaPoli: row.PoliNama,
            Status: "-",
            StatusBayar: "-")).ToList();
    }
}
