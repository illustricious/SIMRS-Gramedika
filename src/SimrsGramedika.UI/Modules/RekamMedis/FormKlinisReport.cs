using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Data;
using SimrsGramedika.Data.RekamMedis;
using SimrsGramedika.UI.Modules.Registrasi;

namespace SimrsGramedika.UI.Modules.RekamMedis;

/// <summary>
/// Cetakan umum satu catatan rekam medis: kop RS, identitas pasien, seluruh isian, dan QR identitas pemilik catatan.
/// Susunannya tidak meniru templat Jasper Khanza per formulir.
/// </summary>
public static class FormKlinisReport
{
    public static string GeneratePdf(FormKlinis form, CetakKlinis cetak, KopInstansi kop)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_{form.Tabel}_{Guid.NewGuid():N}.pdf");
        Dokumen(form, cetak, kop).GeneratePdf(path);
        return path;
    }

    internal static Document Dokumen(FormKlinis form, CetakKlinis cetak, KopInstansi kop)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        static string Tanggal(DateTime? t) => t?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";
        var (kunjungan, catatan) = (cetak.Kunjungan, cetak.Catatan);
        var waktu = catatan.Waktu.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var qr = KodeQr.Png(TandaTanganSurat.Teks(kop.Nama, kop.Kota, catatan.NamaPemilik, cetak.IdPemilik,
            Tanggal(catatan.Waktu)));
        var sebutan = char.ToUpperInvariant(form.SebutanPemilik[0]) + form.SebutanPemilik[1..];

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Element(c => RegistrasiReport.Kop(c, kop));
            page.Footer().AlignRight().Text(t =>
            {
                t.Span($"{kunjungan.NoRawat} · halaman ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Item().PaddingBottom(6).AlignCenter().Text(form.Judul.ToUpperInvariant()).FontSize(12).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(85);
                        c.RelativeColumn(3);
                        c.ConstantColumn(85);
                        c.RelativeColumn(2);
                    });
                    foreach (var isi in new[]
                    {
                        "Nama Pasien", kunjungan.Pasien, "No. Rekam Medis", kunjungan.NoRekamMedis,
                        "Jenis Kelamin", kunjungan.JenisKelamin, "Tgl Lahir", Tanggal(kunjungan.TanggalLahir),
                        "Umur", kunjungan.Umur, "No. Rawat", kunjungan.NoRawat,
                        "Waktu Catatan", waktu, sebutan, catatan.NamaPemilik,
                    })
                        table.Cell().PaddingVertical(1.5f).PaddingRight(4).Text(isi);
                });

                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(170);
                        c.RelativeColumn();
                    });
                    foreach (var isian in form.Isian)
                    {
                        if (isian.Bagian is { } bagian)
                            table.Cell().ColumnSpan(2).PaddingTop(8).PaddingBottom(2).Text(bagian).Bold();
                        var nilai = catatan.Isi.GetValueOrDefault(isian.Kolom, "");
                        table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingRight(6).Text(isian.Label);
                        table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).Text(nilai.Length == 0 ? "-" : nilai);
                    }
                });

                if (form.Keterangan?.Invoke(catatan.Isi) is { Length: > 0 } keterangan)
                    col.Item().PaddingTop(8).Text(keterangan).Bold();

                col.Item().PaddingTop(18).ShowEntire().AlignRight().Width(190).Column(ttd =>
                {
                    ttd.Item().AlignCenter().Text($"{sebutan},");
                    ttd.Item().PaddingVertical(4).AlignCenter().Width(66).Image(qr);
                    ttd.Item().AlignCenter().Text($"( {catatan.NamaPemilik} )");
                });
            });
        }));
    }
}
