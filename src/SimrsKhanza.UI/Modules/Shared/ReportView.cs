using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using SimrsKhanza.Config;
using SimrsKhanza.Data.Master;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.UI.Modules.Shared;

/// <summary>Generic read-only report screen for a <see cref="ReportSpec"/>, with CSV export.</summary>
public class ReportView : PageView
{
    private readonly ReportSpec _spec;
    private readonly ReportRepository _repo = new(DatabaseSettings.Load());
    private readonly DataGrid _grid;
    private readonly TextBox _cari;
    private readonly PeriodPicker? _period;
    private readonly NumericUpDown _tahun = new() { Minimum = 2000, Maximum = 2100, Value = DateTime.Today.Year, Width = 110, FormatString = "0" };
    private List<Row> _rows = [];

    private readonly string? _visit;

    public ReportView(ReportSpec spec, string? visit = null)
    {
        _spec = spec;
        _visit = visit;
        _cari = Ui.Text(spec.SearchWatermark);
        _grid = Ui.Grid(spec.Columns.Select(c => new Col(c.Column, c.Header)).ToArray());
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await ReloadAsync(); };

        var filters = new List<Control> { _cari };
        if (visit is not null) filters.Add(Ui.L($"No. Rawat: {visit}"));
        else if (spec.Period == ReportPeriod.Year) { filters.Add(Ui.L("Tahun")); filters.Add(_tahun); }
        else if (spec.Period != ReportPeriod.None) filters.Add(_period = new PeriodPicker(spec.Period == ReportPeriod.Month,
            spec.Period == ReportPeriod.DateRange ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : null));
        var tampilkan = Ui.Btn("Tampilkan", async (_, _) => await ReloadAsync());
        tampilkan.Classes.Add("accent");
        filters.Add(tampilkan);
        filters.Add(Ui.Btn("Ekspor CSV", async (_, _) => await ExportAsync()));

        var body = new DockPanel();
        var bar = Ui.Bar(filters.ToArray());
        DockPanel.SetDock(bar, Dock.Top);
        body.Children.Add(bar);
        body.Children.Add(_grid);
        Build(spec.Title, body, spec.Hint);
    }

    public override async Task ReloadAsync() => await RunAsync(async () =>
    {
        _rows = _visit is not null ? await _repo.RunForVisitAsync(_spec, _visit, _cari.Text) : await _repo.RunAsync(_spec, _cari.Text, _period?.From ?? DateTime.Today, _period?.To ?? DateTime.Today,
            (int)(_tahun.Value ?? DateTime.Today.Year));
        Ui.Fill(_grid, _rows);
        var totals = _spec.TotalColumns.Select(column =>
            $"{_spec.Columns.First(c => c.Column == column).Header}: {_rows.Sum(row => row.D(column)):N2}");
        Ui.Info(StatusText, string.Join(" | ", new[] { $"{_rows.Count} baris" }.Concat(totals)));
    });

    private async Task ExportAsync()
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Simpan CSV",
            SuggestedFileName = string.Concat(_spec.Title.Split(Path.GetInvalidFileNameChars())) + ".csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
        });
        if (file is null) return;
        await RunAsync(async () =>
        {
            var cols = _spec.Columns.Select(c => new Col(c.Column, c.Header)).ToArray();
            static string Q(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", cols.Select(c => Q(c.Header))));
            foreach (var r in _rows)
                sb.AppendLine(string.Join(";", cols.Select(c => Q(CsvCell(r, c)))));
            await using var s = await file.OpenWriteAsync();
            await using var w = new StreamWriter(s, new UTF8Encoding(true));
            await w.WriteAsync(sb.ToString());
        }, "CSV tersimpan.");
    }

    private static string CsvCell(Row row, Col column)
    {
        var value = Ui.Fmt(row, column);
        if (row[column.Name] is string && value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@')
            return "'" + value;
        return value;
    }
}
