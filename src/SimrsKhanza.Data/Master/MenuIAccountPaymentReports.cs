namespace SimrsKhanza.Data.Master;

public static class MenuIAccountPaymentReports
{
    private static ReportSpec ByAccountPeriod(string title, bool byPaymentName, bool byShift, bool bySource)
    {
        var account = byPaymentName ? "ab.nama_bayar" : "rek.nm_rek";
        var dateFilter = byShift ? "t.tgl_bayar BETWEEN @p1 AND DATE_ADD(@p2, INTERVAL 1 DAY)" : "t.tgl_bayar BETWEEN @p1 AND @p2";
        var shift = byShift ? "c.shift" : "''";
        return new(title, $"""
        SELECT q.tgl_bayar,q.nota,q.nama_pasien,q.jenis,q.kd_rek,q.nm_rek,{shift} AS shift,SUM(q.nilai) AS nilai
        FROM (
          SELECT t.tgl_bayar,ni.no_nota AS nota,t.nama_pasien,'Rawat Inap' AS jenis,
                 ab.kd_rek,{account} AS nm_rek,d.besar_bayar AS nilai
          FROM tagihan_sadewa t JOIN nota_inap ni ON ni.no_rawat=t.no_nota
          JOIN detail_nota_inap d ON d.no_rawat=ni.no_rawat JOIN akun_bayar ab ON ab.nama_bayar=d.nama_bayar
          LEFT JOIN rekening rek ON rek.kd_rek=ab.kd_rek
          WHERE {dateFilter}
          UNION ALL
          SELECT t.tgl_bayar,nj.no_nota,t.nama_pasien,'Rawat Jalan',ab.kd_rek,{account},d.besar_bayar
          FROM tagihan_sadewa t JOIN nota_jalan nj ON nj.no_rawat=t.no_nota
          JOIN detail_nota_jalan d ON d.no_rawat=nj.no_rawat JOIN akun_bayar ab ON ab.nama_bayar=d.nama_bayar
          LEFT JOIN rekening rek ON rek.kd_rek=ab.kd_rek
          WHERE {dateFilter} AND NOT EXISTS(SELECT 1 FROM nota_inap z WHERE z.no_rawat=t.no_nota)
          UNION ALL
          SELECT t.tgl_bayar,s.nota_jual,t.nama_pasien,'Penjualan Farmasi',ab.kd_rek,{account},
                 COALESCE((SELECT SUM(d.total) FROM detailjual d WHERE d.nota_jual=s.nota_jual),0)+COALESCE(s.ongkir,0)+s.ppn
          FROM tagihan_sadewa t JOIN penjualan s ON s.nota_jual=t.no_nota
          JOIN akun_bayar ab ON ab.nama_bayar=s.nama_bayar LEFT JOIN rekening rek ON rek.kd_rek=ab.kd_rek
          WHERE {dateFilter} AND NOT EXISTS(SELECT 1 FROM nota_inap z WHERE z.no_rawat=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM nota_jalan z WHERE z.no_rawat=t.no_nota)
          UNION ALL
          SELECT t.tgl_bayar,d.no_deposit,t.nama_pasien,'Deposit',ab.kd_rek,{account},d.besar_deposit
          FROM tagihan_sadewa t JOIN deposit d ON d.no_deposit=t.no_nota
          JOIN akun_bayar ab ON ab.nama_bayar=d.nama_bayar LEFT JOIN rekening rek ON rek.kd_rek=ab.kd_rek
          WHERE {dateFilter} AND NOT EXISTS(SELECT 1 FROM nota_inap z WHERE z.no_rawat=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM nota_jalan z WHERE z.no_rawat=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM penjualan z WHERE z.nota_jual=t.no_nota)
          UNION ALL
          SELECT t.tgl_bayar,x.no_masuk,t.nama_pasien,'Pemasukan Lain',k.kd_rek2,rek.nm_rek,x.besar
          FROM tagihan_sadewa t JOIN pemasukan_lain x ON x.no_masuk=t.no_nota
          JOIN kategori_pemasukan_lain k ON k.kode_kategori=x.kode_kategori LEFT JOIN rekening rek ON rek.kd_rek=k.kd_rek2
          WHERE {dateFilter} AND NOT EXISTS(SELECT 1 FROM nota_inap z WHERE z.no_rawat=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM nota_jalan z WHERE z.no_rawat=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM penjualan z WHERE z.nota_jual=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM deposit z WHERE z.no_deposit=t.no_nota)
          UNION ALL
          SELECT t.tgl_bayar,x.no_pembayaran,t.nama_pasien,'Lab Kesling',ab.kd_rek,{account},d.besar_bayar
          FROM tagihan_sadewa t JOIN labkesling_pembayaran_pengujian_sampel x ON x.no_pembayaran=t.no_nota
          JOIN labkesling_detail_pembayaran_pengujian_sampel d ON d.no_pembayaran=x.no_pembayaran
          JOIN akun_bayar ab ON ab.nama_bayar=d.nama_bayar LEFT JOIN rekening rek ON rek.kd_rek=ab.kd_rek
          WHERE {dateFilter} AND NOT EXISTS(SELECT 1 FROM nota_inap z WHERE z.no_rawat=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM nota_jalan z WHERE z.no_rawat=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM penjualan z WHERE z.nota_jual=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM deposit z WHERE z.no_deposit=t.no_nota)
            AND NOT EXISTS(SELECT 1 FROM pemasukan_lain z WHERE z.no_masuk=t.no_nota)
        ) q
        {(byShift ? """
          JOIN closing_kasir c ON (
            DATE(q.tgl_bayar) BETWEEN DATE(@p1) AND DATE(@p2)
            AND TIME(q.tgl_bayar)>=c.jam_masuk
            AND (c.shift='Malam' OR TIME(q.tgl_bayar)<=c.jam_pulang)
          ) OR (
            c.shift='Malam' AND TIME(q.tgl_bayar)<=c.jam_pulang
            AND DATE_SUB(DATE(q.tgl_bayar), INTERVAL 1 DAY) BETWEEN DATE(@p1) AND DATE(@p2)
          )
          """ : "")}
        WHERE q.nota LIKE @p0 OR q.nama_pasien LIKE @p0 OR q.kd_rek LIKE @p0 OR q.nm_rek LIKE @p0 OR q.jenis LIKE @p0
        GROUP BY q.tgl_bayar,q.nota,q.nama_pasien,q.jenis,q.kd_rek,q.nm_rek{(byShift ? ",c.shift" : "")}
        ORDER BY {(bySource ? "q.jenis," : "")}q.tgl_bayar,q.nota,q.kd_rek
        """, [("tgl_bayar", "Waktu Bayar"), ("nota", "No. Nota"), ("nama_pasien", "Nama"),
         ("jenis", "Jenis Transaksi"), ("kd_rek", "Kode Rekening"), ("nm_rek", byPaymentName ? "Akun Bayar" : "Rekening"),
         ..(byShift ? new[] { ("shift", "Shift") } : []), ("nilai", "Nilai")],
        ReportPeriod.DateRange, "Rincian nota ralan/ranap, farmasi, deposit, pemasukan lain dan lab kesling. Tata letak dan filter khusus Khanza belum tersedia."
        + (byShift ? " Jika jam shift tumpang tindih, penerimaan bisa muncul pada beberapa shift." : ""))
        { SearchWatermark = "Nota, nama, jenis transaksi, atau rekening", TotalColumns = byShift ? [] : ["nilai"] };
    }

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnPembayaranAkunBayar"] = ByAccountPeriod("Pembayaran Per Akun Bayar", false, true, false),
        ["btnPembayaranAkunBayar2"] = ByAccountPeriod("Pembayaran Per Akun Bayar 2", false, false, false),
        ["btnPembayaranAkunBayar3"] = ByAccountPeriod("Pembayaran Per Akun Bayar 3", false, false, false),
        ["btnPembayaranAkunBayar4"] = ByAccountPeriod("Pembayaran Per Akun Bayar 4", true, false, true),
        ["btnPembayaranAkunBayar5"] = ByAccountPeriod("Pembayaran Per Akun Bayar 5", true, false, true),
    };
}
