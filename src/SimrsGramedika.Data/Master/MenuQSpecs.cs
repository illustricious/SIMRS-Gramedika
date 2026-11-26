namespace SimrsGramedika.Data.Master;

/// <summary>
/// Menu Q (library). Catalog and member tables are editable. Loans and fine payments are read-only lists
/// because Khanza updates copy status and posts fines with them.
/// </summary>
public static class MenuQSpecs
{
    private static MasterField Key(string column, string label, int len) => new(column, label, MaxLen: len, Key: true);
    private static MasterField Req(string column, string label, int len) => new(column, label, MaxLen: len, Required: true);
    private static MasterField Look(string column, string label, string table, string key, string name, bool required = false) =>
        new(column, label, FieldKind.Lookup, Required: required,
            LookupSql: $"SELECT {key} AS kode,{name} AS nama FROM {table} ORDER BY {name},{key}") { NullWhenEmpty = !required };

    private static MasterSpec Reference(string title, string table, string code, string name, int codeLen, int nameLen) => new(title, table,
        [Key(code, "Kode", codeLen), Req(name, "Nama", nameLen)],
        $"SELECT {code},{name} FROM `{table}` WHERE CONCAT_WS(' ',{code},{name}) LIKE @p0 ORDER BY {name},{code}",
        [(code, "Kode"), (name, "Nama")])
    { CanDelete = false, Hint = "Kode yang dipakai koleksi tidak dihapus dari layar ini." };

    private static readonly string[] BookStatus = ["Ada", "Rusak", "Hilang", "Dipinjam", "-"];

