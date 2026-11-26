namespace SimrsGramedika.Data.Master;

public static partial class MenuJSpecs
{
    private static ReportSpec DentalActivity() => new(
        "RL 3.3 Gigi dan Mulut",
        """
        SELECT p.nm_poli, j.kd_jenis_prw, j.nm_perawatan, COUNT(*) AS jumlah
        FROM rawat_jl_dr a
        JOIN reg_periksa r ON r.no_rawat = a.no_rawat
        JOIN poliklinik p ON p.kd_poli = r.kd_poli
        JOIN jns_perawatan j ON j.kd_jenis_prw = a.kd_jenis_prw
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND LOWER(p.nm_poli) LIKE '%gigi%'
          AND (p.nm_poli LIKE @p0 OR j.kd_jenis_prw LIKE @p0 OR j.nm_perawatan LIKE @p0)
        GROUP BY p.kd_poli, p.nm_poli, j.kd_jenis_prw, j.nm_perawatan
        ORDER BY p.nm_poli, j.nm_perawatan, j.kd_jenis_prw
        """,
        [("nm_poli", "Poli"), ("kd_jenis_prw", "Kode"), ("nm_perawatan", "Tindakan"), ("jumlah", "Jumlah")],
        ReportPeriod.DateRange, "Rekap tindakan dokter pada poli yang namanya memuat kata Gigi.")
        { SearchWatermark = "Poli, kode, atau nama tindakan" };

    private static ReportSpec SurgeryActivity() => new(
        "RL 3.6 Pembedahan",
        """
        SELECT p.kode_paket, p.nm_perawatan, o.kategori, COUNT(*) AS jumlah
        FROM operasi o
        JOIN paket_operasi p ON p.kode_paket = o.kode_paket
        WHERE o.tgl_operasi BETWEEN @p1 AND @p2 AND p.kategori = 'Operasi'
          AND (p.kode_paket LIKE @p0 OR p.nm_perawatan LIKE @p0 OR o.kategori LIKE @p0)
        GROUP BY p.kode_paket, p.nm_perawatan, o.kategori
        ORDER BY p.nm_perawatan, p.kode_paket, o.kategori
        """,
        [("kode_paket", "Kode"), ("nm_perawatan", "Paket Operasi"), ("kategori", "Kategori"), ("jumlah", "Jumlah")],
        ReportPeriod.DateRange, "Jumlah tindakan per paket dan kategori berdasarkan tanggal operasi.")
        { SearchWatermark = "Paket atau kategori operasi" };

    private static ReportSpec AnnualRespiratoryScreening() => new(
        "Skrining Pernapasan Ralan Per Tahun",
        """
        SELECT DATE_FORMAT(x.tanggal, '%Y-%m') AS bulan, x.pernapasan, COUNT(*) AS jumlah
        FROM skrining_rawat_jalan x
        WHERE x.tanggal >= MAKEDATE(@p1, 1) AND x.tanggal < MAKEDATE(@p1 + 1, 1)
          AND x.pernapasan LIKE @p0
        GROUP BY bulan, x.pernapasan
        ORDER BY bulan, x.pernapasan
        """,
        [("bulan", "Bulan"), ("pernapasan", "Hasil Skrining"), ("jumlah", "Jumlah")],
        ReportPeriod.Year, "Jumlah skrining dengan hasil pernapasan tercatat, per bulan dan kategori hasil.")
        { SearchWatermark = "Kategori hasil pernapasan" };

    private static ReportSpec AnnualTreatmentRefusals() => new(
        "Laporan Tahunan Penolakan Anjuran Medis",
        """
        SELECT DATE_FORMAT(x.tanggal, '%Y-%m') AS bulan, r.status_lanjut, x.kode_penolakan,
               m.nama_penolakan, COUNT(*) AS jumlah
        FROM surat_penolakan_anjuran_medis x
        JOIN reg_periksa r ON r.no_rawat = x.no_rawat
        LEFT JOIN master_menolak_anjuran_medis m ON m.kode_penolakan = x.kode_penolakan
        WHERE x.tanggal >= MAKEDATE(@p1, 1) AND x.tanggal < MAKEDATE(@p1 + 1, 1)
          AND (r.status_lanjut LIKE @p0 OR x.kode_penolakan LIKE @p0 OR m.nama_penolakan LIKE @p0)
        GROUP BY bulan, r.status_lanjut, x.kode_penolakan, m.nama_penolakan
        ORDER BY bulan, r.status_lanjut, x.kode_penolakan
        """,
        [("bulan", "Bulan"), ("status_lanjut", "Layanan"), ("kode_penolakan", "Kode"),
         ("nama_penolakan", "Jenis Penolakan"), ("jumlah", "Jumlah Surat")],
        ReportPeriod.Year, "Rekap surat menurut bulan pencatatan, jenis penolakan, dan layanan.")
        { SearchWatermark = "Jenis penolakan, kode, atau layanan" };

    private static ReportSpec PhysicalExamsByDisease() => new(
        "Pemeriksaan Fisik Ralan Per Penyakit",
        """
        SELECT e.tgl_perawatan, e.jam_rawat, r.no_rawat, r.no_rkm_medis, p.nm_pasien,
               k.kd_penyakit, k.nm_penyakit, e.tinggi, e.berat, e.lingkar_perut, e.tensi
        FROM pemeriksaan_ralan e
        JOIN reg_periksa r ON r.no_rawat = e.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN diagnosa_pasien d ON d.no_rawat = r.no_rawat AND d.status = 'Ralan'
        JOIN penyakit k ON k.kd_penyakit = d.kd_penyakit
        WHERE e.tgl_perawatan BETWEEN @p1 AND @p2 AND r.stts <> 'Batal'
          AND (k.kd_penyakit LIKE @p0 OR k.nm_penyakit LIKE @p0)
        ORDER BY e.tgl_perawatan, e.jam_rawat, r.no_rawat, k.kd_penyakit
        """,
        [("tgl_perawatan", "Tanggal Pemeriksaan"), ("jam_rawat", "Jam"), ("no_rawat", "No. Rawat"),
         ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("kd_penyakit", "Kode ICD 10"),
         ("nm_penyakit", "Penyakit"), ("tinggi", "Tinggi"), ("berat", "Berat"),
         ("lingkar_perut", "Lingkar Perut"), ("tensi", "Tekanan Darah")],
        ReportPeriod.DateRange, "Daftar pemeriksaan fisik dan diagnosis rawat jalan. Pilih periode dan cari penyakit.")
        { SearchWatermark = "Kode atau nama penyakit" };
}
