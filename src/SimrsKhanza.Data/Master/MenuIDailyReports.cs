namespace SimrsKhanza.Data.Master;

/// <summary>Daily legacy report subsets, grouped by the registration doctor.</summary>
public static class MenuIDailyReports
{
    private static ReportSpec Doctor(bool perClinic)
    {
        var title = perClinic ? "Harian Dokter Poli" : "Harian Dokter Ralan";
        var grouping = perClinic ? "q.kd_poli,q.nm_poli,q.kd_dokter,q.nm_dokter" : "q.kd_dokter,q.nm_dokter";
        var clinicFields = perClinic ? "q.kd_poli,q.nm_poli," : "";
        var ordering = perClinic ? "q.nm_poli,q.nm_dokter" : "q.nm_dokter";
        var sql = $"""
            SELECT {clinicFields} q.kd_dokter,q.nm_dokter,COUNT(*) AS jumlah,
                   SUM(q.registrasi) AS registrasi,SUM(q.tindakan) AS tindakan,
                   SUM(q.obat) AS obat,SUM(q.obat_langsung) AS obat_langsung,
                   SUM(q.lab) AS laboratorium,SUM(q.radiologi) AS radiologi,
                   SUM(q.tambahan) AS tambahan,SUM(q.potongan) AS potongan
            FROM (
              SELECT r.no_rawat,r.kd_dokter,d.nm_dokter,r.kd_poli,poli.nm_poli,
                     COALESCE(r.biaya_reg,0) AS registrasi,
                     COALESCE((SELECT SUM(t.biaya_rawat) FROM rawat_jl_dr t WHERE t.no_rawat=r.no_rawat AND t.kd_dokter=r.kd_dokter),0)
                       + COALESCE((SELECT SUM(t.biaya_rawat) FROM rawat_jl_drpr t WHERE t.no_rawat=r.no_rawat AND t.kd_dokter=r.kd_dokter),0) AS tindakan,
                     COALESCE((SELECT SUM(x.total) FROM detail_pemberian_obat x WHERE x.no_rawat=r.no_rawat),0) AS obat,
                     COALESCE((SELECT SUM(x.besar_tagihan) FROM tagihan_obat_langsung x WHERE x.no_rawat=r.no_rawat),0) AS obat_langsung,
                     COALESCE((SELECT SUM(x.biaya) FROM periksa_lab x WHERE x.no_rawat=r.no_rawat),0)
                       + COALESCE((SELECT SUM(i.biaya_item) FROM detail_periksa_lab i JOIN periksa_lab x
                           ON x.no_rawat=i.no_rawat AND x.kd_jenis_prw=i.kd_jenis_prw AND x.tgl_periksa=i.tgl_periksa AND x.jam=i.jam
                           WHERE x.no_rawat=r.no_rawat),0) AS lab,
                     COALESCE((SELECT SUM(x.biaya) FROM periksa_radiologi x WHERE x.no_rawat=r.no_rawat),0) AS radiologi,
                     COALESCE((SELECT SUM(x.besar_biaya) FROM tambahan_biaya x WHERE x.no_rawat=r.no_rawat),0) AS tambahan,
                     COALESCE((SELECT SUM(x.besar_pengurangan) FROM pengurangan_biaya x WHERE x.no_rawat=r.no_rawat),0) AS potongan
              FROM reg_periksa r JOIN dokter d ON d.kd_dokter=r.kd_dokter
              JOIN poliklinik poli ON poli.kd_poli=r.kd_poli
              WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
                AND NOT EXISTS (SELECT 1 FROM kamar_inap ki WHERE ki.no_rawat=r.no_rawat)
                AND (d.kd_dokter LIKE @p0 OR d.nm_dokter LIKE @p0 OR poli.kd_poli LIKE @p0 OR poli.nm_poli LIKE @p0)
            ) q
            GROUP BY {grouping} ORDER BY {ordering}
            """;
        var columns = new List<(string, string)>();
        if (perClinic) columns.AddRange([("kd_poli", "Kode Poli"), ("nm_poli", "Poli")]);
        columns.AddRange([("kd_dokter", "Kode Dokter"), ("nm_dokter", "Dokter Registrasi"), ("jumlah", "Kunjungan"),
            ("registrasi", "Registrasi"), ("tindakan", "Tindakan Dokter Registrasi"), ("obat", "Obat"),
            ("obat_langsung", "Obat Langsung"), ("laboratorium", "Laboratorium"), ("radiologi", "Radiologi"),
            ("tambahan", "Tambahan"), ("potongan", "Potongan")]);
        return new ReportSpec(title, sql, columns.ToArray(), ReportPeriod.DateRange,
            "Berdasarkan tanggal dan dokter registrasi; kunjungan dengan riwayat kamar dikecualikan. Tindakan dihitung hanya bila dokter tindakan sama dengan dokter registrasi, sehingga dapat berbeda dari rekap Khanza ketika keduanya berbeda. Detail pasien, filter cara bayar dan cetak legacy belum tersedia; nilai belum diuji pada database.")
            { SearchWatermark = "Kode/nama dokter atau poli" };
    }

