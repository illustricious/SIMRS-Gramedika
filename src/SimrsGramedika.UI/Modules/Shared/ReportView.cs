using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.UI.Modules.Shared;

/// <summary>Generic read-only report screen for a <see cref="ReportSpec"/>, with CSV export and a letterhead PDF.</summary>
public class ReportView : PageView
{
    private readonly ReportSpec _spec;
    private readonly ReportRepository _repo = new(DatabaseSettings.Load());
    private readonly DataGrid _grid;
    private readonly TextBox _cari;
    private readonly PeriodPicker? _period;
    private readonly NumericUpDown _tahun = new() { Minimum = 2000, Maximum = 2100, Value = DateTime.Today.Year, Width = 110, FormatString = "0" };
    private List<Row> _rows = [];
    // Keterangan dan total milik data yang sedang tampil, bukan isian filter terkini.
    private string _keterangan = "";
    private string[] _total = [];
    private readonly WrapPanel _bar;

    private readonly string? _visit;

    private readonly string? _noRekamMedis;

    /// <param name="noRekamMedis">Batasi hasil ke satu pasien (dibuka dari menu klik kanan).</param>
    public ReportView(ReportSpec spec, string? visit = null, string? noRekamMedis = null)
    {
        _spec = spec;
        _visit = visit;
        _noRekamMedis = noRekamMedis;
        _cari = Ui.Text(spec.SearchWatermark);
        // Query dipersempit dengan No. RM; hasilnya masih disaring persis di ReloadAsync.
        _cari.Text = noRekamMedis;
        _grid = Ui.Grid(spec.Columns.Select(c => new Col(c.Column, c.Header)).ToArray());
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await ReloadAsync(); };

        var filters = new List<Control> { _cari };
        if (noRekamMedis is not null)
        {
            // Pencarian tetap berlaku di dalam data pasien ini.
            filters.Add(Ui.L($"Pasien No. RM: {noRekamMedis}"));
        }
        if (visit is not null) filters.Add(Ui.L($"No. Rawat: {visit}"));
        else if (spec.Period == ReportPeriod.Year) { filters.Add(Ui.L("Tahun")); filters.Add(_tahun); }
        else if (spec.Period != ReportPeriod.None) filters.Add(_period = new PeriodPicker(spec.Period == ReportPeriod.Month,
            spec.Period == ReportPeriod.DateRange ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : null));
        var tampilkan = Ui.Btn("Tampilkan", async (_, _) => await ReloadAsync());
        tampilkan.Classes.Add("accent");
        filters.Add(tampilkan);
        filters.Add(Ui.Btn("Ekspor CSV", async (_, _) => await ExportAsync()));
        filters.Add(Ui.Btn("Cetak PDF", async (_, _) => await CetakAsync()));

        _bar = Ui.Bar(filters.ToArray());
        _bar.Margin = default;
        Build(spec.Title, _grid, spec.Hint, _bar);
    }

    /// <summary>Perintah tambahan yang menerima baris terpilih, bila ada.</summary>
    public void TambahAksi(string label, Func<Row?, Task> aksi) => _bar.Children.Add(Ui.Btn(label, async (_, _) =>
    {
        try { await aksi(Ui.Selected<Row>(_grid)); }
        catch (Exception ex) { Ui.Err(StatusText, ex.Message); }
    }));

    public override async Task ReloadAsync() => await RunAsync(async () =>
    {
        var keterangan = Keterangan();
        _rows = _visit is not null ? await _repo.RunForVisitAsync(_spec, _visit, _cari.Text) : await _repo.RunAsync(_spec, _cari.Text, _period?.From ?? DateTime.Today, _period?.To ?? DateTime.Today,
            (int)(_tahun.Value ?? DateTime.Today.Year));
        if (_noRekamMedis is not null) _rows = MilikPasien(_rows, _noRekamMedis);
        Ui.Fill(_grid, _rows);
        _keterangan = keterangan;
        _total = _spec.TotalColumns.Select(column =>
            $"{_spec.Columns.First(c => c.Column == column).Header}: {_rows.Sum(row => row.D(column)):N2}").ToArray();
        Ui.Info(StatusText, string.Join(" | ", new[] { $"{_rows.Count} baris" }.Concat(_total)));
    });

    private string Keterangan()
    {
        var bagian = new List<string>();
        if (_noRekamMedis is not null) bagian.Add($"No. RM: {_noRekamMedis}");
        if (_visit is not null) bagian.Add($"No. Rawat: {_visit}");
        else if (_spec.Period == ReportPeriod.Year) bagian.Add($"Tahun {(int)(_tahun.Value ?? DateTime.Today.Year)}");
        else if (_period is not null) bagian.Add($"Periode {_period.From:dd/MM/yyyy} s.d. {_period.To:dd/MM/yyyy}");
        var cari = _cari.Text?.Trim() ?? "";
        if (cari.Length > 0 && cari != _noRekamMedis) bagian.Add($"Pencarian: {cari}");
        return string.Join(" | ", bagian);
    }

    private async Task CetakAsync() => await RunAsync(async () =>
    {
        var kolom = _spec.Columns.Select(c => new Col(c.Column, c.Header)).ToArray();
        var path = ReportPdf.GeneratePdf(_spec.Title, _keterangan, kolom, _rows, _total,
            await KopInstansi.MuatAsync(DatabaseSettings.Load()));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }, "PDF dibuat.");

    /// <summary>LIKE pada pencarian bisa mencocokkan No. RM di dalam No. Rawat pasien lain.</summary>
    internal static List<Row> MilikPasien(IEnumerable<Row> rows, string noRekamMedis) =>
        rows.Where(row => row.S("no_rkm_medis") == noRekamMedis).ToList();

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
