using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

public record ResepListItem(
    string NoResep, string NoRawat, string NamaPasien, string? NamaDokter,
    DateOnly? TanggalPeresepan, TimeOnly? JamPeresepan);

public record ResepItemLine(string KodeBarang, string? NamaBarang, decimal? Jumlah, string? AturanPakai);

public record PenjualanListItem(
    string NoKeluar, DateOnly Tanggal, string Keterangan,
    string? NamaBarang, decimal Jumlah, decimal Total);

public interface IFarmasiRepository
{
    Task<IReadOnlyList<DataBarang>> SearchBarangAsync(string keyword, CancellationToken ct = default);

    Task<IReadOnlyList<ResepListItem>> GetResepDokterAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<IReadOnlyList<ResepItemLine>> GetResepItemsAsync(string noResep, CancellationToken ct = default);

    /// <summary>Real INSERT into pengeluaran_obat_bhp + one detail_pengeluaran_obat_bhp line. Returns the generated NoKeluar.</summary>
    Task<string> InputPenjualanAsync(string kodeBarang, decimal jumlah, string keterangan, string? noFaktur, string nip, CancellationToken ct = default);

    Task<IReadOnlyList<PenjualanListItem>> GetPenjualanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public class FarmasiRepository(SimrsKhanzaDbContext db) : IFarmasiRepository
{
    public async Task<IReadOnlyList<DataBarang>> SearchBarangAsync(string keyword, CancellationToken ct = default)
    {
        var query = db.DataBarangSet.Where(d => d.Aktif).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(d => d.NamaBarang != null && d.NamaBarang.Contains(keyword)
                                   || d.KodeBarang.Contains(keyword));
        }
        return await query.OrderBy(d => d.NamaBarang).Take(50).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ResepListItem>> GetResepDokterAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var query =
            from r in db.ResepObatSet
            join reg in db.RegPeriksaSet on r.NoRawat equals reg.NoRawat into regJoin
            from reg in regJoin.DefaultIfEmpty()
            where r.TanggalPeresepan >= fromDate && r.TanggalPeresepan <= toDate
            orderby r.TanggalPeresepan descending, r.JamPeresepan descending
            select new ResepListItem(
                r.NoResep, r.NoRawat, reg != null && reg.Pasien != null ? reg.Pasien.NamaPasien : "",
                reg != null && reg.Dokter != null ? reg.Dokter.NamaDokter : null,
                r.TanggalPeresepan, r.JamPeresepan);

        return await query.Take(200).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ResepItemLine>> GetResepItemsAsync(string noResep, CancellationToken ct = default)
    {
        var query =
            from item in db.ResepDokterItemSet.AsNoTracking()
            join b in db.DataBarangSet on item.KodeBarang equals b.KodeBarang into bJoin
            from b in bJoin.DefaultIfEmpty()
            where item.NoResep == noResep
            select new ResepItemLine(item.KodeBarang, b != null ? b.NamaBarang : null, item.Jumlah, item.AturanPakai);

        return await query.ToListAsync(ct);
    }

    public async Task<string> InputPenjualanAsync(string kodeBarang, decimal jumlah, string keterangan, string? noFaktur, string nip, CancellationToken ct = default)
    {
        var barang = await db.DataBarangSet.FirstOrDefaultAsync(b => b.KodeBarang == kodeBarang, ct)
            ?? throw new InvalidOperationException($"Barang '{kodeBarang}' tidak ditemukan.");

        var noKeluar = GenerateNoKeluar();
        var hargaBeli = barang.Dasar;

        db.PengeluaranObatBhpSet.Add(new PengeluaranObatBhp
        {
            NoKeluar = noKeluar,
            Tanggal = DateOnly.FromDateTime(DateTime.Now),
            Nip = nip,
            Keterangan = keterangan,
        });

        db.DetailPengeluaranObatBhpSet.Add(new DetailPengeluaranObatBhp
        {
            NoKeluar = noKeluar,
            KodeBarang = kodeBarang,
            KodeSatuan = barang.KodeSatuanBesar,
            Jumlah = jumlah,
            HargaBeli = hargaBeli,
            Total = hargaBeli * jumlah,
            NoFaktur = string.IsNullOrWhiteSpace(noFaktur) ? "-" : noFaktur,
        });

        await db.SaveChangesAsync(ct);
        return noKeluar;
    }

    public async Task<IReadOnlyList<PenjualanListItem>> GetPenjualanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var query =
            from h in db.PengeluaranObatBhpSet
            join d in db.DetailPengeluaranObatBhpSet on h.NoKeluar equals d.NoKeluar into dJoin
            from d in dJoin.DefaultIfEmpty()
            join b in db.DataBarangSet on d.KodeBarang equals b.KodeBarang into bJoin
            from b in bJoin.DefaultIfEmpty()
            where h.Tanggal >= fromDate && h.Tanggal <= toDate
            orderby h.Tanggal descending
            select new PenjualanListItem(
                h.NoKeluar, h.Tanggal, h.Keterangan,
                b != null ? b.NamaBarang : null, d != null ? d.Jumlah : 0, d != null ? d.Total : 0);

        return await query.ToListAsync(ct);
    }

    /// <summary>Same approach as PenunjangRepository.GenerateNoOrder — no_keluar is varchar(15), not AUTO_INCREMENT.</summary>
    private static string GenerateNoKeluar()
        => DateTime.Now.ToString("yyMMddHHmmss") + Random.Shared.Next(10, 99);
}
