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
    private readonly TextBox _cari = Ui.Text("Cari...");
    private readonly PeriodPicker? _period;
    private readonly NumericUpDown _tahun = new() { Minimum = 2000, Maximum = 2100, Value = DateTime.Today.Year, Width = 110, FormatString = "0" };
    private List<Row> _rows = [];

    public ReportView(ReportSpec spec)
    {
        _spec = spec;
        _grid = Ui.Grid(spec.Columns.Select(c => new Col(c.Column, c.Header)).ToArray());
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await ReloadAsync(); };

        var filters = new List<Control> { _cari };
        if (spec.Period == ReportPeriod.Year) { filters.Add(Ui.L("Tahun")); filters.Add(_tahun); }
        else filters.Add(_period = new PeriodPicker(spec.Period == ReportPeriod.Month,
            spec.Period == ReportPeriod.DateRange ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : null));
        filters.Add(Ui.Btn("🔎 Tampilkan", async (_, _) => await ReloadAsync()));
        filters.Add(Ui.Btn("📄 Ekspor CSV", async (_, _) => await ExportAsync()));

        var body = new DockPanel();
        var bar = Ui.Bar(filters.ToArray());
        DockPanel.SetDock(bar, Dock.Top);
        body.Children.Add(bar);
        body.Children.Add(_grid);
        Build(spec.Title, body, spec.Hint ?? "Laporan dari tabel SIMRS Khanza yang sama.");
    }

    public override async Task ReloadAsync() => await RunAsync(async () =>
    {
        _rows = await _repo.RunAsync(_spec, _cari.Text, _period?.From ?? DateTime.Today, _period?.To ?? DateTime.Today,
            (int)(_tahun.Value ?? DateTime.Today.Year));
        Ui.Fill(_grid, _rows);
        Ui.Info(StatusText, $"{_rows.Count} baris");
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
            foreach (var r in _rows) sb.AppendLine(string.Join(";", cols.Select(c => Q(Ui.Fmt(r, c)))));
            await using var s = await file.OpenWriteAsync();
            await using var w = new StreamWriter(s, new UTF8Encoding(true));
            await w.WriteAsync(sb.ToString());
        }, "CSV tersimpan.");
    }
}
