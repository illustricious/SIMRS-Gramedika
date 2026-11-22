using SimrsKhanza.Data.Master;

namespace SimrsKhanza.Data.Inventaris;

internal static class MenuSStockReports
{
    private static readonly (string Column, string Label, string Header, string Detail, string Doc, string Date, string Qty, bool Inbound)[] Flows =
    [
        ("beli", "Pengadaan", "tokopembelian", "toko_detail_beli", "no_faktur", "tgl_beli", "jumlah", true),
        ("pesan", "Penerimaan", "tokopemesanan", "toko_detail_pesan", "no_faktur", "tgl_pesan", "jumlah", true),
        ("jual", "Penjualan", "tokopenjualan", "toko_detail_jual", "nota_jual", "tgl_jual", "jumlah", false),
        ("piutang", "Piutang", "tokopiutang", "toko_detail_piutang", "nota_piutang", "tgl_piutang", "jumlah", false),
        ("retur_beli", "Retur Suplier", "tokoreturbeli", "toko_detail_returbeli", "no_retur_beli", "tgl_retur", "jml_retur", false),
        ("retur_jual", "Retur Jual", "tokoreturjual", "toko_detail_returjual", "no_retur_jual", "tgl_retur", "jml_retur", true),
        ("retur_piutang", "Retur Piutang", "tokoreturpiutang", "toko_detail_returpiutang", "no_retur_piutang", "tgl_retur", "jml_retur", true),
    ];

