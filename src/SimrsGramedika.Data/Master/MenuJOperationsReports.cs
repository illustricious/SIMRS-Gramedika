namespace SimrsGramedika.Data.Master;

public static partial class MenuJSpecs
{
    private static ReportSpec ClinicRegistrations() => new(
        "Registrasi Poli Per Tanggal",
        """
        SELECT r.tgl_registrasi AS tanggal, p.kd_poli, p.nm_poli,
               COUNT(CASE WHEN r.stts_daftar = 'Baru' THEN 1 END) AS baru,
               COUNT(CASE WHEN r.stts_daftar = 'Lama' THEN 1 END) AS lama,
               COUNT(CASE WHEN r.stts_daftar = 'Baru' AND r.stts = 'Batal' THEN 1 END) AS batal_baru,
               COUNT(CASE WHEN r.stts_daftar = 'Lama' AND r.stts = 'Batal' THEN 1 END) AS batal_lama,
               COUNT(CASE WHEN r.stts <> 'Batal' THEN 1 END) AS tidak_batal,
               COUNT(*) AS jumlah
        FROM reg_periksa r
        JOIN poliklinik p ON p.kd_poli = r.kd_poli
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND (p.kd_poli LIKE @p0 OR p.nm_poli LIKE @p0)
        GROUP BY r.tgl_registrasi, p.kd_poli, p.nm_poli
        ORDER BY tanggal, p.nm_poli, p.kd_poli
        """,
        [("tanggal", "Tanggal"), ("kd_poli", "Kode Poli"), ("nm_poli", "Poli"),
         ("baru", "Baru"), ("lama", "Lama"), ("batal_baru", "Batal Baru"), ("batal_lama", "Batal Lama"),
         ("tidak_batal", "Tidak Batal"), ("jumlah", "Total")],
        ReportPeriod.DateRange, "Total baru dan lama termasuk registrasi yang dibatalkan.")
        { SearchWatermark = "Kode atau nama poli" };

