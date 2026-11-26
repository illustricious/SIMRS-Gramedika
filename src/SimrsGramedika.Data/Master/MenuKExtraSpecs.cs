namespace SimrsGramedika.Data.Master;

/// <summary>Menu K, second batch of editable masters: more tarif and finance reference tables.</summary>
public static class MenuKExtraSpecs
{
    private static MasterField Key(string column, string label, int len) => new(column, label, MaxLen: len, Key: true);

    private static MasterSpec ServiceTarif(string title, string table, string managementColumn, bool withClass)
    {
        string[] parts = ["bagian_rs", "bhp", "tarif_perujuk", "tarif_tindakan_dokter", "tarif_tindakan_petugas", "kso", managementColumn];
        return new(title, table,
            [
                Key("kd_jenis_prw", "Kode Tarif", 15), new("nm_perawatan", "Nama Pemeriksaan", MaxLen: 80, Required: true),
                MenuKSpecs.Money("bagian_rs", "Bagian RS"), MenuKSpecs.Money("bhp", "BHP"), MenuKSpecs.Money("tarif_perujuk", "Jasa Perujuk"),
                MenuKSpecs.Money("tarif_tindakan_dokter", "Jasa Dokter"), MenuKSpecs.Money("tarif_tindakan_petugas", "Jasa Petugas"),
                MenuKSpecs.Money("kso", "KSO"), MenuKSpecs.Money(managementColumn, "Manajemen"),
                new("kd_pj", "Cara Bayar", FieldKind.Lookup, Required: true, LookupSql: MenuKSpecs.Penjab),
                .. withClass ? [new MasterField("kelas", "Kelas", FieldKind.Choice, Choices: MenuKSpecs.KelasLab, Required: true)] : Array.Empty<MasterField>(),
                new("status", "Aktif (1/0)", FieldKind.Choice, Choices: MenuKSpecs.Aktif, Required: true) { Default = "1" },
            ],
            $"""
            SELECT t.*,j.png_jawab FROM `{table}` t LEFT JOIN penjab j ON j.kd_pj=t.kd_pj
            WHERE CONCAT_WS(' ',t.kd_jenis_prw,t.nm_perawatan,j.png_jawab) LIKE @p0 ORDER BY t.status DESC,t.nm_perawatan,t.kd_jenis_prw
            """,
            [("kd_jenis_prw", "Kode"), ("nm_perawatan", "Pemeriksaan"), ("png_jawab", "Cara Bayar"),
             .. withClass ? [("kelas", "Kelas")] : Array.Empty<(string, string)>(),
             ("bagian_rs", "Bagian RS"), ("bhp", "BHP"), ("tarif_perujuk", "Jasa Perujuk"), ("tarif_tindakan_dokter", "Jasa Dokter"),
             ("tarif_tindakan_petugas", "Jasa Petugas"), ("kso", "KSO"), (managementColumn, "Manajemen"), ("total_byr", "Total"), ("status", "Aktif")])
        {
            CanDelete = false,
            ComputedColumns = ["total_byr"],
            BeforeSave = (_, v, _) =>
            {
                MenuKSpecs.NonNegative(v, parts);
                v["total_byr"] = parts.Sum(column => MenuKSpecs.D(v, column));
                return Task.CompletedTask;
            },
            Hint = "Total dihitung otomatis dari komponen tarif. Nonaktifkan tarif lewat kolom Aktif.",
        };
    }

    private static MasterSpec Party(string title, string table, string key, string name, string keyLabel) => new(title, table,
        [
            Key(key, keyLabel, 5), new(name, "Nama", MaxLen: 50, Required: true), new("alamat", "Alamat", MaxLen: 150),
            new("no_telp", "Telepon", MaxLen: 13), MenuKSpecs.Account("kd_rek", "Akun Rekening"),
            new("status", "Aktif (1/0)", FieldKind.Choice, Choices: MenuKSpecs.Aktif, Required: true) { Default = "1" },
        ],
        $"""
        SELECT t.*,r.nm_rek FROM `{table}` t LEFT JOIN rekening r ON r.kd_rek=t.kd_rek
        WHERE CONCAT_WS(' ',t.{key},t.{name},t.alamat,t.no_telp,t.kd_rek,r.nm_rek) LIKE @p0 ORDER BY t.status DESC,t.{name},t.{key}
        """,
        [(key, keyLabel), (name, "Nama"), ("alamat", "Alamat"), ("no_telp", "Telepon"), ("kd_rek", "Rekening"), ("nm_rek", "Nama Rekening"), ("status", "Aktif")])
    { CanDelete = false, Hint = "Dinonaktifkan, tidak dihapus, karena terkait riwayat piutang atau hutang." };

