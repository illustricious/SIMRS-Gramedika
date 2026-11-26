namespace SimrsGramedika.Data.Master;

/// <summary>Menu K, second batch of read-only lists: vendor payables, company receivables, other debts, and bank payment feeds.</summary>
public static class MenuKPayablesSpecs
{
    private static ReportSpec R(string title, string sql, (string, string)[] columns, string[]? totals = null, string? hint = null,
        ReportPeriod period = ReportPeriod.DateRange) => MenuKSpecs.Report(title, sql, columns, period, hint, totals);

    private const string OpenStatuses = "'Belum Dibayar','Belum Lunas','Titip Faktur'";

    private static readonly (string, string)[] HutangColumns =
    [
        ("no_faktur", "No. Faktur"), ("tgl_faktur", "Tgl. Faktur"), ("tgl_tempo", "Jatuh Tempo"), ("nama_suplier", "Suplier"),
        ("tagihan", "Tagihan"), ("dibayar", "Sudah Dibayar"), ("sisa", "Sisa"), ("status", "Status"),
    ];

    private static readonly (string, string)[] TitipColumns =
    [
        ("no_tagihan", "No. Tagihan"), ("tanggal", "Tanggal"), ("status", "Status"), ("jumlah", "Jumlah Faktur"),
        ("nip", "NIP"), ("nama", "Petugas"), ("keterangan", "Keterangan"),
    ];

    private static readonly (string, string)[] PayColumns =
    [
        ("tgl_bayar", "Tgl. Bayar"), ("no_faktur", "No. Faktur"), ("nama_suplier", "Suplier"), ("besar_bayar", "Dibayar"),
        ("nama_bayar", "Akun Bayar"), ("no_bukti", "No. Bukti"), ("nip", "NIP"), ("keterangan", "Keterangan"),
    ];

    private static string TitipSql(string titip, string detail, string filter) => $"""
        SELECT h.no_tagihan,h.tanggal,h.status,(SELECT COUNT(*) FROM `{detail}` d WHERE d.no_tagihan=h.no_tagihan) AS jumlah,
               h.nip,p.nama,h.keterangan
        FROM `{titip}` h LEFT JOIN pegawai p ON p.nik=h.nip
        WHERE h.tanggal BETWEEN @p1 AND @p2 {filter} AND CONCAT_WS(' ',h.no_tagihan,h.status,h.nip,p.nama,h.keterangan) LIKE @p0
        ORDER BY h.tanggal DESC,h.no_tagihan
        """;

    private static string Paid(string pay) => $"COALESCE((SELECT SUM(b.besar_bayar) FROM `{pay}` b WHERE b.no_faktur=h.no_faktur),0)";

    private static ReportSpec Hutang(string label, string order, string supplier, string pay) => R($"Hutang {label}", $"""
        SELECT h.no_faktur,h.tgl_faktur,h.tgl_tempo,s.nama_suplier,h.tagihan,{Paid(pay)} AS dibayar,h.tagihan-{Paid(pay)} AS sisa,h.status
        FROM `{order}` h LEFT JOIN `{supplier}` s ON s.kode_suplier=h.kode_suplier
        WHERE h.status IN ({OpenStatuses}) AND h.tgl_faktur BETWEEN @p1 AND @p2
          AND CONCAT_WS(' ',h.no_faktur,h.no_order,s.nama_suplier,h.status) LIKE @p0
        ORDER BY h.tgl_tempo,h.no_faktur
        """, HutangColumns, ["tagihan", "dibayar", "sisa"]);

    private static ReportSpec Ringkasan(string label, string order, string supplier, string pay) => R($"Ringkasan Hutang Vendor {label}", $"""
        SELECT COALESCE(s.nama_suplier,h.kode_suplier) AS nama_suplier,COUNT(*) AS jumlah,SUM(h.tagihan) AS tagihan,
               SUM({Paid(pay)}) AS dibayar,SUM(h.tagihan-{Paid(pay)}) AS sisa
        FROM `{order}` h LEFT JOIN `{supplier}` s ON s.kode_suplier=h.kode_suplier
        WHERE h.status IN ({OpenStatuses}) AND h.tgl_faktur BETWEEN @p1 AND @p2 AND COALESCE(s.nama_suplier,h.kode_suplier) LIKE @p0
        GROUP BY h.kode_suplier,s.nama_suplier ORDER BY s.nama_suplier
        """,
        [("nama_suplier", "Suplier"), ("jumlah", "Jumlah Faktur"), ("tagihan", "Tagihan"), ("dibayar", "Sudah Dibayar"), ("sisa", "Sisa Hutang")],
        ["jumlah", "tagihan", "dibayar", "sisa"]);