    private static readonly (string, string)[] EbookColumns =
    [
        ("kode_ebook", "Kode"), ("judul_ebook", "Judul"), ("nama_pengarang", "Pengarang"), ("nama_penerbit", "Penerbit"),
        ("thn_terbit", "Tahun"), ("nama_kategori", "Kategori"), ("nama_jenis", "Jenis"), ("jml_halaman", "Halaman"), ("berkas", "Berkas"),
    ];

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnRuangPerpustakaan"] = Reference("Ruang Perpustakaan", "perpustakaan_ruang", "kd_ruang", "nm_ruang", 5, 40),
        ["btnKategoriPerpustakaan"] = Reference("Kategori Koleksi", "perpustakaan_kategori", "id_kategori", "nama_kategori", 5, 40),
        ["btnJenisPerpustakaan"] = Reference("Jenis Koleksi", "perpustakaan_jenis_buku", "id_jenis", "nama_jenis", 5, 40),
        ["btnPengarangPerpustakaan"] = Reference("Pengarang/Penulis", "perpustakaan_pengarang", "kode_pengarang", "nama_pengarang", 7, 40),
        ["btnPenerbitPerpustakaan"] = new("Penerbit Koleksi", "perpustakaan_penerbit",
            [
                Key("kode_penerbit", "Kode", 10), Req("nama_penerbit", "Nama Penerbit", 40), new("alamat_penerbit", "Alamat", MaxLen: 70),
                new("no_telp", "Telepon", MaxLen: 13), new("email", "Email", MaxLen: 25), new("website_penerbit", "Website", MaxLen: 30),
            ],
            """
            SELECT kode_penerbit,nama_penerbit,alamat_penerbit,no_telp,email,website_penerbit FROM perpustakaan_penerbit
            WHERE CONCAT_WS(' ',kode_penerbit,nama_penerbit,alamat_penerbit,no_telp,email,website_penerbit) LIKE @p0 ORDER BY nama_penerbit
            """,
            [("kode_penerbit", "Kode"), ("nama_penerbit", "Penerbit"), ("alamat_penerbit", "Alamat"), ("no_telp", "Telepon"),
             ("email", "Email"), ("website_penerbit", "Website")])
        { CanDelete = false },
        ["btnKoleksiPerpustakaan"] = new("Koleksi Perpustakaan", "perpustakaan_buku",
            [
                Key("kode_buku", "Kode Buku", 10), Req("judul_buku", "Judul", 200), new("jml_halaman", "Jumlah Halaman", MaxLen: 5),
                Look("kode_penerbit", "Penerbit", "perpustakaan_penerbit", "kode_penerbit", "nama_penerbit"),
                Look("kode_pengarang", "Pengarang", "perpustakaan_pengarang", "kode_pengarang", "nama_pengarang"),
                new("thn_terbit", "Tahun Terbit", FieldKind.Integer), new("isbn", "ISBN", MaxLen: 20),
                Look("id_kategori", "Kategori", "perpustakaan_kategori", "id_kategori", "nama_kategori"),
                Look("id_jenis", "Jenis", "perpustakaan_jenis_buku", "id_jenis", "nama_jenis"),
            ],
            """
            SELECT b.*,p.nama_penerbit,a.nama_pengarang,k.nama_kategori,j.nama_jenis,
                   (SELECT COUNT(*) FROM perpustakaan_inventaris i WHERE i.kode_buku=b.kode_buku) AS eksemplar
            FROM perpustakaan_buku b LEFT JOIN perpustakaan_penerbit p ON p.kode_penerbit=b.kode_penerbit
            LEFT JOIN perpustakaan_pengarang a ON a.kode_pengarang=b.kode_pengarang
            LEFT JOIN perpustakaan_kategori k ON k.id_kategori=b.id_kategori LEFT JOIN perpustakaan_jenis_buku j ON j.id_jenis=b.id_jenis
            WHERE CONCAT_WS(' ',b.kode_buku,b.judul_buku,b.isbn,p.nama_penerbit,a.nama_pengarang,k.nama_kategori,j.nama_jenis) LIKE @p0
            ORDER BY b.judul_buku,b.kode_buku
            """,
            [("kode_buku", "Kode"), ("judul_buku", "Judul"), ("nama_pengarang", "Pengarang"), ("nama_penerbit", "Penerbit"),
             ("thn_terbit", "Tahun"), ("isbn", "ISBN"), ("nama_kategori", "Kategori"), ("nama_jenis", "Jenis"),
             ("jml_halaman", "Halaman"), ("eksemplar", "Eksemplar")])
        { CanDelete = false, ComputedColumns = [], Hint = "Eksemplar dikelola di Inventaris Perpustakaan." },
        ["btnInventarisPerpustakaan"] = new("Inventaris Perpustakaan", "perpustakaan_inventaris",
            [
                Key("no_inventaris", "No. Inventaris", 20),
                Look("kode_buku", "Buku", "perpustakaan_buku", "kode_buku", "judul_buku", required: true),
                new("asal_buku", "Asal Buku", FieldKind.Choice, Choices: ["Beli", "Bantuan", "Hibah", "-"], Required: true),
                new("tgl_pengadaan", "Tanggal Pengadaan", FieldKind.Date), MenuKSpecs.Money("harga", "Harga"),
                new("status_buku", "Status", FieldKind.Choice, Choices: BookStatus, Required: true) { Default = "Ada" },
                Look("kd_ruang", "Ruang", "perpustakaan_ruang", "kd_ruang", "nm_ruang"),
                new("no_rak", "No. Rak", MaxLen: 3), new("no_box", "No. Box", MaxLen: 3),
            ],
            """
            SELECT i.*,b.judul_buku,r.nm_ruang FROM perpustakaan_inventaris i
            LEFT JOIN perpustakaan_buku b ON b.kode_buku=i.kode_buku LEFT JOIN perpustakaan_ruang r ON r.kd_ruang=i.kd_ruang
            WHERE CONCAT_WS(' ',i.no_inventaris,i.kode_buku,b.judul_buku,i.asal_buku,i.status_buku,r.nm_ruang,i.no_rak,i.no_box) LIKE @p0
            ORDER BY b.judul_buku,i.no_inventaris
            """,
            [("no_inventaris", "No. Inventaris"), ("kode_buku", "Kode Buku"), ("judul_buku", "Judul"), ("asal_buku", "Asal"),
             ("tgl_pengadaan", "Tgl. Pengadaan"), ("harga", "Harga"), ("status_buku", "Status"), ("nm_ruang", "Ruang"),
             ("no_rak", "Rak"), ("no_box", "Box")])
        {
            CanDelete = false,
            BeforeSave = (_, v, _) =>
            {
                if (MenuKSpecs.D(v, "harga") < 0) throw new ArgumentException("Harga harus nol atau lebih.");
                return Task.CompletedTask;
            },
            Hint = "Status Dipinjam diatur oleh peminjaman di Khanza. Jangan ubah manual untuk eksemplar yang sedang dipinjam.",
        },
        ["btnPengaturanPeminjamanPerpustakaan"] = new("Pengaturan Peminjaman", "perpustakaan_set_peminjaman",
            [
                new("max_pinjam", "Maksimal Pinjam (buku)", FieldKind.Integer, Required: true),
                new("lama_pinjam", "Lama Pinjam (hari)", FieldKind.Integer, Required: true),
                new("denda_perhari", "Denda Per Hari", FieldKind.Number, Required: true),
            ],
            "SELECT * FROM perpustakaan_set_peminjaman LIMIT 2", [])
        {
            Singleton = true,
            CanDelete = false,
            BeforeSave = (_, v, _) =>
            {
                if (MenuKSpecs.D(v, "max_pinjam") < 1 || MenuKSpecs.D(v, "lama_pinjam") < 1 || MenuKSpecs.D(v, "denda_perhari") < 0)
                    throw new ArgumentException("Maksimal dan lama pinjam minimal 1, denda tidak boleh negatif.");
                return Task.CompletedTask;
            },
        },
        ["btnDendaPerpustakaan"] = new("Denda Perpustakaan", "perpustakaan_denda",
            [Key("kode_denda", "Kode", 5), Req("jenis_denda", "Jenis Denda", 40), MenuKSpecs.Money("besar_denda", "Besar Denda")],
            "SELECT kode_denda,jenis_denda,besar_denda FROM perpustakaan_denda WHERE CONCAT_WS(' ',kode_denda,jenis_denda) LIKE @p0 ORDER BY jenis_denda",
            [("kode_denda", "Kode"), ("jenis_denda", "Jenis Denda"), ("besar_denda", "Besar Denda")])
        {
            CanDelete = false,
            BeforeSave = (_, v, _) =>
            {
                if (MenuKSpecs.D(v, "besar_denda") < 0) throw new ArgumentException("Besar denda harus nol atau lebih.");
                return Task.CompletedTask;
            },
        },
        ["btnAnggotaPerpustakaan"] = new("Anggota Perpustakaan", "perpustakaan_anggota",
            [
                Key("no_anggota", "No. Anggota", 10), Req("nama_anggota", "Nama", 40), new("tmp_lahir", "Tempat Lahir", MaxLen: 20),
                new("tgl_lahir", "Tanggal Lahir", FieldKind.Date), new("j_kel", "Jenis Kelamin", FieldKind.Choice, Choices: ["L", "P"], Required: true),
                new("alamat", "Alamat", MaxLen: 70), new("no_telp", "Telepon", MaxLen: 13), new("email", "Email", MaxLen: 25),
                new("tgl_gabung", "Tanggal Gabung", FieldKind.Date), new("masa_berlaku", "Masa Berlaku", FieldKind.Date),
                new("jenis_anggota", "Jenis Anggota", FieldKind.Choice, Choices: ["Pasien", "Pegawai", "Umum"], Required: true),
                new("nomer_id", "Nomor ID", MaxLen: 20),
            ],
            """
            SELECT a.*,(SELECT COUNT(*) FROM perpustakaan_peminjaman p WHERE p.no_anggota=a.no_anggota AND p.status_pinjam='Masih Dipinjam') AS dipinjam
            FROM perpustakaan_anggota a
            WHERE CONCAT_WS(' ',a.no_anggota,a.nama_anggota,a.alamat,a.no_telp,a.email,a.jenis_anggota,a.nomer_id) LIKE @p0
            ORDER BY a.nama_anggota,a.no_anggota
            """,
            [("no_anggota", "No. Anggota"), ("nama_anggota", "Nama"), ("jenis_anggota", "Jenis"), ("j_kel", "JK"), ("alamat", "Alamat"),
             ("no_telp", "Telepon"), ("email", "Email"), ("tgl_gabung", "Gabung"), ("masa_berlaku", "Berlaku s/d"),
             ("nomer_id", "Nomor ID"), ("dipinjam", "Sedang Dipinjam")])
        { CanDelete = false, Hint = "Anggota terkait riwayat peminjaman dan denda, jadi tidak dihapus dari layar ini." },
        ["btnEbookPerpustakaan"] = new("Data Koleksi Ebook", "perpustakaan_ebook",
            [
                Key("kode_ebook", "Kode Ebook", 10), Req("judul_ebook", "Judul", 200), new("jml_halaman", "Jumlah Halaman", MaxLen: 5),
                Look("kode_penerbit", "Penerbit", "perpustakaan_penerbit", "kode_penerbit", "nama_penerbit"),
                Look("kode_pengarang", "Pengarang", "perpustakaan_pengarang", "kode_pengarang", "nama_pengarang"),
                new("thn_terbit", "Tahun Terbit", FieldKind.Integer),
                Look("id_kategori", "Kategori", "perpustakaan_kategori", "id_kategori", "nama_kategori"),
                Look("id_jenis", "Jenis", "perpustakaan_jenis_buku", "id_jenis", "nama_jenis"),
                Req("berkas", "Berkas (lokasi/URL)", 1000),
            ],
            EbookSql(), EbookColumns)
        { CanDelete = false, Hint = "Isi lokasi berkas sebagaimana disimpan Khanza. Berkas tidak diunggah dari layar ini." },
        ["btnPenelitianPerpustakaan"] = new("Koleksi Penelitian", "riwayat_penelitian",
            [
                new("id", "Pegawai", FieldKind.Lookup, Key: true, Required: true, LookupSql: MasterLookups.PegawaiId),
                Key("judul_penelitian", "Judul Penelitian", 60), new("tahun", "Tahun", FieldKind.Integer, Key: true),
                Req("jenis_penelitian", "Jenis Penelitian", 30), Req("peranan", "Peranan", 30), Req("judul_jurnal", "Judul Jurnal", 60),
                MenuKSpecs.Money("biaya_penelitian", "Biaya Penelitian"), Req("asal_dana", "Asal Dana", 30), Req("berkas", "Berkas", 500),
            ],
            """
            SELECT r.*,p.nama,p.nik FROM riwayat_penelitian r LEFT JOIN pegawai p ON p.id=r.id
            WHERE CONCAT_WS(' ',p.nama,p.nik,r.jenis_penelitian,r.peranan,r.judul_penelitian,r.judul_jurnal,r.tahun,r.asal_dana) LIKE @p0
            ORDER BY r.tahun DESC,r.judul_penelitian
            """,
            [("nama", "Peneliti"), ("nik", "NIK"), ("tahun", "Tahun"), ("judul_penelitian", "Judul Penelitian"),
             ("jenis_penelitian", "Jenis"), ("peranan", "Peranan"), ("judul_jurnal", "Judul Jurnal"),
             ("biaya_penelitian", "Biaya"), ("asal_dana", "Asal Dana"), ("berkas", "Berkas")])
        { CanDelete = false, Hint = "Riwayat penelitian pegawai. Berkas berisi lokasi yang disimpan Khanza." },
    };

    private static string EbookSql() => """
        SELECT e.*,p.nama_penerbit,a.nama_pengarang,k.nama_kategori,j.nama_jenis FROM perpustakaan_ebook e
        LEFT JOIN perpustakaan_penerbit p ON p.kode_penerbit=e.kode_penerbit LEFT JOIN perpustakaan_pengarang a ON a.kode_pengarang=e.kode_pengarang
        LEFT JOIN perpustakaan_kategori k ON k.id_kategori=e.id_kategori LEFT JOIN perpustakaan_jenis_buku j ON j.id_jenis=e.id_jenis
        WHERE CONCAT_WS(' ',e.kode_ebook,e.judul_ebook,p.nama_penerbit,a.nama_pengarang,k.nama_kategori,j.nama_jenis) LIKE @p0
        ORDER BY e.judul_ebook,e.kode_ebook
        """;

    private static ReportSpec R(string title, string sql, (string, string)[] columns, ReportPeriod period, string? hint = null,
        params string[] totals) => MenuKSpecs.Report(title, sql, columns, period, hint, totals);

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnPeminjamanPerpustakaan"] = R("Peminjaman Koleksi Perpustakaan", """
            SELECT p.tgl_pinjam,p.tgl_kembali,p.no_anggota,a.nama_anggota,p.no_inventaris,b.judul_buku,p.status_pinjam,u.nama AS petugas,
                   CASE WHEN p.status_pinjam='Masih Dipinjam' AND p.tgl_kembali<CURDATE() THEN DATEDIFF(CURDATE(),p.tgl_kembali) ELSE 0 END AS hari_terlambat
            FROM perpustakaan_peminjaman p LEFT JOIN perpustakaan_anggota a ON a.no_anggota=p.no_anggota
            LEFT JOIN perpustakaan_inventaris i ON i.no_inventaris=p.no_inventaris LEFT JOIN perpustakaan_buku b ON b.kode_buku=i.kode_buku
            LEFT JOIN petugas u ON u.nip=p.nip
            WHERE p.tgl_pinjam BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',p.no_anggota,a.nama_anggota,p.no_inventaris,b.judul_buku,p.status_pinjam,u.nama) LIKE @p0
            ORDER BY p.tgl_pinjam DESC,p.no_anggota
            """,
            [("tgl_pinjam", "Tgl. Pinjam"), ("tgl_kembali", "Batas Kembali"), ("no_anggota", "No. Anggota"), ("nama_anggota", "Anggota"),
             ("no_inventaris", "No. Inventaris"), ("judul_buku", "Judul"), ("status_pinjam", "Status"), ("petugas", "Petugas"),
             ("hari_terlambat", "Hari Terlambat")], ReportPeriod.DateRange,
            "Peminjaman dan pengembalian dicatat di Khanza. Hari terlambat dihitung untuk buku yang masih dipinjam."),
        ["btnBayarDendaPerpustakaan"] = R("Bayar Denda Perpustakaan", """
            SELECT k.* FROM (
              SELECT d.tgl_denda,d.no_anggota,d.no_inventaris,'Denda' AS jenis,dn.jenis_denda AS rincian,d.besar_denda,d.keterangan_denda AS keterangan
              FROM perpustakaan_bayar_denda d LEFT JOIN perpustakaan_denda dn ON dn.kode_denda=d.kode_denda
              UNION ALL
              SELECT h.tgl_denda,h.no_anggota,h.no_inventaris,'Terlambat',CONCAT(h.keterlambatan,' hari'),h.besar_denda,''
              FROM perpustakaan_bayar_denda_harian h
            ) k LEFT JOIN perpustakaan_anggota a ON a.no_anggota=k.no_anggota
            WHERE k.tgl_denda BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',k.no_anggota,a.nama_anggota,k.no_inventaris,k.rincian,k.keterangan) LIKE @p0
            ORDER BY k.tgl_denda DESC,k.no_anggota
            """,
            [("tgl_denda", "Tanggal"), ("no_anggota", "No. Anggota"), ("nama_anggota", "Anggota"), ("no_inventaris", "No. Inventaris"),
             ("jenis", "Jenis"), ("rincian", "Rincian"), ("besar_denda", "Besar Denda"), ("keterangan", "Keterangan")],
            ReportPeriod.DateRange, "Denda manual dan denda keterlambatan harian. Pembayaran dicatat di Khanza.", "besar_denda"),
        ["btnCariEbook"] = R("Cari Koleksi Ebook", EbookSql(), EbookColumns, ReportPeriod.None,
            "Cari judul, pengarang, penerbit, kategori, atau jenis."),
        ["btnCariInventarisPerpustakaan"] = R("Cari Inventaris Perpustakaan", """
            SELECT i.no_inventaris,i.kode_buku,b.judul_buku,a.nama_pengarang,p.nama_penerbit,i.status_buku,r.nm_ruang,i.no_rak,i.no_box,
                   i.asal_buku,i.tgl_pengadaan
            FROM perpustakaan_inventaris i LEFT JOIN perpustakaan_buku b ON b.kode_buku=i.kode_buku
            LEFT JOIN perpustakaan_pengarang a ON a.kode_pengarang=b.kode_pengarang LEFT JOIN perpustakaan_penerbit p ON p.kode_penerbit=b.kode_penerbit
            LEFT JOIN perpustakaan_ruang r ON r.kd_ruang=i.kd_ruang
            WHERE CONCAT_WS(' ',i.no_inventaris,i.kode_buku,b.judul_buku,b.isbn,a.nama_pengarang,p.nama_penerbit,i.status_buku,r.nm_ruang) LIKE @p0
            ORDER BY b.judul_buku,i.no_inventaris
            """,
            [("no_inventaris", "No. Inventaris"), ("kode_buku", "Kode Buku"), ("judul_buku", "Judul"), ("nama_pengarang", "Pengarang"),
             ("nama_penerbit", "Penerbit"), ("status_buku", "Status"), ("nm_ruang", "Ruang"), ("no_rak", "Rak"), ("no_box", "Box"),
             ("asal_buku", "Asal"), ("tgl_pengadaan", "Tgl. Pengadaan")], ReportPeriod.None,
            "Cari judul, pengarang, atau status. Ketik 'Ada' untuk eksemplar yang tersedia."),
    };
}
