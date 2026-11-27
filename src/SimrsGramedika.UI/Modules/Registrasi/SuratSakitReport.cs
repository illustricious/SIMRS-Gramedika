using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Data;

namespace SimrsGramedika.UI.Modules.Registrasi;

/// <summary>
/// Surat Cuti Sakit dengan isi rptSuratSakit Khanza ("Cetak Surat Sakit 2"), termasuk QR identitas penanda tangan.
/// </summary>
public static class SuratSakitReport
{
    public static string GeneratePdf(SuratSakitCetak surat, KopInstansi kop)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_Surat_Sakit_{Guid.NewGuid():N}.pdf");
        Dokumen(surat, kop).GeneratePdf(path);
        return path;
    }

    internal static Document Dokumen(SuratSakitCetak surat, KopInstansi kop)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        static string Tanggal(DateTime? t) => t?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";
        var awal = Tanggal(surat.Surat.TanggalAwal);
        var qr = KodeQr.Png(TandaTanganSurat.Teks(kop.Nama, kop.Kota, surat.Dokter, surat.IdDokter,
            Tanggal(surat.TanggalRegistrasi)));

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Element(c => RegistrasiReport.Kop(c, kop));

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Item().AlignCenter().Text("SURAT CUTI SAKIT").FontSize(11).Underline();
                col.Item().AlignCenter().Text($"No. {surat.Surat.NoSurat}");

                // Template Khanza menulis "dr" di depan nama; dilewati bila nama sudah memuat gelar itu.
                var gelar = surat.Dokter.StartsWith("dr", StringComparison.OrdinalIgnoreCase) ? "" : "dr ";
                col.Item().PaddingTop(12).Text($"Yang bertanda tangan dibawah ini {gelar}{surat.Dokter}");
                col.Item().Text($"Dokter pada {kop.Nama}, menerangkan bahwa :");

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(80);
                        c.ConstantColumn(10);
                        c.RelativeColumn();
                    });
                    foreach (var (judul, isi) in new[]
                    {
                        ("Nama Pasien", surat.Pasien), ($"Nama {surat.HubunganKeluarga}", surat.NamaKeluarga),
                        ("Tanggal Lahir", Tanggal(surat.TanggalLahir)), ("Jenis Kelamin", surat.JenisKelamin),
                        ("Pekerjaan", surat.Pekerjaan), ("Alamat", surat.Alamat),
                    })
                    {
                        table.Cell().PaddingVertical(1.5f).Text(judul);
                        table.Cell().PaddingVertical(1.5f).Text(":");
                        table.Cell().PaddingVertical(1.5f).Text(isi);
                    }
                });

                col.Item().PaddingTop(8).Text(
                    "Karena sakit yang dideritanya/sembuh dari sakit/habis bersalin/prematur*) maka perlu cuti selama :");
                col.Item().Text($"{surat.Surat.LamaSakit} Hari dari tanggal {awal} s/d {Tanggal(surat.Surat.TanggalAkhir)}");
                col.Item().Text("Demikian untuk menjadi periksa dan guna seperlunya.");

                col.Item().PaddingTop(18).ShowEntire().AlignRight().Width(190).Column(ttd =>
                {
                    ttd.Item().AlignCenter().Text($"{kop.Kota}, {awal}");
                    ttd.Item().AlignCenter().Text(kop.Nama);
                    ttd.Item().AlignCenter().Text("Dokter,");
                    ttd.Item().PaddingVertical(4).AlignCenter().Width(66).Image(qr);
                    ttd.Item().AlignCenter().Text($"( {surat.Dokter} )");
                });

                col.Item().PaddingTop(12).Text("*) Coret yang tidak perlu").FontSize(8);
            });
        }));
    }
}
