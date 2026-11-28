using SimrsGramedika.Data.Master;
using K = SimrsGramedika.Data.Master.FieldKind;

namespace SimrsGramedika.Data.Farmasi;

/// <summary>
/// The remaining menu [D] tiles: Telaah Resep (checklist per resep), the inpatient patient-stock and
/// return lists (input happens from the Rawat Inap patient menu, as in Khanza), and list screens for
/// purchasing documents. See <see cref="MenuDSpecs"/> for masters and reports.
/// </summary>
public static class MenuDLists
{
    private static readonly string[] YaTidak = ["Ya", "Tidak"];

    private static readonly (string Col, string Label)[] TelaahResep =
    [
        ("identifikasi_pasien", "Identifikasi Pasien"), ("tepat_obat", "Tepat Obat"), ("tepat_dosis", "Tepat Dosis"),
        ("tepat_cara_pemberian", "Tepat Cara Pemberian"), ("tepat_waktu_pemberian", "Tepat Waktu Pemberian"),
        ("ada_tidak_duplikasi_obat", "Ada/Tidak Duplikasi Obat"), ("interaksi_obat", "Interaksi Obat"), ("kontra_indikasi_obat", "Kontra Indikasi Obat"),
    ];

    private static readonly (string Col, string Label)[] TelaahObat =
    [
        ("obat_tepat_pasien", "Obat: Tepat Pasien"), ("obat_tepat_obat", "Obat: Tepat Obat"), ("obat_tepat_dosis", "Obat: Tepat Dosis"),
        ("obat_tepat_cara_pemberian", "Obat: Tepat Cara Pemberian"), ("obat_tepat_waktu_pemberian", "Obat: Tepat Waktu Pemberian"),
    ];

    private static readonly MasterSpec Telaah = new(
        "Pengkajian Resep & Obat", "telaah_farmasi",
        [
            new("no_resep", "No.Resep", Key: true),
            .. TelaahResep.SelectMany(t => new MasterField[]
            {
                new($"resep_{t.Col}", $"Resep: {t.Label}", K.Choice, Choices: YaTidak),
                new($"resep_ket_{t.Col}", "   Keterangan", MaxLen: 30),
            }),
            .. TelaahObat.Select(t => new MasterField(t.Col, t.Label, K.Choice, Choices: YaTidak)),
            new("nip", "Petugas Farmasi", K.Lookup, LookupSql: "SELECT nip AS kode, nama FROM petugas WHERE status='1' ORDER BY nama", Required: true),
        ],
        """
        SELECT t.*, ro.no_resep, ro.tgl_peresepan, ro.jam_peresepan, ro.no_rawat, p.no_rkm_medis, p.nm_pasien, d.nm_dokter,
        g.status_lanjut, IF(t.no_resep IS NULL,'Belum','Sudah') AS telaah, pt.nama AS petugas
        FROM resep_obat ro JOIN reg_periksa g ON g.no_rawat=ro.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis
        LEFT JOIN dokter d ON d.kd_dokter=ro.kd_dokter LEFT JOIN telaah_farmasi t ON t.no_resep=ro.no_resep LEFT JOIN petugas pt ON pt.nip=t.nip
        WHERE ro.tgl_peresepan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',ro.no_resep,ro.no_rawat,p.no_rkm_medis,p.nm_pasien,d.nm_dokter) LIKE @p0
        ORDER BY ro.tgl_peresepan DESC, ro.jam_peresepan DESC LIMIT 3000
        """,
        [("no_resep", "No.Resep"), ("tgl_peresepan", "Tgl.Resep"), ("jam_peresepan", "Jam"), ("no_rawat", "No.Rawat"), ("no_rkm_medis", "No.RM"),
         ("nm_pasien", "Pasien"), ("nm_dokter", "Dokter"), ("status_lanjut", "Rawat"), ("telaah", "Telaah"), ("petugas", "Petugas")])
    {
        Period = PeriodFilter.DateRange, Upsert = true, CanInsert = false,
        Hint = "Pilih resep di daftar (kolom Telaah = Belum/Sudah), isi hasil pengkajian, lalu Simpan.",
    };

