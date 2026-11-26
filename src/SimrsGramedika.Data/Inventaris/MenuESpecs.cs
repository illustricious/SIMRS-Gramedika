using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

public static class MenuESpecs
{
    private static readonly LogisticsCatalog Catalog = LogisticsCatalog.Nonmedical;

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnJenisIpsrs"] = new("Jenis Barang Non Medis", "ipsrsjenisbarang",
            [new("kd_jenis", "Kode", MaxLen: 5, Key: true), new("nm_jenis", "Nama Jenis", MaxLen: 50, Required: true)],
            "SELECT kd_jenis,nm_jenis FROM ipsrsjenisbarang WHERE CONCAT_WS(' ',kd_jenis,nm_jenis) LIKE @p0 ORDER BY nm_jenis,kd_jenis",
            [("kd_jenis", "Kode"), ("nm_jenis", "Nama Jenis")]),
        ["btnBarangIpsrs"] = LogisticsMasterSpecs.Items("Barang Non Medis", Catalog,
            new("jenis", "Jenis Barang", FieldKind.Lookup,
                LookupSql: "SELECT kd_jenis AS kode,nm_jenis AS nama FROM ipsrsjenisbarang ORDER BY nm_jenis,kd_jenis")
            { NullWhenEmpty = true }),
        ["btnSuplierIPSRS"] = LogisticsMasterSpecs.Suppliers(Catalog),
    };

    private static readonly LogisticsStockSpecs Stock = new(Catalog);

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports =
        new Dictionary<string, ReportSpec>(new LogisticsTransactionSpecs(Catalog).NonmedicalReports()
            .Concat(LogisticsMonitoringSpecs.NonmedicalReports))
    {
        ["btnRiwayatBarangNonMedis"] = Stock.History("Riwayat Barang Non Medis"),
        ["btnSirkulasiNonMedis"] = Stock.Circulation("Sirkulasi Non Medis"),
        ["btnSirkulasiNonMedis2"] = Stock.CirculationWithStocktake("Sirkulasi Non Medis 2"),

        ["btnRingkasanStokKeluarNonMedis"] = new("Ringkasan Stok Keluar Non Medis", """
            SELECT x.kode_brng,b.nama_brng,j.nm_jenis,x.kode_sat,s.satuan,
                   SUM(x.jumlah) AS jumlah,SUM(x.total) AS total,COUNT(DISTINCT h.no_keluar) AS transaksi
            FROM ipsrspengeluaran h JOIN ipsrsdetailpengeluaran x ON x.no_keluar=h.no_keluar
            LEFT JOIN ipsrsbarang b ON b.kode_brng=x.kode_brng
            LEFT JOIN ipsrsjenisbarang j ON j.kd_jenis=b.jenis
            LEFT JOIN kodesatuan s ON s.kode_sat=x.kode_sat
            LEFT JOIN petugas p ON p.nip=h.nip
            WHERE h.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_keluar,h.keterangan,h.nip,p.nama,x.kode_brng,b.nama_brng,b.jenis,j.nm_jenis,x.kode_sat,s.satuan) LIKE @p0
            GROUP BY x.kode_brng,b.nama_brng,j.nm_jenis,x.kode_sat,s.satuan
            ORDER BY b.nama_brng,x.kode_brng,x.kode_sat
            """,
            [("kode_brng", "Kode Barang"), ("nama_brng", "Nama Barang"), ("nm_jenis", "Jenis"),
             ("kode_sat", "Kode Satuan"), ("satuan", "Satuan Transaksi"), ("jumlah", "Jumlah"),
             ("total", "Nilai Keluar"), ("transaksi", "Jml Transaksi")],
            ReportPeriod.DateRange, "Jumlah dan nilai dikelompokkan berdasarkan satuan yang tercatat di detail pengeluaran.")
        { SearchWatermark = "No. keluar, barang, jenis, atau petugas" },
    };
}
