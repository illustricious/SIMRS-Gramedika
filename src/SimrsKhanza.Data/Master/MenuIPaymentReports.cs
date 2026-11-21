namespace SimrsKhanza.Data.Master;

/// <summary>Read-only summaries of existing outpatient and inpatient billing records.</summary>
public static class MenuIPaymentReports
{
    private static ReportSpec DailyBilling(bool inpatient)
    {
        var visit = inpatient
            ? "SELECT DISTINCT ki.tgl_keluar AS tanggal, ki.no_rawat FROM kamar_inap ki JOIN reg_periksa r ON r.no_rawat=ki.no_rawat JOIN nota_inap n ON n.no_rawat=ki.no_rawat WHERE ki.tgl_keluar BETWEEN @p1 AND @p2 AND ki.stts_pulang NOT IN ('-', 'Pindah Kamar') AND NOT EXISTS (SELECT 1 FROM piutang_pasien pp WHERE pp.no_rawat=ki.no_rawat)"
            : "SELECT r.tgl_registrasi AS tanggal, r.no_rawat FROM reg_periksa r WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.status_lanjut='Ralan' AND NOT EXISTS (SELECT 1 FROM piutang_pasien pp WHERE pp.no_rawat=r.no_rawat)";
        var services = inpatient
            ? "'Ranap Dokter','Ranap Dokter Paramedis','Ranap Paramedis','Ralan Dokter','Ralan Dokter Paramedis','Ralan Paramedis'"
            : "'Ralan Dokter','Ralan Dokter Paramedis','Ralan Paramedis'";
        var allowed = inpatient
            ? "'Registrasi','Obat','Retur Obat','Resep Pulang','Ranap Dokter','Ranap Dokter Paramedis','Ranap Paramedis','Ralan Dokter','Ralan Dokter Paramedis','Ralan Paramedis','Laborat','Radiologi','Operasi','Potongan','Tambahan','Kamar','Harian','Service'"
            : "'Registrasi','Obat','Ralan Dokter','Ralan Dokter Paramedis','Ralan Paramedis','Laborat','Radiologi','Operasi','Potongan','Tambahan'";
        return new ReportSpec(inpatient ? "Rekap Pembayaran Ranap" : "Rekap Pembayaran Ralan", $"""
            SELECT v.tanggal,
                   COALESCE(SUM(CASE WHEN b.status='Registrasi' THEN b.totalbiaya ELSE 0 END),0) AS registrasi,
                   COALESCE(SUM(CASE WHEN b.status IN ({services}) THEN b.totalbiaya ELSE 0 END),0) AS tindakan,
                   COALESCE(SUM(CASE WHEN b.status='Obat' THEN b.totalbiaya ELSE 0 END),0) AS obat,
                   COALESCE(SUM(CASE WHEN b.status='Laborat' THEN b.totalbiaya ELSE 0 END),0) AS laboratorium,
                   COALESCE(SUM(CASE WHEN b.status='Radiologi' THEN b.totalbiaya ELSE 0 END),0) AS radiologi,
                   COALESCE(SUM(CASE WHEN b.status='Operasi' THEN b.totalbiaya ELSE 0 END),0) AS operasi,
                   COALESCE(SUM(CASE WHEN b.status='Tambahan' THEN b.totalbiaya ELSE 0 END),0) AS tambahan,
                   COALESCE(SUM(CASE WHEN b.status='Potongan' THEN b.totalbiaya ELSE 0 END),0) AS potongan,
                   COALESCE(SUM(CASE WHEN b.status IN ('Kamar','Harian','Service','Retur Obat','Resep Pulang') THEN b.totalbiaya ELSE 0 END),0) AS ranap_lain,
                   COALESCE(SUM(CASE WHEN b.status IN ({allowed}) THEN b.totalbiaya ELSE 0 END),0) AS total
            FROM ({visit}) v
            LEFT JOIN billing b ON b.no_rawat=v.no_rawat
            WHERE CAST(v.tanggal AS CHAR) LIKE @p0
            GROUP BY v.tanggal ORDER BY v.tanggal
            """,
            [("tanggal", inpatient ? "Tanggal Pulang" : "Tanggal Registrasi"), ("registrasi", "Registrasi"),
             ("tindakan", "Tindakan"), ("obat", "Obat"), ("laboratorium", "Laboratorium"),
             ("radiologi", "Radiologi"), ("operasi", "Operasi"), ("tambahan", "Tambahan"),
             ("potongan", "Potongan"), ("ranap_lain", "Kamar dan Komponen Ranap"), ("total", "Total Billing")],
            ReportPeriod.DateRange,
            "Rekap billing tercatat, bukan arus kas. " + (inpatient ? "Per tanggal pulang, hanya kunjungan bernota inap;" : "Per tanggal registrasi rawat jalan;") +
            " kunjungan berpiutang dikecualikan. Filter unit/dokter/cara bayar dan cetak legacy belum tersedia.")
            { SearchWatermark = "Tanggal (YYYY-MM-DD)", TotalColumns = ["total"] };
    }

