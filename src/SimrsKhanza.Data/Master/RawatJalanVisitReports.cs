namespace SimrsKhanza.Data.Master;

public static class RawatJalanVisitReports
{
    public static readonly ReportSpec Obstetri = Pemeriksaan("Pemeriksaan Obstetri", "pemeriksaan_obstetri_ralan",
        ("tinggi_uteri", "Tinggi Uteri"), ("janin", "Janin"), ("letak", "Letak"), ("panggul", "Panggul"),
        ("denyut", "Denyut"), ("kontraksi", "Kontraksi"), ("kualitas_mnt", "Kualitas (menit)"),
        ("kualitas_dtk", "Kualitas (detik)"), ("fluksus", "Fluksus"), ("albus", "Albus"), ("vulva", "Vulva"),
        ("portio", "Portio"), ("dalam", "Dalam"), ("tebal", "Tebal"), ("arah", "Arah"),
        ("pembukaan", "Pembukaan"), ("penurunan", "Penurunan"), ("denominator", "Denominator"),
        ("ketuban", "Ketuban"), ("feto", "Feto"));

    public static readonly ReportSpec Ginekologi = Pemeriksaan("Pemeriksaan Ginekologi", "pemeriksaan_ginekologi_ralan",
        ("inspeksi", "Inspeksi"), ("inspeksi_vulva", "Inspeksi Vulva"), ("inspekulo_gine", "Inspekulo"),
        ("fluxus_gine", "Fluksus"), ("fluor_gine", "Fluor"), ("vulva_inspekulo", "Vulva Inspekulo"),
        ("portio_inspekulo", "Portio Inspekulo"), ("sondage", "Sondage"), ("portio_dalam", "Portio Dalam"),
        ("bentuk", "Bentuk"), ("cavum_uteri", "Cavum Uteri"), ("mobilitas", "Mobilitas"), ("ukuran", "Ukuran"),
        ("nyeri_tekan", "Nyeri Tekan"), ("adnexa_kanan", "Adnexa Kanan"), ("adnexa_kiri", "Adnexa Kiri"),
        ("cavum_douglas", "Cavum Douglas"));

    public static readonly ReportSpec Diagnosa = Buat("Diagnosa",
        """
        SELECT s.kd_penyakit,p.nm_penyakit,s.prioritas,s.status_penyakit FROM diagnosa_pasien s
        JOIN penyakit p ON p.kd_penyakit=s.kd_penyakit WHERE s.no_rawat=@p1 AND s.status='Ralan'
        AND CONCAT_WS(' ',s.kd_penyakit,p.nm_penyakit) LIKE @p0 ORDER BY s.prioritas,s.kd_penyakit
        """, ("kd_penyakit", "Kode ICD-10"), ("nm_penyakit", "Diagnosa"), ("prioritas", "Prioritas"), ("status_penyakit", "Status"));

    public static readonly ReportSpec Lab = Permintaan("Permintaan Lab", "permintaan_lab");
    public static readonly ReportSpec Radiologi = Permintaan("Permintaan Radiologi", "permintaan_radiologi");

    public static readonly ReportSpec Riwayat = Buat("Riwayat Pasien",
        """
        SELECT r.no_rawat,r.tgl_registrasi,p.nm_poli,d.nm_dokter,r.status_lanjut,r.stts
        FROM reg_periksa r LEFT JOIN poliklinik p ON p.kd_poli=r.kd_poli LEFT JOIN dokter d ON d.kd_dokter=r.kd_dokter
        WHERE r.no_rkm_medis=(SELECT a.no_rkm_medis FROM reg_periksa a WHERE a.no_rawat=@p1)
        AND CONCAT_WS(' ',r.no_rawat,p.nm_poli,d.nm_dokter) LIKE @p0 ORDER BY r.tgl_registrasi DESC,r.no_rawat DESC
        LIMIT 500
        """, ("no_rawat", "No. Rawat"), ("tgl_registrasi", "Tanggal"), ("nm_poli", "Poli"),
        ("nm_dokter", "Dokter"), ("status_lanjut", "Jenis Rawat"), ("stts", "Status"));

