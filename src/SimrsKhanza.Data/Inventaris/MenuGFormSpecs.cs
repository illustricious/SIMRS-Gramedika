using SimrsKhanza.Data.Master;

namespace SimrsKhanza.Data.Inventaris;

internal static class MenuGFormSpecs
{
    private const string PetugasLookup = "SELECT nip AS kode,nama FROM petugas ORDER BY nama,nip";

    private static MasterField Ukuran(string column, string label, bool required = false) =>
        new(column, label, FieldKind.Number, Required: required) { NullWhenEmpty = !required, Default = required ? 0d : null };

    private static MasterField TeksOpsional(string column, string label, int length) =>
        new(column, label, MaxLen: length) { NullWhenEmpty = true };

    private static MasterSpec CatatanKesling(string title, string table, string[] searchColumns, params MasterField[] fields) => new(
        title, table,
        [
            new("nip", "Petugas", FieldKind.Lookup, LookupSql: PetugasLookup, Key: true),
            new("tanggal", "Waktu Pencatatan", FieldKind.DateTime, Key: true),
            .. fields
        ],
        $"""
        SELECT x.*,p.nama AS petugas
        FROM {table} x LEFT JOIN petugas p ON p.nip=x.nip
        WHERE x.tanggal BETWEEN @p1 AND @p2
          AND CONCAT_WS(' ',{string.Join(",", new[] { "x.nip", "p.nama" }.Concat(searchColumns.Select(c => "x." + c)))}) LIKE @p0
        ORDER BY x.tanggal DESC,x.nip
        """,
        [("tanggal", "Waktu Pencatatan"), ("nip", "NIP"), ("petugas", "Petugas"), .. fields.Select(f => (f.Column, f.Label))])
    { Period = PeriodFilter.DateRange };

    private static MasterSpec PemakaianAir(string title, string table) => CatatanKesling(title, table, ["keterangan"],
        Ukuran("meteran", "Meteran"), Ukuran("jumlahharian", "Jumlah Harian (m³)"), TeksOpsional("keterangan", "Keterangan", 50));

    private static MasterSpec LimbahB3(string title, string table, string unit) => CatatanKesling(title, table,
        ["tujuan_penyerahan", "bukti_dokumen", "keterangan"],
        Ukuran("jmllimbah", $"Jumlah ({unit})"), TeksOpsional("tujuan_penyerahan", "Tujuan Penyerahan", 50),
        TeksOpsional("bukti_dokumen", "Bukti Dokumen", 20), Ukuran("sisa_di_tps", $"Sisa di TPS ({unit})"),
        new MasterField("keterangan", "Keterangan", MaxLen: 60));

    private static readonly MasterSpec Gedung = new(
        "Pemeliharaan Gedung", "pemeliharaan_gedung",
        [
            new("no_pemeliharaan", "No. Pemeliharaan", Key: true, AutoKey: true, MaxLen: 20),
            new("tanggal", "Tanggal", FieldKind.Date, Required: true),
            new("uraian_kegiatan", "Uraian Kegiatan", FieldKind.Memo, MaxLen: 255, Required: true),
            new("nip", "Petugas", FieldKind.Lookup, LookupSql: PetugasLookup, Required: true),
            new("pelaksana", "Pelaksana", FieldKind.Choice, Choices: ["Teknisi Rumah Sakit", "Pihak ke III"]),
            Ukuran("biaya", "Biaya", required: true),
            new("jenis_pemeliharaan", "Jenis Pemeliharaan", FieldKind.Choice,
                Choices: ["Running Maintenance", "Shut Down Maintenance", "Emergency Maintenance"]),
            new("tindak_lanjut", "Tindak Lanjut", MaxLen: 100),
        ],
        """
        SELECT x.*,p.nama AS petugas
        FROM pemeliharaan_gedung x LEFT JOIN petugas p ON p.nip=x.nip
        WHERE x.tanggal BETWEEN @p1 AND @p2
          AND CONCAT_WS(' ',x.no_pemeliharaan,x.uraian_kegiatan,x.nip,p.nama,x.pelaksana,x.jenis_pemeliharaan,x.tindak_lanjut) LIKE @p0
        ORDER BY x.tanggal DESC,x.no_pemeliharaan
        """,
        [("no_pemeliharaan", "No. Pemeliharaan"), ("tanggal", "Tanggal"), ("uraian_kegiatan", "Uraian Kegiatan"),
         ("nip", "NIP"), ("petugas", "Petugas"), ("pelaksana", "Pelaksana"), ("biaya", "Biaya"),
         ("jenis_pemeliharaan", "Jenis Pemeliharaan"), ("tindak_lanjut", "Tindak Lanjut")],
        "SELECT COALESCE(MAX(CAST(RIGHT(no_pemeliharaan,3) AS UNSIGNED)),0) FROM pemeliharaan_gedung WHERE tanggal=@p0", 3)
    { Period = PeriodFilter.DateRange, AutoKeyPrefix = "PG{0:yyyyMMdd}", AutoKeyDateField = "tanggal" };

