using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SimrsKhanza.Config;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Laboratorium;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Radiologi;

public enum RadRowKind { Header, SubHeader, Item, BhpHeader, Bhp, Total }

/// <summary>One line of the nested grid — columns are reused by row kind exactly like the legacy table model.</summary>
public class RadDisplayRow
{
    public RadRowKind Kind { get; init; }
    public RadPeriksaKey? Key { get; init; }
    public string C0 { get; init; } = "";
    public string C1 { get; init; } = "";
    public string C2 { get; init; } = "";
    public string C3 { get; init; } = "";
    public string C4 { get; init; } = "";
    public string C5 { get; init; } = "";
    public string C6 { get; init; } = "";
    public string[] Cells => [C0, C1, C2, C3, C4, C5, C6];
}

public partial class PeriksaRadiologiListView : UserControl
{
    private bool _ready, _lookupsLoaded;
    private List<RadPeriksaGroup> _groups = new();
    private List<RadDisplayRow> _rows = new();
    private IReadOnlyList<LabPick> _dokter = Array.Empty<LabPick>();
    private IReadOnlyList<LabPick> _petugas = Array.Empty<LabPick>();

    public event EventHandler? BackRequested;
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    /// <summary>"Periksa Baru" — arg is the selected/filtered no_rawat (or null) to prefill the entry form.</summary>
    public event EventHandler<string?>? InputRequested;

    public PeriksaRadiologiListView()
    {
        InitializeComponent();

        var hasil = this.FindControl<ComboBox>("HasilCombo")!;
        hasil.ItemsSource = new[] { "Semua", "Sudah Keluar Bacaan", "Belum Keluar Bacaan" };
        hasil.SelectedIndex = 0;

        // Default period = today (perf lesson from Registrasi: never default to a wide range on big tables).
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<DataGrid>("NestedGrid")!.LoadingRow += OnLoadingRow;
        PatientContextMenu.Attach<RadDisplayRow>(this.FindControl<DataGrid>("NestedGrid")!,
            row => row.Key is { } key && _groups.FirstOrDefault(g => g.Key == key) is { } patient
                ? new(key.NoRawat, patient.NoRm, patient.NamaPasien) : null,
            request => PatientMenuRequested?.Invoke(this, request), PatientMenuKind.Diagnostic);
        _ready = true;
    }

