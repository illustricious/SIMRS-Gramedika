using System.Xml.Linq;
using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Registrasi.Labels;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

/// <summary>Menu klik kanan Registrasi: ubah status, catatan pasien, laporan, dan format cetak.</summary>
public class RegistrasiMenuTests
{
    private static readonly RegistrasiAkses Admin = new(true, false);

    [Fact]
    public async Task UbahStatusDitolakSebelumKoneksi()
    {
        var service = new RegistrasiService(new DatabaseSettings { Host = "127.0.0.1", Port = 1 });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetStatusAsync("R", "Dirujuk", Admin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatusAsync("R", "Sudah", new(false, false)));
    }

    [Theory]
    [InlineData("btnKunjunganRalan")]
    [InlineData("btnKunjunganRanap")]
    [InlineData("btnSensusHarianPoli")]
    [InlineData("btnRekapKunjungan")]
    [InlineData("btnRl32")]
    [InlineData("btnFrekuensiRalan")]
    [InlineData("btnRegistrasiPoliPerTanggal")]
    [InlineData("btnGrafikKunjunganPoli")]
    [InlineData("btnGrafikKunjunganPerDokter")]
    [InlineData("btnGrafikKunjunganPerTahun")]
    [InlineData("btnGrafikKunjunganPerBulan")]
    [InlineData("btnGrafikKunjunganPerTanggal")]
    [InlineData("btnGrafikDemografiRegistrasi")]
    [InlineData("btnBerkasDigitalPerawatan")]
    [InlineData("btnInsidenKeselamatanPasien")]
    public void SetiapLaporanDiMenuTersedia(string key)
    {
        Assert.True(ContextReports.TryGet(key, out var report));
        if (ContextReports.IsPatientReport(key))
            Assert.Contains(report.Columns, c => c.Column == "no_rkm_medis");
    }

    [Fact]
    public void LaporanPasienTidakMemuatPasienLain()
    {
        // No. RM "000001" juga muncul di ujung No. Rawat pasien lain.
        Row Baris(string rm, string rawat) => new(new() { ["no_rkm_medis"] = rm, ["no_rawat"] = rawat });
        var hasil = ReportView.MilikPasien([Baris("000001", "2026/11/26/000009"), Baris("000777", "2026/11/26/000001")], "000001");
        Assert.Equal("000001", Assert.Single(hasil).S("no_rkm_medis"));
    }

    private static PatientLabel Label(string template)
    {
        using var resource = typeof(PatientLabel).Assembly.GetManifestResourceStream(
            $"SimrsGramedika.UI.Modules.Registrasi.Labels.Templates.{template}.jrxml")!;
        var visit = new Row(new()
        {
            ["no_rawat"] = "2026/11/26/000001", ["no_reg"] = "001", ["no_rkm_medis"] = "000001", ["nm_pasien"] = "PASIEN UJI",
            ["jk"] = "P", ["almt_pj"] = "ALAMAT PJ", ["tgl_lahir"] = new DateTime(1990, 2, 3),
            ["tgl_registrasi"] = new DateTime(2026, 11, 26), ["jam_reg"] = new TimeSpan(8, 0, 0),
        });
        return new PatientLabel(XElement.Load(resource), visit, null, PatientLabel.Formats.Single(f => f.Template == template));
    }

