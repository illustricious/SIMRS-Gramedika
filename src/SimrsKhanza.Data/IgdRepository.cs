using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

/// <summary>Row for the "belum triase" IGD queue.</summary>
public record IgdAntrianItem(
    string NoRawat, string NoRekamMedis, string NamaPasien,
    DateOnly? TanggalRegistrasi, TimeOnly? JamRegistrasi, string? NamaPoli);

public interface IIgdRepository
{
    /// <summary>reg_periksa rows at an IGD/UGD poliklinik with no data_triase_igd row yet.</summary>
    Task<IReadOnlyList<IgdAntrianItem>> GetAntrianBelumTriaseAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    Task<IReadOnlyList<MasterTriaseKasus>> GetMacamKasusAsync(CancellationToken ct = default);

    /// <summary>Real INSERT into data_triase_igd.</summary>
    Task SaveTriaseAsync(TriaseIgd triase, CancellationToken ct = default);
}

public class IgdRepository(SimrsKhanzaDbContext db) : IIgdRepository
{
    /// <summary>
    /// Khanza's "IGD" isn't a separate patient-type flag — it's just a
    /// poliklinik like any other (kd_poli). These are the codes whose
    /// nm_poli matched "IGD"/"UGD"/"GAWAT" on the real DB when this was
    /// written (see docs/audit/HANDOVER.md) — hardcoded rather than a
    /// nm_poli LIKE query so this stays stable if someone renames a poli.
    /// </summary>
    private static readonly string[] IgdKodePoli = ["IGD", "U0020", "IGD2", "IGD3", "IGDK"];

    public async Task<IReadOnlyList<IgdAntrianItem>> GetAntrianBelumTriaseAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var sudahTriase = db.TriaseIgdSet.Select(t => t.NoRawat);

        var query =
            from r in db.RegPeriksaSet
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join poli in db.PoliklinikSet on r.KodePoli equals poli.KodePoli into poliJoin
            from poli in poliJoin.DefaultIfEmpty()
            where r.TanggalRegistrasi >= fromDate && r.TanggalRegistrasi <= toDate
                  && r.KodePoli != null && IgdKodePoli.Contains(r.KodePoli)
                  && !sudahTriase.Contains(r.NoRawat)
            orderby r.TanggalRegistrasi descending, r.JamRegistrasi descending
            select new IgdAntrianItem(
                r.NoRawat, r.NoRekamMedis, p.NamaPasien,
                r.TanggalRegistrasi, r.JamRegistrasi, poli != null ? poli.NamaPoli : null);

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MasterTriaseKasus>> GetMacamKasusAsync(CancellationToken ct = default)
        => await db.MasterTriaseKasusSet.OrderBy(m => m.MacamKasus).ToListAsync(ct);

    public async Task SaveTriaseAsync(TriaseIgd triase, CancellationToken ct = default)
    {
        db.TriaseIgdSet.Add(triase);
        await db.SaveChangesAsync(ct);
    }
}
