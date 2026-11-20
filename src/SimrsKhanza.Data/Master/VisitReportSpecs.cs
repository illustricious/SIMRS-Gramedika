namespace SimrsKhanza.Data.Master;

public static class VisitReportSpecs
{
    public static readonly ReportSpec HourlyArrivals = new(
        "Kedatangan Pasien Per Jam",
        """
        SELECT r.tgl_registrasi AS tanggal,
               CONCAT(LPAD(HOUR(r.jam_reg), 2, '0'), ':00') AS jam,
               COUNT(*) AS jumlah
        FROM reg_periksa r
        JOIN poliklinik p ON p.kd_poli = r.kd_poli
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND r.jam_reg >= '00:00:00' AND r.jam_reg < '24:00:00'
          AND (p.kd_poli LIKE @p0 OR p.nm_poli LIKE @p0)
        GROUP BY r.tgl_registrasi, jam
        ORDER BY tanggal, jam
        """,
        [("tanggal", "Tanggal"), ("jam", "Jam Mulai"), ("jumlah", "Registrasi")],
        ReportPeriod.DateRange,
        "Jumlah registrasi per tanggal dan jam registrasi, termasuk yang kemudian dibatalkan.")
        { SearchWatermark = "Kode atau nama poli" };

    public static readonly ReportSpec DailyClinicCensus = new(
        "Sensus Harian Poli",
        """
        SELECT r.tgl_registrasi AS tanggal, p.kd_poli, p.nm_poli,
               COUNT(CASE WHEN r.stts_daftar = 'Baru' THEN 1 END) AS baru,
               COUNT(CASE WHEN r.stts_daftar = 'Lama' THEN 1 END) AS lama,
               COUNT(*) AS jumlah
        FROM reg_periksa r
        JOIN poliklinik p ON p.kd_poli = r.kd_poli
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND r.stts <> 'Batal'
          AND (p.kd_poli LIKE @p0 OR p.nm_poli LIKE @p0)
        GROUP BY r.tgl_registrasi, p.kd_poli, p.nm_poli
        ORDER BY tanggal, p.nm_poli, p.kd_poli
        """,
        [("tanggal", "Tanggal"), ("kd_poli", "Kode Poli"), ("nm_poli", "Poli"),
         ("baru", "Baru"), ("lama", "Lama"), ("jumlah", "Jumlah")],
        ReportPeriod.DateRange,
        "Rekap registrasi per tanggal dan poli. Registrasi batal dikeluarkan dari sensus.")
        { SearchWatermark = "Kode atau nama poli" };

