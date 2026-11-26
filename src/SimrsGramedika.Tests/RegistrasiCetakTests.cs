using System.Xml.Linq;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Registrasi;

namespace SimrsGramedika.Tests;

/// <summary>Cetak "Data Registrasi Periksa".</summary>
public class RegistrasiCetakTests
{
    [Fact]
    public void KolomMengisiLebarHalamanKhanza()
    {
        // rptReg.jrxml: halaman 913, margin 20, lebar kolom 873.
        Assert.Equal(873, RegistrasiReport.KolomRegistrasi.Sum(k => k.Lebar));
        Assert.Equal(14, RegistrasiReport.KolomRegistrasi.Length);
    }

    [Fact]
    public void BarisPanjangTetapMenjadiPdf()
    {
        var row = new RegistrasiCetakRow(new DateTime(2026, 11, 26), "10:00:00", new string('D', 50), "000001",
            new string('P', 40), "081234567890", "30 Th", "L", "Poli Umum", new string('J', 100), new string('A', 200),
            "Lama", new string('R', 60), "UMUM");
        var path = RegistrasiReport.GenerateRegistrasiPdf([row, row], new KopInstansi("RS UJI", "ALAMAT", "0800", "", null));
        try
        {
            Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith("%PDF-"u8));
        }
        finally { File.Delete(path); }
    }
}