    internal static readonly IReadOnlyDictionary<string, ReportSpec> Specs = new Dictionary<string, ReportSpec>
    {
        ["btnStokOpnameToko"] = new("Stok Opname Toko", """
            SELECT o.tanggal,o.kode_brng,b.nama_brng,s.satuan,o.dasar,o.stok,o.real,o.selisih,o.nomihilang,o.keterangan
            FROM tokoopname o
            LEFT JOIN tokobarang b ON b.kode_brng=o.kode_brng
            LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            WHERE o.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',o.kode_brng,b.nama_brng,s.satuan,o.keterangan) LIKE @p0
            ORDER BY o.tanggal DESC,b.nama_brng,o.kode_brng
            """,
            [("tanggal", "Tanggal"), ("kode_brng", "Kode Barang"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"),
             ("dasar", "Harga Dasar"), ("stok", "Stok Sistem"), ("real", "Stok Fisik"), ("selisih", "Selisih"),
             ("nomihilang", "Nilai Selisih"), ("keterangan", "Keterangan")],
            ReportPeriod.DateRange, "Hasil opname yang sudah disimpan. Stok fisik dicatat di Khanza.")
        { TotalColumns = ["nomihilang"] },

        ["btnSirkulasiBarangToko"] = Circulation(false),
        ["btnSirkulasiBarangToko2"] = Circulation(true),
    };

    private static ReportSpec Circulation(bool fromStocktake)
    {
        var transactions = string.Join("\nUNION ALL\n", Flows.Select(flow => $"""
            SELECT x.kode_brng,h.{flow.Date} AS tanggal,'{flow.Column}' AS jenis,x.{flow.Qty} AS jumlah,x.total AS nilai
            FROM {flow.Header} h JOIN {flow.Detail} x ON x.{flow.Doc}=h.{flow.Doc}
            WHERE h.{flow.Date} BETWEEN @p1 AND @p2
            """));
        // The date of the first stocktake is inclusive, matching Khanza's date-based reports.
        const string firstStocktake = """
            SELECT kode_brng,MIN(tanggal) AS tanggal FROM tokoopname
            WHERE tanggal BETWEEN @p1 AND @p2 GROUP BY kode_brng
            """;
        var stocktakeJoin = fromStocktake ? $"""
            LEFT JOIN ({firstStocktake}) f ON f.kode_brng=b.kode_brng
            LEFT JOIN tokoopname o ON o.kode_brng=f.kode_brng AND o.tanggal=f.tanggal
            """ : "";
        var movementWindow = fromStocktake ? $"""
            LEFT JOIN ({firstStocktake}) f ON f.kode_brng=x.kode_brng
            WHERE x.tanggal >= COALESCE(f.tanggal,@p1)
            """ : "";

        var sums = string.Join(",\n", Flows.SelectMany(flow => new[]
        {
            $"SUM(CASE WHEN x.jenis='{flow.Column}' THEN COALESCE(x.jumlah,0) ELSE 0 END) AS jml_{flow.Column}",
            $"SUM(CASE WHEN x.jenis='{flow.Column}' THEN COALESCE(x.nilai,0) ELSE 0 END) AS nilai_{flow.Column}",
        }));
        string Value(string prefix, string column) => $"COALESCE(m.{prefix}_{column},0)";
        string Net(string prefix) => string.Join(" ", Flows.Select(flow =>
            $"{(flow.Inbound ? "+" : "-")} {Value(prefix, flow.Column)}"));
        var movements = string.Join(",\n", Flows.SelectMany(flow => new[]
        {
            $"{Value("jml", flow.Column)} AS jml_{flow.Column}", $"{Value("nilai", flow.Column)} AS nilai_{flow.Column}",
        }));
        var balances = fromStocktake ? $"""
            o.tanggal AS tgl_opname,
            CASE WHEN o.real>0 THEN o.real ELSE b.stok-({Net("jml")}) END AS stok_awal,
            CASE WHEN o.real>0 THEN o.real*o.dasar ELSE b.stok*b.dasar-({Net("nilai")}) END AS nilai_awal,
            CASE WHEN o.real>0 THEN o.real+({Net("jml")}) ELSE b.stok END AS stok_akhir,
            CASE WHEN o.real>0 THEN o.real*o.dasar+({Net("nilai")}) ELSE b.stok*b.dasar END AS nilai_akhir
            """ : "b.stok,b.stok*b.dasar AS nilai_stok";
        (string, string)[] balancesBefore = fromStocktake
            ? [("tgl_opname", "Opname Pertama"), ("stok_awal", "Stok Awal Hitung"), ("nilai_awal", "Nilai Awal Hitung")]
            : [("stok", "Stok Saat Ini"), ("nilai_stok", "Nilai Stok Saat Ini")];
        (string, string)[] balancesAfter = fromStocktake
            ? [("stok_akhir", "Stok Akhir Hitung"), ("nilai_akhir", "Nilai Akhir Hitung")]
            : [];

        return new(fromStocktake ? "Sirkulasi Barang Toko 2" : "Sirkulasi Barang Toko", $"""
            SELECT b.kode_brng,b.nama_brng,s.satuan,{movements},{balances}
            FROM tokobarang b LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            {stocktakeJoin}
            LEFT JOIN (
                SELECT x.kode_brng,{sums}
                FROM ({transactions}) x
                {movementWindow}
                GROUP BY x.kode_brng
            ) m ON m.kode_brng=b.kode_brng
            WHERE CONCAT_WS(' ',b.kode_brng,b.nama_brng,s.satuan) LIKE @p0
              AND (b.stok<>0 OR m.kode_brng IS NOT NULL{(fromStocktake ? " OR o.kode_brng IS NOT NULL" : "")})
            ORDER BY b.nama_brng,b.kode_brng
            """,
            [("kode_brng", "Kode Barang"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"),
             .. balancesBefore, .. Flows.SelectMany(flow => new[]
             {
                 ($"jml_{flow.Column}", $"Jml {flow.Label}"), ($"nilai_{flow.Column}", $"Nilai {flow.Label}"),
             }), .. balancesAfter],
            ReportPeriod.DateRange, fromStocktake
                ? "Mutasi dihitung mulai opname pertama dalam periode. Bila stok opname kosong atau nol, saldo hitung memakai stok saat ini."
                : "Stok memakai saldo saat ini dan harga dasar. Mutasi memakai detail transaksi dalam periode pencarian.")
        { SearchWatermark = "Kode, nama barang, atau satuan" };
    }
}
