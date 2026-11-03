using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Registrasi;

public partial class RegistrasiWindow : Window
{
    public RegistrasiWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private void OnToggleInputData(object? sender, RoutedEventArgs e)
    {
        var panel = this.FindControl<Border>("InputDataPanel")!;
        var toggle = (ToggleButton)sender!;
        panel.IsVisible = toggle.IsChecked == true;
    }

    private async void OnReloadClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private async System.Threading.Tasks.Task ReloadAsync()
    {
        var grid = this.FindControl<DataGrid>("RegistrasiGrid")!;
        var recordCount = this.FindControl<TextBlock>("RecordCountText")!;

        try
        {
            var settings = DatabaseSettings.FromEnvironment();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new RegistrasiRepository(db);
            var items = await repo.GetTodayAsync();

            grid.ItemsSource = items;
            recordCount.Text = $"Record : {items.Count}";
        }
        catch (Exception)
        {
            // No reachable MySQL server — show an empty grid rather than crash.
            grid.ItemsSource = new List<RegistrasiListItem>();
            recordCount.Text = "Record : 0 (tidak konek DB)";
        }
    }

    private void OnPlaceholderClick(object? sender, RoutedEventArgs e)
    {
        var label = (sender as Button)?.Content?.ToString() ?? "Aksi";
        var dialog = new Window
        {
            Width = 300,
            Height = 120,
            Title = label,
            Content = new TextBlock
            {
                Text = $"\"{label}\" belum diimplementasikan di tahap scaffold ini.",
                Margin = new Avalonia.Thickness(16),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            }
        };
        _ = dialog.ShowDialog(this);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
