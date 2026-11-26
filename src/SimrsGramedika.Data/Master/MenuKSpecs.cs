namespace SimrsGramedika.Data.Master;

/// <summary>
/// Menu K (tarif, keuangan, akuntansi). Tarif and account masters are editable; money transactions are
/// read-only lists because Khanza posts a journal entry with each one.
/// </summary>
public static class MenuKSpecs
{
    internal const string Rekening = "SELECT kd_rek AS kode,nm_rek AS nama FROM rekening ORDER BY kd_rek";
    internal const string Penjab = "SELECT kd_pj AS kode,png_jawab AS nama FROM penjab ORDER BY png_jawab,kd_pj";
    internal const string Kategori = "SELECT kd_kategori AS kode,nm_kategori AS nama FROM kategori_perawatan ORDER BY nm_kategori";
    internal const string Poli = "SELECT kd_poli AS kode,nm_poli AS nama FROM poliklinik ORDER BY nm_poli";
    internal const string Bangsal = "SELECT kd_bangsal AS kode,nm_bangsal AS nama FROM bangsal ORDER BY nm_bangsal";
    internal static readonly string[] Kelas = ["-", "Kelas 1", "Kelas 2", "Kelas 3", "Kelas Utama", "Kelas VIP", "Kelas VVIP"];
    internal static readonly string[] KelasLab = ["-", "Rawat Jalan", .. Kelas[1..]];
    internal static readonly string[] Aktif = ["0", "1"];

    internal static MasterField Money(string column, string label) => new(column, label, FieldKind.Number);
    internal static MasterField Account(string column, string label, bool required = false) =>
        new(column, label, FieldKind.Lookup, Required: required, LookupSql: Rekening) { NullWhenEmpty = !required };
    internal static double D(Dictionary<string, object?> values, string column) =>
        values.GetValueOrDefault(column) is double d && double.IsFinite(d) ? d : 0d;
    internal static void NonNegative(Dictionary<string, object?> values, params string[] columns)
    {
        if (columns.Any(column => D(values, column) < 0))
            throw new ArgumentException("Semua komponen tarif harus nol atau lebih.");
    }

    private static MasterField[] TarifFields(string key, string name, bool poli, string[]? classes) =>
    [
        new(key, "Kode Tarif", MaxLen: 15, Key: true), new("nm_perawatan", "Nama Perawatan", MaxLen: 80, Required: true),
        new("kd_kategori", "Kategori", FieldKind.Lookup, Required: true, LookupSql: Kategori),
        Money("material", "Jasa Sarana"), Money("bhp", "BHP"),
        Money("tarif_tindakandr", "Jasa Dokter"), Money("tarif_tindakanpr", "Jasa Perawat"),
        Money("kso", "KSO"), Money("menejemen", "Manajemen"),
        new("kd_pj", "Cara Bayar", FieldKind.Lookup, Required: true, LookupSql: Penjab),
        poli ? new("kd_poli", "Poli", FieldKind.Lookup, Required: true, LookupSql: Poli)
             : new("kd_bangsal", "Bangsal", FieldKind.Lookup, Required: true, LookupSql: Bangsal),
        .. classes is null ? Array.Empty<MasterField>() : [new MasterField("kelas", "Kelas", FieldKind.Choice, Choices: classes, Required: true)],
        new("status", "Aktif (1/0)", FieldKind.Choice, Choices: Aktif, Required: true) { Default = "1" },
    ];