    private static readonly string[] OperationParts =
    [
        "operator1", "operator2", "operator3", "asisten_operator1", "asisten_operator2", "asisten_operator3", "instrumen",
        "dokter_anak", "perawaat_resusitas", "dokter_anestesi", "asisten_anestesi", "asisten_anestesi2", "bidan", "bidan2",
        "bidan3", "perawat_luar", "sewa_ok", "alat", "akomodasi", "bagian_rs", "omloop", "omloop2", "omloop3", "omloop4",
        "omloop5", "sarpras", "dokter_pjanak", "dokter_umum",
    ];

    private static string Label(string column) => string.Join(' ', column.Replace("perawaat", "perawat").Split('_')
        .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    private static MasterSpec Operation() => new("Tarif Operasi/VK", "paket_operasi",
        [
            Key("kode_paket", "Kode Paket", 15), new("nm_perawatan", "Nama Paket", MaxLen: 80, Required: true),
            new("kategori", "Kategori", FieldKind.Choice, Choices: ["Operasi", "Kebidanan"], Required: true),
            .. OperationParts.Select(column => MenuKSpecs.Money(column, Label(column))),
            new("kd_pj", "Cara Bayar", FieldKind.Lookup, Required: true, LookupSql: MenuKSpecs.Penjab),
            new("status", "Aktif (1/0)", FieldKind.Choice, Choices: MenuKSpecs.Aktif, Required: true) { Default = "1" },
            new("kelas", "Kelas", FieldKind.Choice, Choices: MenuKSpecs.KelasLab, Required: true),
        ],
        $"""
        SELECT p.*,j.png_jawab,({string.Join('+', OperationParts.Select(column => $"COALESCE(p.{column},0)"))}) AS total_paket
        FROM paket_operasi p LEFT JOIN penjab j ON j.kd_pj=p.kd_pj
        WHERE CONCAT_WS(' ',p.kode_paket,p.nm_perawatan,p.kategori,j.png_jawab,p.kelas) LIKE @p0
        ORDER BY p.status DESC,p.nm_perawatan,p.kode_paket
        """,
        [("kode_paket", "Kode"), ("nm_perawatan", "Nama Paket"), ("kategori", "Kategori"), ("png_jawab", "Cara Bayar"),
         ("kelas", "Kelas"), ("operator1", "Operator 1"), ("dokter_anestesi", "Dokter Anestesi"), ("sewa_ok", "Sewa OK"),
         ("alat", "Alat"), ("bagian_rs", "Bagian RS"), ("total_paket", "Total Paket"), ("status", "Aktif")])
    {
        CanDelete = false,
        BeforeSave = (_, v, _) =>
        {
            MenuKSpecs.NonNegative(v, OperationParts);
            return Task.CompletedTask;
        },
        Hint = "Total paket dijumlahkan dari semua komponen saat daftar dimuat. Paket dinonaktifkan, tidak dihapus.",
    };

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnTarifRadiologi"] = ServiceTarif("Tarif Radiologi", "jns_perawatan_radiologi", "menejemen", true),
        ["btnTarifUtd"] = ServiceTarif("Tarif UTD", "jns_perawatan_utd", "manajemen", false),
        ["btnPaketOperasi"] = Operation(),
        ["btnSetTarifOnline"] = new("Set Tarif Online", "set_tarif_online",
            [new("kd_jenis_prw", "Tarif Ralan", FieldKind.Lookup, Key: true, Required: true,
                LookupSql: "SELECT kd_jenis_prw AS kode,nm_perawatan AS nama FROM jns_perawatan ORDER BY nm_perawatan")],
            """
            SELECT s.kd_jenis_prw,t.nm_perawatan,t.total_byrdr,t.total_byrpr,t.total_byrdrpr FROM set_tarif_online s
            LEFT JOIN jns_perawatan t ON t.kd_jenis_prw=s.kd_jenis_prw
            WHERE CONCAT_WS(' ',s.kd_jenis_prw,t.nm_perawatan) LIKE @p0 ORDER BY t.nm_perawatan
            """,
            [("kd_jenis_prw", "Kode"), ("nm_perawatan", "Tarif"), ("total_byrdr", "Total Dokter"),
             ("total_byrpr", "Total Perawat"), ("total_byrdrpr", "Total Dokter+Perawat")])
        { Hint = "Tarif yang dicantumkan di sini tampil pada pendaftaran online." },
        ["btnAkunPenagihanPiutang"] = new("Akun Penagihan Piutang", "akun_penagihan_piutang",
            [
                new("kd_rek", "Akun Rekening", FieldKind.Lookup, Key: true, Required: true, LookupSql: MenuKSpecs.Rekening),
                new("nama_bank", "Nama Bank", MaxLen: 70), new("atas_nama", "Atas Nama", MaxLen: 50), new("no_rek", "No. Rekening", MaxLen: 20),
            ],
            """
            SELECT a.kd_rek,r.nm_rek,a.nama_bank,a.atas_nama,a.no_rek FROM akun_penagihan_piutang a LEFT JOIN rekening r ON r.kd_rek=a.kd_rek
            WHERE CONCAT_WS(' ',a.kd_rek,r.nm_rek,a.nama_bank,a.atas_nama,a.no_rek) LIKE @p0 ORDER BY a.kd_rek
            """,
            [("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening"), ("nama_bank", "Bank"), ("atas_nama", "Atas Nama"), ("no_rek", "No. Rekening")]),
        ["btnAkunAsetInventaris"] = new("Akun Jenis Aset/Inventaris", "akun_aset_inventaris",
            [
                new("id_jenis", "Jenis Inventaris", FieldKind.Lookup, Key: true, Required: true,
                    LookupSql: "SELECT id_jenis AS kode,nama_jenis AS nama FROM inventaris_jenis ORDER BY nama_jenis"),
                MenuKSpecs.Account("kd_rek", "Akun Rekening", required: true),
            ],
            """
            SELECT a.id_jenis,j.nama_jenis,a.kd_rek,r.nm_rek FROM akun_aset_inventaris a
            LEFT JOIN inventaris_jenis j ON j.id_jenis=a.id_jenis LEFT JOIN rekening r ON r.kd_rek=a.kd_rek
            WHERE CONCAT_WS(' ',a.id_jenis,j.nama_jenis,a.kd_rek,r.nm_rek) LIKE @p0 ORDER BY j.nama_jenis
            """,
            [("id_jenis", "ID Jenis"), ("nama_jenis", "Jenis"), ("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening")]),
        ["btnKategoriPiutangJasaPerusahaan"] = new("Kategori Piutang Jasa Perusahaan", "kategori_piutang_jasa_perusahaan",
            [Key("kode_kategori", "Kode", 5), new("nama_kategori", "Nama Kategori", MaxLen: 70, Required: true)],
            "SELECT kode_kategori,nama_kategori FROM kategori_piutang_jasa_perusahaan WHERE CONCAT_WS(' ',kode_kategori,nama_kategori) LIKE @p0 ORDER BY nama_kategori",
            [("kode_kategori", "Kode"), ("nama_kategori", "Kategori")]) { CanDelete = false },
        ["btnPeminjamPiutang"] = Party("Peminjam Piutang", "peminjampiutang", "kode_peminjam", "nama_peminjam", "Kode Peminjam"),
        ["btnPemberiHutangLain"] = Party("Pemberi Hutang Lain", "pemberi_hutang_lain", "kode_pemberi_hutang", "nama_pemberi_hutang", "Kode Pemberi"),
    };
}
