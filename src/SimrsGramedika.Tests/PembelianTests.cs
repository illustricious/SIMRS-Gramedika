using SimrsGramedika.Config;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class PembelianTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_PEMBELIAN_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_pembelian_fixture.";
        }
    }

    [FixtureFact]
    public async Task PembelianMencatatStokBatchJurnalDanRollback()
    {
        var settings = new DatabaseSettings
        {
            Host = "127.0.0.1", Database = "simrs_pembelian_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_PEMBELIAN_FIXTURE_PORT")!),
        };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var no = $"PG{DateTime.Today:yyyyMMdd}{suffix}";
        var noBatch = no + "B";
        var noGagal = no + "X";
        var bangsal = "BG" + suffix[..3];
        var barang = "OB" + suffix;
        var rekeningPersediaan = "IV" + suffix;
        var rekeningPpn = "VT" + suffix;
        var rekeningBayar = "BK" + suffix;
        var suplier = suffix[..5];
        var nip = "PT" + suffix;
        var kodeSatuan = "S" + suffix[..3];
        var kodeJenis = "J" + suffix[..3];
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES(@p0,'Gudang Sintetis','1')", bangsal);
        await db.ExecAsync("INSERT INTO datasuplier(kode_suplier,nama_suplier) VALUES(@p0,'Suplier Sintetis')", suplier);
        await db.ExecAsync("INSERT INTO petugas(nip,nama,email,status) VALUES(@p0,'Petugas Sintetis','uji@example.invalid','1')", nip);
        await db.ExecAsync("INSERT INTO kodesatuan(kode_sat,satuan) VALUES(@p0,'Tablet'),(@p1,'Box')", kodeSatuan, "B" + suffix[..3]);
        await db.ExecAsync("INSERT INTO jenis(kdjns,nama,keterangan) VALUES(@p0,'Obat Sintetis','Untuk fixture')", kodeJenis);
        await db.ExecAsync("""
            INSERT INTO databarang(kode_brng,nama_brng,kode_satbesar,kode_sat,dasar,h_beli,ralan,kelas1,kelas2,kelas3,utama,vip,vvip,beliluar,jualbebas,karyawan,kdjns,isi,kapasitas,status)
            VALUES(@p0,'Obat Sintetis',@p1,@p2,800,900,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,@p3,10,0,'1')
            """, barang, "B" + suffix[..3], kodeSatuan, kodeJenis);
        await db.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES(@p0,'Persediaan'),(@p1,'PPN Masukan'),(@p2,'Bank')",
            rekeningPersediaan, rekeningPpn, rekeningBayar);
        await db.ExecAsync("INSERT INTO akun_bayar(nama_bayar,kd_rek,ppn) VALUES('Transfer Sintetis',@p0,0)", rekeningBayar);
        await db.ExecAsync("INSERT INTO set_akun(Pengadaan_Obat,PPN_Masukan) VALUES(@p0,@p1)", rekeningPersediaan, rekeningPpn);
        await db.ExecAsync("INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES(@p0,@p1,5,'','')", barang, bangsal);
        await db.ExecAsync("INSERT INTO set_harga_obat(setharga,hargadasar,ppn) VALUES('Umum','Harga Beli','No')");
        await db.ExecAsync("INSERT INTO setpenjualanumum VALUES(20,25,25,25,20,30,35,20,30,10)");

        var item = Assert.Single(await new PembelianRepository(settings).CariBarangAsync(barang));
        item.Jumlah = 3;
        item.Harga = 1000;
        item.DiskonPersen = 10;
        var header = new PembelianHeader(no, suplier, nip, DateTime.Today, bangsal, "Gudang Sintetis", "Transfer Sintetis", 11);
        await new PembelianRepository(settings).SimpanAsync(header, [item], true, nip);

        Assert.Equal((2700d, 297d, 2997d), (
            Convert.ToDouble(await db.ScalarAsync("SELECT total2 FROM pembelian WHERE no_faktur=@p0", no)),
            Convert.ToDouble(await db.ScalarAsync("SELECT ppn FROM pembelian WHERE no_faktur=@p0", no)),
            Convert.ToDouble(await db.ScalarAsync("SELECT tagihan FROM pembelian WHERE no_faktur=@p0", no))));
        Assert.Equal(3d, Convert.ToDouble(await db.ScalarAsync("SELECT jumlah FROM detailbeli WHERE no_faktur=@p0", no)));
        Assert.Equal(8d, Convert.ToDouble(await db.ScalarAsync("SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch='' AND no_faktur=''", barang, bangsal)));
        Assert.Equal((2700d, 297d, 2997d), (
            Convert.ToDouble(await db.ScalarAsync("SELECT debet FROM detailjurnal WHERE kd_rek=@p0 AND no_jurnal=(SELECT no_jurnal FROM jurnal WHERE no_bukti=@p1) ORDER BY debet DESC LIMIT 1", rekeningPersediaan, no)),
            Convert.ToDouble(await db.ScalarAsync("SELECT debet FROM detailjurnal WHERE kd_rek=@p0 AND no_jurnal=(SELECT no_jurnal FROM jurnal WHERE no_bukti=@p1)", rekeningPpn, no)),
            Convert.ToDouble(await db.ScalarAsync("SELECT kredit FROM detailjurnal WHERE kd_rek=@p0 AND no_jurnal=(SELECT no_jurnal FROM jurnal WHERE no_bukti=@p1)", rekeningBayar, no))));
        Assert.Equal(("Pengadaan", "Simpan", 5d, 8d), (
            await db.StrAsync("SELECT posisi FROM riwayat_barang_medis WHERE no_faktur='' AND kode_brng=@p0 AND keterangan LIKE @p1 ORDER BY tanggal DESC,jam DESC LIMIT 1", barang, $"{no}%"),
            await db.StrAsync("SELECT status FROM riwayat_barang_medis WHERE no_faktur='' AND kode_brng=@p0 AND keterangan LIKE @p1 ORDER BY tanggal DESC,jam DESC LIMIT 1", barang, $"{no}%"),
            Convert.ToDouble(await db.ScalarAsync("SELECT stok_awal FROM riwayat_barang_medis WHERE no_faktur='' AND kode_brng=@p0 AND keterangan LIKE @p1 ORDER BY tanggal DESC,jam DESC LIMIT 1", barang, $"{no}%")),
            Convert.ToDouble(await db.ScalarAsync("SELECT stok_akhir FROM riwayat_barang_medis WHERE no_faktur='' AND kode_brng=@p0 AND keterangan LIKE @p1 ORDER BY tanggal DESC,jam DESC LIMIT 1", barang, $"{no}%"))));
        Assert.Equal(900d, Convert.ToDouble(await db.ScalarAsync("SELECT h_beli FROM databarang WHERE kode_brng=@p0", barang)));

        // Batch purchase stores its own expiration and price snapshot and can update the master prices.
        var batchSettings = settings.With(batch: true);
        var batchItem = Assert.Single(await new PembelianRepository(batchSettings).CariBarangAsync(barang));
        batchItem.Jumlah = 2;
        batchItem.PakaiSatuanBesar = true;
        batchItem.Harga = 12000;
        batchItem.NoBatch = "B" + suffix;
        batchItem.Kadaluarsa = "31/12/2027";
        batchItem.UbahHarga = true;
        var batchHeader = header with { NoFaktur = noBatch, PpnPersen = 0 };
        await new PembelianRepository(batchSettings).SimpanAsync(batchHeader, [batchItem], true, nip);
        Assert.Equal((20d, 20d, "Pengadaan", new DateTime(2027, 12, 31)), (
            Convert.ToDouble(await db.ScalarAsync("SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3", barang, bangsal, batchItem.NoBatch, noBatch)),
            Convert.ToDouble(await db.ScalarAsync("SELECT sisa FROM data_batch WHERE kode_brng=@p0 AND no_batch=@p1 AND no_faktur=@p2", barang, batchItem.NoBatch, noBatch)),
            await db.StrAsync("SELECT asal FROM data_batch WHERE kode_brng=@p0 AND no_batch=@p1 AND no_faktur=@p2", barang, batchItem.NoBatch, noBatch),
            Convert.ToDateTime(await db.ScalarAsync("SELECT tgl_kadaluarsa FROM data_batch WHERE kode_brng=@p0 AND no_batch=@p1 AND no_faktur=@p2", barang, batchItem.NoBatch, noBatch))));
        Assert.Equal(1200d, Convert.ToDouble(await db.ScalarAsync("SELECT h_beli FROM databarang WHERE kode_brng=@p0", barang)));

        // A storage failure after the header and detail have been written must roll back the whole purchase.
        await db.ExecAsync("CREATE TRIGGER tolak_riwayat_pembelian BEFORE INSERT ON riwayat_barang_medis FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='fixture rollback'");
        var gagal = new PembelianLine
        {
            Kode = item.Kode, Nama = item.Nama, KdJns = item.KdJns, SatKecil = item.SatKecil, NamaSatKecil = item.NamaSatKecil,
            SatBesar = item.SatBesar, NamaSatBesar = item.NamaSatBesar, Isi = item.Isi, HargaLama = item.HargaLama, Jumlah = 1, Harga = 1000,
        };
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => new PembelianRepository(settings).SimpanAsync(
                header with { NoFaktur = noGagal, PpnPersen = 0 }, [gagal], true, nip));
        }
        finally { await db.ExecAsync("DROP TRIGGER tolak_riwayat_pembelian"); }
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM pembelian WHERE no_faktur=@p0", noGagal));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detailbeli WHERE no_faktur=@p0", noGagal));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM jurnal WHERE no_bukti=@p0", noGagal));
        Assert.Equal(8d, Convert.ToDouble(await db.ScalarAsync("SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch='' AND no_faktur=''", barang, bangsal)));
    }
}
