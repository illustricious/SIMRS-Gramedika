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

        await HapusDataSalahAsync(db, settings);
    }

    private static async Task HapusDataSalahAsync(Db db, DatabaseSettings settings)
    {
        // Pasien SALAH pindah dari KM (kini diisi pasien lain) ke KM2.
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status,trf_kamar) VALUES ('KM2','BG','Kelas 1','ISI',250000)");
        await db.ExecAsync("UPDATE kamar SET status='ISI' WHERE kd_kamar='KM'");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_dokter,kd_pj,status_lanjut,stts,status_bayar)
            VALUES ('SALAH','RM','DR','PJ','Ranap','Belum','Belum Bayar')
            """);
        await db.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang,trf_kamar) VALUES
                ('SALAH','KM','2026-01-03','08:00:00','Pindah Kamar',250000),('SALAH','KM2','2026-01-04','08:00:00','-',250000),
                ('LAIN','KM','2026-01-05','08:00:00','-',250000)
            """);
        var pindah = new RawatInapKey("SALAH", "KM", new DateOnly(2026, 1, 3), new TimeOnly(8, 0));
        var aktif = new RawatInapKey("SALAH", "KM2", new DateOnly(2026, 1, 4), new TimeOnly(8, 0));
        async Task<bool> HapusAsync(RawatInapKey kunci, bool admin)
        {
            await using var ctx = SimrsGramedikaDbContextFactory.Create(settings);
            return await new RawatInapRepository(ctx).HapusDataSalahAsync(kunci, admin);
        }

        await db.ExecAsync("DELETE FROM set_jam_minimal");
        var mati = await Assert.ThrowsAsync<InvalidOperationException>(() => HapusAsync(aktif, false));
        Assert.Contains("tidak diaktifkan", mati.Message);
        await db.ExecAsync("""
            INSERT INTO set_jam_minimal(lamajam,hariawal,feeperujuk,diagnosaakhir,bayi,aktifkan_hapus_data_salah,ubah_status_kamar)
            VALUES (1,'No',0,'No',0,'Yes','No')
            """);
        await db.ExecAsync("INSERT INTO billing(noindex,no_rawat,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya) VALUES(0,'SALAH','','Uji',':',0,0,0,0)");
        await Assert.ThrowsAsync<InvalidOperationException>(() => HapusAsync(aktif, true));
        await db.ExecAsync("DELETE FROM billing WHERE no_rawat='SALAH'");
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM kamar_inap WHERE no_rawat='SALAH'"));

        Assert.False(await HapusAsync(aktif, false));
        Assert.Equal("KOSONG", await db.StrAsync("SELECT status FROM kamar WHERE kd_kamar='KM2'"));
        Assert.Equal("Ranap", await db.StrAsync("SELECT status_lanjut FROM reg_periksa WHERE no_rawat='SALAH'"));
        Assert.Equal("Pindah Kamar", await db.StrAsync("SELECT stts_pulang FROM kamar_inap WHERE no_rawat='SALAH'"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => HapusAsync(aktif, true));

        await db.ExecAsync("INSERT INTO ranap_gabung(no_rawat,no_rawat2) VALUES ('SALAH','BAYI')");
        var gabung = await Assert.ThrowsAsync<InvalidOperationException>(() => HapusAsync(pindah, true));
        Assert.Contains("rawat gabung", gabung.Message);
        await db.ExecAsync("DELETE FROM ranap_gabung");
        Assert.True(await HapusAsync(pindah, false));
        Assert.Equal("ISI", await db.StrAsync("SELECT status FROM kamar WHERE kd_kamar='KM'"));
        Assert.Equal("Ralan", await db.StrAsync("SELECT status_lanjut FROM reg_periksa WHERE no_rawat='SALAH'"));
        Assert.Equal("Sudah", await db.StrAsync("SELECT stts FROM reg_periksa WHERE no_rawat='SALAH'"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM kamar_inap WHERE no_rawat='LAIN'"));
    }
}
