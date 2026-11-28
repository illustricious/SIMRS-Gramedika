using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Registrasi;

namespace SimrsGramedika.UI.Modules.RekamMedis;

/// <summary>Surat Keterangan Lahir berkop. Susunannya sendiri, bukan salinan templat Jasper Khanza.</summary>
internal static class KelahiranBayiReport
{
    public static string GeneratePdf(KelahiranBayi bayi, KopInstansi kop, DateOnly hariIni)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_Surat_Lahir_{Guid.NewGuid():N}.pdf");
        Dokumen(bayi, kop, hariIni).GeneratePdf(path);
        return path;
    }

    internal static Document Dokumen(KelahiranBayi bayi, KopInstansi kop, DateOnly hariIni)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var id = CultureInfo.GetCultureInfo("id-ID");
        static string Isi(string teks) => string.IsNullOrWhiteSpace(teks) ? "-" : teks;

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Element(c => RegistrasiReport.Kop(c, kop));
            page.Content().PaddingTop(14).Column(col =>
            {
                col.Item().AlignCenter().Text("SURAT KETERANGAN LAHIR").FontSize(13).Bold().Underline();
                col.Item().AlignCenter().Text($"No. {bayi.NoSkl}");
                col.Item().PaddingTop(14).Text(
                    $"Yang bertanda tangan di bawah ini menerangkan bahwa pada hari {bayi.TanggalLahir.ToString("dddd", id)}, " +
                    $"tanggal {bayi.TanggalLahir.ToString("d MMMM yyyy", id)}, pukul {bayi.JamLahir:HH\\:mm} telah lahir seorang bayi " +
                    $"{(bayi.JenisKelamin == JenisKelamin.Perempuan ? "perempuan" : "laki-laki")} di {kop.Nama}.");

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(130);
                        c.ConstantColumn(12);
                        c.RelativeColumn();
                    });
                    foreach (var (judul, isi) in new[]
                    {
                        ("Nama Bayi", bayi.Nama), ("No. Rekam Medis", bayi.NoRekamMedis), ("Anak Ke", bayi.AnakKe),
                        ("Berat Badan", bayi.BeratBadan), ("Panjang Badan", bayi.PanjangBadan), ("Lingkar Kepala", bayi.LingkarKepala),
                        ("Nama Ibu", bayi.NamaIbu), ("Umur Ibu", bayi.UmurIbu), ("Nama Ayah", bayi.NamaAyah), ("Umur Ayah", bayi.UmurAyah),
                        ("Alamat", bayi.Alamat),
                    })
                    {
                        table.Cell().PaddingVertical(2).Text(judul);
                        table.Cell().PaddingVertical(2).Text(":");
                        table.Cell().PaddingVertical(2).Text(Isi(isi));
                    }
                });

                col.Item().PaddingTop(12).Text("Demikian surat keterangan ini dibuat untuk dipergunakan sebagaimana mestinya.");
                col.Item().PaddingTop(24).ShowEntire().AlignRight().Width(220).Column(ttd =>
                {
                    ttd.Item().AlignCenter().Text($"{kop.Kota}, {hariIni.ToString("d MMMM yyyy", id)}");
                    ttd.Item().AlignCenter().Text("Penolong Persalinan,");
                    ttd.Item().PaddingTop(54).AlignCenter().Text($"( {Isi(bayi.NamaPenolong)} )");
                });
            });
        }));
    }
}