    private static readonly ReportSpec UnitSummary = new(
        "Rekap Pembayaran Per Unit", """
        SELECT poli.kd_poli, poli.nm_poli, COUNT(DISTINCT r.no_rawat) AS jumlah,
               SUM(d.besar_bayar) AS total
        FROM detail_nota_jalan d JOIN reg_periksa r ON r.no_rawat=d.no_rawat
        JOIN poliklinik poli ON poli.kd_poli=r.kd_poli
        WHERE r.status_lanjut='Ralan' AND r.tgl_registrasi BETWEEN @p1 AND @p2
          AND NOT EXISTS (SELECT 1 FROM piutang_pasien pp WHERE pp.no_rawat=r.no_rawat)
          AND (poli.kd_poli LIKE @p0 OR poli.nm_poli LIKE @p0)
        GROUP BY poli.kd_poli,poli.nm_poli HAVING SUM(d.besar_bayar)>0
        ORDER BY poli.nm_poli,poli.kd_poli
        """, [("kd_poli", "Kode"), ("nm_poli", "Unit/Poli"), ("jumlah", "Kunjungan"), ("total", "Pembayaran")],
        ReportPeriod.DateRange, "Pembayaran pada detail nota rawat jalan per unit berdasarkan tanggal registrasi; kunjungan berpiutang dikecualikan.")
        { SearchWatermark = "Kode atau nama poli", TotalColumns = ["total"] };

    private static readonly ReportSpec UnitShift = new(
        "Pembayaran Per Unit", """
        SELECT r.tgl_registrasi AS tanggal, poli.kd_poli, poli.nm_poli, dok.kd_dokter, dok.nm_dokter,
               c.shift, d.nama_bayar, COUNT(DISTINCT r.no_rawat) AS jumlah, SUM(d.besar_bayar) AS total
        FROM reg_periksa r JOIN poliklinik poli ON poli.kd_poli=r.kd_poli
        JOIN dokter dok ON dok.kd_dokter=r.kd_dokter
        JOIN detail_nota_jalan d ON d.no_rawat=r.no_rawat
        JOIN nota_jalan n ON n.no_rawat=r.no_rawat
        JOIN closing_kasir c ON TIMESTAMP(n.tanggal,n.jam) BETWEEN TIMESTAMP(r.tgl_registrasi,c.jam_masuk)
          AND TIMESTAMP(DATE_ADD(r.tgl_registrasi, INTERVAL CASE WHEN c.shift='Malam' THEN 1 ELSE 0 END DAY),c.jam_pulang)
        WHERE r.status_lanjut='Ralan' AND r.tgl_registrasi BETWEEN @p1 AND @p2
          AND NOT EXISTS (SELECT 1 FROM piutang_pasien pp WHERE pp.no_rawat=r.no_rawat)
          AND (poli.kd_poli LIKE @p0 OR poli.nm_poli LIKE @p0 OR dok.nm_dokter LIKE @p0
               OR c.shift LIKE @p0 OR d.nama_bayar LIKE @p0)
        GROUP BY r.tgl_registrasi,poli.kd_poli,poli.nm_poli,dok.kd_dokter,dok.nm_dokter,c.shift,d.nama_bayar
        HAVING SUM(d.besar_bayar)>0
        ORDER BY tanggal,poli.nm_poli,dok.nm_dokter,c.shift,d.nama_bayar
        """, [("tanggal", "Tanggal Registrasi"), ("nm_poli", "Unit/Poli"), ("nm_dokter", "Dokter"),
         ("shift", "Shift"), ("nama_bayar", "Akun Bayar"), ("jumlah", "Kunjungan"), ("total", "Pembayaran")],
        ReportPeriod.DateRange, "Rawat jalan tanpa piutang; nota dicocokkan ke jam shift closing kasir, termasuk shift Malam sampai hari berikutnya. Rentang menggunakan tanggal registrasi. Jika jam shift tumpang tindih, nota dapat muncul pada lebih dari satu shift seperti di Khanza.")
        { SearchWatermark = "Poli, dokter, shift, atau akun bayar", TotalColumns = ["total"] };

