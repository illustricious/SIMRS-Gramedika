using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Data;

namespace SimrsGramedika.UI.Modules.Farmasi;

public partial class DataPenjualanView : UserControl
{
    public event EventHandler? BackRequested;

    public DataPenjualanView() => InitializeComponent();

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var grid = this.FindControl<DataGrid>("PenjualanGrid")!;
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
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
