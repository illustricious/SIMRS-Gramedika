using Microsoft.EntityFrameworkCore;

namespace SimrsGramedika.Data;

public partial class KasirRepository
{
    private const string Operasi = "Operasi";
    /// <summary>
    /// Penanda baris obat/BHP kamar operasi. Billing menyimpannya sebagai "Obat" seperti Khanza, tetapi jurnal input
    /// mencatatnya pada akun operasi, jadi pelunasan harus menghitungnya sebagai operasi.
    /// </summary>
    private const string BhpOperasi = "BHP Operasi · ";

    private async Task<List<KasirRincian>> MuatOperasiAsync(string noRawat, CancellationToken ct)
    {
        var kolom = string.Join(" UNION ALL ", OperasiService.Komponen.Select((k, i) => $"""
            SELECT o.tgl_operasi AS Waktu,o.kode_paket AS Kode,{i} AS Urut,COALESCE(p.nm_perawatan,o.kode_paket) AS Paket,
                   CAST(COALESCE(o.{k.Biaya},0) AS DECIMAL(20,4)) AS Biaya
            FROM operasi o LEFT JOIN paket_operasi p ON p.kode_paket=o.kode_paket WHERE o.no_rawat={"{0}"}
            """));
        var komponen = (await db.Database.SqlQueryRaw<BiayaOperasi>(kolom, noRawat).ToListAsync(ct))
            .OrderBy(b => b.Waktu).ThenBy(b => b.Kode, StringComparer.Ordinal).ThenBy(b => b.Urut).ToList();
        var rinci = komponen.Count > 0 && await db.Database.SqlQuery<bool>($"""
            SELECT (rincianoperasi='Yes') IS TRUE AS Value FROM set_nota LIMIT 1
            """).FirstOrDefaultAsync(ct);
        var rows = new List<KasirRincian>();
        if (rinci)
            rows.AddRange(komponen.Where(b => b.Biaya > 0).Select(b => new KasirRincian(Operasi,
                $"{b.Paket} · {OperasiService.Komponen[b.Urut].Label}", b.Biaya, 1, b.Biaya)));
        else
            rows.AddRange(komponen.GroupBy(b => (b.Waktu, b.Kode)).Select(g =>
                new KasirRincian(Operasi, g.First().Paket, g.Sum(b => b.Biaya), 1, g.Sum(b => b.Biaya))));

        var obat = await db.Database.SqlQuery<KasirRincian>($"""
            SELECT 'Obat' AS Jenis,COALESCE(b.nm_obat,o.kd_obat) AS Nama,CAST(o.hargasatuan AS DECIMAL(20,4)) AS Tarif,
                   CAST(SUM(o.jumlah) AS DECIMAL(20,4)) AS Jumlah,CAST(SUM(o.hargasatuan*o.jumlah) AS DECIMAL(20,4)) AS Total
            FROM beri_obat_operasi o LEFT JOIN obatbhp_ok b ON b.kd_obat=o.kd_obat
            WHERE o.no_rawat={noRawat}
            GROUP BY o.kd_obat,b.nm_obat,o.hargasatuan
            ORDER BY b.nm_obat,o.kd_obat,o.hargasatuan
            """).ToListAsync(ct);
        rows.AddRange(obat.Select(r => r with { Nama = BhpOperasi + r.Nama }));
        return rows;
    }

    private sealed class BiayaOperasi
    {
        public DateTime Waktu { get; set; }
        public string Kode { get; set; } = "";
        public int Urut { get; set; }
        public string Paket { get; set; } = "";
        public decimal Biaya { get; set; }
    }
}
