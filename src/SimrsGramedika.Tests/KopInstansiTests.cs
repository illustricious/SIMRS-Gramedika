using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Registrasi;
using SkiaSharp;

namespace SimrsGramedika.Tests;

/// <summary>Kop rumah sakit pada cetakan daftar Registrasi.</summary>
public class KopInstansiTests
{
    private static byte[] Logo()
    {
        using var bitmap = new SKBitmap(60, 60);
        bitmap.Erase(SKColors.SteelBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Theory]
    [InlineData("ada")]
    [InlineData("kosong")]
    [InlineData("rusak")]
    public void CetakBerkopTetapJadi(string jenisLogo)
    {
        var logo = jenisLogo switch { "ada" => Logo(), "kosong" => [], _ => new byte[] { 1, 2, 3, 4 } };
        var kop = new KopInstansi("RS UJI", "JALAN UJI, KOTA UJI", "0800", "uji@example.test", logo);
        var item = new RujukanInternalItem("2026/11/26/000001", "26/11/2026", "10:00", "D2", "Dokter Tujuan", "000001",
            "PASIEN UJI", "L", "30 Th", "P2", "Poli Tujuan", "UMUM", "-", "-", "DIRI SENDIRI", "Lama", "Belum");

        var path = RegistrasiReport.GenerateRujukanPdf([item], kop);
        try
        {
            Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith("%PDF-"u8));
        }
        finally { File.Delete(path); }
    }
}
