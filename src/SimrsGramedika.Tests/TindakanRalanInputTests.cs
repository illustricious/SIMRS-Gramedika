using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public class TindakanRalanInputTests
{
    private const string Rawat = "2026/11/24/000001";
    private static readonly DateTime Waktu = DateTime.Today.AddHours(10);

    [Fact]
    public async Task AkunTanpaIzinDitolakSebelumAksesDatabase()
    {
        var service = new TindakanRalanInputService(new DatabaseSettings { Host = "127.0.0.1", Port = 1 }, Rawat, new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CariTarifAsync(TindakanRalanJenis.Dokter, ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PelaksanaAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Dokter, Waktu, "DR-UJI", "", []));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MuatTindakanAsync(TindakanRalanJenis.Dokter, "X", "DR-UJI", "", Waktu));
        var snapshot = new TindakanRalanSnapshot(TindakanRalanJenis.Dokter, Rawat, "X", "DR-UJI", "", Waktu, 1, 0, 0, 0, 0, 0, 0, "Belum");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(snapshot));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(snapshot, Waktu, "DR-UJI", "", new("X", "Uji", "", 1)));
    }

    public sealed class RalanFixtureFactAttribute : FactAttribute
    {
        public RalanFixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_RALAN_FIXTURE_PORT"), out _))
                Skip = "Memerlukan database skema kosong simrs_ralan_fixture pada MariaDB lokal.";
        }
    }

    [RalanFixtureFact]
    public async Task TigaJenisTindakanMemakaiTarifKunjunganDanMenjagaJurnal()
    {
        var settings = new DatabaseSettings
        {
            Host = "127.0.0.1",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_RALAN_FIXTURE_PORT")!),
            Database = "simrs_ralan_fixture",
            User = "root",
            Password = Environment.GetEnvironmentVariable("SIMRS_RALAN_FIXTURE_PASSWORD") ?? ""
        };
        await using var db = await Db.OpenAsync(settings);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM reg_periksa"));
        await SeedAsync(db);
        await VerifikasiSoapAsync(db, settings);
        await VerifikasiLaporanFarmasiAsync(db, settings);
        var service = new TindakanRalanInputService(settings, Rawat, new()
        {
            UserId = "PET-UJI",
            Permissions = new HashSet<string> { TindakanRalanInputService.Izin },
        });

        var (dokter, petugas) = await service.PelaksanaAsync();
        Assert.Contains(dokter, p => p.Nik == "DR-UJI");
        Assert.Contains(petugas, p => p.Nik == "PET-UJI");
        var semua = await service.CariTarifAsync(TindakanRalanJenis.Dokter, "Kategori Uji");
        Assert.Equal(new[] { "A-UMUM", "Z-UJI" }, semua.Select(t => t.Kode).Order());
        await db.ExecAsync("UPDATE set_tarif SET poli_ralan='No'");
        Assert.Contains(await service.CariTarifAsync(TindakanRalanJenis.Dokter, ""), t => t.Kode == "POLI-LAIN");
        await db.ExecAsync("UPDATE set_tarif SET poli_ralan='Yes',cara_bayar_ralan='No'");
        Assert.Contains(await service.CariTarifAsync(TindakanRalanJenis.Dokter, ""), t => t.Kode == "PJ-LAIN");
        await db.ExecAsync("UPDATE set_tarif SET cara_bayar_ralan='Yes'");

        var tarifDr = Assert.Single(await service.CariTarifAsync(TindakanRalanJenis.Dokter, "Z-UJI"));
        var tarifPr = Assert.Single(await service.CariTarifAsync(TindakanRalanJenis.Paramedis, "Z-UJI"));
        var tarifDrPr = Assert.Single(await service.CariTarifAsync(TindakanRalanJenis.DokterParamedis, "Z-UJI"));
        // Simulate another Gramedika client closing billing while holding the same visit lock.
        await db.BeginAsync();
        await db.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", Rawat);
        await db.ExecAsync("INSERT INTO billing(noindex,no_rawat,`no`,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya) VALUES (1,@p0,'','Uji','',0,1,0,0)", Rawat);
        var bersamaan = service.SimpanAsync(TindakanRalanJenis.Dokter, Waktu, "DR-UJI", "", [tarifDr]);
        await Task.Delay(300);
        Assert.False(bersamaan.IsCompleted, "Simpan tindakan harus menunggu kunci kunjungan.");
        await db.CommitAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bersamaan);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM rawat_jl_dr"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
        await db.ExecAsync("DELETE FROM billing WHERE no_rawat=@p0", Rawat);

        // Reverse order: a treatment is committed while the real cashier waits on the visit lock.
        await db.BeginAsync();
        await db.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", Rawat);
        await db.ExecAsync("""
            INSERT INTO rawat_jl_dr(no_rawat,kd_jenis_prw,kd_dokter,tgl_perawatan,jam_rawat,material,bhp,tarif_tindakandr,kso,menejemen,biaya_rawat,stts_bayar)
            VALUES(@p0,'Z-UJI','DR-UJI',@p1,@p2,3,2,10,4,1,100,'Belum')
            """, Rawat, Waktu.Date, Waktu.TimeOfDay);
        await using (var kasirDb = SimrsGramedikaDbContextFactory.Create(settings))
        {
            var kasir = new KasirRepository(kasirDb);
            var snapshot = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(Rawat));
            var kasirUser = new AuthenticatedUser { UserId = "PET-UJI", Permissions = new HashSet<string> { "kasir_ralan" } };
            var pembayaran = kasir.BayarRalanAsync(snapshot, "Tunai", kasirUser);
            await Task.Delay(300);
            Assert.False(pembayaran.IsCompleted, "Kasir harus menunggu kunci kunjungan.");
            await db.CommitAsync();
            var changed = await Assert.ThrowsAsync<InvalidOperationException>(() => pembayaran);
            Assert.Contains("Rincian tagihan berubah", changed.Message);
        }
        Assert.Equal("Belum Bayar", await db.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat=@p0", Rawat));
        await db.ExecAsync("DELETE FROM rawat_jl_dr WHERE no_rawat=@p0", Rawat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Dokter, Waktu.Date.AddSeconds(-1), "DR-UJI", "", [tarifDr]));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(TindakanRalanJenis.Paramedis, Waktu, "", "HILANG", [tarifPr]));
        await service.SimpanAsync(TindakanRalanJenis.Dokter, Waktu, "DR-UJI", "", [tarifDr]);
        await service.SimpanAsync(TindakanRalanJenis.Paramedis, Waktu, "", "PET-UJI", [tarifPr]);
        await service.SimpanAsync(TindakanRalanJenis.DokterParamedis, Waktu, "DR-UJI", "PET-UJI", [tarifDrPr]);

        Assert.Equal(100, await db.CountAsync("SELECT biaya_rawat FROM rawat_jl_dr WHERE kd_dokter='DR-UJI' AND stts_bayar='Belum'"));
        Assert.Equal(200, await db.CountAsync("SELECT biaya_rawat FROM rawat_jl_pr WHERE nip='PET-UJI' AND stts_bayar='Belum'"));
        Assert.Equal(300, await db.CountAsync("SELECT biaya_rawat FROM rawat_jl_drpr WHERE kd_dokter='DR-UJI' AND nip='PET-UJI' AND stts_bayar='Belum'"));
        Assert.Equal(3, await db.CountAsync("SELECT COUNT(*) FROM jurnal WHERE no_bukti=@p0 AND keterangan LIKE '%PET-UJI'", Rawat));
        var totals = await db.FirstAsync("SELECT SUM(debet) AS d,SUM(kredit) AS k FROM detailjurnal");
        Assert.Equal(690, totals!.D("d"));
        Assert.Equal(690, totals.D("k"));
        Assert.Equal(20, await db.CountAsync("SELECT SUM(debet) FROM detailjurnal WHERE kd_rek='JDR-D'"));
        Assert.Equal(40, await db.CountAsync("SELECT SUM(debet) FROM detailjurnal WHERE kd_rek='JPR-D'"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Dokter, Waktu, "DR-UJI", "", semua));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM rawat_jl_dr"));
        await db.ExecAsync("UPDATE jns_perawatan SET total_byrdr=101 WHERE kd_jenis_prw='Z-UJI'");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Dokter, Waktu.AddSeconds(1), "DR-UJI", "", semua));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM rawat_jl_dr"));
        await db.ExecAsync("UPDATE set_akun_ralan SET Tindakan_Ralan=NULL");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Paramedis, Waktu.AddSeconds(1), "", "PET-UJI", [tarifPr]));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM rawat_jl_pr"));
        await db.ExecAsync("UPDATE set_akun_ralan SET Tindakan_Ralan='CR'");
        await db.ExecAsync("INSERT INTO billing(noindex,no_rawat,`no`,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya) VALUES (1,@p0,'','Uji','',0,1,0,0)", Rawat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Paramedis, Waktu.AddSeconds(2), "", "PET-UJI", [tarifPr]));
        await db.ExecAsync("DELETE FROM billing WHERE no_rawat=@p0", Rawat);
        await db.ExecAsync("UPDATE reg_periksa SET stts='Batal' WHERE no_rawat=@p0", Rawat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Paramedis, Waktu.AddSeconds(2), "", "PET-UJI", [tarifPr]));
        Assert.Equal(3, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
        await db.ExecAsync("UPDATE reg_periksa SET stts='Belum' WHERE no_rawat=@p0", Rawat);
        var dr = await service.MuatTindakanAsync(TindakanRalanJenis.Dokter, "Z-UJI", "DR-UJI", "", Waktu);
        var pr = await service.MuatTindakanAsync(TindakanRalanJenis.Paramedis, "Z-UJI", "", "PET-UJI", Waktu);
        var drpr = await service.MuatTindakanAsync(TindakanRalanJenis.DokterParamedis, "Z-UJI", "DR-UJI", "PET-UJI", Waktu);
        var pengganti = Assert.Single(semua, t => t.Kode == "A-UMUM");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(dr with { NoRawat = "LAIN" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(dr with { NoRawat = "LAIN" }, Waktu, "DR-UJI", "", pengganti));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(dr with { Waktu = Waktu.AddDays(-3) }));
        await db.ExecAsync("INSERT INTO billing(noindex,no_rawat,`no`,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya) VALUES (1,@p0,'','Uji','',0,1,0,0)", Rawat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(dr));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(dr, Waktu.AddSeconds(3), "DR-UJI", "", pengganti));
        await db.ExecAsync("DELETE FROM billing WHERE no_rawat=@p0", Rawat);
        await db.ExecAsync("UPDATE rawat_jl_dr SET biaya_rawat=101 WHERE no_rawat=@p0", Rawat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(dr));
        await db.ExecAsync("UPDATE rawat_jl_dr SET biaya_rawat=100 WHERE no_rawat=@p0", Rawat);
        await db.ExecAsync("UPDATE set_akun_ralan SET Tindakan_Ralan=NULL");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(dr));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(dr, Waktu.AddSeconds(3), "DR-UJI", "", pengganti));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM rawat_jl_dr"));
        await db.ExecAsync("UPDATE set_akun_ralan SET Tindakan_Ralan='CR'");
        await service.UbahAsync(dr, Waktu.AddSeconds(3), "DR-UJI", "", pengganti);
        Assert.Equal(90, await db.CountAsync("SELECT biaya_rawat FROM rawat_jl_dr WHERE kd_jenis_prw='A-UMUM'"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(dr));
        dr = await service.MuatTindakanAsync(TindakanRalanJenis.Dokter, "A-UMUM", "DR-UJI", "", Waktu.AddSeconds(3));
        await service.UbahAsync(pr, Waktu.AddSeconds(4), "", "PET-UJI", tarifPr);
        await service.UbahAsync(drpr, Waktu.AddSeconds(5), "DR-UJI", "PET-UJI", tarifDrPr);
        pr = await service.MuatTindakanAsync(TindakanRalanJenis.Paramedis, "Z-UJI", "", "PET-UJI", Waktu.AddSeconds(4));
        drpr = await service.MuatTindakanAsync(TindakanRalanJenis.DokterParamedis, "Z-UJI", "DR-UJI", "PET-UJI", Waktu.AddSeconds(5));
        await service.HapusAsync(dr);
        await service.HapusAsync(pr);
        await service.HapusAsync(drpr);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM rawat_jl_dr"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM rawat_jl_pr"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM rawat_jl_drpr"));
        Assert.Equal(9, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
        var pembalik = await db.FirstAsync("SELECT SUM(debet) AS d,SUM(kredit) AS k FROM detailjurnal");
        Assert.Equal(2700, pembalik!.D("d"));
        Assert.Equal(2700, pembalik.D("k"));
        Assert.Equal(0, await db.CountAsync("SELECT SUM(debet-kredit) FROM detailjurnal WHERE kd_rek='JDR-D'"));
    }

    private static async Task VerifikasiLaporanFarmasiAsync(Db db, DatabaseSettings settings)
    {
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        try
        {
            await db.ExecAsync("INSERT INTO databarang(kode_brng,nama_brng) VALUES ('OBAT-UJI','Obat Sintetis')");
            await db.ExecAsync("INSERT INTO penjualan(nota_jual,tgl_jual,keterangan,ppn,kd_bangsal) VALUES ('NOTA-UJI',@p0,'Uji',0,'UJI')", Waktu.Date);
            await db.ExecAsync("INSERT INTO detailjual(nota_jual,kode_brng,jumlah,total) VALUES ('NOTA-UJI','OBAT-UJI',2,250)");
        }
        finally { await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=1"); }
        await using var context = SimrsGramedikaDbContextFactory.Create(settings);
        var farmasi = new FarmasiRepository(context);
        var item = Assert.Single(await farmasi.GetPenjualanAsync(DateOnly.FromDateTime(Waktu), DateOnly.FromDateTime(Waktu)));
        Assert.Equal("NOTA-UJI", item.NoKeluar);
        Assert.Equal("Obat Sintetis", item.NamaBarang);
        Assert.Equal(250, item.Total);
        await Assert.ThrowsAsync<InvalidOperationException>(() => farmasi.SimpanPenjualanAsync([], "Uji", "PET-UJI"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM pengeluaran_obat_bhp"));
    }

    private static async Task VerifikasiSoapAsync(Db db, DatabaseSettings settings)
    {
        var service = new RawatJalanService(settings);
        var author = new RawatJalanAkses(false, "PET-UJI", true);
        var other = new RawatJalanAkses(false, "LAIN-UJI", true);
        var waktu = (DateTime)(await db.ScalarAsync("SELECT NOW()"))!;
        var input = new PemeriksaanRalanInput(Rawat, waktu.Date, waktu.TimeOfDay, "", "", "", "", "", "", "", "", "Compos Mentis",
            "Keluhan ralan", "", "", "", "", "", "", "", "PET-UJI");
        await service.SimpanAsync(input, author);
        var ralan = Assert.Single(await service.PemeriksaanAsync(Rawat, author));
        Assert.Equal("Keluhan ralan", ralan.S("keluhan"));
        Assert.Single(await service.LimaTerakhirAsync(Rawat, "PET-UJI", "Keluhan", author));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LimaTerakhirAsync(Rawat, "PET-UJI", "", other));

        await db.ExecAsync("""
            INSERT INTO pemeriksaan_ranap(no_rawat,tgl_perawatan,jam_rawat,tensi,spo2,kesadaran,keluhan,penilaian,rtl,instruksi,evaluasi,nip)
            VALUES(@p0,@p1,@p2,'','','Compos Mentis','Keluhan ranap','','','','','PET-UJI')
            """, Rawat, waktu.Date, waktu.TimeOfDay);
        Assert.Equal("Keluhan ranap", Assert.Single(await service.LimaTerakhirAsync(Rawat, "PET-UJI", "ranap", author, true)).S("keluhan"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(input, input with { Keluhan = "Lain" }, other));
        await service.UbahAsync(input, input with { Keluhan = "Berubah" }, author);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(input, author));
        await service.HapusAsync(input with { Keluhan = "Berubah" }, author);
        Assert.Empty(await service.PemeriksaanAsync(Rawat, author));
    }

    private static async Task SeedAsync(Db db)
    {
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        try
        {
            await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('DR-UJI','Dokter Uji')");
            await db.ExecAsync("INSERT INTO petugas(nip,nama) VALUES ('PET-UJI','Petugas Uji')");
            await db.ExecAsync("INSERT INTO pegawai(nik,nama,stts_aktif) VALUES ('PET-UJI','Petugas Uji','AKTIF')");
            await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM-UJI','Pasien Sintetis')");
            await db.ExecAsync("""
                INSERT INTO reg_periksa(no_reg,no_rawat,tgl_registrasi,jam_reg,no_rkm_medis,biaya_reg,stts,stts_daftar,status_lanjut,kd_pj,kd_poli,status_bayar,status_poli)
                VALUES('001',@p0,@p1,'00:00:00','RM-UJI',0,'Belum','Lama','Ralan','UJI','UJ1','Belum Bayar','Lama')
                """, Rawat, Waktu.Date);
            await db.ExecAsync("INSERT INTO set_tarif(poli_ralan,cara_bayar_ralan) VALUES('Yes','Yes')");
            await db.ExecAsync("INSERT INTO kategori_perawatan(kd_kategori,nm_kategori) VALUES ('KU','Kategori Uji')");
            await db.ExecAsync("""
                INSERT INTO jns_perawatan(kd_jenis_prw,nm_perawatan,kd_kategori,kd_pj,kd_poli,status,total_byrdr,total_byrpr,total_byrdrpr,
                    tarif_tindakandr,tarif_tindakanpr,material,bhp,kso,menejemen)
                VALUES ('Z-UJI','Tarif spesifik','KU','UJI','UJ1','1',100,200,300,10,20,3,2,4,1),
                    ('A-UMUM','Tarif umum','KU','-','-','1',90,190,290,0,0,0,0,0,0),
                    ('POLI-LAIN','Poli lain','KU','UJI','UJ2','1',110,210,310,0,0,0,0,0,0),
                    ('PJ-LAIN','Penjamin lain','KU','XX','UJ1','1',120,220,320,0,0,0,0,0,0),
                    ('NONAKTIF','Nonaktif','KU','UJI','UJ1','0',130,230,330,0,0,0,0,0,0),
                    ('NOL','Nol','KU','UJI','UJ1','1',0,0,0,0,0,0,0,0,0)
                """);
            await db.ExecAsync("""
                INSERT INTO rekening(kd_rek,nm_rek)
                VALUES ('DB','Debit'),('CR','Kredit'),('JDR-D','Jasa dokter'),('JPR-D','Jasa petugas')
                """);
            await db.ExecAsync("""
                INSERT INTO set_akun_ralan(Suspen_Piutang_Tindakan_Ralan,Tindakan_Ralan,
                    Beban_Jasa_Medik_Dokter_Tindakan_Ralan,Utang_Jasa_Medik_Dokter_Tindakan_Ralan,
                    Beban_Jasa_Medik_Paramedis_Tindakan_Ralan,Utang_Jasa_Medik_Paramedis_Tindakan_Ralan,
                    Beban_KSO_Tindakan_Ralan,Utang_KSO_Tindakan_Ralan,Beban_Jasa_Sarana_Tindakan_Ralan,Utang_Jasa_Sarana_Tindakan_Ralan,
                    HPP_BHP_Tindakan_Ralan,Persediaan_BHP_Tindakan_Ralan,Beban_Jasa_Menejemen_Tindakan_Ralan,Utang_Jasa_Menejemen_Tindakan_Ralan)
                VALUES('DB','CR','JDR-D','CR','JPR-D','CR','DB','CR','DB','CR','DB','CR','DB','CR')
                """);
        }
        finally { await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=1"); }
    }
}
