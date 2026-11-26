using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Data;

namespace SimrsGramedika.UI.Modules.Farmasi;

public partial class DataPenjualanView : UserControl
{
    public event EventHandler? BackRequested;

    public DataPenjualanView()
    {
        InitializeComponent();
        var today = DateTime.Today;
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = new DateTime(today.Year, today.Month, 1);
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = today;
    }

    public System.Threading.Tasks.Task ReloadAsync() => LoadAsync();

    private async void OnTampilkanClick(object? sender, RoutedEventArgs e) => await LoadAsync();

    private async void OnKeywordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        var grid = this.FindControl<DataGrid>("PenjualanGrid")!;
        var status = this.FindControl<TextBlock>("StatusText")!;
        var fromPicker = this.FindControl<CalendarDatePicker>("FromPicker")!;
        var toPicker = this.FindControl<CalendarDatePicker>("ToPicker")!;
        var today = DateTime.Today;
        var from = DateOnly.FromDateTime((fromPicker.SelectedDate ?? new DateTime(today.Year, today.Month, 1)).Date);
        var to = DateOnly.FromDateTime((toPicker.SelectedDate ?? today).Date);
        if (from > to)
        {
            grid.ItemsSource = null;
            SetStatus(status, "Periode awal tidak boleh melewati periode akhir.", error: true);
            return;
        }

        grid.ItemsSource = null;
        SetStatus(status, "Memuat data penjualan…");
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var keyword = this.FindControl<TextBox>("KeywordBox")!.Text?.Trim() ?? "";
            var rows = await new FarmasiRepository(db).GetPenjualanAsync(from, to, keyword: keyword);
            grid.ItemsSource = rows;
            SetStatus(status, $"{rows.Count:N0} baris barang · Total barang: Rp {rows.Sum(row => row.Total):N0}");
        }
        catch (Exception ex)
        {
            grid.ItemsSource = null;
            SetStatus(status, $"Gagal memuat data penjualan: {ex.Message}", error: true);
        }
    }

    private static void SetStatus(TextBlock target, string message, bool error = false)
    {
        target.Text = message;
        target.Classes.Set("error", error);
        target.Classes.Set("success", !error && message.Length > 0 && message != "Memuat data penjualan…");
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
