using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Farmasi;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Tests;

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

        await using var context = SimrsKhanzaDbContextFactory.Create(settings);
        var kasir = new KasirRepository(context);
        var bill = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync("RJ-UJI"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRegistrasiRalanAsync(bill, "Tunai", new AuthenticatedUser()));
        await sql.ExecAsync("INSERT INTO resep_obat(no_resep,no_rawat,tgl_peresepan,status) VALUES ('RESEP-UJI','RJ-UJI',CURRENT_DATE,'ralan')");
        var openOrder = await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRegistrasiRalanAsync(bill, "Tunai", user));
        Assert.Contains("Kasir Khanza", openOrder.Message);
        await sql.ExecAsync("DELETE FROM resep_obat WHERE no_resep='RESEP-UJI'");
        await sql.ExecAsync("UPDATE set_akun_ralan SET Registrasi_Ralan=NULL");
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRegistrasiRalanAsync(bill, "Tunai", user));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM nota_jalan"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM billing"));
        Assert.Equal("Belum Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat='RJ-UJI'"));
        await sql.ExecAsync("UPDATE set_akun_ralan SET Registrasi_Ralan='REG'");
        var nomor = await kasir.BayarRegistrasiRalanAsync(bill, "Tunai", user);
        Assert.Contains("/RJ", nomor);
        Assert.Equal(15000, await sql.CountAsync("SELECT besar_bayar FROM detail_nota_jalan"));
        Assert.Equal(15000, await sql.CountAsync("SELECT kredit FROM detailjurnal WHERE kd_rek='REG'"));
        Assert.Equal(2, await sql.CountAsync("SELECT COUNT(*) FROM tagihan_sadewa"));
        Assert.Equal("Sudah Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat='RJ-UJI'"));
        var lunas = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync("RJ-UJI"));
        Assert.Equal(nomor, lunas.NoNota);
        Assert.True(lunas.RincianTersimpan);
        Assert.Equal(15000m, lunas.Total);
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRegistrasiRalanAsync(bill, "Tunai", user));
    }
}