    private static ReportSpec Titip(string label, string titip, string detail) =>
        R($"Titip Faktur/Tagihan {label}", TitipSql(titip, detail, ""), TitipColumns);

    private static ReportSpec Validasi(string label, string titip, string detail) =>
        R($"Validasi Titip Faktur/Tagihan {label}", TitipSql(titip, detail, "AND h.status='Ditagihkan'"), TitipColumns,
            hint: "Tagihan yang masih berstatus Ditagihkan. Validasi dan pembayaran dicatat di Khanza.");

    private static ReportSpec Bayar(string label, string order, string supplier, string pay) => R($"Bayar Pesan {label}", $"""
        SELECT b.tgl_bayar,b.no_faktur,s.nama_suplier,b.besar_bayar,b.nama_bayar,b.no_bukti,b.nip,b.keterangan
        FROM `{pay}` b LEFT JOIN `{order}` h ON h.no_faktur=b.no_faktur LEFT JOIN `{supplier}` s ON s.kode_suplier=h.kode_suplier
        WHERE b.tgl_bayar BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',b.no_faktur,s.nama_suplier,b.nama_bayar,b.no_bukti,b.keterangan) LIKE @p0
        ORDER BY b.tgl_bayar DESC,b.no_faktur
        """, PayColumns, ["besar_bayar"]);

    private static readonly (string, string)[] BankColumns =
    [
        ("no_nota", "No. Nota"), ("tgl_registrasi", "Tgl. Registrasi"), ("tgl_closing", "Tgl. Closing"), ("no_rkm_medis", "No. RM"),
        ("nm_pasien", "Pasien"), ("jenis", "Jenis"), ("besar_bayar", "Besar Bayar"), ("status_bayar", "Status Bayar"),
        ("kasir", "Kasir"), ("diupdatebank", "Update Bank"), ("referensi", "Referensi"),
    ];

    private static ReportSpec Bank(string title, string table, string number, string type) => R(title, $"""
        SELECT t.{number} AS no_nota,t.tgl_registrasi,t.tgl_closing,t.no_rkm_medis,t.nm_pasien,t.{type} AS jenis,t.besar_bayar,
               t.status_bayar,t.kasir,t.diupdatebank,t.referensi
        FROM `{table}` t
        WHERE t.tgl_registrasi BETWEEN @p1 AND @p2
          AND CONCAT_WS(' ',t.{number},t.no_rkm_medis,t.nm_pasien,t.status_bayar,t.kasir,t.referensi) LIKE @p0
        ORDER BY t.tgl_registrasi DESC,t.{number}
        """, BankColumns, ["besar_bayar"], "Data tagihan bank yang dikirim Khanza. Daftar hanya-baca.");

    private static readonly (string, string)[] CompanyColumns =
    [
        ("no_piutang", "No. Piutang"), ("tgl_piutang", "Tanggal"), ("jatuh_tempo", "Jatuh Tempo"), ("nama_perusahaan", "Perusahaan"),
        ("grand_total", "Grand Total"), ("jasa_menejemen", "Jasa Manajemen"), ("ppn", "PPN"), ("pph", "PPh"),
        ("totalpiutang", "Total Piutang"), ("sisapiutang", "Sisa Piutang"), ("status", "Status"), ("keterangan", "Keterangan"),
    ];

    private static ReportSpec Company(string title, string filter) => R(title, $"""
        SELECT h.no_piutang,h.tgl_piutang,h.jatuh_tempo,c.nama_perusahaan,h.grand_total,h.jasa_menejemen,h.ppn,h.pph,
               h.totalpiutang,h.sisapiutang,h.status,h.keterangan
        FROM piutang_jasa_perusahaan h LEFT JOIN perusahaan_pasien c ON c.kode_perusahaan=h.kode_perusahaan
        WHERE h.tgl_piutang BETWEEN @p1 AND @p2 {filter} AND CONCAT_WS(' ',h.no_piutang,c.nama_perusahaan,h.status,h.keterangan) LIKE @p0
        ORDER BY h.tgl_piutang DESC,h.no_piutang
        """, CompanyColumns, ["totalpiutang", "sisapiutang"]);

