namespace SimrsKhanza.Data.Master;

public static partial class MenuJSpecs
{
    private static ReportSpec EmergencyActivity() => new(
        "RL 3.2 Rawat Darurat",
        """
        SELECT p.kd_poli, p.nm_poli,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM rujuk_masuk a WHERE a.no_rawat = r.no_rawat) THEN 1 END) AS rujukan,
               COUNT(CASE WHEN NOT EXISTS (SELECT 1 FROM rujuk_masuk a WHERE a.no_rawat = r.no_rawat) THEN 1 END) AS nonrujukan,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM kamar_inap a WHERE a.no_rawat = r.no_rawat) THEN 1 END) AS dirawat,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM rujuk a WHERE a.no_rawat = r.no_rawat) THEN 1 END) AS dirujuk,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM kamar_inap a WHERE a.no_rawat = r.no_rawat
                   AND a.stts_pulang NOT IN ('-', 'Pindah Kamar', 'Rujuk', 'Meninggal', '+')) THEN 1 END) AS pulang,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM kamar_inap a WHERE a.no_rawat = r.no_rawat
                   AND a.stts_pulang IN ('Meninggal', '+')) THEN 1 END) AS meninggal,
               COUNT(*) AS jumlah
        FROM reg_periksa r
        JOIN poliklinik p ON p.kd_poli = r.kd_poli
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.stts <> 'Batal'
          AND (p.kd_poli IN ('IGD', 'IGDK') OR UPPER(p.nm_poli) LIKE '%IGD%'
               OR UPPER(p.nm_poli) LIKE '%UGD%' OR LOWER(p.nm_poli) LIKE '%gawat darurat%')
          AND (p.kd_poli LIKE @p0 OR p.nm_poli LIKE @p0)
        GROUP BY p.kd_poli, p.nm_poli
        ORDER BY p.nm_poli, p.kd_poli
        """,
        [("kd_poli", "Kode"), ("nm_poli", "Unit Darurat"), ("rujukan", "Rujukan Masuk"),
         ("nonrujukan", "Nonrujukan"), ("dirawat", "Dirawat"), ("dirujuk", "Dirujuk Keluar"),
         ("pulang", "Pulang dari Ranap"), ("meninggal", "Meninggal di Ranap"), ("jumlah", "Kunjungan")],
        ReportPeriod.DateRange, "Rekap registrasi unit darurat dan hasil yang tercatat pada rujukan dan rawat inap.")
        { SearchWatermark = "Kode atau nama unit darurat" };

    private static ReportSpec ObstetricActivity() => new(
        "RL 3.4 Kebidanan",
        """
        SELECT p.kode_paket, p.nm_perawatan, COUNT(*) AS jumlah,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM rujuk_masuk r WHERE r.no_rawat = o.no_rawat) THEN 1 END) AS rujukan,
               COUNT(CASE WHEN NOT EXISTS (SELECT 1 FROM rujuk_masuk r WHERE r.no_rawat = o.no_rawat) THEN 1 END) AS nonrujukan,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM rujuk_masuk r WHERE r.no_rawat = o.no_rawat
                   AND (LOWER(r.perujuk) LIKE '%rs%' OR LOWER(r.perujuk) LIKE '%rumah sakit%')) THEN 1 END) AS rumah_sakit,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM rujuk_masuk r WHERE r.no_rawat = o.no_rawat
                   AND (LOWER(r.perujuk) LIKE '%bidan%' OR LOWER(r.perujuk) LIKE '%amd.keb%')) THEN 1 END) AS bidan,
               COUNT(CASE WHEN EXISTS (SELECT 1 FROM rujuk r WHERE r.no_rawat = o.no_rawat) THEN 1 END) AS dirujuk
        FROM operasi o
        JOIN paket_operasi p ON p.kode_paket = o.kode_paket
        WHERE o.tgl_operasi BETWEEN @p1 AND @p2 AND p.kategori = 'Kebidanan'
          AND (p.kode_paket LIKE @p0 OR p.nm_perawatan LIKE @p0)
        GROUP BY p.kode_paket, p.nm_perawatan
        ORDER BY p.nm_perawatan, p.kode_paket
        """,
        [("kode_paket", "Kode"), ("nm_perawatan", "Tindakan Kebidanan"), ("jumlah", "Jumlah"),
         ("rujukan", "Rujukan Masuk"), ("nonrujukan", "Nonrujukan"), ("rumah_sakit", "Rujukan RS"),
         ("bidan", "Rujukan Bidan"), ("dirujuk", "Dirujuk Keluar")],
        ReportPeriod.DateRange, "Jumlah tindakan paket kebidanan dan status rujukan pada kunjungan.")
        { SearchWatermark = "Kode atau nama tindakan" };

