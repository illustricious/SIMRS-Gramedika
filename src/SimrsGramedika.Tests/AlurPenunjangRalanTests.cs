using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class AlurPenunjangRalanTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_PENUNJANG_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema kosong simrs_penunjang_fixture pada MariaDB lokal.";
        }
    }

    [FixtureFact]
    public async Task OrderTampilDiUnitTujuanDanResumeMengikutiKunjungan()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_penunjang_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_PENUNJANG_FIXTURE_PORT")!),
            Password = Environment.GetEnvironmentVariable("SIMRS_PENUNJANG_FIXTURE_PASSWORD") ?? "" };
        const string rawat = "RALAN-UJI";
        const string rm = "RM-UJI";
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM-UJI','Pasien Uji')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('DR','Dokter Registrasi'),('AUTH','Dokter Penulis')");
        await db.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Umum')");
        await db.ExecAsync("INSERT INTO poliklinik(kd_poli,nm_poli) VALUES ('POL','Poli Uji')");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_dokter,kd_poli,kd_pj,status_lanjut,stts,status_bayar,tgl_registrasi)
            VALUES ('RALAN-UJI','RM-UJI','DR','POL','PJ','Ralan','Belum','Belum Bayar',CURRENT_DATE)
            """);
        await db.ExecAsync("INSERT INTO databarang(kode_brng,nama_brng,status) VALUES ('OB','Obat Uji','1'),('NON','Obat Nonaktif','0')");
        await db.ExecAsync("""
            INSERT INTO jns_perawatan_lab(kd_jenis_prw,nm_perawatan,status,kategori,kelas,kd_pj)
            VALUES ('PK','Patologi Klinik Uji','1','PK','Rawat Jalan','PJ'),('PA','Patologi Anatomi Uji','1','PA','-','-'),
            ('MB','Mikrobiologi Uji','1','MB','-','-'),('LAIN','Tarif Penjamin Lain','1','PK','-','XYZ')
            """);
        await db.ExecAsync("INSERT INTO jns_perawatan_radiologi(kd_jenis_prw,nm_perawatan,status,kelas,kd_pj) VALUES ('RAD','Radiologi Uji','1','-','-')");
        await db.ExecAsync("INSERT INTO template_laboratorium(id_template,kd_jenis_prw,Pemeriksaan) VALUES (1,'PK','Rincian 1'),(2,'PK','Rincian 2'),(3,'MB','Rincian MB')");
        await db.ExecAsync("INSERT INTO penyakit(kd_penyakit,nm_penyakit,validcode) VALUES ('A00','Diagnosis Uji','1'),('B00','Diagnosis Tambahan','1')");
        await db.ExecAsync("INSERT INTO diagnosa_pasien(no_rawat,kd_penyakit,status,prioritas) VALUES ('RALAN-UJI','A00','Ralan',1)");
        var user = new AuthenticatedUser { UserId = "AUTH", Permissions = new HashSet<string> { "resep_dokter", "permintaan_lab", "permintaan_radiologi", "data_resume_pasien" } };
        var denied = new AuthenticatedUser { UserId = "AUTH" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new OrderRalanService(settings, rawat, rm, denied, JenisOrderRalan.Resep).CariAsync(""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ResumeRalanService(settings, rawat, rm, denied).MuatAsync());
        OrderRalanService Order(JenisOrderRalan jenis) => new(settings, rawat, rm, user, jenis);
        var resep = Order(JenisOrderRalan.Resep);
        var obat = Assert.Single(await resep.CariAsync(""));
        var inactive = new BarisOrderRalan("NON", "", 1, "Uji");
        await Assert.ThrowsAsync<InvalidOperationException>(() => resep.SimpanAsync([obat with { Jumlah = 2, Aturan = "Aturan uji" }, inactive], "", ""));
        Assert.Empty(await resep.RiwayatAsync());
        var noResep = await resep.SimpanAsync([obat with { Jumlah = 2, Aturan = "Aturan uji" }], "", "");
        Assert.Equal("Menunggu farmasi", Assert.Single(await resep.RiwayatAsync()).S("status"));
        var lab = Order(JenisOrderRalan.LabPk);
        var pk = Assert.Single(await lab.CariAsync(""));
        Assert.Equal(2, (await lab.TemplateAsync(pk.Kode)).Count);
        var noPk = await lab.SimpanAsync([pk with { Template = [2] }], "Diagnosis uji", "");
        var noPa = await Order(JenisOrderRalan.LabPa).SimpanAsync([new("PA","")], "Diagnosis uji", "",
            new(DateTime.Today, "Bahan uji", "Lokasi uji", "", "", null, "", ""));
        var noMb = await Order(JenisOrderRalan.LabMb).SimpanAsync([new("MB","", Template: [3])], "Diagnosis uji", "");
        var noRad = await Order(JenisOrderRalan.Radiologi).SimpanAsync([new("RAD","")], "Diagnosis uji", "");
        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        var today = DateOnly.FromDateTime((await db.FirstAsync("SELECT CURRENT_DATE waktu"))!.Dt("waktu")!.Value);
        var farmasi = new FarmasiRepository(context);
        Assert.Equal("Dokter Penulis", Assert.Single(await farmasi.GetResepDokterAsync(today, today), r => r.NoResep == noResep).NamaDokter);
        Assert.Single(await farmasi.GetResepItemsAsync(noResep));
        var labRepo = new LabPermintaanRepository(context);
        var labFilter = new LabPermintaanFilter(LabLokasi.RawatJalan, today, today, null, null, rawat, false);
        Assert.Equal(noPk, Assert.Single(await labRepo.GetListAsync(LabCategory.PK, labFilter)).NoOrder);
        Assert.Contains(await labRepo.GetItemsAsync(LabCategory.PK, [noPk]), row => row.Detail == "Rincian 2");
        Assert.Equal(noPa, Assert.Single(await labRepo.GetListAsync(LabCategory.PA, labFilter)).NoOrder);
        Assert.Equal(noMb, Assert.Single(await labRepo.GetListAsync(LabCategory.MB, labFilter)).NoOrder);
        var radRepo = new RadiologiPermintaanRepository(context);
        Assert.Equal(noRad, Assert.Single(await radRepo.GetItemRowsAsync(new(RadiologiLokasi.RawatJalan, today, today, null, null, rawat, false))).NoOrder);
        var resume = new ResumeRalanService(settings, rawat, rm, user);
        var narasi = new IsiResumeRalan("Keluhan uji", "Perjalanan uji", "", "", "Hidup", "");
        await resume.SimpanAsync(narasi, null);
        var (snapshot, kode) = await resume.MuatAsync();
        Assert.Equal(narasi, snapshot?.Isi);
        Assert.Equal("A00", Assert.Single(kode).Kode);
        Assert.Equal("Diagnosis Uji", await db.StrAsync("SELECT diagnosa_utama FROM resume_pasien WHERE no_rawat=@p0", rawat));
        await resume.SimpanAsync(narasi with { Laborat = "Hasil uji" }, snapshot);
        await Assert.ThrowsAsync<InvalidOperationException>(() => resume.SimpanAsync(narasi, snapshot));
        var diagnosaService = new DiagnosaRalanService(settings, rawat, new AuthenticatedUser { IsAdminUtama = true });
        await diagnosaService.TambahAsync(false, "B00", 2, "");
        Assert.Equal("Diagnosis Tambahan", await db.StrAsync("SELECT diagnosa_sekunder FROM resume_pasien WHERE no_rawat=@p0", rawat));
        await diagnosaService.HapusAsync(false, "B00", 2);
        Assert.Equal("", await db.StrAsync("SELECT diagnosa_sekunder FROM resume_pasien WHERE no_rawat=@p0", rawat));
        await db.ExecAsync("UPDATE reg_periksa SET status_bayar='Sudah Bayar' WHERE no_rawat=@p0", rawat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lab.SimpanAsync([pk], "Diagnosis uji", ""));
        var wrongPatient = new OrderRalanService(settings, rawat, "RM-LAIN", user, JenisOrderRalan.Resep);
        await Assert.ThrowsAsync<InvalidOperationException>(() => wrongPatient.SimpanAsync([obat with { Jumlah = 1, Aturan = "Uji" }], "", ""));
    }
}
