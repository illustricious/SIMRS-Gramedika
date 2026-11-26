using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

public static class MenuSSpecs
{
    private static readonly LogisticsCatalog Catalog = new("toko", "Toko", "", "", "tokojenisbarang");
    private static readonly string[] PriceColumns = ["dasar", "h_beli", "distributor", "grosir", "retail"];

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnSuplierToko"] = LogisticsMasterSpecs.Suppliers(Catalog),
        ["btnJenisToko"] = new("Jenis Barang Toko", "tokojenisbarang",
            [new("kd_jenis", "Kode", MaxLen: 5, Key: true), new("nm_jenis", "Nama Jenis", MaxLen: 50, Required: true)],
            "SELECT kd_jenis,nm_jenis FROM tokojenisbarang WHERE CONCAT_WS(' ',kd_jenis,nm_jenis) LIKE @p0 ORDER BY nm_jenis,kd_jenis",
            [("kd_jenis", "Kode"), ("nm_jenis", "Nama Jenis")]),
        ["btnMemberToko"] = new MasterSpec("Member Toko", "tokomember",
            [
                new("no_member", "No. Member", MaxLen: 10, Key: true),
                new("nama", "Nama", MaxLen: 50, Required: true),
                new("jk", "Jenis Kelamin", FieldKind.Choice, Choices: ["L", "P"]),
                new("tmp_lahir", "Tempat Lahir", MaxLen: 20),
                new("tgl_lahir", "Tanggal Lahir", FieldKind.Date),
                new("alamat", "Alamat", MaxLen: 60), new("no_telp", "Telepon", MaxLen: 40),
                new("email", "Email", MaxLen: 60),
            ],
            """
            SELECT no_member,nama,jk,tmp_lahir,tgl_lahir,alamat,no_telp,email FROM tokomember
            WHERE CONCAT_WS(' ',no_member,nama,alamat,no_telp,email) LIKE @p0 ORDER BY nama,no_member
            """,
            [("no_member", "No. Member"), ("nama", "Nama"), ("jk", "Jenis Kelamin"),
             ("tmp_lahir", "Tempat Lahir"), ("tgl_lahir", "Tanggal Lahir"), ("alamat", "Alamat"),
             ("no_telp", "Telepon"), ("email", "Email")])
        { CanDelete = false, Hint = "Data member dapat terkait dengan riwayat transaksi dan retur." },

        ["btnBarangToko"] = new MasterSpec("Barang Toko", "tokobarang",
            [
                new("kode_brng", "Kode Barang", MaxLen: 40, Key: true),
                new("nama_brng", "Nama Barang", MaxLen: 80, Required: true),
                new("kode_sat", "Satuan", FieldKind.Lookup, Required: true,
                    LookupSql: "SELECT kode_sat AS kode,satuan AS nama FROM kodesatuan ORDER BY satuan,kode_sat"),
                new("jenis", "Jenis Barang", FieldKind.Lookup,
                    LookupSql: "SELECT kd_jenis AS kode,nm_jenis AS nama FROM tokojenisbarang ORDER BY nm_jenis,kd_jenis")
                    { NullWhenEmpty = true },
                new("dasar", "Harga Dasar", FieldKind.Number), new("h_beli", "Harga Beli", FieldKind.Number),
                new("distributor", "Harga Distributor", FieldKind.Number), new("grosir", "Harga Grosir", FieldKind.Number),
                new("retail", "Harga Retail", FieldKind.Number),
            ],
            """
            SELECT b.*,s.satuan,j.nm_jenis FROM tokobarang b
            LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            LEFT JOIN tokojenisbarang j ON j.kd_jenis=b.jenis
            WHERE CONCAT_WS(' ',b.kode_brng,b.nama_brng,s.satuan,j.nm_jenis,b.status) LIKE @p0
            ORDER BY b.status DESC,b.nama_brng,b.kode_brng
            """,
            [("kode_brng", "Kode Barang"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"),
             ("nm_jenis", "Jenis"), ("stok", "Stok Saat Ini"), ("dasar", "Harga Dasar"), ("h_beli", "Harga Beli"),
             ("distributor", "Distributor"), ("grosir", "Grosir"), ("retail", "Retail"), ("status", "Aktif (1/0)")])
        {
            CanDelete = false,
            ComputedColumns = ["stok", "status"],
            BeforeSave = (_, values, originalKeys) =>
            {
                if (PriceColumns.Any(column => values.GetValueOrDefault(column) is not double price
                    || !double.IsFinite(price) || price < 0))
                    throw new ArgumentException("Harga dasar, beli, distributor, grosir, dan retail harus nol atau lebih.");
                if (originalKeys is null)
                {
                    values["stok"] = 0d;
                    values["status"] = "1";
                }
                return Task.CompletedTask;
            },
            Actions = [
                new("Nonaktifkan", "UPDATE tokobarang SET status='0' WHERE kode_brng=@p0 AND status='1'", "Nonaktifkan barang terpilih?"),
                new("Aktifkan", "UPDATE tokobarang SET status='1' WHERE kode_brng=@p0 AND status='0'", "Aktifkan barang terpilih?"),
            ],
            Hint = "Harga diisi manual. Barang baru dimulai dengan stok 0. Stok berubah melalui transaksi toko di Khanza.",
        },
    };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports =
        MenuSTransactionReports.Specs.Concat(MenuSFinancialReports.Specs).Concat(MenuSStockReports.Specs)
            .Append(new("btnRiwayatBarangToko", new LogisticsStockSpecs(Catalog).History("Riwayat Barang Toko")))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
}
