using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Radiologi;

public partial class PermintaanRadiologiView : UserControl
{
    public event EventHandler? BackRequested;

    public PermintaanRadiologiView()
    {
        InitializeComponent();
        var combo = this.FindControl<ComboBox>("StatusCombo")!;
        combo.ItemsSource = Enum.GetValues<StatusKunjunganPenunjang>();
        combo.SelectedItem = StatusKunjunganPenunjang.RawatJalan;
    }

    private async void OnKirimClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        var noRawat = this.FindControl<TextBox>("NoRawatBox")!.Text?.Trim();
        var dokter = this.FindControl<TextBox>("DokterBox")!.Text?.Trim();

        if (string.IsNullOrWhiteSpace(noRawat) || string.IsNullOrWhiteSpace(dokter))
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = "No. Rawat dan Dokter Perujuk wajib diisi.";
            status.IsVisible = true;
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

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Permintaan radiologi {noOrder} terkirim untuk {noRawat}.";
            status.IsVisible = true;
            this.FindControl<TextBox>("NoRawatBox")!.Text = string.Empty;
            this.FindControl<TextBox>("DiagnosaBox")!.Text = string.Empty;
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal kirim permintaan: {ex.Message}";
            status.IsVisible = true;
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
