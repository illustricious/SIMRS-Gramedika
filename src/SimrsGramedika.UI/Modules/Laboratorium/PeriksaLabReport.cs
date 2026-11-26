using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SimrsGramedika.UI.Modules.Laboratorium;

/// <summary>QuestPDF stand-in for the legacy rptDataLab* Jasper reports — prints whatever the list currently shows (same pattern as RawatInapReport).</summary>
public static class PeriksaLabReport
{
    public static string GeneratePdf(string title, string[] headers, IReadOnlyList<string[]> rows, string footer)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_{title.Replace(' ', '_').Replace('&', 'n')}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(7));

            page.Header().Column(col =>
            {
                col.Item().Text("SIMRS Gramedika").FontSize(14).Bold();
                col.Item().Text($"::[ {title} ]::").FontSize(11);
                col.Item().Text($"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}  —  {footer}").FontSize(8).FontColor(Colors.Grey.Medium);
            });

            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(c => { foreach (var _ in headers) c.RelativeColumn(); });
                table.Header(h =>
                {
                    foreach (var head in headers) h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(head).Bold();
                });
                foreach (var row in rows)
                    foreach (var cell in row)
                        table.Cell().Padding(2).Text(cell);
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Halaman ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        })).GeneratePdf(path);

        return path;
    }
}
