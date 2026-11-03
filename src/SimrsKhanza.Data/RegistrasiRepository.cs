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
    Task<IReadOnlyList<RegistrasiListItem>> GetTodayAsync(CancellationToken ct = default);
}

public class RegistrasiRepository(SimrsKhanzaDbContext db) : IRegistrasiRepository
{
    public async Task<IReadOnlyList<RegistrasiListItem>> GetTodayAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query =
            from r in db.RegPeriksaSet
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join poli in db.PoliklinikSet on r.KodePoli equals poli.KodePoli into poliJoin
            from poli in poliJoin.DefaultIfEmpty()
            join dok in db.DokterSet on r.KodeDokter equals dok.KodeDokter into dokJoin
            from dok in dokJoin.DefaultIfEmpty()
            where r.TanggalRegistrasi == today
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
