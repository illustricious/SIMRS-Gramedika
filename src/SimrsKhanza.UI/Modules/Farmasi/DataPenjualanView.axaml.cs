using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Farmasi;

public partial class DataPenjualanView : UserControl
{
    public event EventHandler? BackRequested;

    public DataPenjualanView() => InitializeComponent();

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var grid = this.FindControl<DataGrid>("PenjualanGrid")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
            var to = DateOnly.FromDateTime(DateTime.Today);
            grid.ItemsSource = await new FarmasiRepository(db).GetPenjualanAsync(from, to);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<PenjualanListItem>();
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
