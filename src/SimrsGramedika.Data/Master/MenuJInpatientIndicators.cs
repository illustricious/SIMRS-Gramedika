namespace SimrsGramedika.Data.Master;

public static partial class MenuJSpecs
{
    private static (string Field, string Label)[] InfectionFields =>
    [
        ("ETT", "ETT"), ("CVL", "CVL"), ("IVL", "IVL"), ("UC", "UC"), ("VAP", "VAP"),
        ("IAD", "IAD"), ("PLEB", "PLEB"), ("ISK", "ISK"), ("ILO", "ILO"),
        ("HAP", "HAP"), ("Tinea", "Tinea"), ("Scabies", "Scabies"), ("DEKU", "Dekubitus"),
    ];

    private static ReportSpec InfectionDaily(string title, bool microbiology)
    {
        (string Field, string Label)[] fields = microbiology
            ? [("SPUTUM", "Sputum"), ("DARAH", "Darah"), ("URINE", "Urine"), ("ANTIBIOTIK", "Antibiotik"), .. InfectionFields]
            : [.. InfectionFields, ("SPUTUM", "Sputum"), ("DARAH", "Darah"), ("URINE", "Urine"), ("ANTIBIOTIK", "Antibiotik")];
        var select = string.Join(", ", fields.Select(f => $"h.{f.Field}"));
        return new(title,
            $"""
            SELECT h.tanggal, h.no_rawat, r.no_rkm_medis, p.nm_pasien, b.nm_bangsal, h.kd_kamar, j.png_jawab,
                   {select}
            FROM data_HAIs h
            JOIN reg_periksa r ON r.no_rawat = h.no_rawat
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            JOIN penjab j ON j.kd_pj = r.kd_pj
            LEFT JOIN kamar k ON k.kd_kamar = h.kd_kamar
            LEFT JOIN bangsal b ON b.kd_bangsal = k.kd_bangsal
            WHERE h.tanggal BETWEEN @p1 AND @p2
              AND (h.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
                   OR b.nm_bangsal LIKE @p0 OR j.png_jawab LIKE @p0)
            ORDER BY h.tanggal, h.no_rawat
            """,
            [("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("nm_bangsal", "Bangsal"), ("kd_kamar", "Kamar"), ("png_jawab", "Cara Bayar"), .. fields],
            ReportPeriod.DateRange, "Daftar observasi HAIs yang tercatat per tanggal dan nomor rawat.")
            { SearchWatermark = "Pasien, nomor kunjungan, bangsal, atau cara bayar" };
    }

    private static ReportSpec InfectionSummary(string title, bool ward)
    {
        var grouping = ward ? "b.kd_bangsal, b.nm_bangsal" : "h.tanggal";
        var totals = string.Join(", ", InfectionFields.Select(f =>
            f.Field == "DEKU" ? "COUNT(CASE WHEN h.DEKU = 'IYA' THEN 1 END) AS DEKU" : $"SUM(h.{f.Field}) AS {f.Field}"));
        return new(title,
            $"""
            SELECT {grouping}, COUNT(*) AS observasi, COUNT(DISTINCT h.no_rawat) AS pasien,
                   {totals},
                   COUNT(CASE WHEN TRIM(h.SPUTUM) <> '' THEN 1 END) AS sputum,
                   COUNT(CASE WHEN TRIM(h.DARAH) <> '' THEN 1 END) AS darah,
                   COUNT(CASE WHEN TRIM(h.URINE) <> '' THEN 1 END) AS urine,
                   COUNT(CASE WHEN TRIM(h.ANTIBIOTIK) <> '' THEN 1 END) AS antibiotik
            FROM data_HAIs h
            JOIN reg_periksa r ON r.no_rawat = h.no_rawat
            JOIN penjab j ON j.kd_pj = r.kd_pj
            JOIN kamar k ON k.kd_kamar = h.kd_kamar
            JOIN bangsal b ON b.kd_bangsal = k.kd_bangsal
            WHERE h.tanggal BETWEEN @p1 AND @p2
              AND (b.kd_bangsal LIKE @p0 OR b.nm_bangsal LIKE @p0 OR j.png_jawab LIKE @p0)
            GROUP BY {grouping}
            ORDER BY {grouping}
            """,
            [ward ? ("nm_bangsal", "Bangsal") : ("tanggal", "Tanggal"), ("observasi", "Observasi"),
             ("pasien", "Kunjungan Unik"), .. InfectionFields, ("sputum", "Sputum Tercatat"),
             ("darah", "Darah Tercatat"), ("urine", "Urine Tercatat"), ("antibiotik", "Antibiotik Tercatat")],
            ward ? ReportPeriod.DateRange : ReportPeriod.Month,
            "Menjumlahkan nilai observasi tercatat. Kolom kultur dan antibiotik menghitung entri yang terisi.")
            { SearchWatermark = "Bangsal atau cara bayar" };
    }

    private static ReportSpec BedOccupancy() => new(
        "Hitung BOR",
        """
        SELECT x.kd_bangsal, x.nm_bangsal, x.kapasitas, x.hari_periode, x.hari_terisi,
               CAST(ROUND(100.0 * x.hari_terisi / NULLIF(x.kapasitas * x.hari_periode, 0), 2) AS CHAR) AS bor
        FROM (
            SELECT b.kd_bangsal, b.nm_bangsal, COUNT(DISTINCT k.kd_kamar) AS kapasitas,
                   DATEDIFF(@p2, @p1) + 1 AS hari_periode,
                   COALESCE(SUM(
                       CASE WHEN h.no_rawat IS NULL THEN 0 ELSE
                           GREATEST(0, DATEDIFF(
                               LEAST(
                                   CASE WHEN h.stts_pulang = '-' AND (h.tgl_keluar IS NULL OR YEAR(h.tgl_keluar) = 0)
                                        THEN DATE_ADD(CURDATE(), INTERVAL 1 DAY)
                                        ELSE GREATEST(h.tgl_keluar, DATE_ADD(h.tgl_masuk, INTERVAL 1 DAY)) END,
                                   DATE_ADD(DATE(@p2), INTERVAL 1 DAY)),
                               GREATEST(h.tgl_masuk, DATE(@p1)))) END
                   ), 0) AS hari_terisi
            FROM bangsal b
            JOIN kamar k ON k.kd_bangsal = b.kd_bangsal AND k.statusdata = '1'
            LEFT JOIN kamar_inap h ON h.kd_kamar = k.kd_kamar AND h.tgl_masuk <= @p2
              AND (h.tgl_keluar >= @p1 OR (h.stts_pulang = '-' AND (h.tgl_keluar IS NULL OR YEAR(h.tgl_keluar) = 0)))
            WHERE b.kd_bangsal LIKE @p0 OR b.nm_bangsal LIKE @p0
            GROUP BY b.kd_bangsal, b.nm_bangsal
        ) x
        ORDER BY x.nm_bangsal, x.kd_bangsal
        """,
        [("kd_bangsal", "Kode Bangsal"), ("nm_bangsal", "Bangsal"), ("kapasitas", "Tempat Tidur Aktif"),
         ("hari_periode", "Hari Periode"), ("hari_terisi", "Hari Rawat"), ("bor", "BOR (%)")],
        ReportPeriod.DateRange,
        "BOR memakai kapasitas aktif saat ini dan hari rawat dalam periode. Episode masuk-keluar di hari yang sama dihitung satu hari per kamar.")
        { SearchWatermark = "Kode atau nama bangsal" };

    private static ReportSpec AverageLengthOfStay() => new(
        "Hitung ALOS",
        """
        SELECT b.kd_bangsal, b.nm_bangsal, COUNT(*) AS pasien,
               CAST(ROUND(SUM(x.hari), 2) AS CHAR) AS hari,
               CAST(ROUND(AVG(x.hari), 2) AS CHAR) AS alos
        FROM kamar_inap h
        JOIN kamar k ON k.kd_kamar = h.kd_kamar
        JOIN bangsal b ON b.kd_bangsal = k.kd_bangsal
        JOIN (SELECT no_rawat, SUM(COALESCE(lama, 0)) AS hari FROM kamar_inap GROUP BY no_rawat) x
          ON x.no_rawat = h.no_rawat
        WHERE h.tgl_keluar BETWEEN @p1 AND @p2 AND h.stts_pulang NOT IN ('Pindah Kamar', '-')
          AND NOT EXISTS (
              SELECT 1 FROM kamar_inap z WHERE z.no_rawat = h.no_rawat
                AND z.stts_pulang NOT IN ('Pindah Kamar', '-')
                AND (z.tgl_masuk, z.jam_masuk) > (h.tgl_masuk, h.jam_masuk)
          )
          AND (b.kd_bangsal LIKE @p0 OR b.nm_bangsal LIKE @p0)
        GROUP BY b.kd_bangsal, b.nm_bangsal
        ORDER BY b.nm_bangsal, b.kd_bangsal
        """,
        [("kd_bangsal", "Kode Bangsal"), ("nm_bangsal", "Bangsal Terakhir"), ("pasien", "Pasien Keluar"),
         ("hari", "Total Hari Rawat"), ("alos", "ALOS (Hari)")],
        ReportPeriod.DateRange, "Rata-rata hari rawat tercatat per pasien keluar. Hari di kamar sebelumnya ikut dihitung.")
        { SearchWatermark = "Kode atau nama bangsal" };
}
