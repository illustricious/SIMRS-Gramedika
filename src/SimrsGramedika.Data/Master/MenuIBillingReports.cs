namespace SimrsGramedika.Data.Master;

public static class MenuIBillingReports
{
    private static ReportSpec Visits(bool inpatient, bool debt)
    {
        var title = (debt ? "Piutang " : "Pembayaran ") + (inpatient ? "Ranap" : "Ralan");
        var date = inpatient ? "k.tgl_keluar" : "r.tgl_registrasi";
        var room = inpatient ? "JOIN kamar_inap k ON k.no_rawat=r.no_rawat AND k.stts_pulang NOT IN ('-', 'Pindah Kamar') JOIN kamar km ON km.kd_kamar=k.kd_kamar JOIN bangsal bs ON bs.kd_bangsal=km.kd_bangsal" : "JOIN poliklinik poli ON poli.kd_poli=r.kd_poli";
        var unit = inpatient ? "CONCAT(k.kd_kamar, ' ', bs.nm_bangsal)" : "poli.nm_poli";
        var condition = inpatient ? "" : "AND r.status_lanjut='Ralan'";
        var hasDebt = debt ? "JOIN piutang_pasien pp ON pp.no_rawat=r.no_rawat" : "LEFT JOIN piutang_pasien pp ON pp.no_rawat=r.no_rawat";
        var debtFilter = debt ? "" : "AND pp.no_rawat IS NULL";
        var debtFields = debt
            ? "pp.status AS status_piutang, pp.totalpiutang, pp.uangmuka, COALESCE(pay.cicilan,0) AS cicilan, COALESCE(pay.diskon,0) AS diskon, COALESCE(pay.tidak_terbayar,0) AS tidak_terbayar, COALESCE(pp.totalpiutang,0)-COALESCE(pp.uangmuka,0)-COALESCE(pay.cicilan,0)-COALESCE(pay.diskon,0)-COALESCE(pay.tidak_terbayar,0) AS sisa,"
            : "";
        var statusList = inpatient
            ? "'Registrasi','Obat','Retur Obat','Resep Pulang','Ranap Dokter','Ranap Dokter Paramedis','Ranap Paramedis','Ralan Dokter','Ralan Dokter Paramedis','Ralan Paramedis','Laborat','Radiologi','Operasi','Potongan','Tambahan','Kamar','Harian','Service'"
            : "'Registrasi','Obat','Ralan Dokter','Ralan Dokter Paramedis','Ralan Paramedis','Laborat','Radiologi','Operasi','Tambahan','Potongan'";
        var sql = $"""
            SELECT {date} AS tanggal, r.no_rawat, r.no_rkm_medis, p.nm_pasien, j.png_jawab,
                   {unit} AS unit, {debtFields}
                   COALESCE(bi.total,0) AS total,
                   CASE WHEN COALESCE(bi.total,0)>0 THEN 'Sudah Bayar' ELSE 'Belum Bayar' END AS status_billing
            FROM reg_periksa r
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN penjab j ON j.kd_pj=r.kd_pj
            {room} {hasDebt}
            LEFT JOIN (SELECT no_rawat, SUM(totalbiaya) AS total FROM billing
                       WHERE status IN ({statusList}) GROUP BY no_rawat) bi ON bi.no_rawat=r.no_rawat
            {(debt ? "LEFT JOIN (SELECT no_rawat, SUM(besar_cicilan) AS cicilan, SUM(diskon_piutang) AS diskon, SUM(tidak_terbayar) AS tidak_terbayar FROM bayar_piutang GROUP BY no_rawat) pay ON pay.no_rawat=r.no_rawat" : "")}
            WHERE {date} BETWEEN @p1 AND @p2 {condition} {debtFilter}
              AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
                   OR j.png_jawab LIKE @p0 OR {unit} LIKE @p0)
            ORDER BY tanggal, r.no_rawat
            """;
        var columns = new List<(string, string)>
        {
            ("tanggal", inpatient ? "Tanggal Pulang" : "Tanggal Registrasi"), ("no_rawat", "No. Rawat"),
            ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"), ("unit", inpatient ? "Kamar/Bangsal" : "Poli"),
            ("total", "Total Billing")
        };
        if (debt) columns.AddRange([("status_piutang", "Status Piutang"), ("totalpiutang", "Total Piutang"),
            ("uangmuka", "Uang Muka"), ("cicilan", "Cicilan"), ("diskon", "Diskon"),
            ("tidak_terbayar", "Tidak Terbayar"), ("sisa", "Sisa")]);
        else columns.Add(("status_billing", "Status Billing"));
        return new ReportSpec(title, sql, columns.ToArray(), ReportPeriod.DateRange,
            "Ringkasan baca saja dari billing tercatat; rincian komponen, cetak dan pembukaan billing belum tersedia. " +
            (inpatient ? "Periode memakai tanggal pulang dan mengecualikan pindah kamar." : "Periode memakai tanggal registrasi.") +
            (debt ? " Sisa = total piutang dikurangi uang muka, cicilan, diskon dan tidak terbayar." : " Kunjungan dengan piutang dikecualikan; status tampilan mengikuti jumlah billing."))
            { SearchWatermark = "Pasien, No. rawat, No. RM, unit, atau cara bayar", TotalColumns = debt ? ["total", "sisa"] : ["total"] };
    }

    private static readonly ReportSpec Registration = new(
        "Rekap Biaya Registrasi",
        """
        SELECT r.tgl_registrasi AS tanggal, r.no_rawat, r.no_rkm_medis, p.nm_pasien,
               j.png_jawab, poli.nm_poli, d.nm_dokter, r.status_lanjut, r.biaya_reg AS nilai
        FROM reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
        JOIN penjab j ON j.kd_pj=r.kd_pj JOIN poliklinik poli ON poli.kd_poli=r.kd_poli
        JOIN dokter d ON d.kd_dokter=r.kd_dokter
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
               OR j.png_jawab LIKE @p0 OR poli.nm_poli LIKE @p0 OR d.nm_dokter LIKE @p0)
        ORDER BY tanggal, r.no_rawat
        """,
        [("tanggal", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"), ("nm_poli", "Poli"),
         ("nm_dokter", "Dokter"), ("status_lanjut", "Layanan"), ("nilai", "Biaya Registrasi")],
        ReportPeriod.DateRange, "Biaya registrasi berdasarkan tanggal registrasi. Diagnosis, alamat dan format cetak legacy belum tersedia.")
        { SearchWatermark = "Pasien, No. rawat, No. RM, cara bayar, poli, atau dokter", TotalColumns = ["nilai"] };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnRalanMasuk"] = Visits(false, false),
        ["btnRanapMasuk"] = Visits(true, false),
        ["btnPiutangRalan"] = Visits(false, true),
        ["btnPiutangRanap"] = Visits(true, true),
        ["btnRekapBiayaRegistrasi"] = Registration,
    };
}
