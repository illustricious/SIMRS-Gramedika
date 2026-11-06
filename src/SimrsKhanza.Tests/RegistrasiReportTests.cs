using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Registrasi;

namespace SimrsKhanza.Tests;

public class RegistrasiReportTests
{
    [Fact]
    public void GeneratePdf_WritesANonEmptyPdfFile()
    {
        var items = new List<RegistrasiListItem>
        {
            new(NoReg: "001", NoRawat: "2026/11/06/000001", KodeDokter: "D01", NamaDokter: "dr. Contoh",
                NoRekamMedis: "D00001", NamaPasien: "PASIEN TEST CETAK", NamaPoli: "Poli Umum",
                PenanggungJawab: "-", AlamatPj: "-", HubunganPj: "DIRI SENDIRI", BiayaReg: 10000,
                JenisBayar: "Umum", Status: "Sudah", TanggalRegistrasi: DateOnly.FromDateTime(DateTime.Today),
                JamRegistrasi: TimeOnly.FromDateTime(DateTime.Now), StatusBayar: "Sudah Bayar",
                StatusPoli: "Baru", NoTelpPasien: "08123456789"),
        };

        var path = RegistrasiReport.GeneratePdf(items, "Test Cetak");

        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 500); // a real PDF, not an empty/stub file
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4)); // PDF magic header

        File.Delete(path);
    }
}
