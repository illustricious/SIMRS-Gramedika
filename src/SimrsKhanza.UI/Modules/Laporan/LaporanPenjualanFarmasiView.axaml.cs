using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Laporan;

public partial class LaporanPenjualanFarmasiView : UserControl
{
    public event EventHandler? BackRequested;

    public LaporanPenjualanFarmasiView()
    {
        InitializeComponent();
        var today = DateTime.Today;
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = today;
    }

    public async System.Threading.Tasks.Task ReloadAsync() => await LoadAsync();

    private async void OnTampilkanClick(object? sender, RoutedEventArgs e) => await LoadAsync();

    private async System.Threading.Tasks.Task LoadAsync()
    {
        var grid = this.FindControl<DataGrid>("Grid")!;
        var ringkasan = this.FindControl<TextBlock>("RingkasanText")!;
        var from = this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate is { } f ? DateOnly.FromDateTime(f.Date) : DateOnly.FromDateTime(DateTime.Today);
        var to = this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate is { } t ? DateOnly.FromDateTime(t.Date) : DateOnly.FromDateTime(DateTime.Today);

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var items = await new FarmasiRepository(db).GetPenjualanAsync(from, to);
            grid.ItemsSource = items;
            var total = items.Sum(i => i.Total);
            ringkasan.Text = $"Total: Rp {total:N0}  —  {items.Count} transaksi";
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<PenjualanListItem>();
            ringkasan.Text = "Tidak konek DB";
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
