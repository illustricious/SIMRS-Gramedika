namespace SimrsKhanza.Data.Master;

public static partial class MenuJSpecs
{
    private static MasterSpec DiseaseDrugs() => new(
        "Obat Penyakit", "obat_penyakit",
        [new("kd_penyakit", "Penyakit", FieldKind.Lookup, LookupSql:
             "SELECT kd_penyakit AS kode, nm_penyakit AS nama FROM penyakit ORDER BY nm_penyakit",
             Key: true, Required: true),
         new("kode_brng", "Obat", FieldKind.Lookup, LookupSql:
             "SELECT kode_brng AS kode, nama_brng AS nama FROM databarang ORDER BY nama_brng",
             Key: true, Required: true),
         new("referensi", "Referensi", MaxLen: 60)],
        """
        SELECT o.kd_penyakit, p.nm_penyakit, o.kode_brng, b.nama_brng, o.referensi
        FROM obat_penyakit o
        JOIN penyakit p ON p.kd_penyakit = o.kd_penyakit
        JOIN databarang b ON b.kode_brng = o.kode_brng
        WHERE o.kd_penyakit LIKE @p0 OR p.nm_penyakit LIKE @p0
           OR o.kode_brng LIKE @p0 OR b.nama_brng LIKE @p0 OR o.referensi LIKE @p0
        ORDER BY p.nm_penyakit, b.nama_brng, o.kd_penyakit, o.kode_brng
        """,
        [("kd_penyakit", "Kode ICD 10"), ("nm_penyakit", "Penyakit"),
         ("kode_brng", "Kode Obat"), ("nama_brng", "Obat"), ("referensi", "Referensi")])
        { Hint = "Kelola pasangan penyakit dan obat beserta rujukan yang dicatat." };

    private static ReportSpec BedAvailability() => new(
        "RL 1.3 Ketersediaan Tempat Tidur",
        """
        SELECT b.kd_bangsal, b.nm_bangsal, k.kelas, COUNT(*) AS total,
               COUNT(CASE WHEN k.status = 'ISI' THEN 1 END) AS isi,
               COUNT(CASE WHEN k.status = 'KOSONG' THEN 1 END) AS kosong
        FROM kamar k
        JOIN bangsal b ON b.kd_bangsal = k.kd_bangsal
        WHERE k.statusdata = '1' AND b.status = '1'
          AND (b.kd_bangsal LIKE @p0 OR b.nm_bangsal LIKE @p0 OR k.kelas LIKE @p0)
        GROUP BY b.kd_bangsal, b.nm_bangsal, k.kelas
        ORDER BY b.nm_bangsal, b.kd_bangsal, k.kelas
        """,
        [("kd_bangsal", "Kode"), ("nm_bangsal", "Bangsal"), ("kelas", "Kelas"),
         ("total", "Total Aktif"), ("isi", "Terisi"), ("kosong", "Kosong")],
        ReportPeriod.None, "Kondisi terkini dari master kamar. Total aktif juga mencakup kamar yang sedang dibersihkan, dipesan, atau diperbaiki.")
        { SearchWatermark = "Kode bangsal, nama bangsal, atau kelas" };

    private static ReportSpec WardDischarges() => new(
        "Ranap Per Ruang",
        """
        SELECT b.kd_bangsal, b.nm_bangsal, j.png_jawab,
               COUNT(DISTINCT r.no_rawat) AS pasien, SUM(k.lama) AS hari
        FROM kamar_inap k
        JOIN kamar m ON m.kd_kamar = k.kd_kamar
        JOIN bangsal b ON b.kd_bangsal = m.kd_bangsal
        JOIN reg_periksa r ON r.no_rawat = k.no_rawat
        JOIN penjab j ON j.kd_pj = r.kd_pj
        WHERE k.tgl_keluar BETWEEN @p1 AND @p2 AND k.stts_pulang NOT IN ('Pindah Kamar', '-')
          AND (b.kd_bangsal LIKE @p0 OR b.nm_bangsal LIKE @p0 OR j.png_jawab LIKE @p0)
        GROUP BY b.kd_bangsal, b.nm_bangsal, j.kd_pj, j.png_jawab
        ORDER BY b.nm_bangsal, b.kd_bangsal, j.png_jawab
        """,
        [("kd_bangsal", "Kode"), ("nm_bangsal", "Bangsal"), ("png_jawab", "Cara Bayar"),
         ("pasien", "Kunjungan Keluar"), ("hari", "Hari Rawat di Kamar Terakhir")],
        ReportPeriod.DateRange, "Rekap menurut tanggal keluar dan bangsal terakhir.")
        { SearchWatermark = "Bangsal atau cara bayar" };

