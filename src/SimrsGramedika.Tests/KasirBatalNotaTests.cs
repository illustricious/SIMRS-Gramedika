using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class KasirBatalNotaTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_BATAL_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_batal_fixture.";
        }
    }

    [FixtureFact]
    public async Task BatalNotaMembalikJurnalDanMembukaTagihan()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_batal_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_BATAL_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        var kemarin = DateTime.Today.AddDays(-1);
        const string jalan = "2026/01/01/000001", cicil = "2026/01/01/000002", nol = "2026/01/01/000003",
            khanza = "2026/01/01/000004", belum = "2026/01/01/000005";
        const string inap = "2026/01/02/000001", bayi = "2026/01/02/000002";
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien,tgl_lahir) VALUES ('RM1','Pasien Satu','1990-01-01')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Penjamin Uji')");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('BG','Bangsal Uji','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status,trf_kamar) VALUES ('KM','BG','Kelas 1','KOSONG',100000)");
        foreach (var rawat in new[] { jalan, cicil, nol, khanza, belum, inap, bayi })
            await db.ExecAsync("""
                INSERT INTO reg_periksa(no_reg,no_rawat,tgl_registrasi,jam_reg,kd_dokter,no_rkm_medis,biaya_reg,stts,status_lanjut,kd_pj,status_bayar)
                VALUES ('001',@p0,@p1,'07:00:00','DR','RM1',100000,'Sudah',@p2,'PJ','Belum Bayar')
                """, rawat, kemarin, rawat is inap or bayi ? "Ranap" : "Ralan");
        await db.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,tgl_keluar,jam_keluar,lama,ttl_biaya,stts_pulang,trf_kamar)
            VALUES (@p0,'KM',@p1,'08:00:00',@p1,'20:00:00',1,100000,'Sehat',100000)
            """, inap, kemarin);
        await db.ExecAsync("INSERT INTO ranap_gabung(no_rawat,no_rawat2) VALUES (@p0,@p1)", inap, bayi);
        await db.ExecAsync("INSERT INTO set_jam_minimal VALUES (1,'No',0,'No',0,'No','No','Yes')");
        await db.ExecAsync("INSERT INTO set_nota (tampilkan_administrasi_di_billingranap) VALUES ('Yes')");
        await db.ExecAsync("""
            INSERT INTO rekening(kd_rek,nm_rek) VALUES ('KAS','Kas'),('PTG','Piutang'),('REG','Registrasi'),('REG2','Registrasi Baru'),
                ('KMR','Kamar'),('UM','Uang Muka'),('SUM','Sisa Uang Muka'),('DSK','Diskon'),('HPS','Tak Terbayar')
            """);
        await db.ExecAsync("INSERT INTO akun_bayar VALUES ('Tunai Uji','KAS',0)");
        await db.ExecAsync("INSERT INTO akun_piutang VALUES ('Piutang Uji','PTG','PJ')");
        await db.ExecAsync("INSERT INTO set_akun(Diskon_Piutang,Piutang_Tidak_Terbayar) VALUES ('DSK','HPS')");
        await db.ExecAsync("INSERT INTO set_akun_ralan(Registrasi_Ralan) VALUES ('REG')");
        await db.ExecAsync("INSERT INTO set_akun_ranap(Registrasi_Ranap,Kamar_Inap) VALUES ('REG','KMR')");
        await db.ExecAsync("INSERT INTO set_akun_ranap2(Uang_Muka_Ranap,Sisa_Uang_Muka_Ranap) VALUES ('UM','SUM')");

        var petugas = new AuthenticatedUser { UserId = "uji",
            Permissions = new HashSet<string> { "billing_ralan", "billing_ranap", "bayar_piutang", "hapus_nota_salah" } };
        var tanpaHak = new AuthenticatedUser { UserId = "lain", Permissions = new HashSet<string> { "billing_ralan", "billing_ranap" } };
        var hanyaRalan = new AuthenticatedUser { UserId = "ralan",
            Permissions = new HashSet<string> { "billing_ralan", "hapus_nota_salah" } };
        await using var ctx = SimrsGramedikaDbContextFactory.Create(settings);
        var kasir = new KasirRepository(ctx);
        async Task<KasirBillItem> Muat(string rawat)
        {
            ctx.ChangeTracker.Clear();
            return Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(rawat));
        }
        async Task<string> Lunasi(string rawat, KasirPiutang? piutang = null)
        {
            var bill = await Muat(rawat);
            ctx.ChangeTracker.Clear();
            return bill.Ranap ? await kasir.BayarRanapAsync(bill, "Tunai Uji", petugas, piutang: piutang)
                : await kasir.BayarRalanAsync(bill, "Tunai Uji", petugas, piutang: piutang);
        }
        Task<RencanaBatalNota> Rencana(string rawat) => kasir.RencanaBatalNotaAsync(rawat, petugas);
        async Task<string> Ditolak(string rawat, AuthenticatedUser? akun = null) => (await Assert.ThrowsAsync<InvalidOperationException>(
            () => kasir.RencanaBatalNotaAsync(rawat, akun ?? petugas))).Message;
        Task<long> Saldo(string rawat, string rekening) => db.CountAsync("""
            SELECT COALESCE(SUM(d.debet-d.kredit),0) FROM detailjurnal d JOIN jurnal j USING(no_jurnal)
            WHERE j.no_bukti=@p0 AND d.kd_rek=@p1
            """, rawat, rekening);
        Task<long> Jumlah(string tabel, string rawat, string kolom = "no_rawat") =>
            db.CountAsync($"SELECT COUNT(*) FROM {tabel} WHERE {kolom}=@p0", rawat);
        Task<string> Status(string rawat) => db.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat=@p0", rawat);

        // Tanpa nota tidak ada yang bisa dibatalkan.
        Assert.Contains("Tidak ada nota", await Ditolak(belum));

        // Rawat jalan tunai: hak akses, isi rencana, lalu semua jejak pelunasan hilang dan jurnalnya terbalik.
        var tagihanAwal = await Muat(jalan);
        var nota = await Lunasi(jalan);
        await db.ExecAsync("""
            INSERT INTO tagihan_mandiri(no_rkm_medis,no_nota,no_rawat,status_lanjut,status_bayar)
            VALUES ('RM1','M1',@p0,'Ralan','Pending'),('RM1','M2',@p0,'Ralan','Sudah')
            """, jalan);
        await db.ExecAsync("""
            INSERT INTO tagihan_briva(no_rkm_medis,no_tagihan,status_tagihan,status_bayar) VALUES ('RM1','2601010001','Ralan','Pending')
            """);
        Assert.Contains("Hapus Nota Salah", await Ditolak(jalan, tanpaHak));
        var rencana = await Rencana(jalan);
        Assert.Equal((nota, false, false, 100_000m, 0m, "Pasien Satu"),
            (rencana.NoNota, rencana.Ranap, rencana.Piutang, rencana.Tunai, rencana.JumlahPiutang, rencana.NamaPasien));
        Assert.NotNull(rencana.NoJurnal);

        // Rekening yang diganti sesudah pelunasan tidak mengubah baris yang dibalik.
        await db.ExecAsync("UPDATE set_akun_ralan SET Registrasi_Ralan='REG2'");

        // Kegagalan di tengah jalan tidak meninggalkan perubahan apa pun.
        await db.ExecAsync("CREATE TRIGGER tolak_hapus BEFORE DELETE ON billing FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='uji'");
        await Assert.ThrowsAnyAsync<Exception>(() => kasir.BatalkanNotaAsync(rencana, petugas));
        await db.ExecAsync("DROP TRIGGER tolak_hapus");
        Assert.Equal((1L, 1L, "Sudah Bayar"),
            (await Jumlah("jurnal", jalan, "no_bukti"), await Jumlah("nota_jalan", jalan), await Status(jalan)));

        await kasir.BatalkanNotaAsync(rencana, petugas);
        foreach (var tabel in new[] { "nota_jalan", "detail_nota_jalan", "billing" })
            Assert.Equal(0, await Jumlah(tabel, jalan));
        Assert.Equal(0, await Jumlah("tagihan_sadewa", jalan, "no_nota"));
        Assert.Equal("M2", await db.StrAsync("SELECT GROUP_CONCAT(no_nota) FROM tagihan_mandiri"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM tagihan_briva"));
        Assert.Equal("Belum Bayar", await Status(jalan));
        Assert.Equal((0L, 0L, 0L), (await Saldo(jalan, "KAS"), await Saldo(jalan, "REG"), await Saldo(jalan, "REG2")));
        Assert.Equal(1, await db.CountAsync("""
            SELECT COUNT(*) FROM jurnal WHERE no_bukti=@p0 AND keterangan=
                CONCAT('PEMBATALAN PEMBAYARAN PASIEN RAWAT JALAN ',@p0,' RM1 Pasien Satu, DIBATALKAN OLEH uji')
            """, jalan));
        var terbuka = await Muat(jalan);
        Assert.Null(terbuka.AlasanBelumBisaBayar);
        Assert.Equal(tagihanAwal.Total, terbuka.Total);
        Assert.Contains("Tidak ada nota", await Ditolak(jalan));
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BatalkanNotaAsync(rencana, petugas));

        // Dilunasi lagi lalu dibatalkan lagi: yang dibalik jurnal terbaru, dan rencana lama ditolak.
        await Lunasi(jalan);
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BatalkanNotaAsync(rencana, petugas));
        var kedua = await Rencana(jalan);
        Assert.NotEqual(rencana.NoJurnal, kedua.NoJurnal);
        await kasir.BatalkanNotaAsync(kedua, petugas);
        Assert.Equal((0L, 0L, 0L), (await Saldo(jalan, "KAS"), await Saldo(jalan, "REG"), await Saldo(jalan, "REG2")));
        Assert.Equal(4, await Jumlah("jurnal", jalan, "no_bukti"));

        // Piutang: cicilan, penagihan dan perubahan sisa menahan pembatalan.
        var tempo = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        await Lunasi(cicil, new KasirPiutang("Piutang Uji", 25_000, tempo));
        var layananPiutang = new PiutangPasienService(settings, petugas);
        var akun = Assert.Single(await layananPiutang.AkunAsync(cicil));
        await layananPiutang.BayarAsync(cicil, akun, "Tunai Uji", 10_000, 0, 0, "Cicilan uji");
        Assert.Contains("cicilan", await Ditolak(cicil));
        await layananPiutang.BatalkanAsync(cicil, Assert.Single(await layananPiutang.RiwayatAsync(cicil)));
        await db.ExecAsync("INSERT INTO detail_penagihan_piutang(no_tagihan,no_rawat,sisapiutang) VALUES ('TG1',@p0,75000)", cicil);
        Assert.Contains("penagihan", await Ditolak(cicil));
        await db.ExecAsync("DELETE FROM detail_penagihan_piutang");
        await db.ExecAsync("UPDATE piutang_pasien SET status='Lunas' WHERE no_rawat=@p0", cicil);
        Assert.Contains("tanpa catatan cicilan", await Ditolak(cicil));
        await db.ExecAsync("UPDATE piutang_pasien SET status='Belum Lunas' WHERE no_rawat=@p0", cicil);
        await db.ExecAsync("UPDATE detail_piutang_pasien SET sisapiutang=70000 WHERE no_rawat=@p0", cicil);
        Assert.Contains("tanpa catatan cicilan", await Ditolak(cicil));
        await db.ExecAsync("UPDATE detail_piutang_pasien SET sisapiutang=75000 WHERE no_rawat=@p0", cicil);
        var piutang = await Rencana(cicil);
        Assert.Equal((true, 25_000m, 75_000m), (piutang.Piutang, piutang.Tunai, piutang.JumlahPiutang));
        await kasir.BatalkanNotaAsync(piutang, petugas);
        Assert.Equal((0L, 0L), (await Jumlah("piutang_pasien", cicil), await Jumlah("detail_piutang_pasien", cicil)));
        Assert.Equal((0L, 0L), (await Saldo(cicil, "KAS"), await Saldo(cicil, "PTG")));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM jurnal WHERE no_bukti=@p0 AND keterangan LIKE 'PEMBATALAN PIUTANG PASIEN RAWAT JALAN %'", cicil));
        Assert.Null((await Muat(cicil)).AlasanBelumBisaBayar);

        // Nota Khanza memakai kalimat jurnalnya sendiri; billing parsial dan jurnal yang tidak cocok ditolak.
        await db.ExecAsync("INSERT INTO nota_jalan VALUES (@p0,'2026/01/01/RJ0900',@p1,'10:00:00')", khanza, kemarin);
        await db.ExecAsync("INSERT INTO detail_nota_jalan VALUES (@p0,'Tunai Uji',0,50000)", khanza);
        await db.ExecAsync("""
            INSERT INTO billing(noindex,no_rawat,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
            VALUES (0,@p0,'No.Nota','Biaya',':',50000,1,0,50000,'Registrasi')
            """, khanza);
        await db.ExecAsync("UPDATE reg_periksa SET status_bayar='Sudah Bayar' WHERE no_rawat=@p0", khanza);
        await db.ExecAsync("INSERT INTO jurnal VALUES ('JRK1',@p0,@p1,'10:00:00','U','PEMBAYARAN PASIEN RAWAT JALAN, DIPOSTING OLEH x')",
            khanza, kemarin);
        Assert.Contains("tidak ditemukan", await Ditolak(khanza));
        await db.ExecAsync("""
            UPDATE jurnal SET keterangan=CONCAT('PEMBAYARAN PASIEN RAWAT JALAN ',no_bukti,' RM1 Pasien Satu, DIPOSTING OLEH x')
            WHERE no_jurnal='JRK1'
            """);
        await db.ExecAsync("INSERT INTO detailjurnal VALUES ('JRK1','KAS',40000,0),('JRK1','REG',0,40000)");
        Assert.Contains("tidak cocok", await Ditolak(khanza));
        await db.ExecAsync("UPDATE detailjurnal SET debet=IF(debet>0,50000,0),kredit=IF(kredit>0,50000,0) WHERE no_jurnal='JRK1'");
        await kasir.BatalkanNotaAsync(await Rencana(khanza), petugas);
        Assert.Equal((0L, 0L, "Belum Bayar"), (await Saldo(khanza, "KAS"), await Saldo(khanza, "REG"), await Status(khanza)));

        // Nota tanpa nilai uang tidak punya jurnal.
        await db.ExecAsync("UPDATE reg_periksa SET biaya_reg=0 WHERE no_rawat=@p0", nol);
        await Lunasi(nol);
        var tanpaNilai = await Rencana(nol);
        Assert.Null(tanpaNilai.NoJurnal);
        await kasir.BatalkanNotaAsync(tanpaNilai, petugas);
        Assert.Equal((0L, 0L, "Belum Bayar"),
            (await Jumlah("jurnal", nol, "no_bukti"), await Jumlah("nota_jalan", nol), await Status(nol)));

        // Rawat inap dengan deposit berlebih dan bayi rawat gabung.
        await db.ExecAsync("INSERT INTO deposit VALUES ('DP1',@p0,@p1,'Tunai Uji',0,250000,'uji','Uji')", inap, kemarin);
        var sebelum = await Muat(inap);
        await Lunasi(inap);
        Assert.Equal(("Sudah Bayar", "Sudah Bayar"), (await Status(inap), await Status(bayi)));
        Assert.Contains("rawat inap", await Ditolak(inap, hanyaRalan));
        Assert.Contains(inap, await Ditolak(bayi));
        var rencanaInap = await Rencana(inap);
        Assert.Equal((true, 0m, 250_000m, 250_000m - sebelum.Total),
            (rencanaInap.Ranap, rencanaInap.Tunai, rencanaInap.Deposit, rencanaInap.PengembalianDeposit));
        await kasir.BatalkanNotaAsync(rencanaInap, petugas);
        foreach (var tabel in new[] { "nota_inap", "detail_nota_inap", "billing", "pengembalian_deposit" })
            Assert.Equal(0, await Jumlah(tabel, inap));
        Assert.Equal(1, await Jumlah("deposit", inap));
        Assert.Equal(("Belum Bayar", "Belum Bayar"), (await Status(inap), await Status(bayi)));
        foreach (var rekening in new[] { "UM", "SUM", "REG", "REG2", "KMR" })
            Assert.Equal(0, await Saldo(inap, rekening));
        var sesudah = await Muat(inap);
        Assert.Null(sesudah.AlasanBelumBisaBayar);
        Assert.Equal((sebelum.Total, sebelum.Deposit), (sesudah.Total, sesudah.Deposit));
        await Lunasi(inap);

        // Bayi yang punya billing sendiri tidak ikut dibuka.
        await db.ExecAsync("""
            INSERT INTO billing(noindex,no_rawat,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
            VALUES (0,@p0,'','Billing bayi',':',1,1,0,1,'Registrasi')
            """, bayi);
        await kasir.BatalkanNotaAsync(await Rencana(inap), petugas);
        Assert.Equal(("Belum Bayar", "Sudah Bayar"), (await Status(inap), await Status(bayi)));

        Assert.Equal(0, await db.CountAsync(
            "SELECT COUNT(*) FROM (SELECT no_jurnal FROM detailjurnal GROUP BY no_jurnal HAVING SUM(debet)<>SUM(kredit)) x"));
    }
}
