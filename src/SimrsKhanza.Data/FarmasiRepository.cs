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

/// <summary>One row of the "Input Penjualan" catalog grid — every active databarang row, priced and stocked for one lokasi (see BarangKatalogItem doc / GudangBarang).</summary>
public record BarangKatalogItem(
    string KodeBarang, string? NamaBarang, string? Kategori, string Satuan, decimal Harga, decimal Stok);

/// <summary>One line of a multi-item sale — see FarmasiRepository.SimpanPenjualanAsync.</summary>
public record PenjualanLineInput(
    string KodeBarang, decimal Jumlah, decimal HargaSatuan, decimal PtgRp, decimal TambahanRp, string? NoBatch, string? NoFaktur);

public interface IFarmasiRepository
{
    Task<IReadOnlyList<DataBarang>> SearchBarangAsync(string keyword, CancellationToken ct = default);

    /// <summary>Full active-item catalog (priced + stocked for `kodeBangsal`) backing the "Input Penjualan" grid — see BarangKatalogItem.</summary>
    Task<IReadOnlyList<BarangKatalogItem>> GetKatalogPenjualanAsync(string kodeBangsal = "F.A", CancellationToken ct = default);

    Task<IReadOnlyList<ResepListItem>> GetResepDokterAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<IReadOnlyList<ResepItemLine>> GetResepItemsAsync(string noResep, CancellationToken ct = default);

    /// <summary>Real INSERT into pengeluaran_obat_bhp + one detail_pengeluaran_obat_bhp row per line. Returns the generated NoKeluar.</summary>
    Task<string> SimpanPenjualanAsync(IReadOnlyList<PenjualanLineInput> lines, string keterangan, string nip, CancellationToken ct = default);

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

    public async Task<IReadOnlyList<BarangKatalogItem>> GetKatalogPenjualanAsync(string kodeBangsal = "F.A", CancellationToken ct = default)
    {
        // Stock is summed in a separate query (per kode_barang) rather than joined
        // in one LINQ query — group-by-then-join doesn't always translate cleanly
        // to SQL via Pomelo, and the active catalog is only ~1-2k rows so an
        // in-memory merge is cheap (same "don't default to a huge query" lesson
        // as Registrasi, just not a concern at this row count).
        var stok = await db.GudangBarangSet
            .Where(g => g.KodeBangsal == kodeBangsal)
            .GroupBy(g => g.KodeBarang)
            .Select(g => new { KodeBarang = g.Key, Stok = g.Sum(x => x.Stok) })
            .ToDictionaryAsync(x => x.KodeBarang, x => x.Stok, ct);

        var query =
            from b in db.DataBarangSet
            join j in db.JenisBarangSet on b.KodeJenis equals j.KodeJenis into jj
            from j in jj.DefaultIfEmpty()
            where b.Aktif
            orderby b.NamaBarang
            select new { b.KodeBarang, b.NamaBarang, Kategori = j != null ? j.Nama : null, b.KodeSatuanBesar, Harga = b.JualBebas ?? b.Dasar };

        var list = await query.ToListAsync(ct);
        return list
            .Select(x => new BarangKatalogItem(
                x.KodeBarang, x.NamaBarang, x.Kategori, x.KodeSatuanBesar, x.Harga,
                stok.TryGetValue(x.KodeBarang, out var s) ? s : 0))
            .ToList();
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

    public async Task<string> SimpanPenjualanAsync(IReadOnlyList<PenjualanLineInput> lines, string keterangan, string nip, CancellationToken ct = default)
    {
        if (lines.Count == 0)
            throw new InvalidOperationException("Minimal satu barang dengan jumlah lebih dari 0.");

        var noKeluar = GenerateNoKeluar();

        db.PengeluaranObatBhpSet.Add(new PengeluaranObatBhp
        {
            NoKeluar = noKeluar,
            Tanggal = DateOnly.FromDateTime(DateTime.Now),
            Nip = nip,
            Keterangan = keterangan,
        });

        foreach (var line in lines)
        {
            var barang = await db.DataBarangSet.AsNoTracking().FirstOrDefaultAsync(b => b.KodeBarang == line.KodeBarang, ct)
                ?? throw new InvalidOperationException($"Barang '{line.KodeBarang}' tidak ditemukan.");

            var subtotal = line.HargaSatuan * line.Jumlah;
            var total = subtotal - line.PtgRp + line.TambahanRp;

            db.DetailPengeluaranObatBhpSet.Add(new DetailPengeluaranObatBhp
            {
                NoKeluar = noKeluar,
                KodeBarang = line.KodeBarang,
                KodeSatuan = barang.KodeSatuanBesar,
                NoBatch = string.IsNullOrWhiteSpace(line.NoBatch) ? "-" : line.NoBatch,
                Jumlah = line.Jumlah,
                HargaBeli = line.HargaSatuan,
                Total = total,
                NoFaktur = string.IsNullOrWhiteSpace(line.NoFaktur) ? "-" : line.NoFaktur,
            });
        }

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