    private static ReportSpec MedicalRecordMovement() => new(
        "Rekap Mutasi Berkas",
        """
        SELECT r.tgl_registrasi, r.no_rawat, r.no_rkm_medis, p.nm_pasien, r.status_lanjut,
               m.status, m.dikirim, m.diterima, m.kembali, m.tidakada, m.ranap
        FROM mutasi_berkas m
        JOIN reg_periksa r ON r.no_rawat = m.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
               OR m.status LIKE @p0 OR r.status_lanjut LIKE @p0)
        ORDER BY r.tgl_registrasi DESC, r.jam_reg DESC, r.no_rawat
        """,
        [("tgl_registrasi", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("status_lanjut", "Layanan"), ("status", "Status Berkas"),
         ("dikirim", "Dikirim"), ("diterima", "Diterima"), ("kembali", "Kembali"),
         ("tidakada", "Tidak Ada"), ("ranap", "Masuk Ranap")],
        ReportPeriod.DateRange, "Riwayat pencatatan mutasi berkas menurut tanggal registrasi.")
        { SearchWatermark = "Pasien, nomor kunjungan, atau status" };

    private static ReportSpec MedicalRecordPreparation() => new(
        "Lama Penyiapan RM",
        """
        SELECT r.no_rawat, r.no_rkm_medis, p.nm_pasien, poli.nm_poli, d.nm_dokter,
               TIMESTAMP(r.tgl_registrasi, r.jam_reg) AS registrasi, m.dikirim,
               TIMESTAMPDIFF(SECOND, TIMESTAMP(r.tgl_registrasi, r.jam_reg), m.dikirim) AS durasi
        FROM mutasi_berkas m
        JOIN reg_periksa r ON r.no_rawat = m.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN poliklinik poli ON poli.kd_poli = r.kd_poli
        JOIN dokter d ON d.kd_dokter = r.kd_dokter
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND m.dikirim >= TIMESTAMP(r.tgl_registrasi, r.jam_reg)
          AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
               OR poli.nm_poli LIKE @p0 OR d.nm_dokter LIKE @p0)
        ORDER BY registrasi, r.no_rawat
        """,
        [("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
         ("nm_poli", "Poli"), ("nm_dokter", "Dokter"), ("registrasi", "Registrasi"),
         ("dikirim", "Berkas Dikirim"), ("durasi", "Durasi (Detik)")],
        ReportPeriod.DateRange, "Durasi registrasi sampai berkas dikirim, termasuk bila pengiriman melewati tengah malam.")
        { SearchWatermark = "Pasien, nomor kunjungan, poli, atau dokter" };

    private static ReportSpec RadiologyDose() => new(
        "Dosis Radiologi",
        """
        SELECT e.tgl_periksa, e.jam, e.no_rawat, r.no_rkm_medis, p.nm_pasien,
               j.nm_perawatan, e.status, e.proyeksi, e.kV, e.mAS, e.FFD, e.BSF,
               e.inak, e.jml_penyinaran, e.dosis
        FROM periksa_radiologi e
        JOIN reg_periksa r ON r.no_rawat = e.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN jns_perawatan_radiologi j ON j.kd_jenis_prw = e.kd_jenis_prw
        WHERE e.tgl_periksa BETWEEN @p1 AND @p2
          AND (e.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
               OR j.nm_perawatan LIKE @p0)
        ORDER BY e.tgl_periksa, e.jam, e.no_rawat, e.kd_jenis_prw
        """,
        [("tgl_periksa", "Tanggal"), ("jam", "Jam"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("nm_perawatan", "Tindakan"), ("status", "Layanan"),
         ("proyeksi", "Proyeksi"), ("kV", "kV"), ("mAS", "mAs"), ("FFD", "FFD"), ("BSF", "BSF"),
         ("inak", "INAK"), ("jml_penyinaran", "Jumlah Penyinaran"), ("dosis", "Dosis Tercatat")],
        ReportPeriod.DateRange, "Parameter dan dosis sebagaimana dicatat pada pemeriksaan radiologi.")
        { SearchWatermark = "Pasien, nomor kunjungan, atau tindakan" };

    private static ReportSpec DietCounts(string title, string grouping, string label, ReportPeriod period) => new(
        title,
        $"""
        SELECT x.tanggal, {grouping} AS kelompok, COUNT(*) AS jumlah
        FROM detail_beri_diet x
        JOIN diet d ON d.kd_diet = x.kd_diet
        JOIN kamar k ON k.kd_kamar = x.kd_kamar
        LEFT JOIN bangsal b ON b.kd_bangsal = k.kd_bangsal
        WHERE x.tanggal BETWEEN @p1 AND @p2 AND COALESCE({grouping}, '') LIKE @p0
        GROUP BY x.tanggal, {grouping}
        ORDER BY x.tanggal, kelompok
        """,
        [("tanggal", "Tanggal"), ("kelompok", label), ("jumlah", "Jumlah Porsi")],
        period, "Satu porsi untuk setiap baris pemberian diet yang tercatat.")
        { SearchWatermark = $"Cari {label.ToLowerInvariant()}" };

    private static ReportSpec DietLeftovers() => new(
        "Sisa Diet Pasien",
        """
        SELECT s.tanggal, s.waktu, s.no_rawat, r.no_rkm_medis, p.nm_pasien, s.kd_kamar,
               s.karbohidrat, s.hewani, s.nabati, s.sayur, s.buah
        FROM sisa_diet_pasien s
        JOIN reg_periksa r ON r.no_rawat = s.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        WHERE s.tanggal BETWEEN @p1 AND @p2
          AND (s.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0 OR s.kd_kamar LIKE @p0)
        ORDER BY s.tanggal, s.waktu, s.no_rawat, s.kd_kamar
        """,
        [("tanggal", "Tanggal"), ("waktu", "Waktu Makan"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("kd_kamar", "Kamar"), ("karbohidrat", "Karbohidrat (%)"),
         ("hewani", "Hewani (%)"), ("nabati", "Nabati (%)"), ("sayur", "Sayur (%)"), ("buah", "Buah (%)")],
        ReportPeriod.DateRange, "Daftar persentase sisa makanan yang sudah dicatat.")
        { SearchWatermark = "Pasien, nomor kunjungan, atau kamar" };

    private static ReportSpec AnnualActivity(string title, string source, string date, string lookup,
        string sourceKey, string lookupKey, string name, string label) => new(
        title,
        $"""
        SELECT DATE_FORMAT(x.{date}, '%Y-%m') AS bulan, l.{lookupKey} AS kode,
               l.{name} AS nama, COUNT(*) AS jumlah
        FROM {source} x
        JOIN {lookup} l ON l.{lookupKey} = x.{sourceKey}
        WHERE x.{date} >= MAKEDATE(@p1, 1) AND x.{date} < MAKEDATE(@p1 + 1, 1)
          AND (l.{lookupKey} LIKE @p0 OR l.{name} LIKE @p0)
        GROUP BY bulan, l.{lookupKey}, l.{name}
        ORDER BY bulan, l.{name}, l.{lookupKey}
        """,
        [("bulan", "Bulan"), ("kode", "Kode"), ("nama", label), ("jumlah", "Jumlah Tindakan")],
        ReportPeriod.Year, "Jumlah tindakan per bulan pada tahun yang dipilih.")
        { SearchWatermark = "Kode atau nama" };

    private static ReportSpec AnnualLabActivity() => new(
        "Rekap Lab Per Tahun",
        """
        SELECT DATE_FORMAT(e.tgl_periksa, '%Y-%m') AS bulan, j.kd_jenis_prw AS kode,
               j.nm_perawatan AS tindakan, 'Tindakan' AS jenis, '' AS parameter, COUNT(*) AS jumlah
        FROM periksa_lab e
        JOIN jns_perawatan_lab j ON j.kd_jenis_prw = e.kd_jenis_prw
        WHERE e.tgl_periksa >= MAKEDATE(@p1, 1) AND e.tgl_periksa < MAKEDATE(@p1 + 1, 1)
          AND (j.kd_jenis_prw LIKE @p0 OR j.nm_perawatan LIKE @p0)
        GROUP BY bulan, j.kd_jenis_prw, j.nm_perawatan
        UNION ALL
        SELECT DATE_FORMAT(e.tgl_periksa, '%Y-%m') AS bulan, j.kd_jenis_prw AS kode,
               j.nm_perawatan AS tindakan, 'Parameter' AS jenis, t.Pemeriksaan AS parameter, COUNT(*) AS jumlah
        FROM detail_periksa_lab e
        JOIN template_laboratorium t ON t.id_template = e.id_template
        JOIN jns_perawatan_lab j ON j.kd_jenis_prw = t.kd_jenis_prw
        WHERE e.tgl_periksa >= MAKEDATE(@p1, 1) AND e.tgl_periksa < MAKEDATE(@p1 + 1, 1)
          AND (j.kd_jenis_prw LIKE @p0 OR j.nm_perawatan LIKE @p0 OR t.Pemeriksaan LIKE @p0)
        GROUP BY bulan, j.kd_jenis_prw, j.nm_perawatan, t.id_template, t.Pemeriksaan
        ORDER BY bulan, kode, jenis DESC, parameter
        """,
        [("bulan", "Bulan"), ("kode", "Kode"), ("tindakan", "Tindakan"), ("jenis", "Jenis Hitungan"),
         ("parameter", "Parameter Lab"), ("jumlah", "Jumlah")],
        ReportPeriod.Year, "Jumlah tindakan lab dan parameter hasil per bulan ditampilkan sebagai hitungan terpisah.")
        { SearchWatermark = "Kode, tindakan, atau parameter lab" };
}