    private static readonly ReportSpec Room = new(
        "Harian Kamar", """
        SELECT k.tgl_keluar AS tanggal,k.no_rawat,k.kd_bangsal,b.nm_bangsal,p.nm_pasien,j.png_jawab,
               COALESCE(r.biaya_reg,0) AS registrasi,
               COALESCE((SELECT SUM(x.ttl_biaya) FROM kamar_inap x WHERE x.no_rawat=k.no_rawat),0) AS kamar,
               COALESCE((SELECT SUM(x.biaya_rawat) FROM rawat_inap_dr x WHERE x.no_rawat=k.no_rawat),0)
                 + COALESCE((SELECT SUM(x.biaya_rawat) FROM rawat_inap_pr x WHERE x.no_rawat=k.no_rawat),0)
                 + COALESCE((SELECT SUM(x.biaya_rawat) FROM rawat_inap_drpr x WHERE x.no_rawat=k.no_rawat),0) AS tindakan,
               COALESCE((SELECT SUM(x.total) FROM detail_pemberian_obat x WHERE x.no_rawat=k.no_rawat),0) AS obat,
               COALESCE((SELECT SUM(x.besar_tagihan) FROM tagihan_obat_langsung x WHERE x.no_rawat=k.no_rawat),0) AS obat_langsung,
               COALESCE((SELECT SUM(x.biaya) FROM periksa_lab x WHERE x.no_rawat=k.no_rawat),0)
                 + COALESCE((SELECT SUM(x.biaya_item) FROM detail_periksa_lab x WHERE x.no_rawat=k.no_rawat),0) AS laboratorium,
               COALESCE((SELECT SUM(x.biaya) FROM periksa_radiologi x WHERE x.no_rawat=k.no_rawat),0) AS radiologi,
               COALESCE((SELECT SUM(x.besar_biaya) FROM tambahan_biaya x WHERE x.no_rawat=k.no_rawat),0) AS tambahan,
               COALESCE((SELECT SUM(x.besar_pengurangan) FROM pengurangan_biaya x WHERE x.no_rawat=k.no_rawat),0) AS potongan
        FROM (
          SELECT ki.no_rawat,km.kd_bangsal,MIN(ki.tgl_keluar) AS tgl_keluar
          FROM kamar_inap ki JOIN kamar km ON km.kd_kamar=ki.kd_kamar
          WHERE ki.stts_pulang<>'Pindah Kamar' AND ki.tgl_keluar BETWEEN @p1 AND @p2
          GROUP BY ki.no_rawat,km.kd_bangsal
        ) k JOIN reg_periksa r ON r.no_rawat=k.no_rawat JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
        JOIN penjab j ON j.kd_pj=r.kd_pj JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal
        WHERE k.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0 OR b.kd_bangsal LIKE @p0
           OR b.nm_bangsal LIKE @p0 OR j.png_jawab LIKE @p0
        ORDER BY b.nm_bangsal,k.tgl_keluar,k.no_rawat
        """, [("tanggal", "Tanggal Pulang"), ("no_rawat", "No. Rawat"), ("kd_bangsal", "Kode Bangsal"),
         ("nm_bangsal", "Bangsal"), ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"),
         ("registrasi", "Registrasi"), ("kamar", "Kamar"), ("tindakan", "Tindakan Ranap"),
         ("obat", "Obat"), ("obat_langsung", "Obat Langsung"), ("laboratorium", "Laboratorium"),
         ("radiologi", "Radiologi"), ("tambahan", "Tambahan"), ("potongan", "Potongan")],
        ReportPeriod.DateRange, "Berdasarkan tanggal keluar, satu baris per kunjungan dan bangsal. Komponen dihitung untuk seluruh kunjungan; jika kunjungan muncul pada beberapa bangsal, nominal dapat berulang. Biaya operasi, filter khusus dan cetak legacy belum tersedia; nilai belum diuji pada database.")
        { SearchWatermark = "Pasien, No. rawat, bangsal, atau cara bayar" };

    private static readonly ReportSpec PediatricClinic = new(
        "Rekap Poli Anak", """
        SELECT r.tgl_registrasi,r.no_rawat,r.no_rkm_medis,p.nm_pasien,
               poli.nm_poli,d.nm_dokter,j.png_jawab,CONCAT(r.umurdaftar,' ',r.sttsumur) AS umur,
               r.biaya_reg,COALESCE((SELECT SUM(t.biaya_rawat) FROM rawat_jl_dr t WHERE t.no_rawat=r.no_rawat),0) AS tindakan
        FROM reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
        JOIN poliklinik poli ON poli.kd_poli=r.kd_poli JOIN dokter d ON d.kd_dokter=r.kd_dokter
        JOIN penjab j ON j.kd_pj=r.kd_pj
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND LOWER(poli.nm_poli) LIKE '%anak%'
          AND (r.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0 OR d.nm_dokter LIKE @p0
               OR j.png_jawab LIKE @p0 OR poli.nm_poli LIKE @p0)
        ORDER BY r.tgl_registrasi,d.nm_dokter,r.no_rawat
        """,
        [("tgl_registrasi", "Tanggal Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("nm_poli", "Poli"), ("nm_dokter", "Dokter"), ("png_jawab", "Cara Bayar"),
         ("umur", "Umur Saat Daftar"), ("biaya_reg", "Biaya Registrasi"), ("tindakan", "Biaya Tindakan Dokter")],
        ReportPeriod.DateRange, "Rekap kunjungan poli dengan nama yang mengandung kata anak. Rincian klinis dan format cetak Khanza belum tersedia.")
        { SearchWatermark = "Pasien, No. Rawat, dokter, cara bayar, atau poli" };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnTagihanPoli"] = Doctor(true),
        ["btnTagihanDokter"] = Doctor(false),
        ["btnHarianKamar"] = Room,
        ["btnRekapPoliAnak"] = PediatricClinic,
    };
}
