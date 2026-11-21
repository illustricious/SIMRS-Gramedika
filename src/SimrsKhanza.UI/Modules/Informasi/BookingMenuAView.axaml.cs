using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Informasi;

public partial class BookingMenuAView : UserControl
{
    private readonly string _jenis;
    public event EventHandler? BackRequested;
    public BookingMenuAView(string jenis)
    {
        InitializeComponent(); _jenis = jenis;
        this.FindControl<TextBlock>("Judul")!.Text = $"{jenis}";
        this.FindControl<CalendarDatePicker>("Dari")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("Sampai")!.SelectedDate = DateTime.Today;
    }
    public async Task ReloadAsync()
    {
        var status = this.FindControl<TextBlock>("Status")!;
        var from = this.FindControl<CalendarDatePicker>("Dari")!.SelectedDate;
        var to = this.FindControl<CalendarDatePicker>("Sampai")!.SelectedDate;
        if (from is null || to is null || from.Value.Date > to.Value.Date) { status.Text = "Periksa periode tanggal."; return; }
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new MenuARepository(db);
            var keyword = this.FindControl<TextBox>("KataKunci")!.Text?.Trim() ?? "";
            var grid = this.FindControl<DataGrid>("Tabel")!;
            if (_jenis == "Booking Periksa") { var rows = await repo.BookingPeriksaAsync(from.Value,to.Value,keyword); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
            else if (_jenis == "Booking Registrasi") { var rows = await repo.BookingRegistrasiAsync(from.Value,to.Value,keyword); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
            else { var rows = await repo.BookingMcuAsync(from.Value,to.Value,keyword); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
        }
        catch (Exception ex) { status.Text = $"Gagal memuat: {ex.Message}"; }
    }
    private async void OnCari(object? sender, RoutedEventArgs e) => await ReloadAsync();
    private void OnBack(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
