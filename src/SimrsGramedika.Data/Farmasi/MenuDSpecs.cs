using SimrsGramedika.Data.Master;
using K = SimrsGramedika.Data.Master.FieldKind;

namespace SimrsGramedika.Data.Farmasi;

/// <summary>
/// Menu [D] "Transaksi Inventory Obat, BHP Medis, Alat Kesehatan" — master tables and read-only
/// reports as data (legacy DlgBarang, DlgJenis, InventorySuplier, DlgRiwayatBarangMedis, DlgSirkulasi*,
/// InventoryRingkasan*, ...). Stock transactions live in <see cref="FarmasiStokRepository"/>.
/// </summary>
public static class MenuDSpecs
{
    private static MasterField Lk(string col, string label, string sql, bool required = false) =>
        new(col, label, K.Lookup, LookupSql: sql, Required: required);

    private const string SatuanLookup = "SELECT kode_sat AS kode, satuan AS nama FROM kodesatuan ORDER BY satuan";

    private static MasterSpec Simple(string title, string table, string code, int codeLen, string name, int nameLen, params MasterField[] more) => new(
        title, table, [new(code, "Kode", MaxLen: codeLen, Key: true), new(name, "Nama", MaxLen: nameLen, Required: true), .. more],
        $"SELECT * FROM {table} WHERE CONCAT_WS(' ',{string.Join(",", new[] { code, name }.Concat(more.Select(m => m.Column)))}) LIKE @p0 ORDER BY {code}",
        [(code, "Kode"), (name, "Nama"), .. more.Select(m => (m.Column, m.Label))]);

    private static MasterField[] Kontak(int telp) =>
        [new("alamat", "Alamat", MaxLen: 50), new("kota", "Kota", MaxLen: 20), new("no_telp", "No.Telp", MaxLen: telp)];

    private static readonly string[] HargaCols = ["dasar", "h_beli", "ralan", "kelas1", "kelas2", "kelas3", "utama", "vip", "vvip", "beliluar", "jualbebas", "karyawan"];
    private static readonly string[] HargaLabels = ["Harga Dasar", "Harga Beli", "Ralan", "Kelas 1", "Kelas 2", "Kelas 3", "Utama/BPJS", "VIP", "VVIP", "Beli Luar", "Jual Bebas", "Karyawan"];

    private static readonly MasterSpec Obat = new(
        "Obat, Alkes & BHP", "databarang",
        [
            new("kode_brng", "Kode Barang", MaxLen: 15, Key: true),
            new("nama_brng", "Nama Barang", MaxLen: 80, Required: true),
            Lk("kdjns", "Jenis", "SELECT kdjns AS kode, nama FROM jenis ORDER BY nama", true),
            Lk("kode_kategori", "Kategori", "SELECT kode, nama FROM kategori_barang ORDER BY nama", true),
            Lk("kode_golongan", "Golongan", "SELECT kode, nama FROM golongan_barang ORDER BY nama", true),
            Lk("kode_industri", "Industri Farmasi", "SELECT kode_industri AS kode, nama_industri AS nama FROM industrifarmasi ORDER BY nama_industri", true),
            Lk("kode_satbesar", "Satuan Besar", SatuanLookup, true),
            new("isi", "Isi (per satuan besar)", K.Number) { Default = 1d },
            Lk("kode_sat", "Satuan Kecil", SatuanLookup, true),
            new("kapasitas", "Kapasitas", K.Number) { Default = 1d },
            new("letak_barang", "Letak Barang", MaxLen: 100),
            .. HargaCols.Select((c, i) => new MasterField(c, HargaLabels[i], K.Number)),
            new("stokminimal", "Stok Minimal", K.Number),
            new("expire", "Kadaluarsa", K.Date),
            new("status", "Status (1=Aktif)", K.Choice, Choices: ["1", "0"]),
        ],
        """
        SELECT b.*, j.nama AS jenis, s.satuan, sb.satuan AS satbesar, i.nama_industri
        FROM databarang b LEFT JOIN jenis j ON j.kdjns=b.kdjns LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
        LEFT JOIN kodesatuan sb ON sb.kode_sat=b.kode_satbesar LEFT JOIN industrifarmasi i ON i.kode_industri=b.kode_industri
        WHERE CONCAT_WS(' ',b.kode_brng,b.nama_brng,j.nama,b.letak_barang,i.nama_industri) LIKE @p0 ORDER BY b.status DESC, b.nama_brng
        """,
        [("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"), ("satbesar", "Sat.Besar"), ("isi", "Isi"), ("jenis", "Jenis"),
         ("nama_industri", "Industri"), ("dasar", "Dasar"), ("h_beli", "H.Beli"), ("ralan", "Ralan"), ("kelas1", "Kls 1"), ("kelas2", "Kls 2"),
         ("kelas3", "Kls 3"), ("utama", "Utama"), ("vip", "VIP"), ("vvip", "VVIP"), ("jualbebas", "Jual Bebas"), ("stokminimal", "Stok Min"),
         ("expire", "Kadaluarsa"), ("letak_barang", "Letak"), ("status", "Status")])
    { Hint = "Status 0 = tidak aktif (cara Khanza lama \"menghapus\" barang). Harga per satuan kecil." };

