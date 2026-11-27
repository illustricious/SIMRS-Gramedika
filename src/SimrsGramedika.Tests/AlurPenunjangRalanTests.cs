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
        var cetak = await resume.CetakAsync();
        Assert.Equal(("Pasien Uji", "Dokter Penulis", "Poli Uji", "Hasil uji", "AUTH"),
            (cetak.Pasien, cetak.Dokter, cetak.Ruang, cetak.Isi.Laborat, cetak.IdDokter));
        Assert.Equal(("A00", "Diagnosis Uji"), (cetak.Kode[0].Kode, cetak.Kode[0].Nama));
        var pdf = SimrsGramedika.UI.Modules.RawatJalan.ResumeRalanReport.GeneratePdf(cetak,
            new KopInstansi("RS UJI", "Jalan Uji", "000", "", null) { Kota = "Kota Uji" });
        Assert.True(new FileInfo(pdf).Length > 1000);
        File.Delete(pdf);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ResumeRalanService(settings, rawat, "RM-LAIN", user).CetakAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => resume.SimpanAsync(narasi, snapshot));
        var diagnosaService = new DiagnosaRalanService(settings, rawat, new AuthenticatedUser { IsAdminUtama = true });
        await diagnosaService.TambahAsync(false, "B00", 2, "");
        Assert.Equal("Diagnosis Tambahan", await db.StrAsync("SELECT diagnosa_sekunder FROM resume_pasien WHERE no_rawat=@p0", rawat));
        await diagnosaService.HapusAsync(false, "B00", 2);
        Assert.Equal("", await db.StrAsync("SELECT diagnosa_sekunder FROM resume_pasien WHERE no_rawat=@p0", rawat));
        // Ubah resep: dokter lain ditolak, kegagalan satu baris membatalkan semua, data lama ditolak.
        var drafResep = await resep.MuatOrderAsync(noResep);
        var baru = drafResep.Baris[0] with { Jumlah = 5, Aturan = "Aturan baru" };
        var dokterLain = new AuthenticatedUser { UserId = "DR", Permissions = user.Permissions };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new OrderRalanService(settings, rawat, rm, dokterLain, JenisOrderRalan.Resep).MuatOrderAsync(noResep));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resep.UbahAsync(drafResep, [baru, inactive], "", ""));
        Assert.Equal(2, await db.CountAsync("SELECT jml FROM resep_dokter WHERE no_resep=@p0", noResep));
        await resep.UbahAsync(drafResep, [baru], "", "");
        Assert.Equal(5, await db.CountAsync("SELECT jml FROM resep_dokter WHERE no_resep=@p0", noResep));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resep.UbahAsync(drafResep, [baru], "", ""));
        await db.ExecAsync("UPDATE resep_obat SET tgl_perawatan=CURRENT_DATE WHERE no_resep=@p0", noResep);
        await Assert.ThrowsAsync<InvalidOperationException>(() => resep.MuatOrderAsync(noResep));

        // Ubah permintaan lab: rincian diganti; yang sudah diambil sampel atau dibayar ditolak.
        var drafLab = await lab.MuatOrderAsync(noPk);
        Assert.Equal([2], Assert.Single(drafLab.Baris).Template!);
        await lab.UbahAsync(drafLab, [pk with { Template = [1, 2] }], "Diagnosis baru", "Info");
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM permintaan_detail_permintaan_lab WHERE noorder=@p0", noPk));
        Assert.Equal("Diagnosis baru", await db.StrAsync("SELECT diagnosa_klinis FROM permintaan_lab WHERE noorder=@p0", noPk));
        drafLab = await lab.MuatOrderAsync(noPk);
        await db.ExecAsync("UPDATE permintaan_detail_permintaan_lab SET stts_bayar='Sudah' WHERE noorder=@p0 AND id_template=1", noPk);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lab.UbahAsync(drafLab, [pk with { Template = [1] }], "Uji", ""));
        await db.ExecAsync("UPDATE permintaan_detail_permintaan_lab SET stts_bayar='Belum' WHERE noorder=@p0", noPk);
        await db.ExecAsync("UPDATE permintaan_lab SET tgl_sampel=CURRENT_DATE WHERE noorder=@p0", noPk);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lab.UbahAsync(drafLab, [pk with { Template = [1] }], "Uji", ""));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM permintaan_detail_permintaan_lab WHERE noorder=@p0", noPk));

        var pa = Order(JenisOrderRalan.LabPa);
        var drafPa = await pa.MuatOrderAsync(noPa);
        Assert.Equal("Bahan uji", drafPa.Bahan!.Cara);
        await pa.UbahAsync(drafPa, drafPa.Baris, "Diagnosis uji", "", drafPa.Bahan with { Cara = "Biopsi", Diambil = null });
        Assert.Equal("Biopsi", (await pa.MuatOrderAsync(noPa)).Bahan!.Cara);
        var mb = Order(JenisOrderRalan.LabMb);
        var drafMb = await mb.MuatOrderAsync(noMb);
        await mb.UbahAsync(drafMb, drafMb.Baris, "Diagnosis MB", "");
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM permintaan_detail_permintaan_labmb WHERE noorder=@p0", noMb));

        var rad = Order(JenisOrderRalan.Radiologi);
        var drafRad = await rad.MuatOrderAsync(noRad);
        await rad.UbahAsync(drafRad, drafRad.Baris, "Diagnosis radiologi", "Info baru");
        Assert.Equal("Info baru", await db.StrAsync("SELECT informasi_tambahan FROM permintaan_radiologi WHERE noorder=@p0", noRad));
        drafRad = await rad.MuatOrderAsync(noRad);

        await db.ExecAsync("INSERT INTO billing(noindex,no_rawat,`no`,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya) VALUES (1,@p0,'','Uji','',0,1,0,0)", rawat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rad.UbahAsync(drafRad, drafRad.Baris, "Uji", ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => lab.SimpanAsync([pk], "Diagnosis uji", ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => rad.BatalkanAsync(noRad));
        await db.ExecAsync("DELETE FROM billing WHERE no_rawat=@p0", rawat);

        // Batal order: ditolak bila sudah diproses atau bukan pengirim; berhasil menghapus header dan rincian.
        await db.ExecAsync("UPDATE permintaan_radiologi SET tgl_sampel=CURRENT_DATE WHERE noorder=@p0", noRad);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rad.BatalkanAsync(noRad));
        await db.ExecAsync("UPDATE permintaan_radiologi SET tgl_sampel='0000-00-00' WHERE noorder=@p0", noRad);
        var radLain = new OrderRalanService(settings, rawat, rm,
            new AuthenticatedUser { UserId = "DR", Permissions = user.Permissions }, JenisOrderRalan.Radiologi);
        await Assert.ThrowsAsync<InvalidOperationException>(() => radLain.BatalkanAsync(noRad));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM permintaan_radiologi WHERE noorder=@p0", noRad));
        await rad.BatalkanAsync(noRad);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM permintaan_radiologi WHERE noorder=@p0", noRad));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM permintaan_pemeriksaan_radiologi WHERE noorder=@p0", noRad));
        await Assert.ThrowsAsync<InvalidOperationException>(() => rad.BatalkanAsync(noRad));
        await mb.BatalkanAsync(noMb);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM permintaan_detail_permintaan_labmb WHERE noorder=@p0", noMb));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM permintaan_labmb WHERE noorder=@p0", noMb));
        var wrongPatient = new OrderRalanService(settings, rawat, "RM-LAIN", user, JenisOrderRalan.Resep);
        await Assert.ThrowsAsync<InvalidOperationException>(() => wrongPatient.SimpanAsync([obat with { Jumlah = 1, Aturan = "Uji" }], "", ""));
    }
}
