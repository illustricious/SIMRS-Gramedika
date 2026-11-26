namespace SimrsGramedika.Data.Master;

/// <summary>Procedure fee reports. Other legacy fee sources are explicitly excluded.</summary>
public static class MenuIClinicianReports
{
    private static ReportSpec ProcedureFees(bool doctor, bool monthly)
    {
        string Part(bool inpatient, bool combined)
        {
            var table = $"rawat_{(inpatient ? "inap" : "jl")}_{(combined ? "drpr" : doctor ? "dr" : "pr")}";
            var fee = doctor ? "tarif_tindakandr" : "tarif_tindakanpr";
            var staffJoin = doctor ? "JOIN dokter staff ON staff.kd_dokter=t.kd_dokter" : "JOIN petugas staff ON staff.nip=t.nip";
            var staffCode = doctor ? "staff.kd_dokter" : "staff.nip";
            var staffName = doctor ? "staff.nm_dokter" : "staff.nama";
            // Legacy outpatient reports use registration date; inpatient reports use procedure date.
            var date = inpatient ? "t.tgl_perawatan" : "r.tgl_registrasi";
            return $"""
                SELECT {date} AS tanggal, r.no_rawat, p.nm_pasien, j.png_jawab,
                       {staffCode} AS kode, {staffName} AS petugas,
                       '{(inpatient ? "Ranap" : "Ralan")} {(combined ? "Dokter & Paramedis" : doctor ? "Dokter" : "Paramedis")}' AS layanan,
                       k.kd_jenis_prw, k.nm_perawatan, t.{fee} AS nilai
                FROM {table} t JOIN reg_periksa r ON r.no_rawat=t.no_rawat
                JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis JOIN penjab j ON j.kd_pj=r.kd_pj
                {staffJoin}
                JOIN {(inpatient ? "jns_perawatan_inap" : "jns_perawatan")} k ON k.kd_jenis_prw=t.kd_jenis_prw
                WHERE {date} BETWEEN @p1 AND @p2 AND t.{fee}>0
                  AND ({staffCode} LIKE @p0 OR {staffName} LIKE @p0 OR p.nm_pasien LIKE @p0 OR j.png_jawab LIKE @p0 OR k.nm_perawatan LIKE @p0)
                """;
        }
        var union = string.Join(" UNION ALL ", new[] { Part(false, false), Part(false, true), Part(true, false), Part(true, true) });
        var sql = monthly
            ? $"SELECT kode,petugas,png_jawab,layanan,kd_jenis_prw,nm_perawatan,COUNT(*) AS jumlah,SUM(nilai) AS nilai FROM ({union}) q GROUP BY kode,petugas,png_jawab,layanan,kd_jenis_prw,nm_perawatan ORDER BY petugas,png_jawab,layanan,nm_perawatan"
            : $"SELECT tanggal,no_rawat,nm_pasien,png_jawab,kode,petugas,layanan,kd_jenis_prw,nm_perawatan,nilai FROM ({union}) q ORDER BY petugas,tanggal,no_rawat,nm_perawatan";
        (string, string)[] cols = monthly
            ? [("kode", "Kode"), ("petugas", doctor ? "Dokter" : "Paramedis"), ("png_jawab", "Cara Bayar"), ("layanan", "Layanan"), ("kd_jenis_prw", "Kode Tindakan"), ("nm_perawatan", "Tindakan"), ("jumlah", "Jumlah"), ("nilai", "Jasa")]
            : [("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"), ("kode", "Kode"), ("petugas", doctor ? "Dokter" : "Paramedis"), ("layanan", "Layanan"), ("nm_perawatan", "Tindakan"), ("nilai", "Jasa")];
        return new ReportSpec($"{(monthly ? "Bulanan" : "Harian")} {(doctor ? "Dokter" : "Paramedis")}", sql, cols,
            ReportPeriod.DateRange,
            "Cakupan sementara hanya jasa tindakan ralan dan ranap. Operasi, laboratorium, radiologi, fee lainnya, filter status bayar dan pilihan jam legacy belum tersedia; total bukan total seluruh jasa Khanza. " +
            "Ralan memakai tanggal registrasi; ranap memakai tanggal tindakan.")
            { SearchWatermark = "Petugas, pasien, cara bayar, atau tindakan", TotalColumns = ["nilai"] };
    }

