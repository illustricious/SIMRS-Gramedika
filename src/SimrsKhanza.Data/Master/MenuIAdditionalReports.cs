namespace SimrsKhanza.Data.Master;

/// <summary>Menu I report screens, with scope described in each report's hint.</summary>
public static class MenuIAdditionalReports
{
    private static ReportSpec ProcedureShare(string title, string column, bool summary)
    {
        string Part(string table, bool inpatient) => $"""
            SELECT {(inpatient ? "t.tgl_perawatan" : "r.tgl_registrasi")} AS tanggal,
                   r.no_rawat, p.nm_pasien, j.png_jawab,
                   '{(inpatient ? "Ranap" : "Ralan")} {table[(table.LastIndexOf('_') + 1)..].ToUpperInvariant()}' AS layanan,
                   k.nm_perawatan, t.{column} AS nilai
            FROM {table} t
            JOIN reg_periksa r ON r.no_rawat=t.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN penjab j ON j.kd_pj=r.kd_pj
            JOIN {(inpatient ? "jns_perawatan_inap" : "jns_perawatan")} k ON k.kd_jenis_prw=t.kd_jenis_prw
            WHERE {(inpatient ? "t.tgl_perawatan" : "r.tgl_registrasi")} BETWEEN @p1 AND @p2
              AND t.{column}>0
              AND (j.png_jawab LIKE @p0 OR j.kd_pj LIKE @p0 OR k.nm_perawatan LIKE @p0 OR p.nm_pasien LIKE @p0)
            """;

        var parts = new[] { Part("rawat_jl_dr", false), Part("rawat_jl_pr", false), Part("rawat_jl_drpr", false),
                            Part("rawat_inap_dr", true), Part("rawat_inap_pr", true), Part("rawat_inap_drpr", true) };
        var union = string.Join(" UNION ALL ", parts);
        var sql = summary
            ? $"SELECT png_jawab, layanan, nm_perawatan, COUNT(*) AS jumlah, SUM(nilai) AS nilai FROM ({union}) x GROUP BY png_jawab, layanan, nm_perawatan ORDER BY png_jawab, layanan, nm_perawatan"
            : $"SELECT tanggal, no_rawat, nm_pasien, png_jawab, layanan, nm_perawatan, nilai FROM ({union}) x ORDER BY tanggal, no_rawat, layanan, nm_perawatan";
        (string, string)[] cols = summary
            ? [("png_jawab", "Cara Bayar"), ("layanan", "Layanan"), ("nm_perawatan", "Tindakan"), ("jumlah", "Jumlah"), ("nilai", "Nilai")]
            : [("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"), ("layanan", "Layanan"), ("nm_perawatan", "Tindakan"), ("nilai", "Nilai")];
        return new ReportSpec(title, sql, cols, ReportPeriod.DateRange,
            "Cakupan sementara: tindakan rawat jalan dan rawat inap saja. Lab, radiologi dan operasi belum termasuk; total bukan total seluruh jasa Khanza. Ralan menggunakan tanggal registrasi, ranap tanggal tindakan.")
            { SearchWatermark = "Cara bayar, tindakan, atau pasien", TotalColumns = ["nilai"] };
    }

    private static ReportSpec FeeExamination = new(
        "Fee Periksa Ralan",
        """
        SELECT r.tgl_registrasi AS tanggal, t.no_rawat, p.nm_pasien, d.nm_dokter, j.png_jawab,
               k.nm_perawatan, t.bhp, t.material, t.biaya_rawat, t.tarif_tindakandr AS nilai
        FROM rawat_jl_dr t JOIN reg_periksa r ON r.no_rawat=t.no_rawat
        JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis JOIN penjab j ON j.kd_pj=r.kd_pj
        JOIN dokter d ON d.kd_dokter=t.kd_dokter JOIN jns_perawatan k ON k.kd_jenis_prw=t.kd_jenis_prw
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND t.tarif_tindakandr>0
          AND k.nm_perawatan LIKE '%riksa%'
          AND (d.kd_dokter LIKE @p0 OR d.nm_dokter LIKE @p0 OR p.nm_pasien LIKE @p0)
        ORDER BY d.nm_dokter, tanggal, t.no_rawat
        """,
        [("tanggal", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"), ("nm_pasien", "Pasien"),
         ("nm_dokter", "Dokter"), ("png_jawab", "Cara Bayar"), ("nm_perawatan", "Tindakan"),
         ("bhp", "BHP"), ("material", "Sarana"), ("biaya_rawat", "Biaya"), ("nilai", "Jasa Dokter")],
        ReportPeriod.DateRange, "Mengikuti tindakan dokter rawat jalan dengan nama mengandung 'riksa' dan jasa dokter positif.")
        { SearchWatermark = "Kode/nama dokter atau pasien", TotalColumns = ["nilai"] };

    private static ReportSpec ReferralRadiology = new(
        "Fee Rujukan Rontgen",
        """
        SELECT x.tgl_periksa AS tanggal, x.no_rawat, p.nm_pasien, d.nm_dokter, j.png_jawab,
               k.nm_perawatan, x.tarif_perujuk AS nilai
        FROM periksa_radiologi x JOIN reg_periksa r ON r.no_rawat=x.no_rawat
        JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis JOIN penjab j ON j.kd_pj=r.kd_pj
        JOIN dokter d ON d.kd_dokter=x.dokter_perujuk
        JOIN jns_perawatan_radiologi k ON k.kd_jenis_prw=x.kd_jenis_prw
        WHERE x.tgl_periksa BETWEEN @p1 AND @p2
          AND (k.nm_perawatan LIKE '%rontgen%' OR k.nm_perawatan LIKE '%usg%' OR k.nm_perawatan LIKE '%sono%')
          AND (d.kd_dokter LIKE @p0 OR d.nm_dokter LIKE @p0 OR p.nm_pasien LIKE @p0)
        ORDER BY d.nm_dokter, tanggal, x.no_rawat
        """,
        [("tanggal", "Tanggal Periksa"), ("no_rawat", "No. Rawat"), ("nm_pasien", "Pasien"),
         ("nm_dokter", "Dokter Perujuk"), ("png_jawab", "Cara Bayar"), ("nm_perawatan", "Pemeriksaan"), ("nilai", "Fee")],
        ReportPeriod.DateRange, "Pemeriksaan radiologi bernama rontgen, USG atau sono; berdasarkan tanggal pemeriksaan.")
        { SearchWatermark = "Kode/nama dokter perujuk atau pasien", TotalColumns = ["nilai"] };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnRHJasaSarana"] = ProcedureShare("Harian Jasa Sarana", "material", false),
        ["btnRBJasaSarana"] = ProcedureShare("Bulanan Jasa Sarana", "material", true),
        ["btnRHKSO"] = ProcedureShare("Harian KSO", "kso", false),
        ["btnRBKSO"] = ProcedureShare("Bulanan KSO", "kso", true),
        ["btnRHMenejemen"] = ProcedureShare("Harian Menejemen", "menejemen", false),
        ["btnRBMenejemen"] = ProcedureShare("Bulanan Menejemen", "menejemen", true),
        ["btnRHPaketBHP"] = ProcedureShare("Harian BHP Medis/Paket Obat", "bhp", false),
        ["btnRBPaketBHP"] = ProcedureShare("Bulanan BHP Medis/Paket Obat", "bhp", true),
        ["btnfee_ralan"] = FeeExamination,
        ["btnfee_rujukan_rontgen"] = ReferralRadiology,
    };
}
