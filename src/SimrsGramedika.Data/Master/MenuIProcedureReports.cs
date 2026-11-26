namespace SimrsGramedika.Data.Master;

public static class MenuIProcedureReports
{
    private static ReportSpec ProcedureDetail()
    {
        string Part(string table, bool inpatient, bool hasDoctor, bool hasNurse)
        {
            var visit = inpatient ? "Ranap" : "Ralan";
            var actor = hasDoctor && hasNurse ? "Dokter & Paramedis" : hasDoctor ? "Dokter" : "Paramedis";
            var doctors = hasDoctor ? "t.tarif_tindakandr" : "0";
            var nurses = hasNurse ? "t.tarif_tindakanpr" : "0";
            var doctor = hasDoctor ? "d.nm_dokter" : "''";
            var nurse = hasNurse ? "pg.nama" : "''";
            return $"""
                SELECT t.tgl_perawatan AS tanggal,t.jam_rawat AS jam,t.no_rawat,r.no_rkm_medis,p.nm_pasien,
                       j.png_jawab,'{visit} {actor}' AS layanan,t.kd_jenis_prw,k.nm_perawatan,
                       {doctor} AS dokter,{nurse} AS paramedis,t.material,t.bhp,
                       {doctors} AS jasa_dokter,{nurses} AS jasa_paramedis,t.kso,t.menejemen,t.biaya_rawat AS total
                FROM {table} t JOIN reg_periksa r ON r.no_rawat=t.no_rawat
                JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis JOIN penjab j ON j.kd_pj=r.kd_pj
                JOIN {(inpatient ? "jns_perawatan_inap" : "jns_perawatan")} k ON k.kd_jenis_prw=t.kd_jenis_prw
                {(hasDoctor ? "LEFT JOIN dokter d ON d.kd_dokter=t.kd_dokter" : "")}
                {(hasNurse ? "LEFT JOIN petugas pg ON pg.nip=t.nip" : "")}
                WHERE t.tgl_perawatan BETWEEN @p1 AND @p2
                  AND (t.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0
                       OR t.kd_jenis_prw LIKE @p0 OR k.nm_perawatan LIKE @p0 OR j.png_jawab LIKE @p0)
                """;
        }
        var parts = new[] { Part("rawat_jl_dr", false, true, false), Part("rawat_jl_pr", false, false, true),
            Part("rawat_jl_drpr", false, true, true), Part("rawat_inap_dr", true, true, false),
            Part("rawat_inap_pr", true, false, true), Part("rawat_inap_drpr", true, true, true) };
        return new ReportSpec("Detail Tindakan", $"SELECT * FROM ({string.Join(" UNION ALL ", parts)}) q ORDER BY tanggal,jam,no_rawat,layanan,kd_jenis_prw",
            [("tanggal", "Tanggal"), ("jam", "Jam"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
             ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"), ("layanan", "Layanan"),
             ("kd_jenis_prw", "Kode"), ("nm_perawatan", "Tindakan"), ("dokter", "Dokter"), ("paramedis", "Paramedis"),
             ("material", "Sarana"), ("bhp", "BHP"), ("jasa_dokter", "Jasa Dokter"), ("jasa_paramedis", "Jasa Paramedis"),
             ("kso", "KSO"), ("menejemen", "Manajemen"), ("total", "Biaya")], ReportPeriod.DateRange,
            "Detail tindakan rawat jalan dan rawat inap berdasarkan tanggal tindakan. Operasi, lab, radiologi dan filter status bayar/jam legacy belum tersedia; nilai belum diuji pada database.")
            { SearchWatermark = "No. rawat, No. RM, pasien, tindakan, atau cara bayar", TotalColumns = ["total"] };
    }

    private static ReportSpec OperatingRoomDetail()
    {
        (string Person, string Fee, string Label, bool Doctor)[] roles =
        [
            ("operator1", "biayaoperator1", "Operator 1", true), ("operator2", "biayaoperator2", "Operator 2", true),
            ("operator3", "biayaoperator3", "Operator 3", true), ("asisten_operator1", "biayaasisten_operator1", "Asisten Operator 1", false),
            ("asisten_operator2", "biayaasisten_operator2", "Asisten Operator 2", false), ("asisten_operator3", "biayaasisten_operator3", "Asisten Operator 3", false),
            ("instrumen", "biayainstrumen", "Instrumen", false), ("dokter_anak", "biayadokter_anak", "Dokter Anak", true),
            ("perawaat_resusitas", "biayaperawaat_resusitas", "Perawat Resusitasi", false), ("dokter_anestesi", "biayadokter_anestesi", "Dokter Anestesi", true),
            ("asisten_anestesi", "biayaasisten_anestesi", "Asisten Anestesi", false), ("asisten_anestesi2", "biayaasisten_anestesi2", "Asisten Anestesi 2", false),
            ("bidan", "biayabidan", "Bidan", false), ("bidan2", "biayabidan2", "Bidan 2", false), ("bidan3", "biayabidan3", "Bidan 3", false),
            ("perawat_luar", "biayaperawat_luar", "Perawat Luar", false), ("omloop", "biaya_omloop", "Omloop", false),
            ("omloop2", "biaya_omloop2", "Omloop 2", false), ("omloop3", "biaya_omloop3", "Omloop 3", false),
            ("omloop4", "biaya_omloop4", "Omloop 4", false), ("omloop5", "biaya_omloop5", "Omloop 5", false),
            ("dokter_pjanak", "biaya_dokter_pjanak", "Dokter PJ Anak", true), ("dokter_umum", "biaya_dokter_umum", "Dokter Umum", true),
        ];
        var selectRoles = string.Join(",\n", roles.Select((role, i) => $"staff{i}.{(role.Doctor ? "nm_dokter" : "nama")} AS petugas{i},o.{role.Fee} AS jasa{i}"));
        var joins = string.Join("\n", roles.Select((role, i) => $"LEFT JOIN {(role.Doctor ? "dokter" : "petugas")} staff{i} ON staff{i}.{(role.Doctor ? "kd_dokter" : "nip")}=o.{role.Person}"));
        var cols = new List<(string, string)>
        {
            ("tgl_operasi", "Waktu"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
            ("png_jawab", "Cara Bayar"), ("status", "Layanan"), ("kategori_paket", "Kategori"),
            ("kode_paket", "Kode Paket"), ("nm_perawatan", "Paket")
        };
        for (var i = 0; i < roles.Length; i++) { cols.Add(($"petugas{i}", roles[i].Label)); cols.Add(($"jasa{i}", "Jasa " + roles[i].Label)); }
        cols.AddRange([("biayaalat", "Alat"), ("biayasewaok", "Sewa OK"), ("akomodasi", "Akomodasi"),
            ("bagian_rs", "Bagian RS"), ("biayasarpras", "Sarana Prasarana")]);
        return new ReportSpec("Detail VK & OK", $"""
            SELECT o.tgl_operasi,o.no_rawat,r.no_rkm_medis,p.nm_pasien,j.png_jawab,o.status,
                   pk.kategori AS kategori_paket,o.kode_paket,pk.nm_perawatan,
                   {selectRoles},o.biayaalat,o.biayasewaok,o.akomodasi,o.bagian_rs,o.biayasarpras
            FROM operasi o JOIN reg_periksa r ON r.no_rawat=o.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis JOIN penjab j ON j.kd_pj=r.kd_pj
            JOIN paket_operasi pk ON pk.kode_paket=o.kode_paket
            {joins}
            WHERE o.tgl_operasi BETWEEN @p1 AND @p2 AND pk.kategori IN ('Kebidanan','Operasi')
              AND (o.kode_paket LIKE @p0 OR pk.nm_perawatan LIKE @p0 OR o.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0)
            ORDER BY pk.kategori,pk.nm_perawatan,o.tgl_operasi,o.no_rawat
            """, cols.ToArray(), ReportPeriod.DateRange,
            "Rincian operasi/VK dan komponen jasa berdasarkan waktu operasi. Kolom peserta dan biaya dapat digeser secara horizontal; lokasi ruangan serta format cetak legacy belum tersedia. Data belum diuji pada database.")
            { SearchWatermark = "Kode/nama paket, No. rawat, atau pasien" };
    }

    private static readonly ReportSpec ProcedureSummary = new(
        "Ringkasan Tindakan", """
        SELECT q.layanan,q.kd_jenis_prw,q.nm_perawatan,COUNT(*) AS jumlah,
               SUM(q.material) AS material,SUM(q.bhp) AS bhp,
               SUM(q.dokter) AS dokter,SUM(q.paramedis) AS paramedis,
               SUM(q.kso) AS kso,SUM(q.menejemen) AS menejemen,SUM(q.total) AS total
        FROM (
          SELECT 'Ralan Dokter' AS layanan,t.kd_jenis_prw,k.nm_perawatan,t.material,t.bhp,
                 t.tarif_tindakandr AS dokter,0 AS paramedis,t.kso,t.menejemen,t.biaya_rawat AS total
          FROM rawat_jl_dr t JOIN jns_perawatan k ON k.kd_jenis_prw=t.kd_jenis_prw
          WHERE t.tgl_perawatan BETWEEN @p1 AND @p2
          UNION ALL
          SELECT 'Ralan Paramedis',t.kd_jenis_prw,k.nm_perawatan,t.material,t.bhp,
                 0,t.tarif_tindakanpr,t.kso,t.menejemen,t.biaya_rawat
          FROM rawat_jl_pr t JOIN jns_perawatan k ON k.kd_jenis_prw=t.kd_jenis_prw
          WHERE t.tgl_perawatan BETWEEN @p1 AND @p2
          UNION ALL
          SELECT 'Ralan Dokter dan Paramedis',t.kd_jenis_prw,k.nm_perawatan,t.material,t.bhp,
                 t.tarif_tindakandr,t.tarif_tindakanpr,t.kso,t.menejemen,t.biaya_rawat
          FROM rawat_jl_drpr t JOIN jns_perawatan k ON k.kd_jenis_prw=t.kd_jenis_prw
          WHERE t.tgl_perawatan BETWEEN @p1 AND @p2
        ) q
        WHERE q.kd_jenis_prw LIKE @p0 OR q.nm_perawatan LIKE @p0 OR q.layanan LIKE @p0
        GROUP BY q.layanan,q.kd_jenis_prw,q.nm_perawatan
        ORDER BY q.layanan,q.kd_jenis_prw
        """, [("layanan", "Pelaksana"), ("kd_jenis_prw", "Kode"), ("nm_perawatan", "Tindakan"),
         ("jumlah", "Jumlah"), ("material", "Sarana"), ("bhp", "BHP"), ("dokter", "Jasa Dokter"),
         ("paramedis", "Jasa Paramedis"), ("kso", "KSO"), ("menejemen", "Manajemen"), ("total", "Biaya")],
        ReportPeriod.DateRange, "Hanya tindakan rawat jalan oleh dokter dan/atau paramedis, berdasarkan tanggal tindakan. Operasi, ranap, laboratorium dan radiologi serta filter status bayar dan jam legacy belum termasuk; total bukan total semua layanan.")
        { SearchWatermark = "Kode, nama tindakan, atau pelaksana", TotalColumns = ["total"] };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnRingkasanTindakanRalan"] = ProcedureSummary,
        ["btnDetailTindakan"] = ProcedureDetail(),
        ["btnDetailVKOK"] = OperatingRoomDetail(),
    };
}