    [Fact]
    public void SemuaFormatCetakDapatDigambar()
    {
        var shots = Environment.GetEnvironmentVariable("SIMRS_SCREENSHOT_DIR");
        foreach (var format in PatientLabel.Formats)
        {
            using var bitmap = Label(format.Template).Render(string.IsNullOrEmpty(shots) ? 1 : 2);
            Assert.True(bitmap.Width > 0 && bitmap.Height > 0, format.Template);
            if (string.IsNullOrEmpty(shots)) continue;
            Directory.CreateDirectory(shots);
            using var png = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(shots, format.Template + ".png"));
            png.SaveTo(file);
        }
    }

    [Theory]
    [InlineData("rptBuktiRegister")]
    [InlineData("rptBuktiRegister3")]
    public void JenisKelaminBuktiRegisterTidakMemuatArgumenReplaceAll(string template)
    {
        const string expression = "\": \"+$F{jk}.replaceAll(\"L\",\"Laki-Laki\").replaceAll(\"P\",\"Perempuan\")";
        Assert.Equal(": Perempuan", Label(template).Evaluate(expression));
    }

    [Theory]
    [InlineData("rptCheckList3")]
    [InlineData("rptCheckList4")]
    public void AlamatCheckListMemakaiAlamatPenanggungJawab(string template) =>
        Assert.Equal(": ALAMAT PJ", Label(template).Evaluate("\": \"+$P{alamat}"));

    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_REGISTRASI_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema simrs_registrasi_fixture (pasien, reg_periksa, kamar_inap, billing, catatan_pasien) pada MariaDB lokal.";
        }
    }

    private static DatabaseSettings Fixture() => new()
    {
        Host = "127.0.0.1", Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_REGISTRASI_FIXTURE_PORT")!),
        Database = "simrs_registrasi_fixture", User = "root",
        Password = Environment.GetEnvironmentVariable("SIMRS_REGISTRASI_FIXTURE_PASSWORD") ?? "",
    };

    [FixtureFact]
    public async Task UbahStatusMenjagaBillingKamarDanBatasWaktu()
    {
        var settings = Fixture();
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        var kode = Guid.NewGuid().ToString("N")[..10];
        string Rawat(string akhiran) => $"S{kode}{akhiran}";
        foreach (var akhiran in new[] { "A", "B", "K" })
            await db.ExecAsync("INSERT INTO reg_periksa(no_rawat,tgl_registrasi,jam_reg,no_rkm_medis,stts,biaya_reg,status_lanjut) VALUES(@p0,CURRENT_DATE,'00:00:00',@p0,'Belum',25000,'Ralan')", Rawat(akhiran));
        await db.ExecAsync("INSERT INTO billing(noindex,no_rawat) VALUES(1,@p0)", Rawat("B"));
        await db.ExecAsync("INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk) VALUES(@p0,'KMR',CURRENT_DATE,'00:00:00')", Rawat("K"));
        async Task<(string, double)> Baca(string akhiran)
        {
            var row = (await db.FirstAsync("SELECT stts,biaya_reg FROM reg_periksa WHERE no_rawat=@p0", Rawat(akhiran)))!;
            return (row.S("stts"), row.D("biaya_reg"));
        }
        var service = new RegistrasiService(settings);
        var petugas = new RegistrasiAkses(false, true);
        var sekarang = DateTime.Today.AddHours(1);

        await service.SetStatusAsync(Rawat("A"), "Sudah", petugas, sekarang);
        Assert.Equal(("Sudah", 25000d), await Baca("A"));
        await service.SetStatusAsync(Rawat("A"), "Belum", petugas, sekarang);
        Assert.Equal(("Belum", 25000d), await Baca("A"));

        var lewat = DateTime.Today.AddDays(3);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatusAsync(Rawat("A"), "Batal", petugas, lewat));
        Assert.Equal(("Belum", 25000d), await Baca("A"));
        await service.SetStatusAsync(Rawat("A"), "Batal", Admin, lewat);
        Assert.Equal(("Batal", 0d), await Baca("A"));

        foreach (var akhiran in new[] { "B", "K" })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatusAsync(Rawat(akhiran), "Batal", Admin, sekarang));
            Assert.Equal(("Belum", 25000d), await Baca(akhiran));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatusAsync(Rawat("X"), "Sudah", Admin, sekarang));
    }

    [FixtureFact]
    public async Task CatatanPasienSimpanUbahHapus()
    {
        var settings = Fixture();
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        var rm = "C" + Guid.NewGuid().ToString("N")[..10];
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis) VALUES(@p0)", rm);
        var service = new CatatanPasienService(settings);

        Assert.Equal("", await service.BacaAsync(rm));
        await service.SimpanAsync(rm, "  Alergi uji  ");
        Assert.Equal("Alergi uji", await service.BacaAsync(rm));
        await service.SimpanAsync(rm, "Catatan baru");
        Assert.Equal("Catatan baru", await service.BacaAsync(rm));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(rm, " "));
        await service.HapusAsync(rm);
        Assert.Equal("", await service.BacaAsync(rm));
    }
}
