using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.MenuA;

public sealed class MenuAQueries(DatabaseSettings settings)
{
    public async Task<IReadOnlyList<Row>> CariPasienAsync(string query, bool kunjungan)
    {
        await using var db = await Db.OpenAsync(settings);
        var like = $"%{query.Trim()}%";
        return kunjungan
            ? await db.QueryAsync("""
                SELECT r.no_rawat,r.no_rkm_medis,p.nm_pasien,r.tgl_registrasi
                FROM reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
                WHERE r.stts<>'Batal' AND (r.no_rawat LIKE @p0 OR r.no_rkm_medis LIKE @p0 OR p.nm_pasien LIKE @p0)
                ORDER BY r.tgl_registrasi DESC,r.jam_reg DESC LIMIT 100
                """, like)
            : await db.QueryAsync("""
                SELECT no_rkm_medis,nm_pasien FROM pasien WHERE no_rkm_medis LIKE @p0 OR nm_pasien LIKE @p0
                ORDER BY nm_pasien,no_rkm_medis LIMIT 100
                """, like);
    }

    public async Task<IReadOnlyList<Row>> RujukanTarifAsync(string diagnosis)
    {
        await using var db = await Db.OpenAsync(settings);
        const string filter = """
            WHERE d.kd_penyakit=@p0 AND d.prioritas=1 AND d.status='Ranap' AND r.status_lanjut='Ranap'
            AND r.tgl_registrasi BETWEEN DATE_SUB(CURRENT_DATE(),INTERVAL 2 YEAR) AND CURRENT_DATE()
            """;
        return await db.QueryAsync($"""
            WITH kasus AS (
                SELECT d.no_rawat FROM diagnosa_pasien d JOIN reg_periksa r ON r.no_rawat=d.no_rawat {filter}
            ), tarif_rs AS (
                SELECT SUM(b.totalbiaya) AS nilai FROM kasus k JOIN billing b ON b.no_rawat=k.no_rawat GROUP BY k.no_rawat
            ), tarif_inacbg AS (
                SELECT MAX(i.tarif) AS nilai FROM kasus k JOIN bridging_sep s ON s.no_rawat=k.no_rawat
                JOIN inacbg_grouping_stage1 i ON i.no_sep=s.no_sep GROUP BY k.no_rawat
            )
            SELECT 'Tagihan RS terendah' AS sumber,MIN(nilai) AS tarif FROM tarif_rs HAVING COUNT(nilai)>0
            UNION ALL SELECT 'Tagihan RS tertinggi',MAX(nilai) FROM tarif_rs HAVING COUNT(nilai)>0
            UNION ALL SELECT 'Tarif INACBG terendah',MIN(nilai) FROM tarif_inacbg HAVING COUNT(nilai)>0
            UNION ALL SELECT 'Tarif INACBG tertinggi',MAX(nilai) FROM tarif_inacbg HAVING COUNT(nilai)>0
            """, diagnosis);
    }
}
