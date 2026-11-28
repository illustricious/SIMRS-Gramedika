using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class OrderRanapTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_ORDER_RANAP_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_order_ranap_fixture.";
        }
    }

    [FixtureFact]
    public async Task ResepLabDanRadiologiRawatInapMengikutiKamarYangDitempati()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_order_ranap_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_ORDER_RANAP_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        const string ibu = "RI/000001", bayi = "RI/000002", pulang = "RI/000003", jalan = "RJ/000001";
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM1','Ibu Uji'),('RM2','Bayi Uji'),('RM3','Pulang Uji')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('DR','Dokter Registrasi'),('DPJP','Dokter Penanggung'),('AUTH','Dokter Penulis')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Umum'),('-','-')");
        await db.ExecAsync("INSERT INTO poliklinik(kd_poli,nm_poli) VALUES ('POL','Poli Uji')");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('BG','Bangsal Uji','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status) VALUES ('K1','BG','Kelas 1','KOSONG'),('K2','BG','Kelas 2','ISI')");
        foreach (var (rawat, rm, lanjut) in new[] { (ibu, "RM1", "Ranap"), (bayi, "RM2", "Ranap"), (pulang, "RM3", "Ranap"), (jalan, "RM3", "Ralan") })
            await db.ExecAsync("""
                INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_dokter,kd_poli,kd_pj,status_lanjut,stts,status_bayar,tgl_registrasi)
                VALUES (@p0,@p1,'DR','POL','PJ',@p2,'Belum',@p3,CURRENT_DATE)
                """, rawat, rm, lanjut, rawat == bayi ? "Sudah Bayar" : "Belum Bayar");
        // Ibu pindah dari Kelas 1 ke Kelas 2; bayi menumpang kamar ibu; satu pasien lain sudah pulang.
        await db.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,tgl_keluar,jam_keluar,stts_pulang)
            VALUES (@p0,'K1',CURRENT_DATE - INTERVAL 2 DAY,'08:00:00',CURRENT_DATE - INTERVAL 1 DAY,'08:00:00','Pindah Kamar'),
                (@p0,'K2',CURRENT_DATE - INTERVAL 1 DAY,'08:00:00','0000-00-00','00:00:00','-'),
                (@p1,'K1',CURRENT_DATE - INTERVAL 2 DAY,'08:00:00',CURRENT_DATE,'08:00:00','Sehat')
            """, ibu, pulang);
        await db.ExecAsync("INSERT INTO ranap_gabung(no_rawat,no_rawat2) VALUES (@p0,@p1)", ibu, bayi);
        await db.ExecAsync("INSERT INTO dpjp_ranap(no_rawat,kd_dokter) VALUES (@p0,'DPJP')", ibu);
        await db.ExecAsync("INSERT INTO databarang(kode_brng,nama_brng,status) VALUES ('OB','Obat Uji','1')");
        await db.ExecAsync("""
            INSERT INTO jns_perawatan_lab(kd_jenis_prw,nm_perawatan,status,kategori,kelas,kd_pj)
            VALUES ('K1','Lab Kelas 1','1','PK','Kelas 1','-'),('K2','Lab Kelas 2','1','PK','Kelas 2','-'),
                ('RJ','Lab Rawat Jalan','1','PK','Rawat Jalan','-'),('UM','Lab Umum','1','PK','-','-')
            """);
        await db.ExecAsync("""
            INSERT INTO jns_perawatan_radiologi(kd_jenis_prw,nm_perawatan,status,kelas,kd_pj)
            VALUES ('RAD1','Rontgen Kelas 1','1','Kelas 1','-'),('RAD2','Rontgen Kelas 2','1','Kelas 2','-')
            """);

        var dokter = new AuthenticatedUser { UserId = "AUTH",
            Permissions = new HashSet<string> { "resep_dokter", "permintaan_lab", "permintaan_radiologi" } };
        OrderRalanService Ranap(string rawat, string rm, JenisOrderRalan jenis, AuthenticatedUser? akun = null) =>
            new(settings, rawat, rm, akun ?? dokter, jenis, ranap: true);

        // Tarif mengikuti kelas kamar yang sedang ditempati, juga untuk bayi rawat gabung.
        foreach (var (rawat, rm) in new[] { (ibu, "RM1"), (bayi, "RM2") })
        {
            Assert.Equal(["K2", "UM"], (await Ranap(rawat, rm, JenisOrderRalan.LabPk).CariAsync("")).Select(b => b.Kode).Order());
            Assert.Equal("RAD2", Assert.Single(await Ranap(rawat, rm, JenisOrderRalan.Radiologi).CariAsync("")).Kode);
        }

        var resep = Ranap(ibu, "RM1", JenisOrderRalan.Resep);
        var obat = new BarisOrderRalan("OB", "Obat Uji", 2, "3x1");
        // Layanan rawat jalan menolak kunjungan inap, dan layanan inap menolak rawat jalan atau pasien yang sudah pulang.
        var salah = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new OrderRalanService(settings, ibu, "RM1", dokter, JenisOrderRalan.Resep).SimpanAsync([obat], "", ""));
        Assert.Contains("rawat jalan", salah.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ranap(jalan, "RM3", JenisOrderRalan.Resep).SimpanAsync([obat], "", ""));
        var sudahPulang = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Ranap(pulang, "RM3", JenisOrderRalan.Resep).SimpanAsync([obat], "", ""));
        Assert.Contains("tidak sedang menempati kamar", sudahPulang.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Ranap(ibu, "RM1", JenisOrderRalan.Resep, new AuthenticatedUser { UserId = "AUTH" }).SimpanAsync([obat], "", ""));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM resep_obat"));

        var noResep = await resep.SimpanAsync([obat], "", "");
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM resep_obat WHERE no_resep=@p0 AND status='ranap' AND kd_dokter='AUTH'", noResep));
        Assert.Equal("Menunggu farmasi", Assert.Single(await resep.RiwayatAsync()).S("status"));
        var noBayi = await Ranap(bayi, "RM2", JenisOrderRalan.Resep).SimpanAsync([obat], "", "");
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM resep_obat WHERE no_resep=@p0 AND no_rawat=@p1", noBayi, bayi));

        // Admin Utama mengirim atas nama DPJP; tanpa DPJP atas nama dokter registrasi.
        var admin = new AuthenticatedUser { IsAdminUtama = true };
        var noAdmin = await Ranap(ibu, "RM1", JenisOrderRalan.Resep, admin).SimpanAsync([obat], "", "");
        Assert.Equal("DPJP", await db.StrAsync("SELECT kd_dokter FROM resep_obat WHERE no_resep=@p0", noAdmin));
        var noAdminBayi = await Ranap(bayi, "RM2", JenisOrderRalan.Resep, admin).SimpanAsync([obat], "", "");
        Assert.Equal("DR", await db.StrAsync("SELECT kd_dokter FROM resep_obat WHERE no_resep=@p0", noAdminBayi));

        var lab = Ranap(ibu, "RM1", JenisOrderRalan.LabPk);
        // Tarif kelas lain tidak bisa disimpan walau kodenya dikirim.
        await Assert.ThrowsAsync<InvalidOperationException>(() => lab.SimpanAsync([new("K1", "")], "Diagnosis uji", ""));
        var noLab = await lab.SimpanAsync([new("K2", "")], "Diagnosis uji", "");
        var noRad = await Ranap(ibu, "RM1", JenisOrderRalan.Radiologi).SimpanAsync([new("RAD2", "")], "Diagnosis uji", "");
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM permintaan_lab WHERE noorder=@p0 AND status='ranap'", noLab));
        Assert.Single(await lab.RiwayatAsync());

        // Order tampil pada daftar rawat inap unit tujuan, dengan kamar yang sedang ditempati.
        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        var today = DateOnly.FromDateTime((await db.FirstAsync("SELECT CURRENT_DATE waktu"))!.Dt("waktu")!.Value);
        var diLab = Assert.Single(await new LabPermintaanRepository(context)
            .GetListAsync(LabCategory.PK, new(LabLokasi.RawatInap, today, today, null, null, ibu, true)));
        Assert.Equal((noLab, "K2 Bangsal Uji"), (diLab.NoOrder, diLab.Lokasi));
        var diRad = Assert.Single(await new RadiologiPermintaanRepository(context)
            .GetListAsync(new(RadiologiLokasi.RawatInap, today, today, null, null, ibu, true)));
        Assert.Equal(noRad, diRad.NoOrder);

        // Ubah dan batalkan hanya menyentuh order rawat inap milik dokter pengirim.
        var draf = await resep.MuatOrderAsync(noResep);
        await resep.UbahAsync(draf, [obat with { Jumlah = 4 }], "", "");
        Assert.Equal(4, await db.CountAsync("SELECT jml FROM resep_dokter WHERE no_resep=@p0", noResep));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resep.MuatOrderAsync(noAdmin));
        await lab.BatalkanAsync(noLab);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM permintaan_lab"));

        // Sesudah billing tersimpan pada ibu, order ibu maupun bayi ditutup.
        await db.ExecAsync("""
            INSERT INTO billing(noindex,no_rawat,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
            VALUES (1,@p0,'1','Billing sintetis',':',1000,1,0,1000,'Kamar')
            """, ibu);
        foreach (var (rawat, rm) in new[] { (ibu, "RM1"), (bayi, "RM2") })
            await Assert.ThrowsAsync<InvalidOperationException>(() => Ranap(rawat, rm, JenisOrderRalan.Resep).SimpanAsync([obat], "", ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resep.BatalkanAsync(noResep));
    }
}
