using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.UI;

public partial class MainWindow : Window
{
    private static readonly Dictionary<string, string> TileColors = new()
    {
        ["rekammedis"] = "#4F6D7A",
        ["keuangan"] = "#5B8C5A",
        ["kepegawaian"] = "#C97B63",
        ["inventaris"] = "#6B5B95",
        ["laporan"] = "#D4A017",
        ["bridging"] = "#3E7CB1",
    };

    public MainWindow() : this(null)
    {
    }

    public MainWindow(AuthenticatedUser? user)
    {
        InitializeComponent();

        var loginLabel = this.FindControl<TextBlock>("LoggedInUserText");
        if (loginLabel is not null)
        {
            loginLabel.Text = user is null
                ? "Login sebagai: (belum terhubung)"
                : $"Login sebagai: {user.DisplayName}";
        }

        if (user is not null)
        {
            BuildDashboard(user);
        }
    }

    private void BuildDashboard(AuthenticatedUser user)
    {
        IMenuPermissionService menuService = new MenuPermissionService(new DefaultMenuCatalog());
        var visibleItems = menuService.GetVisibleMenu(user);

        var panel = this.FindControl<WrapPanel>("DashboardPanel");
        if (panel is null) return;

        panel.Children.Clear();
        foreach (var item in visibleItems)
        {
            var color = TileColors.GetValueOrDefault(item.Key, "#4F6D7A");
            var button = new Button
            {
                Content = item.Label,
                Background = new SolidColorBrush(Color.Parse(color)),
                Width = 160,
                Height = 120,
                Margin = new Avalonia.Thickness(10),
                Foreground = Brushes.White,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                FontSize = 14,
                CornerRadius = new Avalonia.CornerRadius(8),
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            var key = item.Key;
            button.Click += (_, _) => OnModuleTileClick(key, item.Label);
            panel.Children.Add(button);
        }
    }

    private async void OnModuleTileClick(string moduleKey, string label)
    {
        // Rekam Medis is wired to a working demo of the generic SearchPickerDialog
        // (see Modules/RekamMedis and docs/audit/PLANNING_APLIKASI.md §2).
        // Other modules are not implemented yet in this scaffold stage.
        if (moduleKey == "rekammedis")
        {
            await Modules.RekamMedis.CariDokterDemo.ShowAsync(this);
            return;
        }

        var dialog = new Window
        {
            Width = 320,
            Height = 140,
            Title = label,
            Content = new TextBlock
            {
                Text = $"Modul \"{label}\" belum diimplementasikan di tahap scaffold ini.",
                Margin = new Avalonia.Thickness(16),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            }
        };
        await dialog.ShowDialog(this);
    }
}
