using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Data;

namespace SimrsGramedika.UI.Modules.Laporan;

public partial class LaporanPendapatanView : UserControl
{
    public event EventHandler? BackRequested;

    public LaporanPendapatanView()
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
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var items = await new KasirRepository(db).GetLaporanPendapatanAsync(from, to);
            grid.ItemsSource = items;
            var total = items.Sum(i => i.Total);
            var sudahBayar = items.Where(i => i.StatusBayar == "Sudah Bayar").Sum(i => i.Total);
            ringkasan.Text = $"Total tagihan: Rp {total:N0}  —  Sudah dibayar: Rp {sudahBayar:N0}  —  {items.Count} kunjungan";
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<KasirBillItem>();
            ringkasan.Text = "Tidak konek DB";
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
