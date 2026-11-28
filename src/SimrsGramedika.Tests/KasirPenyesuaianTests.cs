using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class KasirPenyesuaianTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_BIAYA_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_biaya_fixture.";
        }
    }

    [FixtureFact]
    public async Task TambahanPotonganDanDepositMasukTagihanDanJurnal()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_biaya_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_BIAYA_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        var kemarin = DateTime.Today.AddDays(-1);
        const string jalan = "RJ/000001", gratis = "RJ/000002", berlebih = "RJ/000003", jalanDeposit = "RJ/000004";
        const string kurang = "RI/000001", lebih = "RI/000002", cicil = "RI/000003";
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien,tgl_lahir) VALUES ('RM1','Pasien Satu','1990-01-01')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Penjamin Uji')");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('BG','Bangsal Uji','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status,trf_kamar) VALUES ('KM','BG','Kelas 1','KOSONG',100000)");
        foreach (var rawat in new[] { jalan, gratis, berlebih, jalanDeposit, kurang, lebih, cicil })
            await db.ExecAsync("""
                INSERT INTO reg_periksa(no_reg,no_rawat,tgl_registrasi,jam_reg,kd_dokter,no_rkm_medis,biaya_reg,stts,status_lanjut,kd_pj,status_bayar)
                VALUES ('001',@p0,@p1,'07:00:00','DR','RM1',100000,'Sudah',@p2,'PJ','Belum Bayar')
                """, rawat, kemarin, rawat.StartsWith("RI", StringComparison.Ordinal) ? "Ranap" : "Ralan");
        foreach (var rawat in new[] { kurang, lebih, cicil })
            await db.ExecAsync("""
                INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,tgl_keluar,jam_keluar,lama,ttl_biaya,stts_pulang,trf_kamar)
                VALUES (@p0,'KM',@p1,'08:00:00',@p1,'20:00:00',1,100000,'Sehat',100000)
                """, rawat, kemarin);
        await db.ExecAsync("INSERT INTO set_jam_minimal VALUES (1,'No',0,'No',0,'No','No','Yes')");
        await db.ExecAsync("INSERT INTO set_nota (tampilkan_administrasi_di_billingranap) VALUES ('Yes')");
        await db.ExecAsync("""
            INSERT INTO rekening(kd_rek,nm_rek) VALUES ('KAS','Kas'),('PTG','Piutang'),('REG','Registrasi'),('KMR','Kamar'),
                ('TMB','Tambahan'),('PTR','Potongan'),('UM','Uang Muka'),('SUM','Sisa Uang Muka')
            """);
        await db.ExecAsync("INSERT INTO akun_bayar VALUES ('Tunai Uji','KAS',0)");
        await db.ExecAsync("INSERT INTO akun_piutang VALUES ('Piutang Uji','PTG','PJ')");
        await db.ExecAsync("INSERT INTO set_akun_ralan(Registrasi_Ralan,Tambahan_Ralan,Potongan_Ralan) VALUES ('REG','TMB','PTR')");
        await db.ExecAsync("INSERT INTO set_akun_ranap(Registrasi_Ranap,Kamar_Inap,Tambahan_Ranap,Potongan_Ranap) VALUES ('REG','KMR','TMB','PTR')");
        await db.ExecAsync("INSERT INTO set_akun_ranap2(Uang_Muka_Ranap,Sisa_Uang_Muka_Ranap) VALUES ('UM','SUM')");

        var petugas = new AuthenticatedUser { UserId = "uji",
            Permissions = new HashSet<string> { "billing_ralan", "billing_ranap", "tambahan_biaya", "potongan_biaya" } };
        var tanpaHak = new AuthenticatedUser { UserId = "lain", Permissions = new HashSet<string> { "billing_ralan" } };
        await using var ctx = SimrsGramedikaDbContextFactory.Create(settings);
        var kasir = new KasirRepository(ctx);
        async Task<KasirBillItem> Muat(string rawat)
        {
            ctx.ChangeTracker.Clear();
            return Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(rawat));
        }
        Task Tambah(string rawat, JenisPenyesuaian jenis, string nama, decimal besar) =>
            kasir.TambahPenyesuaianAsync(rawat, jenis, nama, besar, petugas);
        Task Deposit(string nomor, string rawat, int besar) => db.ExecAsync(
            "INSERT INTO deposit VALUES (@p0,@p1,@p2,'Tunai Uji',0,@p3,'uji','Uji')", nomor, rawat, kemarin, besar);
        Task<long> Saldo(string rawat, string rekening) => db.CountAsync("""
            SELECT COALESCE(SUM(d.debet-d.kredit),0) FROM detailjurnal d JOIN jurnal j USING(no_jurnal)
            WHERE j.no_bukti=@p0 AND d.kd_rek=@p1
            """, rawat, rekening);

        // Isian ditolak: hak akses, nama, nilai, dan nama ganda.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kasir.TambahPenyesuaianAsync(jalan, JenisPenyesuaian.Potongan, "Keringanan", 1000, tanpaHak));
        await Assert.ThrowsAsync<ArgumentException>(() => Tambah(jalan, JenisPenyesuaian.Tambahan, "  ", 1000));
        await Assert.ThrowsAsync<ArgumentException>(() => Tambah(jalan, JenisPenyesuaian.Tambahan, new string('a', 61), 1000));
        await Assert.ThrowsAsync<ArgumentException>(() => Tambah(jalan, JenisPenyesuaian.Tambahan, "Surat", 0));
        await Assert.ThrowsAsync<ArgumentException>(() => Tambah(jalan, JenisPenyesuaian.Potongan, "Keringanan", -5));
        await Assert.ThrowsAsync<ArgumentException>(() => Tambah(jalan, JenisPenyesuaian.Tambahan, "Surat", 1.00001m));
        await Tambah(jalan, JenisPenyesuaian.Tambahan, " Surat keterangan ", 25_000);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Tambah(jalan, JenisPenyesuaian.Tambahan, "surat keterangan", 1));
        await Tambah(jalan, JenisPenyesuaian.Tambahan, "Salah ketik", 999);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kasir.HapusPenyesuaianAsync(jalan, JenisPenyesuaian.Tambahan, "Salah ketik", tanpaHak));
        await kasir.HapusPenyesuaianAsync(jalan, JenisPenyesuaian.Tambahan, "Salah ketik", petugas);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kasir.HapusPenyesuaianAsync(jalan, JenisPenyesuaian.Tambahan, "Salah ketik", petugas));
        Assert.Equal(new PenyesuaianBiaya("Surat keterangan", 25_000),
            Assert.Single(await kasir.DaftarPenyesuaianAsync(jalan, JenisPenyesuaian.Tambahan)));

        // Rawat jalan: tagihan yang sudah dilihat ditolak bila biayanya berubah.
        var sebelum = await Muat(jalan);
        Assert.Equal(125_000m, sebelum.Total);
        await Tambah(jalan, JenisPenyesuaian.Potongan, "Keringanan", 40_000);
        ctx.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(sebelum, "Tunai Uji", petugas));
        var tagihan = await Muat(jalan);
        Assert.Null(tagihan.AlasanBelumBisaBayar);
        Assert.Equal(85_000m, tagihan.Total);
        Assert.Equal(new KasirRincian("Potongan", "Keringanan", 40_000, 1, -40_000), tagihan.Rincian[^1]);
        ctx.ChangeTracker.Clear();
        await kasir.BayarRalanAsync(tagihan, "Tunai Uji", petugas);
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM billing WHERE no_rawat=@p0 AND status='Potongan' AND biaya=40000 AND tambahan=0 AND totalbiaya=-40000", jalan));
        Assert.Equal(85_000, await Saldo(jalan, "KAS"));
        Assert.Equal(40_000, await Saldo(jalan, "PTR"));
        Assert.Equal(-25_000, await Saldo(jalan, "TMB"));
        Assert.Equal(-100_000, await Saldo(jalan, "REG"));
        var lunas = await Muat(jalan);
        Assert.Equal(85_000m, lunas.Total);
        Assert.Equal(tagihan.Rincian.ToArray(), lunas.Rincian.ToArray());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Tambah(jalan, JenisPenyesuaian.Tambahan, "Terlambat", 1000));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kasir.HapusPenyesuaianAsync(jalan, JenisPenyesuaian.Potongan, "Keringanan", petugas));

        // Potongan yang menghabiskan tagihan tidak memerlukan akun bayar, tetapi tetap dijurnal.
        await Tambah(gratis, JenisPenyesuaian.Potongan, "Dibebaskan", 100_000);
        var nol = await Muat(gratis);
        Assert.Equal(0m, nol.Total);
        ctx.ChangeTracker.Clear();
        await kasir.BayarRalanAsync(nol, "", petugas);
        Assert.Equal(100_000, await Saldo(gratis, "PTR"));
        Assert.Equal(-100_000, await Saldo(gratis, "REG"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detail_nota_jalan WHERE no_rawat=@p0", gratis));
        Assert.NotNull(await kasir.MuatKuitansiAsync(gratis));

        await Tambah(berlebih, JenisPenyesuaian.Potongan, "Terlalu besar", 100_001);
        var minus = await Muat(berlebih);
        Assert.Contains("melebihi tagihan", minus.AlasanBelumBisaBayar);
        ctx.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(minus, "Tunai Uji", petugas));

        await Deposit("DP1", jalanDeposit, 10_000);
        Assert.NotNull((await Muat(jalanDeposit)).AlasanBelumBisaBayar);

        // Rawat inap dengan deposit lebih kecil dari tagihan.
        await Tambah(kurang, JenisPenyesuaian.Tambahan, "Ambulans", 50_000);
        await Tambah(kurang, JenisPenyesuaian.Potongan, "Keringanan", 30_000);
        var awal = await Muat(kurang);
        await Deposit("DP2", kurang, 60_000);
        ctx.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRanapAsync(awal, "Tunai Uji", petugas));
        await Deposit("DP3", kurang, 40_000);
        var inap = await Muat(kurang);
        Assert.Null(inap.AlasanBelumBisaBayar);
        Assert.Equal((220_000m, 100_000m, 120_000m, 0m), (inap.Total, inap.Deposit, inap.HarusDibayar, inap.SisaDeposit));
        ctx.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => kasir.BayarRanapAsync(inap, "", petugas));
        await db.ExecAsync("UPDATE set_akun_ranap2 SET Uang_Muka_Ranap=''");
        ctx.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRanapAsync(inap, "Tunai Uji", petugas));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM nota_inap"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM billing WHERE no_rawat=@p0", kurang));
        await db.ExecAsync("UPDATE set_akun_ranap2 SET Uang_Muka_Ranap='UM'");
        ctx.ChangeTracker.Clear();
        await kasir.BayarRanapAsync(inap, "Tunai Uji", petugas);
        Assert.Equal(120_000, await Saldo(kurang, "KAS"));
        Assert.Equal(100_000, await Saldo(kurang, "UM"));
        Assert.Equal(30_000, await Saldo(kurang, "PTR"));
        Assert.Equal(-50_000, await Saldo(kurang, "TMB"));
        Assert.Equal(-100_000, await Saldo(kurang, "REG"));
        Assert.Equal(-100_000, await Saldo(kurang, "KMR"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM nota_inap WHERE no_rawat=@p0 AND Uang_Muka=100000", kurang));
        Assert.Equal(120_000, await db.CountAsync("SELECT SUM(besar_bayar) FROM detail_nota_inap WHERE no_rawat=@p0", kurang));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM tagihan_sadewa WHERE no_nota=@p0 AND jumlah_tagihan=220000 AND jumlah_bayar=120000", kurang));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM pengembalian_deposit"));
        ctx.ChangeTracker.Clear();
        var kuitansi = Assert.IsType<KasirKuitansi>(await kasir.MuatKuitansiAsync(kurang));
        Assert.Equal((220_000m, 100_000m, 120_000m),
            (kuitansi.Tagihan.Total, kuitansi.Tagihan.Deposit, Assert.Single(kuitansi.Pembayaran).Jumlah));

        // Deposit melebihi tagihan: tanpa pembayaran baru, selisihnya dicatat sebagai pengembalian.
        await Deposit("DP4", lebih, 250_000);
        var sisa = await Muat(lebih);
        Assert.Equal((200_000m, 0m, 50_000m), (sisa.Total, sisa.HarusDibayar, sisa.SisaDeposit));
        var rencana = new KasirPiutang("Piutang Uji", 0, DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
        ctx.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRanapAsync(sisa, "", petugas, piutang: rencana));
        ctx.ChangeTracker.Clear();
        await kasir.BayarRanapAsync(sisa, "", petugas);
        Assert.Equal(250_000, await Saldo(lebih, "UM"));
        Assert.Equal(-50_000, await Saldo(lebih, "SUM"));
        Assert.Equal(0, await Saldo(lebih, "KAS"));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM pengembalian_deposit WHERE no_rawat=@p0 AND petugas='uji' AND besar_pengembalian=50000", lebih));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detail_nota_inap WHERE no_rawat=@p0", lebih));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM tagihan_sadewa WHERE no_nota=@p0", lebih));
        ctx.ChangeTracker.Clear();
        Assert.Equal(50_000m, Assert.IsType<KasirKuitansi>(await kasir.MuatKuitansiAsync(lebih)).Tagihan.SisaDeposit);

        // Deposit, uang muka dan piutang sekaligus.
        await Deposit("DP5", cicil, 50_000);
        var piutang = await Muat(cicil);
        ctx.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            kasir.BayarRanapAsync(piutang, "Tunai Uji", petugas, piutang: rencana with { UangMuka = 150_000 }));
        ctx.ChangeTracker.Clear();
        await kasir.BayarRanapAsync(piutang, "Tunai Uji", petugas, piutang: rencana with { UangMuka = 30_000 });
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM piutang_pasien WHERE no_rawat=@p0 AND totalpiutang=200000 AND uangmuka=80000 AND sisapiutang=120000", cicil));
        Assert.Equal(30_000, await Saldo(cicil, "KAS"));
        Assert.Equal(50_000, await Saldo(cicil, "UM"));
        Assert.Equal(120_000, await Saldo(cicil, "PTG"));

        Assert.Equal(0, await db.CountAsync(
            "SELECT COUNT(*) FROM (SELECT no_jurnal FROM detailjurnal GROUP BY no_jurnal HAVING SUM(debet)<>SUM(kredit)) x"));
    }
}
