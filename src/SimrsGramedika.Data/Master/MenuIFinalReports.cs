namespace SimrsGramedika.Data.Master;

public static class MenuIFinalReports
{
    private static ReportSpec DoctorFees(string title, string projection, string grouping,
        (string, string)[] columns, bool inpatientOnly = false)
    {
        var tables = inpatientOnly
            ? new[] { "rawat_inap_dr", "rawat_inap_drpr" }
            : new[] { "rawat_jl_dr", "rawat_jl_drpr", "rawat_inap_dr", "rawat_inap_drpr" };
        var parts = tables.Select(table => $"""
            SELECT t.no_rawat, t.tgl_perawatan AS tanggal, r.no_rkm_medis, p.nm_pasien,
                   t.kd_dokter, d.nm_dokter, k.nm_perawatan, r.status_bayar, t.tarif_tindakandr AS jasa
            FROM {table} t
            JOIN reg_periksa r ON r.no_rawat=t.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN dokter d ON d.kd_dokter=t.kd_dokter
            JOIN {(table.StartsWith("rawat_inap") ? "jns_perawatan_inap" : "jns_perawatan")} k ON k.kd_jenis_prw=t.kd_jenis_prw
            WHERE {(inpatientOnly ? "EXISTS (SELECT 1 FROM kamar_inap i WHERE i.no_rawat=t.no_rawat AND i.tgl_keluar BETWEEN @p1 AND @p2 AND i.stts_pulang NOT IN ('-', 'Pindah Kamar'))" : "r.tgl_registrasi BETWEEN @p1 AND @p2")}
              AND t.tarif_tindakandr>0
              AND (t.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0 OR d.nm_dokter LIKE @p0 OR d.kd_dokter LIKE @p0)
            """);
        return new(title, $"SELECT {projection} FROM ({string.Join(" UNION ALL ", parts)}) q {grouping}",
            columns, ReportPeriod.DateRange,
            (inpatientOnly ? "Periode berdasarkan tanggal pulang. " : "Periode berdasarkan tanggal registrasi. ") +
            "Cakupan jasa dokter dari tindakan. Operasi, laboratorium, radiologi dan aturan pembayaran jasa belum tercakup.")
            { SearchWatermark = "No. rawat, pasien, atau dokter", TotalColumns = ["jasa"] };
    }

    private static ReportSpec AccountRevenue(bool closing)
    {
        var source = MenuIAccountPaymentReports.Reports[closing ? "btnPembayaranAkunBayar" : "btnPembayaranAkunBayar2"];
        var shift = closing ? "q.shift," : "";
        (string, string)[] columns = [..(closing ? new[] { ("shift", "Shift") } : []),
            ("kd_rek", "Kode Rekening"), ("nm_rek", "Rekening"), ("nilai", "Penerimaan")];
        return new(closing ? "Pendapatan Per Akun Closing" : "Pendapatan Per Akun Rekening",
            $"SELECT {shift}q.kd_rek,q.nm_rek,SUM(q.nilai) AS nilai FROM ({source.Sql}) q GROUP BY {shift}q.kd_rek,q.nm_rek ORDER BY {shift}q.kd_rek",
            columns, ReportPeriod.DateRange,
            "Rekap dari rincian penerimaan yang memiliki akun. Piutang dan komponen pendapatan lainnya belum tercakup." +
            (closing ? " Jam shift yang tumpang tindih dapat mengulang penerimaan." : ""))
            { SearchWatermark = source.SearchWatermark, TotalColumns = closing ? [] : ["nilai"] };
    }

    private static ReportSpec ShiftCash()
    {
        var source = MenuIPaymentPointReports.Reports["btnPaymentPoint"];
        return new("Rekap Uang Pershift", $"""
            SELECT q.tanggal_shift,q.shift,q.jenis_bayar,COUNT(*) AS jumlah,SUM(q.nilai) AS nilai
            FROM ({source.Sql}) q
            GROUP BY q.tanggal_shift,q.shift,q.jenis_bayar
            ORDER BY q.tanggal_shift,q.shift,q.jenis_bayar
            """, [("tanggal_shift", "Tanggal Shift"), ("shift", "Shift"), ("jenis_bayar", "Jenis Bayar"),
                ("jumlah", "Transaksi"), ("nilai", "Penerimaan")], ReportPeriod.DateRange,
            "Rekap penerimaan Payment Point. Pengeluaran, saldo dan rekonsiliasi kas belum tercakup.")
            { SearchWatermark = source.SearchWatermark };
    }

    private static readonly (string, string)[] DetailColumns =
        [("tanggal", "Tanggal Tindakan"), ("no_rawat", "No. Rawat"), ("nm_pasien", "Pasien"),
         ("nm_dokter", "Dokter"), ("nm_perawatan", "Tindakan"), ("status_bayar", "Status Bayar"), ("jasa", "Jasa Dokter")];

    private static readonly ReportSpec ReferralFee = new(
        "Fee Rujukan Ranap", """
        SELECT r.tgl_registrasi, r.no_rawat, r.no_rkm_medis, p.nm_pasien,
               m.no_rujuk, m.perujuk, m.dokter_perujuk, m.jm_perujuk
        FROM rujuk_masuk m
        JOIN reg_periksa r ON r.no_rawat=m.no_rawat
        JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
        WHERE r.status_lanjut='Ranap' AND r.tgl_registrasi BETWEEN @p1 AND @p2
          AND (r.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0 OR m.no_rujuk LIKE @p0
               OR m.perujuk LIKE @p0 OR m.dokter_perujuk LIKE @p0)
        ORDER BY r.tgl_registrasi,r.no_rawat
        """, [("tgl_registrasi", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"),
        ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("no_rujuk", "No. Rujukan"),
        ("perujuk", "Perujuk"), ("dokter_perujuk", "Dokter Perujuk"), ("jm_perujuk", "Jasa Perujuk Tercatat")],
        ReportPeriod.DateRange, "Jasa perujuk yang tercatat pada rujukan masuk pasien ranap, berdasarkan tanggal registrasi.")
        { SearchWatermark = "No. rawat, pasien, no. rujukan, atau perujuk", TotalColumns = ["jm_perujuk"] };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnJMDetailDokter"] = DoctorFees("Detail JM Dokter", "q.*", "ORDER BY nm_dokter,tanggal,no_rawat", DetailColumns, inpatientOnly: true),
        ["btnJMDetailDokter2"] = DoctorFees("Detail JM Dokter 2", "q.*", "ORDER BY no_rawat,tanggal,nm_dokter", DetailColumns),
        ["btnRekapJasaDokter"] = DoctorFees("Rekap JM Dokter", "kd_dokter,nm_dokter,COUNT(*) AS jumlah,SUM(jasa) AS jasa",
            "GROUP BY kd_dokter,nm_dokter ORDER BY nm_dokter",
            [("kd_dokter", "Kode Dokter"), ("nm_dokter", "Dokter"), ("jumlah", "Tindakan"), ("jasa", "Jasa Dokter")]),
        ["btnRekapPershift"] = ShiftCash(),
        ["btnPendapatanPerAkun"] = AccountRevenue(false),
        ["btnPendapatanPerAkunClosing"] = AccountRevenue(true),
        ["btnfee_rujukan_ranap"] = ReferralFee,
    };
}
