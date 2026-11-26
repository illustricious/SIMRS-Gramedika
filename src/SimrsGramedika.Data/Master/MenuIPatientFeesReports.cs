namespace SimrsGramedika.Data.Master;

/// <summary>Patient procedure fee reports; excludes supporting and operating services for now.</summary>
public static class MenuIPatientFeesReports
{
    private static ReportSpec Create(bool summary)
    {
        string Part(string table, string service, string medical) => $"""
            SELECT r.tgl_registrasi AS tanggal,r.no_rawat,r.no_rkm_medis,p.nm_pasien,
                   poli.nm_poli,j.png_jawab,r.status_bayar,'{service}' AS layanan,
                   COALESCE(SUM(t.material),0)+COALESCE(SUM(t.kso),0) AS sarana,
                   {medical} AS medis,COALESCE(SUM(t.menejemen),0) AS manajemen,
                   COALESCE(SUM(t.bhp),0) AS bhp,COALESCE(SUM(t.biaya_rawat),0) AS total
            FROM {table} t JOIN reg_periksa r ON r.no_rawat=t.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN poliklinik poli ON poli.kd_poli=r.kd_poli JOIN penjab j ON j.kd_pj=r.kd_pj
            WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
              AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
                   OR poli.nm_poli LIKE @p0 OR j.png_jawab LIKE @p0)
            GROUP BY r.tgl_registrasi,r.no_rawat,r.no_rkm_medis,p.nm_pasien,poli.nm_poli,j.png_jawab,r.status_bayar
            """;
        const string dr = "COALESCE(SUM(t.tarif_tindakandr),0)";
        const string pr = "COALESCE(SUM(t.tarif_tindakanpr),0)";
        var union = string.Join(" UNION ALL ", new[]
        {
            Part("rawat_jl_dr", "Ralan Dokter", dr), Part("rawat_jl_pr", "Ralan Paramedis", pr),
            Part("rawat_jl_drpr", "Ralan Dokter & Paramedis", $"{dr}+{pr}"),
            Part("rawat_inap_dr", "Ranap Dokter", dr), Part("rawat_inap_pr", "Ranap Paramedis", pr),
            Part("rawat_inap_drpr", "Ranap Dokter & Paramedis", $"{dr}+{pr}")
        });
        var sql = summary
            ? $"SELECT layanan,COUNT(DISTINCT no_rawat) AS jumlah,SUM(sarana) AS sarana,SUM(medis) AS medis,SUM(manajemen) AS manajemen,SUM(bhp) AS bhp,SUM(total) AS total FROM ({union}) q GROUP BY layanan ORDER BY layanan"
            : $"SELECT tanggal,no_rawat,no_rkm_medis,nm_pasien,nm_poli,png_jawab,status_bayar,layanan,sarana,medis,manajemen,bhp,total FROM ({union}) q ORDER BY tanggal,no_rawat,layanan";
        (string, string)[] columns = summary
            ? [("layanan", "Jenis Tindakan"), ("jumlah", "Kunjungan"), ("sarana", "Sarana/KSO"), ("medis", "Jasa Medis"),
               ("manajemen", "Manajemen"), ("bhp", "BHP"), ("total", "Biaya Tindakan")]
            : [("tanggal", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
               ("nm_pasien", "Pasien"), ("nm_poli", "Poli Registrasi"), ("png_jawab", "Cara Bayar"),
               ("status_bayar", "Status Bayar"), ("layanan", "Jenis Tindakan"), ("sarana", "Sarana/KSO"),
               ("medis", "Jasa Medis"), ("manajemen", "Manajemen"), ("bhp", "BHP"), ("total", "Biaya Tindakan")];
        return new ReportSpec(summary ? "Ringkasan Jasa Tindakan Pasien" : "Jasa Tindakan Pasien", sql, columns,
            ReportPeriod.DateRange, "Cakupan sementara: hanya tindakan ralan dan ranap, berdasarkan tanggal registrasi. Operasi, laboratorium, radiologi dan filter status bayar/jam belum termasuk; jumlah bukan total seluruh jasa pasien. Data belum diuji pada database.")
            { SearchWatermark = "No. rawat, No. RM, pasien, poli, atau cara bayar", TotalColumns = ["total"] };
    }

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnJasaTindakanPasien"] = Create(false),
        ["btnRingkasanJasaTindakanPasien"] = Create(true),
    };
}
