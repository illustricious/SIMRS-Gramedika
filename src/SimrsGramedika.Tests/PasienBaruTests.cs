using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

/// <summary>Pendaftaran pasien baru dari Registrasi.</summary>
public class PasienBaruTests
{
    private static readonly DateOnly Hari = new(2026, 11, 26);
    private static readonly AuthenticatedUser Petugas = new() { Permissions = new HashSet<string> { PasienBaruService.Izin } };

    private static PasienBaruData Isian() => new()
    {
        Nama = "PASIEN UJI", TanggalLahir = new(1990, 2, 3), KodePenjab = "UMU", Alamat = "JALAN UJI",
        SukuBangsa = "1", Bahasa = "1", CacatFisik = "1", Perusahaan = "-",
    };

    [Theory]
    [InlineData("1990-02-03", 36, 9, 23)]
    [InlineData("2026-11-26", 0, 0, 0)]
    [InlineData("2026-01-31", 0, 9, 26)]
    [InlineData("2024-02-29", 2, 8, 28)]
    public void UmurSaatDaftar(string lahir, int tahun, int bulan, int hari) =>
        Assert.Equal((tahun, bulan, hari), PasienBaruService.HitungUmurLengkap(DateOnly.Parse(lahir), Hari));

    [Fact]
    public void PjDiriSendiriMengikutiPasienTanpaMenimpaIsian()
    {
        var data = (Isian() with { Alamat = new string('A', 150), PekerjaanPj = "GURU", Wilayah = new("KEL") }).LengkapiPj();
        Assert.Equal("PASIEN UJI", data.NamaKeluarga);
        Assert.Equal("GURU", data.PekerjaanPj);
        Assert.Equal(100, data.AlamatPj.Length);
        Assert.Equal("KEL", data.WilayahPj.Kelurahan);
        Assert.Equal("", (Isian() with { Keluarga = HubunganPenanggungJawab.Ayah }).LengkapiPj().NamaKeluarga);
    }

    [Fact]
    public void ValidasiMenolakIsianKurang()
    {
        PasienBaruService.Validasi(Isian(), [], Hari);
        foreach (var salah in new[]
                 {
                     Isian() with { Nama = " " }, Isian() with { TanggalLahir = null }, Isian() with { TanggalLahir = Hari.AddDays(1) },
                     Isian() with { KodePenjab = "" }, Isian() with { SukuBangsa = "" }, Isian() with { Nama = new string('A', 41) },
                 })
            Assert.Throws<ArgumentException>(() => PasienBaruService.Validasi(salah, [], Hari));
        var galat = Assert.Throws<ArgumentException>(() => PasienBaruService.Validasi(Isian(), [new("no_ktp", 16)], Hari));
        Assert.Contains("No. KTP", galat.Message);
        PasienBaruService.Validasi(Isian() with { NoKtp = new string('1', 16) }, [new("no_ktp", 16)], Hari);
    }

