using SimrsGramedika.Config;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class ReturBeliTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_RETUR_BELI_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_retur_beli_fixture.";
        }
    }

    [FixtureFact]
    public async Task ReturMengurangiStokDanBatchMembuatJurnalDanRollbackSaatGagal()
    {
        var settings = new DatabaseSettings
        {
            Host = "127.0.0.1", Database = "simrs_retur_beli_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_RETUR_BELI_FIXTURE_PORT")!),
        };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var bangsal = "BG" + suffix[..3];
        var suplier = suffix[..5];
        var nip = "PT" + suffix;
        var sat = "S" + suffix[..3];
        var satBesar = "B" + suffix[..3];
        var jenis = "J" + suffix[..3];
        var barang = "OB" + suffix;
        var barangBatch = "BB" + suffix;
        var invoice = "PG" + DateTime.Today.ToString("yyyyMMdd") + suffix;
        var invoiceBatch = invoice + "B";
        var retur = "RB" + DateTime.Today.ToString("yyyyMMdd") + suffix;
        var returBatch = retur + "B";
        var returGagal = retur + "X";
        var rekRetur = "RT" + suffix;
        var rekKontra = "RK" + suffix;
        var rekBayar = "BY" + suffix;

        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES(@p0,'Gudang Retur Uji','1')", bangsal);
        await db.ExecAsync("INSERT INTO datasuplier(kode_suplier,nama_suplier) VALUES(@p0,'Suplier Retur Uji')", suplier);
        await db.ExecAsync("INSERT INTO petugas(nip,nama,email,status) VALUES(@p0,'Petugas Retur Uji','retur@example.invalid','1')", nip);
        await db.ExecAsync("INSERT INTO kodesatuan(kode_sat,satuan) VALUES(@p0,'Tablet'),(@p1,'Box')", sat, satBesar);
        await db.ExecAsync("INSERT INTO jenis(kdjns,nama,keterangan) VALUES(@p0,'Obat Retur Uji','Fixture')", jenis);
        await db.ExecAsync("""
            INSERT INTO databarang(kode_brng,nama_brng,kode_satbesar,kode_sat,dasar,h_beli,ralan,kelas1,kelas2,kelas3,utama,vip,vvip,beliluar,jualbebas,karyawan,kdjns,isi,kapasitas,status)
            VALUES(@p0,'Obat Nonbatch Uji',@p1,@p2,900,1000,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,@p3,10,0,'1'),
                  (@p4,'Obat Batch Uji',@p1,@p2,900,1000,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,@p3,10,0,'1')
            """, barang, satBesar, sat, jenis, barangBatch);
        await db.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES(@p0,'Retur Supplier'),(@p1,'Kontra Retur'),(@p2,'Bank')", rekRetur, rekKontra, rekBayar);
        await db.ExecAsync("INSERT INTO set_akun(Retur_Ke_Suplayer,Kontra_Retur_Ke_Suplayer) VALUES(@p0,@p1)", rekRetur, rekKontra);
        await db.ExecAsync("""
            INSERT INTO pembelian(no_faktur,kode_suplier,nip,tgl_beli,total1,potongan,total2,ppn,tagihan,kd_bangsal,kd_rek)
            VALUES(@p0,@p1,@p2,CURRENT_DATE(),3000,0,3000,0,3000,@p3,@p4),(@p5,@p1,@p2,CURRENT_DATE(),24000,0,24000,0,24000,@p3,@p4)
            """, invoice, suplier, nip, bangsal, rekBayar, invoiceBatch);
        await db.ExecAsync("""
            INSERT INTO detailbeli(no_faktur,kode_brng,kode_sat,jumlah,h_beli,subtotal,dis,besardis,total,no_batch,jumlah2)
            VALUES(@p0,@p1,@p2,3,1000,3000,0,0,3000,'',3),(@p3,@p4,@p5,2,12000,24000,0,0,24000,@p6,20)
            """, invoice, barang, sat, invoiceBatch, barangBatch, satBesar, "B" + suffix[..5]);
        await db.ExecAsync("INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES(@p0,@p1,8,'',''),(@p2,@p1,20,@p3,@p4)",
            barang, bangsal, barangBatch, "B" + suffix[..5], invoiceBatch);
        await db.ExecAsync("""
            INSERT INTO data_batch(no_batch,kode_brng,tgl_beli,tgl_kadaluarsa,asal,no_faktur,dasar,h_beli,ralan,kelas1,kelas2,kelas3,utama,vip,vvip,beliluar,jualbebas,karyawan,jumlahbeli,sisa)
            VALUES(@p0,@p1,CURRENT_DATE(),'2027-12-31','Pengadaan',@p2,900,12000,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,20,20)
            """, "B" + suffix[..5], barangBatch, invoiceBatch);

        var repo = new ReturBeliRepository(settings);
        var nonbatch = Assert.Single(await repo.BarangTersediaAsync(invoice));
        nonbatch.JumlahRetur = 2;
        await repo.SimpanAsync(new(retur, nip, DateTime.Today), [nonbatch], nip);
        Assert.Equal((6d, 2000d, 2000d, "Retur Beli", 8d, 6d), (
            Convert.ToDouble(await db.ScalarAsync("SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch='' AND no_faktur=''", barang, bangsal)),
            Convert.ToDouble(await db.ScalarAsync("SELECT total FROM detreturbeli WHERE no_retur_beli=@p0", retur)),
            Convert.ToDouble(await db.ScalarAsync("SELECT debet FROM detailjurnal WHERE kd_rek=@p0 AND no_jurnal=(SELECT no_jurnal FROM jurnal WHERE no_bukti=@p1)", rekRetur, retur)),
            await db.StrAsync("SELECT posisi FROM riwayat_barang_medis WHERE keterangan LIKE @p0 ORDER BY jam DESC LIMIT 1", $"{retur}%"),
            Convert.ToDouble(await db.ScalarAsync("SELECT stok_awal FROM riwayat_barang_medis WHERE keterangan LIKE @p0 ORDER BY jam DESC LIMIT 1", $"{retur}%")),
            Convert.ToDouble(await db.ScalarAsync("SELECT stok_akhir FROM riwayat_barang_medis WHERE keterangan LIKE @p0 ORDER BY jam DESC LIMIT 1", $"{retur}%"))));

        var sisa = Assert.Single(await repo.BarangTersediaAsync(invoice));
        Assert.Equal((1d, 6d), (sisa.SisaRetur, sisa.Stok));
        sisa.JumlahRetur = 2;
        await Assert.ThrowsAsync<ArgumentException>(() => repo.SimpanAsync(new(retur + "2", nip, DateTime.Today), [sisa], nip));

        var batchSettings = settings.With(batch: true);
        var batchRepo = new ReturBeliRepository(batchSettings);
        var batch = Assert.Single(await batchRepo.BarangTersediaAsync(invoiceBatch));
        Assert.Equal((20d, 1200d, "B" + suffix[..5]), (batch.SisaRetur, batch.HargaKecil, batch.NoBatch));
        batch.JumlahRetur = 5;
        await batchRepo.SimpanAsync(new(returBatch, nip, DateTime.Today), [batch], nip);
        Assert.Equal((15d, 15d, 5d), (
            Convert.ToDouble(await db.ScalarAsync("SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3", barangBatch, bangsal, batch.NoBatch, invoiceBatch)),
            Convert.ToDouble(await db.ScalarAsync("SELECT sisa FROM data_batch WHERE kode_brng=@p0 AND no_batch=@p1 AND no_faktur=@p2", barangBatch, batch.NoBatch, invoiceBatch)),
            Convert.ToDouble(await db.ScalarAsync("SELECT jml_retur2 FROM detreturbeli WHERE no_retur_beli=@p0", returBatch))));

        await db.ExecAsync("CREATE TRIGGER tolak_riwayat_retur BEFORE INSERT ON riwayat_barang_medis FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='fixture rollback'");
        var rollbackItem = Assert.Single(await batchRepo.BarangTersediaAsync(invoiceBatch));
        rollbackItem.JumlahRetur = 1;
        try { await Assert.ThrowsAnyAsync<Exception>(() => batchRepo.SimpanAsync(new(returGagal, nip, DateTime.Today), [rollbackItem], nip)); }
        finally { await db.ExecAsync("DROP TRIGGER tolak_riwayat_retur"); }
        Assert.Equal((0L, 15d, 15d), (
            await db.CountAsync("SELECT COUNT(*) FROM returbeli WHERE no_retur_beli=@p0", returGagal),
            Convert.ToDouble(await db.ScalarAsync("SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3", barangBatch, bangsal, batch.NoBatch, invoiceBatch)),
            Convert.ToDouble(await db.ScalarAsync("SELECT sisa FROM data_batch WHERE kode_brng=@p0 AND no_batch=@p1 AND no_faktur=@p2", barangBatch, batch.NoBatch, invoiceBatch))));

        await batchRepo.HapusAsync(returBatch, nip);
        Assert.Equal((20d, 20d, 0L, 0d), (
            Convert.ToDouble(await db.ScalarAsync("SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3", barangBatch, bangsal, batch.NoBatch, invoiceBatch)),
            Convert.ToDouble(await db.ScalarAsync("SELECT sisa FROM data_batch WHERE kode_brng=@p0 AND no_batch=@p1 AND no_faktur=@p2", barangBatch, batch.NoBatch, invoiceBatch)),
            await db.CountAsync("SELECT COUNT(*) FROM returbeli WHERE no_retur_beli=@p0", returBatch),
            Convert.ToDouble(await db.ScalarAsync("""
                SELECT COALESCE(SUM(d.debet-d.kredit),0) FROM detailjurnal d JOIN jurnal j ON j.no_jurnal=d.no_jurnal
                WHERE j.no_bukti=@p0 AND d.kd_rek=@p1
                """, returBatch, rekRetur))));
    }
}