    private const string StokPasienKeys = "s.tanggal=@p0 AND s.jam=@p1 AND s.no_rawat=@p2 AND s.kode_brng=@p3 AND s.no_batch=@p4 AND s.no_faktur=@p5";

    private static readonly MasterSpec StokPasien = new(
        "Stok Obat Pasien", "stok_obat_pasien",
        [
            new("tanggal", "Tanggal", K.Date, Key: true), new("jam", "Jam", K.Time, Key: true), new("no_rawat", "No.Rawat", Key: true),
            new("kode_brng", "Kode Barang", Key: true), new("no_batch", "No.Batch", Key: true), new("no_faktur", "No.Faktur", Key: true),
            new("jumlah", "Jumlah", K.Number), new("aturan_pakai", "Aturan Pakai", MaxLen: 150),
        ],
        $"""
        SELECT s.tanggal, s.jam, s.no_rawat, s.kode_brng, s.no_batch, s.no_faktur, s.jumlah, s.aturan_pakai, p.no_rkm_medis, p.nm_pasien,
        b.nama_brng, st.satuan, bg.nm_bangsal,
        CONCAT_WS(',',{string.Join(",", Enumerable.Range(0, 24).Select(h => $"IF(s.jam{h:00}='true','{h:00}',NULL)"))}) AS jadwal
        FROM stok_obat_pasien s JOIN reg_periksa g ON g.no_rawat=s.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis
        JOIN databarang b ON b.kode_brng=s.kode_brng LEFT JOIN kodesatuan st ON st.kode_sat=b.kode_sat LEFT JOIN bangsal bg ON bg.kd_bangsal=s.kd_bangsal
        WHERE s.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',s.no_rawat,p.no_rkm_medis,p.nm_pasien,b.nama_brng,bg.nm_bangsal) LIKE @p0
        ORDER BY s.tanggal DESC, s.jam DESC LIMIT 5000
        """,
        [("tanggal", "Tanggal"), ("jam", "Jam"), ("no_rawat", "No.Rawat"), ("no_rkm_medis", "No.RM"), ("nm_pasien", "Pasien"), ("kode_brng", "Kode"),
         ("nama_brng", "Nama Barang"), ("jumlah", "Jumlah"), ("satuan", "Satuan"), ("aturan_pakai", "Aturan Pakai"), ("jadwal", "Jam Pemberian"),
         ("nm_bangsal", "Depo"), ("no_batch", "No.Batch")])
    {
        Period = PeriodFilter.DateRange, CanInsert = false, CanEdit = false, CanDelete = false,
        Hint = "Obat yang disiapkan per pasien rawat inap. Stok dibuat lewat Permintaan Stok Obat Pasien (atau menu pasien Rawat Inap). " +
               "\"Hapus & Kembalikan Stok\" mengembalikan jumlahnya ke depo seperti Khanza lama.",
        Actions =
        [
            new("🗑 Hapus & Kembalikan Stok",
                $"""
                UPDATE data_batch d JOIN stok_obat_pasien s ON d.no_batch=s.no_batch AND d.no_faktur=s.no_faktur AND d.kode_brng=s.kode_brng
                SET d.sisa=d.sisa+s.jumlah WHERE {StokPasienKeys} AND s.no_batch<>'';
                INSERT INTO riwayat_barang_medis (kode_brng,stok_awal,masuk,keluar,stok_akhir,posisi,tanggal,jam,petugas,kd_bangsal,status,no_batch,no_faktur,keterangan)
                SELECT s.kode_brng, IFNULL(g.stok,0), s.jumlah, 0, IFNULL(g.stok,0)+s.jumlah, 'Stok Pasien Ranap', CURRENT_DATE(), CURRENT_TIME(), @p6,
                s.kd_bangsal, 'Hapus', s.no_batch, s.no_faktur, CONCAT('Hapus stok obat pasien ',s.no_rawat)
                FROM stok_obat_pasien s LEFT JOIN gudangbarang g ON g.kode_brng=s.kode_brng AND g.kd_bangsal=s.kd_bangsal AND g.no_batch=s.no_batch AND g.no_faktur=s.no_faktur
                WHERE {StokPasienKeys};
                INSERT INTO gudangbarang (kode_brng,kd_bangsal,stok,no_batch,no_faktur)
                SELECT s.kode_brng, s.kd_bangsal, s.jumlah, s.no_batch, s.no_faktur FROM stok_obat_pasien s WHERE {StokPasienKeys}
                ON DUPLICATE KEY UPDATE stok=gudangbarang.stok+VALUES(stok);
                DELETE s FROM stok_obat_pasien s WHERE {StokPasienKeys};
                """,
                "Hapus stok obat pasien ini dan kembalikan jumlahnya ke stok depo?"),
        ],
    };