    private static MasterSpec Tarif(string title, string table, string key, bool poli, string[]? classes)
    {
        var place = poli ? "p.nm_poli" : "r.nm_bangsal";
        var join = poli ? "LEFT JOIN poliklinik p ON p.kd_poli=t.kd_poli" : "LEFT JOIN bangsal r ON r.kd_bangsal=t.kd_bangsal";
        var kelas = classes is null ? "" : ",t.kelas";
        return new(title, table, TarifFields(key, title, poli, classes),
            $"""
            SELECT t.*,k.nm_kategori,j.png_jawab,{place} AS lokasi FROM `{table}` t
            LEFT JOIN kategori_perawatan k ON k.kd_kategori=t.kd_kategori
            LEFT JOIN penjab j ON j.kd_pj=t.kd_pj {join}
            WHERE CONCAT_WS(' ',t.{key},t.nm_perawatan,k.nm_kategori,j.png_jawab,{place}{kelas}) LIKE @p0
            ORDER BY t.status DESC,t.nm_perawatan,t.{key}
            """,
            [(key, "Kode"), ("nm_perawatan", "Nama Perawatan"), ("nm_kategori", "Kategori"), ("png_jawab", "Cara Bayar"),
             ("lokasi", poli ? "Poli" : "Bangsal"), .. classes is null ? Array.Empty<(string, string)>() : [("kelas", "Kelas")],
             ("material", "Jasa Sarana"), ("bhp", "BHP"), ("tarif_tindakandr", "Jasa Dokter"),
             ("tarif_tindakanpr", "Jasa Perawat"), ("kso", "KSO"), ("menejemen", "Manajemen"),
             ("total_byrdr", "Total Dokter"), ("total_byrpr", "Total Perawat"), ("total_byrdrpr", "Total Dokter+Perawat"),
             ("status", "Aktif")])
        {
            CanDelete = false,
            ComputedColumns = ["total_byrdr", "total_byrpr", "total_byrdrpr"],
            BeforeSave = (_, v, _) =>
            {
                NonNegative(v, "material", "bhp", "tarif_tindakandr", "tarif_tindakanpr", "kso", "menejemen");
                var common = D(v, "material") + D(v, "bhp") + D(v, "kso") + D(v, "menejemen");
                v["total_byrdr"] = common + D(v, "tarif_tindakandr");
                v["total_byrpr"] = common + D(v, "tarif_tindakanpr");
                v["total_byrdrpr"] = common + D(v, "tarif_tindakandr") + D(v, "tarif_tindakanpr");
                return Task.CompletedTask;
            },
            Hint = "Total dihitung otomatis dari komponen tarif. Nonaktifkan tarif lewat kolom Aktif; tarif tidak dihapus karena dipakai transaksi.",
        };
    }

    private static MasterSpec Account(string title, string table, string key, string keyLabel, string name) => new(title, table,
        [new(key, keyLabel, MaxLen: 50, Key: true), Account("kd_rek", "Akun Rekening", required: true)],
        $"""
        SELECT a.{key},a.kd_rek,r.nm_rek FROM `{table}` a LEFT JOIN rekening r ON r.kd_rek=a.kd_rek
        WHERE CONCAT_WS(' ',a.{key},a.kd_rek,r.nm_rek) LIKE @p0 ORDER BY a.{key}
        """,
        [(key, keyLabel), ("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening")]);

