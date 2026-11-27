using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Data;

namespace SimrsGramedika.UI.Modules.Registrasi;

/// <summary>
/// Surat Rujukan dengan isi rptSuratRujukan Khanza, termasuk QR identitas penanda tangan.
/// </summary>
public static class SuratRujukanReport
{
    public static string GeneratePdf(SuratRujukanData surat, KopInstansi kop)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_Surat_Rujukan_{Guid.NewGuid():N}.pdf");
        Dokumen(surat, kop).GeneratePdf(path);
        return path;
    }

    internal static Document Dokumen(SuratRujukanData surat, KopInstansi kop)
    {
        var kota = kop.Kota;
        QuestPDF.Settings.License = LicenseType.Community;
        static string Tanggal(DateTime? t) => t?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";
        // Seperti DlgRujuk, tanggal pada QR berbentuk yyyy-MM-dd.
        var qr = KodeQr.Png(TandaTanganSurat.Teks(kop.Nama, kota, surat.Dokter, surat.IdDokter,
            surat.TanggalRujuk?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""));

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(11));

            page.Header().Element(c => RegistrasiReport.Kop(c, kop));

            page.Content().PaddingTop(14).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(nomor =>
                    {
                        nomor.Item().Text($"Nomor : {surat.NoRujuk}");
                        nomor.Item().PaddingTop(6).Text("Hal      : Surat Rujukan");
                    });
                    row.AutoItem().Text($"{kota}, {Tanggal(surat.TanggalRujuk)}");
                });

                col.Item().PaddingTop(20).Text("Kepada Yth.");
                col.Item().PaddingTop(4).Text(surat.RujukKe);
                col.Item().PaddingTop(4).Text("Di Tempat");

                col.Item().PaddingTop(20).Text("Bersama ini kami beritahukan bahwa kami telah merawat / memeriksa pasien berikut ini.");
                col.Item().PaddingTop(4).Text("Mohon pemeriksaan dan penanganan lebih lanjut penderita :");

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(105);
                        c.ConstantColumn(12);
                        c.RelativeColumn();
                    });
                    foreach (var (judul, isi) in new[]
                    {
                        ("Tanggal Rawat", Tanggal(surat.TanggalRegistrasi)), ("Nama Pasien", surat.Pasien),
                        ("No. Rekam Medis", surat.NoRekamMedis), ("Alamat", surat.Alamat), ("Diagnosa", surat.Diagnosa),
                        ("Tindakan", surat.Tindakan), ("Terapi", surat.Terapi),
                    })
                    {
                        table.Cell().PaddingVertical(3).Text(judul);
                        table.Cell().PaddingVertical(3).Text(":");
                        table.Cell().PaddingVertical(3).Text(isi);
                    }
                });

                col.Item().PaddingTop(18).Text(
                    $"Demikianlah riwayat perawatan selama di {kop.Nama} dengan diagnosa akhir {surat.Diagnosa}. " +
                    "Atas kerjasamanya kami ucapkan terima kasih");

                // Blok tanda tangan tidak boleh terpisah halaman dari nama dokter.
                col.Item().PaddingTop(28).ShowEntire().AlignRight().Width(230).Column(ttd =>
                {
                    ttd.Item().AlignCenter().Text(kop.Nama);
                    ttd.Item().AlignCenter().Text(kota);
                    ttd.Item().AlignCenter().Text("Dokter yang merawat,");
                    ttd.Item().PaddingVertical(4).AlignCenter().Width(78).Image(qr);
                    ttd.Item().AlignCenter().Text($"( {surat.Dokter} )");
                });
            });
        }));
    }
}