    public static readonly ReportSpec OutpatientVisits = new(
        "Kunjungan Ralan",
        """
        SELECT r.tgl_registrasi AS tanggal, r.no_rawat, r.no_rkm_medis, p.nm_pasien,
               poli.nm_poli, d.nm_dokter, j.png_jawab, r.stts_daftar, r.stts
        FROM reg_periksa r
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN poliklinik poli ON poli.kd_poli = r.kd_poli
        JOIN dokter d ON d.kd_dokter = r.kd_dokter
        JOIN penjab j ON j.kd_pj = r.kd_pj
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND r.status_lanjut = 'Ralan' AND r.stts <> 'Batal'
          AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0
               OR p.nm_pasien LIKE @p0 OR poli.nm_poli LIKE @p0)
        ORDER BY r.tgl_registrasi, r.jam_reg, r.no_rawat
        """,
        [("tanggal", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("nm_poli", "Poli"), ("nm_dokter", "Dokter"),
         ("png_jawab", "Cara Bayar"), ("stts_daftar", "Baru/Lama"), ("stts", "Status")],
        ReportPeriod.DateRange,
        "Daftar kunjungan rawat jalan menurut tanggal registrasi. Registrasi batal dikeluarkan.")
        { SearchWatermark = "Pasien, No. RM, No. Rawat, atau poli" };

    public static readonly ReportSpec InpatientVisits = new(
        "Kunjungan Ranap",
        """
        SELECT r.tgl_registrasi AS tanggal, r.no_rawat, r.no_rkm_medis, p.nm_pasien,
               k.tgl_masuk, k.kd_kamar, b.nm_bangsal, j.png_jawab, r.stts_daftar, k.stts_pulang
        FROM reg_periksa r
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN kamar_inap k ON k.no_rawat = r.no_rawat AND k.stts_pulang <> 'Pindah Kamar'
        JOIN kamar km ON km.kd_kamar = k.kd_kamar
        JOIN bangsal b ON b.kd_bangsal = km.kd_bangsal
        JOIN penjab j ON j.kd_pj = r.kd_pj
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND r.status_lanjut = 'Ranap' AND r.stts <> 'Batal'
          AND NOT EXISTS (
              SELECT 1
              FROM kamar_inap newer
              WHERE newer.no_rawat = k.no_rawat AND newer.stts_pulang <> 'Pindah Kamar'
                AND (newer.tgl_masuk, newer.jam_masuk) > (k.tgl_masuk, k.jam_masuk)
          )
          AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0
               OR p.nm_pasien LIKE @p0 OR b.nm_bangsal LIKE @p0)
        ORDER BY r.tgl_registrasi, r.jam_reg, r.no_rawat
        """,
        [("tanggal", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("tgl_masuk", "Tgl. Masuk Kamar"), ("kd_kamar", "Kamar"),
         ("nm_bangsal", "Bangsal"), ("png_jawab", "Cara Bayar"), ("stts_daftar", "Baru/Lama"),
         ("stts_pulang", "Status Pulang")],
        ReportPeriod.DateRange,
        "Daftar kunjungan menurut tanggal registrasi, dengan kamar terakhir per nomor rawat. Registrasi batal dikeluarkan.")
        { SearchWatermark = "Pasien, No. RM, No. Rawat, atau bangsal" };

    public static readonly ReportSpec MonthlyVisitSummary = new(
        "Rekap Kunjungan",
        """
        SELECT DATE_FORMAT(r.tgl_registrasi, '%Y-%m') AS bulan,
               r.status_lanjut AS layanan, r.stts_daftar AS status_daftar,
               COUNT(CASE WHEN p.jk = 'L' THEN 1 END) AS laki,
               COUNT(CASE WHEN p.jk = 'P' THEN 1 END) AS perempuan,
               COUNT(*) AS jumlah,
               COUNT(CASE WHEN LOWER(poli.nm_poli) LIKE '%jiwa%' AND p.jk = 'L' THEN 1 END) AS jiwa_laki,
               COUNT(CASE WHEN LOWER(poli.nm_poli) LIKE '%jiwa%' AND p.jk = 'P' THEN 1 END) AS jiwa_perempuan
        FROM reg_periksa r
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        LEFT JOIN poliklinik poli ON poli.kd_poli = r.kd_poli
        WHERE r.tgl_registrasi >= MAKEDATE(@p1, 1)
          AND r.tgl_registrasi < MAKEDATE(@p1 + 1, 1)
          AND r.stts <> 'Batal' AND r.stts_daftar IN ('Baru', 'Lama')
          AND r.status_lanjut LIKE @p0
        GROUP BY bulan, r.status_lanjut, r.stts_daftar
        ORDER BY bulan, layanan, status_daftar
        """,
        [("bulan", "Bulan"), ("layanan", "Layanan"), ("status_daftar", "Baru/Lama"),
         ("laki", "Laki-laki"), ("perempuan", "Perempuan"), ("jumlah", "Jumlah"),
         ("jiwa_laki", "Poli Jiwa (L)"), ("jiwa_perempuan", "Poli Jiwa (P)")],
        ReportPeriod.Year,
        "Rekap bulanan registrasi per layanan. Kolom Poli Jiwa menghitung bagian kunjungan di poli yang namanya memuat kata Jiwa.")
        { SearchWatermark = "Layanan (Ralan atau Ranap)" };
}
