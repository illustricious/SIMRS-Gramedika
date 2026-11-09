using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SimrsKhanza.UI.Modules.Laboratorium;

/// <summary>Same QuestPDF pattern as RawatInapReport — a plain table of the permintaan list currently on screen.</summary>
public static class PermintaanLabReport
{
    public static string GeneratePdf(string title, string[] headers, IReadOnlyList<string[]> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var path = Path.Combine(Path.GetTempPath(), $"SimrsKhanza_{title.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(6));

                page.Header().Column(col =>
                {
                    col.Item().Text("SIMRS Gramedika").FontSize(14).Bold();
                    col.Item().Text(title).FontSize(11);
                    col.Item().Text($"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}  -  {rows.Count} data").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var _ in headers) c.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(2).Text(h).Bold();
                    });

                    foreach (var row in rows)
                        foreach (var cell in row)
                            table.Cell().Padding(2).Text(string.IsNullOrEmpty(cell) ? "-" : cell);
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
