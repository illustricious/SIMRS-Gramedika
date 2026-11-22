using SimrsKhanza.Data.Master;

namespace SimrsKhanza.Data.Inventaris;

internal static class MenuSTransactionReports
{
    private const string Supplier = "LEFT JOIN tokosuplier party ON party.kode_suplier=h.kode_suplier";
    private const string Member = "LEFT JOIN tokomember party ON party.no_member=h.no_member";
    private const string Staff = "LEFT JOIN petugas p ON p.nip=h.nip";
    private const string Employee = "LEFT JOIN pegawai p ON p.nik=h.nip";

    private static readonly (string, string)[] SupplierColumn = [("pihak", "Suplier")];
    private static readonly (string, string)[] MemberColumn = [("pihak", "Member")];

    internal static readonly IReadOnlyDictionary<string, ReportSpec> Specs = new Dictionary<string, ReportSpec>
    {
        ["btnPengajuanBarangToko"] = Lines("Pengajuan Barang Toko", "toko_pengajuan_barang", "toko_detail_pengajuan_barang",
            "no_pengajuan", "tanggal", Employee, "", "x.jumlah", "h_pengajuan",
            "h.status,h.keterangan", [("status", "Status"), ("keterangan", "Keterangan")]),
        ["btnSuratPemesananToko"] = Lines("Surat Pemesanan Toko", "toko_surat_pemesanan", "toko_detail_surat_pemesanan",
            "no_pemesanan", "tanggal", Supplier + " " + Staff, "party.nama_suplier", "x.jumlah", "h_pesan",
            "h.status", [.. SupplierColumn, ("status", "Status")]),
        ["btnPenerimaanBarangToko"] = Lines("Penerimaan Barang Toko", "tokopemesanan", "toko_detail_pesan",
            "no_faktur", "tgl_pesan", Supplier + " " + Staff, "party.nama_suplier", "x.jumlah", "harga",
            "h.no_order,h.tgl_faktur,h.status",
            [.. SupplierColumn, ("no_order", "No. Pesanan"), ("tgl_faktur", "Tanggal Faktur"), ("status", "Status Bayar")]),
        ["btnPengadaanBarangToko"] = Lines("Pengadaan Barang Toko", "tokopembelian", "toko_detail_beli",
            "no_faktur", "tgl_beli", Supplier + " " + Staff, "party.nama_suplier", "x.jumlah", "harga",
            "h.kd_rek", [.. SupplierColumn, ("kd_rek", "Rekening")]),
        ["btnPenjualanToko"] = Lines("Penjualan Toko", "tokopenjualan", "toko_detail_jual",
            "nota_jual", "tgl_jual", Member + " " + Staff, "COALESCE(NULLIF(h.nm_member,''),party.nama)", "x.jumlah", "h_jual",
            "h.nama_bayar,h.jns_jual,h.keterangan",
            [.. MemberColumn, ("nama_bayar", "Cara Bayar"), ("jns_jual", "Jenis Jual"), ("keterangan", "Keterangan")]),
        ["btnPiutangToko"] = Lines("Piutang Toko", "tokopiutang", "toko_detail_piutang",
            "nota_piutang", "tgl_piutang", Member + " " + Staff, "COALESCE(NULLIF(h.nm_member,''),party.nama)", "x.jumlah", "h_jual",
            "h.tgltempo,h.jns_jual,h.catatan",
            [.. MemberColumn, ("tgltempo", "Jatuh Tempo"), ("jns_jual", "Jenis Jual"), ("catatan", "Catatan")]),
        ["btnReturKeSuplierToko"] = Lines("Retur Ke Suplier Toko", "tokoreturbeli", "toko_detail_returbeli",
            "no_retur_beli", "tgl_retur", Supplier + " " + Staff, "party.nama_suplier", "x.jml_retur", "h_retur",
            "x.no_faktur,h.catatan", [.. SupplierColumn, ("no_faktur", "No. Faktur Asal"), ("catatan", "Catatan")]),
        ["btnReturJualToko"] = Lines("Retur Jual Toko", "tokoreturjual", "toko_detail_returjual",
            "no_retur_jual", "tgl_retur", Member + " " + Staff, "party.nama", "x.jml_retur", "h_retur",
            "x.nota_jual,h.catatan", [.. MemberColumn, ("nota_jual", "Nota Jual Asal"), ("catatan", "Catatan")]),
        ["btnReturPiutangToko"] = Lines("Retur Piutang Toko", "tokoreturpiutang", "toko_detail_returpiutang",
            "no_retur_piutang", "tgl_retur", Member + " " + Staff, "party.nama", "x.jml_retur", "h_retur",
            "x.nota_piutang,h.catatan", [.. MemberColumn, ("nota_piutang", "Nota Piutang Asal"), ("catatan", "Catatan")]),
    };

    private static ReportSpec Lines(string title, string headerTable, string detailTable, string document, string date,
        string headerJoins, string party, string quantity, string price, string extras, (string, string)[] context)
    {
        var partySql = party.Length == 0 ? "NULL" : party;
        return new(title, $"""
            SELECT h.{document} AS dokumen,h.{date} AS tanggal,{partySql} AS pihak,h.nip,p.nama AS petugas,
                   x.kode_brng,b.nama_brng,s.satuan,{quantity} AS jumlah,x.{price} AS harga,x.total,{extras}
            FROM {headerTable} h
            {headerJoins}
            LEFT JOIN {detailTable} x ON x.{document}=h.{document}
            LEFT JOIN tokobarang b ON b.kode_brng=x.kode_brng
            LEFT JOIN kodesatuan s ON s.kode_sat=x.kode_sat
            WHERE h.{date} BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.{document},h.nip,p.nama,{partySql},x.kode_brng,b.nama_brng,s.satuan,{extras}) LIKE @p0
            ORDER BY h.{date} DESC,h.{document},x.kode_brng
            """,
            [("tanggal", "Tanggal"), ("dokumen", "No. Dokumen"), .. context,
             ("nip", "NIP"), ("petugas", "Petugas"), ("kode_brng", "Kode Barang"), ("nama_brng", "Nama Barang"),
             ("satuan", "Satuan"), ("jumlah", "Jumlah"), ("harga", "Harga Satuan"), ("total", "Total Baris")],
            ReportPeriod.DateRange, "Satu baris per detail barang. Transaksi dicatat di Khanza.")
        { SearchWatermark = "Dokumen, barang, petugas, atau keterangan", TotalColumns = ["total"] };
    }
}
