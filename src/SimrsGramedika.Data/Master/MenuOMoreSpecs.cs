namespace SimrsGramedika.Data.Master;

/// <summary>Menu O bagian 2: IKP, TB, kesling (air, limbah), K3, pengajuan aset, HAIs, hemodialisa, diet, perbaikan inventaris, penerimaan obat.</summary>
internal static class MenuOMoreSpecs
{
    private static string Text(string expr) => $"COALESCE(NULLIF(CAST({expr} AS CHAR),''),'-')";

    private static ReportSpec Tb(string title, string column) =>
        MenuOSpecs.Count(title, Text("t." + column), "data_tb t", "", ReportPeriod.None,
            hint: "Semua data TB.03. Tabel agregat hanya-baca.");

    private static ReportSpec K3(string title, string table, string key, string name, string fk) =>
        MenuOSpecs.Count(title, $"COALESCE(x.{name},k.{fk})", $"k3rs_peristiwa k LEFT JOIN {table} x ON x.{key}=k.{fk}", "k.tgl_insiden", ReportPeriod.DateRange);

    private static ReportSpec Amount(string title, string table, string column, string date, ReportPeriod period) =>
        MenuOSpecs.Count(title, period == ReportPeriod.Year ? $"DATE_FORMAT({date},'%Y-%m')" : $"DATE_FORMAT({date},'%Y-%m-%d')",
            table, date, period, $"SUM({column})", order: "kategori",
            hint: "Total jumlah per periode. Tabel agregat hanya-baca.");

    private static ReportSpec Rate(string title, string cases, string days)
    {
        var hasDevice = days.Length > 0;
        var daysSum = hasDevice ? $"SUM(h.{days})" : "0";
        var rate = hasDevice ? $"IF(SUM(h.{days})>0,ROUND(SUM(h.{cases})/SUM(h.{days})*1000,2),0)" : "0";
        var sql = $"""
            SELECT COALESCE(bs.nm_bangsal,k.kd_bangsal,'-') AS ruang,SUM(h.{cases}) AS kasus,{daysSum} AS hari_alat,{rate} AS laju
            FROM data_HAIs h LEFT JOIN kamar k ON k.kd_kamar=h.kd_kamar LEFT JOIN bangsal bs ON bs.kd_bangsal=k.kd_bangsal
            WHERE h.tanggal BETWEEN @p1 AND @p2 AND COALESCE(bs.nm_bangsal,'') LIKE @p0
            GROUP BY ruang ORDER BY laju DESC,ruang
            """;
        var device = hasDevice ? days + " sebagai alat terkait" : "tanpa alat, hari alat 0";
        return new(title, sql,
            [("ruang", "Ruang"), ("kasus", "Kasus"), ("hari_alat", "Hari Alat"), ("laju", "Laju per 1000 Hari Alat")], ReportPeriod.DateRange,
            $"Laju = kasus / hari alat x 1000 ({device}). Pasangan alat adalah asumsi. Tabel hanya-baca.")
        { TotalColumns = ["kasus", "hari_alat"] };
    }

