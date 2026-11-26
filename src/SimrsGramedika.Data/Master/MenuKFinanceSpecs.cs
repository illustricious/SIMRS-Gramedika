namespace SimrsGramedika.Data.Master;

/// <summary>
/// Menu K, last batch: accounting views derived from the journal and payment tables. Everything is read-only;
/// journals are posted by Khanza transactions or by the manual Posting Jurnal screen.
/// </summary>
public static class MenuKFinanceSpecs
{
    public const string PostingJurnalKey = "btnPostingJurnal";

    private static ReportSpec R(string title, string sql, (string, string)[] columns, string[]? totals = null, string? hint = null,
        ReportPeriod period = ReportPeriod.DateRange) => MenuKSpecs.Report(title, sql, columns, period, hint, totals);

    private static string AccountSettingsSql() =>
        "SELECT u.tabel,u.pengaturan,u.kd_rek,r.nm_rek FROM (" + string.Join(" UNION ALL ",
            MenuKAccountColumns.Tables.SelectMany(table => table.Columns.Select(column =>
                $"SELECT '{table.Table}' AS tabel,'{column}' AS pengaturan,t.`{column}` AS kd_rek FROM `{table.Table}` t")))
        + ") u LEFT JOIN rekening r ON r.kd_rek=u.kd_rek WHERE CONCAT_WS(' ',u.tabel,u.pengaturan,u.kd_rek,r.nm_rek) LIKE @p0 "
        + "ORDER BY u.tabel,u.pengaturan";

