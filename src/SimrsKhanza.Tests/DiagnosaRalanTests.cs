using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Tests;

public class DiagnosaRalanTests
{
    [Fact]
    public async Task TanpaIzinDitolakSebelumKoneksi()
    {
        var service = new DiagnosaRalanService(new DatabaseSettings { Host = "127.0.0.1", Port = 1 }, "UJI", new AuthenticatedUser());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CariAsync(false, ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DaftarAsync(true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TambahAsync(false, "A00", 1, "1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(false, "A00", 1));
    }

    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_DIAGNOSA_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema kosong simrs_diagnosa_fixture pada MariaDB lokal.";
        }
    }

    [FixtureFact]
    public async Task TambahKode_MenjagaPrioritasStatusDanResume()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_DIAGNOSA_FIXTURE_PORT")!),
            Database = "simrs_diagnosa_fixture", User = "root", Password = Environment.GetEnvironmentVariable("SIMRS_DIAGNOSA_FIXTURE_PASSWORD") ?? "" };
        await using var db = await Db.OpenAsync(settings);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM reg_periksa"));
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM-UJI','Pasien Sintetis')");
        await db.ExecAsync("INSERT INTO reg_periksa(no_rawat,no_rkm_medis,status_lanjut,stts,tgl_registrasi,jam_reg) VALUES ('RAWAT-UJI','RM-UJI','Ralan','Belum',CURRENT_DATE,'00:00:00'),('RAWAT-LAMA','RM-UJI','Ralan','Belum',CURRENT_DATE,'00:00:00')");
        await db.ExecAsync("INSERT INTO penyakit(kd_penyakit,nm_penyakit,status,validcode,accpdx,asterisk,im) VALUES ('A00','Uji lama','Tidak Menular','1','Y','0','0'),('B00','Uji baru','Tidak Menular','1','Y','0','0')");
        await db.ExecAsync("INSERT INTO icd9(kode,deskripsi_panjang,validcode,accpdx,im) VALUES ('01','Prosedur Uji','1','Y','0')");
        await db.ExecAsync("INSERT INTO diagnosa_pasien VALUES ('RAWAT-LAMA','A00','Ralan',1,'Baru')");
        await db.ExecAsync("INSERT INTO resume_pasien(no_rawat) VALUES ('RAWAT-UJI')");
        await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=1");
        var user = new AuthenticatedUser { UserId = "UJI", Permissions = new HashSet<string> { "diagnosa_pasien" } };
        var service = new DiagnosaRalanService(settings, "RAWAT-UJI", user);
        Assert.Single(await service.CariAsync(false, "A00"));
        await service.TambahAsync(false, "A00", 1, "");
        await service.TambahAsync(false, "B00", 2, "");
        await service.TambahAsync(true, "01", 1, "2");
        Assert.Equal("Lama", await db.StrAsync("SELECT status_penyakit FROM diagnosa_pasien WHERE no_rawat='RAWAT-UJI' AND kd_penyakit='A00'"));
        Assert.Equal("Baru", await db.StrAsync("SELECT status_penyakit FROM diagnosa_pasien WHERE no_rawat='RAWAT-UJI' AND kd_penyakit='B00'"));
        Assert.Equal("A00", await db.StrAsync("SELECT kd_diagnosa_utama FROM resume_pasien WHERE no_rawat='RAWAT-UJI'"));
        Assert.Equal("B00", await db.StrAsync("SELECT kd_diagnosa_sekunder FROM resume_pasien WHERE no_rawat='RAWAT-UJI'"));
        var staff = new AuthenticatedUser { UserId = "UJI", Permissions = new HashSet<string> { "tindakan_ralan" } };
        foreach (var obstetri in new[] { true, false })
        {
            var exam = new PemeriksaanObgynService(settings, "RAWAT-UJI", staff, obstetri);
            var values = exam.Fields.Select(f => f.Choices?.First() ?? "").ToArray();
            values[0] = "Uji";
            var now = ((DateTime)(await db.ScalarAsync("SELECT NOW()"))!).AddSeconds(-1);
            await exam.SimpanAsync(now, values);
            var original = Assert.Single(await exam.DaftarAsync());
            await Assert.ThrowsAnyAsync<Exception>(() => exam.SimpanAsync(now, values));
            var changed = exam.Fields.Select((f, i) => f.Choices is { } options ? options[^1] : new string((char)('A' + i % 26), f.Max)).ToArray();
            await exam.UbahAsync(original, now, changed);
            var updated = Assert.Single(await exam.DaftarAsync());
            for (var i = 0; i < changed.Length; i++) Assert.Equal(changed[i], updated.S(exam.Fields[i].Name));
            await Assert.ThrowsAsync<InvalidOperationException>(() => exam.HapusAsync(original));
            await Assert.ThrowsAsync<InvalidOperationException>(() => exam.UbahAsync(original, now, values));
            foreach (var (field, index) in exam.Fields.Select((f, i) => (f, i)))
            {
                var invalid = (string[])changed.Clone();
                invalid[index] = field.Choices is null ? new string('X', field.Max + 1) : "tidak valid";
                await Assert.ThrowsAsync<ArgumentException>(() => exam.UbahAsync(updated, now, invalid));
            }
            var denied = new PemeriksaanObgynService(settings, "RAWAT-UJI", new AuthenticatedUser(), obstetri);
            await Assert.ThrowsAsync<InvalidOperationException>(() => denied.DaftarAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => denied.SimpanAsync(now, values));
            await Assert.ThrowsAsync<InvalidOperationException>(() => denied.UbahAsync(updated, now, values));
            await Assert.ThrowsAsync<InvalidOperationException>(() => denied.HapusAsync(updated));
            await exam.HapusAsync(updated);
            Assert.Empty(await exam.DaftarAsync());
        }
        Assert.Equal("01", await db.StrAsync("SELECT kd_prosedur_utama FROM resume_pasien WHERE no_rawat='RAWAT-UJI'"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TambahAsync(false, "B00", 1, ""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.TambahAsync(true, "01", 2, "0"));
        Assert.Equal(2, (await service.DaftarAsync(false)).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(false, "A00", 2));
        await service.HapusAsync(false, "A00", 1);
        await service.HapusAsync(true, "01", 1);
        Assert.Equal("", await db.StrAsync("SELECT kd_diagnosa_utama FROM resume_pasien WHERE no_rawat='RAWAT-UJI'"));
        Assert.Equal("", await db.StrAsync("SELECT kd_prosedur_utama FROM resume_pasien WHERE no_rawat='RAWAT-UJI'"));
        Assert.Equal("B00", await db.StrAsync("SELECT kd_diagnosa_sekunder FROM resume_pasien WHERE no_rawat='RAWAT-UJI'"));
    }
}
