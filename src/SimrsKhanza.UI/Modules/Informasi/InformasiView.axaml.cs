using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Informasi;

public partial class InformasiView : UserControl
{
    public event EventHandler? BackRequested;

    public InformasiView() => InitializeComponent();

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new InformasiRepository(db);

            this.FindControl<DataGrid>("KamarInapGrid")!.ItemsSource = await repo.GetPasienKamarInapAsync();
            this.FindControl<DataGrid>("PenggunaanKamarGrid")!.ItemsSource = await repo.GetPenggunaanKamarAsync();
            this.FindControl<DataGrid>("StokDarahGrid")!.ItemsSource = await repo.GetStokDarahAsync();
        }
        catch (Exception)
        {
            // Not connected — tabs just stay empty.
        }
    }

    private async void OnTelusurCariClick(object? sender, RoutedEventArgs e)
    {
        var grid = this.FindControl<DataGrid>("TelusurGrid")!;
        var keyword = this.FindControl<TextBox>("TelusurKeywordBox")!.Text ?? string.Empty;

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            grid.ItemsSource = await new InformasiRepository(db).TelusurKunjunganAsync(keyword);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<TelusurKunjunganItem>();
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
