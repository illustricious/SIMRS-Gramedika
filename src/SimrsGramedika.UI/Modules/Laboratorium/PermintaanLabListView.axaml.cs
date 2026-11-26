using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Laboratorium;

/// <summary>Raised by the "Hasil" button — the parent opens the periksa (result entry) screen for this permintaan.</summary>
public record LabHasilRequest(LabCategory Category, string NoRawat, string NoOrder);

public partial class PermintaanLabListView : UserControl
{
    private sealed class RowVm(string[] cells, LabPermintaanRow? source)
    {
        public string[] Cells { get; } = cells;
        public LabPermintaanRow? Source { get; } = source;
    }

    private enum PendingAction { None, Hapus, Sampel }

    private readonly LabCategory _category;
    private bool _ready;
    private PendingAction _pending = PendingAction.None;
    private LabPermintaanRow? _pendingRow;

    private readonly string[] _headerRalan;
    private readonly string[] _headerRanap;
    private readonly string[] _detailHeader;

    public event EventHandler? BackRequested;
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;
    public event EventHandler<LabHasilRequest>? HasilRequested;

    /// <summary>Legacy lets only "Admin Utama" delete an already-sampled permintaan; the parent sets this from the logged-in user.</summary>
    public bool IsAdminUtama { get; set; }

    public PermintaanLabListView(LabCategory category)
    {
        _category = category;
        InitializeComponent();

        var title = category switch
        {
            LabCategory.PK => "Patologi Klinis",
            LabCategory.PA => "Patologi Anatomi",
            _ => "Mikrobiologi & Bio Molekuler",
        };
        this.FindControl<TextBlock>("TitleText")!.Text = $"Data Permintaan Laboratorium {title}";

        string[] baseHead(string lokasi) =>
        [
            "No.Permintaan", "No.Rawat", "Pasien", "Permintaan", "Jam", "Sampel", "Jam", "Hasil", "Jam",
            "Kode Dokter", "Dokter Perujuk", lokasi, "Informasi Tambahan", "Diagnosis Klinis", "Kode Bayar", "Jenis Bayar",
        ];
        string[] paExtra =
        [
            "Tgl.Bahan", "Diperoleh Dengan", "Lokasi Jaringan", "Diawetkan Dengan",
            "Pernah Dilakukan PA Di", "Pada Tanggal", "Dengan Nomor PA", "Dengan Diagnosa PA", "Permintaan Pemeriksaan",
        ];
        var extra = category == LabCategory.PA ? paExtra : [];
        _headerRalan = [.. baseHead("Poli Registrasi"), .. extra];
        _headerRanap = [.. baseHead("Kamar Terakhir"), .. extra];

        // Same nested-view information as legacy tabMode2/4 but flattened: header fields on the
        // examination row, template items (PK/MB) below it.
        _detailHeader = category == LabCategory.PA
            ? ["No.Permintaan", "No.Rawat", "Pasien", "Pemeriksaan", "Permintaan", "Jam", "Sampel", "Jam", "Hasil", "Jam",
               "Kode Dokter", "Dokter Perujuk", "Lokasi", "Informasi Tambahan", "Diagnosis Klinis", "Kode Bayar", "Jenis Bayar", .. extra[..8]]
            : ["No.Permintaan", "No.Rawat", "Pasien", "Pemeriksaan", "Detail Pemeriksaan", "Satuan", "Nilai Rujukan (LD / LA / PD / PA)",
               "Permintaan", "Jam", "Sampel", "Jam", "Hasil", "Jam", "Kode Dokter", "Dokter Perujuk", "Lokasi",
               "Informasi Tambahan", "Diagnosis Klinis", "Kode Bayar", "Jenis Bayar"];

        BuildColumns(Data(LabLokasi.RawatJalan), _headerRalan);
        BuildColumns(Data(LabLokasi.RawatInap), _headerRanap);
        BuildColumns(Detail(LabLokasi.RawatJalan), _detailHeader);
        BuildColumns(Detail(LabLokasi.RawatInap), _detailHeader);
        foreach (var lokasi in Enum.GetValues<LabLokasi>())
            foreach (var grid in new[] { Data(lokasi), Detail(lokasi) })
                PatientContextMenu.Attach<RowVm>(grid,
                    row => row.Source is { } patient ? new(patient.NoRawat, patient.NoRm, patient.NamaPasien) : null,
                    request => PatientMenuRequested?.Invoke(this, request), PatientMenuKind.Diagnostic);

        FromPickerDefaults();
        _ready = true;
    }

