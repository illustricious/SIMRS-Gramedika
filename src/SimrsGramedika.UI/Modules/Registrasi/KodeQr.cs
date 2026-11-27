using QRCoder;

namespace SimrsGramedika.UI.Modules.Registrasi;

internal static class KodeQr
{
    /// <summary>Gambar PNG kode QR; koreksi galat tingkat H seperti template surat Khanza.</summary>
    public static byte[] Png(string teks)
    {
        using var pembuat = new QRCodeGenerator();
        using var data = pembuat.CreateQrCode(teks, QRCodeGenerator.ECCLevel.H);
        using var gambar = new PngByteQRCode(data);
        return gambar.GetGraphic(8);
    }
}
