using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

public record PasienKamarInapItem(string NoRawat, string NamaPasien, string KodeKamar, string? NamaBangsal, DateOnly TanggalMasuk);

public record PenggunaanKamarItem(string KodeKamar, string? NamaBangsal, KelasKamar Kelas, StatusKamar Status);

public record StokDarahItem(GolonganDarahUtd GolonganDarah, ResusDarah? Resus, int JumlahKantong);

public record TelusurKunjunganItem(string NoRawat, string NoRekamMedis, string NamaPasien, string? NamaPoli, DateOnly? TanggalRegistrasi);

public interface IInformasiRepository
{
    Task<IReadOnlyList<PasienKamarInapItem>> GetPasienKamarInapAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PenggunaanKamarItem>> GetPenggunaanKamarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StokDarahItem>> GetStokDarahAsync(CancellationToken ct = default);

    /// <summary>Searches reg_periksa by No.RM or nama pasien — requires a non-empty keyword (not date-bounded, but capped at 100 rows).</summary>
    Task<IReadOnlyList<TelusurKunjunganItem>> TelusurKunjunganAsync(string keyword, CancellationToken ct = default);
}

public class InformasiRepository(SimrsKhanzaDbContext db) : IInformasiRepository
{
    public async Task<IReadOnlyList<PasienKamarInapItem>> GetPasienKamarInapAsync(CancellationToken ct = default)
    {
        var query =
            from ki in db.KamarInapSet
            where ki.TanggalKeluar == null
            join r in db.RegPeriksaSet on ki.NoRawat equals r.NoRawat
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join b in db.BangsalSet on ki.Kamar!.KodeBangsal equals b.KodeBangsal into bJoin
            from b in bJoin.DefaultIfEmpty()
            select new PasienKamarInapItem(ki.NoRawat, p.NamaPasien, ki.KodeKamar, b != null ? b.NamaBangsal : null, ki.TanggalMasuk);

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PenggunaanKamarItem>> GetPenggunaanKamarAsync(CancellationToken ct = default)
    {
        var query =
            from k in db.KamarSet
            join b in db.BangsalSet on k.KodeBangsal equals b.KodeBangsal into bJoin
            from b in bJoin.DefaultIfEmpty()
            orderby k.KodeKamar
            select new PenggunaanKamarItem(k.KodeKamar, b != null ? b.NamaBangsal : null, k.Kelas, k.Status);

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StokDarahItem>> GetStokDarahAsync(CancellationToken ct = default)
    {
        var query =
            from u in db.UtdStokDarahSet
            where u.Status == StatusKantongDarah.Ada && u.GolonganDarah != null
            group u by new { u.GolonganDarah, u.Resus } into g
            orderby g.Key.GolonganDarah
            select new StokDarahItem(g.Key.GolonganDarah!.Value, g.Key.Resus, g.Count());

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TelusurKunjunganItem>> TelusurKunjunganAsync(string keyword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return [];

        var query =
            from r in db.RegPeriksaSet
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join poli in db.PoliklinikSet on r.KodePoli equals poli.KodePoli into poliJoin
            from poli in poliJoin.DefaultIfEmpty()
            where p.NamaPasien.Contains(keyword) || r.NoRekamMedis.Contains(keyword) || r.NoRawat.Contains(keyword)
            orderby r.TanggalRegistrasi descending
            select new TelusurKunjunganItem(r.NoRawat, r.NoRekamMedis, p.NamaPasien, poli != null ? poli.NamaPoli : null, r.TanggalRegistrasi);

        return await query.Take(100).ToListAsync(ct);
    }
}
