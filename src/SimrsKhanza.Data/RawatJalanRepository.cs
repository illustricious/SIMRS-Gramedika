using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

/// <summary>
/// Read-side row for the Rawat Jalan queue: today's/recent outpatient
/// registrations that don't have a `pemeriksaan_ralan` row yet (i.e. the
/// doctor hasn't examined them). Projection, not an EF entity.
/// </summary>
public record RawatJalanQueueItem(
    string NoRawat,
    string NoRekamMedis,
    string NamaPasien,
    DateOnly? TanggalRegistrasi,
    TimeOnly? JamRegistrasi,
    string? NamaPoli);

public interface IRawatJalanRepository
{
    Task<IReadOnlyList<RawatJalanQueueItem>> GetQueueAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>
    /// Saves one examination row. Unlike RegistrasiView's placeholder
    /// Simpan/Baru/Ganti/Hapus buttons, this is a real write against
    /// `pemeriksaan_ralan` — the first non-read-only feature in the scaffold.
    /// </summary>
    Task SaveExaminationAsync(PemeriksaanRalan examination, CancellationToken ct = default);
}

public class RawatJalanRepository(SimrsKhanzaDbContext db) : IRawatJalanRepository
{
    public async Task<IReadOnlyList<RawatJalanQueueItem>> GetQueueAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var examinedNoRawat = db.PemeriksaanRalanSet.Select(p => p.NoRawat);

        var query =
            from r in db.RegPeriksaSet
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join poli in db.PoliklinikSet on r.KodePoli equals poli.KodePoli into poliJoin
            from poli in poliJoin.DefaultIfEmpty()
            where r.TanggalRegistrasi >= fromDate && r.TanggalRegistrasi <= toDate
                  && !examinedNoRawat.Contains(r.NoRawat)
            orderby r.NoRawat
            select new RawatJalanQueueItem(
                r.NoRawat,
                r.NoRekamMedis,
                p.NamaPasien,
                r.TanggalRegistrasi,
                r.JamRegistrasi,
                poli != null ? poli.NamaPoli : null);

        return await query.ToListAsync(ct);
    }

    public async Task SaveExaminationAsync(PemeriksaanRalan examination, CancellationToken ct = default)
    {
        db.PemeriksaanRalanSet.Add(examination);
        await db.SaveChangesAsync(ct);
    }
}
