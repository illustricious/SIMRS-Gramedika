using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data.Auth;

namespace SimrsKhanza.UI.Modules.Akun;

public partial class TrackerLoginView : UserControl
{
    public event EventHandler? BackRequested;

    public TrackerLoginView()
    {
        InitializeComponent();
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
    }

    public Task ReloadAsync() => LoadAsync();

    private async void OnTampilkanClick(object? sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        var grid = this.FindControl<DataGrid>("Grid")!;
        var info = this.FindControl<TextBlock>("RingkasanText")!;
        var from = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate ?? DateTime.Today);
        try
        {
            var rows = await new KhanzaAkunRepository(DatabaseSettings.Load())
                .ListTrackerAsync(from, to, this.FindControl<TextBox>("CariBox")!.Text?.Trim());
            grid.ItemsSource = rows;
            info.Text = $"{rows.Count} login · {rows.Select(r => r.Nip).Distinct().Count()} user";
        }
        catch (Exception ex)
        {
            grid.ItemsSource = null;
            info.Text = $"Gagal memuat: {ex.Message}";
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