    private static readonly ReportSpec VisitFee = new(
        "Fee Visit Dokter", """
        SELECT k.tgl_keluar AS tanggal,k.no_rawat,k.kd_kamar,b.nm_bangsal,p.nm_pasien,j.png_jawab,
               d.kd_dokter,d.nm_dokter,
               SUM(CASE WHEN LOWER(tarif.nm_perawatan) LIKE '%visit%' THEN 1 ELSE 0 END) AS jumlah_visit,
               SUM(CASE WHEN LOWER(tarif.nm_perawatan) LIKE '%konsul%' AND LOWER(tarif.nm_perawatan) LIKE '%phone%' THEN 1 ELSE 0 END) AS jumlah_konsul,
               SUM(t.biaya_rawat) AS biaya_rawat,SUM(t.tarif_tindakandr) AS jasa,
               SUM(t.material+t.bhp) AS sarana_bhp
        FROM (SELECT DISTINCT no_rawat,kd_kamar,tgl_keluar FROM kamar_inap
              WHERE tgl_keluar BETWEEN @p1 AND @p2 AND tgl_keluar<>'0000-00-00') k
        JOIN kamar km ON km.kd_kamar=k.kd_kamar JOIN bangsal b ON b.kd_bangsal=km.kd_bangsal
        JOIN reg_periksa r ON r.no_rawat=k.no_rawat JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
        JOIN penjab j ON j.kd_pj=r.kd_pj JOIN rawat_inap_dr t ON t.no_rawat=k.no_rawat
        JOIN dokter d ON d.kd_dokter=t.kd_dokter JOIN jns_perawatan_inap tarif ON tarif.kd_jenis_prw=t.kd_jenis_prw
        WHERE t.tarif_tindakandr>0
          AND (LOWER(tarif.nm_perawatan) LIKE '%visit%' OR (LOWER(tarif.nm_perawatan) LIKE '%konsul%' AND LOWER(tarif.nm_perawatan) LIKE '%phone%'))
          AND (d.kd_dokter LIKE @p0 OR d.nm_dokter LIKE @p0 OR p.nm_pasien LIKE @p0)
        GROUP BY k.tgl_keluar,k.no_rawat,k.kd_kamar,b.nm_bangsal,p.nm_pasien,j.png_jawab,d.kd_dokter,d.nm_dokter
        ORDER BY d.nm_dokter,k.tgl_keluar,k.no_rawat,k.kd_kamar
        """, [("tanggal", "Tanggal Pulang"), ("no_rawat", "No. Rawat"), ("kd_kamar", "Kamar"),
         ("nm_bangsal", "Bangsal"), ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"),
         ("nm_dokter", "Dokter"), ("jumlah_visit", "Visit"), ("jumlah_konsul", "Konsul Telepon"),
         ("biaya_rawat", "Biaya"), ("jasa", "Jasa Dokter"), ("sarana_bhp", "Sarana dan BHP")],
        ReportPeriod.DateRange, "Periode memakai tanggal pulang kamar. Tindakan visit dan konsul telepon dihitung untuk seluruh kunjungan; bila satu kunjungan memiliki beberapa baris kamar, jasa dapat terulang pada setiap kamar. Gunakan hasil per kamar, jangan menjumlahkannya sebagai total kunjungan.")
        { SearchWatermark = "Kode/nama dokter atau pasien" };

    private static readonly ReportSpec EcgFee = new(
        "Fee Bacaan EKG", """
        SELECT q.tanggal,q.layanan,q.no_rawat,q.nm_pasien,q.png_jawab,q.kd_dokter,q.nm_dokter,
               COUNT(*) AS jumlah,SUM(q.nilai) AS nilai
        FROM (
          SELECT r.tgl_registrasi AS tanggal,'Ralan' AS layanan,r.no_rawat,p.nm_pasien,j.png_jawab,
                 d.kd_dokter,d.nm_dokter,t.tarif_tindakandr AS nilai
          FROM rawat_jl_dr t JOIN reg_periksa r ON r.no_rawat=t.no_rawat
          JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis JOIN penjab j ON j.kd_pj=r.kd_pj
          JOIN dokter d ON d.kd_dokter=t.kd_dokter JOIN jns_perawatan k ON k.kd_jenis_prw=t.kd_jenis_prw
          WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND t.tarif_tindakandr>0
            AND LOWER(k.nm_perawatan) LIKE '%bacaan%' AND LOWER(k.nm_perawatan) LIKE '%ekg%'
          UNION ALL
          SELECT ki.tgl_keluar AS tanggal,'Ranap' AS layanan,r.no_rawat,p.nm_pasien,j.png_jawab,
                 d.kd_dokter,d.nm_dokter,t.tarif_tindakandr AS nilai
          FROM (SELECT no_rawat,MIN(tgl_keluar) AS tgl_keluar FROM kamar_inap
                WHERE tgl_keluar BETWEEN @p1 AND @p2 AND tgl_keluar<>'0000-00-00' GROUP BY no_rawat) ki
          JOIN reg_periksa r ON r.no_rawat=ki.no_rawat JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
          JOIN penjab j ON j.kd_pj=r.kd_pj JOIN rawat_inap_dr t ON t.no_rawat=r.no_rawat
          JOIN dokter d ON d.kd_dokter=t.kd_dokter JOIN jns_perawatan_inap k ON k.kd_jenis_prw=t.kd_jenis_prw
          WHERE t.tarif_tindakandr>0 AND LOWER(k.nm_perawatan) LIKE '%bacaan%' AND LOWER(k.nm_perawatan) LIKE '%ekg%'
        ) q
        WHERE q.kd_dokter LIKE @p0 OR q.nm_dokter LIKE @p0 OR q.nm_pasien LIKE @p0
        GROUP BY q.tanggal,q.layanan,q.no_rawat,q.nm_pasien,q.png_jawab,q.kd_dokter,q.nm_dokter
        ORDER BY q.nm_dokter,q.tanggal,q.no_rawat
        """, [("tanggal", "Tanggal"), ("layanan", "Layanan"), ("no_rawat", "No. Rawat"),
         ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"), ("nm_dokter", "Dokter"),
         ("jumlah", "Jumlah Bacaan"), ("nilai", "Jasa Dokter")], ReportPeriod.DateRange,
        "Ralan memakai tanggal registrasi; ranap memakai tanggal keluar pertama dalam periode dan seluruh tindakan bacaan EKG untuk kunjungan tersebut. Hanya tindakan dokter, sesuai sumber laporan Khanza.")
        { SearchWatermark = "Kode/nama dokter atau pasien", TotalColumns = ["nilai"] };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnRHDOkter"] = ProcedureFees(true, false),
        ["btnRBDokter"] = ProcedureFees(true, true),
        ["btnRHParamedis"] = ProcedureFees(false, false),
        ["btnRBParamedis"] = ProcedureFees(false, true),
        ["btnfee_visit_dokter"] = VisitFee,
        ["btnfee_bacaan_ekg"] = EcgFee,
    };
}
