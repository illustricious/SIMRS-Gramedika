using Microsoft.EntityFrameworkCore;

namespace SimrsGramedika.Data;

public sealed class NoResepRow
{
    public string NoResep { get; set; } = "";
    public DateTime Tanggal { get; set; }
    public TimeSpan Jam { get; set; }
    public string NoRawat { get; set; } = "";
    public string NoRm { get; set; } = "";
    public string Pasien { get; set; } = "";
    public string Dokter { get; set; } = "";
}
public sealed class NoResepObatRow
{
    public string Obat { get; set; } = "";
    public double Jumlah { get; set; }
    public double Harga { get; set; }
    public double Embalase { get; set; }
    public double Tuslah { get; set; }
    public double Total { get; set; }
    public string Aturan { get; set; } = "";
}
public sealed class NoResepRepository(SimrsGramedikaDbContext db)
{
    public Task<List<NoResepRow>> CariAsync(DateTime from, DateTime to, string keyword) =>
        db.Database.SqlQueryRaw<NoResepRow>(@"select r.no_resep NoResep,r.tgl_perawatan Tanggal,r.jam Jam,r.no_rawat NoRawat,
            p.no_rkm_medis NoRm,p.nm_pasien Pasien,d.nm_dokter Dokter
            from resep_obat r join reg_periksa reg on reg.no_rawat=r.no_rawat
            join pasien p on p.no_rkm_medis=reg.no_rkm_medis join dokter d on d.kd_dokter=r.kd_dokter
            where r.tgl_perawatan between {0} and {1} and (r.no_resep like {2} or r.no_rawat like {2}
            or p.no_rkm_medis like {2} or p.nm_pasien like {2} or d.nm_dokter like {2})
            order by r.tgl_perawatan,r.jam", from.Date, to.Date, "%" + keyword + "%").ToListAsync();

    public Task<List<NoResepObatRow>> ObatAsync(NoResepRow r) =>
        db.Database.SqlQueryRaw<NoResepObatRow>(@"select b.nama_brng Obat,o.jml Jumlah,coalesce(o.biaya_obat,0) Harga,
            coalesce(o.embalase,0) Embalase,coalesce(o.tuslah,0) Tuslah,o.total Total,coalesce(a.aturan,'') Aturan
            from detail_pemberian_obat o join databarang b on b.kode_brng=o.kode_brng
            left join aturan_pakai a on a.tgl_perawatan=o.tgl_perawatan and a.jam=o.jam and a.no_rawat=o.no_rawat and a.kode_brng=o.kode_brng
            where o.tgl_perawatan={0} and o.jam={1} and o.no_rawat={2}
            and not exists (select 1 from detail_obat_racikan x where x.tgl_perawatan=o.tgl_perawatan and x.jam=o.jam and x.no_rawat=o.no_rawat and x.kode_brng=o.kode_brng)", r.Tanggal.Date, r.Jam, r.NoRawat).ToListAsync();

    public async Task<string> SimpanAsync(string noRawat, string dokter, DateTime tanggal, TimeSpan jam, string status)
    {
        var date = tanggal.Date;
        var prefix = date.ToString("yyyyMMdd");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var last = await db.Database.SqlQueryRaw<int>("select coalesce(max(cast(right(no_resep,4) as unsigned)),0) as Value from resep_obat where tgl_peresepan={0} or tgl_perawatan={0}", date).SingleAsync();
            if (last >= 9999) throw new InvalidOperationException("Nomor resep untuk tanggal ini sudah penuh.");
            var number = prefix + (last + 1).ToString("D4");
            try
            {
                await db.Database.ExecuteSqlRawAsync("insert into resep_obat(no_resep,tgl_perawatan,jam,no_rawat,kd_dokter,tgl_peresepan,jam_peresepan,status,tgl_penyerahan,jam_penyerahan) values({0},{1},{2},{3},{4},'0000-00-00','00:00:00',{5},'0000-00-00','00:00:00')", number,date,jam,noRawat,dokter,status);
                return number;
            }
            catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062 && attempt < 4) { }
        }
        throw new InvalidOperationException("Nomor resep sedang digunakan. Silakan coba lagi.");
    }
}
