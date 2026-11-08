using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.RawatInap;

/// <summary>Same QuestPDF pattern as RegistrasiReport — "Laporan 1"-style table of the Rawat Inap list currently on screen.</summary>
public static class RawatInapReport
{
    public static string GeneratePdf(IReadOnlyList<RawatInapListItem> items, string title)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var path = Path.Combine(Path.GetTempPath(), $"SimrsKhanza_{title.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        var headers = new[]
        {
            "No.Rawat", "No.RM", "Pasien", "Jenis Bayar", "Kamar", "Tarif", "Dx Awal", "Dx Akhir",
            "Tgl Masuk", "Tgl Keluar", "Ttl Biaya", "Stts Pulang", "Lama", "Dokter P.J.",
        };

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Column(col =>
                {
                    col.Item().Text("SIMRS Gramedika").FontSize(14).Bold();
                    col.Item().Text(title).FontSize(11);
                    col.Item().Text($"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}  —  {items.Count} data").FontSize(8).FontColor(Colors.Grey.Medium);
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
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(h).Bold();
                    });

                    foreach (var i in items)
                    {
                        foreach (var cell in new[]
                        {
                            i.NoRawat, i.NoRekamMedis, i.NamaPasien, i.JenisBayar, i.Kamar, i.TarifKamar,
                            i.DiagnosaAwal, i.DiagnosaAkhir, i.TglMasuk, i.TglKeluar, i.TotalBiaya,
                            i.StatusPulang, i.Lama, i.DokterPj,
                        })
                        {
                            table.Cell().Padding(3).Text(string.IsNullOrEmpty(cell) ? "-" : cell);
                        }
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
