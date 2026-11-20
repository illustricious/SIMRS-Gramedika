namespace SimrsKhanza.Data.Master;

public static partial class MenuJSpecs
{
    private static ReportSpec AncillaryActivity(string title, bool lab)
    {
        var service = lab ? "lab" : "radiologi";
        var labParameters = lab
            ? """
              UNION ALL
              SELECT j.kd_jenis_prw, j.nm_perawatan, 'Parameter', t.Pemeriksaan,
                     COUNT(CASE WHEN p.jk = 'L' THEN 1 END), COUNT(CASE WHEN p.jk = 'P' THEN 1 END), COUNT(*)
              FROM detail_periksa_lab x
              JOIN template_laboratorium t ON t.id_template = x.id_template
              JOIN jns_perawatan_lab j ON j.kd_jenis_prw = t.kd_jenis_prw
              JOIN reg_periksa r ON r.no_rawat = x.no_rawat
              JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
              WHERE x.tgl_periksa BETWEEN @p1 AND @p2
                AND (j.kd_jenis_prw LIKE @p0 OR j.nm_perawatan LIKE @p0 OR t.Pemeriksaan LIKE @p0)
              GROUP BY j.kd_jenis_prw, j.nm_perawatan, t.id_template, t.Pemeriksaan
              """
            : "";
        return new(title,
            $"""
            SELECT j.kd_jenis_prw AS kode, j.nm_perawatan AS tindakan, 'Tindakan' AS jenis, '' AS parameter,
                   COUNT(CASE WHEN p.jk = 'L' THEN 1 END) AS laki,
                   COUNT(CASE WHEN p.jk = 'P' THEN 1 END) AS perempuan, COUNT(*) AS jumlah
            FROM periksa_{service} x
            JOIN jns_perawatan_{service} j ON j.kd_jenis_prw = x.kd_jenis_prw
            JOIN reg_periksa r ON r.no_rawat = x.no_rawat
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            WHERE x.tgl_periksa BETWEEN @p1 AND @p2 AND (j.kd_jenis_prw LIKE @p0 OR j.nm_perawatan LIKE @p0)
            GROUP BY j.kd_jenis_prw, j.nm_perawatan
            {labParameters}
            ORDER BY tindakan, kode, jenis DESC, parameter
            """,
            [("kode", "Kode"), ("tindakan", "Pemeriksaan"), ("jenis", "Jenis Hitungan"), ("parameter", "Parameter Lab"),
             ("laki", "Laki-laki"), ("perempuan", "Perempuan"), ("jumlah", "Jumlah")],
            ReportPeriod.DateRange, "Jumlah pemeriksaan per jenis dan jenis kelamin. Parameter laboratorium dihitung terpisah.")
            { SearchWatermark = "Kode, pemeriksaan, atau parameter" };
    }

    private static ReportSpec DailyInpatientClassification() => new(
        "Harian Klasifikasi Pasien Ranap",
        """
        SELECT x.tanggal, x.no_rawat, r.no_rkm_medis, p.nm_pasien, x.kd_kamar, b.nm_bangsal,
               j.png_jawab, x.Minimal, x.Partial, x.Total
        FROM data_klasifikasi_pasien_ranap x
        JOIN reg_periksa r ON r.no_rawat = x.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        JOIN penjab j ON j.kd_pj = r.kd_pj
        LEFT JOIN kamar k ON k.kd_kamar = x.kd_kamar
        LEFT JOIN bangsal b ON b.kd_bangsal = k.kd_bangsal
        WHERE x.tanggal BETWEEN @p1 AND @p2
          AND (x.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
               OR b.nm_bangsal LIKE @p0 OR j.png_jawab LIKE @p0)
        ORDER BY x.tanggal, x.no_rawat
        """,
        [("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
         ("kd_kamar", "Kamar"), ("nm_bangsal", "Bangsal"), ("png_jawab", "Cara Bayar"),
         ("Minimal", "Minimal"), ("Partial", "Parsial"), ("Total", "Total")],
        ReportPeriod.DateRange, "Daftar klasifikasi asuhan yang tercatat per tanggal dan nomor rawat.")
        { SearchWatermark = "Pasien, nomor kunjungan, bangsal, atau cara bayar" };

    private static ReportSpec InpatientClassificationSummary(string title, bool byWard)
    {
        var grouping = byWard ? "b.nm_bangsal, b.kd_bangsal" : "x.tanggal";
        return new(title,
            $"""
            SELECT {grouping}, COUNT(DISTINCT x.no_rawat) AS pasien, COUNT(*) AS penilaian,
                   COUNT(CASE WHEN x.Minimal = 'IYA' THEN 1 END) AS minimal,
                   COUNT(CASE WHEN x.Partial = 'IYA' THEN 1 END) AS parsial,
                   COUNT(CASE WHEN x.Total = 'IYA' THEN 1 END) AS total
            FROM data_klasifikasi_pasien_ranap x
            JOIN reg_periksa r ON r.no_rawat = x.no_rawat
            JOIN penjab j ON j.kd_pj = r.kd_pj
            JOIN kamar k ON k.kd_kamar = x.kd_kamar
            JOIN bangsal b ON b.kd_bangsal = k.kd_bangsal
            WHERE x.tanggal BETWEEN @p1 AND @p2
              AND (b.nm_bangsal LIKE @p0 OR b.kd_bangsal LIKE @p0 OR j.png_jawab LIKE @p0)
            GROUP BY {grouping}
            ORDER BY {grouping}
            """,
            [byWard ? ("nm_bangsal", "Bangsal") : ("tanggal", "Tanggal"),
             ("pasien", "Kunjungan Unik"), ("penilaian", "Penilaian"), ("minimal", "Minimal"),
             ("parsial", "Parsial"), ("total", "Total")],
            byWard ? ReportPeriod.DateRange : ReportPeriod.Month,
            "Klasifikasi dihitung per penilaian. Satu kunjungan bisa dinilai pada beberapa hari.")
            { SearchWatermark = "Bangsal atau cara bayar" };
    }
}