    private static readonly ReportSpec Briva = R("Pembayaran BRIVA", """
        SELECT t.no_tagihan,t.tgl_registrasi,t.tgl_closing,t.no_rkm_medis,t.nm_pasien,t.status_tagihan AS jenis,t.besar_bayar,
               t.status_bayar,t.kasir,t.diupdatebank,t.referensi
        FROM tagihan_briva t
        WHERE t.tgl_registrasi BETWEEN @p1 AND @p2
          AND CONCAT_WS(' ',t.no_tagihan,t.no_rkm_medis,t.nm_pasien,t.status_bayar,t.kasir,t.referensi) LIKE @p0
        ORDER BY t.tgl_registrasi DESC,t.no_tagihan
        """,
        [.. BankColumns.Select(column => column.Item1 == "no_nota" ? ("no_tagihan", "No. Tagihan") : column)],
        ["besar_bayar"], "Data tagihan bank yang dikirim Khanza. Daftar hanya-baca.");

    private static ReportSpec Debt(string title, string filter) => R(title, $"""
        SELECT h.no_hutang,h.tgl_hutang,h.tgltempo,m.nama_pemberi_hutang,h.nominal,h.sisahutang,h.status,h.kd_rek,h.keterangan
        FROM beban_hutang_lain h LEFT JOIN pemberi_hutang_lain m ON m.kode_pemberi_hutang=h.kode_pemberi_hutang
        WHERE h.tgl_hutang BETWEEN @p1 AND @p2 {filter} AND CONCAT_WS(' ',h.no_hutang,m.nama_pemberi_hutang,h.status,h.keterangan) LIKE @p0
        ORDER BY h.tgl_hutang DESC,h.no_hutang
        """,
        [("no_hutang", "No. Hutang"), ("tgl_hutang", "Tanggal"), ("tgltempo", "Jatuh Tempo"), ("nama_pemberi_hutang", "Pemberi Hutang"),
         ("nominal", "Nominal"), ("sisahutang", "Sisa Hutang"), ("status", "Status"), ("kd_rek", "Rekening"), ("keterangan", "Keterangan")],
        ["nominal", "sisahutang"]);

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnHutangNonMedis"] = Hutang("Non Medis", "ipsrspemesanan", "ipsrssuplier", "bayar_pemesanan_non_medis"),
        ["btnRingkasanHutangVendorBarangNonMedis"] = Ringkasan("Non Medis", "ipsrspemesanan", "ipsrssuplier", "bayar_pemesanan_non_medis"),
        ["btnTagihanHutangNonMedis"] = Titip("Non Medis", "ipsrs_titip_faktur", "ipsrs_detail_titip_faktur"),
        ["btnValidasiTagihanNonMedis"] = Validasi("Non Medis", "ipsrs_titip_faktur", "ipsrs_detail_titip_faktur"),
        ["btnBayarPesanNonMedis"] = Bayar("Non Medis", "ipsrspemesanan", "ipsrssuplier", "bayar_pemesanan_non_medis"),

        ["btnHutangAsetInventaris"] = Hutang("Aset/Inventaris", "inventaris_pemesanan", "inventaris_suplier", "bayar_pemesanan_inventaris"),
        ["btnRingkasanHutangVendorAsetInventaris"] = Ringkasan("Aset/Inventaris", "inventaris_pemesanan", "inventaris_suplier", "bayar_pemesanan_inventaris"),
        ["btnTagihanHutangAset"] = Titip("Aset/Inventaris", "inventaris_titip_faktur", "inventaris_detail_titip_faktur"),
        ["btnValidasiTagihanAset"] = Validasi("Aset/Inventaris", "inventaris_titip_faktur", "inventaris_detail_titip_faktur"),
        ["btnBayarPemesananInventaris"] = Bayar("Aset/Inventaris", "inventaris_pemesanan", "inventaris_suplier", "bayar_pemesanan_inventaris"),