    private static ReportSpec AnnualWardAdmissions() => new(
        "Masuk Ruang Per Tahun",
        """
        SELECT DATE_FORMAT(r.tgl_registrasi, '%Y-%m') AS bulan, b.kd_bangsal, b.nm_bangsal,
               COUNT(*) AS jumlah
        FROM kamar_inap k
        JOIN reg_periksa r ON r.no_rawat = k.no_rawat AND r.tgl_registrasi = k.tgl_masuk
        JOIN kamar m ON m.kd_kamar = k.kd_kamar
        JOIN bangsal b ON b.kd_bangsal = m.kd_bangsal
        WHERE k.tgl_masuk >= MAKEDATE(@p1, 1) AND k.tgl_masuk < MAKEDATE(@p1 + 1, 1)
          AND (b.kd_bangsal LIKE @p0 OR b.nm_bangsal LIKE @p0)
        GROUP BY bulan, b.kd_bangsal, b.nm_bangsal
        ORDER BY bulan, b.nm_bangsal, b.kd_bangsal
        """,
        [("bulan", "Bulan"), ("kd_bangsal", "Kode"), ("nm_bangsal", "Bangsal"), ("jumlah", "Masuk Ruang")],
        ReportPeriod.Year, "Jumlah masuk ruang ketika tanggal masuk sama dengan tanggal registrasi, mengikuti definisi laporan Khanza.")
        { SearchWatermark = "Kode atau nama bangsal" };

    private static ReportSpec InpatientRegister() => new(
        "Daftar Pasien Ranap",
        """
        SELECT r.tgl_registrasi, r.no_rawat, r.no_rkm_medis, p.nm_pasien, j.png_jawab,
               k.tgl_masuk, k.kd_kamar, b.nm_bangsal, k.stts_pulang,
               CASE WHEN YEAR(k.tgl_keluar) > 0 THEN k.tgl_keluar END AS keluar,
               CASE WHEN k.tgl_keluar >= r.tgl_registrasi THEN DATEDIFF(k.tgl_keluar, r.tgl_registrasi) + 1 END AS hari
        FROM reg_periksa r
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN penjab j ON j.kd_pj = r.kd_pj
        LEFT JOIN kamar_inap k ON k.no_rawat = r.no_rawat AND k.stts_pulang <> 'Pindah Kamar'
          AND NOT EXISTS (
              SELECT 1 FROM kamar_inap z
              WHERE z.no_rawat = k.no_rawat AND z.stts_pulang <> 'Pindah Kamar'
                AND (z.tgl_masuk, z.jam_masuk) > (k.tgl_masuk, k.jam_masuk)
          )
        LEFT JOIN kamar m ON m.kd_kamar = k.kd_kamar
        LEFT JOIN bangsal b ON b.kd_bangsal = m.kd_bangsal
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.status_lanjut = 'Ranap' AND r.stts <> 'Batal'
          AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0 OR b.nm_bangsal LIKE @p0)
        ORDER BY r.tgl_registrasi, r.no_rawat
        """,
        [("tgl_registrasi", "Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"), ("tgl_masuk", "Masuk Kamar"),
         ("kd_kamar", "Kamar"), ("nm_bangsal", "Bangsal Terakhir"), ("stts_pulang", "Status Pulang"),
         ("keluar", "Tanggal Keluar"), ("hari", "Hari sejak Registrasi")],
        ReportPeriod.Month, "Daftar bulanan berdasarkan tanggal registrasi, termasuk pasien yang masih dirawat.")
        { SearchWatermark = "Pasien, nomor kunjungan, atau bangsal" };

