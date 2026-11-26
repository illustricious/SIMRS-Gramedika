using Microsoft.EntityFrameworkCore;

namespace SimrsKhanza.Data;

public partial class KasirRepository
{
    private async Task<IReadOnlyList<KasirRincian>> MuatPenunjangAsync(string noRawat, bool ranap, CancellationToken ct)
    {
        // Only performed exams create charge rows. Order headers and requested items are never charges.
        return await db.Database.SqlQuery<KasirRincian>($"""
            SELECT Jenis,Nama,CAST(Tarif AS DECIMAL(20,4)) AS Tarif,
                   CAST(COUNT(*) AS DECIMAL(20,4)) AS Jumlah,CAST(SUM(Tarif) AS DECIMAL(20,4)) AS Total
            FROM (
                SELECT 'Laborat' AS Jenis,j.kd_jenis_prw AS Kode,j.nm_perawatan AS Nama,
                       p.biaya + COALESCE((SELECT SUM(d.biaya_item) FROM detail_periksa_lab d
                           WHERE d.no_rawat=p.no_rawat AND d.kd_jenis_prw=p.kd_jenis_prw
                             AND d.tgl_periksa=p.tgl_periksa AND d.jam=p.jam),0) AS Tarif
                FROM periksa_lab p JOIN jns_perawatan_lab j ON j.kd_jenis_prw=p.kd_jenis_prw
                WHERE p.no_rawat={noRawat} AND ({ranap} OR p.status='Ralan')
                UNION ALL
                SELECT 'Radiologi',j.kd_jenis_prw,j.nm_perawatan,p.biaya
                FROM periksa_radiologi p JOIN jns_perawatan_radiologi j ON j.kd_jenis_prw=p.kd_jenis_prw
                WHERE p.no_rawat={noRawat} AND ({ranap} OR p.status='Ralan')
            ) periksa
            GROUP BY Jenis,Kode,Nama,Tarif
            ORDER BY Jenis,Nama,Tarif,Kode
            """).ToListAsync(ct);
    }

    private async Task<bool> AdaPermintaanTertundaAsync(string noRawat, bool ranap, CancellationToken ct)
    {
        return await db.Database.SqlQuery<bool>($"""
            SELECT (
                EXISTS(SELECT 1 FROM resep_obat WHERE no_rawat={noRawat} AND ({ranap} OR status='ralan')
                    AND (tgl_perawatan IS NULL OR tgl_perawatan='0000-00-00')) OR
                EXISTS(SELECT 1 FROM permintaan_lab WHERE no_rawat={noRawat} AND ({ranap} OR status='ralan') AND (tgl_hasil IS NULL OR tgl_hasil='0000-00-00')) OR
                EXISTS(SELECT 1 FROM permintaan_labpa WHERE no_rawat={noRawat} AND ({ranap} OR status='ralan') AND (tgl_hasil IS NULL OR tgl_hasil='0000-00-00')) OR
                EXISTS(SELECT 1 FROM permintaan_labmb WHERE no_rawat={noRawat} AND ({ranap} OR status='ralan') AND (tgl_hasil IS NULL OR tgl_hasil='0000-00-00')) OR
                EXISTS(SELECT 1 FROM permintaan_radiologi WHERE no_rawat={noRawat} AND ({ranap} OR status='ralan') AND (tgl_hasil IS NULL OR tgl_hasil='0000-00-00'))
            ) AS Value
            """).SingleAsync(ct);
    }
}
