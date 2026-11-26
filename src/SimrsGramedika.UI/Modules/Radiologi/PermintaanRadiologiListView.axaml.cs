using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Radiologi;

/// <summary>Raised by the "Hasil" button — the parent opens the periksa radiologi (result entry) screen for this permintaan.</summary>
public record RadiologiHasilRequest(string NoRawat, string NoOrder);

public partial class PermintaanRadiologiListView : UserControl
{
    // Source is null for the nested examination rows of "Data Permintaan" (legacy: empty No.Permintaan = not selectable).
    private sealed class RowVm(string[] cells, RadiologiPermintaanRow? source)
    {
        public string[] Cells { get; } = cells;
        public RadiologiPermintaanRow? Source { get; } = source;
    }

    private enum PendingAction { None, Hapus, Sampel }

    private static readonly string[] HeaderRalan =
    [
        "No.Permintaan", "No.Rawat", "Pasien", "Permintaan", "Jam", "Sampel", "Jam", "Hasil", "Jam",
        "Kode Dokter", "Dokter Perujuk", "Poli Registrasi", "Informasi Tambahan", "Diagnosis Klinis", "Kode Bayar", "Jenis Bayar",
    ];

    private static readonly string[] HeaderRanap =
    [
        "No.Permintaan", "No.Rawat", "Pasien", "Permintaan", "Jam", "Sampel", "Jam", "Hasil", "Jam",
        "Kode Dokter", "Dokter Perujuk", "Kamar Terakhir", "Informasi Tambahan", "Diagnosis Klinis", "Kode Bayar", "Jenis Bayar",
    ];

    private static string[] ItemHeader(string lokasi) =>
    [
        "No.Permintaan", "No.Rawat", "Pasien", "Pemeriksaan", "Permintaan", "Jam", "Sampel", "Jam", "Hasil", "Jam",
        "Kode Dokter", "Dokter Perujuk", lokasi, "Informasi Tambahan", "Diagnosis Klinis", "Kode Bayar", "Jenis Bayar",
    ];

    private bool _ready;
    private PendingAction _pending = PendingAction.None;
    private RadiologiPermintaanRow? _pendingRow;

    public event EventHandler? BackRequested;
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;
    public event EventHandler<RadiologiHasilRequest>? HasilRequested;

    /// <summary>Legacy lets only "Admin Utama" delete an already-sampled permintaan; the parent sets this from the logged-in user.</summary>
    public bool IsAdminUtama { get; set; }

    public PermintaanRadiologiListView()
    {
        InitializeComponent();

        BuildColumns(Data(RadiologiLokasi.RawatJalan), HeaderRalan);
        BuildColumns(Data(RadiologiLokasi.RawatInap), HeaderRanap);
        BuildColumns(Items(RadiologiLokasi.RawatJalan), ItemHeader("Poli Registrasi"));
        BuildColumns(Items(RadiologiLokasi.RawatInap), ItemHeader("Kamar Terakhir"));
        foreach (var lokasi in Enum.GetValues<RadiologiLokasi>())
            foreach (var grid in new[] { Data(lokasi), Items(lokasi) })
                PatientContextMenu.Attach<RowVm>(grid,
                    row => row.Source is { } patient ? new(patient.NoRawat, patient.NoRm, patient.NamaPasien) : null,
                    request => PatientMenuRequested?.Invoke(this, request), PatientMenuKind.Diagnostic);

        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        _ready = true;
    }

    private DataGrid Data(RadiologiLokasi l) => this.FindControl<DataGrid>(l == RadiologiLokasi.RawatJalan ? "RalanDataGrid" : "RanapDataGrid")!;
    private DataGrid Items(RadiologiLokasi l) => this.FindControl<DataGrid>(l == RadiologiLokasi.RawatJalan ? "RalanItemGrid" : "RanapItemGrid")!;

    private RadiologiLokasi CurrentLokasi =>
        this.FindControl<TabControl>("LokasiTabs")!.SelectedIndex == 1 ? RadiologiLokasi.RawatInap : RadiologiLokasi.RawatJalan;

    private TabControl SubTabs(RadiologiLokasi l) =>
        this.FindControl<TabControl>(l == RadiologiLokasi.RawatJalan ? "RalanSubTabs" : "RanapSubTabs")!;

