namespace SimrsGramedika.Data.Master;

public static partial class MenuJSpecs
{
    private static MasterSpec VaccinePreventableDiseases() => new(
        "Penyakit AFP & PD3I", "penyakit_pd3i",
        [new("kd_penyakit", "Kode ICD 10", FieldKind.Lookup, LookupSql:
             "SELECT kd_penyakit AS kode, nm_penyakit AS nama FROM penyakit ORDER BY kd_penyakit",
             Key: true, Required: true)],
        """
        SELECT m.kd_penyakit, p.nm_penyakit
        FROM penyakit_pd3i m
        JOIN penyakit p ON p.kd_penyakit = m.kd_penyakit
        WHERE m.kd_penyakit LIKE @p0 OR p.nm_penyakit LIKE @p0
        ORDER BY m.kd_penyakit
        """,
        [("kd_penyakit", "Kode ICD 10"), ("nm_penyakit", "Penyakit")])
        { CanEdit = false, Hint = "Tambah atau hapus kode pada daftar penyakit surveilans AFP dan PD3I." };

    private static ReportSpec DiagnosisStatistics(string title, string care, bool? external = null, bool pd3i = false)
    {
        var careFilter = care.Length == 0 ? "" : $"AND d.status = '{care}'";
        var classification = external switch
        {
            true => "AND LEFT(p.kd_penyakit, 1) IN ('V', 'W', 'X', 'Y')",
            false => "AND LEFT(p.kd_penyakit, 1) NOT IN ('V', 'W', 'X', 'Y')",
            null => "",
        };
        var pd3iJoin = pd3i ? "JOIN penyakit_pd3i pd ON pd.kd_penyakit = d.kd_penyakit" : "";
        var discharged = external.HasValue && care == "Ranap";
        var stayJoin = discharged
            ? """
              JOIN kamar_inap k ON k.no_rawat = r.no_rawat AND k.stts_pulang NOT IN ('Pindah Kamar', '-')
                AND NOT EXISTS (
                    SELECT 1 FROM kamar_inap z WHERE z.no_rawat = k.no_rawat
                      AND z.stts_pulang NOT IN ('Pindah Kamar', '-')
                      AND (z.tgl_masuk, z.jam_masuk) > (k.tgl_masuk, k.jam_masuk)
                )
              """
            : "";
        var dateField = discharged ? "k.tgl_keluar" : "r.tgl_registrasi";
        var outcome = discharged ? ", COUNT(CASE WHEN k.stts_pulang IN ('Meninggal', '+') THEN 1 END) AS meninggal" : "";
        (string, string)[] outcomeColumns = discharged ? [("meninggal", "Meninggal")] : [];
        return new(title,
            $"""
            SELECT p.kd_penyakit, p.nm_penyakit, d.status AS layanan, patient.jk, r.sttsumur, r.umurdaftar,
                   d.status_penyakit, COUNT(*) AS jumlah {outcome}
            FROM diagnosa_pasien d
            JOIN penyakit p ON p.kd_penyakit = d.kd_penyakit
            JOIN reg_periksa r ON r.no_rawat = d.no_rawat
            JOIN pasien patient ON patient.no_rkm_medis = r.no_rkm_medis
            {pd3iJoin}
            {stayJoin}
            WHERE {dateField} BETWEEN @p1 AND @p2 AND d.prioritas = 1 AND r.stts <> 'Batal'
              AND p.kd_penyakit <> '-' {careFilter} {classification}
              AND (p.kd_penyakit LIKE @p0 OR p.nm_penyakit LIKE @p0)
            GROUP BY p.kd_penyakit, p.nm_penyakit, d.status, patient.jk, r.sttsumur, r.umurdaftar, d.status_penyakit
            ORDER BY p.kd_penyakit, d.status, r.sttsumur, r.umurdaftar, patient.jk, d.status_penyakit
            """,
            [("kd_penyakit", "Kode ICD 10"), ("nm_penyakit", "Penyakit"), ("layanan", "Layanan"),
             ("jk", "Jenis Kelamin"), ("sttsumur", "Satuan Umur"), ("umurdaftar", "Umur"),
             ("status_penyakit", "Kasus Baru/Lama"), ("jumlah", "Diagnosis Utama"), .. outcomeColumns],
            ReportPeriod.DateRange, discharged
                ? "Diagnosis utama menurut tanggal keluar. Meninggal dihitung dari status pulang yang tercatat."
                : "Diagnosis utama menurut tanggal registrasi, umur tercatat, jenis kelamin, dan status kasus.")
            { SearchWatermark = "Kode atau nama penyakit" };
    }
}
