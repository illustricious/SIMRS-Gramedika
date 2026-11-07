using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Farmasi;

public partial class DaftarResepView : UserControl
{
    public event EventHandler? BackRequested;

    public DaftarResepView() => InitializeComponent();

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var grid = this.FindControl<DataGrid>("ResepGrid")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
            var to = DateOnly.FromDateTime(DateTime.Today);
            grid.ItemsSource = await new FarmasiRepository(db).GetResepDokterAsync(from, to);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<ResepListItem>();
        }
    }

    private async void OnResepSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var itemGrid = this.FindControl<DataGrid>("ItemGrid")!;
        var selected = this.FindControl<DataGrid>("ResepGrid")!.SelectedItem as ResepListItem;

        if (selected is null)
        {
            itemGrid.ItemsSource = new List<ResepItemLine>();
            return;
        }

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            itemGrid.ItemsSource = await new FarmasiRepository(db).GetResepItemsAsync(selected.NoResep);
        }
        catch (Exception)
        {
            itemGrid.ItemsSource = new List<ResepItemLine>();
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