    private static readonly IBrush HeaderBrush = Brush.Parse("#331E3A8A");
    private static readonly IBrush SubBrush = Brush.Parse("#22888888");
    private static readonly IBrush TotalBrush = Brush.Parse("#2200A86B");

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not RadDisplayRow r) return;
        e.Row.Background = r.Kind switch
        {
            RadRowKind.Header => HeaderBrush,
            RadRowKind.SubHeader or RadRowKind.BhpHeader => SubBrush,
            RadRowKind.Total => TotalBrush,
            _ => Brushes.Transparent,
        };
        e.Row.FontWeight = r.Kind is RadRowKind.Header or RadRowKind.Total ? FontWeight.Bold : FontWeight.Normal;
    }

    private RadPeriksaFilter BuildFilter()
    {
        string? T(string name) { var t = this.FindControl<TextBox>(name)!.Text?.Trim(); return string.IsNullOrEmpty(t) ? null : t; }
        var petugas = this.FindControl<ComboBox>("PetugasCombo")!.SelectedItem as LabPick;
        return new RadPeriksaFilter(
            DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate ?? DateTime.Today),
            DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate ?? DateTime.Today),
            T("NoRawatBox"), T("NoRmBox"), string.IsNullOrEmpty(petugas?.Kode) ? null : petugas.Kode, T("KeywordBox"),
            this.FindControl<ComboBox>("HasilCombo")!.SelectedIndex switch
            {
                1 => RadHasilStatus.SudahAdaHasil,
                2 => RadHasilStatus.BelumAdaHasil,
                _ => RadHasilStatus.Semua,
            });
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            if (!_lookupsLoaded)
            {
                var lab = new LabPeriksaRepository(db);
                _petugas = await lab.GetPetugasAsync();
                _dokter = await lab.GetDokterAsync(null);
                var list = new List<LabPick> { new() { Kode = "", Nama = "(Semua)" } };
                list.AddRange(_petugas);
                var combo = this.FindControl<ComboBox>("PetugasCombo")!;
                combo.ItemsSource = list;
                combo.SelectedIndex = 0;
                _lookupsLoaded = true;
            }

            _groups = (await new RadiologiPeriksaRepository(db).GetPeriksaGroupsAsync(BuildFilter())).ToList();
            _rows = BuildRows(_groups);
            this.FindControl<DataGrid>("NestedGrid")!.ItemsSource = _rows;
            this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {_groups.Count}";
            this.FindControl<TextBlock>("TotalText")!.Text = $"Total : {_groups.Sum(g => g.Total):N0}";
            ClearEditors();
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal memuat data pemeriksaan: {ex.Message}", isError: true);
        }
    }

    private static List<RadDisplayRow> BuildRows(IReadOnlyList<RadPeriksaGroup> groups)
    {
        var rows = new List<RadDisplayRow>();
        decimal ttl = 0;
        foreach (var g in groups)
        {
            rows.Add(new RadDisplayRow
            {
                Kind = RadRowKind.Header, Key = g.Key, C0 = g.Key.NoRawat, C1 = $"{g.NoRm} {g.NamaPasien} ({g.KamarPoli})",
                C2 = g.Petugas, C3 = g.Key.Tgl, C4 = g.Key.Jam, C5 = g.DokterPerujukNama, C6 = g.DokterPjNama,
            });
            rows.Add(new RadDisplayRow
            {
                Kind = RadRowKind.SubHeader, Key = g.Key, C1 = "Proyeksi & Dosis Radiasi", C2 = "Kode Periksa", C3 = "Nama Pemeriksaan",
                C4 = "Biaya", C5 = "Cara Bayar : " + g.CaraBayar,
            });
            foreach (var p in g.Pemeriksaan)
            {
                ttl += p.Biaya;
                rows.Add(new RadDisplayRow { Kind = RadRowKind.Item, Key = g.Key, C1 = p.Proyeksi, C2 = p.KdJenisPrw, C3 = p.Nama, C4 = p.Biaya.ToString("N0") });
            }
            if (g.Bhp.Count > 0)
            {
                rows.Add(new RadDisplayRow { Kind = RadRowKind.BhpHeader, Key = g.Key, C2 = "Kode BHP", C3 = "Nama BHP", C4 = "Satuan", C5 = "Jumlah" });
                foreach (var b in g.Bhp)
                    rows.Add(new RadDisplayRow { Kind = RadRowKind.Bhp, Key = g.Key, C2 = b.KodeBrng, C3 = b.Nama, C4 = b.Satuan, C5 = b.Jumlah });
            }
        }
        if (ttl > 0) rows.Add(new RadDisplayRow { Kind = RadRowKind.Total, C0 = ">>", C1 = $"Total : {ttl:N0}" });
        return rows;
    }

    // ---- selection / hasil editor ----------------------------------------------------------

    private RadPeriksaGroup? SelectedGroup()
    {
        var key = (this.FindControl<DataGrid>("NestedGrid")!.SelectedItem as RadDisplayRow)?.Key;
        return key is null ? null : _groups.FirstOrDefault(g => g.Key == key);
    }

    private void ClearEditors()
    {
        foreach (var n in new[] { "HasilBox", "InformasiBox", "DiagnosisBox" })
            this.FindControl<TextBox>(n)!.Text = string.Empty;
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        var g = SelectedGroup();
        this.FindControl<TextBox>("HasilBox")!.Text = g?.Hasil;
        this.FindControl<TextBox>("InformasiBox")!.Text = string.Empty;
        this.FindControl<TextBox>("DiagnosisBox")!.Text = string.Empty;
        if (g is null) return;

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var info = await new RadiologiPeriksaRepository(db).GetPermintaanInfoAsync(g.Key);
            if (SelectedGroup()?.Key != g.Key) return;
            this.FindControl<TextBox>("InformasiBox")!.Text = info?.InformasiTambahan;
            this.FindControl<TextBox>("DiagnosisBox")!.Text = info?.DiagnosaKlinis;
        }
        catch { /* the info boxes are a convenience; a lookup failure must not break selection */ }
    }

    private async void OnUpdateHasilClick(object? sender, RoutedEventArgs e)
    {
        var g = SelectedGroup();
        if (g is null) { ShowStatus("Pilih dulu data pemeriksaan di tabel.", isError: true); return; }
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new RadiologiPeriksaRepository(db).UpdateHasilAsync(g.Key, this.FindControl<TextBox>("HasilBox")!.Text ?? "");
            var key = g.Key;
            await ReloadAsync();
            ShowStatus("Proses update selesai.", isError: false);
            var idx = _rows.FindIndex(r => r.Key == key);
            if (idx >= 0) this.FindControl<DataGrid>("NestedGrid")!.SelectedIndex = idx;
        }
        catch (Exception ex) { ShowStatus($"Gagal update: {ex.Message}", isError: true); }
    }

    // ---- "Ubah P.J.Rad, Perujuk & Petugas" --------------------------------------------------

    private void OnUbahClick(object? sender, RoutedEventArgs e)
    {
        var g = SelectedGroup();
        if (g is null) { ShowStatus("Pilih dulu data pemeriksaan di tabel.", isError: true); return; }

        this.FindControl<ComboBox>("UbahPjCombo")!.ItemsSource = _dokter;
        this.FindControl<ComboBox>("UbahPerujukCombo")!.ItemsSource = _dokter;
        this.FindControl<ComboBox>("UbahPetugasCombo")!.ItemsSource = _petugas;
        this.FindControl<ComboBox>("UbahPjCombo")!.SelectedItem = _dokter.FirstOrDefault(d => d.Kode == g.DokterPjKode);
        this.FindControl<ComboBox>("UbahPerujukCombo")!.SelectedItem = _dokter.FirstOrDefault(d => d.Kode == g.DokterPerujukKode);
        this.FindControl<ComboBox>("UbahPetugasCombo")!.SelectedItem = _petugas.FirstOrDefault(p => p.Kode == g.Nip);
        this.FindControl<Border>("UbahPanel")!.IsVisible = true;
    }

    private void OnUbahTutupClick(object? sender, RoutedEventArgs e) => this.FindControl<Border>("UbahPanel")!.IsVisible = false;

    private async void OnUbahSimpanClick(object? sender, RoutedEventArgs e)
    {
        var g = SelectedGroup();
        var pj = this.FindControl<ComboBox>("UbahPjCombo")!.SelectedItem as LabPick;
        var perujuk = this.FindControl<ComboBox>("UbahPerujukCombo")!.SelectedItem as LabPick;
        var petugas = this.FindControl<ComboBox>("UbahPetugasCombo")!.SelectedItem as LabPick;
        if (g is null) { ShowStatus("Pilih dulu data pemeriksaan di tabel.", isError: true); return; }
        if (pj is null || perujuk is null || petugas is null) { ShowStatus("P.J. Rad, perujuk, dan petugas wajib diisi.", isError: true); return; }
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new RadiologiPeriksaRepository(db).UpdateDokterPetugasAsync(g.Key, petugas.Kode, perujuk.Kode, pj.Kode);
            this.FindControl<Border>("UbahPanel")!.IsVisible = false;
            await ReloadAsync();
            ShowStatus("Data pemeriksaan diperbarui.", isError: false);
        }
        catch (Exception ex) { ShowStatus($"Gagal menyimpan: {ex.Message}", isError: true); }
    }

    // ---- toolbar ----------------------------------------------------------------------------

    private async void OnFilterKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await ReloadAsync();
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        foreach (var n in new[] { "NoRawatBox", "NoRmBox", "KeywordBox" }) this.FindControl<TextBox>(n)!.Text = string.Empty;
        this.FindControl<ComboBox>("PetugasCombo")!.SelectedIndex = 0;
        this.FindControl<ComboBox>("HasilCombo")!.SelectedIndex = 0;
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        await ReloadAsync();
    }

    private void OnInputClick(object? sender, RoutedEventArgs e)
    {
        var noRawat = SelectedGroup()?.Key.NoRawat;
        if (string.IsNullOrEmpty(noRawat)) noRawat = this.FindControl<TextBox>("NoRawatBox")!.Text?.Trim();
        InputRequested?.Invoke(this, string.IsNullOrEmpty(noRawat) ? null : noRawat);
    }

    private async void OnHapusClick(object? sender, RoutedEventArgs e)
    {
        var g = SelectedGroup();
        if (g is null) { ShowStatus("Pilih dulu data yang mau dihapus (klik barisnya di tabel).", isError: true); return; }

        if (!await LabUi.ConfirmAsync(this, $"Yakin mau menghapus data pemeriksaan ini?\n{g.Key.NoRawat} — {g.NamaPasien} ({g.Key.Tgl} {g.Key.Jam}, {g.Pemeriksaan.Count} pemeriksaan)")) return;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new RadiologiPeriksaRepository(db).DeleteAsync(g.Key);
            await ReloadAsync();
            ShowStatus("Data pemeriksaan dihapus.", isError: false);
        }
        catch (Exception ex) { ShowStatus(ex.Message, isError: true); }
    }

    private void OnCetakClick(object? sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0) { ShowStatus("Tidak ada data yang bisa dicetak.", isError: true); return; }
        try
        {
            var path = PeriksaLabReport.GeneratePdf("Data Pemeriksaan Radiologi",
                ["No.Rawat", "Pasien / Proyeksi & Dosis", "Petugas / Kode", "Tgl / Nama Pemeriksaan", "Jam / Biaya", "Dokter Perujuk / Cara Bayar / Jumlah", "Penanggung Jawab"],
                _rows.Select(r => r.Cells).ToList(), $"{_groups.Count} data");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowStatus($"Gagal membuat/membuka PDF: {ex.Message}", isError: true); }
    }

    private void ShowStatus(string message, bool isError)
    {
        var s = this.FindControl<TextBlock>("StatusText")!;
        s.Foreground = isError ? Brush.Parse("#DC2626") : Brushes.Green;
        s.Text = message;
        s.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
