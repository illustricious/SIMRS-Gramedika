using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Laboratorium;

public partial class LabPeriksaView : UserControl
{
    public event EventHandler? BackRequested;

    private KategoriLab _kategori = KategoriLab.PatologiKlinik;
    private PenunjangAntrianItem? _selected;

    public LabPeriksaView() => InitializeComponent();

    public void SetKategori(KategoriLab kategori)
    {
        _kategori = kategori;
        this.FindControl<TextBlock>("TitleText")!.Text = $":: [ Periksa Lab {KategoriLabel.Short(kategori)} ] ::";
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var grid = this.FindControl<DataGrid>("AntrianGrid")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
            var to = DateOnly.FromDateTime(DateTime.Today);
            grid.ItemsSource = await new PenunjangRepository(db).GetAntrianLabAsync(_kategori, from, to);
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
    }

    private async void OnSimpanHasilClick(object? sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var status = this.FindControl<TextBlock>("HasilStatusText")!;

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new PenunjangRepository(db).SaveHasilLabAsync(
                _selected.NoRawat,
                EmptyToNull(this.FindControl<TextBox>("SaranBox")!.Text),
                EmptyToNull(this.FindControl<TextBox>("KesanBox")!.Text));

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Hasil lab {_selected.NamaPasien} tersimpan.";
            status.IsVisible = true;

            _selected = null;
            this.FindControl<TextBlock>("SelectedText")!.Text = "(belum ada pasien dipilih dari antrian)";
            this.FindControl<Button>("SimpanHasilButton")!.IsEnabled = false;
            this.FindControl<TextBox>("KesanBox")!.Text = string.Empty;
            this.FindControl<TextBox>("SaranBox")!.Text = string.Empty;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal simpan hasil: {ex.Message}";
            status.IsVisible = true;
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
