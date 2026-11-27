using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class OperasiTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_OPERASI_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_operasi_fixture.";
        }
    }

    [FixtureFact]
    public async Task InputHapusDanPelunasanMenjagaJurnalOperasi()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_operasi_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_OPERASI_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        var kemarin = DateTime.Today.AddDays(-1);
        const string rawat = "OP/000001";
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM','Pasien Uji')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Penjamin Uji')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('-','-'),('DR','Dokter Uji')");
        await db.ExecAsync("INSERT INTO petugas(nip,nama) VALUES ('-','-'),('PR','Petugas Uji')");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('BG','Bangsal Uji','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status,trf_kamar) VALUES ('KM','BG','Kelas 1','ISI',100000)");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_pj,biaya_reg,stts,status_lanjut,status_bayar)
            VALUES (@p0,'RM','PJ',0,'Belum','Ranap','Belum Bayar')
            """, rawat);
        await db.ExecAsync("INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang,trf_kamar) VALUES (@p0,'KM',@p1,'08:00:00','-',100000)", rawat, kemarin);
        await db.ExecAsync("INSERT INTO set_jam_minimal VALUES (1,'No',0,'No',0,'No','No','Yes')");
        await db.ExecAsync("INSERT INTO set_nota(rincianoperasi,centangobatranap,tampilkan_ppnobat_ranap,tampilkan_administrasi_di_billingranap) VALUES ('Yes','Yes','No','No')");
        await db.ExecAsync("INSERT INTO set_tarif(cara_bayar_operasi,kelas_operasi) VALUES ('Yes','Yes')");
        await db.ExecAsync("""
            INSERT INTO paket_operasi(kode_paket,nm_perawatan,kategori,operator1,asisten_operator1,alat,kd_pj,status,kelas) VALUES
                ('P1','Paket Uji','Operasi',100000,20000,30000,'PJ','1','Kelas 1'),
                ('P2','Paket Kelas Lain','Operasi',1,0,0,'PJ','1','Kelas 2'),
                ('P3','Paket Nonaktif','Operasi',1,0,0,'PJ','0','Kelas 1')
            """);
        await db.ExecAsync("INSERT INTO obatbhp_ok(kd_obat,nm_obat,kode_sat,hargasatuan) VALUES ('OB','Benang Uji','-',5000)");
        string[] rekening = ["KAS", "KMR", "SUS", "OPR", "BBD", "UTD", "BBP", "UTP", "HPP", "STK"];
        foreach (var kode in rekening) await db.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES (@p0,@p0)", kode);
        await db.ExecAsync("INSERT INTO akun_bayar VALUES ('Tunai Uji','KAS',0)");
        await db.ExecAsync("""
            INSERT INTO set_akun_ranap(Kamar_Inap,Suspen_Piutang_Operasi_Ranap,Operasi_Ranap,
                Beban_Jasa_Medik_Dokter_Operasi_Ranap,Utang_Jasa_Medik_Dokter_Operasi_Ranap,
                Beban_Jasa_Medik_Paramedis_Operasi_Ranap,Utang_Jasa_Medik_Paramedis_Operasi_Ranap,HPP_Obat_Operasi_Ranap)
            VALUES ('KMR','SUS','OPR','BBD','UTD','BBP','UTP','HPP')
            """);
        await db.ExecAsync("INSERT INTO set_akun_ranap2(Persediaan_Obat_Kamar_Operasi_Ranap) VALUES ('STK')");

        var user = new AuthenticatedUser { UserId = "uji", Permissions = new HashSet<string> { "operasi", "billing_ranap" } };
        var service = new OperasiService(settings, rawat, user);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new OperasiService(settings, rawat, new()).DaftarAsync());
        var paket = Assert.Single(await service.CariPaketAsync(""));
        Assert.Equal(("P1", 150000d), (paket.Kode, paket.Total));
        var obat = Assert.Single(await service.CariObatAsync("Benang"));
        var mulai = kemarin.AddHours(9);
        var input = new InputOperasi(mulai, mulai.AddHours(1), "Umum", "Sedang",
            new Dictionary<string, string> { ["operator1"] = "DR", ["asisten_operator1"] = "PR" },
            [paket], [new(obat, 2)],
            new("Pre uji", "Post uji", "Jaringan uji", default, true, "IMP-1", "Laporan uji"));

        await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(input with { Selesai = mulai }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(input with { JenisAnestesi = "" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(input with { Obat = [new(obat, 0)] }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(input with { Tim = new Dictionary<string, string> { ["operator1"] = "PR" } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(input with { Obat = [new(obat with { Harga = 1 }, 1)] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(input with
        {
            Paket = [paket with { Biaya = paket.Biaya.Select(b => b * 2).ToList() }],
        }));
        Assert.Equal(0, await db.CountAsync("SELECT (SELECT COUNT(*) FROM operasi)+(SELECT COUNT(*) FROM jurnal)+(SELECT COUNT(*) FROM beri_obat_operasi)"));

        await service.SimpanAsync(input);
        var baris = (await db.FirstAsync("SELECT * FROM operasi"))!;
        Assert.Equal(("DR", "PR", "-", "-", "Ranap", "Umum", "Sedang"), (baris.S("operator1"), baris.S("asisten_operator1"),
            baris.S("bidan"), baris.S("dokter_umum"), baris.S("status"), baris.S("jenis_anasthesi"), baris.S("kategori")));
        Assert.Equal((100000d, 20000d, 30000d), (baris.D("biayaoperator1"), baris.D("biayaasisten_operator1"), baris.D("biayaalat")));
        Assert.Equal(10000, await db.CountAsync("SELECT SUM(hargasatuan*jumlah) FROM beri_obat_operasi"));
        Task<long> Saldo(string kode) => db.CountAsync("SELECT COALESCE(SUM(debet-kredit),0) FROM detailjurnal WHERE kd_rek=@p0", kode);
        Assert.Equal((160000L, -160000L, 100000L, -100000L), (await Saldo("SUS"), await Saldo("OPR"), await Saldo("BBD"), await Saldo("UTD")));
        Assert.Equal((20000L, -20000L, 10000L, -10000L), (await Saldo("BBP"), await Saldo("UTP"), await Saldo("HPP"), await Saldo("STK")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(input));

        var laporan = Assert.IsType<LaporanOperasi>(await service.MuatLaporanAsync(mulai));
        Assert.Equal(("Laporan uji", true, mulai.AddHours(1)), (laporan.Isi, laporan.PermintaanPa, laporan.Selesai));
        await service.SimpanLaporanAsync(mulai, laporan with { Isi = "Laporan diperbaiki", PermintaanPa = false });
        Assert.Equal("Laporan diperbaiki", (await service.MuatLaporanAsync(mulai))!.Isi);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanLaporanAsync(mulai.AddMinutes(1), laporan));

        // Operasi kedua dihapus kembali: seluruh jurnalnya terbalik, operasi pertama tetap.
        var kedua = mulai.AddHours(3);
        await service.SimpanAsync(input with { Mulai = kedua, Selesai = kedua.AddHours(1) });
        var catatan = (await service.DaftarAsync()).Single(c => c.Waktu == kedua);
        Assert.Equal(("Paket Uji", 150000d, 10000d), (catatan.Paket, catatan.BiayaTindakan, catatan.BiayaObat));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(catatan with { BiayaObat = 1 }));
        await service.HapusAsync(catatan);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(catatan));
        foreach (var tabel in new[] { "operasi", "beri_obat_operasi", "laporan_operasi" })
            Assert.Equal(1, await db.CountAsync($"SELECT COUNT(*) FROM {tabel}"));
        Assert.Equal((160000L, 100000L, 10000L), (await Saldo("SUS"), await Saldo("BBD"), await Saldo("HPP")));

        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
            await new RawatInapRepository(ctx).DischargeAsync(new(rawat, "KM", DateOnly.FromDateTime(kemarin), new TimeOnly(8, 0)),
                StatusPulang.Sehat, "Uji", waktuKeluar: kemarin.AddHours(20));

        async Task<KasirBillItem> TagihanAsync()
        {
            await using var ctx = SimrsGramedikaDbContextFactory.Create(settings);
            return (await new KasirRepository(ctx).GetBillAsync(rawat))!;
        }
        await db.ExecAsync("UPDATE set_nota SET centangobatranap='No'");
        Assert.NotNull((await TagihanAsync()).AlasanBelumBisaBayar);
        await db.ExecAsync("UPDATE set_nota SET centangobatranap='Yes',rincianoperasi='No'");
        Assert.Equal(150000m, Assert.Single((await TagihanAsync()).Rincian, r => r.Jenis == "Operasi").Total);
        await db.ExecAsync("UPDATE set_nota SET rincianoperasi='Yes'");
        var tagihan = await TagihanAsync();
        Assert.Null(tagihan.AlasanBelumBisaBayar);
        Assert.Equal([("Paket Uji · Operator 1", 100000m), ("Paket Uji · Asisten Operator 1", 20000m), ("Paket Uji · Alat", 30000m)],
            tagihan.Rincian.Where(r => r.Jenis == "Operasi").Select(r => (r.Nama, r.Total)));
        Assert.Equal(("BHP Operasi · Benang Uji", 10000m),
            tagihan.Rincian.Where(r => r.Jenis == "Obat").Select(r => (r.Nama, r.Total)).Single());
        Assert.Equal(260000m, tagihan.Total);

        await using (var ctx = SimrsGramedikaDbContextFactory.Create(settings))
            await new KasirRepository(ctx).BayarRanapAsync(tagihan, "Tunai Uji", user);
        // Suspen operasi (termasuk BHP) lunas dan pendapatan operasi tidak dicatat dua kali.
        Assert.Equal((0L, -160000L, 260000L, -100000L), (await Saldo("SUS"), await Saldo("OPR"), await Saldo("KAS"), await Saldo("KMR")));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM (SELECT no_jurnal FROM detailjurnal GROUP BY no_jurnal HAVING SUM(debet)<>SUM(kredit)) x"));
        var tersisa = Assert.Single(await service.DaftarAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(tersisa));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(input with { Mulai = kedua, Selesai = kedua.AddHours(1) }));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM operasi"));
    }
}