    private static ReportSpec AnnualRegistrations(string title, bool emergency)
    {
        var clinicFilter = emergency ? "r.kd_poli = 'IGDK'" : "r.kd_poli <> 'IGDK'";
        return new(title,
            $"""
            SELECT DATE_FORMAT(r.tgl_registrasi, '%Y-%m') AS bulan, p.kd_poli, p.nm_poli, j.png_jawab,
                   COUNT(CASE WHEN r.stts_daftar = 'Baru' THEN 1 END) AS baru,
                   COUNT(CASE WHEN r.stts_daftar = 'Lama' THEN 1 END) AS lama,
                   COUNT(CASE WHEN r.status_lanjut = 'Ranap' THEN 1 END) AS lanjut_ranap,
                   COUNT(*) AS jumlah
            FROM reg_periksa r
            JOIN poliklinik p ON p.kd_poli = r.kd_poli
            JOIN penjab j ON j.kd_pj = r.kd_pj
            WHERE r.tgl_registrasi >= MAKEDATE(@p1, 1) AND r.tgl_registrasi < MAKEDATE(@p1 + 1, 1)
              AND {clinicFilter} AND r.stts <> 'Batal'
              AND (p.kd_poli LIKE @p0 OR p.nm_poli LIKE @p0 OR j.png_jawab LIKE @p0)
            GROUP BY bulan, p.kd_poli, p.nm_poli, j.kd_pj, j.png_jawab
            ORDER BY bulan, p.nm_poli, p.kd_poli, j.png_jawab
            """,
            [("bulan", "Bulan"), ("kd_poli", "Kode Poli"), ("nm_poli", "Poli"), ("png_jawab", "Cara Bayar"),
             ("baru", "Baru"), ("lama", "Lama"), ("lanjut_ranap", "Lanjut Ranap"), ("jumlah", "Jumlah")],
            ReportPeriod.Year, "Rekap bulanan menurut poli dan cara bayar. Pemisahan IRJ dan IGD mengikuti kode IGDK pada Khanza.")
            { SearchWatermark = "Poli atau cara bayar" };
    }

    private static ReportSpec TuberculosisRegister() => new(
        "Data TB",
        """
        SELECT x.tanggal_buat_laporan, x.id_tb_03, x.id_periode_laporan, x.no_rawat, r.no_rkm_medis,
               p.nm_pasien, x.tipe_diagnosis, x.klasifikasi_lokasi_anatomi,
               x.klasifikasi_riwayat_pengobatan, x.tanggal_mulai_pengobatan
        FROM data_tb x
        JOIN reg_periksa r ON r.no_rawat = x.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        WHERE x.tanggal_buat_laporan BETWEEN @p1 AND @p2
          AND (x.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0 OR x.id_tb_03 LIKE @p0)
        ORDER BY x.tanggal_buat_laporan, x.no_rawat
        """,
        [("tanggal_buat_laporan", "Tanggal Laporan"), ("id_tb_03", "ID TB 03"), ("id_periode_laporan", "Periode"),
         ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
         ("tipe_diagnosis", "Tipe Diagnosis"), ("klasifikasi_lokasi_anatomi", "Lokasi"),
         ("klasifikasi_riwayat_pengobatan", "Riwayat Pengobatan"), ("tanggal_mulai_pengobatan", "Mulai Pengobatan")],
        ReportPeriod.DateRange, "Daftar laporan TB yang sudah tersimpan, menurut tanggal laporan.")
        { SearchWatermark = "Pasien, nomor kunjungan, atau ID TB 03" };