    [Fact]
    public async Task TanpaHakDitolakSebelumKoneksi()
    {
        var service = new PasienBaruService(new DatabaseSettings { Host = "127.0.0.1", Port = 1 }, new AuthenticatedUser());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReferensiAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(Isian()));
    }

    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_PASIEN_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema kosong simrs_pasien_fixture (pasien, tabel referensinya, dan pengaturan nomor RM) pada MariaDB lokal.";
        }
    }

    [FixtureFact]
    public async Task SimpanMenomoriDanMemenuhiForeignKey()
    {
        var settings = new DatabaseSettings
        {
            Host = "127.0.0.1", Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_PASIEN_FIXTURE_PORT")!),
            Database = "simrs_pasien_fixture", User = "root",
            Password = Environment.GetEnvironmentVariable("SIMRS_PASIEN_FIXTURE_PASSWORD") ?? "",
        };
        await using var db = await Db.OpenAsync(settings);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM pasien"));
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab,nama_perusahaan,alamat_asuransi,no_telp,attn,status) VALUES('UMU','UMUM','-','-','-','-','1')");
        await db.ExecAsync("INSERT INTO suku_bangsa(id,nama_suku_bangsa) VALUES(1,'-')");
        await db.ExecAsync("INSERT INTO bahasa_pasien(id,nama_bahasa) VALUES(1,'-')");
        await db.ExecAsync("INSERT INTO cacat_fisik(id,nama_cacat) VALUES(1,'-')");
        await db.ExecAsync("INSERT INTO perusahaan_pasien(kode_perusahaan,nama_perusahaan) VALUES('-','-')");
        await db.ExecAsync("INSERT INTO set_urut_no_rkm_medis VALUES('Straight','No','No','Depan')");
        await db.ExecAsync("INSERT INTO set_no_rkm_medis VALUES('000041')");
        await db.ExecAsync("INSERT INTO set_kelengkapan_data_pasien(no_ktp,p_no_ktp,propinsi,p_propinsi,propinsipj,p_propinsipj) VALUES('No',0,'No',0,'No',0)");
        var service = new PasienBaruService(settings, Petugas);

        var referensi = await service.ReferensiAsync();
        Assert.Equal("000042", referensi.PerkiraanNoRm);
        Assert.Equal("UMU", Assert.Single(referensi.Penjab).Kode);

        var data = Isian() with { Wilayah = new("MEKAR", "SUKA", "KOTA UJI", "PROV UJI") };
        var pertama = await service.SimpanAsync(data, Hari);
        var kedua = await service.SimpanAsync(data with { Nama = "PASIEN DUA", Wilayah = new("MEKAR") }, Hari);
        Assert.Equal(("000042", "000043"), (pertama, kedua));
        Assert.Equal("000043", await db.StrAsync("SELECT no_rkm_medis FROM set_no_rkm_medis"));
        // Nama wilayah yang sama memakai satu kode; isian kosong menjadi "-".
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM kelurahan WHERE nm_kel='MEKAR'"));
        var row = (await db.FirstAsync("""
            SELECT p.umur,p.tgl_daftar,p.namakeluarga,p.gol_darah,k.nm_kel,c.nm_kec FROM pasien p
            JOIN kelurahan k ON k.kd_kel=p.kd_kel JOIN kecamatan c ON c.kd_kec=p.kd_kec WHERE p.no_rkm_medis=@p0
            """, kedua))!;
        Assert.Equal("36 Th 9 Bl 23 Hr", row.S("umur"));
        Assert.Equal("2026-11-26", row.S("tgl_daftar"));
        Assert.Equal("PASIEN DUA", row.S("namakeluarga"));
        Assert.Equal("-", row.S("gol_darah"));
        Assert.Equal(("MEKAR", "-"), (row.S("nm_kel"), row.S("nm_kec")));

        // Gagal di tengah transaksi (suku tidak ada): tidak ada pasien, wilayah, atau nomor yang berubah.
        await Assert.ThrowsAnyAsync<Exception>(() => service.SimpanAsync(data with { SukuBangsa = "999", Wilayah = new("BATAL") }, Hari));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM pasien"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM kelurahan WHERE nm_kel='BATAL'"));
        Assert.Equal("000043", await db.StrAsync("SELECT no_rkm_medis FROM set_no_rkm_medis"));

        // Layar Pasien: isian dimuat utuh, dan perubahan memakai aturan serta kode wilayah yang sama.
        var termuat = await service.MuatAsync(pertama);
        Assert.Equal(data.Wilayah, termuat.Wilayah);
        Assert.Equal(("1", "1", "1", "-"), (termuat.SukuBangsa, termuat.Bahasa, termuat.CacatFisik, termuat.Perusahaan));
        await db.ExecAsync("UPDATE pasien SET nip='123' WHERE no_rkm_medis=@p0", pertama);
        await service.UbahAsync(pertama, termuat with { Nama = "PASIEN UBAH", Wilayah = termuat.Wilayah with { Kelurahan = "BARU" } },
            Hari.AddYears(1));
        var ubah = await service.MuatAsync(pertama);
        Assert.Equal(("PASIEN UBAH", "BARU", "SUKA"), (ubah.Nama, ubah.Wilayah.Kelurahan, ubah.Wilayah.Kecamatan));
        var tetap = (await db.FirstAsync("SELECT tgl_daftar,nip,umur FROM pasien WHERE no_rkm_medis=@p0", pertama))!;
        Assert.Equal(("2026-11-26", "123", "37 Th 9 Bl 23 Hr"), (tetap.S("tgl_daftar"), tetap.S("nip"), tetap.S("umur")));
        Assert.Equal("000043", await db.StrAsync("SELECT no_rkm_medis FROM set_no_rkm_medis"));
        await Assert.ThrowsAnyAsync<Exception>(() => service.UbahAsync(pertama, ubah with { Bahasa = "999", Wilayah = new("GAGAL") }, Hari));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM kelurahan WHERE nm_kel='GAGAL'"));
        Assert.Equal("BARU", (await service.MuatAsync(pertama)).Wilayah.Kelurahan);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync("TIDAKADA", ubah, Hari));

        // Aturan kelengkapan dari pengaturan Khanza berlaku.
        await db.ExecAsync("UPDATE set_kelengkapan_data_pasien SET no_ktp='Yes',p_no_ktp=16");
        await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(data, Hari));
        await db.ExecAsync("UPDATE set_kelengkapan_data_pasien SET no_ktp='No'");

        // Urutan Terminal dengan tahun-bulan di belakang.
        await db.ExecAsync("DELETE FROM set_urut_no_rkm_medis");
        await db.ExecAsync("INSERT INTO set_urut_no_rkm_medis VALUES('Terminal','Yes','Yes','Belakang')");
        await db.ExecAsync("UPDATE set_no_rkm_medis SET no_rkm_medis='430000'");
        Assert.Equal($"440000-{DateTime.Today:MMyy}", await service.SimpanAsync(data with { Nama = "PASIEN TIGA" }, Hari));
    }
}
