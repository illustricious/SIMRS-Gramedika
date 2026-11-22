namespace SimrsKhanza.Data.Master;

/// <summary>
/// Menu N (blood transfusion unit, UTD) and Menu R (PCRA/ICRA). Reference tables are editable;
/// stock movements and donation records are read-only lists because Khanza posts stock and journals with them.
/// </summary>
public static class MenuNRSpecs
{
    private static readonly string[] Blood = ["A", "AB", "B", "O"];

    private static MasterSpec Reference(string title, string table, string code, string name, int codeLen, int nameLen) => new(title, table,
        [new(code, "Kode", MaxLen: codeLen, Key: true), new(name, "Nama", MaxLen: nameLen, Required: true)],
        $"SELECT {code},{name} FROM `{table}` WHERE CONCAT_WS(' ',{code},{name}) LIKE @p0 ORDER BY {name},{code}",
        [(code, "Kode"), (name, "Nama")])
    { CanDelete = false, Hint = "Kode yang sudah dipakai pengkajian tidak dihapus dari layar ini." };

    private static MasterField Area(string column, string label, string table, string key, string name) =>
        new(column, label, FieldKind.Lookup, Required: true, LookupSql: $"SELECT {key} AS kode,{name} AS nama FROM {table} ORDER BY {name}");

    private static MasterField Money(string column, string label) => MenuKSpecs.Money(column, label);

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnPCRAICRAJenisAktivitasProyek"] = Reference("Jenis Aktivitas Proyek PCRA", "pcra_icra_jenis_aktivitas_proyek", "kode_aktivitas", "nama_aktivitas", 5, 60),
        ["btnPCRAICRALokasiKelompokRisiko"] = Reference("Lokasi & Kelompok Risiko Area PCRA", "pcra_icra_lokasi_kelompok_risiko_area", "kode_area", "nama_area", 4, 40),
        ["btnPCRAICRAKelasRisikoPencegahan"] = Reference("Kelas Risiko/Kelas Pencegahan PCRA", "pcra_icra_kelas_risiko_pencegahan", "kode_kelas", "nama_kelas", 3, 30),
        ["btnPCRAICRATindakanPengendalian"] = Reference("Tindakan Pengendalian PCRA", "pcra_icra_tindakan_pengendalian", "kode_pengendalian", "nama_pengendalian", 5, 60),
        ["btnPCRAICRAIdentifikasiRisikoInfeksi"] = Reference("Identifikasi Risiko Infeksi PCRA", "pcra_icra_identifkasi_risiko_infeksi", "kode_risiko", "nama_risiko", 5, 60),
        ["btnPCRAICRAIdentifikasiRisikoKeselamatan"] = Reference("Identifikasi Risiko Keselamatan PCRA", "pcra_icra_identifkasi_risiko_keselamatan", "kode_risiko", "nama_risiko", 5, 60),
        ["btnPCRAICRAIdentifikasiRisikoKebakaran"] = Reference("Identifikasi Risiko Kebakaran PCRA", "pcra_icra_identifkasi_risiko_kebakaran", "kode_risiko", "nama_risiko", 5, 60),
        ["btnPCRAICRAIdentifikasiRisikoUtilitas"] = Reference("Identifikasi Risiko Utilitas PCRA", "pcra_icra_identifkasi_risiko_utilitas", "kode_risiko", "nama_risiko", 5, 60),
        ["btnPCRAICRAPersyaratanHarusDipenuhi"] = Reference("Persyaratan Harus Dipenuhi PCRA", "pcra_icra_persyaratan_harus_dipenuhi", "kode_persyaratan", "nama_persyaratan", 5, 300),

