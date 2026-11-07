using Microsoft.EntityFrameworkCore;

namespace SimrsKhanza.Data;

/// <summary>
/// Billing summary for one visit (no_rawat) — registration fee plus, for an
/// inpatient stay, room cost accumulated across kamar_inap rows for that
/// visit (normally just one, but a "Pindah Kamar" transfer could add more).
/// </summary>
public record KasirBillItem(
    string NoRawat,
    string NoRekamMedis,
    string NamaPasien,
    decimal BiayaReg,
    decimal BiayaKamar,
    decimal Total,
    string? StatusBayar);

public interface IKasirRepository
{
    Task<KasirBillItem?> GetBillAsync(string noRawat, CancellationToken ct = default);

    /// <summary>Marks the visit as paid (reg_periksa.status_bayar = 'Sudah Bayar'). Real write.</summary>
    Task MarkPaidAsync(string noRawat, CancellationToken ct = default);

    /// <summary>Billing summary for every visit registered in [fromDate, toDate] — backs Laporan Pendapatan. Capped at 300 rows.</summary>
    Task<IReadOnlyList<KasirBillItem>> GetLaporanPendapatanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public class KasirRepository(SimrsKhanzaDbContext db) : IKasirRepository
{
    public async Task<KasirBillItem?> GetBillAsync(string noRawat, CancellationToken ct = default)
    {
        var reg = await (
            from r in db.RegPeriksaSet
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            where r.NoRawat == noRawat
            select new { r.NoRawat, r.NoRekamMedis, p.NamaPasien, r.BiayaReg, r.StatusBayar }
        ).FirstOrDefaultAsync(ct);

        if (reg is null) return null;

        var biayaKamar = await db.KamarInapSet
            .Where(k => k.NoRawat == noRawat)
            .Select(k => k.TotalBiaya ?? 0m)
            .SumAsync(ct);

        var biayaReg = reg.BiayaReg ?? 0m;

        return new KasirBillItem(
            reg.NoRawat, reg.NoRekamMedis, reg.NamaPasien,
            biayaReg, biayaKamar, biayaReg + biayaKamar,
            reg.StatusBayar is { } sb ? ToDisplay(sb) : null);
    }

    public async Task MarkPaidAsync(string noRawat, CancellationToken ct = default)
    {
        var reg = await db.RegPeriksaSet.FirstOrDefaultAsync(r => r.NoRawat == noRawat, ct)
            ?? throw new InvalidOperationException($"No.Rawat '{noRawat}' tidak ditemukan.");
        reg.StatusBayar = Core.Entities.StatusBayar.SudahBayar;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<KasirBillItem>> GetLaporanPendapatanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var regs = await (
            from r in db.RegPeriksaSet
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            where r.TanggalRegistrasi >= fromDate && r.TanggalRegistrasi <= toDate
            orderby r.TanggalRegistrasi descending
            select new { r.NoRawat, r.NoRekamMedis, p.NamaPasien, r.BiayaReg, r.StatusBayar }
        ).Take(300).ToListAsync(ct);

        var result = new List<KasirBillItem>(regs.Count);
        foreach (var reg in regs)
        {
            var biayaKamar = await db.KamarInapSet
                .Where(k => k.NoRawat == reg.NoRawat)
                .Select(k => k.TotalBiaya ?? 0m)
                .SumAsync(ct);
            var biayaReg = reg.BiayaReg ?? 0m;

            result.Add(new KasirBillItem(
                reg.NoRawat, reg.NoRekamMedis, reg.NamaPasien,
                biayaReg, biayaKamar, biayaReg + biayaKamar,
                reg.StatusBayar is { } sb ? ToDisplay(sb) : null));
        }

        return result;
    }

    private static string ToDisplay(Core.Entities.StatusBayar v)
        => v == Core.Entities.StatusBayar.SudahBayar ? "Sudah Bayar" : "Belum Bayar";
}
