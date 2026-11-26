using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

internal sealed class LogisticsTransactionSpecs(LogisticsCatalog catalog)
{
    private record Document(
        string Header, string Detail, string Key, string Date, string Quantity, string? Price, string? Total,
        string Parties, string Search, (string Sql, string Column, string Label)[] Extra);

    private const string Staff = "LEFT JOIN petugas p ON p.nip=h.nip";
    private const string Employee = "LEFT JOIN pegawai p ON p.nik=h.nip";
    private string Supplier => $"LEFT JOIN {catalog.Suppliers} v ON v.kode_suplier=h.kode_suplier";
    private const string VendorSearch = "h.kode_suplier,v.nama_suplier";

    private static readonly (string Sql, string Column, string Label) StaffName = ("p.nama", "petugas", "Petugas");
    private static readonly (string Sql, string Column, string Label) VendorName = ("v.nama_suplier", "suplier", "Suplier");

    private Document Request => new(
        "permintaan_" + catalog.RequestSuffix, "detail_permintaan_" + catalog.RequestSuffix, "no_permintaan", "tanggal", "jumlah", null, null,
        Employee, "h.ruang,h.status,x.keterangan",
        [("h.ruang", "ruang", "Ruang"), StaffName, ("h.status", "status", "Status"), ("x.keterangan", "keterangan", "Keterangan")]);

    private Document Proposal => new(
        "pengajuan_barang_" + catalog.ProposalSuffix, "detail_pengajuan_barang_" + catalog.ProposalSuffix, "no_pengajuan", "tanggal", "jumlah", "h_pengajuan", "total",
        Employee, "h.status,h.keterangan",
        [StaffName, ("h.status", "status", "Status"), ("h.keterangan", "keterangan", "Keterangan")]);

    private Document Order => new(
        catalog.Order, "detail_" + catalog.Order, "no_pemesanan", "tanggal", "jumlah", "h_pesan", "total",
        Employee + " " + Supplier, VendorSearch + ",h.status",
        [VendorName, StaffName, ("h.status", "status", "Status"), ("x.besardis", "diskon", "Diskon Item")]);

    private Document Purchase => new(
        catalog.Purchase, catalog.PurchaseItems, "no_faktur", "tgl_beli", "jumlah", "harga", "total",
        Staff + " " + Supplier, VendorSearch,
        [VendorName, StaffName, ("x.subtotal", "subtotal", "Subtotal Item"), ("x.besardis", "diskon", "Diskon Item")]);

    private Document Receipt => new(
        catalog.Receipt, catalog.ReceiptItems, "no_faktur", "tgl_pesan", "jumlah", "harga", "total",
        Staff + " " + Supplier, VendorSearch + ",h.no_order,h.status",
        [VendorName, StaffName, ("h.no_order", "no_order", "No. Order"), ("h.tgl_faktur", "tgl_faktur", "Tgl. Faktur"),
         ("h.tgl_tempo", "tgl_tempo", "Jatuh Tempo"), ("h.status", "status", "Status Bayar"), ("x.besardis", "diskon", "Diskon Item")]);

    private Document Grant => new(
        catalog.Prefix + "_hibah", catalog.Prefix + "_detail_hibah", "no_hibah", "tgl_hibah", "jumlah", "h_hibah", "subtotalhibah",
        Staff + " LEFT JOIN pemberihibah v ON v.kode_pemberi=h.kode_pemberi", "v.nama_pemberi,h.kode_pemberi,h.keterangan",
        [("v.nama_pemberi", "pemberi", "Pemberi Hibah"), StaffName, ("h.keterangan", "keterangan", "Keterangan")]);

    private Document Issue => new(
        catalog.Issue, catalog.IssueItems, "no_keluar", "tanggal", "jumlah", "harga", "total",
        Staff, "h.keterangan", [StaffName, ("h.keterangan", "keterangan", "Keterangan")]);

    private Document Return => new(
        catalog.Prefix + "returbeli", catalog.Prefix + "_detail_returbeli", "no_retur_beli", "tgl_retur", "jml_retur", "h_retur", "total",
        Staff + " " + Supplier, VendorSearch + ",h.catatan,x.no_faktur",
        [VendorName, StaffName, ("x.no_faktur", "no_faktur", "No. Faktur Asal"), ("h.catatan", "catatan", "Catatan")]);

    private static readonly (string, string)[] ItemColumns =
        [("kode_brng", "Kode Barang"), ("nama_brng", "Barang"), ("jenis", "Jenis"),
         ("kode_sat", "Kode Satuan"), ("satuan", "Satuan Transaksi")];

    private string ItemSelect => $"x.kode_brng,b.nama_brng,{catalog.Category} AS jenis,x.kode_sat,s.satuan";
    private string ItemGroup => $"x.kode_brng,b.nama_brng,{catalog.Category},x.kode_sat,s.satuan";

