using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data;

public sealed record RujukanInternalItem(string NoRawat, string Tanggal, string Jam, string KodeDokter, string Dokter,
    string NoRm, string Pasien, string JenisKelamin, string Umur, string KodePoli, string Poli,
    string JenisBayar, string PenanggungJawab, string Alamat, string Hubungan, string StatusDaftar, string Status);

public sealed class RujukanInternalService(DatabaseSettings settings, AuthenticatedUser user)
{
    public async Task<IReadOnlyList<RujukanInternalItem>> DaftarAsync(DateOnly from, DateOnly to, string? dokter, string? poli, string? keyword)
    {
        if (!user.HasPermission("registrasi")) throw new InvalidOperationException("Akun tidak memiliki hak registrasi.");
        if (from > to) throw new ArgumentException("Tanggal awal tidak boleh setelah tanggal akhir.");
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT r.no_rawat,r.tgl_registrasi,r.jam_reg,ri.kd_dokter,d.nm_dokter,r.no_rkm_medis,p.nm_pasien,p.jk,
                   CONCAT(r.umurdaftar,' ',r.sttsumur) AS umur,ri.kd_poli,pol.nm_poli,pj.png_jawab,
                   r.p_jawab,r.almt_pj,r.hubunganpj,r.stts_daftar,r.stts
            FROM rujukan_internal_poli ri JOIN reg_periksa r ON r.no_rawat=ri.no_rawat
            JOIN dokter d ON d.kd_dokter=ri.kd_dokter JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN poliklinik pol ON pol.kd_poli=ri.kd_poli JOIN penjab pj ON pj.kd_pj=r.kd_pj
            WHERE r.tgl_registrasi BETWEEN @p0 AND @p1 AND (@p2='' OR ri.kd_dokter=@p2) AND (@p3='' OR ri.kd_poli=@p3)
              AND (r.no_reg LIKE @p4 OR r.no_rawat LIKE @p4 OR r.tgl_registrasi LIKE @p4 OR ri.kd_dokter LIKE @p4
                   OR d.nm_dokter LIKE @p4 OR r.no_rkm_medis LIKE @p4 OR r.stts_daftar LIKE @p4 OR p.nm_pasien LIKE @p4
                   OR pol.nm_poli LIKE @p4 OR r.p_jawab LIKE @p4 OR r.almt_pj LIKE @p4 OR r.hubunganpj LIKE @p4 OR pj.png_jawab LIKE @p4)
            ORDER BY r.tgl_registrasi DESC,r.jam_reg DESC,r.no_rawat,ri.kd_dokter
            """, from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue), dokter ?? "", poli ?? "", "%" + (keyword ?? "").Trim() + "%");
        return rows.Select(r => new RujukanInternalItem(r.S("no_rawat"), r.S("tgl_registrasi"), r.S("jam_reg"), r.S("kd_dokter"), r.S("nm_dokter"),
            r.S("no_rkm_medis"), r.S("nm_pasien"), r.S("jk"), r.S("umur"), r.S("kd_poli"), r.S("nm_poli"), r.S("png_jawab"),
            r.S("p_jawab"), r.S("almt_pj"), r.S("hubunganpj"), r.S("stts_daftar"), r.S("stts"))).ToList();
    }
}