    private static readonly MasterSpec Konversi = new(
        "Konversi Satuan", "konver_sat",
        [
            new("nilai", "Nilai", K.Number, Key: true) { Default = 1d },
            new("kode_sat", "Satuan", K.Lookup, LookupSql: SatuanLookup, Key: true),
            new("nilai_konversi", "Nilai Konversi", K.Number, Key: true) { Default = 1d },
            new("sat_konversi", "Satuan Konversi", K.Lookup, LookupSql: SatuanLookup, Key: true),
        ],
        """
        SELECT k.*, a.satuan AS satuan, b.satuan AS satuan_konversi FROM konver_sat k
        LEFT JOIN kodesatuan a ON a.kode_sat=k.kode_sat LEFT JOIN kodesatuan b ON b.kode_sat=k.sat_konversi
        WHERE CONCAT_WS(' ',a.satuan,b.satuan) LIKE @p0 ORDER BY a.satuan
        """,
        [("nilai", "Nilai"), ("satuan", "Satuan"), ("nilai_konversi", "="), ("satuan_konversi", "Satuan Konversi")]);

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnAsalHibah"] = Simple("Asal Hibah", "pemberihibah", "kode_pemberi", 5, "nama_pemberi", 50, Kontak(13)),
        ["btnIndustriFarmasi"] = Simple("Industri Farmasi", "industrifarmasi", "kode_industri", 5, "nama_industri", 50, Kontak(20)),
        ["btnSuplier"] = Simple("Suplier Obat/Alkes/BHP", "datasuplier", "kode_suplier", 5, "nama_suplier", 50,
            [.. Kontak(13), new("nama_bank", "Bank", MaxLen: 30), new("rekening", "No.Rekening", MaxLen: 20)]),
        ["btnSatuan"] = Simple("Satuan Barang", "kodesatuan", "kode_sat", 4, "satuan", 30),
        ["btnMetodeRacik"] = Simple("Metode Racik", "metode_racik", "kd_racik", 3, "nm_racik", 30),
        ["btnKonversi"] = Konversi,
        ["btnJnsBarang"] = Simple("Jenis Obat, Alkes & BHP", "jenis", "kdjns", 4, "nama", 30, new MasterField("keterangan", "Keterangan", MaxLen: 50)),
        ["btnKategoriBarang"] = Simple("Kategori Obat, Alkes & BHP", "kategori_barang", "kode", 4, "nama", 30),
        ["btnGolonganBarang"] = Simple("Golongan Obat, Alkes & BHP", "golongan_barang", "kode", 4, "nama", 30),
        ["btnObat"] = Obat,
    };

    // ------------------------------------------------------------------ reports

    private const string Brg = "JOIN databarang b ON b.kode_brng=x.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat";
    private static readonly (string, string)[] BarangCols = [("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan")];

    /// <summary>Header + detail transaction summarized per barang (legacy InventoryRingkasan*).</summary>
    private static ReportSpec Ringkasan(string title, string header, string detail, string key, string dateCol, string qty, string total, string? lokasi = "kd_bangsal") => new(
        title,
        $"""
        SELECT x.kode_brng, b.nama_brng, s.satuan, SUM(x.{qty}) AS jumlah, SUM(x.{total}) AS total, COUNT(DISTINCT h.{key}) AS transaksi
        FROM {header} h JOIN {detail} x ON x.{key}=h.{key} {Brg}
        {(lokasi is null ? "" : $"LEFT JOIN bangsal bg ON bg.kd_bangsal=h.{lokasi}")}
        WHERE h.{dateCol} BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng{(lokasi is null ? "" : ",bg.nm_bangsal")}) LIKE @p0
        GROUP BY x.kode_brng, b.nama_brng, s.satuan ORDER BY b.nama_brng
        """,
        [.. BarangCols, ("jumlah", "Jumlah"), ("total", "Total"), ("transaksi", "Jml Transaksi")],
        ReportPeriod.DateRange, $"Ringkasan per barang. Cari juga bisa berisi nama lokasi.");

    private static string Bulanan(string expr, string dateCol) =>
        string.Join(",\n", Enumerable.Range(1, 12).Select(m => $"IFNULL(SUM(IF(MONTH({dateCol})={m},{expr},0)),0) AS b{m}"));

    private static readonly (string, string)[] BulanCols =
        new[] { "Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des" }.Select((m, i) => ($"b{i + 1}", m)).ToArray();

    private const string RiwayatSql = """
        SELECT r.tanggal, r.jam, r.kode_brng, b.nama_brng, bg.nm_bangsal, r.posisi, r.stok_awal, r.masuk, r.keluar, r.stok_akhir,
        r.no_batch, r.no_faktur, r.petugas, r.status, r.keterangan
        FROM riwayat_barang_medis r JOIN databarang b ON b.kode_brng=r.kode_brng LEFT JOIN bangsal bg ON bg.kd_bangsal=r.kd_bangsal
        WHERE r.tanggal BETWEEN @p1 AND @p2 {0} AND CONCAT_WS(' ',r.kode_brng,b.nama_brng,bg.nm_bangsal,r.posisi,r.no_batch,r.no_faktur,r.keterangan) LIKE @p0
        ORDER BY {1} LIMIT 10000
        """;

    private static readonly (string, string)[] RiwayatCols =
    [
        ("tanggal", "Tanggal"), ("jam", "Jam"), ("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("nm_bangsal", "Lokasi"), ("posisi", "Posisi"),
        ("stok_awal", "Stok Awal"), ("masuk", "Masuk"), ("keluar", "Keluar"), ("stok_akhir", "Stok Akhir"), ("no_batch", "No.Batch"),
        ("no_faktur", "No.Faktur"), ("petugas", "Petugas"), ("status", "Status"), ("keterangan", "Keterangan"),
    ];

    /// <summary>riwayat_barang_medis movement totals per barang (optionally per lokasi), grouped by what moved the stock.</summary>
    private static ReportSpec Sirkulasi(string title, string groupCols, string groupSelect, (string, string)[] groupHeaders, bool nilai = false)
    {
        string In(string posisi) => nilai ? $"SUM(IF(r.posisi IN ({posisi}),r.masuk*b.{{hpp}},0))" : $"SUM(IF(r.posisi IN ({posisi}),r.masuk,0))";
        string Out(string posisi) => nilai ? $"SUM(IF(r.posisi IN ({posisi}),r.keluar*b.{{hpp}},0))" : $"SUM(IF(r.posisi IN ({posisi}),r.keluar,0))";
        return new(title,
            $"""
            SELECT {groupSelect},
            {In("'Penerimaan','Pengadaan','Hibah'")} AS pengadaan, {In("'Mutasi'")} AS mutasi_masuk,
            {In("'Retur Jual','Retur Pasien','Retur Piutang'")} AS retur_masuk,
            {Out("'Pemberian Obat','Resep Pulang','Stok Pasien Ranap'")} AS pemberian, {Out("'Penjualan','Piutang'")} AS penjualan,
            {Out("'Mutasi'")} AS mutasi_keluar, {Out("'Stok Keluar','Retur Beli','Pengambilan Medis'")} AS keluar_lain,
            SUM(IF(r.posisi='Opname',1,0)) AS opname
            FROM riwayat_barang_medis r JOIN databarang b ON b.kode_brng=r.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            LEFT JOIN bangsal bg ON bg.kd_bangsal=r.kd_bangsal LEFT JOIN jenis j ON j.kdjns=b.kdjns
            LEFT JOIN kategori_barang kt ON kt.kode=b.kode_kategori LEFT JOIN golongan_barang gl ON gl.kode=b.kode_golongan
            WHERE r.status='Simpan' AND r.tanggal BETWEEN @p1 AND @p2
            AND CONCAT_WS(' ',r.kode_brng,b.nama_brng,bg.nm_bangsal,j.nama,kt.nama,gl.nama) LIKE @p0
            GROUP BY {groupCols} ORDER BY {groupCols}
            """,
            [.. groupHeaders, ("pengadaan", "Penerimaan/Pengadaan/Hibah"), ("mutasi_masuk", "Mutasi Masuk"), ("retur_masuk", "Retur Masuk"),
             ("pemberian", "Pemberian/Resep"), ("penjualan", "Penjualan/Piutang"), ("mutasi_keluar", "Mutasi Keluar"), ("keluar_lain", "Stok Keluar/Retur Beli"),
             ("opname", "Kali Opname")],
            ReportPeriod.DateRange,
            (nilai ? "Nilai (jumlah × HPP) " : "Jumlah ") + "barang masuk/keluar per jenis pergerakan, dari riwayat barang medis.");
    }

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnRiwayatBarangMedis"] = new("Riwayat Obat, Alkes & BHP", string.Format(RiwayatSql, "", "r.tanggal DESC, r.jam DESC"), RiwayatCols, ReportPeriod.DateRange,
            "Kartu stok dari riwayat_barang_medis (maks. 10.000 baris; persempit tanggal/cari bila perlu)."),
        ["btnRiwayatBatch"] = new("Riwayat Batch", string.Format(RiwayatSql, "AND r.no_batch<>''", "r.no_batch, r.kode_brng, r.tanggal, r.jam"), RiwayatCols, ReportPeriod.DateRange,
            "Pergerakan stok per No.Batch."),

        ["btnDataBatch"] = new("Data Batch",
            """
            SELECT d.*, b.nama_brng, s.satuan FROM data_batch d JOIN databarang b ON b.kode_brng=d.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            WHERE d.tgl_beli BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',d.no_batch,d.no_faktur,d.kode_brng,b.nama_brng,d.asal) LIKE @p0 ORDER BY d.tgl_beli DESC
            """,
            [("no_batch", "No.Batch"), ("no_faktur", "No.Faktur"), ("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"), ("asal", "Asal"),
             ("tgl_beli", "Tgl.Beli"), ("tgl_kadaluarsa", "Kadaluarsa"), ("jumlahbeli", "Jml Beli"), ("sisa", "Sisa"), ("dasar", "Dasar"), ("h_beli", "H.Beli"),
             ("ralan", "Ralan"), ("kelas1", "Kls 1"), ("kelas2", "Kls 2"), ("kelas3", "Kls 3"), ("utama", "Utama"), ("vip", "VIP"), ("vvip", "VVIP"),
             ("jualbebas", "Jual Bebas")],
            ReportPeriod.DateRange, "Batch berdasarkan tanggal beli/terima."),
        ["btnKadaluarsaBatch"] = new("Kadaluarsa Batch",
            """
            SELECT d.no_batch, d.no_faktur, d.kode_brng, b.nama_brng, s.satuan, d.tgl_kadaluarsa, DATEDIFF(d.tgl_kadaluarsa,CURDATE()) AS hari, d.sisa,
            d.sisa*d.{hpp} AS nilai FROM data_batch d JOIN databarang b ON b.kode_brng=d.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            WHERE d.sisa>0 AND d.tgl_kadaluarsa BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',d.no_batch,d.kode_brng,b.nama_brng) LIKE @p0 ORDER BY d.tgl_kadaluarsa
            """,
            [("no_batch", "No.Batch"), ("no_faktur", "No.Faktur"), ("kode_brng", "Kode"), ("nama_brng", "Nama Barang"), ("satuan", "Satuan"),
             ("tgl_kadaluarsa", "Kadaluarsa"), ("hari", "Sisa Hari"), ("sisa", "Sisa Stok"), ("nilai", "Nilai")],
            ReportPeriod.DateRange, "Batch yang masih bersisa dan kadaluarsa di rentang tanggal terpilih (pilih tanggal ke depan untuk peringatan)."),

        ["btnDaruratStok"] = new("Darurat Stok",
            """
            SELECT b.kode_brng, b.nama_brng, s.satuan, j.nama AS jenis, b.stokminimal, IFNULL(SUM(g.stok),0) AS stok,
            b.stokminimal-IFNULL(SUM(g.stok),0) AS kurang
            FROM databarang b LEFT JOIN gudangbarang g ON g.kode_brng=b.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat LEFT JOIN jenis j ON j.kdjns=b.kdjns
            WHERE b.status='1' AND CONCAT_WS(' ',b.kode_brng,b.nama_brng,j.nama) LIKE @p0
            GROUP BY b.kode_brng, b.nama_brng, s.satuan, j.nama, b.stokminimal HAVING stok<=b.stokminimal ORDER BY kurang DESC
            """,
            [.. BarangCols, ("jenis", "Jenis"), ("stokminimal", "Stok Minimal"), ("stok", "Stok Semua Lokasi"), ("kurang", "Kekurangan")],
            ReportPeriod.None, "Barang aktif yang total stoknya (semua lokasi) sudah di bawah/sama dengan stok minimal."),
        ["btnSisaStok"] = new("Sisa Stok",
            """
            SELECT g.kode_brng, b.nama_brng, s.satuan, bg.nm_bangsal, SUM(g.stok) AS stok, b.{hpp} AS harga, SUM(g.stok)*b.{hpp} AS nilai
            FROM gudangbarang g JOIN databarang b ON b.kode_brng=g.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            JOIN bangsal bg ON bg.kd_bangsal=g.kd_bangsal
            WHERE CONCAT_WS(' ',g.kode_brng,b.nama_brng,bg.nm_bangsal) LIKE @p0
            GROUP BY g.kode_brng, b.nama_brng, s.satuan, bg.nm_bangsal, b.{hpp} HAVING stok<>0 ORDER BY bg.nm_bangsal, b.nama_brng
            """,
            [.. BarangCols, ("nm_bangsal", "Lokasi"), ("stok", "Stok"), ("harga", "HPP"), ("nilai", "Nilai")],
            ReportPeriod.None, "Stok saat ini per barang per lokasi (semua batch dijumlah)."),
        ["btnObatBHPTidakBergerak"] = new("Obat/Alkes/BHP Tidak Bergerak",
            """
            SELECT b.kode_brng, b.nama_brng, s.satuan, SUM(g.stok) AS stok, SUM(g.stok)*b.{hpp} AS nilai,
            (SELECT MAX(r.tanggal) FROM riwayat_barang_medis r WHERE r.kode_brng=b.kode_brng AND r.keluar>0) AS terakhir_keluar
            FROM databarang b JOIN gudangbarang g ON g.kode_brng=b.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            WHERE CONCAT_WS(' ',b.kode_brng,b.nama_brng) LIKE @p0
            AND NOT EXISTS (SELECT 1 FROM riwayat_barang_medis r WHERE r.kode_brng=b.kode_brng AND r.keluar>0 AND r.tanggal BETWEEN @p1 AND @p2)
            GROUP BY b.kode_brng, b.nama_brng, s.satuan, b.{hpp} HAVING stok>0 ORDER BY nilai DESC
            """,
            [.. BarangCols, ("stok", "Stok"), ("nilai", "Nilai"), ("terakhir_keluar", "Terakhir Keluar")],
            ReportPeriod.DateRange, "Barang yang masih ada stok tetapi tidak keluar sama sekali di periode terpilih."),

        ["btnRingkasanPermintaanMedis"] = Ringkasan("Ringkasan Permintaan Obat & BHP", "permintaan_medis", "detail_permintaan_medis", "no_permintaan", "tanggal", "jumlah", "jumlah"),
        ["btnRingkasanPengajuanMedis"] = Ringkasan("Ringkasan Pengajuan Obat & BHP", "pengajuan_barang_medis", "detail_pengajuan_barang_medis", "no_pengajuan", "tanggal", "jumlah2", "total", null),
        ["btnRingkasanPemesananMedis"] = Ringkasan("Ringkasan Pemesanan Obat & BHP", "surat_pemesanan_medis", "detail_surat_pemesanan_medis", "no_pemesanan", "tanggal", "jumlah2", "total", null),
        ["btnRingkasanPembelianMedis"] = Ringkasan("Ringkasan Pengadaan Obat & BHP", "pembelian", "detailbeli", "no_faktur", "tgl_beli", "jumlah2", "total"),
        ["btnRingkasanPenerimaanMedis"] = Ringkasan("Ringkasan Penerimaan Obat & BHP", "pemesanan", "detailpesan", "no_faktur", "tgl_pesan", "jumlah2", "total"),
        ["btnRingkasanHibahMedis"] = Ringkasan("Ringkasan Hibah Obat & BHP", "hibah_obat_bhp", "detailhibah_obat_bhp", "no_hibah", "tgl_hibah", "jumlah2", "subtotaldiakui"),
        ["btnRingkasanPenjualanMedis"] = Ringkasan("Ringkasan Penjualan Obat & BHP", "penjualan", "detailjual", "nota_jual", "tgl_jual", "jumlah", "total"),
        ["btnRingkasanPiutangObat"] = Ringkasan("Ringkasan Piutang Obat & BHP", "piutang", "detailpiutang", "nota_piutang", "tgl_piutang", "jumlah", "total"),
        ["btnRingkasanStokKeluarObat"] = Ringkasan("Ringkasan Stok Keluar Obat & BHP", "pengeluaran_obat_bhp", "detail_pengeluaran_obat_bhp", "no_keluar", "tanggal", "jumlah", "total"),
        ["btnRingkasanReturSuplierObat"] = Ringkasan("Ringkasan Retur Suplier Obat & BHP", "returbeli", "detreturbeli", "no_retur_beli", "tgl_retur", "jml_retur2", "total"),
        ["btnRingkasanReturJualObat"] = Ringkasan("Ringkasan Retur Pembeli Obat & BHP", "returjual", "detreturjual", "no_retur_jual", "tgl_retur", "jml_retur", "subtotal"),
        ["btnRingkasanBeriObat"] = new("Ringkasan Beri Obat & BHP",
            $"""
            SELECT x.kode_brng, b.nama_brng, s.satuan, SUM(IF(x.status='Ralan',x.jml,0)) AS ralan, SUM(IF(x.status='Ranap',x.jml,0)) AS ranap,
            SUM(x.jml) AS jumlah, SUM(x.total) AS total, SUM(x.h_beli*x.jml) AS hpp
            FROM detail_pemberian_obat x {Brg} LEFT JOIN bangsal bg ON bg.kd_bangsal=x.kd_bangsal
            WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,bg.nm_bangsal) LIKE @p0
            GROUP BY x.kode_brng, b.nama_brng, s.satuan ORDER BY b.nama_brng
            """,
            [.. BarangCols, ("ralan", "Ralan"), ("ranap", "Ranap"), ("jumlah", "Jumlah"), ("total", "Total"), ("hpp", "HPP")],
            ReportPeriod.DateRange),

        ["btnRekapPenerimaanObat"] = new("Rekap Penerimaan Obat & BHP",
            """
            SELECT p.no_faktur, p.no_order, p.tgl_pesan, p.tgl_faktur, p.tgl_tempo, sp.nama_suplier, bg.nm_bangsal, p.total1, p.potongan, p.total2,
            p.ppn, p.meterai, p.tagihan, p.status
            FROM pemesanan p LEFT JOIN datasuplier sp ON sp.kode_suplier=p.kode_suplier LEFT JOIN bangsal bg ON bg.kd_bangsal=p.kd_bangsal
            WHERE p.tgl_pesan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',p.no_faktur,p.no_order,sp.nama_suplier,bg.nm_bangsal,p.status) LIKE @p0 ORDER BY p.tgl_pesan DESC
            """,
            [("no_faktur", "No.Faktur"), ("no_order", "No.Order"), ("tgl_pesan", "Tgl.Terima"), ("tgl_faktur", "Tgl.Faktur"), ("tgl_tempo", "Jatuh Tempo"),
             ("nama_suplier", "Suplier"), ("nm_bangsal", "Lokasi"), ("total1", "Total"), ("potongan", "Potongan"), ("total2", "Setelah Potongan"),
             ("ppn", "PPN"), ("meterai", "Meterai"), ("tagihan", "Tagihan"), ("status", "Status")],
            ReportPeriod.DateRange),
        ["btnPPNObat"] = new("PPN Obat",
            """
            SELECT p.no_faktur, p.tgl_faktur, sp.nama_suplier, p.total2 AS dpp, p.ppn, p.tagihan FROM pemesanan p
            LEFT JOIN datasuplier sp ON sp.kode_suplier=p.kode_suplier
            WHERE p.ppn>0 AND p.tgl_faktur BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',p.no_faktur,sp.nama_suplier) LIKE @p0 ORDER BY p.tgl_faktur
            """,
            [("no_faktur", "No.Faktur"), ("tgl_faktur", "Tgl.Faktur"), ("nama_suplier", "Suplier"), ("dpp", "DPP"), ("ppn", "PPN Masukan"), ("tagihan", "Tagihan")],
            ReportPeriod.DateRange, "PPN masukan dari faktur penerimaan obat."),
        ["btnNilaiPenerimaanVendorFarmasiPerBulan"] = new("Nilai Penerimaan Vendor Farmasi Per Bulan",
            $"""
            SELECT sp.kode_suplier, sp.nama_suplier, {Bulanan("p.tagihan", "p.tgl_pesan")}, IFNULL(SUM(p.tagihan),0) AS total
            FROM datasuplier sp LEFT JOIN pemesanan p ON p.kode_suplier=sp.kode_suplier AND YEAR(p.tgl_pesan)=@p1
            WHERE CONCAT_WS(' ',sp.kode_suplier,sp.nama_suplier) LIKE @p0
            GROUP BY sp.kode_suplier, sp.nama_suplier HAVING total>0 ORDER BY total DESC
            """,
            [("kode_suplier", "Kode"), ("nama_suplier", "Suplier"), .. BulanCols, ("total", "Total")], ReportPeriod.Year),
        ["btnKegiatanFarmasi"] = new("Kegiatan Farmasi",
            """
            SELECT m.bulan,
            (SELECT COUNT(*) FROM resep_obat r JOIN reg_periksa g ON g.no_rawat=r.no_rawat WHERE YEAR(r.tgl_peresepan)=@p1 AND MONTH(r.tgl_peresepan)=m.bulan AND g.status_lanjut='Ralan') AS resep_ralan,
            (SELECT COUNT(*) FROM resep_obat r JOIN reg_periksa g ON g.no_rawat=r.no_rawat WHERE YEAR(r.tgl_peresepan)=@p1 AND MONTH(r.tgl_peresepan)=m.bulan AND g.status_lanjut='Ranap') AS resep_ranap,
            (SELECT COUNT(*) FROM detail_pemberian_obat d WHERE YEAR(d.tgl_perawatan)=@p1 AND MONTH(d.tgl_perawatan)=m.bulan) AS item_obat,
            (SELECT COUNT(*) FROM penjualan j WHERE YEAR(j.tgl_jual)=@p1 AND MONTH(j.tgl_jual)=m.bulan) AS penjualan_bebas,
            (SELECT IFNULL(SUM(d.total),0) FROM detail_pemberian_obat d WHERE YEAR(d.tgl_perawatan)=@p1 AND MONTH(d.tgl_perawatan)=m.bulan) AS nilai_obat
            FROM (SELECT 1 bulan UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8
                  UNION SELECT 9 UNION SELECT 10 UNION SELECT 11 UNION SELECT 12) m
            WHERE @p0 IS NOT NULL ORDER BY m.bulan
            """,
            [("bulan", "Bulan"), ("resep_ralan", "Resep Ralan"), ("resep_ranap", "Resep Ranap"), ("item_obat", "Item Obat Diberikan"),
             ("penjualan_bebas", "Penjualan Bebas"), ("nilai_obat", "Nilai Pemberian Obat")],
            ReportPeriod.Year, "Jumlah kegiatan pelayanan farmasi per bulan."),

        ["btnObatPerTanggal"] = new("Obat/Alkes/BHP Per Tanggal",
            $"""
            SELECT x.tgl_perawatan, x.kode_brng, b.nama_brng, s.satuan, SUM(x.jml) AS jumlah, SUM(x.total) AS total
            FROM detail_pemberian_obat x {Brg}
            WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng) LIKE @p0
            GROUP BY x.tgl_perawatan, x.kode_brng, b.nama_brng, s.satuan ORDER BY x.tgl_perawatan, b.nama_brng
            """,
            [("tgl_perawatan", "Tanggal"), .. BarangCols, ("jumlah", "Jumlah"), ("total", "Total")], ReportPeriod.DateRange),
        ["btnPenjualanPerTanggal"] = new("Penjualan Bebas Per Tanggal",
            $"""
            SELECT h.tgl_jual, x.kode_brng, b.nama_brng, s.satuan, SUM(x.jumlah) AS jumlah, SUM(x.total) AS total
            FROM penjualan h JOIN detailjual x ON x.nota_jual=h.nota_jual {Brg}
            WHERE h.tgl_jual BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng) LIKE @p0
            GROUP BY h.tgl_jual, x.kode_brng, b.nama_brng, s.satuan ORDER BY h.tgl_jual, b.nama_brng
            """,
            [("tgl_jual", "Tanggal"), .. BarangCols, ("jumlah", "Jumlah"), ("total", "Total")], ReportPeriod.DateRange),
        ["btnObatPerResep"] = new("Obat Per Resep",
            $"""
            SELECT ro.no_resep, ro.tgl_peresepan, d.nm_dokter, p.no_rkm_medis, p.nm_pasien, x.kode_brng, b.nama_brng, s.satuan, x.jml, x.total
            FROM resep_obat ro JOIN reg_periksa g ON g.no_rawat=ro.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis
            LEFT JOIN dokter d ON d.kd_dokter=ro.kd_dokter
            JOIN detail_pemberian_obat x ON x.no_rawat=ro.no_rawat AND x.tgl_perawatan=ro.tgl_perawatan AND x.jam=ro.jam {Brg}
            WHERE ro.tgl_peresepan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',ro.no_resep,d.nm_dokter,p.nm_pasien,b.nama_brng) LIKE @p0
            ORDER BY ro.tgl_peresepan DESC, ro.no_resep LIMIT 10000
            """,
            [("no_resep", "No.Resep"), ("tgl_peresepan", "Tgl.Resep"), ("nm_dokter", "Dokter"), ("no_rkm_medis", "No.RM"), ("nm_pasien", "Pasien"),
             .. BarangCols, ("jml", "Jumlah"), ("total", "Total")], ReportPeriod.DateRange),
        ["btnPenggunaObatResep"] = new("Pengguna Obat/Alkes/BHP Resep",
            $"""
            SELECT x.kode_brng, b.nama_brng, s.satuan, p.no_rkm_medis, p.nm_pasien, x.status, COUNT(*) AS kali, SUM(x.jml) AS jumlah, SUM(x.total) AS total
            FROM detail_pemberian_obat x {Brg} JOIN reg_periksa g ON g.no_rawat=x.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis
            WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,p.no_rkm_medis,p.nm_pasien) LIKE @p0
            GROUP BY x.kode_brng, b.nama_brng, s.satuan, p.no_rkm_medis, p.nm_pasien, x.status ORDER BY b.nama_brng, p.nm_pasien LIMIT 10000
            """,
            [.. BarangCols, ("no_rkm_medis", "No.RM"), ("nm_pasien", "Pasien"), ("status", "Rawat"), ("kali", "Kali"), ("jumlah", "Jumlah"), ("total", "Total")],
            ReportPeriod.DateRange, "Siapa saja yang memakai suatu obat (isi nama obat di Cari)."),
        ["btn10BesarObatPoli"] = new("10 Obat Terbanyak Poli",
            $"""
            SELECT * FROM (
              SELECT pl.nm_poli, x.kode_brng, b.nama_brng, s.satuan, SUM(x.jml) AS jumlah, SUM(x.total) AS total,
              ROW_NUMBER() OVER (PARTITION BY g.kd_poli ORDER BY SUM(x.jml) DESC) AS peringkat
              FROM detail_pemberian_obat x {Brg} JOIN reg_periksa g ON g.no_rawat=x.no_rawat JOIN poliklinik pl ON pl.kd_poli=g.kd_poli
              WHERE x.status='Ralan' AND x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',pl.nm_poli,b.nama_brng) LIKE @p0
              GROUP BY g.kd_poli, pl.nm_poli, x.kode_brng, b.nama_brng, s.satuan
            ) t WHERE t.peringkat<=10 ORDER BY t.nm_poli, t.peringkat
            """,
            [("nm_poli", "Poli"), ("peringkat", "No"), .. BarangCols, ("jumlah", "Jumlah"), ("total", "Total")], ReportPeriod.DateRange),
        ["btnRekapObatPoli"] = new("Rekap Obat Per Poli",
            $"""
            SELECT pl.nm_poli, x.kode_brng, b.nama_brng, s.satuan, SUM(x.jml) AS jumlah, SUM(x.total) AS total
            FROM detail_pemberian_obat x {Brg} JOIN reg_periksa g ON g.no_rawat=x.no_rawat JOIN poliklinik pl ON pl.kd_poli=g.kd_poli
            WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',pl.nm_poli,x.kode_brng,b.nama_brng) LIKE @p0
            GROUP BY pl.nm_poli, x.kode_brng, b.nama_brng, s.satuan ORDER BY pl.nm_poli, b.nama_brng
            """,
            [("nm_poli", "Poli"), .. BarangCols, ("jumlah", "Jumlah"), ("total", "Total")], ReportPeriod.DateRange),
        ["btnRekapObatPasien"] = new("Rekap Obat Per Pasien",
            $"""
            SELECT p.no_rkm_medis, p.nm_pasien, x.no_rawat, x.kode_brng, b.nama_brng, s.satuan, SUM(x.jml) AS jumlah, SUM(x.total) AS total
            FROM detail_pemberian_obat x {Brg} JOIN reg_periksa g ON g.no_rawat=x.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis
            WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',p.no_rkm_medis,p.nm_pasien,x.no_rawat,b.nama_brng) LIKE @p0
            GROUP BY p.no_rkm_medis, p.nm_pasien, x.no_rawat, x.kode_brng, b.nama_brng, s.satuan ORDER BY p.nm_pasien, x.no_rawat LIMIT 10000
            """,
            [("no_rkm_medis", "No.RM"), ("nm_pasien", "Pasien"), ("no_rawat", "No.Rawat"), .. BarangCols, ("jumlah", "Jumlah"), ("total", "Total")],
            ReportPeriod.DateRange),
        ["btnRingkasanBiayaObatPasienPerTanggal"] = new("Ringkasan Biaya Obat Pasien Per Tanggal",
            """
            SELECT x.tgl_perawatan, x.no_rawat, p.no_rkm_medis, p.nm_pasien, x.status, COUNT(*) AS item, SUM(x.total) AS total
            FROM detail_pemberian_obat x JOIN reg_periksa g ON g.no_rawat=x.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis
            WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,p.no_rkm_medis,p.nm_pasien) LIKE @p0
            GROUP BY x.tgl_perawatan, x.no_rawat, p.no_rkm_medis, p.nm_pasien, x.status ORDER BY x.tgl_perawatan, p.nm_pasien
            """,
            [("tgl_perawatan", "Tanggal"), ("no_rawat", "No.Rawat"), ("no_rkm_medis", "No.RM"), ("nm_pasien", "Pasien"), ("status", "Rawat"),
             ("item", "Item"), ("total", "Total Biaya Obat")], ReportPeriod.DateRange),

        ["btnKeuntungan"] = new("Keuntungan Penjualan",
            $"""
            SELECT x.kode_brng, b.nama_brng, s.satuan, SUM(x.jumlah) AS jumlah, SUM(x.total) AS penjualan, SUM(x.h_beli*x.jumlah) AS modal,
            SUM(x.total)-SUM(x.h_beli*x.jumlah) AS untung
            FROM penjualan h JOIN detailjual x ON x.nota_jual=h.nota_jual {Brg}
            WHERE h.tgl_jual BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng) LIKE @p0
            GROUP BY x.kode_brng, b.nama_brng, s.satuan ORDER BY untung DESC
            """,
            [.. BarangCols, ("jumlah", "Jumlah"), ("penjualan", "Penjualan"), ("modal", "Modal"), ("untung", "Keuntungan")], ReportPeriod.DateRange),
        ["btnKeuntunganObatRanap"] = new("Keuntungan Beri Obat",
            $"""
            SELECT x.kode_brng, b.nama_brng, s.satuan, SUM(x.jml) AS jumlah, SUM(x.total) AS pendapatan, SUM(x.h_beli*x.jml) AS modal,
            SUM(x.total)-SUM(x.h_beli*x.jml) AS untung
            FROM detail_pemberian_obat x {Brg}
            WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.kode_brng,b.nama_brng,x.status) LIKE @p0
            GROUP BY x.kode_brng, b.nama_brng, s.satuan ORDER BY untung DESC
            """,
            [.. BarangCols, ("jumlah", "Jumlah"), ("pendapatan", "Pendapatan"), ("modal", "Modal"), ("untung", "Keuntungan")], ReportPeriod.DateRange,
            "Pendapatan pemberian obat dikurangi modal (h_beli saat diberikan). Isi Ralan/Ranap di Cari untuk memisah."),
        ["btnKeuntunganObat2"] = new("Keuntungan Beri Obat 2",
            """
            SELECT x.tgl_perawatan, x.status, COUNT(*) AS item, SUM(x.total) AS pendapatan, SUM(x.h_beli*x.jml) AS modal,
            SUM(x.total)-SUM(x.h_beli*x.jml) AS untung
            FROM detail_pemberian_obat x WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND x.status LIKE @p0
            GROUP BY x.tgl_perawatan, x.status ORDER BY x.tgl_perawatan, x.status
            """,
            [("tgl_perawatan", "Tanggal"), ("status", "Rawat"), ("item", "Item"), ("pendapatan", "Pendapatan"), ("modal", "Modal"), ("untung", "Keuntungan")],
            ReportPeriod.DateRange, "Keuntungan pemberian obat per tanggal."),

        ["btnSirkulasi"] = Sirkulasi("Sirkulasi Obat, Alkes & BHP", "r.kode_brng, b.nama_brng, s.satuan", "r.kode_brng, b.nama_brng, s.satuan", BarangCols),
        ["btnSirkulasi2"] = Sirkulasi("Sirkulasi Obat, Alkes & BHP 2 (Per Lokasi)", "bg.nm_bangsal, r.kode_brng, b.nama_brng, s.satuan",
            "bg.nm_bangsal, r.kode_brng, b.nama_brng, s.satuan", [("nm_bangsal", "Lokasi"), .. BarangCols]),
        ["btnSirkulasi3"] = Sirkulasi("Sirkulasi Obat, Alkes & BHP 3 (Nilai)", "r.kode_brng, b.nama_brng, s.satuan", "r.kode_brng, b.nama_brng, s.satuan", BarangCols, nilai: true),
        ["btnSirkulasiObat4"] = Sirkulasi("Sirkulasi Obat, Alkes & BHP 4 (Per Jenis)", "j.nama", "j.nama AS jenis", [("jenis", "Jenis")], nilai: true),
        ["btnSirkulasiObat5"] = Sirkulasi("Sirkulasi Obat, Alkes & BHP 5 (Per Kategori)", "kt.nama", "kt.nama AS kategori", [("kategori", "Kategori")], nilai: true),
        ["btnSirkulasiObat6"] = Sirkulasi("Sirkulasi Obat, Alkes & BHP 6 (Per Golongan)", "gl.nama", "gl.nama AS golongan", [("golongan", "Golongan")], nilai: true),
    };
}
