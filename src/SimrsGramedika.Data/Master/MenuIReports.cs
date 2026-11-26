namespace SimrsGramedika.Data.Master;

/// <summary>Read-only Menu I reports. Dates follow the corresponding legacy dialogs.</summary>
public static class MenuIReports
{
    private static ReportSpec Adjustments(bool discount) => new(
        discount ? "Potongan Biaya Pasien" : "Tambahan Biaya Pasien",
        $"""
        SELECT r.tgl_registrasi AS tanggal, r.no_rawat, p.nm_pasien,
               a.{(discount ? "nama_pengurangan" : "nama_biaya")} AS komponen,
               COALESCE(a.{(discount ? "besar_pengurangan" : "besar_biaya")}, 0) AS nilai
        FROM {(discount ? "pengurangan_biaya" : "tambahan_biaya")} a
        JOIN reg_periksa r ON r.no_rawat = a.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND (r.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0
               OR a.{(discount ? "nama_pengurangan" : "nama_biaya")} LIKE @p0)
        ORDER BY r.tgl_registrasi, r.no_rawat, komponen
        """,
        [("tanggal", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"), ("nm_pasien", "Pasien"),
         ("komponen", discount ? "Nama Potongan" : "Nama Tambahan"), ("nilai", "Nilai")],
        ReportPeriod.DateRange,
        "Laporan berdasarkan tanggal registrasi. Total mengikuti hasil pencarian; transaksi biaya dilakukan di Khanza.")
        { SearchWatermark = "No. rawat, pasien, atau komponen", TotalColumns = ["nilai"] };

    private static readonly ReportSpec Receivables = new(
        "Piutang Pasien",
        """
        SELECT pp.no_rawat, pp.tgl_piutang, pp.no_rkm_medis, p.nm_pasien,
               pp.status, pp.totalpiutang, pp.uangmuka,
               COALESCE(b.cicilan, 0) AS cicilan, COALESCE(b.diskon, 0) AS diskon,
               COALESCE(b.tidak_terbayar, 0) AS tidak_terbayar,
               pp.sisapiutang - COALESCE(b.cicilan, 0) - COALESCE(b.diskon, 0)
                   - COALESCE(b.tidak_terbayar, 0) AS sisa,
               pp.tgltempo, j.png_jawab
        FROM piutang_pasien pp
        JOIN pasien p ON p.no_rkm_medis = pp.no_rkm_medis
        JOIN reg_periksa r ON r.no_rawat = pp.no_rawat
        JOIN penjab j ON j.kd_pj = r.kd_pj
        LEFT JOIN (
            SELECT no_rawat, SUM(besar_cicilan) AS cicilan, SUM(diskon_piutang) AS diskon,
                   SUM(tidak_terbayar) AS tidak_terbayar
            FROM bayar_piutang GROUP BY no_rawat
        ) b ON b.no_rawat = pp.no_rawat
        WHERE pp.tgl_piutang BETWEEN @p1 AND @p2
          AND (pp.no_rawat LIKE @p0 OR pp.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
               OR pp.status LIKE @p0 OR j.png_jawab LIKE @p0)
        ORDER BY pp.tgl_piutang, pp.no_rawat
        """,
        [("no_rawat", "No. Rawat"), ("tgl_piutang", "Tgl. Piutang"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("status", "Status"), ("totalpiutang", "Total Piutang"),
         ("uangmuka", "Uang Muka"), ("cicilan", "Cicilan"), ("diskon", "Diskon"),
         ("tidak_terbayar", "Tidak Terbayar"), ("sisa", "Sisa"), ("tgltempo", "Jatuh Tempo"),
         ("png_jawab", "Cara Bayar")],
        ReportPeriod.DateRange,
        "Sisa dihitung dari sisa awal dikurangi cicilan, diskon, dan tidak terbayar. Pembayaran piutang dilakukan di Khanza.")
        { SearchWatermark = "Pasien, No. RM, No. rawat, status, atau cara bayar", TotalColumns = ["sisa"] };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnTambahanBiaya"] = Adjustments(false),
        ["btnPotonganBiaya"] = Adjustments(true),
        ["btnLihatPiutang"] = Receivables,
    };
}
