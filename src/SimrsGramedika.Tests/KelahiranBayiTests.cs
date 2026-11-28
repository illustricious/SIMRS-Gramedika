using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class KelahiranBayiTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_BAYI_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_bayi_fixture.";
        }
    }

    private static readonly DateOnly Hari = new(2026, 11, 28);

    [Fact]
    public void ApgarDijumlahkanHanyaBilaLengkapDanSah()
    {
        Assert.Equal(7, new Apgar(2, 2, 1, 1, 1).Total);
        Assert.Equal(0, new Apgar(0, 0, 0, 0, 0).Total);
        Assert.Null(new Apgar(2, 2, 1, 1).Total);
        Assert.Null(new Apgar(3, 0, 0, 0, 0).Total);
    }

    private static KelahiranBayi Isian() => new()
    {
        Nama = " BY NY SARI ", JenisKelamin = JenisKelamin.Perempuan, TanggalLahir = new(2026, 11, 20), JamLahir = new(14, 35, 5),
        TanggalDaftar = new(2026, 11, 21), NamaIbu = "SARI", UmurIbu = "28 Th", NamaAyah = "BUDI", UmurAyah = "30 Th",
        Alamat = "JL. UJI 1", BeratBadan = "3100", PanjangBadan = "49", LingkarKepala = "34", LingkarDada = "32", LingkarPerut = "30",
        AnakKe = "2", Gravida = "2", Para = "1", Abortus = "0", ProsesLahir = "Spontan", Diagnosa = "Sehat", PenyulitKehamilan = "-",
        Ketuban = "Jernih", Keterangan = "Tidak ada", Resusitasi = "Tidak", ObatDiberikan = "Vitamin K", Miksi = "Sudah",
        Mekonium = "Belum", Penolong = "BD01", Menit1 = new(2, 2, 1, 1, 1), Menit5 = new(2, 2, 2, 1, 2), Menit10 = new(2, 2, 2, 2, 2),
    };

    [FixtureFact]
    public async Task DataKelahiranMendaftarkanBayiDalamSatuTransaksi()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_bayi_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_BAYI_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        await db.ExecAsync("INSERT INTO set_urut_no_rkm_medis VALUES('Straight','No','No','Depan')");
        await db.ExecAsync("INSERT INTO set_no_rkm_medis VALUES('000041')");
        await db.ExecAsync("INSERT INTO pegawai(nik,nama,stts_aktif) VALUES ('BD01','Bidan Uji','AKTIF'),('BD02','Bidan Keluar','KELUAR')");

        var petugas = new AuthenticatedUser { UserId = "uji", Permissions = new HashSet<string> { "kelahiran_bayi" } };
        var layanan = new KelahiranBayiService(settings, petugas);
        Task<long> Jumlah(string tabel) => db.CountAsync($"SELECT COUNT(*) FROM {tabel}");
        Task<string> NomorTerakhir() => db.StrAsync("SELECT no_rkm_medis FROM set_no_rkm_medis");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new KelahiranBayiService(settings,
            new AuthenticatedUser { UserId = "lain" }).SimpanAsync(Isian(), Hari));
        Assert.Equal(("000042", "0001/RM-SKL/11/2026"), await layanan.PerkiraanNomorAsync(new(2026, 11, 20)));

        // Isian yang ditolak tidak meninggalkan apa pun.
        foreach (var salah in new[]
                 {
                     Isian() with { Nama = " " }, Isian() with { Penolong = "" }, Isian() with { Penolong = "TIDAKADA" },
                     Isian() with { Menit5 = new(2, 2, 2, 2) }, Isian() with { Menit10 = new(2, 2, 2, 2, 3) },
                     Isian() with { TanggalLahir = Hari.AddDays(1), TanggalDaftar = Hari.AddDays(1) },
                     Isian() with { TanggalDaftar = new(2026, 11, 19) }, Isian() with { AnakKe = "100" },
                     Isian() with { Nama = new string('a', 41) }, Isian() with { ObatDiberikan = new string('a', 301) },
                 })
            await Assert.ThrowsAsync<ArgumentException>(() => layanan.SimpanAsync(salah, Hari));
        Assert.Equal((0L, 0L, "000041"), (await Jumlah("pasien"), await Jumlah("pasien_bayi"), await NomorTerakhir()));

        // Bayi baru: pasien dan data kelahiran tersimpan bersama.
        var pertama = await layanan.SimpanAsync(Isian(), Hari);
        Assert.Equal(("000042", "0001/RM-SKL/11/2026"), pertama);
        var pasien = (await db.FirstAsync("""
            SELECT p.nm_pasien,p.jk,p.keluarga,p.namakeluarga,p.kd_pj,p.umur,p.tgl_daftar,p.alamatpj,p.stts_nikah,k.nm_kel,s.nama_suku_bangsa
            FROM pasien p JOIN kelurahan k ON k.kd_kel=p.kd_kel JOIN suku_bangsa s ON s.id=p.suku_bangsa
            WHERE p.no_rkm_medis='000042'
            """))!;
        Assert.Equal(("BY NY SARI", "P", "AYAH", "BUDI", "-"),
            (pasien.S("nm_pasien"), pasien.S("jk"), pasien.S("keluarga"), pasien.S("namakeluarga"), pasien.S("kd_pj")));
        Assert.Equal(("0 Th 0 Bl 8 Hr", "2026-11-21", "JL. UJI 1", "BELUM MENIKAH", "-", "-"),
            (pasien.S("umur"), pasien.S("tgl_daftar"), pasien.S("alamatpj"), pasien.S("stts_nikah"), pasien.S("nm_kel"),
                pasien.S("nama_suku_bangsa")));
        Assert.Equal(1, await db.CountAsync("""
            SELECT COUNT(*) FROM pasien_bayi WHERE no_rkm_medis='000042' AND jam_lahir='14:35:05' AND penolong='BD01'
              AND f1='2' AND t1='1' AND n1='7' AND n5='9' AND n10='10' AND berat_badan='3100' AND mikonium='Belum'
            """));
        var tersimpan = await layanan.MuatAsync("000042");
        Assert.Equal(Isian() with { NoRekamMedis = "000042", Nama = "BY NY SARI", NoSkl = pertama.NoSkl, NamaPenolong = "Bidan Uji" },
            tersimpan);

        // Nomor SKL berurutan per bulan lahir.
        Assert.Equal("0002/RM-SKL/11/2026", (await layanan.SimpanAsync(Isian() with { Nama = "BAYI DUA" }, Hari)).NoSkl);
        Assert.Equal("0001/RM-SKL/10/2026",
            (await layanan.SimpanAsync(Isian() with { Nama = "BAYI OKTOBER", TanggalLahir = new(2026, 10, 3) }, Hari)).NoSkl);

        // No. SKL ganda membatalkan pendaftaran pasiennya juga.
        var sebelum = (await Jumlah("pasien"), await Jumlah("pasien_bayi"), await NomorTerakhir());
        var ganda = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layanan.SimpanAsync(Isian() with { Nama = "BAYI GANDA", NoSkl = pertama.NoSkl }, Hari));
        Assert.Contains("sudah dipakai", ganda.Message);
        Assert.Equal(sebelum, (await Jumlah("pasien"), await Jumlah("pasien_bayi"), await NomorTerakhir()));

        // Pasien yang sudah terdaftar: identitasnya tidak diubah, dan hanya sekali.
        await db.ExecAsync("""
            INSERT INTO pasien(no_rkm_medis,nm_pasien,jk,tgl_lahir,tgl_daftar,nm_ibu,alamat,keluarga,namakeluarga,kd_pj)
            VALUES ('LAMA1','BAYI LAMA','L','2026-09-15','2026-09-15','IBU LAMA','ALAMAT LAMA','IBU','IBU LAMA','-'),
                   ('IBU1','IBU PASIEN','P','1998-01-02','2020-01-01','NENEK','ALAMAT IBU','SUAMI','SUAMI UJI','-')
            """);
        Assert.Contains("LAMA1", (await layanan.CariBayiAsync("")).Select(p => p.NoRekamMedis));
        var lama = await layanan.SimpanAsync(Isian() with { NoRekamMedis = "LAMA1", Nama = "NAMA LAIN" }, Hari);
        // Nomor SKL mengikuti bulan lahir yang tersimpan pada pasien, bukan isian.
        Assert.Equal(("LAMA1", "0001/RM-SKL/09/2026"), lama);
        Assert.Equal("BAYI LAMA", await db.StrAsync("SELECT nm_pasien FROM pasien WHERE no_rkm_medis='LAMA1'"));
        Assert.DoesNotContain("LAMA1", (await layanan.CariBayiAsync("")).Select(p => p.NoRekamMedis));
        await Assert.ThrowsAsync<InvalidOperationException>(() => layanan.SimpanAsync(Isian() with { NoRekamMedis = "LAMA1" }, Hari));
        await Assert.ThrowsAsync<InvalidOperationException>(() => layanan.SimpanAsync(Isian() with { NoRekamMedis = "TIDAKADA" }, Hari));
        var ibu = await layanan.CariIbuAsync("IBU PASIEN");
        Assert.Equal(("IBU1", "SUAMI UJI"), (Assert.Single(ibu).NoRekamMedis, ibu[0].NamaKeluarga));
        Assert.Equal("BD01", Assert.Single(await layanan.CariPenolongAsync("Bidan")).Kode);

        // Ubah: data yang sudah usang ditolak; identitas pasien ikut berubah.
        var dilihat = await layanan.MuatAsync("000042");
        await db.ExecAsync("UPDATE pasien_bayi SET berat_badan='3200' WHERE no_rkm_medis='000042'");
        await Assert.ThrowsAsync<InvalidOperationException>(() => layanan.UbahAsync(dilihat, dilihat with { BeratBadan = "9" }, Hari));
        dilihat = await layanan.MuatAsync("000042");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layanan.UbahAsync(dilihat, dilihat with { NoSkl = "0002/RM-SKL/11/2026" }, Hari));
        await layanan.UbahAsync(dilihat,
            dilihat with { Nama = "BAYI SARI", NamaAyah = "AYAH BARU", NoSkl = "", Menit1 = new(0, 0, 0, 0, 0) }, Hari);
        var diubah = await layanan.MuatAsync("000042");
        Assert.Equal(("BAYI SARI", "AYAH BARU", dilihat.NoSkl, 0), (diubah.Nama, diubah.NamaAyah, diubah.NoSkl, diubah.Menit1.Total));
        Assert.Equal("AYAH BARU", await db.StrAsync("SELECT namakeluarga FROM pasien WHERE no_rkm_medis='000042'"));
        var milikLama = await layanan.MuatAsync("LAMA1");
        await layanan.UbahAsync(milikLama, milikLama with { NamaAyah = "AYAH LAMA" }, Hari);
        // Penanggung jawab pasien ini ibunya, jadi nama keluarga tidak diganti.
        Assert.Equal("IBU LAMA", await db.StrAsync("SELECT namakeluarga FROM pasien WHERE no_rkm_medis='LAMA1'"));

        // Data lama dengan APGAR kosong tetap bisa dimuat, tetapi harus dilengkapi saat disimpan.
        await db.ExecAsync("UPDATE pasien_bayi SET w10='',n10='' WHERE no_rkm_medis='LAMA1'");
        milikLama = await layanan.MuatAsync("LAMA1");
        Assert.Null(milikLama.Menit10.Total);
        await Assert.ThrowsAsync<ArgumentException>(() => layanan.UbahAsync(milikLama, milikLama, Hari));

        Assert.Equal(["000042", "000043"], (await layanan.DaftarAsync("", new(2026, 11, 1), Hari))
            .Select(b => b.NoRekamMedis).Order());
        Assert.Equal("000042", Assert.Single(await layanan.DaftarAsync("AYAH BARU", new(2026, 1, 1), Hari)).NoRekamMedis);

        // Hapus: hanya data kelahirannya.
        await db.ExecAsync("INSERT INTO bridging_dukcapil VALUES ('000042','UID')");
        await Assert.ThrowsAsync<InvalidOperationException>(() => layanan.HapusAsync(dilihat));
        await layanan.HapusAsync(diubah);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM pasien_bayi WHERE no_rkm_medis='000042'"));
        Assert.Equal(0, await Jumlah("bridging_dukcapil"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM pasien WHERE no_rkm_medis='000042'"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => layanan.HapusAsync(diubah));
    }
}
