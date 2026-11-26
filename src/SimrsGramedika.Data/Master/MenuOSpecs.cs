namespace SimrsGramedika.Data.Master;

/// <summary>
/// Menu O (analisa dan info grafik), bagian pertama: registrasi/kunjungan, rawat inap, lab/radiologi ralan,
/// pegawai, item apotek, inventaris, dan pasien meninggal. Semua layar berupa tabel agregat hanya-baca
/// (kategori, jumlah, persen) dari tabel yang sama dengan grafik Khanza. Grafik batang/pai belum digambar.
/// Bagian 2 (IKP, TB, K3, HAIs, kesling, diet, dll.) ada di MenuOMoreSpecs.cs.
/// </summary>
public static class MenuOSpecs
{
    private static readonly (string, string)[] CountColumns = [("kategori", "Kategori"), ("jumlah", "Jumlah")];

    /// <summary>Daftar hitung per kategori. <paramref name="dim"/> = ekspresi kategori, <paramref name="from"/> = FROM + JOIN.</summary>
    internal static ReportSpec Count(string title, string dim, string from, string date, ReportPeriod period,
        string measure = "COUNT(*)", string extraWhere = "", string? hint = null, string order = "jumlah DESC,kategori")
    {
        var dateFilter = period switch
        {
            ReportPeriod.DateRange or ReportPeriod.Month => $"AND {date} BETWEEN @p1 AND @p2",
            ReportPeriod.Year => $"AND YEAR({date})=@p1",
            _ => "",
        };
        var sql = $"""
            SELECT {dim} AS kategori,{measure} AS jumlah FROM {from}
            WHERE 1=1 {extraWhere} {dateFilter} AND COALESCE(CAST({dim} AS CHAR),'') LIKE @p0
            GROUP BY {dim} ORDER BY {order}
            """;
        return new(title, sql, CountColumns, period,
            hint ?? PeriodHint(period))
        {
            SearchWatermark = "Cari kategori...",
            TotalColumns = ["jumlah"],
        };
    }

    private static string PeriodHint(ReportPeriod period) => period switch
    {
        ReportPeriod.Year => "Pilih tahun. Tabel agregat hanya-baca, grafik belum digambar.",
        ReportPeriod.None => "Semua data. Tabel agregat hanya-baca, grafik belum digambar.",
        _ => "Pilih rentang tanggal. Tabel agregat hanya-baca, grafik belum digambar.",
    };

    private const string Reg = "reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis";
    private const string RegDate = "r.tgl_registrasi";

    private static string ByYear(string date) => $"YEAR({date})";
    private static string ByMonth(string date) => $"DATE_FORMAT({date},'%Y-%m')";
    private static string ByDay(string date) => $"DATE_FORMAT({date},'%Y-%m-%d')";

    /// <summary>Tiga tile Per Tahun/Bulan/Tanggal untuk satu sumber data.</summary>
    internal static void Series(Dictionary<string, ReportSpec> map, string[] keys, string label, string from, string date,
        string measure = "COUNT(*)", string extraWhere = "")
    {
        map[keys[0]] = Count($"{label} Per Tahun", ByYear(date), from, date, ReportPeriod.None, measure, extraWhere, order: "kategori");
        map[keys[1]] = Count($"{label} Per Bulan", ByMonth(date), from, date, ReportPeriod.Year, measure, extraWhere, order: "kategori");
        map[keys[2]] = Count($"{label} Per Tanggal", ByDay(date), from, date, ReportPeriod.DateRange, measure, extraWhere, order: "kategori");
    }

    private static ReportSpec Registration(string title, string dim, string join = "") =>
        Count(title, dim, Reg + (join.Length > 0 ? " " + join : ""), RegDate, ReportPeriod.DateRange);

    private static ReportSpec Pegawai(string title, string dim, string join = "") =>
        Count(title, dim, "pegawai g" + (join.Length > 0 ? " " + join : ""), "", ReportPeriod.None,
            hint: "Semua pegawai di tabel pegawai, termasuk yang tidak aktif. Tabel agregat hanya-baca.");

