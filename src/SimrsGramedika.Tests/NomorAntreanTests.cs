using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

/// <summary>Nomor antrean (URUTNOREG, BASENOREG) dan kuota jadwal pada registrasi.</summary>
public class NomorAntreanTests
{
    [Fact]
    public void BawaanSamaDenganPerilakuSebelumnyaDanTersalin()
    {
        var bawaan = new DatabaseSettings();
        Assert.Equal(("dokter", "registrasi", false), (bawaan.UrutNoReg, bawaan.BaseNoReg, bawaan.JadwalDokterDiRegistrasi));
        var diubah = new DatabaseSettings { UrutNoReg = "dokter + poli", BaseNoReg = "booking", JadwalDokterDiRegistrasi = true }
            .With(name: "Salinan");
        Assert.Equal(("dokter + poli", "booking", true), (diubah.UrutNoReg, diubah.BaseNoReg, diubah.JadwalDokterDiRegistrasi));
    }

    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_ANTREAN_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema kosong simrs_antrean_fixture pada MariaDB lokal.";
        }
    }

    [FixtureFact]
    public async Task NomorMengikutiPengaturan()
    {
        DatabaseSettings Atur(string urut = "dokter", string dasar = "registrasi", bool kuota = false) => new()
        {
            Host = "127.0.0.1", Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_ANTREAN_FIXTURE_PORT")!),
            Database = "simrs_antrean_fixture", User = "root",
            Password = Environment.GetEnvironmentVariable("SIMRS_ANTREAN_FIXTURE_PASSWORD") ?? "",
            UrutNoReg = urut, BaseNoReg = dasar, JadwalDokterDiRegistrasi = kuota,
        };

        await using var db = await Db.OpenAsync(Atur());
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM reg_periksa"));
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien,tgl_lahir,tgl_daftar) VALUES('000001','PASIEN UJI','1990-01-01','2020-01-01')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES('D1','SATU'),('D2','DUA')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES('UMU','UMUM')");
        await db.ExecAsync("INSERT INTO poliklinik(kd_poli,nm_poli,registrasi,registrasilama) VALUES('P1','Poli Satu',10,5),('P2','Poli Dua',10,5),('IGDK','IGD',20,15)");

        var waktu = new DateTime(2026, 11, 26, 9, 0, 0);
        var akses = new RegistrasiAkses(false, false);
        async Task<(string NoReg, string NoRawat)> Daftar(DatabaseSettings s, string dokter, string poli)
        {
            var noRawat = await new RegistrasiService(s).DaftarAsync(
                new("000001", dokter, poli, "UMU", "PJ", "ALAMAT", "DIRI SENDIRI", waktu), akses);
            return (await db.StrAsync("SELECT no_reg FROM reg_periksa WHERE no_rawat=@p0", noRawat), noRawat);
        }

        // Per dokter (bawaan): poli tidak memengaruhi urutan.
        Assert.Equal("001", (await Daftar(Atur(), "D1", "P1")).NoReg);
        var kedua = await Daftar(Atur(), "D1", "P2");
        Assert.Equal("002", kedua.NoReg);
        // Per poli dan per dokter+poli.
        Assert.Equal("002", (await Daftar(Atur("poli"), "D2", "P1")).NoReg);
        Assert.Equal("001", (await Daftar(Atur("dokter + poli"), "D2", "P2")).NoReg);

        // Setelah ada yang dihapus, nomor rawat tetap dari yang terbesar sehingga tidak bentrok.
        await db.ExecAsync("DELETE FROM reg_periksa WHERE no_rawat=@p0", kedua.NoRawat);
        var igd = await Daftar(Atur("poli"), "D1", "IGDK");
        Assert.Equal(("001", "2026/11/26/000005"), igd);
        Assert.Equal(("Lama", "Baru"), (
            await db.StrAsync("SELECT stts_daftar FROM reg_periksa WHERE no_rawat=@p0", igd.NoRawat),
            await db.StrAsync("SELECT status_poli FROM reg_periksa WHERE no_rawat=@p0", igd.NoRawat)));

        // Dasar booking: nomor yang sudah dipesan untuk tanggal itu dilewati; dasar registrasi mengabaikannya.
        await db.ExecAsync("INSERT INTO booking_registrasi(no_rkm_medis,tanggal_periksa,kd_dokter,kd_poli,no_reg) VALUES('000001','2026-11-26','D2','P1','010')");
        Assert.Equal("011", (await Daftar(Atur(dasar: "booking"), "D2", "P1")).NoReg);
        await db.ExecAsync("UPDATE booking_registrasi SET no_reg='050'");
        Assert.Equal("012", (await Daftar(Atur(), "D2", "P1")).NoReg);

        // Kuota jadwal (26-11-2026 hari Kamis): berlaku bila diaktifkan, tidak untuk Admin Utama maupun IGD.
        await db.ExecAsync("INSERT INTO jadwal(kd_dokter,hari_kerja,jam_mulai,jam_selesai,kd_poli,kuota) VALUES('D1','KAMIS','08:00','12:00','P1',1),('D1','KAMIS','13:00','14:00','IGDK',1)");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Daftar(Atur(kuota: true), "D1", "P1"));
        await Daftar(Atur(), "D1", "P1");
        await Daftar(Atur(kuota: true), "D1", "IGDK");
        await new RegistrasiService(Atur(kuota: true)).DaftarAsync(
            new("000001", "D1", "P1", "UMU", "PJ", "ALAMAT", "DIRI SENDIRI", waktu), new(true, true));
    }
}
