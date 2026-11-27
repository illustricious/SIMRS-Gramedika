using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Data;
using SkiaSharp;

namespace SimrsGramedika.UI.Modules.Registrasi;

/// <summary>
/// First real "Cetak" (print) feature in the scaffold — generates an actual
/// PDF of the Registrasi/Rawat Jalan list using QuestPDF, replacing the
/// legacy's 1,313 JasperReports .jrxml templates for this one screen. This
/// establishes the pattern (build a Document, save to a temp file, open with
/// the OS default PDF viewer) that the rest of the .jrxml backlog can follow
/// — see docs/handover/04-HISTORY.md for the full list still to port.
///
/// Licensing note: QuestPDF's Community license is free only for
/// organizations under a revenue threshold (see questpdf.com/license) —
/// confirm eligibility, or switch to a commercial QuestPDF license, before
/// this is used in a real hospital's production deployment.
/// </summary>
public static class RegistrasiReport
{
    public static string GeneratePdf(IReadOnlyList<RegistrasiListItem> items, string title, KopInstansi? kop = null) =>
        GeneratePdf(kop, title, ["No.Rawat", "No.RM", "Pasien", "Poli", "Dokter", "Jenis Bayar", "Status", "Status Bayar", "Tanggal", "Jam"],
            items.Select<RegistrasiListItem, string[]>(item =>
                [item.NoRawat, item.NoRekamMedis, item.NamaPasien, item.NamaPoli ?? "-", item.NamaDokter ?? "-",
                 item.JenisBayar ?? "-", item.Status ?? "-", item.StatusBayar ?? "-",
                 item.TanggalRegistrasi?.ToString("dd/MM/yyyy") ?? "-", item.JamRegistrasi?.ToString("HH:mm") ?? "-"]));

    public static string GenerateRujukanPdf(IReadOnlyList<RujukanInternalItem> items, KopInstansi? kop = null) =>
        GeneratePdf(kop, "Daftar Rujukan Internal Poli",
            ["No.Rawat", "No.RM", "Pasien", "Poli Tujuan", "Dokter Tujuan", "Jenis Bayar", "Status", "Tanggal", "Jam"],
            items.Select<RujukanInternalItem, string[]>(item =>
                [item.NoRawat, item.NoRm, item.Pasien, item.Poli, item.Dokter, item.JenisBayar, item.Status, item.Tanggal, item.Jam]));

    /// <summary>Kolom dan lebarnya (poin) sama dengan rptReg.jrxml Khanza.</summary>
    internal static readonly (string Judul, float Lebar, Func<RegistrasiCetakRow, string> Isi)[] KolomRegistrasi =
    [
        ("TGL.REGIS.", 50, r => r.Tanggal?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? ""),
        ("JAM", 37, r => r.Jam), ("DOKTER", 104, r => r.Dokter), ("NO.R.M.", 53, r => r.NoRm),
        ("NAMA PASIEN", 134, r => r.Pasien), ("NO.TELP", 58, r => r.NoTelp), ("UMUR", 30, r => r.Umur),
        ("J.K.", 21, r => r.JenisKelamin), ("POLIKLINIK", 56, r => r.Poli), ("P.JAWAB", 57, r => r.PenanggungJawab),
        ("ALAMAT P.J.", 92, r => r.AlamatPj), ("STATUS", 50, r => r.StatusDaftar),
        ("ASAL RUJUKAN", 74, r => r.AsalRujukan), ("JENIS BAYAR", 57, r => r.JenisBayar),
    ];

    /// <summary>"Data Registrasi Periksa" seperti cetak daftar di Registrasi Khanza.</summary>
    public static string GenerateRegistrasiPdf(IReadOnlyList<RegistrasiCetakRow> rows, KopInstansi kop)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_Data_Registrasi_{Guid.NewGuid():N}.pdf");

        Document.Create(container => container.Page(page =>
        {
            page.Size(913, 595);
            page.Margin(20);
            // Sedikit lebih kecil dari Tahoma 8 Khanza agar nomor telepon muat satu baris.
            page.DefaultTextStyle(x => x.FontSize(7.5f));

            page.Header().Column(col =>
            {
                col.Item().Element(c => Kop(c, kop));
                col.Item().PaddingVertical(5).AlignCenter().Text("DATA REGISTRASI PERIKSA").FontSize(11);
            });

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    foreach (var kolom in KolomRegistrasi) c.ConstantColumn(kolom.Lebar);
                });
                table.Header(header =>
                {
                    foreach (var kolom in KolomRegistrasi)
                        header.Cell().BorderTop(1).BorderBottom(1).Background("#F0F0DC").PaddingVertical(2).PaddingHorizontal(1).Text(kolom.Judul);
                });
                foreach (var row in rows)
                    foreach (var kolom in KolomRegistrasi)
                        // ShowEntire: satu baris tidak terbelah dua halaman.
                        table.Cell().ShowEntire().PaddingVertical(1).PaddingHorizontal(1).Text(kolom.Isi(row));
            });

            page.Footer().BorderTop(0.5f).BorderBottom(0.5f).PaddingVertical(4).Row(row =>
            {
                row.RelativeItem().Text($"{DateTime.Now:dd/MM/yyyy HH:mm}");
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        })).GeneratePdf(path);

        return path;
    }

    private static string GeneratePdf(KopInstansi? kop, string title, string[] headers, IEnumerable<string[]> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_{title.Replace(' ', '_')}_{Guid.NewGuid():N}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    if (kop is not null) col.Item().Element(c => Kop(c, kop));
                    col.Item().PaddingTop(4).AlignCenter().Text(title.ToUpperInvariant()).FontSize(11).Bold();
                    col.Item().AlignRight().Text($"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        // No. rawat dan nama pasien diberi ruang lebih agar tidak terpotong ke baris kedua.
                        foreach (var h in headers) c.RelativeColumn(h is "No.Rawat" ? 1.35f : h is "Pasien" ? 1.7f : 1f);
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(h).Bold();
                        }
                    });

                    foreach (var row in rows)
                        foreach (var value in row)
                            table.Cell().Padding(3).Text(value);
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Halaman ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    /// <summary>Kop seperti rptReg Khanza: logo di kiri, identitas rumah sakit di tengah, lalu garis.</summary>
    internal static void Kop(IContainer container, KopInstansi kop) => container
        // Garis ganda di bawah kop, seperti rptReg.
        .BorderBottom(0.7f).PaddingBottom(1.2f).BorderBottom(0.7f).PaddingBottom(4).Row(row =>
    {
        const float lebarLogo = 52;
        var kiri = row.ConstantItem(lebarLogo);
        if (BisaDigambar(kop.Logo)) kiri.Height(48).Image(kop.Logo!).FitArea();
        row.RelativeItem().Column(teks =>
        {
            teks.Item().AlignCenter().Text(kop.Nama).FontSize(12);
            foreach (var baris in new[] { kop.Alamat, kop.Kontak, kop.Email.Length == 0 ? "" : $"E-mail : {kop.Email}" })
                if (!string.IsNullOrWhiteSpace(baris)) teks.Item().AlignCenter().Text(baris).FontSize(9);
        });
        row.ConstantItem(lebarLogo); // penyeimbang agar teks tepat di tengah halaman
    });

    /// <summary>Logo kosong atau rusak dilewati; cetakan tetap jadi.</summary>
    private static bool BisaDigambar(byte[]? logo)
    {
        if (logo is not { Length: > 0 }) return false;
        using var gambar = SKImage.FromEncodedData(logo);
        return gambar is not null;
    }
}