    private static readonly MasterSpec ReturPasien = new(
        "Retur Obat Ranap", "returpasien",
        [
            new("tanggal", "Tanggal", K.Date, Key: true), new("no_rawat", "No.Rawat", Key: true), new("kode_brng", "Kode Barang", Key: true),
            new("no_batch", "No.Batch", Key: true), new("no_faktur", "No.Faktur", Key: true), new("jml", "Jumlah Retur", K.Number),
        ],
        """
        SELECT r.*, p.no_rkm_medis, p.nm_pasien, b.nama_brng, s.satuan FROM returpasien r JOIN reg_periksa g ON g.no_rawat=r.no_rawat
        JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis JOIN databarang b ON b.kode_brng=r.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
        WHERE r.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',r.no_rawat,p.no_rkm_medis,p.nm_pasien,b.nama_brng) LIKE @p0
        ORDER BY r.tanggal DESC LIMIT 5000
        """,
        [("tanggal", "Tanggal"), ("no_rawat", "No.Rawat"), ("no_rkm_medis", "No.RM"), ("nm_pasien", "Pasien"), ("kode_brng", "Kode"),
         ("nama_brng", "Nama Barang"), ("jml", "Jumlah"), ("satuan", "Satuan"), ("no_batch", "No.Batch"), ("no_faktur", "No.Faktur")])
    {
        Period = PeriodFilter.DateRange, CanInsert = false, CanEdit = false, CanDelete = false,
        Hint = "Obat yang dikembalikan pasien rawat inap (diinput dari menu pasien Rawat Inap, seperti Khanza lama).",
    };

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnTelaahResep"] = Telaah,
        ["btnStokObatPasien"] = StokPasien,
        ["btnReturPasien"] = ReturPasien,
    };

    // ------------------------------------------------------------------ list reports

    private const string Brg = "JOIN databarang b ON b.kode_brng=x.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat";

    /// <summary>Header + detail document list (no input screen yet: these tables are unused in the current database).</summary>
    private static ReportSpec Daftar(string title, string header, string detail, string key, string dateCol, string qty, string total,
        string partyJoin, string party, string? hint = null) => new(
        title,
        $"""
        SELECT h.{key} AS nomor, h.{dateCol} AS tanggal, {party} AS pihak, x.kode_brng, b.nama_brng, s.satuan, x.{qty} AS jumlah, x.{total} AS total
        FROM {header} h JOIN {detail} x ON x.{key}=h.{key} {Brg} {partyJoin}
        WHERE h.{dateCol} BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',h.{key},{party},b.nama_brng) LIKE @p0
        ORDER BY h.{dateCol} DESC, h.{key} LIMIT 5000
        """,
        [("nomor", "No"), ("tanggal", "Tanggal"), ("pihak", "Suplier/Pihak"), ("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"),
         ("jumlah", "Jumlah"), ("total", "Total")],
        ReportPeriod.DateRange,
        hint ?? "Daftar transaksi dari tabel Khanza. Input transaksi ini belum dibuat di aplikasi baru karena belum pernah dipakai di database ini.");

    private const string SuplierJoin = "LEFT JOIN datasuplier sp ON sp.kode_suplier=h.kode_suplier";

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnHibahObatBHP"] = Daftar("Hibah Obat & BHP", "hibah_obat_bhp", "detailhibah_obat_bhp", "no_hibah", "tgl_hibah", "jumlah", "subtotaldiakui",
            "LEFT JOIN pemberihibah ph ON ph.kode_pemberi=h.kode_pemberi", "ph.nama_pemberi"),
        ["btnPengajuanBarangMedis"] = Daftar("Pengajuan Obat & BHP", "pengajuan_barang_medis", "detail_pengajuan_barang_medis", "no_pengajuan", "tanggal",
            "jumlah", "total", "LEFT JOIN pegawai pg ON pg.nik=h.nip", "pg.nama"),
        ["btnSuratPemesananMedis"] = Daftar("Surat Pemesanan Obat & BHP", "surat_pemesanan_medis", "detail_surat_pemesanan_medis", "no_pemesanan", "tanggal",
            "jumlah", "total", SuplierJoin, "sp.nama_suplier"),
        ["btnPiutang"] = Daftar("Piutang Obat & BHP", "piutang", "detailpiutang", "nota_piutang", "tgl_piutang", "jumlah", "total", "", "h.nm_pasien"),
        ["btnReturJual"] = Daftar("Retur Dari Pembeli", "returjual", "detreturjual", "no_retur_jual", "tgl_retur", "jml_retur", "subtotal",
            "LEFT JOIN pasien ps ON ps.no_rkm_medis=h.no_rkm_medis", "ps.nm_pasien",
            "Daftar retur penjualan bebas (input retur belum dibuat di aplikasi baru)."),
        ["btnReturPiutang"] = Daftar("Retur Piutang Pembeli", "returpiutang", "detreturpiutang", "no_retur_piutang", "tgl_retur", "jml_retur", "subtotal",
            "LEFT JOIN pasien ps ON ps.no_rkm_medis=h.no_rkm_medis", "ps.nm_pasien"),
        ["btnVerifikasiPenerimaanFarmasi"] = Daftar("Verifikasi Penerimaan Obat/Alkes/BHP", "pemesanan", "detailpesan", "no_faktur", "tgl_pesan", "jumlah2", "total",
            SuplierJoin, "sp.nama_suplier", "Rincian barang per faktur penerimaan untuk dicocokkan dengan barang fisik."),
        ["btnPengambilanUTD"] = new("Pengambilan BHP UTD",
            $"""
            SELECT x.tanggal, x.kode_brng, b.nama_brng, s.satuan, x.jml, x.hargabeli, x.total, bg.nm_bangsal, x.no_batch, x.keterangan
            FROM utd_pengambilan_medis x {Brg} LEFT JOIN bangsal bg ON bg.kd_bangsal=x.kd_bangsal_dr
            WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,bg.nm_bangsal,x.keterangan) LIKE @p0 ORDER BY x.tanggal DESC
            """,
            [("tanggal", "Tanggal"), ("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"), ("jml", "Jumlah"), ("hargabeli", "Harga"),
             ("total", "Total"), ("nm_bangsal", "Dari Lokasi"), ("no_batch", "No.Batch"), ("keterangan", "Keterangan")],
            ReportPeriod.DateRange, "BHP medis yang diambil unit transfusi darah (UTD)."),
        ["btnPenggunaanBHPOK"] = new("Penggunaan BHP OK/VK",
            """
            SELECT x.tanggal, x.no_rawat, p.no_rkm_medis, p.nm_pasien, x.kd_obat, o.nm_obat, x.jumlah, x.hargasatuan, x.jumlah*x.hargasatuan AS total
            FROM beri_obat_operasi x JOIN reg_periksa g ON g.no_rawat=x.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis
            LEFT JOIN obatbhp_ok o ON o.kd_obat=x.kd_obat
            WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,p.nm_pasien,o.nm_obat) LIKE @p0 ORDER BY x.tanggal DESC
            """,
            [("tanggal", "Tanggal"), ("no_rawat", "No.Rawat"), ("no_rkm_medis", "No.RM"), ("nm_pasien", "Pasien"), ("kd_obat", "Kode"), ("nm_obat", "BHP"),
             ("jumlah", "Jumlah"), ("hargasatuan", "Harga"), ("total", "Total")],
            ReportPeriod.DateRange, "BHP yang dipakai di tindakan operasi/VK."),
        ["btnResepLuar"] = new("Resep Luar",
            """
            SELECT r.no_resep, r.tgl_peresepan, r.jam_peresepan, r.no_rawat, p.no_rkm_medis, p.nm_pasien, d.nm_dokter
            FROM resep_luar r JOIN reg_periksa g ON g.no_rawat=r.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis LEFT JOIN dokter d ON d.kd_dokter=r.kd_dokter
            WHERE r.tgl_peresepan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',r.no_resep,r.no_rawat,p.nm_pasien,d.nm_dokter) LIKE @p0 ORDER BY r.tgl_peresepan DESC
            """,
            [("no_resep", "No.Resep"), ("tgl_peresepan", "Tanggal"), ("jam_peresepan", "Jam"), ("no_rawat", "No.Rawat"), ("no_rkm_medis", "No.RM"),
             ("nm_pasien", "Pasien"), ("nm_dokter", "Dokter")],
            ReportPeriod.DateRange, "Resep yang ditebus di luar rumah sakit."),
        ["btnPermintaanResepPulang"] = new("Permintaan Resep Pulang",
            $"""
            SELECT h.no_permintaan, h.tgl_permintaan, h.jam, h.no_rawat, p.nm_pasien, d.nm_dokter, h.status, x.kode_brng, b.nama_brng, s.satuan, x.jml, x.dosis
            FROM permintaan_resep_pulang h JOIN detail_permintaan_resep_pulang x ON x.no_permintaan=h.no_permintaan {Brg}
            JOIN reg_periksa g ON g.no_rawat=h.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis LEFT JOIN dokter d ON d.kd_dokter=h.kd_dokter
            WHERE h.tgl_permintaan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',h.no_permintaan,h.no_rawat,p.nm_pasien,b.nama_brng,h.status) LIKE @p0
            ORDER BY h.tgl_permintaan DESC, h.no_permintaan
            """,
            [("no_permintaan", "No.Permintaan"), ("tgl_permintaan", "Tanggal"), ("jam", "Jam"), ("no_rawat", "No.Rawat"), ("nm_pasien", "Pasien"),
             ("nm_dokter", "Dokter"), ("status", "Status"), ("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"), ("jml", "Jumlah"),
             ("dosis", "Dosis")],
            ReportPeriod.DateRange, "Permintaan resep pulang dari dokter rawat inap."),
        ["btnStokAkhirFarmasiPerTanggal"] = new("Stok Akhir Farmasi Per Tanggal",
            """
            SELECT r.kode_brng, b.nama_brng, s.satuan, bg.nm_bangsal, SUM(r.stok_akhir) AS stok, SUM(r.stok_akhir)*b.{hpp} AS nilai
            FROM riwayat_barang_medis r
            JOIN (SELECT kode_brng, kd_bangsal, no_batch, no_faktur, MAX(CONCAT(tanggal,' ',jam)) AS mx FROM riwayat_barang_medis
                  WHERE tanggal<=@p2 GROUP BY kode_brng, kd_bangsal, no_batch, no_faktur) m
              ON m.kode_brng=r.kode_brng AND m.kd_bangsal=r.kd_bangsal AND m.no_batch=r.no_batch AND m.no_faktur=r.no_faktur AND CONCAT(r.tanggal,' ',r.jam)=m.mx
            JOIN databarang b ON b.kode_brng=r.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat LEFT JOIN bangsal bg ON bg.kd_bangsal=r.kd_bangsal
            WHERE @p1 IS NOT NULL AND CONCAT_WS(' ',r.kode_brng,b.nama_brng,bg.nm_bangsal) LIKE @p0
            GROUP BY r.kode_brng, b.nama_brng, s.satuan, bg.nm_bangsal, b.{hpp} HAVING stok<>0 ORDER BY bg.nm_bangsal, b.nama_brng
            """,
            [("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"), ("nm_bangsal", "Lokasi"), ("stok", "Stok Akhir"), ("nilai", "Nilai")],
            ReportPeriod.DateRange, "Stok akhir per barang per lokasi pada tanggal akhir periode (dari riwayat barang terakhir sampai tanggal itu)."),
    };
}