    private static ReportSpec InpatientDiagnosisByPayer() => new(
        "Penyakit Ranap Per Cara Bayar",
        """
        SELECT p.kd_penyakit, p.nm_penyakit, j.png_jawab, COUNT(*) AS jumlah
        FROM diagnosa_pasien d
        JOIN penyakit p ON p.kd_penyakit = d.kd_penyakit
        JOIN reg_periksa r ON r.no_rawat = d.no_rawat
        JOIN penjab j ON j.kd_pj = r.kd_pj
        WHERE d.status = 'Ranap' AND r.stts <> 'Batal'
          AND EXISTS (SELECT 1 FROM kamar_inap k WHERE k.no_rawat = r.no_rawat
                      AND k.tgl_keluar BETWEEN @p1 AND @p2 AND k.stts_pulang NOT IN ('Pindah Kamar', '-'))
          AND (p.kd_penyakit LIKE @p0 OR p.nm_penyakit LIKE @p0 OR j.png_jawab LIKE @p0)
        GROUP BY p.kd_penyakit, p.nm_penyakit, j.kd_pj, j.png_jawab
        ORDER BY p.kd_penyakit, j.png_jawab
        """,
        [("kd_penyakit", "Kode ICD 10"), ("nm_penyakit", "Penyakit"), ("png_jawab", "Cara Bayar"), ("jumlah", "Diagnosis")],
        ReportPeriod.DateRange, "Rekap diagnosis rawat inap berdasarkan tanggal keluar dan cara bayar.")
        { SearchWatermark = "Penyakit atau cara bayar" };

    private static ReportSpec OutpatientDailyCensus() => new(
        "Sensus Harian Ralan",
        """
        SELECT r.tgl_registrasi, r.jam_reg, r.no_rawat, r.no_rkm_medis, p.nm_pasien, poli.nm_poli,
               dr.nm_dokter, j.png_jawab, r.stts_daftar, r.status_lanjut,
               (SELECT MIN(d.kd_penyakit) FROM diagnosa_pasien d
                WHERE d.no_rawat = r.no_rawat AND d.status = 'Ralan' AND d.prioritas = 1) AS icd10
        FROM reg_periksa r
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN poliklinik poli ON poli.kd_poli = r.kd_poli
        JOIN dokter dr ON dr.kd_dokter = r.kd_dokter
        JOIN penjab j ON j.kd_pj = r.kd_pj
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.stts <> 'Batal'
          AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
               OR poli.nm_poli LIKE @p0 OR dr.nm_dokter LIKE @p0 OR j.png_jawab LIKE @p0)
        ORDER BY r.tgl_registrasi, r.jam_reg, r.no_rawat
        """,
        [("tgl_registrasi", "Tanggal"), ("jam_reg", "Jam"), ("no_rawat", "No. Rawat"),
         ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("nm_poli", "Poli"),
         ("nm_dokter", "Dokter"), ("png_jawab", "Cara Bayar"), ("stts_daftar", "Baru/Lama"),
         ("status_lanjut", "Layanan Lanjut"), ("icd10", "ICD 10 Utama Ralan")],
        ReportPeriod.DateRange, "Daftar registrasi dan kode diagnosis utama rawat jalan, termasuk kunjungan yang berlanjut ke rawat inap.")
        { SearchWatermark = "Pasien, nomor kunjungan, poli, dokter, atau cara bayar" };

