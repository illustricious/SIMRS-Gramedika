using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Radiologi;

public partial class PeriksaRadiologiView : UserControl
{
    public event EventHandler? BackRequested;

    private PenunjangAntrianItem? _selected;

    public PeriksaRadiologiView() => InitializeComponent();

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var grid = this.FindControl<DataGrid>("AntrianGrid")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
            var to = DateOnly.FromDateTime(DateTime.Today);
            grid.ItemsSource = await new PenunjangRepository(db).GetAntrianRadiologiAsync(from, to);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<PenunjangAntrianItem>();
        }
    }

    private void OnAntrianSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = this.FindControl<DataGrid>("AntrianGrid")!.SelectedItem as PenunjangAntrianItem;
        this.FindControl<TextBlock>("SelectedText")!.Text = _selected is null
            ? "(belum ada pasien dipilih dari antrian)"
            : $"{_selected.NamaPasien}  —  No.Rawat {_selected.NoRawat}";
        this.FindControl<Button>("SimpanHasilButton")!.IsEnabled = _selected is not null;

        if (_selected is not null)
        {
            var toggle = this.FindControl<ToggleButton>("InputToggle")!;
            toggle.IsChecked = true;
            this.FindControl<Border>("InputPanel")!.IsVisible = true;
        }
    }

    private void OnToggleInput(object? sender, RoutedEventArgs e)
    {
        var toggle = this.FindControl<ToggleButton>("InputToggle")!;
        this.FindControl<Border>("InputPanel")!.IsVisible = toggle.IsChecked == true;
    }

    private async void OnSimpanHasilClick(object? sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var status = this.FindControl<TextBlock>("HasilStatusText")!;
        var hasil = this.FindControl<TextBox>("HasilBox")!.Text;

        if (string.IsNullOrWhiteSpace(hasil))
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = "Hasil wajib diisi (kolom NOT NULL di skema legacy hasil_radiologi).";
            status.IsVisible = true;
            return;
        }

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new PenunjangRepository(db).SaveHasilRadiologiAsync(_selected.NoRawat, hasil);

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Hasil radiologi {_selected.NamaPasien} tersimpan.";
            status.IsVisible = true;

            _selected = null;
            this.FindControl<TextBlock>("SelectedText")!.Text = "(belum ada pasien dipilih dari antrian)";
            this.FindControl<Button>("SimpanHasilButton")!.IsEnabled = false;
            this.FindControl<TextBox>("HasilBox")!.Text = string.Empty;
            this.FindControl<ToggleButton>("InputToggle")!.IsChecked = false;
            this.FindControl<Border>("InputPanel")!.IsVisible = false;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal simpan hasil: {ex.Message}";
            status.IsVisible = true;
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