    private static readonly ReportSpec IncomingBills = new(
        "Tagihan Masuk", """
        SELECT b.tgl_byr AS tanggal, b.no_rawat, b.no, b.nm_perawatan, b.status,
               NULLIF(b.biaya,0) AS biaya, NULLIF(b.jumlah,0) AS jumlah,
               NULLIF(b.tambahan,0) AS tambahan, NULLIF(b.totalbiaya,0) AS total
        FROM billing b
        WHERE b.tgl_byr BETWEEN @p1 AND @p2
          AND NOT EXISTS (SELECT 1 FROM piutang_pasien pp WHERE pp.no_rawat=b.no_rawat)
          AND b.no NOT LIKE '%Tagihan + PPN%' AND b.no NOT LIKE '%Total Tagihan%'
          AND (b.no_rawat LIKE @p0 OR b.no LIKE @p0 OR b.nm_perawatan LIKE @p0 OR b.status LIKE @p0)
        ORDER BY b.no_rawat,b.noindex
        """, [("tanggal", "Tanggal Billing"), ("no_rawat", "No. Rawat"), ("no", "No./Keterangan"),
         ("nm_perawatan", "Uraian"), ("status", "Status"), ("biaya", "Biaya"), ("jumlah", "Jumlah"),
         ("tambahan", "Tambahan"), ("total", "Total")], ReportPeriod.DateRange,
        "Baris billing tercatat menurut tanggal billing, tanpa kunjungan berpiutang. Termasuk baris subtotal Khanza; menjumlahkan semua baris dapat menghitung biaya lebih dari sekali.")
        { SearchWatermark = "No. rawat, uraian, atau status" };

    private static readonly ReportSpec ReceivablesByAccount = new(
        "Piutang Per Akun Piutang", """
        SELECT n.layanan,n.tanggal,n.no_nota,pp.no_rawat,p.nm_pasien,pp.uangmuka,
               pp.totalpiutang-pp.uangmuka AS piutang_awal, dp.nama_bayar,dp.totalpiutang AS nilai
        FROM (
            SELECT 'Ralan' AS layanan,no_rawat,no_nota,tanggal FROM nota_jalan
            UNION ALL
            SELECT 'Ranap' AS layanan,no_rawat,no_nota,tanggal FROM nota_inap
        ) n
        JOIN piutang_pasien pp ON pp.no_rawat=n.no_rawat
        JOIN pasien p ON p.no_rkm_medis=pp.no_rkm_medis
        LEFT JOIN detail_piutang_pasien dp ON dp.no_rawat=pp.no_rawat
        WHERE n.tanggal BETWEEN @p1 AND @p2
          AND (p.nm_pasien LIKE @p0 OR n.no_nota LIKE @p0 OR pp.no_rawat LIKE @p0 OR dp.nama_bayar LIKE @p0)
        ORDER BY n.tanggal,n.no_nota,dp.nama_bayar
        """, [("layanan", "Layanan"), ("tanggal", "Tanggal Nota"), ("no_nota", "No. Nota"),
         ("no_rawat", "No. Rawat"), ("nm_pasien", "Pasien"), ("uangmuka", "Uang Muka"),
         ("piutang_awal", "Piutang Setelah Uang Muka"), ("nama_bayar", "Akun Piutang"), ("nilai", "Piutang Per Akun")],
        ReportPeriod.DateRange, "Berdasarkan tanggal nota rawat jalan dan inap. Uang muka dan piutang awal berulang untuk setiap akun; total di bawah hanya menjumlahkan piutang per akun. Jika satu kunjungan mempunyai kedua nota, masing-masing ditampilkan mengikuti laporan Khanza.")
        { SearchWatermark = "Pasien, nota, No. rawat, atau akun piutang", TotalColumns = ["nilai"] };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnTagihanRalanPerhari"] = DailyBilling(false),
        ["btnTagihanRanapPerhari"] = DailyBilling(true),
        ["btnPembayaranPerUnit"] = UnitShift,
        ["btnRekapPembayaranPerUnit"] = UnitSummary,
        ["btnTagihanMasuk"] = IncomingBills,
        ["btnPiutangPerAkunPiutang"] = ReceivablesByAccount,
    };
}