    public static void Add(Dictionary<string, ReportSpec> map)
    {
        const string Ikp = "insiden_keselamatan_pasien k LEFT JOIN insiden_keselamatan i ON i.kode_insiden=k.kode_insiden";
        MenuOSpecs.Series(map, ["btnGrafikKejadianIKPPerTahun", "btnGrafikKejadianIKPPerBulan", "btnGrafikKejadianIKPPerTanggal"], "Kejadian IKP", Ikp, "k.tgl_kejadian");
        map["btnGrafikKejadianIKPPerJenis"] = MenuOSpecs.Count("Kejadian IKP Per Jenis", Text("i.jenis_insiden"), Ikp, "k.tgl_kejadian", ReportPeriod.DateRange);
        map["btnGrafikKejadianIKPPerDampak"] = MenuOSpecs.Count("Kejadian IKP Per Dampak", Text("i.dampak"), Ikp, "k.tgl_kejadian", ReportPeriod.DateRange);

        map["btnGrafikTBLaporanPeriode"] = Tb("Periode Laporan TB", "id_periode_laporan");
        map["btnGrafikTBRujukan"] = Tb("Rujukan TB", "nama_rujukan");
        map["btnGrafikTBRiwayat"] = Tb("Riwayat TB", "klasifikasi_riwayat_pengobatan");
        map["btnGrafikTBTipeDiagnosis"] = Tb("Tipe Diagnosis TB", "tipe_diagnosis");
        map["btnGrafikTBSTatusHIV"] = Tb("Status HIV TB", "klasifikasi_status_hiv");
        map["btnGrafikTBSkoringAnak"] = Tb("Skoring Anak TB", "total_skoring_anak");
        map["btnGrafikTBKonfirmasiSkoring5"] = Tb("Konfirmasi Skoring 5 TB", "konfirmasiSkoring5");
        map["btnGrafikTBKonfirmasiSkoring6"] = Tb("Konfirmasi Skoring 6 TB", "konfirmasiSkoring6");
        map["btnGrafikTBSumberObat"] = Tb("Sumber Obat TB", "sumber_obat");
        map["btnGrafikTBHasilAkhirPengobatan"] = Tb("Hasil Akhir Pengobatan TB", "hasil_akhir_pengobatan");
        map["btnGrafikTBHasilTesHIV"] = Tb("Hasil Tes HIV TB", "hasil_tes_hiv");

        map["btnGrafikPemakaianAirPDAMPerTanggal"] = Amount("Pemakaian Air PDAM Per Tanggal", "kesling_pemakaian_air_pdam", "jumlahharian", "tanggal", ReportPeriod.DateRange);
        map["btnGrafikPemakaianAirPDAMPerBulan"] = Amount("Pemakaian Air PDAM Per Bulan", "kesling_pemakaian_air_pdam", "jumlahharian", "tanggal", ReportPeriod.Year);
        map["btnGrafikPemakaianAirTanahPerTanggal"] = Amount("Pemakaian Air Tanah Per Tanggal", "kesling_pemakaian_air_tanah", "jumlahharian", "tanggal", ReportPeriod.DateRange);
        map["btnGrafikPemakaianAirTanahPerBulan"] = Amount("Pemakaian Air Tanah Per Bulan", "kesling_pemakaian_air_tanah", "jumlahharian", "tanggal", ReportPeriod.Year);
        map["btnGrafikLimbahB3MedisPerTanggal"] = Amount("Limbah B3 Medis Per Tanggal", "kesling_limbah_b3medis", "jmllimbah", "tanggal", ReportPeriod.DateRange);
        map["btnGrafikLimbahB3MedisPerBulan"] = Amount("Limbah B3 Medis Per Bulan", "kesling_limbah_b3medis", "jmllimbah", "tanggal", ReportPeriod.Year);
        map["btnGrafikLimbahDomestikPerTanggal"] = Amount("Limbah Padat Domestik Per Tanggal", "kesling_limbah_domestik", "jumlahlimbah", "tanggal", ReportPeriod.DateRange);
        map["btnGrafikLimbahDomestikPerBulan"] = Amount("Limbah Padat Domestik Per Bulan", "kesling_limbah_domestik", "jumlahlimbah", "tanggal", ReportPeriod.Year);
        map["btnGrafikLimbahB3MedisCairPerTanggal"] = Amount("Limbah B3 Cair Per Tanggal", "kesling_limbah_b3medis_cair", "jmllimbah", "tanggal", ReportPeriod.DateRange);
        map["btnGrafikLimbahB3MedisCairPerBulan"] = Amount("Limbah B3 Cair Per Bulan", "kesling_limbah_b3medis_cair", "jmllimbah", "tanggal", ReportPeriod.Year);

        MenuOSpecs.Series(map, ["btnGrafikK3PerTahun", "btnGrafikK3PerBulan", "btnGrafikK3PerTanggal"], "K3", "k3rs_peristiwa k", "k.tgl_insiden");
        map["btnGrafikK3PerJenisCidera"] = K3("K3 Per Jenis Cidera", "k3rs_jenis_cidera", "kode_cidera", "jenis_cidera", "kode_cidera");
        map["btnGrafikK3PerPenyebab"] = K3("K3 Per Penyebab Kecelakaan", "k3rs_penyebab", "kode_penyebab", "penyebab_kecelakaan", "kode_penyebab");
        map["btnGrafikK3PerJenisLuka"] = K3("K3 Per Jenis Luka", "k3rs_jenis_luka", "kode_luka", "jenis_luka", "kode_luka");
        map["btnGrafikK3PerLokasiKejadian"] = K3("K3 Per Lokasi Kejadian", "k3rs_lokasi_kejadian", "kode_lokasi", "lokasi_kejadian", "kode_lokasi");
        map["btnGrafikK3PerDampakCidera"] = K3("K3 Per Dampak Cidera", "k3rs_dampak_cidera", "kode_dampak", "dampak_cidera", "kode_dampak");
        map["btnGrafikK3PerJenisPekerjaan"] = K3("K3 Per Jenis Pekerjaan", "k3rs_jenis_pekerjaan", "kode_pekerjaan", "jenis_pekerjaan", "kode_pekerjaan");
        map["btnGrafikK3PerBagianTubuh"] = K3("K3 Per Bagian Tubuh", "k3rs_bagian_tubuh", "kode_bagian", "bagian_tubuh", "kode_bagian");

        map["btnGrafikPengajuanAsetUrgensi"] = MenuOSpecs.Count("Pengajuan Aset Per Urgensi", Text("a.urgensi"), "pengajuan_inventaris a", "a.tanggal", ReportPeriod.DateRange);
        map["btnGrafikPengajuanAsetStatus"] = MenuOSpecs.Count("Pengajuan Aset Per Status", Text("a.status"), "pengajuan_inventaris a", "a.tanggal", ReportPeriod.DateRange);
        map["btnGrafikPengajuanAsetDepartemen"] = MenuOSpecs.Count("Pengajuan Aset Per Departemen", "COALESCE(dp.nama,g.departemen,'-')",
            "pengajuan_inventaris a LEFT JOIN pegawai g ON g.nik=a.nik LEFT JOIN departemen dp ON dp.dep_id=g.departemen", "a.tanggal", ReportPeriod.DateRange);

        map["btnGrafikHAIsPasienRuang"] = MenuOSpecs.Count("Pasien HAIs Per Ruang", "COALESCE(bs.nm_bangsal,k.kd_bangsal,'-')",
            "data_HAIs h LEFT JOIN kamar k ON k.kd_kamar=h.kd_kamar LEFT JOIN bangsal bs ON bs.kd_bangsal=k.kd_bangsal", "h.tanggal", ReportPeriod.DateRange, "COUNT(DISTINCT h.no_rawat)");
        map["btnGrafikHAIsPasienBulan"] = MenuOSpecs.Count("Pasien HAIs Per Bulan", "DATE_FORMAT(h.tanggal,'%Y-%m')", "data_HAIs h", "h.tanggal", ReportPeriod.Year,
            "COUNT(DISTINCT h.no_rawat)", order: "kategori");
        map["btnGrafikHAIsLajuVAP"] = Rate("Laju HAIs VAP Per Ruang", "VAP", "ETT");
        map["btnGrafikHAIsLajuIAD"] = Rate("Laju HAIs IAD Per Ruang", "IAD", "CVL");
        map["btnGrafikHAIsLajuPleb"] = Rate("Laju HAIs Plebitis Per Ruang", "PLEB", "IVL");
        map["btnGrafikHAIsLajuISK"] = Rate("Laju HAIs ISK Per Ruang", "ISK", "UC");
        map["btnGrafikHAIsLajuILO"] = Rate("Laju HAIs ILO Per Ruang", "ILO", "");
        map["btnGrafikHAIsLajuHAP"] = Rate("Laju HAIs HAP Per Ruang", "HAP", "ETT");

        map["btnGrafikPenerimaanObatPerBulan"] = Amount("Penerimaan Obat, Alkes & BHP Per Bulan", "pembelian", "total2", "tgl_beli", ReportPeriod.Year);
        MenuOSpecs.Series(map, ["btnGrafikHemodialisaPerTahun", "btnGrafikHemodialisaPerBulan", "btnGrafikHemodialisaPerTanggal"], "Hemodialisa", "hemodialisa hd", "hd.tanggal");
        MenuOSpecs.Series(map, ["btnGrafikPorsiDietPerTahun", "btnGrafikPorsiDietPerBulan", "btnGrafikPorsiDietPerTanggal"], "Porsi Diet", "detail_beri_diet df", "df.tanggal");
        map["btnGrafikPorsiDietPerRuang"] = MenuOSpecs.Count("Porsi Diet Per Ruang", "COALESCE(bs.nm_bangsal,k.kd_bangsal,'-')",
            "detail_beri_diet df LEFT JOIN kamar k ON k.kd_kamar=df.kd_kamar LEFT JOIN bangsal bs ON bs.kd_bangsal=k.kd_bangsal", "df.tanggal", ReportPeriod.DateRange);
        MenuOSpecs.Series(map, ["btnGrafikPerbaikanInventarisPerTahun", "btnGrafikPerbaikanInventarisPerBulan", "btnGrafikPerbaikanInventarisPerTanggal"],
            "Perbaikan Inventaris", "perbaikan_inventaris pi", "pi.tanggal");
        map["btnGrafikPerbaikanInventarisPerPelaksanaStatus"] = MenuOSpecs.Count("Perbaikan Inventaris Per Pelaksana & Status",
            "CONCAT(COALESCE(NULLIF(pi.pelaksana,''),'-'),' / ',COALESCE(NULLIF(pi.status,''),'-'))", "perbaikan_inventaris pi", "pi.tanggal", ReportPeriod.DateRange);
    }
}
