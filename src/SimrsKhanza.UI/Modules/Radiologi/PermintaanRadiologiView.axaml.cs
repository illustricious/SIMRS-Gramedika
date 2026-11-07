using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Radiologi;

public partial class PermintaanRadiologiView : UserControl
{
    public event EventHandler? BackRequested;

    private IReadOnlyList<PenunjangAntrianItem> _all = Array.Empty<PenunjangAntrianItem>();

    public PermintaanRadiologiView()
    {
        InitializeComponent();
        var combo = this.FindControl<ComboBox>("StatusCombo")!;
        combo.ItemsSource = Enum.GetValues<StatusKunjunganPenunjang>();
        combo.SelectedItem = StatusKunjunganPenunjang.RawatJalan;

        this.FindControl<DatePicker>("FilterFromDate")!.SelectedDate = DateTime.Today.AddDays(-7);
        this.FindControl<DatePicker>("FilterToDate")!.SelectedDate = DateTime.Today;
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var from = DateOnly.FromDateTime(this.FindControl<DatePicker>("FilterFromDate")!.SelectedDate?.DateTime ?? DateTime.Today.AddDays(-7));
            var to = DateOnly.FromDateTime(this.FindControl<DatePicker>("FilterToDate")!.SelectedDate?.DateTime ?? DateTime.Today);
            _all = await new PenunjangRepository(db).GetAntrianRadiologiAsync(from, to);
        }
        catch (Exception)
        {
            _all = Array.Empty<PenunjangAntrianItem>();
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var noRawat = this.FindControl<TextBox>("FilterNoRawatBox")!.Text?.Trim();
        var keyword = this.FindControl<TextBox>("KeywordBox")!.Text?.Trim();

        IEnumerable<PenunjangAntrianItem> query = _all;

        if (!string.IsNullOrWhiteSpace(noRawat))
            query = query.Where(x => x.NoRawat.Contains(noRawat, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x =>
                x.NamaPasien.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.NoRawat.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.DokterPerujuk.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        var result = query.ToList();
        this.FindControl<DataGrid>("DataGrid")!.ItemsSource = result;
        this.FindControl<TextBlock>("RecordCountText")!.Text = $"Record: {result.Count}";
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<TextBox>("FilterNoRawatBox")!.Text = string.Empty;
        this.FindControl<TextBox>("KeywordBox")!.Text = string.Empty;
        this.FindControl<DatePicker>("FilterFromDate")!.SelectedDate = DateTime.Today.AddYears(-1);
        this.FindControl<DatePicker>("FilterToDate")!.SelectedDate = DateTime.Today;
        await ReloadAsync();
    }

    private void OnKeywordChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e) => ApplyFilter();

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
    }

    private void OnHapusClick(object? sender, RoutedEventArgs e)
        => ShowStatus("Hapus permintaan belum didukung di v1 (belum ada konfirmasi & penanganan data terkait).", isError: true);

    private void OnCetakClick(object? sender, RoutedEventArgs e)
        => ShowStatus("Cetak belum didukung — rincian biaya/tarif pemeriksaan radiologi belum dimodelkan di v1.", isError: true);

    private void ShowStatus(string message, bool isError)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        status.Foreground = isError ? Avalonia.Media.Brush.Parse("#DC2626") : Avalonia.Media.Brushes.Green;
        status.Text = message;
        status.IsVisible = true;
    }

    private void OnToggleInput(object? sender, RoutedEventArgs e)
    {
        var toggle = this.FindControl<ToggleButton>("InputToggle")!;
        this.FindControl<Border>("InputPanel")!.IsVisible = toggle.IsChecked == true;
    }

    private async void OnKirimClick(object? sender, RoutedEventArgs e)
    {
        var noRawat = this.FindControl<TextBox>("NoRawatBox")!.Text?.Trim();
        var dokter = this.FindControl<TextBox>("DokterBox")!.Text?.Trim();

        if (string.IsNullOrWhiteSpace(noRawat) || string.IsNullOrWhiteSpace(dokter))
        {
            ShowStatus("No. Rawat dan Dokter Perujuk wajib diisi.", isError: true);
            return;
        }

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new PenunjangRepository(db);
            var statusKunjungan = (StatusKunjunganPenunjang)this.FindControl<ComboBox>("StatusCombo")!.SelectedItem!;
            var noOrder = await repo.CreatePermintaanRadiologiAsync(
                noRawat, dokter, statusKunjungan,
                EmptyToNull(this.FindControl<TextBox>("DiagnosaBox")!.Text), null);

            ShowStatus($"Permintaan radiologi {noOrder} terkirim untuk {noRawat}.", isError: false);
            this.FindControl<TextBox>("NoRawatBox")!.Text = string.Empty;
            this.FindControl<TextBox>("DiagnosaBox")!.Text = string.Empty;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal kirim permintaan: {ex.Message}", isError: true);
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
