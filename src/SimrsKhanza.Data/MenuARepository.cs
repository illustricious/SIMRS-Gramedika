using Microsoft.EntityFrameworkCore;

namespace SimrsKhanza.Data;

public sealed class JadwalPraktekItem
{
    public string KodeDokter { get; set; } = "";
    public string Dokter { get; set; } = "";
    public string Hari { get; set; } = "";
    public TimeSpan Mulai { get; set; }
    public TimeSpan? Selesai { get; set; }
    public string Poli { get; set; } = "";
    public int? Kuota { get; set; }
}
public sealed class InformasiKamarItem
{
    public string Bangsal { get; set; } = "";
    public string Kamar { get; set; } = "";
    public string Kelas { get; set; } = "";
    public string Status { get; set; } = "";
    public double Tarif { get; set; }
}
public sealed class MenuARepository(SimrsKhanzaDbContext db)
{
    public Task<int> TambahJadwalAsync(string dokter, string hari, TimeSpan mulai, TimeSpan selesai, string poli, int kuota) =>
        db.Database.ExecuteSqlRawAsync("insert into jadwal(kd_dokter,hari_kerja,jam_mulai,jam_selesai,kd_poli,kuota) values({0},{1},{2},{3},{4},{5})", dokter, hari, mulai, selesai, poli, kuota);
    public Task<List<BookingPeriksaItem>> BookingPeriksaAsync(DateTime from, DateTime to, string keyword) => db.Database.SqlQueryRaw<BookingPeriksaItem>(@"select b.no_booking NoBooking,b.tanggal Tanggal,b.nama Nama,b.alamat Alamat,b.no_telp Telepon,b.email Email,coalesce(p.nm_poli,'') Poli,b.tambahan_pesan Pesan,b.status Status,b.tanggal_booking WaktuBooking from booking_periksa b left join poliklinik p on p.kd_poli=b.kd_poli where b.tanggal between {0} and {1} and (b.no_booking like {2} or b.nama like {2} or b.no_telp like {2} or p.nm_poli like {2} or b.status like {2}) order by b.tanggal,b.no_booking",from.Date,to.Date,"%"+keyword+"%").ToListAsync();
    public Task<List<BookingRegistrasiItem>> BookingRegistrasiAsync(DateTime from, DateTime to, string keyword) => db.Database.SqlQueryRaw<BookingRegistrasiItem>(@"select b.tanggal_periksa Tanggal,b.no_reg NoReg,b.no_rkm_medis NoRm,p.nm_pasien Pasien,coalesce(d.nm_dokter,'') Dokter,coalesce(pol.nm_poli,'') Poli,coalesce(pj.png_jawab,'') JenisBayar,coalesce(b.status,'') Status,b.tanggal_booking TanggalBooking,b.jam_booking JamBooking from booking_registrasi b join pasien p on p.no_rkm_medis=b.no_rkm_medis left join dokter d on d.kd_dokter=b.kd_dokter left join poliklinik pol on pol.kd_poli=b.kd_poli left join penjab pj on pj.kd_pj=b.kd_pj where b.tanggal_periksa between {0} and {1} and (b.no_rkm_medis like {2} or p.nm_pasien like {2} or d.nm_dokter like {2} or pol.nm_poli like {2} or b.no_reg like {2}) order by b.tanggal_periksa,b.no_reg",from.Date,to.Date,"%"+keyword+"%").ToListAsync();
    public Task<List<BookingMcuItem>> BookingMcuAsync(DateTime from, DateTime to, string keyword) => db.Database.SqlQueryRaw<BookingMcuItem>(@"select b.no_mcu NoMcu,b.tanggal_mcu Tanggal,b.no_rkm_medis NoRm,p.nm_pasien Pasien,b.kode_perusahaan KodePerusahaan,coalesce(b.status,'') Status,b.tanggal_booking TanggalBooking,b.jam_booking JamBooking from booking_mcu_perusahaan b join pasien p on p.no_rkm_medis=b.no_rkm_medis where b.tanggal_mcu between {0} and {1} and (b.no_mcu like {2} or b.no_rkm_medis like {2} or p.nm_pasien like {2} or b.kode_perusahaan like {2}) order by b.tanggal_mcu,b.no_mcu",from.Date,to.Date,"%"+keyword+"%").ToListAsync();
    public Task<List<JadwalPraktekItem>> JadwalAsync(string keyword) => db.Database.SqlQueryRaw<JadwalPraktekItem>(@"select j.kd_dokter KodeDokter,d.nm_dokter Dokter,j.hari_kerja Hari,j.jam_mulai Mulai,j.jam_selesai Selesai,coalesce(p.nm_poli,'') Poli,j.kuota Kuota from jadwal j join dokter d on d.kd_dokter=j.kd_dokter left join poliklinik p on p.kd_poli=j.kd_poli where d.nm_dokter like {0} or j.kd_dokter like {0} or p.nm_poli like {0} or j.hari_kerja like {0} order by d.nm_dokter,j.hari_kerja,j.jam_mulai", "%" + keyword + "%").ToListAsync();
    public Task<List<InformasiKamarItem>> KamarAsync(string keyword) => db.Database.SqlQueryRaw<InformasiKamarItem>(@"select b.nm_bangsal Bangsal,k.kd_kamar Kamar,k.kelas Kelas,k.status Status,k.trf_kamar Tarif from kamar k join bangsal b on b.kd_bangsal=k.kd_bangsal where k.statusdata='1' and b.status='1' and (b.nm_bangsal like {0} or k.kd_kamar like {0}) order by b.nm_bangsal,k.status,k.kd_kamar", "%" + keyword + "%").ToListAsync();
}

public sealed class BookingPeriksaItem { public string NoBooking { get; set; } = ""; public DateTime? Tanggal { get; set; } public string? Nama { get; set; } public string? Alamat { get; set; } public string? Telepon { get; set; } public string? Email { get; set; } public string Poli { get; set; } = ""; public string? Pesan { get; set; } public string Status { get; set; } = ""; public DateTime WaktuBooking { get; set; } }
public sealed class BookingRegistrasiItem { public DateTime Tanggal { get; set; } public string? NoReg { get; set; } public string NoRm { get; set; } = ""; public string Pasien { get; set; } = ""; public string Dokter { get; set; } = ""; public string Poli { get; set; } = ""; public string JenisBayar { get; set; } = ""; public string Status { get; set; } = ""; public DateTime? TanggalBooking { get; set; } public TimeSpan? JamBooking { get; set; } }
public sealed class BookingMcuItem { public string NoMcu { get; set; } = ""; public DateTime Tanggal { get; set; } public string NoRm { get; set; } = ""; public string Pasien { get; set; } = ""; public string KodePerusahaan { get; set; } = ""; public string Status { get; set; } = ""; public DateTime? TanggalBooking { get; set; } public TimeSpan? JamBooking { get; set; } }
