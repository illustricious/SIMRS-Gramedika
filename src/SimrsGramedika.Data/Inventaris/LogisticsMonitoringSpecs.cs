using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

internal static class LogisticsMonitoringSpecs
{
    public static readonly IReadOnlyDictionary<string, ReportSpec> NonmedicalReports = new Dictionary<string, ReportSpec>
    {
        ["btnRBiayaIpsrs"] = PurchaseCost(LogisticsCatalog.Nonmedical),
        ["btnOpnameIPSRS"] = Stocktake(LogisticsCatalog.Nonmedical),
        ["btnVerifikasiPenerimaanLogistik"] = Verification(LogisticsCatalog.Nonmedical),
        ["btnNilaiPenerimaanVendorNonMedisPerBulan"] = SupplierTotals(LogisticsCatalog.Nonmedical),
        ["btnPengambilanPenunjangUTD"] = Utd(),
    };

    public static readonly IReadOnlyDictionary<string, ReportSpec> KitchenReports = new Dictionary<string, ReportSpec>
    {
        ["btnRBiayaDapur"] = PurchaseCost(LogisticsCatalog.Kitchen),
        ["btnOpnameDapur"] = Stocktake(LogisticsCatalog.Kitchen),
        ["btnVerifikasiPenerimaanDapur"] = Verification(LogisticsCatalog.Kitchen),
        ["btnNilaiPenerimaanVendorDapurPerBulan"] = SupplierTotals(LogisticsCatalog.Kitchen),
    };

    private static ReportSpec PurchaseCost(LogisticsCatalog catalog) => new($"Biaya Pengadaan {catalog.Label}", $"""
            SELECT h.tgl_beli AS tanggal,COUNT(*) AS faktur,SUM(h.total) AS total
            FROM {catalog.Purchase} h LEFT JOIN {catalog.Suppliers} v ON v.kode_suplier=h.kode_suplier
            WHERE h.tgl_beli BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_faktur,h.kode_suplier,v.nama_suplier,h.nip,h.tgl_beli) LIKE @p0
            GROUP BY h.tgl_beli
            ORDER BY h.tgl_beli
            """,
            [("tanggal", "Tanggal"), ("faktur", "Jumlah Faktur"), ("total", "Total Sebelum Pajak")],
            ReportPeriod.DateRange, "Penjumlahan total faktur pembelian per tanggal, sebelum PPN dan meterai.")
        { SearchWatermark = "No. faktur, suplier, atau tanggal" };

    private static ReportSpec Utd() => new("Pengambilan UTD", """
            SELECT x.tanggal,x.kode_brng,b.nama_brng,s.satuan,x.jml,x.harga,x.total,x.nip,p.nama AS petugas,x.keterangan
            FROM utd_pengambilan_penunjang x
            LEFT JOIN ipsrsbarang b ON b.kode_brng=x.kode_brng
            LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            LEFT JOIN petugas p ON p.nip=x.nip
            WHERE x.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,x.nip,p.nama,x.keterangan) LIKE @p0
            ORDER BY x.tanggal DESC,x.kode_brng,x.nip
            """,
            [("tanggal", "Waktu Pengambilan"), ("kode_brng", "Kode Barang"), ("nama_brng", "Barang"), ("satuan", "Satuan"),
             ("jml", "Jumlah"), ("harga", "Harga Satuan"), ("total", "Nilai"), ("nip", "NIP"),
             ("petugas", "Petugas"), ("keterangan", "Keterangan")],
            ReportPeriod.DateRange, "Riwayat pengambilan. Pencatatan dan penyesuaian stok UTD dilakukan di Khanza.")
        { SearchWatermark = "Barang, petugas, atau keterangan" };

    private static ReportSpec Stocktake(LogisticsCatalog catalog) => new($"Stok Opname Barang {catalog.Label}", $"""
            SELECT x.tanggal,x.kode_brng,b.nama_brng,s.satuan,x.h_beli,x.stok,x.real AS stok_fisik,
                   x.selisih,x.nomihilang,x.lebih,x.nomilebih,x.keterangan
            FROM {catalog.Stocktake} x
            LEFT JOIN {catalog.Items} b ON b.kode_brng=x.kode_brng
            LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            WHERE x.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,s.satuan,x.keterangan) LIKE @p0
            ORDER BY x.tanggal DESC,b.nama_brng,x.kode_brng
            """,
            [("tanggal", "Tanggal"), ("kode_brng", "Kode Barang"), ("nama_brng", "Barang"), ("satuan", "Satuan"),
             ("h_beli", "Harga Beli"), ("stok", "Stok Sistem"), ("stok_fisik", "Stok Fisik"), ("selisih", "Selisih Kurang"),
             ("nomihilang", "Nilai Kurang"), ("lebih", "Selisih Lebih"), ("nomilebih", "Nilai Lebih"), ("keterangan", "Keterangan")],
            ReportPeriod.DateRange, "Hasil opname yang sudah tersimpan. Koreksi saldo dilakukan di Khanza.")
        { SearchWatermark = "Kode, barang, satuan, atau keterangan" };

