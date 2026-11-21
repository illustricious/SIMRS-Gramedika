using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Keuangan;
using SimrsKhanza.Data.Master;
using SimrsKhanza.Data.Pengaturan;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Tests;

public class BillingRanapTests
{
    [Theory]
    [InlineData(6, 0, 6, false, 0)]
    [InlineData(6, 1, 6, false, 1)]
    [InlineData(6, 0, 6, true, 1)]
    [InlineData(6, 1, 6, true, 2)]
    [InlineData(15, 0, 20, false, 1)]
    [InlineData(48, 0, 6, true, 3)]
    public void HitungLama_MengikutiJamMinimalDanHariAwal(int jam, int menit, int jamMinimal, bool hariAwal, int hasil)
    {
        var masuk = new DateTime(2026, 11, 19, 10, 0, 0);
        Assert.Equal(hasil, AturanLamaRawat.HitungHari(masuk, masuk.AddHours(jam).AddMinutes(menit), jamMinimal, hariAwal));
    }

    private sealed class JamFixture : TimeProvider
    {
        public DateTimeOffset Utctime { get; set; } = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Utctime;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public RawatInapKey Key(string rawat, string kamar) =>
            new(rawat, kamar, DateOnly.FromDateTime(Utctime.DateTime), TimeOnly.FromDateTime(Utctime.DateTime));
    }

