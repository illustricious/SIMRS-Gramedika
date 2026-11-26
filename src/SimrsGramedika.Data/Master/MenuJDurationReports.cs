namespace SimrsGramedika.Data.Master;

public static partial class MenuJSpecs
{
    private static ReportSpec ServiceStages(string title, string table, string key,
        string startDate, string startTime, string middleDate, string middleTime,
        string endDate, string endTime, string middleLabel, string endLabel) => new(
        title,
        $"""
        SELECT x.kode, x.no_rawat, r.no_rkm_medis, p.nm_pasien, x.mulai, x.tengah, x.selesai,
               CASE WHEN x.tengah >= x.mulai THEN TIMESTAMPDIFF(SECOND, x.mulai, x.tengah) END AS tahap1,
               CASE WHEN x.selesai >= x.tengah THEN TIMESTAMPDIFF(SECOND, x.tengah, x.selesai) END AS tahap2,
               CASE WHEN x.selesai >= x.mulai THEN TIMESTAMPDIFF(SECOND, x.mulai, x.selesai) END AS total
        FROM (
            SELECT a.{key} AS kode, a.no_rawat,
                   TIMESTAMP(a.{startDate}, a.{startTime}) AS mulai,
                   CASE WHEN YEAR(a.{middleDate}) > 0 THEN TIMESTAMP(a.{middleDate}, a.{middleTime}) END AS tengah,
                   CASE WHEN YEAR(a.{endDate}) > 0 THEN TIMESTAMP(a.{endDate}, a.{endTime}) END AS selesai
            FROM {table} a
            WHERE a.{startDate} BETWEEN @p1 AND @p2
        ) x
        JOIN reg_periksa r ON r.no_rawat = x.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        WHERE x.kode LIKE @p0 OR x.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
        ORDER BY x.mulai, x.kode
        """,
        [("kode", "No. Permintaan/Resep"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("mulai", "Mulai"), ("tengah", middleLabel), ("selesai", endLabel),
         ("tahap1", $"Mulai ke {middleLabel} (Detik)"), ("tahap2", $"{middleLabel} ke {endLabel} (Detik)"),
         ("total", "Total (Detik)")],
        ReportPeriod.DateRange, "Periode mengikuti tanggal permintaan atau resep. Durasi kosong jika waktu tidak tersedia atau urutannya terbalik.")
        { SearchWatermark = "Pasien, No. Rawat, No. RM, atau nomor permintaan" };

    private static ReportSpec RegistrationToService(string title, string endLabel, string table, string date, string time) => new(
        title,
        $"""
        SELECT x.no_rawat, x.no_rkm_medis, x.nm_pasien, x.nm_poli, x.registrasi, x.selesai,
               TIMESTAMPDIFF(SECOND, x.registrasi, x.selesai) AS durasi
        FROM (
            SELECT r.no_rawat, r.no_rkm_medis, p.nm_pasien, poli.nm_poli,
                   TIMESTAMP(r.tgl_registrasi, r.jam_reg) AS registrasi,
                   MIN(TIMESTAMP(s.{date}, s.{time})) AS selesai
            FROM reg_periksa r
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            JOIN poliklinik poli ON poli.kd_poli = r.kd_poli
            JOIN {table} s ON s.no_rawat = r.no_rawat
            WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.stts <> 'Batal'
              AND TIMESTAMP(s.{date}, s.{time}) >= TIMESTAMP(r.tgl_registrasi, r.jam_reg)
              AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0 OR poli.nm_poli LIKE @p0)
            GROUP BY r.no_rawat, r.no_rkm_medis, p.nm_pasien, poli.nm_poli, r.tgl_registrasi, r.jam_reg
        ) x
        ORDER BY x.registrasi, x.no_rawat
        """,
        [("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
         ("nm_poli", "Poli"), ("registrasi", "Registrasi"), ("selesai", endLabel), ("durasi", "Durasi (Detik)")],
        ReportPeriod.DateRange, $"Durasi dari waktu registrasi sampai {endLabel.ToLowerInvariant()}. Periode mengikuti tanggal registrasi.")
        { SearchWatermark = "Pasien, nomor kunjungan, atau poli" };

    private static ReportSpec OperationDuration() => new(
        "Lama Operasi",
        """
        SELECT o.no_rawat, r.no_rkm_medis, p.nm_pasien, o.tanggal, o.selesaioperasi,
               TIMESTAMPDIFF(SECOND, o.tanggal, o.selesaioperasi) AS durasi
        FROM laporan_operasi o
        JOIN reg_periksa r ON r.no_rawat = o.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        WHERE o.tanggal BETWEEN @p1 AND @p2 AND o.selesaioperasi >= o.tanggal
          AND (o.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0)
        ORDER BY o.tanggal, o.no_rawat
        """,
        [("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
         ("tanggal", "Mulai Operasi"), ("selesaioperasi", "Selesai Operasi"), ("durasi", "Durasi (Detik)")],
        ReportPeriod.DateRange, "Durasi berdasarkan waktu mulai dan selesai yang tercatat pada laporan operasi.")
        { SearchWatermark = "Pasien atau nomor kunjungan" };

    private static ReportSpec CssdDuration() => new(
        "Lama Pelayanan CSSD",
        """
        SELECT a.no_sirkulasi, a.no_inventaris, b.nama_barang, a.tgl_ambil,
               s.tgl_sterilisasi, k.tgl_kembali,
               CASE WHEN s.tgl_sterilisasi >= a.tgl_ambil
                    THEN TIMESTAMPDIFF(SECOND, a.tgl_ambil, s.tgl_sterilisasi) END AS steril,
               CASE WHEN k.tgl_kembali >= s.tgl_sterilisasi
                    THEN TIMESTAMPDIFF(SECOND, s.tgl_sterilisasi, k.tgl_kembali) END AS kembali,
               CASE WHEN k.tgl_kembali >= a.tgl_ambil
                    THEN TIMESTAMPDIFF(SECOND, a.tgl_ambil, k.tgl_kembali) END AS total
        FROM inventaris_ambil_cssd a
        JOIN inventaris i ON i.no_inventaris = a.no_inventaris
        JOIN inventaris_barang b ON b.kode_barang = i.kode_barang
        LEFT JOIN inventaris_sterilisasi_cssd s ON s.no_sirkulasi = a.no_sirkulasi
        LEFT JOIN inventaris_kembali_cssd k ON k.no_sirkulasi = a.no_sirkulasi
        WHERE a.tgl_ambil BETWEEN @p1 AND @p2
          AND (a.no_sirkulasi LIKE @p0 OR a.no_inventaris LIKE @p0 OR b.nama_barang LIKE @p0)
        ORDER BY a.tgl_ambil, a.no_sirkulasi
        """,
        [("no_sirkulasi", "No. Sirkulasi"), ("no_inventaris", "No. Inventaris"), ("nama_barang", "Barang"),
         ("tgl_ambil", "Diambil"), ("tgl_sterilisasi", "Sterilisasi"), ("tgl_kembali", "Kembali"),
         ("steril", "Ambil ke Sterilisasi (Detik)"), ("kembali", "Sterilisasi ke Kembali (Detik)"),
         ("total", "Total (Detik)")],
        ReportPeriod.DateRange, "Periode menurut waktu pengambilan. Durasi tampil jika kedua waktu tahap sudah tercatat dan berurutan.")
        { SearchWatermark = "Barang, nomor inventaris, atau sirkulasi" };
}