    private const string ExcludedBillingGroups = "b.status NOT LIKE 'Ttl%' AND b.status NOT IN ('-','Tagihan')";

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnPengaturanRekening"] = R("Pengaturan Rekening", AccountSettingsSql(),
            [("tabel", "Tabel Pengaturan"), ("pengaturan", "Pengaturan"), ("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening")],
            hint: "Peta rekening akuntansi yang dipakai transaksi. Ubah pemetaan di Khanza; transaksi Avalonia membaca nilai ini saat memposting jurnal.",
            period: ReportPeriod.None),
        ["btnSaldoAkunPerBulan"] = R("Saldo Akun Per Bulan", """
            SELECT DATE_FORMAT(j.tgl_jurnal,'%Y-%m') AS bulan,d.kd_rek,r.nm_rek,r.balance,SUM(d.debet) AS debet,SUM(d.kredit) AS kredit,
                   SUM(CASE WHEN r.balance='K' THEN d.kredit-d.debet ELSE d.debet-d.kredit END) AS mutasi
            FROM detailjurnal d JOIN jurnal j ON j.no_jurnal=d.no_jurnal LEFT JOIN rekening r ON r.kd_rek=d.kd_rek
            WHERE j.tgl_jurnal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',d.kd_rek,r.nm_rek) LIKE @p0
            GROUP BY bulan,d.kd_rek,r.nm_rek,r.balance ORDER BY d.kd_rek,bulan
            """,
            [("bulan", "Bulan"), ("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening"), ("balance", "Saldo Normal"),
             ("debet", "Debet"), ("kredit", "Kredit"), ("mutasi", "Mutasi Bulan")],
            ["debet", "kredit"], "Mutasi per bulan dari jurnal. Saldo awal tahun ada di Rekening Tahun."),
        ["btnKlaimRawatJalan"] = R("Klaim Rawat Jalan", """
            SELECT r.no_rawat,r.tgl_registrasi,r.no_rkm_medis,s.nm_pasien,j.png_jawab,b.no_sep,b.no_kartu,p.status AS status_piutang,
                   p.totalpiutang,p.sisapiutang
            FROM reg_periksa r JOIN pasien s ON s.no_rkm_medis=r.no_rkm_medis LEFT JOIN penjab j ON j.kd_pj=r.kd_pj
            LEFT JOIN bridging_sep b ON b.no_rawat=r.no_rawat AND b.jnspelayanan='2'
            LEFT JOIN piutang_pasien p ON p.no_rawat=r.no_rawat
            WHERE r.status_lanjut='Ralan' AND r.tgl_registrasi BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',r.no_rawat,r.no_rkm_medis,s.nm_pasien,j.png_jawab,b.no_sep,b.no_kartu) LIKE @p0
            ORDER BY r.tgl_registrasi DESC,r.no_rawat
            """,
            [("no_rawat", "No. Rawat"), ("tgl_registrasi", "Tgl. Registrasi"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("png_jawab", "Cara Bayar"), ("no_sep", "No. SEP"), ("no_kartu", "No. Kartu"), ("status_piutang", "Status Piutang"),
             ("totalpiutang", "Total Piutang"), ("sisapiutang", "Sisa Piutang")],
            ["totalpiutang", "sisapiutang"], "Kunjungan rawat jalan beserta SEP dan piutangnya. Klaim diajukan melalui bridging di Khanza."),
        ["btnOmsetPenerimaan"] = R("Penerimaan/Omset/Kas Masuk", $"""
            SELECT b.tgl_byr,b.status AS kelompok,COUNT(DISTINCT b.no_rawat) AS jumlah_pasien,SUM(b.totalbiaya) AS total
            FROM billing b
            WHERE b.tgl_byr BETWEEN @p1 AND @p2 AND {ExcludedBillingGroups} AND b.status LIKE @p0
            GROUP BY b.tgl_byr,b.status ORDER BY b.tgl_byr DESC,b.status
            """,
            [("tgl_byr", "Tanggal Bayar"), ("kelompok", "Kelompok Biaya"), ("jumlah_pasien", "Jumlah Pasien"), ("total", "Total")],
            ["total"], "Dihitung dari rincian billing yang tersimpan saat pembayaran, per tanggal dan kelompok biaya."),
        ["btnPendapatanPerCaraBayar"] = R("Pendapatan Per Cara Bayar", $"""
            SELECT COALESCE(j.png_jawab,'-') AS png_jawab,COUNT(DISTINCT b.no_rawat) AS jumlah_pasien,SUM(b.totalbiaya) AS total
            FROM billing b LEFT JOIN reg_periksa r ON r.no_rawat=b.no_rawat LEFT JOIN penjab j ON j.kd_pj=r.kd_pj
            WHERE b.tgl_byr BETWEEN @p1 AND @p2 AND {ExcludedBillingGroups} AND COALESCE(j.png_jawab,'-') LIKE @p0
            GROUP BY j.kd_pj,j.png_jawab ORDER BY j.png_jawab
            """,
            [("png_jawab", "Cara Bayar"), ("jumlah_pasien", "Jumlah Pasien"), ("total", "Total Pendapatan")],
            ["total"], "Dihitung dari rincian billing yang tersimpan saat pembayaran, dikelompokkan menurut cara bayar kunjungan."),
        ["btnPengeluaranPengeluaran"] = R("Pengeluaran-pengeluaran/Kas Keluar", """
            SELECT k.* FROM (
              SELECT 'Pengeluaran Harian' AS sumber,no_keluar AS dokumen,tanggal,biaya AS nominal,keterangan FROM pengeluaran_harian
              UNION ALL SELECT 'Bayar Pesan Obat/BHP',no_faktur,tgl_bayar,besar_bayar,keterangan FROM bayar_pemesanan
              UNION ALL SELECT 'Bayar Pesan Non Medis',no_faktur,tgl_bayar,besar_bayar,keterangan FROM bayar_pemesanan_non_medis
              UNION ALL SELECT 'Bayar Pesan Aset/Inventaris',no_faktur,tgl_bayar,besar_bayar,keterangan FROM bayar_pemesanan_inventaris
              UNION ALL SELECT 'Bayar Pesan Dapur',no_faktur,tgl_bayar,besar_bayar,keterangan FROM bayar_pemesanan_dapur
              UNION ALL SELECT 'Bayar Pesan Toko',no_faktur,tgl_bayar,besar_bayar,keterangan FROM toko_bayar_pemesanan
              UNION ALL SELECT 'Bayar JM Dokter',no_bayar,tanggal,besar_bayar,keterangan FROM bayar_jm_dokter
              UNION ALL SELECT 'Bayar Beban Hutang Lain',no_hutang,tgl_bayar,besar_cicilan,keterangan FROM bayar_beban_hutang_lain
            ) k
            WHERE k.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',k.sumber,k.dokumen,k.keterangan) LIKE @p0
            ORDER BY k.tanggal DESC,k.sumber,k.dokumen
            """,
            [("tanggal", "Tanggal"), ("sumber", "Sumber"), ("dokumen", "No. Dokumen"), ("nominal", "Nominal"), ("keterangan", "Keterangan")],
            ["nominal"], "Gabungan pengeluaran kas dari pengeluaran harian dan pembayaran hutang, JM dokter, dan toko."),
        ["btnCashFlow"] = R("Cash Flow", """
            SELECT DATE_FORMAT(j.tgl_jurnal,'%Y-%m') AS bulan,d.kd_rek,r.nm_rek,SUM(d.debet) AS kas_masuk,SUM(d.kredit) AS kas_keluar,
                   SUM(d.debet-d.kredit) AS arus_bersih
            FROM detailjurnal d JOIN jurnal j ON j.no_jurnal=d.no_jurnal LEFT JOIN rekening r ON r.kd_rek=d.kd_rek
            WHERE d.kd_rek IN (SELECT kd_rek FROM akun_bayar WHERE kd_rek IS NOT NULL)
              AND j.tgl_jurnal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',d.kd_rek,r.nm_rek) LIKE @p0
            GROUP BY bulan,d.kd_rek,r.nm_rek ORDER BY bulan,d.kd_rek
            """,
            [("bulan", "Bulan"), ("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening"), ("kas_masuk", "Kas Masuk"),
             ("kas_keluar", "Kas Keluar"), ("arus_bersih", "Arus Bersih")],
            ["kas_masuk", "kas_keluar", "arus_bersih"], "Arus kas pada rekening kas/bank yang terdaftar di Akun Bayar, dihitung dari jurnal."),
        ["btnLabaRugi"] = R("Keuangan", """
            SELECT r.kd_rek,r.nm_rek,r.balance,SUM(d.debet) AS debet,SUM(d.kredit) AS kredit,SUM(d.kredit-d.debet) AS laba_rugi
            FROM detailjurnal d JOIN jurnal j ON j.no_jurnal=d.no_jurnal JOIN rekening r ON r.kd_rek=d.kd_rek
            WHERE r.tipe='R' AND j.tgl_jurnal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',r.kd_rek,r.nm_rek) LIKE @p0
            GROUP BY r.kd_rek,r.nm_rek,r.balance ORDER BY r.kd_rek
            """,
            [("kd_rek", "Kode Rekening"), ("nm_rek", "Nama Rekening"), ("balance", "Saldo Normal"), ("debet", "Debet"),
             ("kredit", "Kredit"), ("laba_rugi", "Laba/(Rugi)")],
            ["debet", "kredit", "laba_rugi"], "Rekening tipe R dari jurnal. Total Laba/(Rugi) positif berarti laba periode."),
    };
}
