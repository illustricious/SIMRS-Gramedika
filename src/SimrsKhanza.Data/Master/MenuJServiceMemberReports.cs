namespace SimrsKhanza.Data.Master;

public static partial class MenuJSpecs
{
    private static ReportSpec ServiceMemberCare(string title, string service, ReportPeriod period)
    {
        var inpatientFilter = period == ReportPeriod.Month ? "AND r.status_lanjut = 'Ranap'" : "";
        return new(title,
            $"""
            SELECT r.tgl_registrasi, r.no_rawat, r.no_rkm_medis, p.nm_pasien, p.nip,
                   g.nama_golongan, pg.nama_pangkat, s.nama_satuan, jb.nama_jabatan,
                   j.png_jawab, r.status_lanjut, k.kd_kamar,
                   CASE WHEN YEAR(k.tgl_keluar) > 0 THEN k.tgl_keluar END AS tgl_keluar
            FROM reg_periksa r
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            JOIN pasien_{service} m ON m.no_rkm_medis = p.no_rkm_medis
            JOIN golongan_{service} g ON g.id = m.golongan_{service}
            JOIN pangkat_{service} pg ON pg.id = m.pangkat_{service}
            JOIN satuan_{service} s ON s.id = m.satuan_{service}
            JOIN jabatan_{service} jb ON jb.id = m.jabatan_{service}
            JOIN penjab j ON j.kd_pj = r.kd_pj
            LEFT JOIN kamar_inap k ON k.no_rawat = r.no_rawat AND k.stts_pulang <> 'Pindah Kamar'
              AND NOT EXISTS (
                  SELECT 1 FROM kamar_inap newer
                  WHERE newer.no_rawat = k.no_rawat AND newer.stts_pulang <> 'Pindah Kamar'
                    AND (newer.tgl_masuk, newer.jam_masuk) > (k.tgl_masuk, k.jam_masuk)
              )
            WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.stts <> 'Batal' {inpatientFilter}
              AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0 OR p.nip LIKE @p0
                   OR g.nama_golongan LIKE @p0 OR pg.nama_pangkat LIKE @p0 OR s.nama_satuan LIKE @p0 OR jb.nama_jabatan LIKE @p0)
            ORDER BY r.tgl_registrasi, r.no_rawat
            """,
            [("tgl_registrasi", "Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
             ("nm_pasien", "Pasien"), ("nip", "NIP/NRP"), ("nama_golongan", "Golongan"), ("nama_pangkat", "Pangkat"),
             ("nama_satuan", "Satuan"), ("nama_jabatan", "Jabatan"), ("png_jawab", "Cara Bayar"),
             ("status_lanjut", "Layanan"), ("kd_kamar", "Kamar Terakhir"), ("tgl_keluar", "Tanggal Keluar")],
            period, "Daftar menurut tanggal registrasi dan data golongan, pangkat, satuan, serta jabatan pada master pasien.")
            { SearchWatermark = "Pasien, NIP/NRP, pangkat, satuan, atau jabatan" };
    }

    private static ReportSpec ServiceMemberSummary(string title, string service, bool diagnoses)
    {
        var diagnosisJoin = diagnoses
            ? "JOIN diagnosa_pasien d ON d.no_rawat = r.no_rawat JOIN penyakit p ON p.kd_penyakit = d.kd_penyakit"
            : "";
        var sourceColumns = diagnoses ? "p.kd_penyakit, p.nm_penyakit," : "";
        var selectColumns = diagnoses ? "kd_penyakit, nm_penyakit," : "";
        var careFilter = diagnoses ? "" : "AND r.status_lanjut = 'Ralan'";
        var columns = new List<(string, string)>();
        if (diagnoses) columns.AddRange([("kd_penyakit", "Kode ICD 10"), ("nm_penyakit", "Penyakit")]);
        columns.AddRange([("kelompok", "Kelompok"), ("nama", "Jabatan/Cara Bayar"), ("pasien", "Pasien Unik"),
            ("jumlah", diagnoses ? "Jumlah Diagnosis" : "Jumlah Kunjungan")]);

        return new(title,
            $"""
            SELECT {selectColumns} kelompok, nama, COUNT(DISTINCT no_rkm_medis) AS pasien, COUNT(*) AS jumlah
            FROM (
                SELECT {sourceColumns} r.no_rkm_medis,
                       CASE WHEN m.no_rkm_medis IS NULL THEN 'Cara Bayar' ELSE 'Jabatan' END AS kelompok,
                       CASE WHEN m.no_rkm_medis IS NULL THEN j.kd_pj ELSE CAST(jb.id AS CHAR) END AS kode,
                       CASE WHEN m.no_rkm_medis IS NULL THEN j.png_jawab ELSE jb.nama_jabatan END AS nama
                FROM reg_periksa r
                JOIN penjab j ON j.kd_pj = r.kd_pj
                LEFT JOIN pasien_{service} m ON m.no_rkm_medis = r.no_rkm_medis
                LEFT JOIN jabatan_{service} jb ON jb.id = m.jabatan_{service}
                {diagnosisJoin}
                WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND r.stts <> 'Batal' {careFilter}
            ) x
            WHERE nama LIKE @p0 OR kode LIKE @p0
            GROUP BY {selectColumns} kelompok, kode, nama
            ORDER BY {selectColumns} kelompok, nama, kode
            """,
            columns.ToArray(),
            ReportPeriod.DateRange, "Pasien yang tercatat di master keanggotaan dikelompokkan menurut jabatan. Pasien lainnya dikelompokkan menurut cara bayar.")
            { SearchWatermark = "Jabatan atau cara bayar" };
    }
}