    public sealed class MariaDbFixtureFactAttribute : FactAttribute
    {
        public MariaDbFixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_RANAP_FIXTURE_PORT"), out _))
                Skip = "Memerlukan database skema kosong simrs_ranap_fixture pada MariaDB lokal.";
        }
    }

    [MariaDbFixtureFact]
    public async Task MasukPindahPulangPelunasan_MenjagaTarifDanJurnal()
    {
        var settings = new DatabaseSettings
        {
            Host = "127.0.0.1",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_RANAP_FIXTURE_PORT")!),
            Database = "simrs_ranap_fixture",
            User = "root",
            Password = Environment.GetEnvironmentVariable("SIMRS_RANAP_FIXTURE_PASSWORD") ?? ""
        };
        await using var sql = await Db.OpenAsync(settings);
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM reg_periksa"));
        await SeedAsync(sql);

        const string rawat = "2026/11/19/000001";
        await SeedTindakanObatAsync(sql, rawat);
        await using (var ctx = SimrsKhanzaDbContextFactory.Create(settings))
        {
            var kasir = new KasirRepository(ctx);
            var ralan = await kasir.GetBillAsync(rawat);
            Assert.NotNull(ralan);
            Assert.Equal(25_000m, ralan.Total);
            Assert.Equal(14_500m, ralan.RincianTindakanObat.Sum(row => row.Total));
            Assert.Equal(new KasirRincian("Obat & BHP", "Obat Uji", 2_000m, 1.5m, 4_500m),
                Assert.Single(ralan.RincianTindakanObat, row => row.Jenis == "Obat & BHP"));
            Assert.NotNull(ralan.AlasanBelumBisaBayar);
            await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.MarkPaidAsync(rawat));
            Assert.Equal("Belum Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat=@p0", rawat));
        }

        var jam = new JamFixture();
        var roomA = jam.Key(rawat, "UJI-A");
        await using (var ctx = SimrsKhanzaDbContextFactory.Create(settings))
        {
            var repo = new RawatInapRepository(ctx, jam);
            var tersedia = await repo.GetAvailableKamarForVisitAsync(rawat);
            Assert.Equal(300_000m, tersedia.Single(row => row.KodeKamar == "UJI-A").TarifKamar);
            Assert.Equal(450_000m, tersedia.Single(row => row.KodeKamar == "UJI-B").TarifKamar);
            await repo.AdmitAsync(rawat, "UJI-A", "Data sintetis");
        }

        await using (var ctx = SimrsKhanzaDbContextFactory.Create(settings))
        {
            var aktif = await new KasirRepository(ctx).GetBillAsync(rawat);
            Assert.NotNull(aktif);
            Assert.Contains("masih menempati kamar", aktif.AlasanBelumBisaBayar);
            Assert.Equal(29_500m, aktif.RincianTindakanObat.Sum(row => row.Total));
        }
        await sql.ExecAsync("DELETE FROM rawat_jl_pr WHERE no_rawat=@p0", rawat);
        await sql.ExecAsync("DELETE FROM rawat_inap_dr WHERE no_rawat=@p0", rawat);
        await sql.ExecAsync("DELETE FROM detail_pemberian_obat WHERE no_rawat=@p0", rawat);

        await sql.ExecAsync("UPDATE set_harga_kamar SET tarif=900000 WHERE kd_kamar='UJI-A'");
        await new MasterRepository(settings).SaveAsync(MenuUSpecs.Tiles["btnSetHargaKamar"].Single(),
            new() { ["kd_kamar"] = "UJI-B", ["kd_pj"] = "UJI", ["tarif"] = 500_000d }, null);
        jam.Utctime = jam.Utctime.AddHours(26);
        var roomB = jam.Key(rawat, "UJI-B");
        await using (var ctx = SimrsKhanzaDbContextFactory.Create(settings))
            Assert.Equal(300_000m, await new RawatInapRepository(ctx, jam).PindahKamarAsync(roomA, "UJI-B"));

        await sql.ExecAsync("UPDATE set_harga_kamar SET tarif=800000 WHERE kd_kamar='UJI-B'");
        jam.Utctime = jam.Utctime.AddHours(7);
        await using (var ctx = SimrsKhanzaDbContextFactory.Create(settings))
            Assert.Equal(500_000m, await new RawatInapRepository(ctx, jam).DischargeAsync(roomB, StatusPulang.Sehat, "Data sintetis"));

        await using var billingContext = SimrsKhanzaDbContextFactory.Create(settings);
        var billing = new KasirRepository(billingContext);
        var bill = await billing.GetBillAsync(rawat);
        Assert.NotNull(bill);
        Assert.Null(bill.AlasanBelumBisaBayar);
        Assert.Equal(800_000m, bill.BiayaKamar);
        Assert.Equal(85_000m, bill.Rincian.Where(r => r.Jenis == "Harian").Sum(r => r.Total));
        Assert.Equal(125_000m, bill.Rincian.Where(r => r.Jenis == "Biaya Masuk").Sum(r => r.Total));
        // Independent calculation based on the joins in Khanza Java's billing queries.
        var legacy = Convert.ToDecimal(await sql.ScalarAsync("""
            SELECT r.biaya_reg
                + (SELECT SUM(k.ttl_biaya) FROM kamar_inap k WHERE k.no_rawat=r.no_rawat)
                + (SELECT SUM(h.besar_biaya*h.jml*k.lama) FROM kamar_inap k JOIN biaya_harian h USING(kd_kamar) WHERE k.no_rawat=r.no_rawat)
                + (SELECT SUM(s.besar_biaya) FROM kamar_inap k JOIN biaya_sekali s USING(kd_kamar) WHERE k.no_rawat=r.no_rawat)
            FROM reg_periksa r WHERE r.no_rawat=@p0
            """, rawat));
        Assert.Equal(1_035_000m, bill.Total);
        Assert.Equal(legacy, bill.Total);

        await sql.ExecAsync("UPDATE biaya_harian SET besar_biaya=21000 WHERE kd_kamar='UJI-A'");
        await Assert.ThrowsAsync<InvalidOperationException>(() => billing.BayarRanapAsync(bill, "Tunai Uji", "uiji"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM nota_inap"));
        await sql.ExecAsync("UPDATE biaya_harian SET besar_biaya=20000 WHERE kd_kamar='UJI-A'");
        await sql.ExecAsync("UPDATE set_akun_ranap2 SET Harian_Ranap=NULL");
        await Assert.ThrowsAsync<InvalidOperationException>(() => billing.BayarRanapAsync(bill, "Tunai Uji", "uiji"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM nota_inap"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM billing"));
        Assert.Equal("Belum Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat=@p0", rawat));
        await sql.ExecAsync("UPDATE set_akun_ranap2 SET Harian_Ranap='UJI-H'");

        var nota = await billing.BayarRanapAsync(bill, "Tunai Uji", "uiji");
        await Assert.ThrowsAsync<InvalidOperationException>(() => billing.BayarRanapAsync(bill, "Tunai Uji", "uiji"));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM nota_inap WHERE no_nota=@p0", nota));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM jurnal WHERE no_bukti=@p0", rawat));
        var jurnal = await sql.FirstAsync("SELECT SUM(debet) AS Debet,SUM(kredit) AS Kredit FROM detailjurnal");
        Assert.NotNull(jurnal);
        Assert.Equal(legacy, Convert.ToDecimal(jurnal["Debet"]));
        Assert.Equal(legacy, Convert.ToDecimal(jurnal["Kredit"]));
        Assert.Equal(2, await sql.CountAsync("SELECT COUNT(*) FROM kamar WHERE status='KOSONG'"));

        await sql.ExecAsync("UPDATE biaya_harian SET besar_biaya=99000");
        var lunas = await billing.GetBillAsync(rawat);
        Assert.NotNull(lunas);
        Assert.Equal("Sudah Bayar", lunas.StatusBayar);
        Assert.Equal(nota, lunas.NoNota);
        Assert.Equal(legacy, lunas.Total);
        Assert.Equal(bill.Rincian.ToArray(), lunas.Rincian.ToArray());
        Assert.Empty(lunas.RincianTindakanObat);
    }

    private static async Task SeedTindakanObatAsync(Db db, string rawat)
    {
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        try
        {
            await db.ExecAsync("INSERT INTO jns_perawatan (kd_jenis_prw,nm_perawatan,total_byrpr) VALUES ('UJI-PR','Tindakan Ralan Uji',12000)");
            await db.ExecAsync("INSERT INTO jns_perawatan_inap (kd_jenis_prw,nm_perawatan) VALUES ('UJI-DR','Tindakan Ranap Uji')");
            await db.ExecAsync("INSERT INTO databarang (kode_brng,nama_brng) VALUES ('UJI-OB','Obat Uji')");
            await db.ExecAsync("""
                INSERT INTO rawat_jl_pr (no_rawat,kd_jenis_prw,nip,tgl_perawatan,jam_rawat,biaya_rawat)
                VALUES (@p0,'UJI-PR','UJI','2026-11-19','09:20:00',10000)
                """, rawat);
            await db.ExecAsync("""
                INSERT INTO rawat_inap_dr (no_rawat,kd_jenis_prw,kd_dokter,tgl_perawatan,jam_rawat,biaya_rawat)
                VALUES (@p0,'UJI-DR','UJI','2026-11-19','11:00:00',13000)
                """, rawat);
            await db.ExecAsync("""
                INSERT INTO detail_pemberian_obat
                (tgl_perawatan,jam,no_rawat,kode_brng,biaya_obat,jml,embalase,tuslah,total,status,no_batch,no_faktur)
                VALUES ('2026-11-19','09:30:00',@p0,'UJI-OB',2000,1.5,500,1000,4500,'Ralan','UJI','UJI')
                """, rawat);
        }
        finally { await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=1"); }
    }

    private static async Task SeedAsync(Db db)
    {
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        try
        {
            await db.ExecAsync("INSERT INTO penjab VALUES ('UJI','Cara Bayar Uji','','','','','1')");
            await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien,nm_ibu,alamat,umur,pnd,namakeluarga,kd_pj) VALUES ('UJI-RM','Pasien Sintetis','','Alamat Uji','30 Th','-','','UJI')");
            await db.ExecAsync("""
                INSERT INTO reg_periksa
                (no_reg,no_rawat,tgl_registrasi,jam_reg,no_rkm_medis,biaya_reg,stts,stts_daftar,status_lanjut,kd_pj,status_bayar,status_poli)
                VALUES ('001','2026/11/19/000001','2026-11-19','09:00:00','UJI-RM',25000,'Belum','Lama','Ralan','UJI','Belum Bayar','Lama')
                """);
            await db.ExecAsync("INSERT INTO bangsal VALUES ('UJI','Bangsal Uji','1')");
            await db.ExecAsync("INSERT INTO kamar VALUES ('UJI-A','UJI',400000,'KOSONG','Kelas 1','1'),('UJI-B','UJI',450000,'KOSONG','Kelas 1','1')");
            await db.ExecAsync("INSERT INTO set_harga_kamar VALUES ('UJI-A','UJI',300000)");
            await db.ExecAsync("INSERT INTO set_jam_minimal VALUES (6,'No',0,'No',0,'No','No','Yes')");
            await db.ExecAsync("INSERT INTO set_nota (tampilkan_administrasi_di_billingranap) VALUES ('Yes')");
            await db.ExecAsync("INSERT INTO biaya_harian VALUES ('UJI-A','Harian A',20000,2),('UJI-B','Harian B',15000,3)");
            await db.ExecAsync("INSERT INTO biaya_sekali VALUES ('UJI-A','Masuk A',50000),('UJI-B','Masuk B',75000)");
            await db.ExecAsync("INSERT INTO rekening (kd_rek,nm_rek) VALUES ('UJI-C','Kas Uji'),('UJI-R','Registrasi Uji'),('UJI-K','Kamar Uji'),('UJI-H','Harian Uji')");
            await db.ExecAsync("INSERT INTO akun_bayar VALUES ('Tunai Uji','UJI-C',0)");
            await db.ExecAsync("INSERT INTO set_akun_ranap (Registrasi_Ranap,Kamar_Inap) VALUES ('UJI-R','UJI-K')");
            await db.ExecAsync("INSERT INTO set_akun_ranap2 (Harian_Ranap) VALUES ('UJI-H')");
        }
        finally { await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=1"); }
    }
}