    private static ReportSpec PrimaryDiagnosisIndex(string title, string care)
    {
        var inpatient = care == "Ranap";
        var date = inpatient ? "k.tgl_keluar" : "r.tgl_registrasi";
        var stayJoin = inpatient
            ? """
              JOIN kamar_inap k ON k.no_rawat = r.no_rawat AND k.stts_pulang NOT IN ('Pindah Kamar', '-')
                AND NOT EXISTS (
                    SELECT 1 FROM kamar_inap newer WHERE newer.no_rawat = k.no_rawat
                      AND newer.stts_pulang NOT IN ('Pindah Kamar', '-')
                      AND (newer.tgl_masuk, newer.jam_masuk) > (k.tgl_masuk, k.jam_masuk)
                )
              """
            : "";
        var outcome = inpatient ? "k.stts_pulang" : "r.stts";
        return new(title,
            $"""
            SELECT {date} AS tanggal, r.no_rawat, r.no_rkm_medis, p.nm_pasien, p.jk,
                   r.umurdaftar, r.sttsumur, x.kd_penyakit, x.nm_penyakit,
                   d.status_penyakit, j.png_jawab, {outcome} AS status
            FROM diagnosa_pasien d
            JOIN reg_periksa r ON r.no_rawat = d.no_rawat
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            JOIN penyakit x ON x.kd_penyakit = d.kd_penyakit
            JOIN penjab j ON j.kd_pj = r.kd_pj
            {stayJoin}
            WHERE {date} BETWEEN @p1 AND @p2 AND d.status = '{care}' AND d.prioritas = 1
              AND r.status_lanjut = '{care}' AND r.stts <> 'Batal'
              AND (x.kd_penyakit LIKE @p0 OR x.nm_penyakit LIKE @p0)
            ORDER BY tanggal, r.no_rawat, x.kd_penyakit
            """,
            [("tanggal", inpatient ? "Tanggal Keluar" : "Tanggal Registrasi"), ("no_rawat", "No. Rawat"),
             ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("jk", "Jenis Kelamin"),
             ("umurdaftar", "Umur"), ("sttsumur", "Satuan Umur"), ("kd_penyakit", "Kode ICD 10"),
             ("nm_penyakit", "Diagnosis Utama"), ("status_penyakit", "Baru/Lama"), ("png_jawab", "Cara Bayar"),
             ("status", inpatient ? "Status Pulang" : "Status Registrasi")],
            ReportPeriod.DateRange, "Daftar pasien menurut diagnosis utama. Pilih periode dan cari kode atau nama penyakit.")
            { SearchWatermark = "Kode atau nama penyakit" };
    }

