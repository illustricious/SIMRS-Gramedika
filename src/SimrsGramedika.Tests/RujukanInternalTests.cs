using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Registrasi;

namespace SimrsGramedika.Tests;

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
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RujukanInternalService(settings, allowed).TambahAsync("RAWAT", "DR", "POLI"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RujukanInternalService(settings, allowed).HapusAsync(null!));
    }

    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_RUJUKAN_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema kosong simrs_rujukan_fixture pada MariaDB lokal dengan tabel Registrasi, rujukan, penyakit dan kamar_inap.";
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
        var service = new RujukanInternalService(settings, new AuthenticatedUser { Permissions = new HashSet<string> { "registrasi", "rujukan_poli_internal" } });
        await service.TambahAsync("RUJUK-UJI", "DR-TUJUAN", "PT");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TambahAsync("RUJUK-UJI", "DR-TUJUAN", "PA"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        var row = Assert.Single(await service.DaftarAsync(today, today, "DR-TUJUAN", "PT", "Pasien Uji Rujukan"));
        Assert.Equal("RUJUK-UJI", row.NoRawat); Assert.Equal("Dokter Tujuan", row.Dokter); Assert.Equal("Poli Tujuan", row.Poli);
        var pdf = RegistrasiReport.GenerateRujukanPdf([row]);
        try
        {
            var bytes = await File.ReadAllBytesAsync(pdf);
            Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        }
        finally { File.Delete(pdf); }
        Assert.Empty(await service.DaftarAsync(today, today, "DR-ASAL", "PT", ""));
        Assert.Empty(await service.DaftarAsync(today, today, "", "PA", ""));
        Assert.Empty(await service.DaftarAsync(today.AddDays(-2), today.AddDays(-1), null, null, ""));
        Assert.Empty(await service.DaftarAsync(today, today, null, null, "tidak cocok"));
        await service.TambahAsync("RUJUK-UJI", "DR-ASAL", "PA");
        Assert.False(await service.HapusAsync(row with { KodePoli = "PA" }));
        Assert.True(await service.HapusAsync(row));
        Assert.False(await service.HapusAsync(row));
        Assert.Equal("DR-ASAL", Assert.Single(await service.DaftarAsync(today, today, null, null, null)).KodeDokter);

        await db.ExecAsync("INSERT INTO penyakit(kd_penyakit,nm_penyakit,status,validcode) VALUES ('R69','Diagnosis Uji','Tidak Menular','1')");
        var user = new AuthenticatedUser { Permissions = new HashSet<string> { "registrasi", "rujukan_masuk" } };
        var incoming = new RujukanMasukService(settings, user);
        var info = new RujukanMasukData("Faskes Uji", "Alamat Uji", "R-01", 0, "Dokter Uji", "R69", "-", "Uji", "B-01");
        Assert.Null(await incoming.BacaAsync("RUJUK-UJI"));
        await incoming.SimpanAsync("RUJUK-UJI", info, null);
        Assert.Equal(info, await incoming.BacaAsync("RUJUK-UJI"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => incoming.SimpanAsync("RUJUK-UJI", info, null));
        var updated = info with { Perujuk = "Faskes Uji 2" };
        await incoming.SimpanAsync("RUJUK-UJI", updated, info);
        await Assert.ThrowsAsync<InvalidOperationException>(() => incoming.SimpanAsync("RUJUK-UJI", info, info));
        Assert.Equal(updated, await incoming.BacaAsync("RUJUK-UJI"));
        Assert.Equal(new PerujukTerdaftar(updated.Perujuk, updated.Alamat), Assert.Single(await incoming.CariPerujukAsync("Faskes Uji")));
        Assert.Empty(await incoming.CariPerujukAsync("tidak cocok"));
        var denied = new RujukanMasukService(settings, new AuthenticatedUser { Permissions = new HashSet<string> { "registrasi" } });
        await Assert.ThrowsAsync<InvalidOperationException>(() => denied.SimpanAsync("RUJUK-UJI", info, updated));

        var registrasi = new RegistrasiService(settings);
        var baru = new RegistrasiBaru("RM-RUJUK", "DR-ASAL", "PA", "PJ", "PJ Uji", "Alamat Uji", "DIRI SENDIRI", DateTime.Now);
        var akses = new RegistrasiAkses(false, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registrasi.DaftarAsync(baru, akses, rujukan: info with { Diagnosa = "TIDAK-ADA" }, pengguna: user));
        Assert.Equal(1L, await db.CountAsync("SELECT COUNT(*) FROM reg_periksa WHERE no_rkm_medis='RM-RUJUK'"));
        var noRawatBaru = await registrasi.DaftarAsync(baru, akses, rujukan: info, pengguna: user);
        Assert.Equal(info, await incoming.BacaAsync(noRawatBaru));
    }
}
