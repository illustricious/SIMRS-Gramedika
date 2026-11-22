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
        => await UnexaminedVisits(db.RegPeriksaSet.Where(row => row.TanggalRegistrasi >= fromDate
            && row.TanggalRegistrasi <= toDate)).ToListAsync(ct);

    public Task<RawatJalanQueueItem?> GetUnexaminedVisitAsync(string noRawat, CancellationToken ct = default)
        => UnexaminedVisits(db.RegPeriksaSet.Where(row => row.NoRawat == noRawat
            && row.StatusLanjut == StatusLanjut.RawatJalan)).SingleOrDefaultAsync(ct);

    private IQueryable<RawatJalanQueueItem> UnexaminedVisits(IQueryable<RegPeriksa> visits)
    {
        var examinedNoRawat = db.PemeriksaanRalanSet.Select(p => p.NoRawat);

        return
            from r in visits
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join poli in db.PoliklinikSet on r.KodePoli equals poli.KodePoli into poliJoin
            from poli in poliJoin.DefaultIfEmpty()
            where !examinedNoRawat.Contains(r.NoRawat)
            orderby r.NoRawat
            select new RawatJalanQueueItem(
                r.NoRawat,
                r.NoRekamMedis,
                p.NamaPasien,
                r.TanggalRegistrasi,
                r.JamRegistrasi,
                poli != null ? poli.NamaPoli : null);
    }

    public async Task SaveExaminationAsync(PemeriksaanRalan examination, CancellationToken ct = default)
    {
        db.PemeriksaanRalanSet.Add(examination);
        await db.SaveChangesAsync(ct);
    }
}
