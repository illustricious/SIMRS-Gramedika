using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>Satu baris cetak "Data Registrasi Periksa", kolom mengikuti rptReg Khanza.</summary>
public sealed record RegistrasiCetakRow(
    DateTime? Tanggal, string Jam, string Dokter, string NoRm, string Pasien, string NoTelp, string Umur,
    string JenisKelamin, string Poli, string PenanggungJawab, string AlamatPj, string StatusDaftar,
    string AsalRujukan, string JenisBayar)
{
    /// <summary>Data cetak untuk kunjungan yang tampil di daftar, dengan urutan yang sama.</summary>
    public static async Task<IReadOnlyList<RegistrasiCetakRow>> MuatAsync(DatabaseSettings settings, IReadOnlyList<string> noRawat)
    {
        await using var db = await Db.OpenAsync(settings);
        var hasil = new Dictionary<string, RegistrasiCetakRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var bagian in noRawat.Chunk(500))
        {
            // Hanya nama parameter yang disusun ke dalam SQL; nilainya tetap lewat parameter.
            var parameter = string.Join(",", bagian.Select((_, i) => "@p" + i));
            var rows = await db.QueryAsync($"""
                SELECT r.no_rawat,r.tgl_registrasi,r.jam_reg,d.nm_dokter,r.no_rkm_medis,p.nm_pasien,p.no_tlp,
                       CONCAT_WS(' ',r.umurdaftar,r.sttsumur) AS umur,p.jk,pl.nm_poli,r.p_jawab,r.almt_pj,r.stts_daftar,
                       (SELECT MIN(m.perujuk) FROM rujuk_masuk m WHERE m.no_rawat=r.no_rawat) AS perujuk,pj.png_jawab
                FROM reg_periksa r
                JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
                LEFT JOIN dokter d ON d.kd_dokter=r.kd_dokter
                LEFT JOIN poliklinik pl ON pl.kd_poli=r.kd_poli
                LEFT JOIN penjab pj ON pj.kd_pj=r.kd_pj
                WHERE r.no_rawat IN ({parameter})
                """, [.. bagian]);
            foreach (var r in rows)
                hasil[r.S("no_rawat")] = new(r.Dt("tgl_registrasi"), r.S("jam_reg"), r.S("nm_dokter"), r.S("no_rkm_medis"),
                    r.S("nm_pasien"), r.S("no_tlp"), r.S("umur"), r.S("jk"), r.S("nm_poli"), r.S("p_jawab"), r.S("almt_pj"),
                    r.S("stts_daftar"), r.S("perujuk"), r.S("png_jawab"));
        }
        // Kunjungan yang sudah dihapus pengguna lain dilewati.
        return noRawat.Where(hasil.ContainsKey).Select(n => hasil[n]).ToList();
    }
}
