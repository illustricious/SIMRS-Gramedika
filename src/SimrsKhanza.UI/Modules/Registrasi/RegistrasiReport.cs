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
    public static string GeneratePdf(IReadOnlyList<RegistrasiListItem> items, string title) =>
        GeneratePdf(title, ["No.Rawat", "No.RM", "Pasien", "Poli", "Dokter", "Jenis Bayar", "Status", "Status Bayar", "Tanggal", "Jam"],
            items.Select<RegistrasiListItem, string[]>(item =>
                [item.NoRawat, item.NoRekamMedis, item.NamaPasien, item.NamaPoli ?? "-", item.NamaDokter ?? "-",
                 item.JenisBayar ?? "-", item.Status ?? "-", item.StatusBayar ?? "-",
                 item.TanggalRegistrasi?.ToString("dd/MM/yyyy") ?? "-", item.JamRegistrasi?.ToString("HH:mm") ?? "-"]));

    public static string GenerateRujukanPdf(IReadOnlyList<RujukanInternalItem> items) =>
        GeneratePdf("Daftar Rujukan Internal Poli",
            ["No.Rawat", "No.RM", "Pasien", "Poli Tujuan", "Dokter Tujuan", "Jenis Bayar", "Status", "Tanggal", "Jam"],
            items.Select<RujukanInternalItem, string[]>(item =>
                [item.NoRawat, item.NoRm, item.Pasien, item.Poli, item.Dokter, item.JenisBayar, item.Status, item.Tanggal, item.Jam]));

    private static string GeneratePdf(string title, string[] headers, IEnumerable<string[]> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var path = Path.Combine(Path.GetTempPath(), $"SimrsKhanza_{title.Replace(' ', '_')}_{Guid.NewGuid():N}.pdf");

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
                        foreach (var _ in headers) c.RelativeColumn();
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
}
