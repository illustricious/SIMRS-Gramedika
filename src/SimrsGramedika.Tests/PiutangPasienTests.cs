using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class PiutangPasienTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_PIUTANG_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_piutang_fixture.";
        }
    }

    [FixtureFact]
    public async Task TagihanMenjadiPiutangLaluDicicilDanDibatalkan()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_piutang_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_PIUTANG_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        var kemarin = DateTime.Today.AddDays(-1);
        const string jalan = "RJ/000001", tanpaMuka = "RJ/000002", inap = "RI/000001";
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien,tgl_lahir) VALUES ('RM1','Pasien Satu','1990-01-01'),('RM2','Pasien Dua','1990-01-01')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Penjamin Uji')");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('BG','Bangsal Uji','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status,trf_kamar) VALUES ('KM','BG','Kelas 1','KOSONG',100000)");
        foreach (var (rawat, rm, lanjut) in new[] { (jalan, "RM1", "Ralan"), (tanpaMuka, "RM2", "Ralan"), (inap, "RM2", "Ranap") })
            await db.ExecAsync("""
                INSERT INTO reg_periksa(no_reg,no_rawat,tgl_registrasi,jam_reg,kd_dokter,no_rkm_medis,biaya_reg,stts,status_lanjut,kd_pj,status_bayar)
                VALUES ('001',@p0,@p1,'07:00:00','DR',@p2,100000,'Sudah',@p3,'PJ','Belum Bayar')
                """, rawat, kemarin, rm, lanjut);
        await db.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,tgl_keluar,jam_keluar,lama,ttl_biaya,stts_pulang,trf_kamar)
            VALUES (@p0,'KM',@p1,'08:00:00',@p1,'20:00:00',1,100000,'Sehat',100000)
            """, inap, kemarin);
        await db.ExecAsync("INSERT INTO set_jam_minimal VALUES (1,'No',0,'No',0,'No','No','Yes')");
        await db.ExecAsync("INSERT INTO set_nota (tampilkan_administrasi_di_billingranap) VALUES ('Yes')");
        await db.ExecAsync("""
            INSERT INTO rekening(kd_rek,nm_rek) VALUES ('KAS','Kas'),('BNK','Bank'),('PTG','Piutang'),('REG','Registrasi'),('KMR','Kamar'),
                ('DSK','Diskon'),('HPS','Tak Terbayar')
            """);
        await db.ExecAsync("INSERT INTO akun_bayar VALUES ('Tunai Uji','KAS',0),('Transfer Uji','BNK',0)");
        await db.ExecAsync("INSERT INTO akun_piutang VALUES ('Piutang Uji','PTG','PJ')");
        await db.ExecAsync("INSERT INTO set_akun_ralan(Registrasi_Ralan) VALUES ('REG')");
        await db.ExecAsync("INSERT INTO set_akun_ranap(Registrasi_Ranap,Kamar_Inap) VALUES ('REG','KMR')");
        await db.ExecAsync("INSERT INTO set_akun(Diskon_Piutang,Piutang_Tidak_Terbayar) VALUES ('DSK','HPS')");

        var petugas = new AuthenticatedUser { UserId = "uji",
            Permissions = new HashSet<string> { "billing_ralan", "billing_ranap", "bayar_piutang" } };
        var hariIni = DateOnly.FromDateTime(DateTime.Today);
        var rencana = new KasirPiutang("Piutang Uji", 20_000, hariIni.AddDays(30));

        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var kasir = new KasirRepository(ctx);
            Assert.Equal(["Piutang Uji"], await kasir.AkunPiutangAsync());
            var tagihan = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(jalan));
            Assert.Null(tagihan.AlasanBelumBisaBayar);
            Assert.Equal(100_000m, tagihan.Total);
            Task Coba(string akunBayar, KasirPiutang p) => kasir.BayarRalanAsync(tagihan, akunBayar, petugas, piutang: p);
            await Assert.ThrowsAsync<ArgumentException>(() => Coba("Tunai Uji", rencana with { UangMuka = 100_000 }));
            await Assert.ThrowsAsync<ArgumentException>(() => Coba("Tunai Uji", rencana with { UangMuka = -1 }));
            await Assert.ThrowsAsync<ArgumentException>(() => Coba("Tunai Uji", rencana with { JatuhTempo = hariIni.AddDays(-1) }));
            await Assert.ThrowsAsync<ArgumentException>(() => Coba("", rencana));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Coba("Tunai Uji", rencana with { AkunPiutang = "Tidak Ada" }));
        }
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM nota_jalan"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));

        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var kasir = new KasirRepository(ctx);
            await kasir.BayarRalanAsync((await kasir.GetBillAsync(jalan))!, "Tunai Uji", petugas, piutang: rencana);
        }
        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            // Tanpa uang muka tidak perlu akun bayar, dan tidak ada baris nota bayar maupun tagihan_sadewa.
            var kasir = new KasirRepository(ctx);
            await kasir.BayarRalanAsync((await kasir.GetBillAsync(tanpaMuka))!, "", petugas, piutang: rencana with { UangMuka = 0 });
        }
        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var kasir = new KasirRepository(ctx);
            await kasir.BayarRanapAsync((await kasir.GetBillAsync(inap))!, "", petugas, piutang: rencana with { UangMuka = 0 });
        }

        var kepala = (await db.FirstAsync("SELECT * FROM piutang_pasien WHERE no_rawat=@p0", jalan))!;
        Assert.Equal(("Belum Lunas", 100000d, 20000d, 80000d),
            (kepala.S("status"), kepala.D("totalpiutang"), kepala.D("uangmuka"), kepala.D("sisapiutang")));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM detail_piutang_pasien WHERE no_rawat=@p0 AND kd_pj='PJ' AND sisapiutang=80000 AND tgltempo=@p1",
            jalan, DateTime.Today.AddDays(30)));
        Assert.Equal(20_000, await db.CountAsync("SELECT SUM(besar_bayar) FROM detail_nota_jalan"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM tagihan_sadewa WHERE jenis_bayar='Uang Muka' AND status='Belum' AND jumlah_bayar=20000"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM tagihan_sadewa"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM nota_inap WHERE no_rawat=@p0", inap));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detail_nota_inap"));
        Assert.Equal(3, await db.CountAsync("SELECT COUNT(*) FROM reg_periksa WHERE status_bayar='Sudah Bayar'"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM jurnal WHERE keterangan LIKE 'PIUTANG PASIEN RAWAT INAP%'"));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM jurnal WHERE keterangan LIKE 'PIUTANG PASIEN RAWAT JALAN%'"));
        Task<long> Saldo(string rekening) =>
            db.CountAsync("SELECT COALESCE(SUM(debet-kredit),0) FROM detailjurnal WHERE kd_rek=@p0", rekening);
        Assert.Equal(20_000, await Saldo("KAS"));
        Assert.Equal(80_000 + 100_000 + 200_000, await Saldo("PTG"));
        Assert.Equal(-300_000, await Saldo("REG"));
        Assert.Equal(-100_000, await Saldo("KMR"));

        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var kasir = new KasirRepository(ctx);
            var tercatat = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(jalan));
            Assert.Equal(("Sudah Bayar", "Belum Lunas"), (tercatat.StatusBayar, tercatat.StatusPiutang));
            var nota = Assert.IsType<KasirKuitansi>(await kasir.MuatKuitansiAsync(tanpaMuka));
            Assert.Empty(nota.Pembayaran);
            Assert.Equal(("Piutang Uji", 100_000m), (nota.Piutang.Single().AkunPiutang, nota.Piutang.Single().Sisa));
        }

        var service = new PiutangPasienService(settings, petugas);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PiutangPasienService(settings, new()).DaftarAsync("", true));
        Assert.Equal(3, (await service.DaftarAsync("", true)).Count);
        Assert.Equal(80_000m, Assert.Single(await service.DaftarAsync(jalan, true)).Sisa);
        var akun = Assert.Single(await service.AkunAsync(jalan));
        await Assert.ThrowsAsync<ArgumentException>(() => service.BayarAsync(jalan, akun, "Tunai Uji", 0, 80_000, 0, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.BayarAsync(jalan, akun, "Tunai Uji", 80_000, 1, 0, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.BayarAsync(jalan, akun, "Tunai Uji", 1000, 0, 0, " "));
        await Assert.ThrowsAsync<ArgumentException>(() => service.BayarAsync(jalan, akun, "Tidak Ada", 1000, 0, 0, "x"));

        await service.BayarAsync(jalan, akun, "Tunai Uji", 30_000, 5_000, 0, "Cicilan pertama");
        await Assert.ThrowsAsync<InvalidOperationException>(() => // sisa di layar sudah usang
            service.BayarAsync(jalan, akun, "Transfer Uji", 1000, 0, 0, "x"));
        akun = Assert.Single(await service.AkunAsync(jalan));
        Assert.Equal(45_000m, akun.Sisa);
        var jumlahJurnal = await db.CountAsync("SELECT COUNT(*) FROM jurnal");
        var ganda = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BayarAsync(jalan, akun, "Tunai Uji", 1000, 0, 0, "x"));
        Assert.Contains("hari ini", ganda.Message);
        Assert.Equal(akun, Assert.Single(await service.AkunAsync(jalan)));
        Assert.Equal(jumlahJurnal, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));

        await service.BayarAsync(jalan, akun, "Transfer Uji", 40_000, 0, 5_000, "Pelunasan");
        Assert.DoesNotContain(await service.DaftarAsync("", true), p => p.NoRawat == jalan);
        var lunas = Assert.Single(await service.DaftarAsync(jalan, false));
        Assert.Equal(("Lunas", 0m), (lunas.Status, lunas.Sisa));
        Assert.Equal(20_000 + 30_000, await Saldo("KAS"));
        Assert.Equal(40_000, await Saldo("BNK"));
        Assert.Equal(5_000, await Saldo("DSK"));
        Assert.Equal(5_000, await Saldo("HPS"));
        Assert.Equal(300_000, await Saldo("PTG"));

        var riwayat = await service.RiwayatAsync(jalan);
        Assert.Equal(2, riwayat.Count);
        var kedua = riwayat.Single(b => b.RekeningBayar == "BNK");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BatalkanAsync(jalan, kedua with { Cicilan = 1 }));
        await service.BatalkanAsync(jalan, kedua);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BatalkanAsync(jalan, kedua));
        var kembali = Assert.Single(await service.DaftarAsync(jalan, true));
        Assert.Equal(("Belum Lunas", 45_000m), (kembali.Status, kembali.Sisa));
        Assert.Equal(45_000m, Assert.Single(await service.AkunAsync(jalan)).Sisa);
        Assert.Equal(0, await Saldo("BNK"));
        Assert.Equal(0, await Saldo("HPS"));
        Assert.Equal(345_000, await Saldo("PTG"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM (SELECT no_jurnal FROM detailjurnal GROUP BY no_jurnal HAVING SUM(debet)<>SUM(kredit)) x"));
    }
}
