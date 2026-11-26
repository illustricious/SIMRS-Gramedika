namespace SimrsGramedika.Data.Master;

public static class MenuIPaymentPointReports
{
    private const string DisplayJoin = """
        LEFT JOIN nota_inap ni ON ni.no_rawat=t.no_nota
        LEFT JOIN nota_jalan nj ON nj.no_rawat=t.no_nota
        LEFT JOIN (SELECT nik,MIN(nama) AS nama FROM pegawai GROUP BY nik) pg ON pg.nik=t.petugas
        """;

    private static readonly ReportSpec PaymentByPeriod = new(
        "Payment Point 2", $"""
        SELECT t.tgl_bayar,COALESCE(NULLIF(ni.no_nota,''),NULLIF(nj.no_nota,''),t.no_nota) AS nota,
               t.nama_pasien,t.jenis_bayar,FLOOR(t.jumlah_bayar+0.5) AS nilai,
               t.petugas,pg.nama AS nama_petugas
        FROM tagihan_sadewa t {DisplayJoin}
        WHERE t.tgl_bayar BETWEEN @p1 AND @p2
          AND (t.nama_pasien LIKE @p0 OR t.no_nota LIKE @p0 OR t.petugas LIKE @p0 OR pg.nama LIKE @p0)
        ORDER BY t.tgl_bayar,t.no_nota
        """, [("tgl_bayar", "Waktu Bayar"), ("nota", "No. Nota"), ("nama_pasien", "Nama"),
         ("jenis_bayar", "Jenis Bayar"), ("nilai", "Pembayaran"), ("petugas", "Kode Petugas"),
         ("nama_petugas", "Petugas")], ReportPeriod.DateRange,
        "Daftar tagihan_sadewa sepanjang tanggal terpilih; jumlah dibulatkan per baris seperti Khanza. Pemilihan jam dan cetak legacy belum tersedia.")
        { SearchWatermark = "Nama, nomor nota, atau petugas", TotalColumns = ["nilai"] };

    private static readonly ReportSpec PaymentByShift = new(
        "Payment Point", $"""
        SELECT q.tanggal_shift,q.shift,q.tgl_bayar,q.nota,q.nama_pasien,q.jenis_bayar,q.nilai,q.petugas,q.nama_petugas
        FROM (
          SELECT DATE(t.tgl_bayar) AS tanggal_shift,c.shift,t.tgl_bayar,
                 COALESCE(NULLIF(ni.no_nota,''),NULLIF(nj.no_nota,''),t.no_nota) AS nota,
                 t.nama_pasien,t.jenis_bayar,FLOOR(t.jumlah_bayar+0.5) AS nilai,t.petugas,pg.nama AS nama_petugas
          FROM tagihan_sadewa t JOIN closing_kasir c ON t.tgl_bayar BETWEEN
               TIMESTAMP(DATE(t.tgl_bayar),c.jam_masuk) AND
               TIMESTAMP(DATE_ADD(DATE(t.tgl_bayar),INTERVAL CASE WHEN c.shift='Malam' THEN 1 ELSE 0 END DAY),c.jam_pulang)
          {DisplayJoin}
          WHERE t.tgl_bayar BETWEEN @p1 AND DATE_ADD(@p2,INTERVAL 1 DAY)
            AND (t.nama_pasien LIKE @p0 OR t.no_nota LIKE @p0 OR t.petugas LIKE @p0 OR pg.nama LIKE @p0 OR c.shift LIKE @p0)
          UNION ALL
          SELECT DATE_SUB(DATE(t.tgl_bayar),INTERVAL 1 DAY),c.shift,t.tgl_bayar,
                 COALESCE(NULLIF(ni.no_nota,''),NULLIF(nj.no_nota,''),t.no_nota),
                 t.nama_pasien,t.jenis_bayar,FLOOR(t.jumlah_bayar+0.5),t.petugas,pg.nama
          FROM tagihan_sadewa t JOIN closing_kasir c ON c.shift='Malam' AND t.tgl_bayar BETWEEN
               TIMESTAMP(DATE_SUB(DATE(t.tgl_bayar),INTERVAL 1 DAY),c.jam_masuk) AND TIMESTAMP(DATE(t.tgl_bayar),c.jam_pulang)
          {DisplayJoin}
          WHERE t.tgl_bayar BETWEEN @p1 AND DATE_ADD(@p2,INTERVAL 1 DAY)
            AND (t.nama_pasien LIKE @p0 OR t.no_nota LIKE @p0 OR t.petugas LIKE @p0 OR pg.nama LIKE @p0 OR c.shift LIKE @p0)
        ) q
        WHERE q.tanggal_shift BETWEEN @p1 AND @p2
        ORDER BY q.tanggal_shift,q.shift,q.tgl_bayar,q.nota
        """, [("tanggal_shift", "Tanggal Shift"), ("shift", "Shift"), ("tgl_bayar", "Waktu Bayar"),
         ("nota", "No. Nota"), ("nama_pasien", "Nama"), ("jenis_bayar", "Jenis Bayar"),
         ("nilai", "Pembayaran"), ("petugas", "Kode Petugas"), ("nama_petugas", "Petugas")],
        ReportPeriod.DateRange, "Tanggal filter adalah tanggal mulai shift; shift malam berlanjut ke hari berikutnya. Shift bertumpang tindih dapat mengulang pembayaran seperti Khanza. Pengaturan modal awal dan cetak legacy belum tersedia.")
        { SearchWatermark = "Nama, nomor nota, petugas, atau shift", TotalColumns = ["nilai"] };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnPaymentPoint"] = PaymentByShift,
        ["btnPaymentPoint2"] = PaymentByPeriod,
    };
}
