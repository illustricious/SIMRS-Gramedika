using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

internal sealed class LogisticsStockSpecs(LogisticsCatalog catalog)
{
    private bool IncludesUtd => catalog == LogisticsCatalog.Nonmedical;

    private static readonly (string, string)[] Item =
        [("kode_brng", "Kode Barang"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan")];

    private (string, string)[] Movement =>
        [("jml_beli", "Jml Pengadaan"), ("nilai_beli", "Nilai Pengadaan"),
         ("jml_pesan", "Jml Penerimaan"), ("nilai_pesan", "Nilai Penerimaan"),
         ("jml_keluar", "Jml Keluar"), ("nilai_keluar", "Nilai Keluar"),
         .. (IncludesUtd ? new[] { ("jml_utd", "Jml UTD"), ("nilai_utd", "Nilai UTD") } : Array.Empty<(string, string)>()),
         ("jml_hibah", "Jml Hibah"), ("nilai_hibah", "Nilai Hibah")];

    public ReportSpec History(string title) => new(title, $"""
        SELECT r.tanggal,r.jam,r.kode_brng,b.nama_brng,r.stok_awal,r.masuk,r.keluar,r.stok_akhir,r.posisi,r.petugas,r.status
        FROM {catalog.Prefix}_riwayat_barang r
        LEFT JOIN {catalog.Items} b ON b.kode_brng=r.kode_brng
        WHERE r.tanggal BETWEEN @p1 AND @p2
          AND CONCAT_WS(' ',r.kode_brng,b.nama_brng,r.posisi,r.petugas,r.status) LIKE @p0
        ORDER BY r.tanggal,r.jam,r.kode_brng
        """,
        [("tanggal", "Tanggal"), ("jam", "Jam"), ("kode_brng", "Kode Barang"), ("nama_brng", "Nama Barang"),
         ("stok_awal", "Stok Awal"), ("masuk", "Masuk"), ("keluar", "Keluar"), ("stok_akhir", "Stok Akhir"),
         ("posisi", "Transaksi"), ("petugas", "Petugas"), ("status", "Status")],
        ReportPeriod.DateRange, "Riwayat mencakup status Simpan dan Hapus yang tercatat di Khanza.")
    { SearchWatermark = "Barang, transaksi, petugas, atau status" };

    public ReportSpec Circulation(string title) => new(title, CirculationSql(false) + "\nORDER BY b.kode_brng",
        [.. Item, ("stok", "Stok Saat Ini"), ("nilai_stok", "Nilai Stok Saat Ini"), .. Movement],
        ReportPeriod.DateRange, "Stok memakai saldo saat ini. Mutasi mengikuti periode.")
    { SearchWatermark = "Kode, nama barang, atau satuan" };

    public ReportSpec CirculationWithStocktake(string title)
    {
        var quantity = "c.jml_beli+c.jml_pesan-c.jml_keluar+c.jml_hibah" + (IncludesUtd ? "-c.jml_utd" : "");
        var value = "c.nilai_beli+c.nilai_pesan-c.nilai_keluar+c.nilai_hibah" + (IncludesUtd ? "-c.nilai_utd" : "");
        var hint = IncludesUtd
            ? "Mutasi dihitung sejak opname pertama di periode. Untuk hasil opname kosong atau nol, saldo awal diturunkan dari stok saat ini."
            : "Hibah dihitung sepanjang periode. Mutasi lain dihitung sejak opname pertama. Untuk opname kosong atau nol, saldo awal diturunkan dari stok saat ini.";
        return new(title, $"""
            SELECT c.*,o.tanggal AS tgl_opname,
                   CASE WHEN o.real>0 THEN o.real ELSE c.stok-({quantity}) END AS stok_awal,
                   CASE WHEN o.real>0 THEN o.real*o.h_beli ELSE c.nilai_stok-({value}) END AS nilai_awal,
                   CASE WHEN o.real>0 THEN o.real+({quantity}) ELSE c.stok END AS stok_akhir,
                   CASE WHEN o.real>0 THEN o.real*o.h_beli+({value}) ELSE c.nilai_stok END AS nilai_akhir
            FROM ({CirculationSql(true)}) c
            LEFT JOIN {catalog.Stocktake} o ON o.kode_brng=c.kode_brng
              AND o.tanggal=(SELECT MIN(a.tanggal) FROM {catalog.Stocktake} a WHERE a.kode_brng=c.kode_brng AND a.tanggal BETWEEN @p1 AND @p2)
            ORDER BY c.kode_brng
            """,
            [.. Item, ("tgl_opname", "Opname Pertama"), ("stok_awal", "Stok Awal Hitung"), ("nilai_awal", "Nilai Awal Hitung"),
             .. Movement, ("stok_akhir", "Stok Akhir Hitung"), ("nilai_akhir", "Nilai Akhir Hitung")],
            ReportPeriod.DateRange, hint)
        { SearchWatermark = "Kode, nama barang, atau satuan" };
    }

    private string CirculationSql(bool fromStocktake)
    {
        var start = fromStocktake
            ? $"COALESCE((SELECT MIN(a.tanggal) FROM {catalog.Stocktake} a WHERE a.kode_brng=x.kode_brng AND a.tanggal BETWEEN @p1 AND @p2),@p1)"
            : "@p1";
        // Khanza's kitchen circulation includes grants for the entire period, even before the first stocktake.
        var grantStart = IncludesUtd ? start : "@p1";
        var utdMovement = IncludesUtd ? $"""
            UNION ALL
            SELECT x.kode_brng,'utd',x.jml,x.total FROM utd_pengambilan_penunjang x WHERE x.tanggal BETWEEN {start} AND @p2
            """ : "";
        return $"""
            SELECT b.kode_brng,b.nama_brng,s.satuan,b.stok,b.stok*b.harga AS nilai_stok,
                   COALESCE(r.jml_beli,0) AS jml_beli,COALESCE(r.nilai_beli,0) AS nilai_beli,
                   COALESCE(r.jml_pesan,0) AS jml_pesan,COALESCE(r.nilai_pesan,0) AS nilai_pesan,
                   COALESCE(r.jml_keluar,0) AS jml_keluar,COALESCE(r.nilai_keluar,0) AS nilai_keluar,
                   COALESCE(r.jml_utd,0) AS jml_utd,COALESCE(r.nilai_utd,0) AS nilai_utd,
                   COALESCE(r.jml_hibah,0) AS jml_hibah,COALESCE(r.nilai_hibah,0) AS nilai_hibah
            FROM {catalog.Items} b
            LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            LEFT JOIN (
                SELECT kode_brng,
                       SUM(CASE WHEN jenis='beli' THEN jumlah ELSE 0 END) AS jml_beli,
                       SUM(CASE WHEN jenis='beli' THEN nilai ELSE 0 END) AS nilai_beli,
                       SUM(CASE WHEN jenis='pesan' THEN jumlah ELSE 0 END) AS jml_pesan,
                       SUM(CASE WHEN jenis='pesan' THEN nilai ELSE 0 END) AS nilai_pesan,
                       SUM(CASE WHEN jenis='keluar' THEN jumlah ELSE 0 END) AS jml_keluar,
                       SUM(CASE WHEN jenis='keluar' THEN nilai ELSE 0 END) AS nilai_keluar,
                       SUM(CASE WHEN jenis='utd' THEN jumlah ELSE 0 END) AS jml_utd,
                       SUM(CASE WHEN jenis='utd' THEN nilai ELSE 0 END) AS nilai_utd,
                       SUM(CASE WHEN jenis='hibah' THEN jumlah ELSE 0 END) AS jml_hibah,
                       SUM(CASE WHEN jenis='hibah' THEN nilai ELSE 0 END) AS nilai_hibah
                FROM (
                    SELECT x.kode_brng,'beli' AS jenis,x.jumlah,x.subtotal AS nilai
                    FROM {catalog.Purchase} h JOIN {catalog.PurchaseItems} x ON x.no_faktur=h.no_faktur
                    WHERE h.tgl_beli BETWEEN {start} AND @p2
                    UNION ALL
                    SELECT x.kode_brng,'pesan',x.jumlah,x.subtotal
                    FROM {catalog.Receipt} h JOIN {catalog.ReceiptItems} x ON x.no_faktur=h.no_faktur
                    WHERE h.tgl_pesan BETWEEN {start} AND @p2
                    UNION ALL
                    SELECT x.kode_brng,'keluar',x.jumlah,x.total
                    FROM {catalog.Issue} h JOIN {catalog.IssueItems} x ON x.no_keluar=h.no_keluar
                    WHERE h.tanggal BETWEEN {start} AND @p2
                    {utdMovement}
                    UNION ALL
                    SELECT x.kode_brng,'hibah',x.jumlah,x.subtotalhibah
                    FROM {catalog.Prefix}_hibah h JOIN {catalog.Prefix}_detail_hibah x ON x.no_hibah=h.no_hibah
                    WHERE h.tgl_hibah BETWEEN {grantStart} AND @p2
                ) pergerakan
                GROUP BY kode_brng
            ) r ON r.kode_brng=b.kode_brng
            WHERE CONCAT_WS(' ',b.kode_brng,b.nama_brng,s.satuan) LIKE @p0
              AND (b.stok>0 OR r.jml_beli>0 OR r.jml_pesan>0 OR r.jml_keluar>0 OR r.jml_utd>0 OR r.jml_hibah>0)
            """;
    }
}