    private static MasterSpec Category(string title, string table) => new(title, table,
        [
            new("kode_kategori", "Kode", MaxLen: 5, Key: true), new("nama_kategori", "Nama Kategori", MaxLen: 40, Required: true),
            Account("kd_rek", "Rekening Debet", required: true), Account("kd_rek2", "Rekening Kredit", required: true),
        ],
        $"""
        SELECT k.kode_kategori,k.nama_kategori,k.kd_rek,a.nm_rek,k.kd_rek2,b.nm_rek AS nm_rek2 FROM `{table}` k
        LEFT JOIN rekening a ON a.kd_rek=k.kd_rek LEFT JOIN rekening b ON b.kd_rek=k.kd_rek2
        WHERE CONCAT_WS(' ',k.kode_kategori,k.nama_kategori,k.kd_rek,a.nm_rek,k.kd_rek2,b.nm_rek) LIKE @p0
        ORDER BY k.nama_kategori,k.kode_kategori
        """,
        [("kode_kategori", "Kode"), ("nama_kategori", "Kategori"), ("kd_rek", "Rek. Debet"), ("nm_rek", "Nama Rek. Debet"),
         ("kd_rek2", "Rek. Kredit"), ("nm_rek2", "Nama Rek. Kredit")])
    { CanDelete = false, Hint = "Kategori yang sudah dipakai transaksi tidak dihapus." };

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnKamar"] = new("Kamar", "kamar",
            [
                new("kd_kamar", "Kode Kamar", MaxLen: 15, Key: true),
                new("kd_bangsal", "Bangsal", FieldKind.Lookup, Required: true, LookupSql: Bangsal),
                Money("trf_kamar", "Tarif Kamar"),
                new("kelas", "Kelas", FieldKind.Choice, Choices: Kelas[1..], Required: true),
                new("status", "Status", FieldKind.Choice, Choices: ["KOSONG", "DIBERSIHKAN", "DIBOOKING", "PERBAIKAN"], Required: true) { Default = "KOSONG" },
                new("statusdata", "Aktif (1/0)", FieldKind.Choice, Choices: Aktif, Required: true) { Default = "1" },
            ],
            """
            SELECT k.*,b.nm_bangsal FROM kamar k LEFT JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal
            WHERE CONCAT_WS(' ',k.kd_kamar,b.nm_bangsal,k.kelas,k.status) LIKE @p0 ORDER BY k.statusdata DESC,b.nm_bangsal,k.kd_kamar
            """,
            [("kd_kamar", "Kode Kamar"), ("nm_bangsal", "Bangsal"), ("kelas", "Kelas"), ("trf_kamar", "Tarif Kamar"),
             ("status", "Status"), ("statusdata", "Aktif")])
        {
            CanDelete = false,
            BeforeSave = (_, v, _) =>
            {
                if (D(v, "trf_kamar") < 0) throw new ArgumentException("Tarif kamar harus nol atau lebih.");
                return Task.CompletedTask;
            },
            Hint = "Status ISI diatur oleh Rawat Inap dan tidak dapat dipilih manual.",
        },
        ["btnTindakanRalan"] = Tarif("Tarif Ralan", "jns_perawatan", "kd_jenis_prw", poli: true, classes: null),
        ["btnTindakanRanap"] = Tarif("Tarif Ranap", "jns_perawatan_inap", "kd_jenis_prw", poli: false, classes: Kelas),
        ["btnTarifLab"] = new("Tarif Lab", "jns_perawatan_lab",
            [
                new("kd_jenis_prw", "Kode Tarif", MaxLen: 15, Key: true), new("nm_perawatan", "Nama Pemeriksaan", MaxLen: 80, Required: true),
                new("kategori", "Kategori", FieldKind.Choice, Choices: ["PK", "PA", "MB"], Required: true),
                Money("bagian_rs", "Bagian RS"), Money("bhp", "BHP"), Money("tarif_perujuk", "Jasa Perujuk"),
                Money("tarif_tindakan_dokter", "Jasa Dokter"), Money("tarif_tindakan_petugas", "Jasa Petugas"),
                Money("kso", "KSO"), Money("menejemen", "Manajemen"),
                new("kd_pj", "Cara Bayar", FieldKind.Lookup, Required: true, LookupSql: Penjab),
                new("kelas", "Kelas", FieldKind.Choice, Choices: KelasLab, Required: true),
                new("status", "Aktif (1/0)", FieldKind.Choice, Choices: Aktif, Required: true) { Default = "1" },
            ],
            """
            SELECT t.*,j.png_jawab FROM jns_perawatan_lab t LEFT JOIN penjab j ON j.kd_pj=t.kd_pj
            WHERE CONCAT_WS(' ',t.kd_jenis_prw,t.nm_perawatan,t.kategori,j.png_jawab,t.kelas) LIKE @p0
            ORDER BY t.status DESC,t.kategori,t.nm_perawatan,t.kd_jenis_prw
            """,
            [("kd_jenis_prw", "Kode"), ("nm_perawatan", "Pemeriksaan"), ("kategori", "Kategori"), ("png_jawab", "Cara Bayar"),
             ("kelas", "Kelas"), ("bagian_rs", "Bagian RS"), ("bhp", "BHP"), ("tarif_perujuk", "Jasa Perujuk"),
             ("tarif_tindakan_dokter", "Jasa Dokter"), ("tarif_tindakan_petugas", "Jasa Petugas"), ("kso", "KSO"),
             ("menejemen", "Manajemen"), ("total_byr", "Total"), ("status", "Aktif")])
        {
            CanDelete = false,
            ComputedColumns = ["total_byr"],
            BeforeSave = (_, v, _) =>
            {
                string[] parts = ["bagian_rs", "bhp", "tarif_perujuk", "tarif_tindakan_dokter", "tarif_tindakan_petugas", "kso", "menejemen"];
                NonNegative(v, parts);
                v["total_byr"] = parts.Sum(column => D(v, column));
                return Task.CompletedTask;
            },
            Hint = "Total dihitung otomatis. Template detail pemeriksaan tetap dikelola di Menu Laboratorium.",
        },
        ["btnCaraBayar"] = new("Asuransi/Askes/Jenis Bayar", "penjab",
            [
                new("kd_pj", "Kode", MaxLen: 3, Key: true), new("png_jawab", "Penanggung Jawab", MaxLen: 30, Required: true),
                new("nama_perusahaan", "Nama Perusahaan", MaxLen: 60), new("alamat_asuransi", "Alamat", MaxLen: 130),
                new("no_telp", "Telepon", MaxLen: 40), new("attn", "Attn", MaxLen: 60),
                new("status", "Aktif (1/0)", FieldKind.Choice, Choices: Aktif, Required: true) { Default = "1" },
            ],
            """
            SELECT kd_pj,png_jawab,nama_perusahaan,alamat_asuransi,no_telp,attn,status FROM penjab
            WHERE CONCAT_WS(' ',kd_pj,png_jawab,nama_perusahaan,alamat_asuransi,no_telp,attn) LIKE @p0 ORDER BY status DESC,png_jawab,kd_pj
            """,
            [("kd_pj", "Kode"), ("png_jawab", "Penanggung Jawab"), ("nama_perusahaan", "Perusahaan"),
             ("alamat_asuransi", "Alamat"), ("no_telp", "Telepon"), ("attn", "Attn"), ("status", "Aktif")])
        { CanDelete = false, Hint = "Cara bayar dinonaktifkan, tidak dihapus, karena terkait kunjungan dan tarif." },
        ["btnRekening"] = new("Akun Rekening", "rekening",
            [
                new("kd_rek", "Kode Rekening", MaxLen: 15, Key: true), new("nm_rek", "Nama Rekening", MaxLen: 100, Required: true),
                new("tipe", "Tipe", FieldKind.Choice, Choices: ["N", "M", "R"], Required: true),
                new("balance", "Saldo Normal", FieldKind.Choice, Choices: ["D", "K"], Required: true),
                new("level", "Level (0/1)", FieldKind.Choice, Choices: Aktif, Required: true),
            ],
            """
            SELECT kd_rek,nm_rek,tipe,balance,level FROM rekening
            WHERE CONCAT_WS(' ',kd_rek,nm_rek,tipe,balance) LIKE @p0 ORDER BY kd_rek
            """,
            [("kd_rek", "Kode"), ("nm_rek", "Nama Rekening"), ("tipe", "Tipe"), ("balance", "Saldo Normal"), ("level", "Level")])
        {
            CanDelete = false,
            Hint = "Tipe mengikuti kode Khanza (N, M, R). Saldo normal D=Debet, K=Kredit. Rekening yang dipakai jurnal tidak dihapus.",
        },
        ["btnRekeningTahun"] = new("Rekening Tahun", "rekeningtahun",
            [
                new("thn", "Tahun", FieldKind.Integer, Key: true),
                new("kd_rek", "Rekening", FieldKind.Lookup, Key: true, Required: true, LookupSql: Rekening),
                Money("saldo_awal", "Saldo Awal"),
            ],
            """
            SELECT t.thn,t.kd_rek,r.nm_rek,t.saldo_awal FROM rekeningtahun t LEFT JOIN rekening r ON r.kd_rek=t.kd_rek
            WHERE CONCAT_WS(' ',t.thn,t.kd_rek,r.nm_rek) LIKE @p0 ORDER BY t.thn DESC,t.kd_rek
            """,
            [("thn", "Tahun"), ("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening"), ("saldo_awal", "Saldo Awal")])
        { CanDelete = false, Hint = "Saldo awal tahun dipakai buku besar dan neraca." },
        ["btnakun_bayar"] = Account("Akun Bayar", "akun_bayar", "nama_bayar", "Nama Bayar", "akun bayar"),
        ["btnAkunBayarHutang"] = Account("Akun Bayar Hutang", "akun_bayar_hutang", "nama_bayar", "Nama Bayar", "akun bayar hutang"),
        ["btnAkunPiutang"] = new("Akun Piutang", "akun_piutang",
            [
                new("nama_bayar", "Nama Akun Piutang", MaxLen: 50, Key: true), Account("kd_rek", "Akun Rekening", required: true),
                new("kd_pj", "Cara Bayar", FieldKind.Lookup, LookupSql: Penjab) { NullWhenEmpty = true },
            ],
            """
            SELECT a.nama_bayar,a.kd_rek,r.nm_rek,a.kd_pj,j.png_jawab FROM akun_piutang a
            LEFT JOIN rekening r ON r.kd_rek=a.kd_rek LEFT JOIN penjab j ON j.kd_pj=a.kd_pj
            WHERE CONCAT_WS(' ',a.nama_bayar,a.kd_rek,r.nm_rek,j.png_jawab) LIKE @p0 ORDER BY a.nama_bayar
            """,
            [("nama_bayar", "Nama Akun"), ("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening"), ("png_jawab", "Cara Bayar")]),
        ["btnKategoriPengeluaranHarian"] = Category("Kategori Pengeluaran Harian", "kategori_pengeluaran_harian"),
        ["btnKategoriPemasukanLian"] = Category("Kategori Pemasukan Lain-lain", "kategori_pemasukan_lain"),
    };

    internal static ReportSpec Report(string title, string sql, (string, string)[] columns, ReportPeriod period,
        string? hint = null, string[]? totals = null) =>
        new(title, sql, columns, period, hint ?? "Daftar hanya-baca. Transaksi dan jurnalnya dicatat di Khanza.")
        { TotalColumns = totals ?? [] };

    private static readonly (string, string)[] PengajuanColumns =
    [
        ("no_pengajuan", "No. Pengajuan"), ("tanggal", "Tanggal"), ("pemohon", "Pemohon"), ("urgensi", "Urgensi"),
        ("tujuan_pengajuan", "Tujuan"), ("jumlah", "Jumlah"), ("harga", "Harga"), ("total", "Total"),
        ("penanggung", "Penanggung Jawab"), ("status", "Status"), ("keterangan", "Keterangan"),
    ];

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnPengeluaran"] = Report("Pengeluaran Harian", """
            SELECT h.no_keluar,h.tanggal,h.kode_kategori,k.nama_kategori,h.biaya,h.nip,p.nama,h.keterangan
            FROM pengeluaran_harian h LEFT JOIN kategori_pengeluaran_harian k ON k.kode_kategori=h.kode_kategori
            LEFT JOIN petugas p ON p.nip=h.nip
            WHERE h.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',h.no_keluar,k.nama_kategori,h.nip,p.nama,h.keterangan) LIKE @p0
            ORDER BY h.tanggal DESC,h.no_keluar
            """,
            [("no_keluar", "No. Keluar"), ("tanggal", "Tanggal"), ("nama_kategori", "Kategori"), ("biaya", "Biaya"),
             ("nip", "NIP"), ("nama", "Petugas"), ("keterangan", "Keterangan")], ReportPeriod.DateRange, totals: ["biaya"]),
        ["btnPemasukanLain"] = Report("Pemasukan Lain-Lain", """
            SELECT h.no_masuk,h.tanggal,k.nama_kategori,h.besar,h.nip,p.nama,h.keterangan,h.keperluan
            FROM pemasukan_lain h LEFT JOIN kategori_pemasukan_lain k ON k.kode_kategori=h.kode_kategori
            LEFT JOIN petugas p ON p.nip=h.nip
            WHERE h.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',h.no_masuk,k.nama_kategori,h.nip,p.nama,h.keterangan,h.keperluan) LIKE @p0
            ORDER BY h.tanggal DESC,h.no_masuk
            """,
            [("no_masuk", "No. Masuk"), ("tanggal", "Tanggal"), ("nama_kategori", "Kategori"), ("besar", "Jumlah"),
             ("nip", "NIP"), ("nama", "Petugas"), ("keterangan", "Keterangan"), ("keperluan", "Keperluan")],
            ReportPeriod.DateRange, totals: ["besar"]),
        ["btnPiutangBelumLunas"] = Report("Piutang Belum Lunas", """
            SELECT p.no_rawat,p.tgl_piutang,p.no_rkm_medis,s.nm_pasien,j.png_jawab,p.totalpiutang,p.uangmuka,p.sisapiutang,p.tgltempo
            FROM piutang_pasien p LEFT JOIN pasien s ON s.no_rkm_medis=p.no_rkm_medis
            LEFT JOIN reg_periksa r ON r.no_rawat=p.no_rawat LEFT JOIN penjab j ON j.kd_pj=r.kd_pj
            WHERE p.status='Belum Lunas' AND p.tgl_piutang BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',p.no_rawat,p.no_rkm_medis,s.nm_pasien,j.png_jawab) LIKE @p0
            ORDER BY p.tgltempo,p.no_rawat
            """,
            [("no_rawat", "No. Rawat"), ("tgl_piutang", "Tgl. Piutang"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("png_jawab", "Cara Bayar"), ("totalpiutang", "Total Piutang"), ("uangmuka", "Uang Muka"),
             ("sisapiutang", "Sisa Piutang"), ("tgltempo", "Jatuh Tempo")], ReportPeriod.DateRange,
            totals: ["totalpiutang", "uangmuka", "sisapiutang"]),
        ["btnPiutangPerCaraBayar"] = Report("Piutang Per Cara Bayar", """
            SELECT COALESCE(j.png_jawab,'-') AS png_jawab,COUNT(*) AS jumlah,SUM(p.totalpiutang) AS totalpiutang,
                   SUM(p.uangmuka) AS uangmuka,SUM(p.sisapiutang) AS sisapiutang
            FROM piutang_pasien p LEFT JOIN reg_periksa r ON r.no_rawat=p.no_rawat LEFT JOIN penjab j ON j.kd_pj=r.kd_pj
            WHERE p.status='Belum Lunas' AND p.tgl_piutang BETWEEN @p1 AND @p2 AND COALESCE(j.png_jawab,'-') LIKE @p0
            GROUP BY j.kd_pj,j.png_jawab ORDER BY j.png_jawab
            """,
            [("png_jawab", "Cara Bayar"), ("jumlah", "Jumlah Piutang"), ("totalpiutang", "Total Piutang"),
             ("uangmuka", "Uang Muka"), ("sisapiutang", "Sisa Piutang")], ReportPeriod.DateRange,
            "Hanya piutang pasien yang belum lunas pada periode tanggal piutang.", ["jumlah", "totalpiutang", "uangmuka", "sisapiutang"]),
        ["btnBayarPiutang"] = Report("Bayar Piutang", """
            SELECT b.tgl_bayar,b.no_rawat,b.no_rkm_medis,s.nm_pasien,b.besar_cicilan,b.diskon_piutang,b.tidak_terbayar,
                   b.kd_rek,b.kd_rek_kontra,b.catatan
            FROM bayar_piutang b LEFT JOIN pasien s ON s.no_rkm_medis=b.no_rkm_medis
            WHERE b.tgl_bayar BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',b.no_rawat,b.no_rkm_medis,s.nm_pasien,b.catatan) LIKE @p0
            ORDER BY b.tgl_bayar DESC,b.no_rawat
            """,
            [("tgl_bayar", "Tgl. Bayar"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("besar_cicilan", "Cicilan"), ("diskon_piutang", "Diskon"), ("tidak_terbayar", "Tidak Terbayar"),
             ("kd_rek", "Rek. Kas"), ("kd_rek_kontra", "Rek. Piutang"), ("catatan", "Catatan")], ReportPeriod.DateRange,
            totals: ["besar_cicilan", "diskon_piutang", "tidak_terbayar"]),
        ["btnPenagihanPiutangPasien"] = Report("Penagihan Piutang Pasien", """
            SELECT h.no_tagihan,h.tanggal,h.tanggaltempo,h.tempo,j.png_jawab,p.nama AS petugas,m.nama AS penyetuju,h.status,h.catatan,
                   COUNT(d.no_rawat) AS jumlah,COALESCE(SUM(d.sisapiutang),0) AS sisapiutang
            FROM penagihan_piutang h LEFT JOIN penjab j ON j.kd_pj=h.kd_pj LEFT JOIN pegawai p ON p.nik=h.nip
            LEFT JOIN pegawai m ON m.nik=h.nip_menyetujui LEFT JOIN detail_penagihan_piutang d ON d.no_tagihan=h.no_tagihan
            WHERE h.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',h.no_tagihan,j.png_jawab,p.nama,m.nama,h.status,h.catatan) LIKE @p0
            GROUP BY h.no_tagihan ORDER BY h.tanggal DESC,h.no_tagihan
            """,
            [("no_tagihan", "No. Tagihan"), ("tanggal", "Tanggal"), ("tanggaltempo", "Jatuh Tempo"), ("tempo", "Tempo (hari)"),
             ("png_jawab", "Cara Bayar"), ("petugas", "Petugas"), ("penyetuju", "Menyetujui"), ("status", "Status"),
             ("jumlah", "Jumlah Piutang"), ("sisapiutang", "Sisa Piutang"), ("catatan", "Catatan")], ReportPeriod.DateRange,
            totals: ["jumlah", "sisapiutang"]),
        ["btnPiutangLainLain"] = Report("Piutang Peminjaman Uang", """
            SELECT h.nota_piutang,h.tgl_piutang,h.tgltempo,m.nama_peminjam,h.nominal,h.sisapiutang,h.status,h.nama_bayar,h.keterangan
            FROM piutang_lainlain h LEFT JOIN peminjampiutang m ON m.kode_peminjam=h.kode_peminjam
            WHERE h.tgl_piutang BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',h.nota_piutang,m.nama_peminjam,h.status,h.keterangan) LIKE @p0
            ORDER BY h.tgl_piutang DESC,h.nota_piutang
            """,
            [("nota_piutang", "Nota"), ("tgl_piutang", "Tanggal"), ("tgltempo", "Jatuh Tempo"), ("nama_peminjam", "Peminjam"),
             ("nominal", "Nominal"), ("sisapiutang", "Sisa"), ("status", "Status"), ("nama_bayar", "Akun Bayar"), ("keterangan", "Keterangan")],
            ReportPeriod.DateRange, totals: ["nominal", "sisapiutang"]),
        ["btnPiutangPeminjamanUangBelumLunas"] = Report("Piutang Peminjaman Uang Belum Lunas", """
            SELECT h.nota_piutang,h.tgl_piutang,h.tgltempo,m.nama_peminjam,h.nominal,h.sisapiutang,h.keterangan
            FROM piutang_lainlain h LEFT JOIN peminjampiutang m ON m.kode_peminjam=h.kode_peminjam
            WHERE h.status='Belum Lunas' AND CONCAT_WS(' ',h.nota_piutang,m.nama_peminjam,h.keterangan) LIKE @p0
            ORDER BY h.tgltempo,h.nota_piutang
            """,
            [("nota_piutang", "Nota"), ("tgl_piutang", "Tanggal"), ("tgltempo", "Jatuh Tempo"), ("nama_peminjam", "Peminjam"),
             ("nominal", "Nominal"), ("sisapiutang", "Sisa"), ("keterangan", "Keterangan")], ReportPeriod.None,
            totals: ["nominal", "sisapiutang"]),
        ["btnBayarPiutangLainLain"] = Report("Bayar Piutang Peminjaman Uang", """
            SELECT b.tgl_bayar,b.nota_piutang,m.nama_peminjam,b.besar_cicilan,b.nama_bayar,b.kd_rek,b.keterangan
            FROM bayar_piutang_lainlain b LEFT JOIN peminjampiutang m ON m.kode_peminjam=b.kode_peminjam
            WHERE b.tgl_bayar BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',b.nota_piutang,m.nama_peminjam,b.nama_bayar,b.keterangan) LIKE @p0
            ORDER BY b.tgl_bayar DESC,b.nota_piutang
            """,
            [("tgl_bayar", "Tgl. Bayar"), ("nota_piutang", "Nota"), ("nama_peminjam", "Peminjam"), ("besar_cicilan", "Cicilan"),
             ("nama_bayar", "Akun Bayar"), ("kd_rek", "Rekening"), ("keterangan", "Keterangan")], ReportPeriod.DateRange,
            totals: ["besar_cicilan"]),
        ["btnHutangObat"] = Report("Hutang Obat & BHP", """
            SELECT h.no_faktur,h.tgl_faktur,h.tgl_tempo,s.nama_suplier,h.tagihan,
                   COALESCE((SELECT SUM(b.besar_bayar) FROM bayar_pemesanan b WHERE b.no_faktur=h.no_faktur),0) AS dibayar,h.status
            FROM pemesanan h LEFT JOIN datasuplier s ON s.kode_suplier=h.kode_suplier
            WHERE h.status IN ('Belum Dibayar','Belum Lunas','Titip Faktur') AND h.tgl_faktur BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_faktur,h.no_order,s.nama_suplier,h.status) LIKE @p0
            ORDER BY h.tgl_tempo,h.no_faktur
            """,
            [("no_faktur", "No. Faktur"), ("tgl_faktur", "Tgl. Faktur"), ("tgl_tempo", "Jatuh Tempo"), ("nama_suplier", "Suplier"),
             ("tagihan", "Tagihan"), ("dibayar", "Sudah Dibayar"), ("status", "Status")], ReportPeriod.DateRange,
            totals: ["tagihan", "dibayar"]),
        ["btnTagihanHutangObat"] = Report("Titip Faktur/Tagihan Obat & BHP", """
            SELECT h.no_tagihan,h.tanggal,h.status,h.nip,p.nama,h.keterangan
            FROM titip_faktur h LEFT JOIN pegawai p ON p.nik=h.nip
            WHERE h.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',h.no_tagihan,h.status,h.nip,p.nama,h.keterangan) LIKE @p0
            ORDER BY h.tanggal DESC,h.no_tagihan
            """,
            [("no_tagihan", "No. Tagihan"), ("tanggal", "Tanggal"), ("status", "Status"), ("nip", "NIP"),
             ("nama", "Petugas"), ("keterangan", "Keterangan")], ReportPeriod.DateRange),
        ["btnbayar_pemesanan"] = Report("Bayar Pesan Obat/BHP", """
            SELECT b.tgl_bayar,b.no_faktur,s.nama_suplier,b.besar_bayar,b.nama_bayar,b.no_bukti,b.nip,b.keterangan
            FROM bayar_pemesanan b LEFT JOIN pemesanan h ON h.no_faktur=b.no_faktur LEFT JOIN datasuplier s ON s.kode_suplier=h.kode_suplier
            WHERE b.tgl_bayar BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',b.no_faktur,s.nama_suplier,b.nama_bayar,b.no_bukti,b.keterangan) LIKE @p0
            ORDER BY b.tgl_bayar DESC,b.no_faktur
            """,
            [("tgl_bayar", "Tgl. Bayar"), ("no_faktur", "No. Faktur"), ("nama_suplier", "Suplier"), ("besar_bayar", "Dibayar"),
             ("nama_bayar", "Akun Bayar"), ("no_bukti", "No. Bukti"), ("nip", "NIP"), ("keterangan", "Keterangan")],
            ReportPeriod.DateRange, totals: ["besar_bayar"]),
        ["btnPengajuanBiayaKuangan"] = Report("Pengajuan Biaya", PengajuanSql(""), PengajuanColumns, ReportPeriod.DateRange,
            totals: ["total"]),
        ["btnPersetujuanPengajuanBiaya"] = Report("Persetujuan Pengajuan Biaya", PengajuanSql("AND h.status IN ('Disetujui','Ditolak')"),
            PengajuanColumns, ReportPeriod.DateRange, totals: ["total"]),
        ["btnValidasiPersetujuanPengajuanBiaya"] = Report("Validasi Persetujuan Pengajuan Biaya",
            PengajuanSql("AND h.status IN ('Disetujui','Divalidasi')"), PengajuanColumns, ReportPeriod.DateRange, totals: ["total"]),
        ["btnRekapPengajuanBiaya"] = Report("Rekap Pengajuan Biaya", """
            SELECT h.status,COUNT(*) AS jumlah,SUM(h.total) AS total,COALESCE(SUM(d.total),0) AS disetujui
            FROM pengajuan_biaya h LEFT JOIN pengajuan_biaya_disetujui d ON d.no_pengajuan=h.no_pengajuan
            WHERE h.tanggal BETWEEN @p1 AND @p2 AND h.status LIKE @p0 GROUP BY h.status ORDER BY h.status
            """,
            [("status", "Status"), ("jumlah", "Jumlah Pengajuan"), ("total", "Total Diajukan"), ("disetujui", "Total Disetujui")],
            ReportPeriod.DateRange, totals: ["jumlah", "total", "disetujui"]),
        ["btnJurnalHarian"] = Report("Jurnal Harian", """
            SELECT j.no_jurnal,j.tgl_jurnal,j.jam_jurnal,j.no_bukti,j.jenis,d.kd_rek,r.nm_rek,d.debet,d.kredit,j.keterangan
            FROM jurnal j LEFT JOIN detailjurnal d ON d.no_jurnal=j.no_jurnal LEFT JOIN rekening r ON r.kd_rek=d.kd_rek
            WHERE j.tgl_jurnal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',j.no_jurnal,j.no_bukti,d.kd_rek,r.nm_rek,j.keterangan) LIKE @p0
            ORDER BY j.tgl_jurnal,j.jam_jurnal,j.no_jurnal,d.debet DESC,d.kd_rek
            """,
            [("no_jurnal", "No. Jurnal"), ("tgl_jurnal", "Tanggal"), ("jam_jurnal", "Jam"), ("no_bukti", "No. Bukti"),
             ("jenis", "Jenis (U/P)"), ("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening"), ("debet", "Debet"),
             ("kredit", "Kredit"), ("keterangan", "Keterangan")], ReportPeriod.DateRange, totals: ["debet", "kredit"]),
        ["btnBubes"] = Report("Buku Besar", """
            SELECT d.kd_rek,r.nm_rek,j.tgl_jurnal,j.jam_jurnal,j.no_jurnal,j.no_bukti,j.keterangan,d.debet,d.kredit
            FROM detailjurnal d JOIN jurnal j ON j.no_jurnal=d.no_jurnal LEFT JOIN rekening r ON r.kd_rek=d.kd_rek
            WHERE j.tgl_jurnal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',d.kd_rek,r.nm_rek,j.no_jurnal,j.no_bukti,j.keterangan) LIKE @p0
            ORDER BY d.kd_rek,j.tgl_jurnal,j.jam_jurnal,j.no_jurnal
            """,
            [("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening"), ("tgl_jurnal", "Tanggal"), ("jam_jurnal", "Jam"),
             ("no_jurnal", "No. Jurnal"), ("no_bukti", "No. Bukti"), ("keterangan", "Keterangan"), ("debet", "Debet"), ("kredit", "Kredit")],
            ReportPeriod.DateRange, "Mutasi jurnal per rekening. Saldo awal tahun ada di Rekening Tahun.", ["debet", "kredit"]),
    };

    private static string PengajuanSql(string statusFilter) => $"""
        SELECT h.no_pengajuan,h.tanggal,p.nama AS pemohon,h.urgensi,h.tujuan_pengajuan,h.jumlah,h.harga,h.total,
               q.nama AS penanggung,h.status,h.keterangan
        FROM pengajuan_biaya h LEFT JOIN pegawai p ON p.nik=h.nik LEFT JOIN pegawai q ON q.nik=h.nik_pj
        WHERE h.tanggal BETWEEN @p1 AND @p2 {statusFilter}
          AND CONCAT_WS(' ',h.no_pengajuan,p.nama,h.tujuan_pengajuan,q.nama,h.status,h.keterangan) LIKE @p0
        ORDER BY h.tanggal DESC,h.no_pengajuan
        """;
}
