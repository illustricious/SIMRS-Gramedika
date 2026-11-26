using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class PulangRanapTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_PULANG_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_pulang_fixture.";
        }
    }

    [FixtureFact]
    public async Task ResumeDanPulangMempertahankanIdentitasTagihanDanKamar()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_pulang_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_PULANG_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=0,sql_mode=''");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM','Pasien Uji')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('DR','Dokter Uji'),('LAIN','Dokter Lain')");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('BG','Bangsal Uji','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status,trf_kamar) VALUES ('KM','BG','Kelas 1','ISI',250000)");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_dokter,kd_pj,status_lanjut,stts,status_bayar)
            VALUES ('RANAP-UJI','RM','DR','PJ','Ranap','Belum','Belum Bayar')
            """);
        await db.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang,trf_kamar)
            VALUES ('RANAP-UJI','KM','2026-01-01','08:00:00','-',250000)
            """);
        await db.ExecAsync("INSERT INTO penyakit(kd_penyakit,nm_penyakit) VALUES ('R69','Diagnosis Uji')");
        await db.ExecAsync("INSERT INTO diagnosa_pasien(no_rawat,kd_penyakit,status,prioritas) VALUES ('RANAP-UJI','R69','Ranap',1)");
        var doctor = new AuthenticatedUser { UserId = "DR", Permissions = new HashSet<string> { "data_resume_pasien" } };
        var service = new ResumePulangRanapService(settings, "RANAP-UJI", "RM", doctor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ResumePulangRanapService(settings, "RANAP-UJI", "RM", new()).MuatAsync());
        string[] input = ResumePulangRanapService.Isian.Select(f => f.Pilihan?.First() ?? "").ToArray();
        input[2] = "Keluhan sintetis";
        input[^1] = "Instruksi obat sintetis";
        var kontrol = new DateTime(2026, 1, 6, 9, 0, 0);
        await service.SimpanAsync(input, kontrol, null);
        var saved = Assert.IsType<ResumePulangRanap>(await service.MuatAsync());
        Assert.Equal(kontrol, saved.Kontrol);
        Assert.Contains("R69 Diagnosis Uji", saved.KodeKlinis);
        var other = new ResumePulangRanapService(settings, "RANAP-UJI", "RM",
            new AuthenticatedUser { UserId = "LAIN", Permissions = doctor.Permissions });
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.SimpanAsync(input, null, saved));
        await service.SimpanAsync(input, null, saved);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(input, kontrol, saved));

        var key = new RawatInapKey("RANAP-UJI", "KM", new DateOnly(2026, 1, 1), new TimeOnly(8, 0));
        await using var ctx = SimrsGramedikaDbContextFactory.Create(settings);
        var repo = new RawatInapRepository(ctx);
        var before = new DateTime(2026, 1, 1, 7, 0, 0);
        await Assert.ThrowsAsync<ArgumentException>(() => repo.DischargeAsync(key, StatusPulang.Sembuh, "Uji", waktuKeluar: before));
        Assert.Equal("-", await db.StrAsync("SELECT stts_pulang FROM kamar_inap"));
        Assert.Equal("ISI", await db.StrAsync("SELECT status FROM kamar"));
        var keluar = new DateTime(2026, 1, 2, 10, 25, 0);
        Assert.Equal(250000m, await repo.DischargeAsync(key, StatusPulang.Sembuh, "Uji", waktuKeluar: keluar));
        Assert.Equal("Sembuh", await db.StrAsync("SELECT stts_pulang FROM kamar_inap"));
        Assert.Equal("10:25:00", await db.StrAsync("SELECT jam_keluar FROM kamar_inap"));
        Assert.Equal("KOSONG", await db.StrAsync("SELECT status FROM kamar"));
        Assert.Equal("Belum Bayar", await db.StrAsync("SELECT status_bayar FROM reg_periksa"));
        Assert.NotNull(await service.MuatAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.DischargeAsync(key, StatusPulang.Sembuh, "Uji", waktuKeluar: keluar));
    }
}
