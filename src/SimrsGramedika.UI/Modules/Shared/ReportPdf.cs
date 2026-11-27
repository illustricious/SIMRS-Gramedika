using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Registrasi;

namespace SimrsGramedika.UI.Modules.Shared;

/// <summary>Cetakan berkop untuk <see cref="ReportView"/>: kolom dan format nilainya sama dengan tabel di layar.</summary>
internal static class ReportPdf
{
    /// <summary>Lebih dari ini hasil cetaknya ratusan halaman; pakai ekspor CSV.</summary>
    public const int BatasBaris = 3000;

    public static string GeneratePdf(string judul, string keterangan, IReadOnlyList<Col> kolom, IReadOnlyList<Row> baris,
        IReadOnlyList<string> total, KopInstansi kop)
    {
        if (baris.Count == 0) throw new InvalidOperationException("Tampilkan data lebih dulu; belum ada baris untuk dicetak.");
        if (baris.Count > BatasBaris)
            throw new InvalidOperationException($"Cetak PDF dibatasi {BatasBaris} baris. Persempit pencarian atau periode, atau pakai Ekspor CSV.");

        QuestPDF.Settings.License = LicenseType.Community;
        var isi = baris.Select(r => kolom.Select(c => Ui.Fmt(r, c)).ToArray()).ToList();
        // Lebar kolom mengikuti isi terpanjang, dibatasi agar satu kolom tidak menghabiskan halaman.
        var bobot = kolom.Select((c, i) => (float)Math.Clamp(Math.Max(c.Header.Length, isi.Max(r => r[i].Length)), 5, 30)).ToArray();
        var huruf = kolom.Count > 14 ? 6f : kolom.Count > 9 ? 7f : 8f;
        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_Laporan_{Guid.NewGuid():N}.pdf");

        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(huruf));
            page.Header().Column(col =>
            {
                col.Item().Element(c => RegistrasiReport.Kop(c, kop));
                col.Item().PaddingTop(4).AlignCenter().Text(judul.ToUpperInvariant()).FontSize(11).Bold();
                if (keterangan.Length > 0) col.Item().AlignCenter().Text(keterangan).FontSize(8);
                col.Item().PaddingBottom(4).AlignRight().Text($"Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7);
            });
            page.Footer().AlignRight().Text(t =>
            {
                t.Span("Halaman ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
            page.Content().Column(col =>
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { foreach (var lebar in bobot) c.RelativeColumn(lebar); });
                    table.Header(h =>
                    {
                        foreach (var c in kolom)
                            h.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(2).Text(c.Header).Bold();
                    });
                    foreach (var row in isi)
                        foreach (var sel in row)
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(2).Text(sel);
                });
                col.Item().PaddingTop(6).Text(string.Join(" | ", new[] { $"{baris.Count} baris" }.Concat(total))).Bold();
            });
        })).GeneratePdf(path);
        return path;
    }
}
