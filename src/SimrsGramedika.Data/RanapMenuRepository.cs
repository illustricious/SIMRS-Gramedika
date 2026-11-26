using Microsoft.EntityFrameworkCore;
namespace SimrsGramedika.Data;

public sealed class DpjpRow { public DateTime? Tanggal {get;set;} public string NoRawat {get;set;}=""; public string NoRm {get;set;}=""; public string Pasien {get;set;}=""; public string KodeDokter {get;set;}=""; public string Dokter {get;set;}=""; }
public sealed class TindakanRanapRow { public string NoRawat {get;set;}=""; public string NoRm {get;set;}=""; public string Pasien {get;set;}=""; public string Tindakan {get;set;}=""; public string Pelaksana {get;set;}=""; public DateTime Tanggal {get;set;} public TimeSpan Jam {get;set;} public double? Biaya {get;set;} }
public sealed class OperasiRow { public string NoRawat {get;set;}=""; public string NoRm {get;set;}=""; public string Pasien {get;set;}=""; public DateTime TanggalOperasi {get;set;} public string? JenisAnestesi {get;set;} }
public sealed class RanapMenuRepository(SimrsGramedikaDbContext db)
{
    public async Task SimpanDpjpAsync(string rawat, string dokter, DpjpRow? lama)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (lama is not null)
        {
            var removed = await db.Database.ExecuteSqlRawAsync("delete from dpjp_ranap where no_rawat={0} and kd_dokter={1}", lama.NoRawat, lama.KodeDokter);
            if (removed != 1) throw new InvalidOperationException("Data DPJP berubah atau sudah dihapus. Muat ulang daftar.");
        }
        await db.Database.ExecuteSqlRawAsync("insert into dpjp_ranap(no_rawat,kd_dokter) values({0},{1})", rawat, dokter);
        await tx.CommitAsync();
    }
    public async Task HapusDpjpAsync(DpjpRow row)
    {
        var count = await db.Database.ExecuteSqlRawAsync("delete from dpjp_ranap where no_rawat={0} and kd_dokter={1}",row.NoRawat,row.KodeDokter);
        if (count != 1) throw new InvalidOperationException("Data DPJP sudah berubah. Muat ulang daftar.");
    }
    public Task<List<DpjpRow>> DpjpAsync(DateTime from, DateTime to, string q) => db.Database.SqlQueryRaw<DpjpRow>(@"select r.tgl_registrasi Tanggal,x.no_rawat NoRawat,r.no_rkm_medis NoRm,p.nm_pasien Pasien,d.kd_dokter KodeDokter,d.nm_dokter Dokter from dpjp_ranap x join reg_periksa r on r.no_rawat=x.no_rawat join pasien p on p.no_rkm_medis=r.no_rkm_medis join dokter d on d.kd_dokter=x.kd_dokter where r.tgl_registrasi between {0} and {1} and (x.no_rawat like {2} or r.no_rkm_medis like {2} or p.nm_pasien like {2} or d.nm_dokter like {2}) order by r.tgl_registrasi",from.Date,to.Date,"%"+q+"%").ToListAsync();
    public Task<List<TindakanRanapRow>> TindakanAsync(DateTime from, DateTime to, string q, int kategori)
    {
        var table = kategori switch { 0 => "rawat_inap_dr", 1 => "rawat_inap_pr", _ => "rawat_inap_drpr" };
        var name = kategori switch { 0 => "d.nm_dokter", 1 => "pet.nama", _ => "concat(d.nm_dokter, ' / ', pet.nama)" };
        var joins = kategori switch { 0 => "join dokter d on d.kd_dokter=x.kd_dokter", 1 => "join petugas pet on pet.nip=x.nip", _ => "join dokter d on d.kd_dokter=x.kd_dokter join petugas pet on pet.nip=x.nip" };
        return db.Database.SqlQueryRaw<TindakanRanapRow>($@"select x.no_rawat NoRawat,r.no_rkm_medis NoRm,p.nm_pasien Pasien,j.nm_perawatan Tindakan,{name} Pelaksana,x.tgl_perawatan Tanggal,x.jam_rawat Jam,x.biaya_rawat Biaya from {table} x join reg_periksa r on r.no_rawat=x.no_rawat join pasien p on p.no_rkm_medis=r.no_rkm_medis join jns_perawatan_inap j on j.kd_jenis_prw=x.kd_jenis_prw {joins} where x.tgl_perawatan between {{0}} and {{1}} and (x.no_rawat like {{2}} or r.no_rkm_medis like {{2}} or p.nm_pasien like {{2}} or j.nm_perawatan like {{2}}) order by x.tgl_perawatan,x.jam_rawat",from.Date,to.Date,"%"+q+"%").ToListAsync();
    }
    public Task<List<OperasiRow>> OperasiAsync(DateTime from, DateTime to, string q) => db.Database.SqlQueryRaw<OperasiRow>(@"select x.no_rawat NoRawat,r.no_rkm_medis NoRm,p.nm_pasien Pasien,x.tgl_operasi TanggalOperasi,max(x.jenis_anasthesi) JenisAnestesi from operasi x join reg_periksa r on r.no_rawat=x.no_rawat join pasien p on p.no_rkm_medis=r.no_rkm_medis where x.tgl_operasi >= {0} and x.tgl_operasi < {1} and (x.no_rawat like {2} or r.no_rkm_medis like {2} or p.nm_pasien like {2}) group by x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.tgl_operasi order by x.tgl_operasi",from.Date,to.Date.AddDays(1),"%"+q+"%").ToListAsync();
}