    private static void BuildColumns(DataGrid grid, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var wide = headers[i] is "Pasien" or "Dokter Perujuk" or "Diagnosis Klinis" or "Informasi Tambahan" or "Pemeriksaan" or "Jenis Bayar";
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = headers[i],
                Binding = new Binding($"Cells[{i}]"),
                Width = new DataGridLength(wide ? 1.6 : 1, DataGridLengthUnitType.Star),
                MinWidth = wide ? 130 : 70,
            });
        }
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var lokasi = CurrentLokasi;
        var ranap = lokasi == RadiologiLokasi.RawatInap;

        this.FindControl<TextBlock>("LokasiLabel")!.Text = ranap ? "Ruang :" : "Unit/Poli :";
        this.FindControl<TextBlock>("StatusLabel")!.IsVisible = ranap;
        this.FindControl<ComboBox>("StatusCombo")!.IsVisible = ranap;

        var filter = new RadiologiPermintaanFilter(
            lokasi, PickDate("FromPicker"), PickDate("ToPicker"),
            Text("DokterBox"), Text("LokasiBox"), Text("KeywordBox"),
            ranap && this.FindControl<ComboBox>("StatusCombo")!.SelectedIndex == 1);

        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new RadiologiPermintaanRepository(db);
            var rows = await repo.GetListAsync(filter);
            var exams = await repo.GetItemsAsync(rows.Select(r => r.NoOrder).ToList());
            var itemRows = await repo.GetItemRowsAsync(filter);

            Data(lokasi).ItemsSource = DataRows(rows, exams);
            Items(lokasi).ItemsSource = itemRows.Select(r => new RowVm(ItemCells(r), r)).ToList();
            this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {(SubTabs(lokasi).SelectedIndex == 1 ? itemRows.Count : rows.Count)}";
            HideStatus();
        }
        catch (Exception ex)
        {
            Data(lokasi).ItemsSource = new List<RowVm>();
            Items(lokasi).ItemsSource = new List<RowVm>();
            this.FindControl<TextBlock>("RecordText")!.Text = "Record : 0";
            ShowStatus($"Gagal memuat data permintaan: {ex.Message}", isError: true);
        }
    }

    private static string[] HeaderCells(RadiologiPermintaanRow r) =>
    [
        r.NoOrder, r.NoRawat, $"{r.NoRm} {r.NamaPasien}", r.TglPermintaan, r.JamPermintaan, r.TglSampel, r.JamSampel,
        r.TglHasil, r.JamHasil, r.KodeDokter, r.NamaDokter, r.Lokasi, r.InformasiTambahan, r.DiagnosaKlinis, r.KodePj, r.JenisBayar,
    ];

    private static string[] ItemCells(RadiologiPermintaanRow r) =>
    [
        r.NoOrder, r.NoRawat, $"{r.NoRm} {r.NamaPasien}", r.Pemeriksaan, r.TglPermintaan, r.JamPermintaan, r.TglSampel, r.JamSampel,
        r.TglHasil, r.JamHasil, r.KodeDokter, r.NamaDokter, r.Lokasi, r.InformasiTambahan, r.DiagnosaKlinis, r.KodePj, r.JenisBayar,
    ];

    // Legacy: each permintaan row is followed by one row per ordered examination, the name sitting in the "Pasien" column.
    private static List<RowVm> DataRows(IReadOnlyList<RadiologiPermintaanRow> rows, IReadOnlyList<RadiologiPermintaanItem> exams)
    {
        var result = new List<RowVm>();
        foreach (var r in rows)
        {
            result.Add(new RowVm(HeaderCells(r), r));
            foreach (var e in exams.Where(e => e.NoOrder == r.NoOrder))
                result.Add(new RowVm(["", "", e.Pemeriksaan, "", "", "", "", "", "", "", "", "", "", "", "", ""], null));
        }
        return result;
    }

    private DateOnly PickDate(string name)
        => DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>(name)!.SelectedDate ?? DateTime.Today);

    private string? Text(string name)
    {
        var t = this.FindControl<TextBox>(name)!.Text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private async void OnLokasiTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Nested sub-tab selection bubbles up here too; only react to the Rawat Jalan/Inap switch.
        if (!_ready || !ReferenceEquals(e.Source, sender)) return;
        await ReloadAsync();
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnFilterKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await ReloadAsync();
    }

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        foreach (var name in new[] { "DokterBox", "LokasiBox", "KeywordBox" })
            this.FindControl<TextBox>(name)!.Text = string.Empty;
        this.FindControl<ComboBox>("StatusCombo")!.SelectedIndex = 0;
        await ReloadAsync();
    }

    /// <summary>Legacy: hapus/sampel/hasil only work from the "Data Permintaan" view; elsewhere they bounce back to it.</summary>
    private RadiologiPermintaanRow? SelectedRow()
    {
        var lokasi = CurrentLokasi;
        var subTabs = SubTabs(lokasi);
        if (subTabs.SelectedIndex == 1)
        {
            ShowStatus("Maaf, silakan pilih Data Permintaan.", isError: true);
            subTabs.SelectedIndex = 0;
            return null;
        }

        var row = Data(lokasi).SelectedItem as RowVm;
        if (row?.Source is null)
        {
            ShowStatus("Maaf, silakan pilih data permintaan.", isError: true);
            return null;
        }
        return row.Source;
    }

    private void OnHapusClick(object? sender, RoutedEventArgs e)
    {
        var row = SelectedRow();
        if (row is null) return;

        _pending = PendingAction.Hapus;
        _pendingRow = row;
        this.FindControl<TextBlock>("ConfirmText")!.Text =
            $"Hapus permintaan {row.NoOrder} ({row.NoRm} {row.NamaPasien})? Data tidak bisa dikembalikan.";
        this.FindControl<StackPanel>("SampelFields")!.IsVisible = false;
        this.FindControl<Border>("ConfirmPanel")!.IsVisible = true;
    }

    private void OnSampelClick(object? sender, RoutedEventArgs e)
    {
        var row = SelectedRow();
        if (row is null) return;

        _pending = PendingAction.Sampel;
        _pendingRow = row;
        var now = DateTime.Now;
        this.FindControl<TextBlock>("ConfirmText")!.Text =
            $"Update waktu pengambilan sampel permintaan {row.NoOrder} ({row.NoRm} {row.NamaPasien})";
        this.FindControl<CalendarDatePicker>("SampelDatePicker")!.SelectedDate = now.Date;
        this.FindControl<TextBox>("SampelTimeBox")!.Text = now.ToString("HH:mm:ss");
        this.FindControl<StackPanel>("SampelFields")!.IsVisible = true;
        this.FindControl<Border>("ConfirmPanel")!.IsVisible = true;
    }

    private void OnConfirmCancelClick(object? sender, RoutedEventArgs e) => CloseConfirm();

    private void CloseConfirm()
    {
        _pending = PendingAction.None;
        _pendingRow = null;
        this.FindControl<Border>("ConfirmPanel")!.IsVisible = false;
    }

    private async void OnConfirmYesClick(object? sender, RoutedEventArgs e)
    {
        var row = _pendingRow;
        var action = _pending;
        if (row is null || action == PendingAction.None) return;

        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new RadiologiPermintaanRepository(db);

            if (action == PendingAction.Hapus)
            {
                await repo.DeleteAsync(row.NoOrder, IsAdminUtama);
                CloseConfirm();
                await ReloadAsync();
                ShowStatus($"Permintaan {row.NoOrder} dihapus.", isError: false);
            }
            else
            {
                var date = this.FindControl<CalendarDatePicker>("SampelDatePicker")!.SelectedDate?.Date ?? DateTime.Today;
                if (!TimeSpan.TryParse(this.FindControl<TextBox>("SampelTimeBox")!.Text, out var time))
                {
                    ShowStatus("Format jam harus HH:mm:ss.", isError: true);
                    return;
                }
                await repo.UpdateSampelAsync(row.NoOrder, date + time, CurrentLokasi == RadiologiLokasi.RawatJalan);
                CloseConfirm();
                await ReloadAsync();
                ShowStatus($"Waktu pengambilan sampel permintaan {row.NoOrder} tersimpan.", isError: false);
            }
        }
        catch (Exception ex)
        {
            CloseConfirm();
            ShowStatus($"Gagal: {ex.Message}", isError: true);
        }
    }

    private void OnHasilClick(object? sender, RoutedEventArgs e)
    {
        var row = SelectedRow();
        if (row is null) return;
        HasilRequested?.Invoke(this, new RadiologiHasilRequest(row.NoRawat, row.NoOrder));
    }

    private void OnCetakClick(object? sender, RoutedEventArgs e)
    {
        var lokasi = CurrentLokasi;
        var onItems = SubTabs(lokasi).SelectedIndex == 1;
        var grid = onItems ? Items(lokasi) : Data(lokasi);
        var rows = (grid.ItemsSource as IEnumerable<RowVm>)?.Select(r => r.Cells).ToList() ?? new List<string[]>();
        if (rows.Count == 0)
        {
            ShowStatus("Maaf, data sudah habis. Tidak ada data yang bisa dicetak.", isError: true);
            return;
        }

        var ranap = lokasi == RadiologiLokasi.RawatInap;
        var headers = onItems ? ItemHeader(ranap ? "Kamar Terakhir" : "Poli Registrasi") : (ranap ? HeaderRanap : HeaderRalan);
        var count = onItems ? rows.Count : rows.Count(r => r[0].Length > 0);

        try
        {
            var path = PermintaanRadiologiReport.GeneratePdf(
                $"Data Permintaan Radiologi - {(ranap ? "Rawat Inap" : "Rawat Jalan")}{(onItems ? " (Item)" : "")}",
                headers, rows, count);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal membuat/membuka PDF: {ex.Message}", isError: true);
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        var s = this.FindControl<TextBlock>("StatusText")!;
        s.Foreground = isError ? Avalonia.Media.Brush.Parse("#DC2626") : Avalonia.Media.Brushes.Green;
        s.Text = message;
        s.IsVisible = true;
    }

    private void HideStatus() => this.FindControl<TextBlock>("StatusText")!.IsVisible = false;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
