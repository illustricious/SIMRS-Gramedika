using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

/// <summary>Row for the "kamar kosong" picker shown when admitting a patient.</summary>
public record KamarTersediaItem(string KodeKamar, string? NamaBangsal, KelasKamar Kelas, decimal TarifKamar);

/// <summary>Row for the "sedang dirawat" list — active admissions (tgl_keluar still null).</summary>
public record RawatInapAktifItem(
    string NoRawat,
    string NoRekamMedis,
    string NamaPasien,
    string KodeKamar,
    string? NamaBangsal,
    DateOnly TanggalMasuk,
    TimeOnly JamMasuk,
    string? DiagnosaAwal);

public interface IRawatInapRepository
{
    Task<IReadOnlyList<KamarTersediaItem>> GetAvailableKamarAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RawatInapAktifItem>> GetActiveInpatientsAsync(CancellationToken ct = default);

    /// <summary>
    /// Admits a patient already registered (reg_periksa row with that NoRawat
    /// must exist) into a room: inserts kamar_inap, flips the room to "ISI",
    /// and flips reg_periksa.StatusLanjut to RawatInap.
    /// </summary>
    Task AdmitAsync(string noRawat, string kodeKamar, string? diagnosaAwal, CancellationToken ct = default);

    /// <summary>
    /// Discharges the patient's active stay: stamps tgl_keluar/jam_keluar,
    /// computes Lama (days, minimum 1) and TotalBiaya (Lama * TarifKamar),
    /// frees the room back to "KOSONG". Returns the computed room bill.
    /// </summary>
    Task<decimal> DischargeAsync(string noRawat, StatusPulang statusPulang, string? diagnosaAkhir, CancellationToken ct = default);
}

public class RawatInapRepository(SimrsKhanzaDbContext db) : IRawatInapRepository
{
    public async Task<IReadOnlyList<KamarTersediaItem>> GetAvailableKamarAsync(CancellationToken ct = default)
    {
        var query =
            from k in db.KamarSet
            join b in db.BangsalSet on k.KodeBangsal equals b.KodeBangsal into bJoin
            from b in bJoin.DefaultIfEmpty()
            where k.Status == StatusKamar.Kosong
            select new KamarTersediaItem(k.KodeKamar, b != null ? b.NamaBangsal : null, k.Kelas, k.TarifKamar);

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RawatInapAktifItem>> GetActiveInpatientsAsync(CancellationToken ct = default)
    {
        var query =
            from ki in db.KamarInapSet
            where ki.TanggalKeluar == null
            join r in db.RegPeriksaSet on ki.NoRawat equals r.NoRawat
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join b in db.BangsalSet on ki.Kamar!.KodeBangsal equals b.KodeBangsal into bJoin
            from b in bJoin.DefaultIfEmpty()
            orderby ki.TanggalMasuk descending, ki.JamMasuk descending
            select new RawatInapAktifItem(
                ki.NoRawat, r.NoRekamMedis, p.NamaPasien, ki.KodeKamar,
                b != null ? b.NamaBangsal : null, ki.TanggalMasuk, ki.JamMasuk, ki.DiagnosaAwal);

        return await query.ToListAsync(ct);
    }

    public async Task AdmitAsync(string noRawat, string kodeKamar, string? diagnosaAwal, CancellationToken ct = default)
    {
        var kamar = await db.KamarSet.FirstOrDefaultAsync(k => k.KodeKamar == kodeKamar, ct)
            ?? throw new InvalidOperationException($"Kamar '{kodeKamar}' tidak ditemukan.");
        var reg = await db.RegPeriksaSet.FirstOrDefaultAsync(r => r.NoRawat == noRawat, ct)
            ?? throw new InvalidOperationException($"No.Rawat '{noRawat}' tidak ditemukan — pasien harus registrasi dulu.");

        db.KamarInapSet.Add(new KamarInap
        {
            NoRawat = noRawat,
            KodeKamar = kodeKamar,
            TarifKamar = kamar.TarifKamar,
            TanggalMasuk = DateOnly.FromDateTime(DateTime.Now),
            JamMasuk = TimeOnly.FromDateTime(DateTime.Now),
            DiagnosaAwal = diagnosaAwal,
            StatusPulang = StatusPulang.BelumPulang,
        });

        kamar.Status = StatusKamar.Isi;
        reg.StatusLanjut = StatusLanjut.RawatInap;

        await db.SaveChangesAsync(ct);
    }

    public async Task<decimal> DischargeAsync(string noRawat, StatusPulang statusPulang, string? diagnosaAkhir, CancellationToken ct = default)
    {
        var stay = await db.KamarInapSet
            .Where(k => k.NoRawat == noRawat && k.TanggalKeluar == null)
            .OrderByDescending(k => k.TanggalMasuk).ThenByDescending(k => k.JamMasuk)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Tidak ada rawat inap aktif untuk No.Rawat '{noRawat}'.");

        var keluar = DateOnly.FromDateTime(DateTime.Now);
        var lama = Math.Max(1, keluar.DayNumber - stay.TanggalMasuk.DayNumber);
        var tarif = stay.TarifKamar ?? 0m;
        var total = lama * tarif;

        stay.TanggalKeluar = keluar;
        stay.JamKeluar = TimeOnly.FromDateTime(DateTime.Now);
        stay.Lama = lama;
        stay.TotalBiaya = total;
        stay.StatusPulang = statusPulang;
        stay.DiagnosaAkhir = diagnosaAkhir;

        var kamar = await db.KamarSet.FirstOrDefaultAsync(k => k.KodeKamar == stay.KodeKamar, ct);
        if (kamar is not null) kamar.Status = StatusKamar.Kosong;

        await db.SaveChangesAsync(ct);
        return total;
    }
}
