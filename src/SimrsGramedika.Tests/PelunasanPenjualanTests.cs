using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class PelunasanPenjualanTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_KASIR_FARMASI_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_kasir_farmasi_fixture.";
        }
    }

    [FixtureFact]
    public async Task PenjualanDanPelunasanMempostingAtomik()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_kasir_farmasi_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_KASIR_FARMASI_FIXTURE_PORT")!) };
        await using var sql = await Db.OpenAsync(settings);
        await sql.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        await sql.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM','Pasien Sintetis')");
        await sql.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('F.A','Farmasi Uji','1')");
        await sql.ExecAsync("INSERT INTO petugas(nip,nama,status) VALUES ('PT','Petugas Uji','1')");
        await sql.ExecAsync("INSERT INTO reg_periksa(no_rawat,no_rkm_medis,status_lanjut,stts,status_bayar,biaya_reg) VALUES ('RJ-UJI','RM','Ralan','Belum','Belum Bayar',15000)");
        await sql.ExecAsync("INSERT INTO databarang(kode_brng,nama_brng,status,jualbebas,dasar) VALUES ('OB','Obat Uji','1',5000,3000)");
        await sql.ExecAsync("INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES ('OB','F.A',6,'','')");
        await sql.ExecAsync("INSERT INTO set_nota(verifikasi_penjualan_di_kasir,tampilkan_ppnobat_ralan) VALUES ('No','No')");
        await sql.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES ('KAS','Kas'),('PEND','Penjualan'),('HPP','HPP'),('STOK','Stok'),('REG','Registrasi')");
        await sql.ExecAsync("INSERT INTO akun_bayar(nama_bayar,kd_rek,ppn) VALUES ('Tunai','KAS',0)");
        await sql.ExecAsync("INSERT INTO set_akun(Penjualan_Obat,HPP_Obat_Jual_Bebas,Persediaan_Obat_Jual_Bebas) VALUES ('PEND','HPP','STOK')");
        await sql.ExecAsync("INSERT INTO set_akun_ralan(Registrasi_Ralan) VALUES ('REG')");
        var user = new AuthenticatedUser { UserId = "PT", Permissions = new HashSet<string> { "kasir_ralan", "penjualan_obat" } };
        var sale = new PenjualanTunaiService(settings, user);
        var item = new PenjualanLineInput("OB", 2, 5000, 0, 0, null, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sale.SimpanAsync([item], "Uji", "PT", "RM", "F.A", "Tunai", 9999));
        Assert.Equal(6, await sql.CountAsync("SELECT stok FROM gudangbarang"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM penjualan"));
        var nota = await sale.SimpanAsync([item], "Uji", "PT", "RM", "F.A", "Tunai", 10000);
        Assert.StartsWith("PJ", nota);
        Assert.Equal(4, await sql.CountAsync("SELECT stok FROM gudangbarang"));
        Assert.Equal(2, await sql.CountAsync("SELECT keluar FROM riwayat_barang_medis"));
        Assert.Equal(10000, await sql.CountAsync("SELECT total FROM detailjual"));
        Assert.Equal(6000, await sql.CountAsync("SELECT debet FROM detailjurnal WHERE kd_rek='HPP'"));
        Assert.Equal(6000, await sql.CountAsync("SELECT kredit FROM detailjurnal WHERE kd_rek='STOK'"));
        await sql.ExecAsync("UPDATE set_akun SET Penjualan_Obat=''");
        await Assert.ThrowsAsync<InvalidOperationException>(() => sale.SimpanAsync([item], "Uji", "PT", "RM", "F.A", "Tunai", 10000));
        Assert.Equal(4, await sql.CountAsync("SELECT stok FROM gudangbarang"));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM penjualan"));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM riwayat_barang_medis"));

        await using var context = SimrsGramedikaDbContextFactory.Create(settings);
        var kasir = new KasirRepository(context);
        var bill = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync("RJ-UJI"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(bill, "Tunai", new AuthenticatedUser()));
        await sql.ExecAsync("INSERT INTO resep_obat(no_resep,no_rawat,tgl_peresepan,status) VALUES ('RESEP-UJI','RJ-UJI',CURRENT_DATE,'ralan')");
        var openOrder = await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(bill, "Tunai", user));
        Assert.Contains("masih menunggu", openOrder.Message);
        await sql.ExecAsync("DELETE FROM resep_obat WHERE no_resep='RESEP-UJI'");
        await sql.ExecAsync("UPDATE set_akun_ralan SET Registrasi_Ralan=NULL");
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(bill, "Tunai", user));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM nota_jalan"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM billing"));
        Assert.Equal("Belum Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat='RJ-UJI'"));
        await sql.ExecAsync("UPDATE set_akun_ralan SET Registrasi_Ralan='REG'");
        await sql.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES ('TIN','Tindakan'),('SUSP','Suspen'),('LAB','Lab'),('RAD','Radiologi'),('RESEP','Pemberian obat'),('PPN','PPN')");
        await sql.ExecAsync("UPDATE set_akun_ralan SET Suspen_Piutang_Tindakan_Ralan='SUSP',Tindakan_Ralan='TIN',Laborat_Ralan='LAB',Radiologi_Ralan='RAD',Obat_Ralan='RESEP'");
        await sql.ExecAsync("UPDATE set_nota SET tampilkan_ppnobat_ralan='Yes'");
        await sql.ExecAsync("UPDATE set_akun SET PPN_Keluaran='PPN'");
        await sql.ExecAsync("INSERT INTO jns_perawatan(kd_jenis_prw,nm_perawatan,total_byrpr) VALUES ('T1','Tindakan Uji',99999)");
        await sql.ExecAsync("INSERT INTO rawat_jl_dr(no_rawat,kd_jenis_prw,tgl_perawatan,biaya_rawat) VALUES ('RJ-UJI','T1',CURRENT_DATE,4000)");
        await sql.ExecAsync("INSERT INTO rawat_jl_pr(no_rawat,kd_jenis_prw,tgl_perawatan,biaya_rawat) VALUES ('RJ-UJI','T1',CURRENT_DATE,2000)");
        await sql.ExecAsync("INSERT INTO rawat_jl_drpr(no_rawat,kd_jenis_prw,tgl_perawatan,biaya_rawat) VALUES ('RJ-UJI','T1',CURRENT_DATE,3000)");
        await sql.ExecAsync("INSERT INTO jurnal(no_jurnal,no_bukti,tgl_jurnal,jam_jurnal,jenis,keterangan) VALUES ('UJI-TIN','RJ-UJI',CURRENT_DATE,'12:00:00','U','TINDAKAN RAWAT JALAN')");
        await sql.ExecAsync("INSERT INTO detailjurnal VALUES ('UJI-TIN','SUSP',9000,0),('UJI-TIN','TIN',0,9000)");
        await sql.ExecAsync("INSERT INTO jns_perawatan_lab(kd_jenis_prw,nm_perawatan) VALUES ('L1','Darah'),('L2','Belum diperiksa')");
        await sql.ExecAsync("INSERT INTO periksa_lab(no_rawat,kd_jenis_prw,tgl_periksa,jam,biaya,status,kategori) VALUES ('RJ-UJI','L1',CURRENT_DATE,'09:00:00',5000,'Ralan','PK'),('RJ-UJI','L1',CURRENT_DATE,'10:00:00',5000,'Ralan','PK')");
        await sql.ExecAsync("INSERT INTO detail_periksa_lab(no_rawat,kd_jenis_prw,tgl_periksa,jam,id_template,biaya_item) VALUES ('RJ-UJI','L1',CURRENT_DATE,'09:00:00',1,500),('RJ-UJI','L1',CURRENT_DATE,'10:00:00',1,700)");
        await sql.ExecAsync("INSERT INTO jns_perawatan_radiologi(kd_jenis_prw,nm_perawatan) VALUES ('R1','Radiologi Uji')");
        await sql.ExecAsync("INSERT INTO periksa_radiologi(no_rawat,kd_jenis_prw,tgl_periksa,jam,biaya,status) VALUES ('RJ-UJI','R1',CURRENT_DATE,'08:00:00',8000,'Ralan')");
        await sql.ExecAsync("INSERT INTO detail_pemberian_obat(no_rawat,kode_brng,tgl_perawatan,jam,biaya_obat,jml,embalase,tuslah,total,status,no_batch,no_faktur) VALUES ('RJ-UJI','OB',CURRENT_DATE,'08:00:00',5000,1,200,300,5500,'Ralan','','')");
        await sql.ExecAsync("INSERT INTO permintaan_lab(noorder,no_rawat,tgl_permintaan,tgl_hasil,status) VALUES ('PENDING','RJ-UJI',CURRENT_DATE,'0000-00-00','ralan'),('SELESAI','RJ-UJI',CURRENT_DATE,CURRENT_DATE,'ralan')");
        await sql.ExecAsync("INSERT INTO permintaan_pemeriksaan_lab(noorder,kd_jenis_prw) VALUES ('PENDING','L2'),('SELESAI','L1')");
        var menunggu = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync("RJ-UJI"));
        Assert.Contains("masih menunggu", menunggu.AlasanBelumBisaBayar);
        Assert.DoesNotContain(menunggu.Rincian, r => r.Nama == "Belum diperiksa");
        Assert.Equal(49305m, menunggu.Total);
        Assert.Equal(11200m, menunggu.Rincian.Where(r => r.Jenis == "Laborat").Sum(r => r.Total));
        Assert.Equal(2000m, Assert.Single(menunggu.Rincian, r => r.Jenis == "Ralan Paramedis").Total);
        Assert.Equal(5500m, Assert.Single(menunggu.Rincian, r => r.Nama == "Obat Uji").Total);
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(menunggu, "Tunai", user));
        await sql.ExecAsync("DELETE FROM permintaan_pemeriksaan_lab WHERE noorder='PENDING'");
        await sql.ExecAsync("DELETE FROM permintaan_lab WHERE noorder='PENDING'");
        var tagihan = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync("RJ-UJI"));
        Assert.Null(tagihan.AlasanBelumBisaBayar);
        Assert.Equal(menunggu.Rincian, tagihan.Rincian);
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(bill, "Tunai", user));
        await sql.ExecAsync("UPDATE set_akun_ralan SET Radiologi_Ralan=NULL");
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(tagihan, "Tunai", user));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM billing"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM nota_jalan"));
        await sql.ExecAsync("UPDATE set_akun_ralan SET Radiologi_Ralan='RAD'");
        Assert.Null(await kasir.MuatKuitansiAsync("RJ-UJI"));
        await sql.ExecAsync("CREATE TRIGGER uji_gagal_detail_jurnal BEFORE INSERT ON detailjurnal FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Jurnal uji gagal'");
        try
        {
            var gagal = await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() => kasir.BayarRalanAsync(tagihan, "Tunai", user));
            Assert.Contains("Jurnal uji gagal", gagal.Message);
            Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM nota_jalan"));
            Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM detail_nota_jalan"));
            Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM billing"));
            Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM jurnal WHERE no_bukti='RJ-UJI'"));
            Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM tagihan_sadewa WHERE no_nota='RJ-UJI'"));
            Assert.Equal("Belum Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat='RJ-UJI'"));
        }
        finally
        {
            await sql.ExecAsync("DROP TRIGGER uji_gagal_detail_jurnal");
        }
        async Task<string?> BayarDariKasirAsync()
        {
            await using var koneksiKasir = SimrsGramedikaDbContextFactory.Create(settings);
            try
            {
                return await new KasirRepository(koneksiKasir).BayarRalanAsync(tagihan, "Tunai", user);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("tagihan sudah tercatat"))
            {
                return null;
            }
        }

        var hasil = await Task.WhenAll(BayarDariKasirAsync(), BayarDariKasirAsync());
        var nomor = Assert.Single(hasil.OfType<string>());
        Assert.Contains("/RJ", nomor);
        Assert.Equal(49305, await sql.CountAsync("SELECT besar_bayar FROM detail_nota_jalan"));
        Assert.Equal(15000, await sql.CountAsync("SELECT kredit FROM detailjurnal WHERE kd_rek='REG'"));
        Assert.Equal(2, await sql.CountAsync("SELECT COUNT(*) FROM tagihan_sadewa"));
        Assert.Equal("Sudah Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat='RJ-UJI'"));
        var lunas = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync("RJ-UJI"));
        Assert.Equal(nomor, lunas.NoNota);
        Assert.True(lunas.RincianTersimpan);
        Assert.Equal(49305m, lunas.Total);
        await using var koneksiPenunjang = SimrsGramedikaDbContextFactory.Create(settings);
        var labLocked = await Assert.ThrowsAsync<InvalidOperationException>(() => new LabPeriksaRepository(koneksiPenunjang)
            .DeleteAsync(LabCategory.PK, new("RJ-UJI", "2026-11-26", "09:00:00"), "L1"));
        Assert.Contains("billing sudah terverifikasi", labLocked.Message);
        var radLocked = await Assert.ThrowsAsync<InvalidOperationException>(() => new RadiologiPeriksaRepository(koneksiPenunjang)
            .DeleteAsync(new("RJ-UJI", "2026-11-26", "08:00:00")));
        Assert.Contains("billing sudah terverifikasi", radLocked.Message);
        Assert.Equal(tagihan.Rincian, lunas.Rincian);
        var kuitansi = Assert.IsType<KasirKuitansi>(await kasir.MuatKuitansiAsync("RJ-UJI"));
        var cetakUlang = Assert.IsType<KasirKuitansi>(await kasir.MuatKuitansiAsync("RJ-UJI"));
        Assert.Equal(nomor, cetakUlang.Tagihan.NoNota);
        Assert.Equal(kuitansi.WaktuBayar, cetakUlang.WaktuBayar);
        Assert.Equal(kuitansi.Tagihan.Rincian, cetakUlang.Tagihan.Rincian);
        Assert.Equal(new KasirPembayaran("Tunai", 0, 49305), Assert.Single(cetakUlang.Pembayaran));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM nota_jalan"));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM detail_nota_jalan"));
        Assert.Equal(2, await sql.CountAsync("SELECT COUNT(*) FROM jurnal WHERE no_bukti='RJ-UJI'"));
        Assert.Equal(9000, await sql.CountAsync("SELECT SUM(kredit) FROM detailjurnal WHERE kd_rek='TIN'"));
        Assert.Equal(9000, await sql.CountAsync("SELECT SUM(kredit) FROM detailjurnal WHERE kd_rek='SUSP'"));
        Assert.Equal(11200, await sql.CountAsync("SELECT SUM(kredit) FROM detailjurnal WHERE kd_rek='LAB'"));
        Assert.Equal(605, await sql.CountAsync("SELECT SUM(kredit) FROM detailjurnal WHERE kd_rek='PPN'"));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM penjualan"));
        Assert.Equal(4, await sql.CountAsync("SELECT stok FROM gudangbarang"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRalanAsync(bill, "Tunai", user));
    }
}