        ["btnHutangDapur"] = Hutang("Barang Dapur", "dapurpemesanan", "dapursuplier", "bayar_pemesanan_dapur"),
        ["btnRingkasanHutangVendorBarangDapur"] = Ringkasan("Dapur", "dapurpemesanan", "dapursuplier", "bayar_pemesanan_dapur"),
        ["btnTagihanHutangDapur"] = Titip("Dapur", "dapur_titip_faktur", "dapur_detail_titip_faktur"),
        ["btnValidasiTagihanDapur"] = Validasi("Dapur", "dapur_titip_faktur", "dapur_detail_titip_faktur"),
        ["btnBayarPesanDapur"] = Bayar("Dapur", "dapurpemesanan", "dapursuplier", "bayar_pemesanan_dapur"),

        ["btnRingkasanHutangVendorFarmasi"] = Ringkasan("Farmasi", "pemesanan", "datasuplier", "bayar_pemesanan"),
        ["btnValidasiTagihanObatBHP"] = Validasi("Obat & BHP", "titip_faktur", "detail_titip_faktur"),

        ["btnPembayaranBankJateng"] = Bank("Pembayaran Bank Jateng", "tagihan_bpd_jateng", "no_nota", "status_lanjut"),
        ["btnPembayaranBankPapua"] = Bank("Pembayaran Bank Papua", "tagihan_bpd_papua", "no_nota", "status_lanjut"),
        ["btnPembayaranBankJabar"] = Bank("Pembayaran Bank Jabar", "tagihan_bpd_jabar", "no_nota", "status_lanjut"),
        ["btnPembayaranBankMandiri"] = Bank("Pembayaran Bank Mandiri", "tagihan_mandiri", "no_nota", "status_lanjut"),
        ["btnPembayaranBRIVA"] = Briva,
        ["btnPembayaranPihakKe3BankMandiri"] = R("Pembayaran Pihak Ke 3 Bank Mandiri", """
            SELECT nomor_pembayaran,tgl_pembayaran,asal_transaksi,nomor_tagihan,atas_nama_rekening_tujuan,no_rekening_tujuan,
                   nominal_pembayaran,status_transaksi,kode_bank,kode_metode
            FROM pembayaran_pihak_ke3_bankmandiri
            WHERE tgl_pembayaran BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',nomor_pembayaran,asal_transaksi,nomor_tagihan,atas_nama_rekening_tujuan,no_rekening_tujuan,status_transaksi) LIKE @p0
            ORDER BY tgl_pembayaran DESC,nomor_pembayaran
            """,
            [("nomor_pembayaran", "No. Pembayaran"), ("tgl_pembayaran", "Tanggal"), ("asal_transaksi", "Asal Transaksi"),
             ("nomor_tagihan", "No. Tagihan"), ("atas_nama_rekening_tujuan", "Atas Nama Tujuan"), ("no_rekening_tujuan", "Rekening Tujuan"),
             ("nominal_pembayaran", "Nominal"), ("status_transaksi", "Status"), ("kode_bank", "Kode Bank"), ("kode_metode", "Metode")],
            ["nominal_pembayaran"]),

        ["btnPiutangJasaPerusahaan"] = Company("Piutang Jasa Perusahaan", ""),
        ["btnPiutangJasaPerusahaanBelumLunas"] = Company("Piutang Jasa Perusahaan Belum Lunas", "AND h.status='Belum Lunas'"),
        ["btnBayarPiutangJasaPerusahaan"] = R("Bayar Piutang Jasa Perusahaan", """
            SELECT b.tgl_bayar,b.no_piutang,c.nama_perusahaan,b.besar_cicilan,b.nama_bayar,b.kd_rek,b.keterangan
            FROM bayar_piutang_jasa_perusahaan b LEFT JOIN perusahaan_pasien c ON c.kode_perusahaan=b.kode_perusahaan
            WHERE b.tgl_bayar BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',b.no_piutang,c.nama_perusahaan,b.nama_bayar,b.keterangan) LIKE @p0
            ORDER BY b.tgl_bayar DESC,b.no_piutang
            """,
            [("tgl_bayar", "Tgl. Bayar"), ("no_piutang", "No. Piutang"), ("nama_perusahaan", "Perusahaan"), ("besar_cicilan", "Cicilan"),
             ("nama_bayar", "Akun Bayar"), ("kd_rek", "Rekening"), ("keterangan", "Keterangan")], ["besar_cicilan"]),

