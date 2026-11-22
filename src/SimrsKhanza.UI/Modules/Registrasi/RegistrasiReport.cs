using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Registrasi;

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
    public static string GeneratePdf(IReadOnlyList<RegistrasiListItem> items, string title)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var path = Path.Combine(Path.GetTempPath(), $"SimrsKhanza_{title.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text("SIMRS Khanza").FontSize(14).Bold();
                    col.Item().Text(title).FontSize(11);
                    col.Item().Text($"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        for (var i = 0; i < 10; i++) c.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in new[] { "No.Rawat", "No.RM", "Pasien", "Poli", "Dokter", "Jenis Bayar", "Status", "Status Bayar", "Tanggal", "Jam" })
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(h).Bold();
                        }
                    });

                    foreach (var item in items)
                    {
                        table.Cell().Padding(3).Text(item.NoRawat);
                        table.Cell().Padding(3).Text(item.NoRekamMedis);
                        table.Cell().Padding(3).Text(item.NamaPasien);
                        table.Cell().Padding(3).Text(item.NamaPoli ?? "-");
                        table.Cell().Padding(3).Text(item.NamaDokter ?? "-");
                        table.Cell().Padding(3).Text(item.JenisBayar ?? "-");
                        table.Cell().Padding(3).Text(item.Status ?? "-");
                        table.Cell().Padding(3).Text(item.StatusBayar ?? "-");
                        table.Cell().Padding(3).Text(item.TanggalRegistrasi?.ToString("dd/MM/yyyy") ?? "-");
                        table.Cell().Padding(3).Text(item.JamRegistrasi?.ToString("HH:mm") ?? "-");
                    }
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
}