    private static ReportSpec Inventory(string title, string dim, string join) =>
        Count(title, dim, "inventaris i JOIN inventaris_barang b ON b.kode_barang=i.kode_barang " + join, "", ReportPeriod.None,
            hint: "Jumlah unit inventaris per kategori. Tabel agregat hanya-baca.");

    private static ReportSpec Pharmacy(string title, string dim, string join) =>
        Count(title, dim, "databarang b " + join, "", ReportPeriod.None, extraWhere: "AND b.status='1'",
            hint: "Item aktif di databarang. Tabel agregat hanya-baca.");

    private const string AgeBucket =
        "CASE WHEN r.sttsumur<>'Th' THEN '0 - 1 Tahun' WHEN r.umurdaftar<5 THEN '1 - 4 Tahun' WHEN r.umurdaftar<15 THEN '5 - 14 Tahun' " +
        "WHEN r.umurdaftar<25 THEN '15 - 24 Tahun' WHEN r.umurdaftar<45 THEN '25 - 44 Tahun' WHEN r.umurdaftar<65 THEN '45 - 64 Tahun' ELSE '65 Tahun ke atas' END";

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = Build();

    private static Dictionary<string, ReportSpec> Build()
    {
        var map = new Dictionary<string, ReportSpec>
        {
            // Registrasi
            ["btnGrafikKunjunganPoli"] = Registration("Registrasi Per Poli", "COALESCE(pl.nm_poli,r.kd_poli)", "LEFT JOIN poliklinik pl ON pl.kd_poli=r.kd_poli"),
            ["btnGrafikKunjunganPerDokter"] = Registration("Registrasi Per Dokter", "COALESCE(d.nm_dokter,r.kd_dokter)", "LEFT JOIN dokter d ON d.kd_dokter=r.kd_dokter"),
            ["btnGrafikKunjunganPerPekerjaan"] = Registration("Registrasi Per Pekerjaan", "COALESCE(NULLIF(p.pekerjaan,''),'-')"),
            ["btnGrafikKunjunganPerPendidikan"] = Registration("Registrasi Per Pendidikan", "COALESCE(NULLIF(p.pnd,''),'-')"),
            ["btnGrafikKunjunganPerAgama"] = Registration("Registrasi Per Agama", "COALESCE(NULLIF(p.agama,''),'-')"),
            ["btnGrafikKunjunganPerUmur"] = Registration("Registrasi Per Umur", AgeBucket),
            ["btnGrafikKunjunganPerSuku"] = Registration("Registrasi Per Suku/Bangsa", "COALESCE(s.nama_suku_bangsa,'-')", "LEFT JOIN suku_bangsa s ON s.id=p.suku_bangsa"),
            ["btnGrafikKunjunganPerBahasa"] = Registration("Registrasi Per Bahasa", "COALESCE(b.nama_bahasa,'-')", "LEFT JOIN bahasa_pasien b ON b.id=p.bahasa_pasien"),
            ["btnGrafikKunjunganPerCacat"] = Registration("Registrasi Per Cacat Fisik", "COALESCE(c.nama_cacat,'-')", "LEFT JOIN cacat_fisik c ON c.id=p.cacat_fisik"),
            ["btnGrafikKunjunganPerCarabayar"] = Registration("Registrasi Per Cara Bayar", "COALESCE(j.png_jawab,r.kd_pj)", "LEFT JOIN penjab j ON j.kd_pj=r.kd_pj"),
            ["btnGrafikDemografiRegistrasi"] = Registration("Demografi Registrasi",
                "CONCAT_WS(' / ',COALESCE(kb.nm_kab,'-'),COALESCE(kc.nm_kec,'-'))",
                "LEFT JOIN kabupaten kb ON kb.kd_kab=p.kd_kab LEFT JOIN kecamatan kc ON kc.kd_kec=p.kd_kec"),
            ["btnGrafikPerPerujuk"] = Count("Registrasi Per Perujuk", "COALESCE(NULLIF(m.perujuk,''),'-')",
                "rujuk_masuk m JOIN reg_periksa r ON r.no_rawat=m.no_rawat", RegDate, ReportPeriod.DateRange),

            // Rawat inap
            ["btnGrafikKunjunganRanapRuang"] = Count("Kunjungan Ranap Per Ruang", "COALESCE(bs.nm_bangsal,k.kd_bangsal)",
                "kamar_inap ki JOIN kamar k ON k.kd_kamar=ki.kd_kamar LEFT JOIN bangsal bs ON bs.kd_bangsal=k.kd_bangsal",
                "ki.tgl_masuk", ReportPeriod.DateRange),
            ["btnGrafikStatusPulangRanap"] = Count("Status Pulang Ranap", "COALESCE(NULLIF(ki.stts_pulang,''),'-')", "kamar_inap ki",
                "ki.tgl_keluar", ReportPeriod.DateRange, extraWhere: "AND ki.stts_pulang<>'-'",
                hint: "Berdasarkan tanggal keluar. Yang masih dirawat tidak ikut. Tabel agregat hanya-baca."),
            ["btnGrafikMeninggalPerBulan"] = Count("Pasien Meninggal Per Bulan", ByMonth("m.tanggal"), "pasien_mati m", "m.tanggal", ReportPeriod.Year),

            // Pegawai
            ["btnGrafikJenjangJabatanPegawai"] = Pegawai("Pegawai Per Jenjang Jabatan", "COALESCE(j.nama,g.jnj_jabatan)", "LEFT JOIN jnj_jabatan j ON j.kode=g.jnj_jabatan"),
            ["btnGrafikBidangPegawai"] = Pegawai("Pegawai Per Bidang/Bagian", "COALESCE(NULLIF(g.bidang,''),'-')"),
            ["btnGrafikDepartemenPegawai"] = Pegawai("Pegawai Per Departemen", "COALESCE(dp.nama,g.departemen)", "LEFT JOIN departemen dp ON dp.dep_id=g.departemen"),
            ["btnGrafikPendidikanPegawai"] = Pegawai("Pegawai Per Pendidikan", "COALESCE(NULLIF(g.pendidikan,''),'-')"),
            ["btnGrafikStatusWPPegawai"] = Pegawai("Pegawai Per Status WP", "COALESCE(NULLIF(g.stts_wp,''),'-')"),
            ["btnGrafikStatusKerjaPegawai"] = Pegawai("Pegawai Per Status Kerja", "COALESCE(NULLIF(g.stts_kerja,''),'-')"),
            ["btnGrafikKelompokJabatanPegawai"] = Pegawai("Pegawai Per Kelompok Jabatan", "COALESCE(kj.nama_kelompok,g.kode_kelompok)", "LEFT JOIN kelompok_jabatan kj ON kj.kode_kelompok=g.kode_kelompok"),
            ["btnGrafikRisikoKerjaPegawai"] = Pegawai("Pegawai Per Risiko Kerja", "COALESCE(rk.nama_resiko,g.kode_resiko)", "LEFT JOIN resiko_kerja rk ON rk.kode_resiko=g.kode_resiko"),
            ["btnGrafikEmergencyIndexPegawai"] = Pegawai("Pegawai Per Emergency Index", "COALESCE(ei.nama_emergency,g.kode_emergency)", "LEFT JOIN emergency_index ei ON ei.kode_emergency=g.kode_emergency"),

            // Item apotek
            ["btnGrafikItemApotekPerJenis"] = Pharmacy("Item Apotek Per Jenis", "COALESCE(j.nama,b.kdjns)", "LEFT JOIN jenis j ON j.kdjns=b.kdjns"),
            ["btnGrafikItemApotekPerKategori"] = Pharmacy("Item Apotek Per Kategori", "COALESCE(k.nama,b.kode_kategori)", "LEFT JOIN kategori_barang k ON k.kode=b.kode_kategori"),
            ["btnGrafikItemApotekPerGolongan"] = Pharmacy("Item Apotek Per Golongan", "COALESCE(g.nama,b.kode_golongan)", "LEFT JOIN golongan_barang g ON g.kode=b.kode_golongan"),
            ["btnGrafikItemApotekPerIndustriFarmasi"] = Pharmacy("Item Apotek Per Industri Farmasi", "COALESCE(f.nama_industri,b.kode_industri)", "LEFT JOIN industrifarmasi f ON f.kode_industri=b.kode_industri"),

            // Inventaris
            ["btnGrafikInventarisRuang"] = Inventory("Jumlah Inventaris Per Ruang", "COALESCE(ru.nama_ruang,i.id_ruang)", "LEFT JOIN inventaris_ruang ru ON ru.id_ruang=i.id_ruang"),
            ["btnGrafikInventarisJenis"] = Inventory("Jumlah Inventaris Per Jenis", "COALESCE(jn.nama_jenis,b.id_jenis)", "LEFT JOIN inventaris_jenis jn ON jn.id_jenis=b.id_jenis"),
            ["btnGrafikInventarisKategori"] = Inventory("Jumlah Inventaris Per Kategori", "COALESCE(kt.nama_kategori,b.id_kategori)", "LEFT JOIN inventaris_kategori kt ON kt.id_kategori=b.id_kategori"),
            ["btnGrafikInventarisMerk"] = Inventory("Jumlah Inventaris Per Merk", "COALESCE(mk.nama_merk,b.id_merk)", "LEFT JOIN inventaris_merk mk ON mk.id_merk=b.id_merk"),
            ["btnGrafikInventarisProdusen"] = Inventory("Jumlah Inventaris Per Produsen", "COALESCE(pr.nama_produsen,b.kode_produsen)", "LEFT JOIN inventaris_produsen pr ON pr.kode_produsen=b.kode_produsen"),
        };

        Series(map, ["btnGrafikKunjunganPerTahun", "btnGrafikKunjunganPerBulan", "btnGrafikKunjunganPerTanggal"], "Registrasi", Reg, RegDate);
        Series(map, ["btnGrafikStatusRegPerTahun", "btnGrafikStatusRegPerBulan", "btnGrafikStatusRegPerTanggal"], "Registrasi Lama", Reg, RegDate,
            extraWhere: "AND r.stts_daftar='Lama'");
        Series(map, ["btnGrafikStatusRegPerTahun2", "btnGrafikStatusRegPerBulan2", "btnGrafikStatusRegPerTanggal2"], "Registrasi Baru", Reg, RegDate,
            extraWhere: "AND r.stts_daftar='Baru'");
        Series(map, ["btnGrafikStatusRegBatalPerTahun", "btnGrafikStatusRegBatalPerBulan", "btnGrafikStatusRegBatalPerTanggal"], "Batal Periksa", Reg, RegDate,
            extraWhere: "AND r.stts='Batal'");
        Series(map, ["btnGrafikKunjunganRanapPerTahun", "btnGrafikKunjunganRanapBulan", "btnGrafikKunjunganRanapTanggal"], "Kunjungan Ranap",
            "kamar_inap ki", "ki.tgl_masuk");
        Series(map, ["btnGrafikLabRalanPerTahun", "btnGrafikLabRalanPerBulan", "btnGrafikLabRalanPerHari"], "Kunjungan Lab Ralan",
            "periksa_lab l JOIN reg_periksa r ON r.no_rawat=l.no_rawat", "l.tgl_periksa", "COUNT(DISTINCT l.no_rawat)", "AND r.status_lanjut='Ralan'");
        Series(map, ["btnGrafikRadRalanPerTahun", "btnGrafikRadRalanPerBulan", "btnGrafikRadRalanPerHari"], "Kunjungan Rad Ralan",
            "periksa_radiologi l JOIN reg_periksa r ON r.no_rawat=l.no_rawat", "l.tgl_periksa", "COUNT(DISTINCT l.no_rawat)", "AND r.status_lanjut='Ralan'");
        MenuOMoreSpecs.Add(map);
        return map;
    }
}