        ["btnBebanHutangLain"] = Debt("Beban Hutang Lain", ""),
        ["btnRingkasanBebanHutangLain"] = R("Ringkasan Beban Hutang Lain", """
            SELECT COALESCE(m.nama_pemberi_hutang,h.kode_pemberi_hutang) AS pemberi,COUNT(*) AS jumlah,SUM(h.nominal) AS nominal,
                   SUM(h.sisahutang) AS sisahutang
            FROM beban_hutang_lain h LEFT JOIN pemberi_hutang_lain m ON m.kode_pemberi_hutang=h.kode_pemberi_hutang
            WHERE h.status='Belum Lunas' AND h.tgl_hutang BETWEEN @p1 AND @p2 AND COALESCE(m.nama_pemberi_hutang,h.kode_pemberi_hutang) LIKE @p0
            GROUP BY h.kode_pemberi_hutang,m.nama_pemberi_hutang ORDER BY m.nama_pemberi_hutang
            """,
            [("pemberi", "Pemberi Hutang"), ("jumlah", "Jumlah Hutang"), ("nominal", "Nominal"), ("sisahutang", "Sisa Hutang")],
            ["jumlah", "nominal", "sisahutang"]),
        ["btnBayarBebanHutangLain"] = R("Bayar Beban Hutang Lain", """
            SELECT b.tgl_bayar,b.no_hutang,m.nama_pemberi_hutang,b.besar_cicilan,b.nama_bayar,b.no_bukti,b.kd_rek,b.keterangan
            FROM bayar_beban_hutang_lain b LEFT JOIN pemberi_hutang_lain m ON m.kode_pemberi_hutang=b.kode_pemberi_hutang
            WHERE b.tgl_bayar BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',b.no_hutang,m.nama_pemberi_hutang,b.nama_bayar,b.no_bukti,b.keterangan) LIKE @p0
            ORDER BY b.tgl_bayar DESC,b.no_hutang
            """,
            [("tgl_bayar", "Tgl. Bayar"), ("no_hutang", "No. Hutang"), ("nama_pemberi_hutang", "Pemberi Hutang"), ("besar_cicilan", "Cicilan"),
             ("nama_bayar", "Akun Bayar"), ("no_bukti", "No. Bukti"), ("kd_rek", "Rekening"), ("keterangan", "Keterangan")], ["besar_cicilan"]),
        ["btnBayarJMDokter"] = R("Bayar JM Dokter", """
            SELECT b.no_bayar,b.tanggal,d.nm_dokter,b.rawatjalan,b.rawatinap,b.labrawatjalan,b.labrawatinap,
                   b.radrawatjalan,b.radrawatinap,b.operasiralan,b.operasiranap,b.besar_bayar,b.nama_bayar,b.keterangan
            FROM bayar_jm_dokter b LEFT JOIN dokter d ON d.kd_dokter=b.kd_dokter
            WHERE b.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',b.no_bayar,b.kd_dokter,d.nm_dokter,b.nama_bayar,b.keterangan) LIKE @p0
            ORDER BY b.tanggal DESC,b.no_bayar
            """,
            [("no_bayar", "No. Bayar"), ("tanggal", "Tanggal"), ("nm_dokter", "Dokter"), ("rawatjalan", "Rawat Jalan"), ("rawatinap", "Rawat Inap"),
             ("labrawatjalan", "Lab Ralan"), ("labrawatinap", "Lab Ranap"), ("radrawatjalan", "Rad Ralan"), ("radrawatinap", "Rad Ranap"),
             ("operasiralan", "Operasi Ralan"), ("operasiranap", "Operasi Ranap"), ("besar_bayar", "Dibayar"), ("nama_bayar", "Akun Bayar"),
             ("keterangan", "Keterangan")], ["besar_bayar"]),