    private static ReportSpec Verification(LogisticsCatalog catalog) => new($"Verifikasi Penerimaan {catalog.Label}", $"""
            SELECT o.no_pemesanan,o.tanggal AS tgl_order,o.kode_suplier,v.nama_suplier,
                   x.kode_brng,b.nama_brng,NULLIF(x.kode_sat,'') AS kode_sat,s.satuan,
                   SUM(x.pesan) AS dipesan,SUM(x.terima) AS diterima,SUM(x.terima)-SUM(x.pesan) AS selisih,
                   SUM(x.nilai_pesan) AS nilai_pesan,SUM(x.nilai_terima) AS nilai_terima,
                   MIN(x.tgl_terima) AS terima_pertama,MAX(x.tgl_terima) AS terima_terakhir,
                   DATEDIFF(MIN(x.tgl_terima),o.tanggal) AS hari_kirim,COUNT(DISTINCT x.no_faktur) AS faktur
            FROM {catalog.Order} o
            JOIN (
                SELECT x.no_pemesanan,x.kode_brng,COALESCE(x.kode_sat,'') AS kode_sat,
                       COALESCE(x.jumlah,0) AS pesan,0 AS terima,COALESCE(x.total,0) AS nilai_pesan,0 AS nilai_terima,
                       NULL AS tgl_terima,NULL AS no_faktur
                FROM detail_{catalog.Order} x
                JOIN {catalog.Order} h ON h.no_pemesanan=x.no_pemesanan
                WHERE h.status='Sudah Datang' AND h.tanggal BETWEEN @p1 AND @p2
                UNION ALL
                SELECT r.no_order,x.kode_brng,COALESCE(x.kode_sat,''),0,x.jumlah,0,x.total,r.tgl_pesan,r.no_faktur
                FROM {catalog.Receipt} r JOIN {catalog.ReceiptItems} x ON x.no_faktur=r.no_faktur
                JOIN {catalog.Order} h ON h.no_pemesanan=r.no_order
                WHERE h.status='Sudah Datang' AND h.tanggal BETWEEN @p1 AND @p2
            ) x ON x.no_pemesanan=o.no_pemesanan
            LEFT JOIN {catalog.Suppliers} v ON v.kode_suplier=o.kode_suplier
            LEFT JOIN {catalog.Items} b ON b.kode_brng=x.kode_brng
            LEFT JOIN kodesatuan s ON s.kode_sat=x.kode_sat
            LEFT JOIN pegawai p ON p.nik=o.nip
            WHERE CONCAT_WS(' ',o.no_pemesanan,o.kode_suplier,v.nama_suplier,o.nip,p.nama,x.kode_brng,b.nama_brng,s.satuan) LIKE @p0
            GROUP BY o.no_pemesanan,o.tanggal,o.kode_suplier,v.nama_suplier,x.kode_brng,b.nama_brng,x.kode_sat,s.satuan
            ORDER BY o.tanggal DESC,o.no_pemesanan,x.kode_brng,x.kode_sat
            """,
            [("no_pemesanan", "No. Order"), ("tgl_order", "Tgl. Order"), ("kode_suplier", "Kode Suplier"), ("nama_suplier", "Suplier"),
             ("kode_brng", "Kode Barang"), ("nama_brng", "Barang"), ("kode_sat", "Kode Satuan"), ("satuan", "Satuan"),
             ("dipesan", "Dipesan"), ("diterima", "Diterima"), ("selisih", "Selisih Qty"), ("nilai_pesan", "Nilai Pesan"),
             ("nilai_terima", "Nilai Terima"), ("terima_pertama", "Terima Pertama"), ("terima_terakhir", "Terima Terakhir"),
             ("hari_kirim", "Hari ke Terima Pertama"), ("faktur", "Jumlah Faktur")],
            ReportPeriod.DateRange, "Perbandingan surat berstatus Sudah Datang dengan seluruh faktur penerimaan, per barang dan satuan.")
        { SearchWatermark = "No. order, suplier, barang, atau petugas" };

    private static ReportSpec SupplierTotals(LogisticsCatalog catalog) => new($"Nilai Penerimaan Vendor {catalog.Label} Per Bulan", $"""
            SELECT r.kode_suplier,v.nama_suplier,DATE_FORMAT(r.tgl_pesan,'%Y-%m') AS bulan,
                   COUNT(DISTINCT r.no_faktur) AS faktur,SUM(x.total) AS nilai
            FROM {catalog.Receipt} r JOIN {catalog.ReceiptItems} x ON x.no_faktur=r.no_faktur
            LEFT JOIN {catalog.Suppliers} v ON v.kode_suplier=r.kode_suplier
            LEFT JOIN {catalog.Items} b ON b.kode_brng=x.kode_brng
            {catalog.CategoryJoin}
            WHERE r.tgl_pesan >= MAKEDATE(@p1,1) AND r.tgl_pesan < MAKEDATE(@p1+1,1)
              AND CONCAT_WS(' ',r.kode_suplier,v.nama_suplier,x.kode_brng,b.nama_brng,{catalog.Category}) LIKE @p0
            GROUP BY r.kode_suplier,v.nama_suplier,DATE_FORMAT(r.tgl_pesan,'%Y-%m')
            ORDER BY v.nama_suplier,r.kode_suplier,bulan
            """,
            [("kode_suplier", "Kode Suplier"), ("nama_suplier", "Suplier"), ("bulan", "Bulan"),
             ("faktur", "Jumlah Faktur"), ("nilai", "Nilai Penerimaan")],
            ReportPeriod.Year, "Penjumlahan nilai item penerimaan per vendor dan bulan berdasarkan tanggal pesan.")
        { SearchWatermark = "Suplier, barang, atau jenis barang" };
}
