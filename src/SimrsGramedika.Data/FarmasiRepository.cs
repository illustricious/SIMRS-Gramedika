using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Core.Entities;

namespace SimrsGramedika.Data;

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

    /// <summary>Penjualan dinonaktifkan sampai alur penjualan, stok, dan jurnal Khanza diimplementasikan.</summary>
    Task<string> SimpanPenjualanAsync(IReadOnlyList<PenjualanLineInput> lines, string keterangan, string nip, CancellationToken ct = default);

    Task<IReadOnlyList<PenjualanListItem>> GetPenjualanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public class FarmasiRepository(SimrsGramedikaDbContext db) : IFarmasiRepository
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
                r.Dokter != null ? r.Dokter.NamaDokter : null,
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

    public Task<string> SimpanPenjualanAsync(IReadOnlyList<PenjualanLineInput> lines, string keterangan, string nip, CancellationToken ct = default)
        => Task.FromException<string>(new InvalidOperationException(
            "Penjualan farmasi belum tersedia: transaksi belum mencatat penjualan, stok, dan jurnal sesuai Khanza. Gunakan Khanza untuk transaksi ini."));

    public async Task<IReadOnlyList<PenjualanListItem>> GetPenjualanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<PenjualanRow>($"""
            SELECT p.nota_jual AS NoKeluar,p.tgl_jual AS Tanggal,COALESCE(p.keterangan,'') AS Keterangan,
                   b.nama_brng AS NamaBarang,CAST(COALESCE(d.jumlah,0) AS DECIMAL(18,4)) AS Jumlah,
                   CAST(COALESCE(d.total,0) AS DECIMAL(18,2)) AS Total
            FROM penjualan p LEFT JOIN detailjual d ON d.nota_jual=p.nota_jual
            LEFT JOIN databarang b ON b.kode_brng=d.kode_brng
            WHERE p.tgl_jual BETWEEN {fromDate} AND {toDate}
            ORDER BY p.tgl_jual DESC,p.nota_jual DESC,d.kode_brng
            """).ToListAsync(ct);
        return rows.Select(r => new PenjualanListItem(r.NoKeluar, DateOnly.FromDateTime(r.Tanggal), r.Keterangan,
            r.NamaBarang, r.Jumlah, r.Total)).ToList();
    }

    private sealed class PenjualanRow
    {
        public string NoKeluar { get; set; } = "";
        public DateTime Tanggal { get; set; }
        public string Keterangan { get; set; } = "";
        public string? NamaBarang { get; set; }
        public decimal Jumlah { get; set; }
        public decimal Total { get; set; }
    }
}