        ["btnPiutangObatBelumLunas"] = R("Piutang Obat & BHP Belum Lunas", """
            SELECT h.nota_piutang,h.tgl_piutang,h.tgltempo,h.no_rkm_medis,h.nm_pasien,h.jns_jual,h.uangmuka,h.sisapiutang,h.catatan
            FROM piutang h
            WHERE h.sisapiutang>0 AND h.tgl_piutang BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.nota_piutang,h.no_rkm_medis,h.nm_pasien,h.jns_jual,h.catatan) LIKE @p0
            ORDER BY h.tgltempo,h.nota_piutang
            """,
            [("nota_piutang", "Nota"), ("tgl_piutang", "Tanggal"), ("tgltempo", "Jatuh Tempo"), ("no_rkm_medis", "No. RM"),
             ("nm_pasien", "Pasien"), ("jns_jual", "Jenis Jual"), ("uangmuka", "Uang Muka"), ("sisapiutang", "Sisa Piutang"), ("catatan", "Catatan")],
            ["uangmuka", "sisapiutang"]),
        ["btnRincianPiutangPasien"] = R("Rincian Piutang Pasien", """
            SELECT d.no_rawat,p.tgl_piutang,p.no_rkm_medis,s.nm_pasien,d.nama_bayar,j.png_jawab,d.totalpiutang,d.sisapiutang,d.tgltempo
            FROM detail_piutang_pasien d JOIN piutang_pasien p ON p.no_rawat=d.no_rawat
            LEFT JOIN pasien s ON s.no_rkm_medis=p.no_rkm_medis LEFT JOIN penjab j ON j.kd_pj=d.kd_pj
            WHERE p.tgl_piutang BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',d.no_rawat,p.no_rkm_medis,s.nm_pasien,d.nama_bayar,j.png_jawab) LIKE @p0
            ORDER BY p.tgl_piutang DESC,d.no_rawat,d.nama_bayar
            """,
            [("no_rawat", "No. Rawat"), ("tgl_piutang", "Tgl. Piutang"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("nama_bayar", "Akun Piutang"), ("png_jawab", "Cara Bayar"), ("totalpiutang", "Total Piutang"), ("sisapiutang", "Sisa Piutang"),
             ("tgltempo", "Jatuh Tempo")], ["totalpiutang", "sisapiutang"]),
        ["btnRingkasanPiutangPerJenisBayar"] = R("Ringkasan Piutang Per Cara Bayar", """
            SELECT COALESCE(j.png_jawab,'-') AS png_jawab,COUNT(DISTINCT d.no_rawat) AS jumlah,SUM(d.totalpiutang) AS totalpiutang,
                   SUM(d.sisapiutang) AS sisapiutang
            FROM detail_piutang_pasien d JOIN piutang_pasien p ON p.no_rawat=d.no_rawat LEFT JOIN penjab j ON j.kd_pj=d.kd_pj
            WHERE p.tgl_piutang BETWEEN @p1 AND @p2 AND COALESCE(j.png_jawab,'-') LIKE @p0
            GROUP BY d.kd_pj,j.png_jawab ORDER BY j.png_jawab
            """,
            [("png_jawab", "Cara Bayar"), ("jumlah", "Jumlah Pasien"), ("totalpiutang", "Total Piutang"), ("sisapiutang", "Sisa Piutang")],
            ["totalpiutang", "sisapiutang"]),
        ["btnNilaiPiutangPerJenisBayarPerBulan"] = R("Nilai Piutang Per Cara Bayar Per Bulan", """
            SELECT DATE_FORMAT(p.tgl_piutang,'%Y-%m') AS bulan,COALESCE(j.png_jawab,'-') AS png_jawab,SUM(d.totalpiutang) AS totalpiutang,
                   SUM(d.sisapiutang) AS sisapiutang
            FROM detail_piutang_pasien d JOIN piutang_pasien p ON p.no_rawat=d.no_rawat LEFT JOIN penjab j ON j.kd_pj=d.kd_pj
            WHERE p.tgl_piutang BETWEEN @p1 AND @p2 AND COALESCE(j.png_jawab,'-') LIKE @p0
            GROUP BY bulan,d.kd_pj,j.png_jawab ORDER BY bulan,j.png_jawab
            """,
            [("bulan", "Bulan"), ("png_jawab", "Cara Bayar"), ("totalpiutang", "Total Piutang"), ("sisapiutang", "Sisa Piutang")],
            ["totalpiutang", "sisapiutang"]),
        ["btnValidasiPenagihanPiutang"] = R("Validasi Penagihan Piutang", """
            SELECT h.no_tagihan,h.tanggal,h.tanggaltempo,j.png_jawab,p.nama AS petugas,m.nama AS penyetuju,h.status,
                   COUNT(d.no_rawat) AS jumlah,COALESCE(SUM(d.sisapiutang),0) AS sisapiutang,h.catatan
            FROM penagihan_piutang h LEFT JOIN penjab j ON j.kd_pj=h.kd_pj LEFT JOIN pegawai p ON p.nik=h.nip
            LEFT JOIN pegawai m ON m.nik=h.nip_menyetujui LEFT JOIN detail_penagihan_piutang d ON d.no_tagihan=h.no_tagihan
            WHERE h.status='Proses Penagihan' AND h.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_tagihan,j.png_jawab,p.nama,m.nama,h.catatan) LIKE @p0
            GROUP BY h.no_tagihan ORDER BY h.tanggal DESC,h.no_tagihan
            """,
            [("no_tagihan", "No. Tagihan"), ("tanggal", "Tanggal"), ("tanggaltempo", "Jatuh Tempo"), ("png_jawab", "Cara Bayar"),
             ("petugas", "Petugas"), ("penyetuju", "Menyetujui"), ("status", "Status"), ("jumlah", "Jumlah Piutang"),
             ("sisapiutang", "Sisa Piutang"), ("catatan", "Catatan")],
            ["jumlah", "sisapiutang"], "Penagihan yang masih berstatus Proses Penagihan. Validasi dicatat di Khanza."),
        ["btnRVPPiutangBPJS"] = R("RVP Piutang BPJS", """
            SELECT r.no_rawat,r.tanggal_rvp,r.no_sep,s.nm_pasien,r.totalpiutang,r.uangmuka,r.sudahdibayar,r.sisapiutang,r.tarifinacbg,
                   r.dibayarbpjs,r.persenbayar,r.rugi,r.lebih
            FROM rvp_klaim_bpjs r LEFT JOIN reg_periksa g ON g.no_rawat=r.no_rawat LEFT JOIN pasien s ON s.no_rkm_medis=g.no_rkm_medis
            WHERE r.tanggal_rvp BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',r.no_rawat,r.no_sep,s.nm_pasien) LIKE @p0
            ORDER BY r.tanggal_rvp DESC,r.no_rawat
            """,
            [("no_rawat", "No. Rawat"), ("tanggal_rvp", "Tgl. RVP"), ("no_sep", "No. SEP"), ("nm_pasien", "Pasien"),
             ("totalpiutang", "Total Piutang"), ("uangmuka", "Uang Muka"), ("sudahdibayar", "Sudah Dibayar"), ("sisapiutang", "Sisa Piutang"),
             ("tarifinacbg", "Tarif INACBG"), ("dibayarbpjs", "Dibayar BPJS"), ("persenbayar", "% Bayar"), ("rugi", "Rugi"), ("lebih", "Lebih")],
            ["totalpiutang", "sisapiutang", "tarifinacbg", "dibayarbpjs", "rugi", "lebih"]),
        ["btnPengembalianDepositPasien"] = R("Pengembalian Deposit Pasien", """
            SELECT d.no_rawat,d.tanggal,g.no_rkm_medis,s.nm_pasien,d.besar_pengembalian,p.nama AS nama_petugas
            FROM pengembalian_deposit d LEFT JOIN reg_periksa g ON g.no_rawat=d.no_rawat LEFT JOIN pasien s ON s.no_rkm_medis=g.no_rkm_medis
            LEFT JOIN petugas p ON p.nip=d.petugas
            WHERE d.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',d.no_rawat,g.no_rkm_medis,s.nm_pasien,p.nama) LIKE @p0
            ORDER BY d.tanggal DESC,d.no_rawat
            """,
            [("no_rawat", "No. Rawat"), ("tanggal", "Tanggal"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("besar_pengembalian", "Dikembalikan"), ("nama_petugas", "Petugas")], ["besar_pengembalian"]),
    };
}