    private static readonly MasterSpec BarangCssd = new(
        "Barang CSSD", "cssd_barang",
        [
            new("no_inventaris", "Inventaris", FieldKind.Lookup, Key: true, LookupSql: """
                SELECT i.no_inventaris AS kode,CONCAT_WS(' · ',b.nama_barang,r.nama_ruang,i.no_inventaris) AS nama
                FROM inventaris i LEFT JOIN inventaris_barang b ON b.kode_barang=i.kode_barang
                LEFT JOIN inventaris_ruang r ON r.id_ruang=i.id_ruang
                ORDER BY b.nama_barang,i.no_inventaris
                """),
            new("jenis_barang", "Jenis Barang CSSD", FieldKind.Choice, Choices:
                ["-", "Heacting Set", "Partus Set", "Set Bedah", "Set Minor", "Set SC", "Set Kuret", "Set Hernia",
                 "Set THT", "Set APP", "Set Histerektomi", "Set Tonsil", "Set Mata", "Set Pheco", "Set Bedah Mulut",
                 "Set Othopedi Minor", "Set Bor Orthopedi", "Set Vaskuler", "Set Hemoroid", "Set Duk",
                 "Set Instrumen Satuan", "Selang"], Required: true)
        ],
        """
        SELECT c.no_inventaris,c.jenis_barang,i.kode_barang,b.nama_barang,r.nama_ruang,i.status_barang
        FROM cssd_barang c JOIN inventaris i ON i.no_inventaris=c.no_inventaris
        LEFT JOIN inventaris_barang b ON b.kode_barang=i.kode_barang
        LEFT JOIN inventaris_ruang r ON r.id_ruang=i.id_ruang
        WHERE CONCAT_WS(' ',c.no_inventaris,c.jenis_barang,i.kode_barang,b.nama_barang,r.nama_ruang,i.status_barang) LIKE @p0
        ORDER BY b.nama_barang,c.no_inventaris
        """,
        [("no_inventaris", "No. Inventaris"), ("kode_barang", "Kode Barang"), ("nama_barang", "Nama Barang"),
         ("jenis_barang", "Jenis Barang CSSD"), ("nama_ruang", "Ruang"), ("status_barang", "Status Inventaris")])
    { Hint = "Pilih aset yang sudah terdaftar di Data Inventaris untuk menambahkan kategori CSSD." };

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnPemeliharaanGedung"] = Gedung,
        ["btnBarangCSSD"] = BarangCssd,
        ["btnPemakaianAirPDAM"] = PemakaianAir("Pemakaian Air PDAM", "kesling_pemakaian_air_pdam"),
        ["btnPemakaianAirTanah"] = PemakaianAir("Pemakaian Air Tanah", "kesling_pemakaian_air_tanah"),
        ["btnLimbahB3Medis"] = LimbahB3("Limbah Padat B3 Medis", "kesling_limbah_b3medis", "kg"),
        ["btnLimbahB3MedisCair"] = LimbahB3("Limbah Cair B3 Medis", "kesling_limbah_b3medis_cair", "L"),
        ["btnLimbahDomestik"] = CatatanKesling("Limbah Padat Domestik", "kesling_limbah_domestik", ["keterangan"],
            Ukuran("jumlahlimbah", "Jumlah (kg)"), new MasterField("tanggalangkut", "Waktu Angkut", FieldKind.DateTime) { NullWhenEmpty = true },
            TeksOpsional("keterangan", "Keterangan", 50)),
        ["btnMutuAirLimbah"] = CatatanKesling("Mutu Air Limbah", "kesling_mutu_air_limbah", [],
            Ukuran("meteran", "Meteran"), Ukuran("jumlahharian", "Jumlah Harian (m³)"), Ukuran("ph", "pH"),
            Ukuran("suhu", "Suhu (°C)"), Ukuran("tds", "TDS (ppm)", required: true),
            Ukuran("ec", "EC (µS/cm)", required: true), Ukuran("salt", "Salt (ppm)", required: true)),
        ["btnPestControl"] = CatatanKesling("Pest Control", "kesling_pest_control", ["rincian_kegiatan", "rekomendasi"],
            new MasterField("rincian_kegiatan", "Rincian Kegiatan", FieldKind.Memo) { NullWhenEmpty = true },
            new MasterField("rekomendasi", "Rekomendasi", FieldKind.Memo) { NullWhenEmpty = true }),
    };
}