    public static readonly ReportSpec Resume = Buat("Resume Pasien Rawat Jalan",
        """
        SELECT d.nm_dokter,s.keluhan_utama,s.jalannya_penyakit,s.pemeriksaan_penunjang,s.hasil_laborat,
               s.diagnosa_utama,s.prosedur_utama,s.kondisi_pulang,s.obat_pulang
        FROM resume_pasien s LEFT JOIN dokter d ON d.kd_dokter=s.kd_dokter WHERE s.no_rawat=@p1
        AND CONCAT_WS(' ',d.nm_dokter,s.keluhan_utama,s.diagnosa_utama) LIKE @p0
        """, ("nm_dokter", "Dokter"), ("keluhan_utama", "Keluhan Utama"), ("jalannya_penyakit", "Jalannya Penyakit"),
        ("pemeriksaan_penunjang", "Pemeriksaan Penunjang"), ("hasil_laborat", "Hasil Laboratorium"), ("diagnosa_utama", "Diagnosa Utama"),
        ("prosedur_utama", "Prosedur Utama"), ("kondisi_pulang", "Kondisi Pulang"), ("obat_pulang", "Obat Pulang"));

    public static readonly ReportSpec Obat = Buat("Data Obat & BHP",
        """
        SELECT s.tgl_perawatan,s.jam,s.kode_brng,b.nama_brng,s.jml,s.total FROM detail_pemberian_obat s
        LEFT JOIN databarang b ON b.kode_brng=s.kode_brng WHERE s.no_rawat=@p1 AND s.status='Ralan'
        AND CONCAT_WS(' ',s.kode_brng,b.nama_brng) LIKE @p0 ORDER BY s.tgl_perawatan DESC,s.jam DESC,s.kode_brng
        """, ("tgl_perawatan", "Tanggal"), ("jam", "Jam"), ("kode_brng", "Kode"), ("nama_brng", "Obat/BHP"), ("jml", "Jumlah"), ("total", "Total"));

    private static ReportSpec Pemeriksaan(string judul, string tabel, params (string Column, string Header)[] fields)
    {
        var columns = string.Join(",", fields.Select(f => $"`{f.Column}`"));
        return Buat(judul, $"""
            SELECT tgl_perawatan,jam_rawat,{columns} FROM `{tabel}` WHERE no_rawat=@p1
            AND CONCAT_WS(' ',{columns}) LIKE @p0 ORDER BY tgl_perawatan DESC,jam_rawat DESC
            """, [("tgl_perawatan", "Tanggal"), ("jam_rawat", "Jam"), .. fields]);
    }

    private static ReportSpec Permintaan(string judul, string tabel) => Buat(judul, $"""
        SELECT s.noorder,s.tgl_permintaan,s.jam_permintaan,d.nm_dokter,s.diagnosa_klinis,s.informasi_tambahan
        FROM `{tabel}` s LEFT JOIN dokter d ON d.kd_dokter=s.dokter_perujuk
        WHERE s.no_rawat=@p1 AND s.status='ralan'
        AND CONCAT_WS(' ',s.noorder,d.nm_dokter,s.diagnosa_klinis,s.informasi_tambahan) LIKE @p0
        ORDER BY s.tgl_permintaan DESC,s.jam_permintaan DESC,s.noorder
        """, ("noorder", "No. Permintaan"), ("tgl_permintaan", "Tanggal"), ("jam_permintaan", "Jam"),
        ("nm_dokter", "Dokter Perujuk"), ("diagnosa_klinis", "Diagnosa Klinis"), ("informasi_tambahan", "Informasi Tambahan"));

    private static ReportSpec Buat(string judul, string sql, params (string Column, string Header)[] fields) =>
        new(judul, sql, fields, ReportPeriod.None, "Baca saja") { VisitSql = sql };
}
