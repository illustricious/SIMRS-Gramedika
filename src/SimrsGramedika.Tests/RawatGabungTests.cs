using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Barcode;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.MenuA;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class RawatGabungTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_GABUNG_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_gabung_fixture.";
        }
    }

    [FixtureFact]
    public async Task BayiMemakaiKamarIbuDanTagihanDigabung()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_gabung_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_GABUNG_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        var kemarin = DateTime.Today.AddDays(-1);
        const string ibu = "IBU/000001";
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien,tgl_lahir) VALUES ('RMI','Ibu Uji','1996-01-01'),('RMB','Bayi Uji',@p0),('RMX','Lain','2000-01-01')", kemarin);
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Penjamin Uji')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('DR','Dokter Uji')");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('BG','Bangsal Uji','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status,trf_kamar) VALUES ('KM','BG','Kelas 1','ISI',100000),('KM2','BG','Kelas 1','KOSONG',100000)");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_reg,no_rawat,tgl_registrasi,jam_reg,kd_dokter,no_rkm_medis,p_jawab,biaya_reg,stts,status_lanjut,kd_pj,status_bayar)
            VALUES ('001',@p0,@p1,'07:00:00','DR','RMI','Suami Uji',25000,'Belum','Ranap','PJ','Belum Bayar')
            """, ibu, kemarin);
        await db.ExecAsync("INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang,trf_kamar) VALUES (@p0,'KM',@p1,'08:00:00','-',100000)", ibu, kemarin);
        await db.ExecAsync("INSERT INTO set_jam_minimal VALUES (1,'No',0,'No',40,'No','No','Yes')");
        await db.ExecAsync("INSERT INTO set_nota (tampilkan_administrasi_di_billingranap) VALUES ('Yes')");
        await db.ExecAsync("INSERT INTO kategori_perawatan(kd_kategori,nm_kategori) VALUES ('K','Kategori Uji')");
        await db.ExecAsync("""
            INSERT INTO jns_perawatan_inap(kd_jenis_prw,nm_perawatan,kd_kategori,kd_pj,kd_bangsal,kelas,status,material,bhp,
                tarif_tindakandr,tarif_tindakanpr,kso,menejemen,total_byrdr,total_byrpr,total_byrdrpr)
            VALUES ('T','Tindakan Uji','K','PJ','BG','Kelas 1','1',10000,0,5000,0,0,0,15000,0,0)
            """);
        await db.ExecAsync("""
            INSERT INTO rekening(kd_rek,nm_rek) VALUES ('KAS','Kas'),('REG','Registrasi'),('KMR','Kamar'),('SUS','Suspen'),('TIN','Tindakan'),
                ('BBD','Beban Dokter'),('UTD','Utang Dokter'),('BBS','Beban Sarana'),('UTS','Utang Sarana')
            """);
        await db.ExecAsync("INSERT INTO akun_bayar VALUES ('Tunai Uji','KAS',0)");
        await db.ExecAsync("""
            INSERT INTO set_akun_ranap(Registrasi_Ranap,Kamar_Inap,Suspen_Piutang_Tindakan_Ranap,Tindakan_Ranap,
                Beban_Jasa_Medik_Dokter_Tindakan_Ranap,Utang_Jasa_Medik_Dokter_Tindakan_Ranap,
                Beban_Jasa_Sarana_Tindakan_Ranap,Utang_Jasa_Sarana_Tindakan_Ranap)
            VALUES ('REG','KMR','SUS','TIN','BBD','UTD','BBS','UTS')
            """);

        var petugas = new AuthenticatedUser { UserId = "uji",
            Permissions = new HashSet<string> { "kamar_inap", "tindakan_ranap", "billing_ranap" } };
        var service = new RawatGabungService(settings, petugas);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RawatGabungService(settings, new()).GabungAsync(ibu, "RMB"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GabungAsync(ibu, "RMI"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GabungAsync(ibu, "TIDAK-ADA"));
        Assert.Equal("RMB", (await service.CariPasienAsync("Bayi"))[0].NoRekamMedis);
        var anak = await service.GabungAsync(ibu, "RMB");
        Assert.Equal($"{kemarin:yyyy/MM/dd}/000002", anak);
        var reg = (await db.FirstAsync("SELECT * FROM reg_periksa WHERE no_rawat=@p0", anak))!;
        Assert.Equal(("RMB", "Ranap", "Sudah Bayar", "PJ", "DR", "Suami Uji", "Hr"),
            (reg.S("no_rkm_medis"), reg.S("status_lanjut"), reg.S("status_bayar"), reg.S("kd_pj"), reg.S("kd_dokter"), reg.S("p_jawab"), reg.S("sttsumur")));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM ranap_gabung WHERE no_rawat=@p0 AND no_rawat2=@p1", ibu, anak));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GabungAsync(ibu, "RMX"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GabungAsync(anak, "RMX"));
        var daftarBayi = Assert.Single(await service.DaftarAsync(ibu));
        Assert.Equal((anak, "Bayi Uji", 0), (daftarBayi.NoRawat, daftarBayi.Nama, daftarBayi.JumlahTransaksi));

        // Daftar kamar menampilkan bayi di bawah ibu dengan segmen kamar yang sama.
        RawatInapListItem barisBayi;
        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var repo = new RawatInapRepository(ctx);
            var rows = await repo.GetDaftarAsync(new(RawatInapMode.BelumPulang, default, default, null, null, null, null, null, null));
            Assert.Equal([ibu, anak], rows.Select(r => r.NoRawat));
            barisBayi = rows[1];
            Assert.True(barisBayi.BayiGabung);
            Assert.Equal((ibu, "RMB", "KM"), (barisBayi.NoRawatIbu, barisBayi.NoRekamMedis, barisBayi.KodeKamar));
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AdmitAsync(anak, "KM2", null));
        }

        // Layanan bayi memakai kamar ibu.
        var tindakan = new TindakanRanapInputService(settings, barisBayi.Key, barisBayi.NoRekamMedis, petugas);
        var tarif = Assert.Single(await tindakan.CariAsync(TindakanRalanJenis.Dokter, ""));
        await tindakan.SimpanAsync(TindakanRalanJenis.Dokter, kemarin.AddHours(10), "DR", "", [tarif]);
        Assert.Equal(1, (await service.DaftarAsync(ibu))[0].JumlahTransaksi);
        await SmokeQueryKamarAsync(db, settings, anak);

        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var kasir = new KasirRepository(ctx);
            var tagihanBayi = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(anak));
            Assert.Equal((ibu, 0m, null), (tagihanBayi.NoRawatIbu, tagihanBayi.Total, tagihanBayi.StatusBayar));
            Assert.Empty(tagihanBayi.Rincian);
            Assert.Contains("digabung", tagihanBayi.AlasanBelumBisaBayar);
            await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRanapAsync(tagihanBayi, "Tunai Uji", petugas));
        }

        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var kamarIbu = new RawatInapKey(ibu, "KM", DateOnly.FromDateTime(kemarin), new TimeOnly(8, 0));
            await new RawatInapRepository(ctx).DischargeAsync(kamarIbu, StatusPulang.Sehat, "Uji", waktuKeluar: kemarin.AddHours(20));
        }

        // Permintaan bayi yang belum dikerjakan menahan pelunasan ibu.
        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
            Assert.Contains("menunggu", (await new KasirRepository(ctx).GetBillAsync(ibu))!.AlasanBelumBisaBayar);
        await db.ExecAsync("DELETE FROM permintaan_lab");
        await db.ExecAsync("DELETE FROM permintaan_radiologi");

        string nota;
        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var kasir = new KasirRepository(ctx);
            var tagihan = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(ibu));
            Assert.Null(tagihan.AlasanBelumBisaBayar);
            // Registrasi 25.000 + kamar ibu 100.000 + bayi 40% + tindakan bayi 15.000.
            Assert.Equal(180_000m, tagihan.Total);
            Assert.Equal(140_000m, tagihan.BiayaKamar);
            Assert.Equal(40_000m, tagihan.Rincian.Single(r => r.Jenis == "Kamar" && r.Nama.EndsWith("(Bayi)")).Total);
            Assert.Equal("Bayi · Tindakan Uji", tagihan.Rincian.Single(r => r.Jenis == "Ranap Dokter").Nama);

            await Assert.ThrowsAsync<InvalidOperationException>(() => // segmen kamar ibu sudah ditutup
                tindakan.SimpanAsync(TindakanRalanJenis.Dokter, kemarin.AddHours(11), "DR", "", [tarif]));
            nota = await kasir.BayarRanapAsync(tagihan, "Tunai Uji", petugas);
        }
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM reg_periksa WHERE status_bayar='Sudah Bayar'"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM nota_inap WHERE no_nota=@p0", nota));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM billing WHERE no_rawat=@p0", anak));
        Assert.Equal(180_000, await db.CountAsync("SELECT SUM(debet) FROM detailjurnal WHERE kd_rek='KAS'"));
        Assert.Equal(140_000, await db.CountAsync("SELECT SUM(kredit) FROM detailjurnal WHERE kd_rek='KMR'"));
        // Suspen tindakan yang dijurnal atas No. Rawat bayi ikut dilunasi.
        Assert.Equal(0, await db.CountAsync("SELECT SUM(debet-kredit) FROM detailjurnal WHERE kd_rek='SUS'"));
        Assert.Equal(15_000, await db.CountAsync("SELECT SUM(kredit-debet) FROM detailjurnal WHERE kd_rek='TIN'"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM (SELECT no_jurnal FROM detailjurnal GROUP BY no_jurnal HAVING SUM(debet)<>SUM(kredit)) x"));

        // Sesudah tagihan ibu tersimpan, data bayi ikut terkunci dan gabungan tidak bisa dilepas.
        var baris = Assert.Single(await tindakan.DaftarAsync()).Isi;
        var terkunci = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TindakanRanapInputService(settings, barisBayi.Key, "RMB", new AuthenticatedUser { IsAdminUtama = true }).HapusAsync([baris]));
        Assert.Contains("tagihan", terkunci.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LepasAsync(ibu, anak));
        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var lunas = Assert.IsType<KasirBillItem>(await new KasirRepository(ctx).GetBillAsync(ibu));
            Assert.Equal((180_000m, "Sudah Bayar"), (lunas.Total, lunas.StatusBayar));
        }

        await db.ExecAsync("DELETE FROM billing");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LepasAsync(ibu, "LAIN"));
        await service.LepasAsync(ibu, anak);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM ranap_gabung"));
        Assert.Equal("Ralan", await db.StrAsync("SELECT status_lanjut FROM reg_periksa WHERE no_rawat=@p0", anak));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM rawat_inap_dr WHERE no_rawat=@p0", anak));
    }

    /// <summary>Query yang mencari kamar lewat ibu harus tetap sah di MariaDB dan menemukan kamar bayi.</summary>
    private static async Task SmokeQueryKamarAsync(Db db, DatabaseSettings settings, string anak)
    {
        var hari = DateOnly.FromDateTime(DateTime.Today);
        await db.ExecAsync("INSERT INTO permintaan_lab(noorder,no_rawat,tgl_permintaan,dokter_perujuk,status) VALUES ('PK-UJI',@p0,@p1,'DR','ranap')", anak, DateTime.Today);
        await db.ExecAsync("INSERT INTO permintaan_radiologi(noorder,no_rawat,tgl_permintaan,dokter_perujuk,status) VALUES ('PR-UJI',@p0,@p1,'DR','ranap')", anak, DateTime.Today);
        Assert.True((await new BarcodeRepository(settings).LoadAsync(anak, ranap: true)).Ranap);
        await using var ctx = SimrsGramedikaDbContextFactory.Create(settings);
        var lab = new LabPeriksaRepository(ctx);
        Assert.Contains("KM", (await lab.GetRegInfoAsync(anak))!.KamarPoli);
        await lab.GetPeriksaGroupsAsync(LabCategory.PK, new(hari, hari));
        await lab.GetPeriksaPaAsync(new(hari, hari));
        var order = Assert.Single(await new LabPermintaanRepository(ctx).GetListAsync(LabCategory.PK,
            new(LabLokasi.RawatInap, hari, hari, null, "Bangsal Uji", null, true)));
        Assert.Equal("PK-UJI", order.NoOrder);
        var radiologi = new RadiologiPermintaanRepository(ctx);
        Assert.Single(await radiologi.GetListAsync(new(RadiologiLokasi.RawatInap, hari, hari, null, "Bangsal Uji", null, true)));
        await radiologi.GetItemRowsAsync(new(RadiologiLokasi.RawatInap, hari, hari, null, null, null, false));
        await new RadiologiPeriksaRepository(ctx).GetPeriksaGroupsAsync(new(hari, hari));
        await new FarmasiStokRepository(settings).PermintaanStokPasienAsync(DateTime.Today, DateTime.Today, null);
        await new ResepPulangRepository(settings).CariStokAsync(anak, "BG", "");
    }
}