    private string DetailFilter(Document d, bool includeEmptyDocuments = false)
    {
        var join = includeEmptyDocuments ? "LEFT JOIN" : "JOIN";
        return $"""
            FROM {d.Header} h {join} {d.Detail} x ON x.{d.Key}=h.{d.Key}
            LEFT JOIN {catalog.Items} b ON b.kode_brng=x.kode_brng
            {catalog.CategoryJoin}
            LEFT JOIN kodesatuan s ON s.kode_sat=x.kode_sat
            {d.Parties}
            WHERE h.{d.Date} BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.{d.Key},h.nip,p.nama,x.kode_brng,b.nama_brng,{catalog.Category},x.kode_sat,s.satuan,{d.Search}) LIKE @p0
            """;
    }

    private ReportSpec Items(string title, Document d, string numberLabel, string dateLabel = "Tanggal") => new(
        title,
        $"""
        SELECT h.{d.Key} AS nomor,h.{d.Date} AS tanggal,{ItemSelect},x.{d.Quantity} AS jumlah
               {(d.Price is null ? "" : $",x.{d.Price} AS harga")}
               {(d.Total is null ? "" : $",x.{d.Total} AS nilai")}
               {string.Concat(d.Extra.Select(e => $",{e.Sql} AS {e.Column}"))}
        {DetailFilter(d, includeEmptyDocuments: true)}
        ORDER BY h.{d.Date} DESC,h.{d.Key},x.kode_brng,x.kode_sat
        """,
        [("nomor", numberLabel), ("tanggal", dateLabel), .. ItemColumns, ("jumlah", "Jumlah"),
         .. (d.Price is null ? Array.Empty<(string, string)>() : new[] { ("harga", "Harga Satuan") }),
         .. (d.Total is null ? Array.Empty<(string, string)>() : new[] { ("nilai", "Nilai Item") }),
         .. d.Extra.Select(e => (e.Column, e.Label))],
        ReportPeriod.DateRange, "Rincian per item dokumen. Pencatatan transaksi dilakukan di Khanza.")
    { SearchWatermark = "No. dokumen, barang, petugas, atau suplier" };

    private ReportSpec Summary(string title, Document d, bool perDay = false)
    {
        var date = perDay ? $"h.{d.Date}," : "";
        var value = d.Total is null ? "" : $",SUM(x.{d.Total}) AS nilai";
        return new(
            title,
            $"""
            SELECT {(perDay ? $"h.{d.Date} AS tanggal," : "")}{ItemSelect},
                   SUM(x.{d.Quantity}) AS jumlah,COUNT(DISTINCT h.{d.Key}) AS dokumen{value}
            {DetailFilter(d)}
            GROUP BY {date}{ItemGroup}
            ORDER BY {date}b.nama_brng,x.kode_brng,x.kode_sat
            """,
            [.. (perDay ? new[] { ("tanggal", "Tanggal") } : Array.Empty<(string, string)>()),
             .. ItemColumns, ("jumlah", "Jumlah"), ("dokumen", "Jumlah Dokumen"),
             .. (d.Total is null ? Array.Empty<(string, string)>() : new[] { ("nilai", "Nilai Item") })],
            ReportPeriod.DateRange, perDay ? "Jumlah per tanggal, barang, dan satuan transaksi." : "Jumlah per barang dan satuan transaksi.")
        { SearchWatermark = "No. dokumen, barang, petugas, atau suplier" };
    }

    private ReportSpec InvoiceSummary(string title, bool receipt)
    {
        var d = receipt ? Receipt : Purchase;
        var total = receipt ? "h.total2" : "h.total";
        var tags = receipt ? "h.no_order,h.status," : "";
        return new(
            title,
            $"""
            SELECT h.no_faktur,h.{d.Date} AS tanggal,{tags}v.nama_suplier,p.nama AS petugas,
                   {total} AS nilai,h.ppn,h.meterai,h.tagihan
            FROM {d.Header} h {d.Parties}
            WHERE h.{d.Date} BETWEEN @p1 AND @p2
              AND (CONCAT_WS(' ',{tags}h.no_faktur,h.kode_suplier,v.nama_suplier,h.nip,p.nama) LIKE @p0
                   OR EXISTS (
                       SELECT 1 FROM {d.Detail} x LEFT JOIN {catalog.Items} b ON b.kode_brng=x.kode_brng
                       {catalog.CategoryJoin}
                       WHERE x.no_faktur=h.no_faktur AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,{catalog.Category}) LIKE @p0
                   ))
            ORDER BY h.{d.Date} DESC,h.no_faktur
            """,
            [("no_faktur", "No. Faktur"), ("tanggal", receipt ? "Tgl. Pesan" : "Tgl. Beli"),
             .. (receipt ? new[] { ("no_order", "No. Order"), ("status", "Status Bayar") } : Array.Empty<(string, string)>()),
             ("nama_suplier", "Suplier"), ("petugas", "Petugas"), ("nilai", "Total Sebelum Pajak"),
             ("ppn", "PPN"), ("meterai", "Meterai"), ("tagihan", "Tagihan")],
            ReportPeriod.DateRange, "Satu baris per faktur. Jumlah dan pajak diambil dari nilai yang tersimpan.")
        { SearchWatermark = "Faktur, order, suplier, barang, atau petugas" };
    }

