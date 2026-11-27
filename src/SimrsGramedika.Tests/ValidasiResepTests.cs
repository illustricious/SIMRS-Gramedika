using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

/// <summary>
/// Validasi dan penyerahan resep pada skema sintetis simrs_resep_fixture (tabel dari schema_sik.sql, kosong)
/// di MariaDB lokal.
/// </summary>
public class ValidasiResepTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_RESEP_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_resep_fixture.";
        }
    }

    private const string Resep = "202611270001";
    private const string Rawat = "2026/11/27/000001";

    private static DatabaseSettings Settings(bool batch = false, bool bulat = false) => new()
    {
        Host = "127.0.0.1", Database = "simrs_resep_fixture", User = "root", AktifkanBatchObat = batch, PembulatanHargaObat = bulat,
        Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_RESEP_FIXTURE_PORT")!),
    };

    private static AuthenticatedUser Apoteker => new() { UserId = "AP1", Permissions = new HashSet<string> { "beri_obat" } };

    private static ValidasiResepService Service(bool batch = false, bool bulat = false) => new(Settings(batch, bulat), Apoteker);

    /// <summary>Data awal: satu kunjungan, resep dua obat, stok 10 dan 5, embalase 200, tuslah 100.</summary>
    internal static async Task SiapkanAsync(Db db)
    {
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        foreach (var t in new[]
                 {
                     "detailjurnal", "jurnal", "rekening", "aturan_pakai", "detail_pemberian_obat", "riwayat_barang_medis", "data_batch",
                     "gudangbarang", "databarang", "kodesatuan", "set_akun_ralan", "set_harga_obat_ralan", "set_embalase", "set_depo_ralan",
                     "set_lokasi", "bangsal", "billing", "resep_dokter_racikan", "resep_dokter", "resep_obat", "reg_periksa", "pasien",
                     "dokter", "poliklinik", "penjab", "metode_racik", "resep_dokter_racikan_detail", "obat_racikan",
                     "detail_obat_racikan", "set_depo_ranap", "set_harga_obat_ranap", "kamar", "kamar_inap", "set_akun_ranap",
                     "returpasien",
                 })
            await db.ExecAsync($"DELETE FROM {t}");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES('000001','PASIEN SINTETIS')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES('D1','DOKTER UJI')");
        await db.ExecAsync("INSERT INTO poliklinik(kd_poli,nm_poli) VALUES('UMUM','Poli Umum')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES('UMU','UMUM')");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_dokter,kd_poli,kd_pj,status_lanjut,stts,status_bayar,tgl_registrasi)
            VALUES(@p0,'000001','D1','UMUM','UMU','Ralan','Sudah','Belum Bayar',CURRENT_DATE)
            """, Rawat);
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES('DPJ','Depo Rawat Jalan','1')");
        await db.ExecAsync("INSERT INTO set_depo_ralan(kd_poli,kd_bangsal) VALUES('UMUM','DPJ')");
        await db.ExecAsync("INSERT INTO set_embalase(embalase_per_obat,tuslah_per_obat) VALUES(200,100)");
        await db.ExecAsync("INSERT INTO kodesatuan(kode_sat,satuan) VALUES('TAB','Tablet')");
        await db.ExecAsync("""
            INSERT INTO databarang(kode_brng,nama_brng,kode_sat,status,ralan,h_beli,dasar)
            VALUES('OB1','Amoksisilin Uji','TAB','1',1500,900,1000),('OB2','Parasetamol Uji','TAB','1',500,250,300)
            """);
        await db.ExecAsync("INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES('OB1','DPJ',10,'',''),('OB2','DPJ',5,'','')");
        await db.ExecAsync("""
            INSERT INTO resep_obat(no_resep,tgl_perawatan,jam,no_rawat,kd_dokter,tgl_peresepan,jam_peresepan,status,tgl_penyerahan,jam_penyerahan)
            VALUES(@p0,'0000-00-00','00:00:00',@p1,'D1',CURRENT_DATE,'08:00:00','ralan','0000-00-00','00:00:00')
            """, Resep, Rawat);
        await db.ExecAsync("INSERT INTO resep_dokter(no_resep,kode_brng,jml,aturan_pakai) VALUES(@p0,'OB1',2,'3x1'),(@p0,'OB2',3,'')", Resep);
        await db.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES('SUS','Suspen'),('PDP','Pendapatan'),('HPP','HPP'),('PSD','Persediaan')");
        await db.ExecAsync("""
            INSERT INTO set_akun_ralan(Suspen_Piutang_Obat_Ralan,Obat_Ralan,HPP_Obat_Rawat_Jalan,Persediaan_Obat_Rawat_Jalan)
            VALUES('SUS','PDP','HPP','PSD')
            """);
    }

    private static async Task<List<InputValidasi>> IsiAsync(ValidasiResepService service)
    {
        var r = await service.RincianAsync(Resep);
        return r.Baris.Select(b => new InputValidasi(b.Kode, b.JumlahResep, r.Embalase, r.Tuslah, b.Harga, b.NoBatch, b.NoFaktur, b.Racik)).ToList();
    }

    private static async Task BelumAdaPerubahanAsync(Db db)
    {
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM aturan_pakai"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM riwayat_barang_medis"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
        Assert.Equal(15, await db.CountAsync("SELECT SUM(stok) FROM gudangbarang"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM resep_obat WHERE tgl_perawatan='0000-00-00'"));
    }

    [Fact]
    public async Task TanpaHakAksesDitolakSebelumKoneksi()
    {
        var mati = new DatabaseSettings { Host = "127.0.0.1", Port = 1 };
        var tanpa = new ValidasiResepService(mati, new AuthenticatedUser());
        await Assert.ThrowsAsync<InvalidOperationException>(() => tanpa.DaftarAsync(DateTime.Today, DateTime.Today));
        // Dokter boleh melihat daftar, tetapi tidak memvalidasi atau menyerahkan.
        var dokter = new ValidasiResepService(mati, new AuthenticatedUser { Permissions = new HashSet<string> { "resep_dokter" } });
        await Assert.ThrowsAsync<InvalidOperationException>(() => dokter.ValidasiAsync("X", [new("A", 1, 0, 0, 0, "", "")]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dokter.SerahkanAsync("X"));
        var apoteker = new ValidasiResepService(mati, Apoteker);
        await Assert.ThrowsAsync<ArgumentException>(() => apoteker.ValidasiAsync("X", [new("A", 0, 0, 0, 0, "", "")]));
        await Assert.ThrowsAsync<ArgumentException>(() => apoteker.ValidasiAsync("X", [new("A", -1, 0, 0, 0, "", "")]));
        await Assert.ThrowsAsync<ArgumentException>(() => apoteker.ValidasiAsync("X", [new("A", double.NaN, 0, 0, 0, "", "")]));
    }

    [Fact]
    public void TotalDibulatkanSepertiJava()
    {
        Assert.Equal(3, ValidasiResepService.Total(1, 2.5, 0, 0));
        Assert.Equal(3300, ValidasiResepService.Total(1500, 2, 200, 100));
    }

    [FixtureFact]
    public async Task ValidasiMencatatObatStokDanJurnalLaluDiserahkan()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        var service = Service();

        var menunggu = Assert.Single(await service.DaftarAsync(DateTime.Today, DateTime.Today, "sintetis", ResepFarmasi.Menunggu));
        Assert.Equal(ResepFarmasi.Menunggu, menunggu.Status);
        var belum = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SerahkanAsync(Resep));
        Assert.Contains("belum divalidasi", belum.Message);

        // OB1 2 x 1500 + 300 = 3300; OB2 3 x 500 + 300 = 1800. HPP 2 x 1000 + 3 x 300 = 2900.
        Assert.Equal(5100, await service.ValidasiAsync(Resep, await IsiAsync(service)));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat WHERE status='Ralan' AND kd_bangsal='DPJ'"));
        Assert.Equal(5100, await db.CountAsync("SELECT SUM(total) FROM detail_pemberian_obat"));
        Assert.Equal(1000, await db.CountAsync("SELECT h_beli FROM detail_pemberian_obat WHERE kode_brng='OB1'"));
        Assert.Equal("3x1", await db.StrAsync("SELECT aturan FROM aturan_pakai"));
        Assert.Equal(8, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB1'"));
        Assert.Equal(2, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB2'"));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM riwayat_barang_medis WHERE kode_brng='OB1' AND stok_awal=10 AND keluar=2 AND stok_akhir=8 AND posisi='Pemberian Obat' AND petugas='AP1'"));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM resep_obat r JOIN detail_pemberian_obat d ON d.tgl_perawatan=r.tgl_perawatan AND d.jam=r.jam WHERE d.kode_brng='OB1'"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM jurnal WHERE no_bukti=@p0", Rawat));
        Assert.Equal(5100, await db.CountAsync("SELECT debet FROM detailjurnal WHERE kd_rek='SUS'"));
        Assert.Equal(5100, await db.CountAsync("SELECT kredit FROM detailjurnal WHERE kd_rek='PDP'"));
        Assert.Equal(2900, await db.CountAsync("SELECT debet FROM detailjurnal WHERE kd_rek='HPP'"));
        Assert.Equal(2900, await db.CountAsync("SELECT kredit FROM detailjurnal WHERE kd_rek='PSD'"));

        Assert.Empty(await service.DaftarAsync(DateTime.Today, DateTime.Today, "", ResepFarmasi.Menunggu));
        var lagi = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ValidasiAsync(Resep, await IsiAsync(service)));
        Assert.Contains("sudah divalidasi", lagi.Message);

        await service.SerahkanAsync(Resep);
        var selesai = Assert.Single(await service.DaftarAsync(DateTime.Today, DateTime.Today, "", ResepFarmasi.Selesai));
        Assert.NotNull(selesai.Diserahkan);
        var dua = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SerahkanAsync(Resep));
        Assert.Contains("sudah diserahkan", dua.Message);
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
    }

    [FixtureFact]
    public async Task ObatBerjumlahNolTidakDiberikan()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        var service = Service();
        var isi = await IsiAsync(service);
        isi = isi.Select(i => i.Kode == "OB2" ? i with { Jumlah = 0 } : i with { Jumlah = 1 }).ToList();
        Assert.Equal(1800, await service.ValidasiAsync(Resep, isi));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat"));
        Assert.Equal(5, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB2'"));
    }

    [FixtureFact]
    public async Task PenolakanTidakMeninggalkanPerubahan()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        var service = Service();
        var isi = await IsiAsync(service);
        async Task Tolak(IReadOnlyList<InputValidasi> input, string pesan)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidasiAsync(Resep, input));
            Assert.Contains(pesan, ex.Message);
        }

        await Tolak(isi.Select(i => i with { Jumlah = 4 }).ToList(), "melebihi resep");
        await Tolak(isi.Select(i => i with { Harga = i.Harga - 1 }).ToList(), "berubah");
        await Tolak([isi[0] with { Kode = "LAIN" }], "tidak ditemukan");

        await db.ExecAsync("UPDATE gudangbarang SET stok=2 WHERE kode_brng='OB2'");
        await Tolak(isi, "tidak cukup");
        await db.ExecAsync("UPDATE gudangbarang SET stok=5 WHERE kode_brng='OB2'");

        // Rekening belum diatur: jurnal gagal setelah stok dipotong, seluruhnya dibatalkan.
        await db.ExecAsync("UPDATE set_akun_ralan SET Obat_Ralan=''");
        await Tolak(isi, "Rekening");
        await db.ExecAsync("UPDATE set_akun_ralan SET Obat_Ralan='PDP'");

        await db.ExecAsync("INSERT INTO billing(no_rawat,no,nm_perawatan) VALUES(@p0,'1','x')", Rawat);
        await Tolak(isi, "Billing");
        await db.ExecAsync("DELETE FROM billing");

        await db.ExecAsync("UPDATE reg_periksa SET stts='Batal'");
        await Tolak(isi, "batal");
        await db.ExecAsync("UPDATE reg_periksa SET stts='Sudah'");

        await db.ExecAsync("UPDATE databarang SET status='0' WHERE kode_brng='OB1'");
        await Tolak(isi, "tidak aktif");
        await db.ExecAsync("UPDATE databarang SET status='1' WHERE kode_brng='OB1'");

        await db.ExecAsync("UPDATE resep_obat SET status='ranap'");
        await Tolak(isi, "belum punya kamar");
        await db.ExecAsync("UPDATE resep_obat SET status='ralan'");

        await BelumAdaPerubahanAsync(db);
    }

    [FixtureFact]
    public async Task DuaValidasiBersamaanHanyaSatuBerhasil()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        var isi = await IsiAsync(Service());
        async Task<bool> Coba()
        {
            try { await Service().ValidasiAsync(Resep, isi); return true; }
            catch (InvalidOperationException) { return false; }
        }

        var hasil = await Task.WhenAll(Coba(), Coba());
        Assert.Single(hasil, h => h);
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat"));
        Assert.Equal(10, await db.CountAsync("SELECT SUM(stok) FROM gudangbarang"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
    }

    [FixtureFact]
    public async Task ValidasiYangMenungguKunciMelihatPerubahanTerbaru()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        var isi = await IsiAsync(Service());
        // Klien lain memegang kunci kunjungan, lalu memvalidasi resep itu sebelum melepasnya.
        await db.BeginAsync();
        await db.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", Rawat);
        var menunggu = Task.Run(() => Service().ValidasiAsync(Resep, isi));
        await Task.Delay(700);
        Assert.False(menunggu.IsCompleted);
        await db.ExecAsync("UPDATE resep_obat SET tgl_perawatan=CURRENT_DATE,jam='07:00:00' WHERE no_resep=@p0", Resep);
        await db.CommitAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => menunggu);
        Assert.Contains("sudah divalidasi", ex.Message);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat"));
        Assert.Equal(15, await db.CountAsync("SELECT SUM(stok) FROM gudangbarang"));
    }

    private static async Task RacikanAsync(Db db)
    {
        await db.ExecAsync("INSERT INTO metode_racik(kd_racik,nm_racik) VALUES('R01','Puyer')");
        await db.ExecAsync("""
            INSERT INTO resep_dokter_racikan(no_resep,no_racik,nama_racik,kd_racik,jml_dr,aturan_pakai,keterangan)
            VALUES(@p0,'1','Puyer Batuk','R01',10,'3x1 bungkus','sesudah makan')
            """, Resep);
        // OB1 juga ada di luar racikan.
        await db.ExecAsync("""
            INSERT INTO resep_dokter_racikan_detail(no_resep,no_racik,kode_brng,p1,p2,kandungan,jml)
            VALUES(@p0,'1','OB1',1,1,'250',1),(@p0,'1','OB2',1,1,'500',2)
            """, Resep);
    }

    private static Task ObatKetigaAsync(Db db, string status = "1") => db.ExecAsync("""
        REPLACE INTO databarang(kode_brng,nama_brng,kode_sat,status,ralan,kelas2,h_beli,dasar)
        VALUES('OB3','Cetirizine Uji','TAB',@p0,800,850,500,600);
        REPLACE INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES('OB3','DPJ',20,'','')
        """, status);

    private static async Task RanapAsync(Db db)
    {
        await db.ExecAsync("UPDATE resep_obat SET status='ranap'");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES('ANG','Anggrek','1'),('DPI','Depo Rawat Inap','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas) VALUES('ANG-1','ANG','Kelas 2')");
        await db.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang) VALUES(@p0,'ANG-1',CURRENT_DATE,'07:00:00','-')
            """, Rawat);
        await db.ExecAsync("INSERT INTO set_depo_ranap(kd_bangsal,kd_depo) VALUES('ANG','DPI')");
        await db.ExecAsync("UPDATE databarang SET kelas2=IF(kode_brng='OB1',1700,600)");
        await db.ExecAsync("INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES('OB1','DPI',10,'',''),('OB2','DPI',5,'','')");
        await db.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES('SUSI','Suspen Inap'),('PDPI','Pendapatan Inap'),('HPPI','HPP Inap'),('PSDI','Persediaan Inap')");
        await db.ExecAsync("""
            INSERT INTO set_akun_ranap(Suspen_Piutang_Obat_Ranap,Obat_Ranap,HPP_Obat_Rawat_Inap,Persediaan_Obat_Rawat_Inap)
            VALUES('SUSI','PDPI','HPPI','PSDI')
            """);
    }

    [FixtureFact]
    public async Task RacikanDicatatDanObatKembarDigabung()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        await RacikanAsync(db);
        var service = Service();
        var r = await service.RincianAsync(Resep);
        Assert.Empty(r.Halangan);
        Assert.Equal("Puyer", Assert.Single(r.Racikan).Metode);
        Assert.Equal(["", "", "1", "1"], r.Baris.Select(b => b.Racik));

        // Nonracik 3300 + 1800; racikan OB1 1x1500+300 = 1800, OB2 2x500+300 = 1300.
        Assert.Equal(8200, await service.ValidasiAsync(Resep, await IsiAsync(service)));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat"));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM detail_pemberian_obat WHERE kode_brng='OB1' AND jml=3 AND embalase=400 AND tuslah=200 AND total=5100"));
        Assert.Equal(7, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB1'"));
        Assert.Equal(0, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB2'"));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM obat_racikan WHERE no_racik='1' AND nama_racik='Puyer Batuk' AND jml_dr=10 AND aturan_pakai='3x1 bungkus'"));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM detail_obat_racikan"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM aturan_pakai"));
        Assert.Equal(8200, await db.CountAsync("SELECT debet FROM detailjurnal WHERE kd_rek='SUS'"));
        Assert.Equal(2, (await service.RincianAsync(Resep)).Diberikan.Count);
    }

    [FixtureFact]
    public async Task StokDihitungGabunganObatKembar()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        await RacikanAsync(db);
        // OB2 diminta 3 + 2 = 5; tersedia 4.
        await db.ExecAsync("UPDATE gudangbarang SET stok=4 WHERE kode_brng='OB2'");
        var service = Service();
        var isi = await IsiAsync(service);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidasiAsync(Resep, isi));
        Assert.Contains("tidak cukup", ex.Message);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM obat_racikan"));
    }

    [FixtureFact]
    public async Task ObatTambahanDanPengganti()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        await RacikanAsync(db);
        await ObatKetigaAsync(db);
        var service = Service();
        var cari = Assert.Single(await service.CariObatAsync(Resep, "cetiri"));
        Assert.Equal(800, cari.Harga);
        Assert.Equal(20, cari.Stok);

        // OB1 nonracik diganti OB3; di racikan OB2 diganti OB3 pula.
        var isi = (await IsiAsync(service))
            .Select(i => (i.Kode == "OB1" && i.Racik == "") || (i.Kode == "OB2" && i.Racik == "1") ? i with { Jumlah = 0 } : i)
            .ToList();
        var ganti = new InputValidasi("OB3", 4, 0, 0, cari.Harga, "", "", "", "1x1 malam");
        var diRacik = new InputValidasi("OB3", 1, 0, 0, cari.Harga, "", "", "1");
        async Task Tolak(InputValidasi input, string pesan)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidasiAsync(Resep, [.. isi, input]));
            Assert.Contains(pesan, ex.Message);
        }

        await Tolak(ganti with { Aturan = "  " }, "aturan pakai");
        await Tolak(ganti with { Racik = "9" }, "tidak ada pada resep");
        await Tolak(ganti with { Harga = 1 }, "berubah");
        await Tolak(ganti with { Jumlah = 21 }, "tidak cukup");
        await ObatKetigaAsync(db, "0");
        await Tolak(ganti, "tidak aktif");
        await ObatKetigaAsync(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ValidasiAsync(Resep, [.. isi, ganti, ganti]));

        // OB2 nonracik 1800 + OB1 racik 1800 + OB3 (4+1) x 800 = 4000.
        Assert.Equal(7600, await service.ValidasiAsync(Resep, [.. isi, ganti, diRacik]));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat WHERE kode_brng='OB3' AND jml=5 AND total=4000"));
        Assert.Equal(15, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB3'"));
        Assert.Equal("1x1 malam", await db.StrAsync("SELECT aturan FROM aturan_pakai WHERE kode_brng='OB3'"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM detail_obat_racikan WHERE kode_brng='OB3' AND no_racik='1'"));
        // Resep dokter tidak diubah.
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM resep_dokter"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM resep_dokter WHERE kode_brng='OB3'"));
    }

    [FixtureFact]
    public async Task RawatInapMemakaiKelasDepoDanRekeningRanap()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        await RanapAsync(db);
        var service = Service();
        var r = await service.RincianAsync(Resep);
        Assert.Equal("Kelas 2", r.Kelas);
        Assert.Equal("DPI", r.KodeDepo);
        // 2 x 1700 + 300 = 3700; 3 x 600 + 300 = 2100.
        Assert.Equal(5800, await service.ValidasiAsync(Resep, await IsiAsync(service)));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat WHERE status='Ranap' AND kd_bangsal='DPI'"));
        Assert.Equal(8, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB1' AND kd_bangsal='DPI'"));
        Assert.Equal(10, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB1' AND kd_bangsal='DPJ'"));
        Assert.Equal(5800, await db.CountAsync("SELECT debet FROM detailjurnal WHERE kd_rek='SUSI'"));
        Assert.Equal(2900, await db.CountAsync("SELECT kredit FROM detailjurnal WHERE kd_rek='PSDI'"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detailjurnal WHERE kd_rek IN ('SUS','PDP')"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SerahkanAsync(Resep));
        Assert.Contains("rawat jalan", ex.Message);

        await service.BatalAsync(Resep);
        Assert.Equal(10, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB1' AND kd_bangsal='DPI'"));
        Assert.Equal(5800, await db.CountAsync("SELECT SUM(kredit) FROM detailjurnal WHERE kd_rek='SUSI'"));

        // Kenaikan per kelas: h_beli 900 + 10%. Tanpa depo khusus, stok bangsal dipakai bila diatur demikian.
        await db.ExecAsync("INSERT INTO set_harga_obat_ranap(kd_pj,kelas,hargajual) VALUES('UMU','Kelas 2',10)");
        await db.ExecAsync("DELETE FROM set_depo_ranap");
        await db.ExecAsync("INSERT INTO set_lokasi(kd_bangsal,asal_stok) VALUES('DPJ','Gunakan Stok Bangsal')");
        var naik = await service.RincianAsync(Resep);
        Assert.Equal("ANG", naik.KodeDepo);
        Assert.Equal(990, naik.Baris.Single(b => b.Kode == "OB1").Harga);
    }

    [FixtureFact]
    public async Task BatalValidasiMengembalikanStokDanJurnal()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        await RacikanAsync(db);
        var service = Service();
        await service.ValidasiAsync(Resep, await IsiAsync(service));

        await service.BatalAsync(Resep);
        foreach (var tabel in new[] { "detail_pemberian_obat", "aturan_pakai", "obat_racikan", "detail_obat_racikan" })
            Assert.Equal(0, await db.CountAsync($"SELECT COUNT(*) FROM {tabel}"));
        Assert.Equal(10, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB1'"));
        Assert.Equal(5, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB2'"));
        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM riwayat_barang_medis WHERE kode_brng='OB1' AND status='Hapus' AND stok_awal=7 AND masuk=3 AND stok_akhir=10"));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
        // Setiap rekening kembali nol.
        Assert.Equal(0, await db.CountAsync(
            "SELECT COUNT(*) FROM (SELECT kd_rek FROM detailjurnal GROUP BY kd_rek HAVING SUM(debet)<>SUM(kredit)) x"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM resep_obat WHERE tgl_perawatan='0000-00-00' AND jam='00:00:00'"));
        Assert.Equal(4, await db.CountAsync("SELECT COUNT(*) FROM resep_dokter_racikan_detail") + await db.CountAsync("SELECT COUNT(*) FROM resep_dokter"));

        var lagi = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BatalAsync(Resep));
        Assert.Contains("belum divalidasi", lagi.Message);
        Assert.Equal(8200, await service.ValidasiAsync(Resep, await IsiAsync(service)));
    }

    [FixtureFact]
    public async Task BatalValidasiDitolakTanpaPerubahan()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        var service = Service();
        await service.ValidasiAsync(Resep, await IsiAsync(service));
        async Task Tolak(string pesan)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BatalAsync(Resep));
            Assert.Contains(pesan, ex.Message);
        }

        await db.ExecAsync("INSERT INTO billing(no_rawat,no,nm_perawatan) VALUES(@p0,'1','x')", Rawat);
        await Tolak("Billing");
        await db.ExecAsync("DELETE FROM billing");

        await db.ExecAsync("INSERT INTO returpasien(tanggal,no_rawat,kode_brng,jml,no_batch,no_faktur) VALUES(CURRENT_DATE,@p0,'OB1',1,'','')", Rawat);
        await Tolak("retur");
        await db.ExecAsync("DELETE FROM returpasien");

        await db.ExecAsync("UPDATE set_akun_ralan SET Obat_Ralan=''");
        await Tolak("Rekening");
        await db.ExecAsync("UPDATE set_akun_ralan SET Obat_Ralan='PDP'");

        // Waktu catatan tidak cocok dengan resep: resep tidak boleh terbuka lagi.
        await db.ExecAsync("UPDATE resep_obat SET jam=ADDTIME(jam,'00:00:01')");
        await Tolak("Tidak ada catatan");
        await db.ExecAsync("UPDATE resep_obat SET jam=SUBTIME(jam,'00:00:01')");

        await service.SerahkanAsync(Resep);
        await Tolak("sudah diserahkan");
        await db.ExecAsync("UPDATE resep_obat SET tgl_penyerahan='0000-00-00',jam_penyerahan='00:00:00'");

        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM detail_pemberian_obat"));
        Assert.Equal(10, await db.CountAsync("SELECT SUM(stok) FROM gudangbarang"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));

        // Lewat 48 jam hanya Admin Utama.
        await db.ExecAsync("UPDATE resep_obat SET tgl_perawatan=DATE_SUB(tgl_perawatan,INTERVAL 3 DAY)");
        await db.ExecAsync("UPDATE detail_pemberian_obat SET tgl_perawatan=DATE_SUB(tgl_perawatan,INTERVAL 3 DAY)");
        await db.ExecAsync("UPDATE aturan_pakai SET tgl_perawatan=DATE_SUB(tgl_perawatan,INTERVAL 3 DAY)");
        await Tolak("2 x 24 jam");
        var admin = new ValidasiResepService(Settings(), new AuthenticatedUser { UserId = "admin", IsAdminUtama = true });
        await admin.BatalAsync(Resep);
        Assert.Equal(15, await db.CountAsync("SELECT SUM(stok) FROM gudangbarang"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM aturan_pakai"));
    }

    [FixtureFact]
    public async Task DuaPembatalanBersamaanHanyaSatuBerhasil()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        await Service().ValidasiAsync(Resep, await IsiAsync(Service()));
        async Task<bool> Coba()
        {
            try { await Service().BatalAsync(Resep); return true; }
            catch (InvalidOperationException) { return false; }
        }

        var hasil = await Task.WhenAll(Coba(), Coba());
        Assert.Single(hasil, h => h);
        Assert.Equal(15, await db.CountAsync("SELECT SUM(stok) FROM gudangbarang"));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
    }

    [FixtureFact]
    public async Task HargaKenaikanDanPembulatan()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        await db.ExecAsync("INSERT INTO set_harga_obat_ralan(kd_pj,hargajual) VALUES('UMU',25)");
        // 900 + 25% = 1125; 250 + 25% = 312,5.
        var biasa = (await Service().RincianAsync(Resep)).Baris.ToDictionary(b => b.Kode, b => b.Harga);
        Assert.Equal(1125, biasa["OB1"]);
        Assert.Equal(313, biasa["OB2"]);
        var bulat = (await Service(bulat: true).RincianAsync(Resep)).Baris.ToDictionary(b => b.Kode, b => b.Harga);
        Assert.Equal(1200, bulat["OB1"]);
        Assert.Equal(400, bulat["OB2"]);
    }

    [FixtureFact]
    public async Task ModeBatchMemakaiKadaluarsaTerdekat()
    {
        await using var db = await Db.OpenAsync(Settings());
        await SiapkanAsync(db);
        await db.ExecAsync("DELETE FROM gudangbarang");
        await db.ExecAsync("""
            INSERT INTO data_batch(no_batch,kode_brng,no_faktur,tgl_kadaluarsa,h_beli,dasar,ralan,sisa)
            VALUES('B-LAMA','OB1','F1','2027-01-01',800,850,1400,20),('B-BARU','OB1','F2','2028-01-01',900,950,1600,20),
                  ('B-2','OB2','F3','2027-06-01',200,220,450,20)
            """);
        await db.ExecAsync("""
            INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur)
            VALUES('OB1','DPJ',4,'B-LAMA','F1'),('OB1','DPJ',9,'B-BARU','F2'),('OB2','DPJ',9,'B-2','F3')
            """);
        var service = Service(batch: true);
        // 2 x 1400 + 300 = 3100; 3 x 450 + 300 = 1650.
        Assert.Equal(4750, await service.ValidasiAsync(Resep, await IsiAsync(service)));
        Assert.Equal(2, await db.CountAsync("SELECT stok FROM gudangbarang WHERE no_batch='B-LAMA'"));
        Assert.Equal(9, await db.CountAsync("SELECT stok FROM gudangbarang WHERE no_batch='B-BARU'"));
        Assert.Equal(18, await db.CountAsync("SELECT sisa FROM data_batch WHERE no_batch='B-LAMA'"));
        Assert.Equal(850, await db.CountAsync("SELECT h_beli FROM detail_pemberian_obat WHERE no_batch='B-LAMA' AND no_faktur='F1'"));
        await service.BatalAsync(Resep);
        Assert.Equal(4, await db.CountAsync("SELECT stok FROM gudangbarang WHERE no_batch='B-LAMA'"));
        Assert.Equal(20, await db.CountAsync("SELECT sisa FROM data_batch WHERE no_batch='B-LAMA'"));
    }
}