    private void FromPickerDefaults()
    {
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
    }

    private DataGrid Data(LabLokasi l) => this.FindControl<DataGrid>(l == LabLokasi.RawatJalan ? "RalanDataGrid" : "RanapDataGrid")!;
    private DataGrid Detail(LabLokasi l) => this.FindControl<DataGrid>(l == LabLokasi.RawatJalan ? "RalanDetailGrid" : "RanapDetailGrid")!;

    private LabLokasi CurrentLokasi => this.FindControl<TabControl>("LokasiTabs")!.SelectedIndex == 1 ? LabLokasi.RawatInap : LabLokasi.RawatJalan;

    private static void BuildColumns(DataGrid grid, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var wide = headers[i] is "Pasien" or "Dokter Perujuk" or "Diagnosis Klinis" or "Informasi Tambahan" or "Pemeriksaan"
                or "Detail Pemeriksaan" or "Permintaan Pemeriksaan" or "Dengan Diagnosa PA" or "Jenis Bayar";
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
        var ranap = lokasi == LabLokasi.RawatInap;

        this.FindControl<TextBlock>("LokasiLabel")!.Text = ranap ? "Kamar :" : "Poli :";
        this.FindControl<TextBlock>("StatusLabel")!.IsVisible = ranap;
        this.FindControl<ComboBox>("StatusCombo")!.IsVisible = ranap;

        var filter = new LabPermintaanFilter(
            lokasi, PickDate("FromPicker"), PickDate("ToPicker"),
            Text("DokterBox"), Text("LokasiBox"), Text("KeywordBox"),
            ranap && this.FindControl<ComboBox>("StatusCombo")!.SelectedIndex == 1);

        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new LabPermintaanRepository(db);
            var rows = await repo.GetListAsync(_category, filter);
            var items = await repo.GetItemsAsync(_category, rows.Select(r => r.NoOrder).ToList());

            Data(lokasi).ItemsSource = rows.Select(r => new RowVm(DataCells(r, items), r)).ToList();
            Detail(lokasi).ItemsSource = DetailRows(rows, items);
            this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {rows.Count}";
            HideStatus();
        }
        catch (Exception ex)
        {
            Data(lokasi).ItemsSource = new List<RowVm>();
            Detail(lokasi).ItemsSource = new List<RowVm>();
            this.FindControl<TextBlock>("RecordText")!.Text = "Record : 0";
            ShowStatus($"Gagal memuat data permintaan: {ex.Message}", isError: true);
        }
    }

    private string[] DataCells(LabPermintaanRow r, IReadOnlyList<LabPermintaanItem> items)
    {
        string[] cells =
        [
            r.NoOrder, r.NoRawat, $"{r.NoRm} {r.NamaPasien}", r.TglPermintaan, r.JamPermintaan, r.TglSampel, r.JamSampel,
            r.TglHasil, r.JamHasil, r.KodeDokter, r.NamaDokter, r.Lokasi, r.InformasiTambahan, r.DiagnosaKlinis, r.KodePj, r.JenisBayar,
        ];
        if (_category != LabCategory.PA) return cells;

        var names = string.Join(", ", items.Where(i => i.NoOrder == r.NoOrder && i.Pemeriksaan.Length > 0).Select(i => i.Pemeriksaan));
        return
        [
            .. cells, r.TglBahan, r.DiperolehDengan, r.LokasiJaringan, r.DiawetkanDengan, r.PernahDilakukanDi,
            r.TglPaSebelumnya, r.NoPaSebelumnya, r.DiagnosaPaSebelumnya, names,
        ];
    }

    private List<RowVm> DetailRows(IReadOnlyList<LabPermintaanRow> rows, IReadOnlyList<LabPermintaanItem> items)
    {
        var result = new List<RowVm>();
        foreach (var r in rows)
        {
            var pasien = $"{r.NoRm} {r.NamaPasien}";
            foreach (var it in items.Where(i => i.NoOrder == r.NoOrder))
            {
                if (it.Pemeriksaan.Length > 0)
                {
                    result.Add(new RowVm(_category == LabCategory.PA
                        ?
                        [
                            r.NoOrder, r.NoRawat, pasien, it.Pemeriksaan, r.TglPermintaan, r.JamPermintaan, r.TglSampel, r.JamSampel,
                            r.TglHasil, r.JamHasil, r.KodeDokter, r.NamaDokter, r.Lokasi, r.InformasiTambahan, r.DiagnosaKlinis,
                            r.KodePj, r.JenisBayar, r.TglBahan, r.DiperolehDengan, r.LokasiJaringan, r.DiawetkanDengan,
                            r.PernahDilakukanDi, r.TglPaSebelumnya, r.NoPaSebelumnya, r.DiagnosaPaSebelumnya,
                        ]
                        :
                        [
                            r.NoOrder, r.NoRawat, pasien, it.Pemeriksaan, "", "", "", r.TglPermintaan, r.JamPermintaan,
                            r.TglSampel, r.JamSampel, r.TglHasil, r.JamHasil, r.KodeDokter, r.NamaDokter, r.Lokasi,
                            r.InformasiTambahan, r.DiagnosaKlinis, r.KodePj, r.JenisBayar,
                        ], r));
                }
                else
                {
                    var rujukan = string.Join(" / ", new[] { it.RujukanLd, it.RujukanLa, it.RujukanPd, it.RujukanPa });
                    result.Add(new RowVm(
                    [
                        "", "", "", "", "  " + it.Detail, it.Satuan, rujukan, "", "", "", "", "", "", "", "", "", "", "", "", "",
                    ], r));
                }
            }
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

    private LabPermintaanRow? SelectedRow()
    {
        var grid = Data(CurrentLokasi);
        var detailTabSelected = (CurrentLokasi == LabLokasi.RawatJalan
            ? this.FindControl<TabControl>("RalanSubTabs")!
            : this.FindControl<TabControl>("RanapSubTabs")!).SelectedIndex == 1;

        // Legacy: hapus/sampel only work from the "Data Permintaan" view.
        var source = detailTabSelected ? Detail(CurrentLokasi) : grid;
        return (source.SelectedItem as RowVm)?.Source;
    }

    private void OnHapusClick(object? sender, RoutedEventArgs e)
    {
        var row = SelectedRow();
        if (row is null)
        {
            ShowStatus("Maaf, silakan pilih data permintaan.", isError: true);
            return;
        }
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
        if (row is null)
        {
            ShowStatus("Maaf, silakan pilih data permintaan.", isError: true);
            return;
        }
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
            var repo = new LabPermintaanRepository(db);

            if (action == PendingAction.Hapus)
            {
                await repo.DeleteAsync(_category, row.NoOrder, IsAdminUtama);
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
                await repo.UpdateSampelAsync(_category, row.NoOrder, date + time);
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
        if (row is null)
        {
            ShowStatus("Maaf, silakan pilih data permintaan.", isError: true);
            return;
        }
        HasilRequested?.Invoke(this, new LabHasilRequest(_category, row.NoRawat, row.NoOrder));
    }

    private void OnCetakClick(object? sender, RoutedEventArgs e)
    {
        var lokasi = CurrentLokasi;
        var rows = (Data(lokasi).ItemsSource as IEnumerable<RowVm>)?.Select(r => r.Cells).ToList() ?? new List<string[]>();
        if (rows.Count == 0)
        {
            ShowStatus("Maaf, data sudah habis. Tidak ada data yang bisa dicetak.", isError: true);
            return;
        }

        try
        {
            var title = this.FindControl<TextBlock>("TitleText")!.Text!.Trim(':', '[', ']', ' ');
            var path = PermintaanLabReport.GeneratePdf(
                $"{title} - {(lokasi == LabLokasi.RawatJalan ? "Rawat Jalan" : "Rawat Inap")}",
                lokasi == LabLokasi.RawatJalan ? _headerRalan : _headerRanap, rows);
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