    private static ReportSpec MedicalRecordStatus()
    {
        (string Table, string Alias, string Header)[] checks =
        [
            ("pemeriksaan_ralan", "soap_ralan", "SOAP Ralan"), ("pemeriksaan_ranap", "soap_ranap", "SOAP Ranap"),
            ("resume_pasien", "resume_ralan", "Resume Ralan"), ("resume_pasien_ranap", "resume_ranap", "Resume Ranap"),
            ("data_triase_igd", "triase", "Triase IGD"), ("penilaian_awal_keperawatan_igd", "askep", "Askep IGD"),
            ("diagnosa_pasien", "icd10", "ICD 10"), ("prosedur_pasien", "icd9", "ICD 9"),
        ];
        var fields = string.Join(",\n", checks.Select(c =>
            $"CASE WHEN EXISTS (SELECT 1 FROM {c.Table} x WHERE x.no_rawat = r.no_rawat) THEN 'Ada' ELSE 'Tidak Ada' END AS {c.Alias}"));
        return new("Status Data RM",
            $"""
            SELECT r.tgl_registrasi, r.no_rawat, r.no_rkm_medis, p.nm_pasien, poli.nm_poli, r.status_lanjut,
                   {fields}
            FROM reg_periksa r
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            JOIN poliklinik poli ON poli.kd_poli = r.kd_poli
            WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.stts <> 'Batal'
              AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0 OR poli.nm_poli LIKE @p0)
            ORDER BY r.tgl_registrasi, r.no_rawat
            """,
            [("tgl_registrasi", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
             ("nm_pasien", "Pasien"), ("nm_poli", "Poli"), ("status_lanjut", "Layanan"),
             .. checks.Select(c => (c.Alias, c.Header))],
            ReportPeriod.DateRange, "Status menunjukkan keberadaan entri per nomor rawat.")
            { SearchWatermark = "Pasien, nomor kunjungan, atau poli" };
    }

    private static ReportSpec OutpatientEntryAudit()
    {
        (string Table, string DateMatch, string Alias, string Header)[] checks =
        [
            ("rawat_jl_dr a", "a.tgl_perawatan = d.tanggal", "dokter", "Tindakan Dokter"),
            ("rawat_jl_pr a", "a.tgl_perawatan = d.tanggal", "petugas", "Tindakan Petugas"),
            ("rawat_jl_drpr a", "a.tgl_perawatan = d.tanggal", "dokter_petugas", "Tindakan Gabungan"),
            ("pemeriksaan_ralan a", "a.tgl_perawatan = d.tanggal", "soap", "Pemeriksaan"),
            ("periksa_lab a", "a.tgl_periksa = d.tanggal AND a.status = 'Ralan'", "lab", "Lab"),
            ("periksa_radiologi a", "a.tgl_periksa = d.tanggal AND a.status = 'Ralan'", "rad", "Radiologi"),
            ("operasi a", "a.tgl_operasi >= d.tanggal AND a.tgl_operasi < DATE_ADD(d.tanggal, INTERVAL 1 DAY) AND a.status = 'Ralan'", "operasi", "Operasi"),
            ("detail_pemberian_obat a", "a.tgl_perawatan = d.tanggal AND a.status = 'Ralan'", "obat", "Pemberian Obat"),
            ("diagnosa_pasien a JOIN reg_periksa r ON r.no_rawat = a.no_rawat",
                "r.tgl_registrasi = d.tanggal AND a.status = 'Ralan'", "diagnosis", "Diagnosis"),
            ("prosedur_pasien a JOIN reg_periksa r ON r.no_rawat = a.no_rawat",
                "r.tgl_registrasi = d.tanggal AND a.status = 'Ralan'", "prosedur", "Prosedur"),
            ("nota_jalan a", "a.tanggal = d.tanggal", "billing", "Nota Ralan"),
        ];
        var fields = string.Join(",\n", checks.Select(c =>
            $"(SELECT COUNT(DISTINCT a.no_rawat) FROM {c.Table} WHERE {c.DateMatch}) AS {c.Alias}"));
        return new("Cek Entry Ralan",
            $"""
            SELECT d.tanggal, d.registrasi, d.lanjut_ranap, {fields}
            FROM (
                SELECT tgl_registrasi AS tanggal, COUNT(*) AS registrasi,
                       COUNT(CASE WHEN status_lanjut = 'Ranap' THEN 1 END) AS lanjut_ranap
                FROM reg_periksa WHERE tgl_registrasi BETWEEN @p1 AND @p2
                GROUP BY tgl_registrasi
            ) d
            WHERE DATE_FORMAT(d.tanggal, '%Y-%m-%d') LIKE @p0
            ORDER BY d.tanggal
            """,
            [("tanggal", "Tanggal"), ("registrasi", "Registrasi"), ("lanjut_ranap", "Lanjut Ranap"),
             .. checks.Select(c => (c.Alias, c.Header))],
            ReportPeriod.DateRange, "Jumlah nomor rawat unik yang memiliki entri pada tiap jenis layanan per hari.")
            { SearchWatermark = "Tanggal (YYYY-MM-DD)" };
    }

    private static ReportSpec VisitAgeDemographics() => new(
        "Demografi Umur Kunjungan",
        """
        SELECT r.status_lanjut, p.jk, r.sttsumur, r.umurdaftar,
               COUNT(CASE WHEN r.stts_daftar = 'Baru' THEN 1 END) AS baru,
               COUNT(CASE WHEN r.stts_daftar = 'Lama' THEN 1 END) AS lama, COUNT(*) AS jumlah
        FROM reg_periksa r
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN poliklinik poli ON poli.kd_poli = r.kd_poli
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.stts <> 'Batal'
          AND (r.status_lanjut LIKE @p0 OR poli.nm_poli LIKE @p0)
        GROUP BY r.status_lanjut, p.jk, r.sttsumur, r.umurdaftar
        ORDER BY r.status_lanjut, r.sttsumur, r.umurdaftar, p.jk
        """,
        [("status_lanjut", "Layanan"), ("jk", "Jenis Kelamin"), ("sttsumur", "Satuan Umur"), ("umurdaftar", "Umur"),
         ("baru", "Registrasi Baru"), ("lama", "Registrasi Lama"), ("jumlah", "Total")],
        ReportPeriod.DateRange, "Distribusi berdasarkan umur yang tercatat pada saat registrasi, satuan umur, layanan, dan jenis kelamin.")
        { SearchWatermark = "Layanan atau nama poli" };
}
