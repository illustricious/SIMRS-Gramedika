using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

internal static class LogisticsMasterSpecs
{
    public static MasterSpec Items(string title, LogisticsCatalog catalog, MasterField category) => new(
        title, catalog.Items,
        [
            new("kode_brng", "Kode Barang", MaxLen: 15, Key: true),
            new("nama_brng", "Nama Barang", MaxLen: 80, Required: true),
            new("kode_sat", "Satuan", FieldKind.Lookup, Required: true,
                LookupSql: "SELECT kode_sat AS kode,satuan AS nama FROM kodesatuan ORDER BY satuan,kode_sat"),
            category,
            new("harga", "Harga", FieldKind.Number),
        ],
        $"""
        SELECT b.*,s.satuan,{catalog.Category} AS nm_jenis
        FROM {catalog.Items} b
        LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
        {catalog.CategoryJoin}
        WHERE CONCAT_WS(' ',b.kode_brng,b.nama_brng,s.satuan,{catalog.Category},b.status) LIKE @p0
        ORDER BY b.status DESC,b.nama_brng,b.kode_brng
        """,
        [("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"), ("nm_jenis", "Jenis"),
         ("stok", "Stok Saat Ini"), ("harga", "Harga"), ("status", "Aktif (1/0)")])
    {
        CanDelete = false,
        ComputedColumns = ["stok", "status"],
        BeforeSave = (_, values, originalKeys) =>
        {
            if (values.GetValueOrDefault("harga") is not double harga || !double.IsFinite(harga) || harga < 0)
                throw new ArgumentException("Harga harus berupa angka nol atau lebih.");
            if (originalKeys is null)
            {
                values["stok"] = 0d;
                values["status"] = "1";
            }
            return Task.CompletedTask;
        },
        Actions = [new("Nonaktifkan", $"UPDATE {catalog.Items} SET status='0' WHERE kode_brng=@p0 AND status='1'",
            "Nonaktifkan barang terpilih?")],
        Hint = "Barang baru dimulai dengan stok 0. Stok berubah melalui transaksi logistik di Khanza.",
    };

    public static MasterSpec Suppliers(LogisticsCatalog catalog) => new($"Suplier {catalog.Label}", catalog.Suppliers,
        [
            new("kode_suplier", "Kode", MaxLen: 5, Key: true),
            new("nama_suplier", "Nama Suplier", MaxLen: 50, Required: true),
            new("alamat", "Alamat", MaxLen: 50), new("kota", "Kota", MaxLen: 20), new("no_telp", "Telepon", MaxLen: 13),
            new("nama_bank", "Bank", MaxLen: 30), new("rekening", "No. Rekening", MaxLen: 20),
        ],
        $"""
        SELECT * FROM {catalog.Suppliers}
        WHERE CONCAT_WS(' ',kode_suplier,nama_suplier,alamat,kota,no_telp,nama_bank,rekening) LIKE @p0
        ORDER BY nama_suplier,kode_suplier
        """,
        [("kode_suplier", "Kode"), ("nama_suplier", "Nama Suplier"), ("alamat", "Alamat"), ("kota", "Kota"),
         ("no_telp", "Telepon"), ("nama_bank", "Bank"), ("rekening", "No. Rekening")])
    { CanDelete = false, Hint = "Data suplier dapat terkait dengan riwayat retur barang." };
}
