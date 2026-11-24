using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Tests;

public class RujukanInternalTests
{
    [Fact]
    public async Task HakAksesDanPeriodeDiperiksaSebelumKoneksi()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Port = 1 };
        var today = DateOnly.FromDateTime(DateTime.Today);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RujukanInternalService(settings, new AuthenticatedUser()).DaftarAsync(today, today, null, null, null));
        var allowed = new AuthenticatedUser { Permissions = new HashSet<string> { "registrasi" } };
        await Assert.ThrowsAsync<ArgumentException>(() => new RujukanInternalService(settings, allowed).DaftarAsync(today, today.AddDays(-1), null, null, null));
    }

    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_RUJUKAN_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema kosong simrs_rujukan_fixture pada MariaDB lokal.";
        }
    }

    [FixtureFact]
    public async Task DaftarMenggunakanDokterDanPoliTujuan()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_RUJUKAN_FIXTURE_PORT")!), Database = "simrs_rujukan_fixture", User = "root", Password = Environment.GetEnvironmentVariable("SIMRS_RUJUKAN_FIXTURE_PASSWORD") ?? "" };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM-RUJUK','Pasien Uji Rujukan')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('DR-ASAL','Dokter Asal'),('DR-TUJUAN','Dokter Tujuan')");
        await db.ExecAsync("INSERT INTO poliklinik(kd_poli,nm_poli) VALUES ('PA','Poli Asal'),('PT','Poli Tujuan')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Umum')");
        await db.ExecAsync("INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_dokter,kd_poli,kd_pj,status_lanjut,stts,tgl_registrasi,jam_reg) VALUES ('RUJUK-UJI','RM-RUJUK','DR-ASAL','PA','PJ','Ralan','Belum',CURRENT_DATE,'10:00:00')");
        await db.ExecAsync("INSERT INTO rujukan_internal_poli(no_rawat,kd_dokter,kd_poli) VALUES ('RUJUK-UJI','DR-TUJUAN','PT')");
        var service = new RujukanInternalService(settings, new AuthenticatedUser { Permissions = new HashSet<string> { "registrasi" } });
        var today = DateOnly.FromDateTime(DateTime.Today);
        var row = Assert.Single(await service.DaftarAsync(today, today, "DR-TUJUAN", "PT", "Pasien Uji Rujukan"));
        Assert.Equal("RUJUK-UJI", row.NoRawat); Assert.Equal("Dokter Tujuan", row.Dokter); Assert.Equal("Poli Tujuan", row.Poli);
        Assert.Empty(await service.DaftarAsync(today, today, "DR-ASAL", "PT", ""));
        Assert.Empty(await service.DaftarAsync(today, today, "", "PA", ""));
        Assert.Empty(await service.DaftarAsync(today.AddDays(-2), today.AddDays(-1), null, null, ""));
        Assert.Empty(await service.DaftarAsync(today, today, null, null, "tidak cocok"));
    }
}