        ["btnUTDKomponenDarah"] = new("Komponen Darah", "utd_komponen_darah",
            [
                new("kode", "Kode", MaxLen: 5, Key: true), new("nama", "Nama Komponen", MaxLen: 70, Required: true),
                new("lama", "Lama Simpan (hari)", FieldKind.Integer),
                Money("jasa_sarana", "Jasa Sarana"), Money("paket_bhp", "Paket BHP"), Money("kso", "KSO"), Money("manajemen", "Manajemen"),
                Money("pembatalan", "Biaya Pembatalan"),
            ],
            """
            SELECT kode,nama,lama,jasa_sarana,paket_bhp,kso,manajemen,total,pembatalan FROM utd_komponen_darah
            WHERE CONCAT_WS(' ',kode,nama) LIKE @p0 ORDER BY nama,kode
            """,
            [("kode", "Kode"), ("nama", "Komponen"), ("lama", "Lama (hari)"), ("jasa_sarana", "Jasa Sarana"), ("paket_bhp", "Paket BHP"),
             ("kso", "KSO"), ("manajemen", "Manajemen"), ("total", "Total"), ("pembatalan", "Pembatalan")])
        {
            CanDelete = false,
            ComputedColumns = ["total"],
            BeforeSave = (_, v, _) =>
            {
                string[] parts = ["jasa_sarana", "paket_bhp", "kso", "manajemen", "pembatalan"];
                MenuKSpecs.NonNegative(v, parts);
                if (MenuKSpecs.D(v, "lama") < 0) throw new ArgumentException("Lama simpan harus nol atau lebih.");
                v["total"] = MenuKSpecs.D(v, "jasa_sarana") + MenuKSpecs.D(v, "paket_bhp") + MenuKSpecs.D(v, "kso") + MenuKSpecs.D(v, "manajemen");
                return Task.CompletedTask;
            },
            Hint = "Total dihitung otomatis dari jasa sarana, paket BHP, KSO, dan manajemen.",
        },
        ["btnPendonorDarah"] = new("Pendonor Darah", "utd_pendonor",
            [
                new("no_pendonor", "No. Pendonor", MaxLen: 15, Key: true), new("nama", "Nama", MaxLen: 40, Required: true),
                new("no_ktp", "No. KTP", MaxLen: 20, Required: true),
                new("jk", "Jenis Kelamin", FieldKind.Choice, Choices: ["L", "P"], Required: true),
                new("tmp_lahir", "Tempat Lahir", MaxLen: 15, Required: true), new("tgl_lahir", "Tanggal Lahir", FieldKind.Date),
                new("alamat", "Alamat", MaxLen: 100, Required: true),
                Area("kd_kel", "Kelurahan", "kelurahan", "kd_kel", "nm_kel"), Area("kd_kec", "Kecamatan", "kecamatan", "kd_kec", "nm_kec"),
                Area("kd_kab", "Kabupaten", "kabupaten", "kd_kab", "nm_kab"), Area("kd_prop", "Provinsi", "propinsi", "kd_prop", "nm_prop"),
                new("golongan_darah", "Golongan Darah", FieldKind.Choice, Choices: Blood, Required: true),
                new("resus", "Resus", FieldKind.Choice, Choices: ["(-)", "(+)"], Required: true),
                new("no_telp", "Telepon", MaxLen: 40, Required: true),
            ],
            """
            SELECT p.*,k.nm_kel,c.nm_kec,b.nm_kab,r.nm_prop FROM utd_pendonor p
            LEFT JOIN kelurahan k ON k.kd_kel=p.kd_kel LEFT JOIN kecamatan c ON c.kd_kec=p.kd_kec
            LEFT JOIN kabupaten b ON b.kd_kab=p.kd_kab LEFT JOIN propinsi r ON r.kd_prop=p.kd_prop
            WHERE CONCAT_WS(' ',p.no_pendonor,p.nama,p.no_ktp,p.alamat,p.no_telp,p.golongan_darah,k.nm_kel,c.nm_kec,b.nm_kab) LIKE @p0
            ORDER BY p.nama,p.no_pendonor
            """,
            [("no_pendonor", "No. Pendonor"), ("nama", "Nama"), ("no_ktp", "No. KTP"), ("jk", "JK"), ("tgl_lahir", "Tgl. Lahir"),
             ("golongan_darah", "Gol. Darah"), ("resus", "Resus"), ("alamat", "Alamat"), ("nm_kel", "Kelurahan"),
             ("nm_kec", "Kecamatan"), ("nm_kab", "Kabupaten"), ("no_telp", "Telepon")])
        { CanDelete = false, Hint = "Pendonor terkait riwayat donor dan tidak dihapus dari layar ini. Donor dicatat di Khanza." },
    };

    private static ReportSpec R(string title, string sql, (string, string)[] columns, string[]? totals = null, string? hint = null,
        ReportPeriod period = ReportPeriod.DateRange) => MenuKSpecs.Report(title, sql, columns, period, hint, totals);

    private static (string, string)[] MovementColumns(string name) =>
    [
        ("tanggal", "Tanggal"), ("kode_brng", "Kode Barang"), ("nama_brng", name), ("jml", "Jumlah"), ("harga", "Harga"),
        ("total", "Total"), ("pelaksana", "Petugas/Bangsal"), ("keterangan", "Keterangan"),
    ];

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnPengambilanUTD2"] = R("Pengambilan BHP Medis", """
            SELECT x.tanggal,x.kode_brng,b.nama_brng,x.jml,x.hargabeli AS harga,x.total,g.nm_bangsal AS pelaksana,
                   CONCAT_WS(' / ',NULLIF(x.keterangan,''),NULLIF(x.no_batch,''),NULLIF(x.no_faktur,'')) AS keterangan
            FROM utd_pengambilan_medis x LEFT JOIN databarang b ON b.kode_brng=x.kode_brng LEFT JOIN bangsal g ON g.kd_bangsal=x.kd_bangsal_dr
            WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,g.nm_bangsal,x.keterangan,x.no_batch,x.no_faktur) LIKE @p0
            ORDER BY x.tanggal DESC,x.kode_brng
            """, MovementColumns("Nama Barang"), ["total"]),
        ["btnUTDMedisRusak"] = R("BHP Medis Rusak", """
            SELECT x.tanggal,x.kode_brng,b.nama_brng,x.jml,x.hargabeli AS harga,x.total,p.nama AS pelaksana,x.keterangan
            FROM utd_medis_rusak x LEFT JOIN databarang b ON b.kode_brng=x.kode_brng LEFT JOIN petugas p ON p.nip=x.nip
            WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,p.nama,x.keterangan) LIKE @p0
            ORDER BY x.tanggal DESC,x.kode_brng
            """, MovementColumns("Nama Barang"), ["total"]),
        ["btnPengambilanPenunjangUTD2"] = R("Pengambilan BHP Non Medis", """
            SELECT x.tanggal,x.kode_brng,b.nama_brng,x.jml,x.harga,x.total,p.nama AS pelaksana,x.keterangan
            FROM utd_pengambilan_penunjang x LEFT JOIN ipsrsbarang b ON b.kode_brng=x.kode_brng LEFT JOIN petugas p ON p.nip=x.nip
            WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,p.nama,x.keterangan) LIKE @p0
            ORDER BY x.tanggal DESC,x.kode_brng
            """, MovementColumns("Nama Barang"), ["total"]),
        ["btnUTDPenunjangRusak"] = R("BHP Non Medis Rusak", """
            SELECT x.tanggal,x.kode_brng,b.nama_brng,x.jml,x.harga,x.total,p.nama AS pelaksana,x.keterangan
            FROM utd_penunjang_rusak x LEFT JOIN ipsrsbarang b ON b.kode_brng=x.kode_brng LEFT JOIN petugas p ON p.nip=x.nip
            WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,p.nama,x.keterangan) LIKE @p0
            ORDER BY x.tanggal DESC,x.kode_brng
            """, MovementColumns("Nama Barang"), ["total"]),
        ["btnUTDDonorDarah"] = R("Donor Darah", """
            SELECT d.no_donor,d.tanggal,d.dinas,p.nama AS pendonor,p.golongan_darah,p.resus,d.jenis_donor,d.jenis_bag,d.no_bag,d.tensi,
                   d.tempat_aftap,a.nama AS petugas_aftap,d.hbsag,d.hcv,d.hiv,d.spilis,d.malaria,u.nama AS petugas_saring,d.status
            FROM utd_donor d LEFT JOIN utd_pendonor p ON p.no_pendonor=d.no_pendonor
            LEFT JOIN petugas a ON a.nip=d.petugas_aftap LEFT JOIN petugas u ON u.nip=d.petugas_u_saring
            WHERE d.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',d.no_donor,d.no_pendonor,p.nama,d.status,d.jenis_donor,a.nama,u.nama) LIKE @p0
            ORDER BY d.tanggal DESC,d.no_donor
            """,
            [("no_donor", "No. Donor"), ("tanggal", "Tanggal"), ("dinas", "Dinas"), ("pendonor", "Pendonor"), ("golongan_darah", "Gol. Darah"),
             ("resus", "Resus"), ("jenis_donor", "Jenis Donor"), ("jenis_bag", "Jenis Bag"), ("no_bag", "No. Bag"), ("tensi", "Tensi"),
             ("tempat_aftap", "Tempat Aftap"), ("petugas_aftap", "Petugas Aftap"), ("hbsag", "HBsAg"), ("hcv", "HCV"), ("hiv", "HIV"),
             ("spilis", "Sifilis"), ("malaria", "Malaria"), ("petugas_saring", "Petugas Uji Saring"), ("status", "Status")],
            hint: "Donor dicatat di Khanza. Status Cekal berarti kantong dimusnahkan."),
        ["btnUTDCekalDarah"] = R("Pencekalan Darah", """
            SELECT c.no_donor,c.tanggal,c.dinas,p.nama AS pendonor,p.golongan_darah,p.resus,u.nama AS petugas,c.keterangan
            FROM utd_cekal_darah c LEFT JOIN utd_donor d ON d.no_donor=c.no_donor LEFT JOIN utd_pendonor p ON p.no_pendonor=d.no_pendonor
            LEFT JOIN petugas u ON u.nip=c.petugas_pemusnahan
            WHERE c.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',c.no_donor,p.nama,u.nama,c.keterangan) LIKE @p0
            ORDER BY c.tanggal DESC,c.no_donor
            """,
            [("no_donor", "No. Donor"), ("tanggal", "Tanggal"), ("dinas", "Dinas"), ("pendonor", "Pendonor"), ("golongan_darah", "Gol. Darah"),
             ("resus", "Resus"), ("petugas", "Petugas Pemusnahan"), ("keterangan", "Keterangan")]),
        ["btnUTDPemisahanDarah"] = R("Pemisahan Darah", """
            SELECT m.no_donor,m.tanggal,m.dinas,u.nama AS petugas,x.no_kantong,k.nama AS komponen,x.tanggal_kadaluarsa
            FROM utd_pemisahan_komponen m LEFT JOIN petugas u ON u.nip=m.nip
            LEFT JOIN utd_detail_pemisahan_komponen x ON x.no_donor=m.no_donor LEFT JOIN utd_komponen_darah k ON k.kode=x.kode_komponen
            WHERE m.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',m.no_donor,u.nama,x.no_kantong,k.nama) LIKE @p0
            ORDER BY m.tanggal DESC,m.no_donor,x.no_kantong
            """,
            [("no_donor", "No. Donor"), ("tanggal", "Tanggal"), ("dinas", "Dinas"), ("petugas", "Petugas"), ("no_kantong", "No. Kantong"),
             ("komponen", "Komponen"), ("tanggal_kadaluarsa", "Kadaluarsa")], hint: "Satu baris per kantong hasil pemisahan."),
        ["btnUTDStokDarah"] = R("Stok Darah", """
            SELECT s.no_kantong,k.nama AS komponen,s.golongan_darah,s.resus,s.asal_darah,s.tanggal_aftap,s.tanggal_kadaluarsa,s.status,
                   CASE WHEN s.status='Ada' AND s.tanggal_kadaluarsa<CURDATE() THEN 'Ya' ELSE '' END AS kadaluarsa
            FROM utd_stok_darah s LEFT JOIN utd_komponen_darah k ON k.kode=s.kode_komponen
            WHERE CONCAT_WS(' ',s.no_kantong,k.nama,s.golongan_darah,s.resus,s.asal_darah,s.status) LIKE @p0
            ORDER BY s.status,s.tanggal_kadaluarsa,s.no_kantong
            """,
            [("no_kantong", "No. Kantong"), ("komponen", "Komponen"), ("golongan_darah", "Gol. Darah"), ("resus", "Resus"),
             ("asal_darah", "Asal Darah"), ("tanggal_aftap", "Tgl. Aftap"), ("tanggal_kadaluarsa", "Kadaluarsa"), ("status", "Status"),
             ("kadaluarsa", "Lewat Kadaluarsa")], hint: "Cari 'Ada' untuk stok tersedia atau 'Diambil' untuk yang sudah diserahkan.", period: ReportPeriod.None),
        ["btnUTDPenyerahanDarah"] = R("Penyerahan Darah", """
            SELECT h.no_penyerahan,h.tanggal,h.dinas,h.pengambil_darah,h.alamat_pengambil_darah,u.nama AS petugas,h.status,
                   COUNT(d.no_kantong) AS jumlah_kantong,COALESCE(SUM(d.total),0) AS total,h.besarppn,h.keterangan
            FROM utd_penyerahan_darah h LEFT JOIN petugas u ON u.nip=h.nip_cross LEFT JOIN utd_penyerahan_darah_detail d ON d.no_penyerahan=h.no_penyerahan
            WHERE h.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_penyerahan,h.pengambil_darah,h.alamat_pengambil_darah,u.nama,h.status,h.keterangan) LIKE @p0
            GROUP BY h.no_penyerahan ORDER BY h.tanggal DESC,h.no_penyerahan
            """,
            [("no_penyerahan", "No. Penyerahan"), ("tanggal", "Tanggal"), ("dinas", "Dinas"), ("pengambil_darah", "Pengambil"),
             ("alamat_pengambil_darah", "Alamat Pengambil"), ("petugas", "Petugas"), ("status", "Status Bayar"),
             ("jumlah_kantong", "Jumlah Kantong"), ("total", "Total"), ("besarppn", "PPN"), ("keterangan", "Keterangan")],
            ["jumlah_kantong", "total", "besarppn"], "Penyerahan dan pembayarannya dicatat di Khanza."),
        ["btnPCRAICRAPengkajianRisikoPraKonstruksi"] = R("Pengkajian Risiko Pra Konstruksi/PCRA", """
            SELECT h.no_proyek,h.tanggal_pengkajian,h.nama_proyek,h.lokasi_proyek,h.mulai_proyek,h.perkiraan_selesai,h.penangung_jawab_proyek,
                   h.pelaksana_proyek,a.nama_aktivitas,k.nama_kelas,h.dibutuhkan_icra,h.hal_yang_perlu_dimonitor
            FROM pcra_icra_pengkajian_risiko_prakonstruksi h
            LEFT JOIN pcra_icra_jenis_aktivitas_proyek a ON a.kode_aktivitas=h.kode_aktivitas
            LEFT JOIN pcra_icra_kelas_risiko_pencegahan k ON k.kode_kelas=h.kode_kelas_risiko
            WHERE h.tanggal_pengkajian BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_proyek,h.nama_proyek,h.lokasi_proyek,h.penangung_jawab_proyek,h.pelaksana_proyek,a.nama_aktivitas,k.nama_kelas) LIKE @p0
            ORDER BY h.tanggal_pengkajian DESC,h.no_proyek
            """,
            [("no_proyek", "No. Proyek"), ("tanggal_pengkajian", "Tgl. Pengkajian"), ("nama_proyek", "Proyek"), ("lokasi_proyek", "Lokasi"),
             ("mulai_proyek", "Mulai"), ("perkiraan_selesai", "Perkiraan Selesai"), ("penangung_jawab_proyek", "Penanggung Jawab"),
             ("pelaksana_proyek", "Pelaksana"), ("nama_aktivitas", "Aktivitas"), ("nama_kelas", "Kelas Risiko"),
             ("dibutuhkan_icra", "Perlu ICRA"), ("hal_yang_perlu_dimonitor", "Hal Dimonitor")],
            hint: "Ringkasan pengkajian. Identifikasi risiko, persyaratan, dan pengendalian per proyek diisi di Khanza."),
    };
}