    private static ReportSpec SurgicalDocumentation()
    {
        (string Table, string Label)[] checks =
        [
            ("checklist_pre_operasi", "Preoperasi"), ("signin_sebelum_anestesi", "Sign In"),
            ("timeout_sebelum_insisi", "Time Out"), ("signout_sebelum_menutup_luka", "Sign Out"),
            ("checklist_post_operasi", "Pascaoperasi"),
        ];
        var fields = string.Join(",\n", checks.Select(c =>
            $"CASE WHEN EXISTS (SELECT 1 FROM {c.Table} x WHERE x.no_rawat = o.no_rawat) THEN 'Ada' ELSE 'Tidak Ada' END AS {c.Table}"));
        return new("Kepatuhan Kelengkapan Keselamatan Bedah",
            $"""
            SELECT o.tgl_operasi, o.no_rawat, r.no_rkm_medis, p.nm_pasien, j.nm_perawatan, dr.nm_dokter, {fields}
            FROM operasi o
            JOIN reg_periksa r ON r.no_rawat = o.no_rawat
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            JOIN paket_operasi j ON j.kode_paket = o.kode_paket
            LEFT JOIN dokter dr ON dr.kd_dokter = o.operator1
            WHERE o.tgl_operasi BETWEEN @p1 AND @p2
              AND (o.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
                   OR j.nm_perawatan LIKE @p0 OR dr.nm_dokter LIKE @p0)
            ORDER BY o.tgl_operasi, o.no_rawat, o.kode_paket
            """,
            [("tgl_operasi", "Tanggal Operasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
             ("nm_pasien", "Pasien"), ("nm_perawatan", "Tindakan"), ("nm_dokter", "Operator"),
             .. checks.Select(c => (c.Table, c.Label))],
            ReportPeriod.DateRange, "Status menunjukkan keberadaan dokumen per nomor rawat.")
            { SearchWatermark = "Pasien, nomor kunjungan, tindakan, atau operator" };
    }

    private static ReportSpec AnnualAgeTargets(string title, bool senior)
    {
        var age = senior ? ">= 60" : "BETWEEN 19 AND 59";
        return new(title,
            $"""
            SELECT p.no_rkm_medis, p.nm_pasien, p.jk,
                   TIMESTAMPDIFF(YEAR, p.tgl_lahir, DATE_SUB(MAKEDATE(@p1 + 1, 1), INTERVAL 1 DAY)) AS umur,
                   COUNT(r.no_rawat) AS kunjungan,
                   COUNT(DISTINCT MONTH(r.tgl_registrasi)) AS bulan_dikunjungi
            FROM pasien p
            LEFT JOIN reg_periksa r ON r.no_rkm_medis = p.no_rkm_medis
              AND r.tgl_registrasi >= MAKEDATE(@p1, 1) AND r.tgl_registrasi < MAKEDATE(@p1 + 1, 1)
              AND r.stts <> 'Batal'
            WHERE YEAR(p.tgl_lahir) > 0
              AND TIMESTAMPDIFF(YEAR, p.tgl_lahir, DATE_SUB(MAKEDATE(@p1 + 1, 1), INTERVAL 1 DAY)) {age}
              AND (p.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0)
            GROUP BY p.no_rkm_medis, p.nm_pasien, p.jk, p.tgl_lahir
            ORDER BY p.nm_pasien, p.no_rkm_medis
            """,
            [("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("jk", "Jenis Kelamin"), ("umur", "Umur (Tahun)"),
             ("kunjungan", "Kunjungan"), ("bulan_dikunjungi", "Bulan dengan Kunjungan")],
            ReportPeriod.Year, "Umur dihitung pada akhir tahun laporan. Pasien tanpa kunjungan tetap ditampilkan.")
            { SearchWatermark = "No. RM atau nama pasien" };
    }

    private static ReportSpec PatientServiceDuration() => new(
        "Lama Pelayanan Pasien",
        """
        SELECT x.*,
               CASE WHEN x.berkas_dikirim >= x.registrasi THEN TIMESTAMPDIFF(SECOND, x.registrasi, x.berkas_dikirim) END AS ke_berkas,
               CASE WHEN x.dilayani >= x.registrasi THEN TIMESTAMPDIFF(SECOND, x.registrasi, x.dilayani) END AS ke_poli,
               CASE WHEN x.nota >= x.registrasi THEN TIMESTAMPDIFF(SECOND, x.registrasi, x.nota) END AS ke_nota
        FROM (
            SELECT r.no_rawat, r.no_rkm_medis, p.nm_pasien, pl.nm_poli, d.nm_dokter,
                   TIMESTAMP(r.tgl_registrasi, r.jam_reg) AS registrasi,
                   m.dikirim AS berkas_dikirim, m.diterima AS berkas_diterima,
                   (SELECT MIN(TIMESTAMP(e.tgl_perawatan, e.jam_rawat)) FROM pemeriksaan_ralan e
                    WHERE e.no_rawat = r.no_rawat) AS dilayani,
                   (SELECT MIN(TIMESTAMP(o.tgl_peresepan, o.jam_peresepan)) FROM resep_obat o
                    WHERE o.no_rawat = r.no_rawat) AS resep,
                   TIMESTAMP(n.tanggal, n.jam) AS nota
            FROM reg_periksa r
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            JOIN poliklinik pl ON pl.kd_poli = r.kd_poli
            JOIN dokter d ON d.kd_dokter = r.kd_dokter
            LEFT JOIN mutasi_berkas m ON m.no_rawat = r.no_rawat
            LEFT JOIN nota_jalan n ON n.no_rawat = r.no_rawat
            WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.stts <> 'Batal'
              AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
                   OR pl.nm_poli LIKE @p0 OR d.nm_dokter LIKE @p0)
        ) x
        ORDER BY x.registrasi, x.no_rawat
        """,
        [("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("nm_poli", "Poli"),
         ("nm_dokter", "Dokter"), ("registrasi", "Registrasi"), ("berkas_dikirim", "Berkas Dikirim"),
         ("berkas_diterima", "Berkas Diterima"), ("dilayani", "Periksa Pertama"), ("resep", "Resep Pertama"),
         ("nota", "Nota"), ("ke_berkas", "Ke Kirim Berkas (Detik)"), ("ke_poli", "Ke Periksa (Detik)"), ("ke_nota", "Ke Nota (Detik)")],
        ReportPeriod.DateRange, "Alur dari registrasi, mutasi berkas, pemeriksaan, dan resep sampai nota. Tahap yang belum tercatat dibiarkan kosong.")
        { SearchWatermark = "Pasien, nomor kunjungan, poli, atau dokter" };
}