    public IReadOnlyDictionary<string, ReportSpec> NonmedicalReports() => new Dictionary<string, ReportSpec>
    {
        ["btnPermintaanNonMedis"] = Items("Permintaan Barang Non Medis", Request, "No. Permintaan"),
        ["btnRekapPermintaanNonMedis"] = Summary("Ringkasan Permintaan Barang Non Medis", Request),
        ["btnPengajuanBarangNonMedis"] = Items("Pengajuan Barang Non Medis", Proposal, "No. Pengajuan"),
        ["btnRingkasanPengajuanNonMedis"] = Summary("Ringkasan Pengajuan Non Medis", Proposal),
        ["btnSuratPemesananNonMedis"] = Items("Surat Pemesanan Barang Non Medis", Order, "No. Pemesanan"),
        ["btnRingkasanPemesananNonMedis"] = Summary("Ringkasan Pemesanan Non Medis", Order),
        ["btnPembelianIpsrs"] = Items("Pengadaan Barang Non Medis", Purchase, "No. Faktur", "Tgl. Beli"),
        ["btnRingkasanPengadaanNonMedis"] = Summary("Ringkasan Pengadaan Non Medis", Purchase),
        ["btnPenerimaanNonMedis"] = Items("Penerimaan Barang Non Medis", Receipt, "No. Faktur", "Tgl. Pesan"),
        ["btnRingkasanPenerimaanNonMedis"] = Summary("Ringkasan Penerimaan Non Medis", Receipt),
        ["btnHibahNonMedis"] = Items("Hibah Non Medis", Grant, "No. Hibah"),
        ["btnPengeluaranIpsrs"] = Items("Stok Keluar Non Medis", Issue, "No. Keluar"),
        ["btnReturBarangNonMedis"] = Items("Retur Ke Suplier Non Medis", Return, "No. Retur"),
        ["btnRingkasanReturSuplierNonMedis"] = Summary("Ringkasan Retur Suplier Non Medis", Return),
        ["btnRHMasukIpsrs"] = InvoiceSummary("Rekap Pengadaan Non Medis", receipt: false),
        ["btnRHKeluarIpsrs"] = Summary("Rekap Stok Keluar Non Medis", Issue),
        ["btnPengadaanIPSRSPerTanggal"] = Summary("Pengadaan Non Medis Per Tanggal", Purchase, perDay: true),
        ["btnStokKeluarIPSRSPerTanggal"] = Summary("Stok Keluar Non Medis Per Tanggal", Issue, perDay: true),
        ["btnRekapPenerimaanNonMedis"] = InvoiceSummary("Rekap Penerimaan Non Medis", receipt: true),
    };

    public IReadOnlyDictionary<string, ReportSpec> KitchenReports() => new Dictionary<string, ReportSpec>
    {
        ["btnPermintaanDapur"] = Items("Permintaan Barang Dapur", Request, "No. Permintaan"),
        ["btnPengajuanBarangDapur"] = Items("Pengajuan Barang Dapur", Proposal, "No. Pengajuan"),
        ["btnRingkasanPengajuanDapur"] = Summary("Ringkasan Pengajuan Dapur", Proposal),
        ["btnSuratPemesananDapur"] = Items("Surat Pemesanan Barang Dapur", Order, "No. Pemesanan"),
        ["btnRingkasanPemesananDapur"] = Summary("Ringkasan Pemesanan Dapur", Order),
        ["btnPembelianDapur"] = Items("Pengadaan Barang Dapur", Purchase, "No. Faktur", "Tgl. Beli"),
        ["btnRingkasanPengadaanDapur"] = Summary("Ringkasan Pengadaan Dapur", Purchase),
        ["btnPenerimaanBarangDapur"] = Items("Penerimaan Barang Dapur", Receipt, "No. Faktur", "Tgl. Pesan"),
        ["btnRingkasanPenerimaanDapur"] = Summary("Ringkasan Penerimaan Dapur", Receipt),
        ["btnHibahDapur"] = Items("Hibah Barang Dapur", Grant, "No. Hibah"),
        ["btnPengeluaranDapur"] = Items("Stok Keluar Dapur", Issue, "No. Keluar"),
        ["btnRingkasanStokKeluarDapur"] = Summary("Ringkasan Stok Keluar Dapur", Issue),
        ["btnReturBarangDapur"] = Items("Retur Ke Suplier Dapur", Return, "No. Retur"),
        ["btnRingkasanReturBeliDapur"] = Summary("Ringkasan Retur Suplier Dapur", Return),
        ["btnRekapPengadaanDapur"] = InvoiceSummary("Rekap Pengadaan Dapur", receipt: false),
        ["btnStokKeluarDapurPerTanggal"] = Summary("Stok Keluar Dapur Per Tanggal", Issue, perDay: true),
    };
}
