using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.UI;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private AuthenticatedUser? _user;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(AuthenticatedUser? user)
    {
        InitializeComponent();
        _user = user;

        var loginLabel = this.FindControl<TextBlock>("LoggedInUserText");
        if (loginLabel is not null)
        {
            loginLabel.Text = user is null
                ? "Login sebagai: (belum terhubung)"
                : $"Login sebagai: {user.DisplayName}";
        }

        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        if (user is not null)
        {
            SetupMenuCategoryCombo(user);
        }
    }

    private void UpdateClock()
    {
        var clock = this.FindControl<TextBlock>("ClockText");
        if (clock is not null)
        {
            clock.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }
    }

    private void OnThemeToggleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is null) return;

        var goingDark = Application.Current.RequestedThemeVariant != ThemeVariant.Dark;
        Application.Current.RequestedThemeVariant = goingDark ? ThemeVariant.Dark : ThemeVariant.Light;

        if (sender is ToggleButton toggle)
        {
            toggle.Content = goingDark ? "☀  Mode Terang" : "🌙  Mode Gelap";
        }
    }

    private void OnMenuShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var menu = this.FindControl<Grid>("MenuPanel")!;
        // "Menu" toggles between the hero/welcome panel and the icon-grid
        // dashboard, mirroring the legacy app's behavior where clicking
        // "Menu" opens the module grid over the home screen.
        ShowPage(menu.IsVisible ? Page.Hero : Page.Menu);
    }

    /// <summary>
    /// Single-window navigation: exactly one of Hero / Menu / PageHost (feature
    /// views like Registrasi) is visible at a time. Avoids popping separate
    /// Windows per feature, per the user's feedback that the legacy app stays
    /// in one window and swaps the view in place.
    /// </summary>
    private enum Page { Hero, Menu, Feature }

    private void ShowPage(Page page)
    {
        var hero = this.FindControl<StackPanel>("HeroPanel")!;
        var menu = this.FindControl<Grid>("MenuPanel")!;
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        hero.IsVisible = page == Page.Hero;
        menu.IsVisible = page == Page.Menu;
        pageHost.IsVisible = page == Page.Feature;
    }

    private void SetupMenuCategoryCombo(AuthenticatedUser user)
    {
        var combo = this.FindControl<ComboBox>("MenuCategoryCombo")!;
        // Single category for now — the legacy app groups ~1200 features into
        // lettered categories (A, B, C, ...); our catalog only has the 6
        // top-level modules so far, so there is one category until more are added.
        combo.ItemsSource = new List<string> { "Menu Utama — Modul Tersedia" };
        combo.SelectedIndex = 0;

        BuildDashboard(user);
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
            var button = new Button
            {
                Classes = { "menuTile" },
                Content = item.Label,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            var key = item.Key;
            button.Click += (_, _) => OnModuleTileClick(key, item.Label);
            panel.Children.Add(button);
        }
    }

    private async void OnModuleTileClick(string moduleKey, string label)
    {
        // Rekam Medis opens the Pasien (patient master data) screen — the real
        // starting point of the module, replacing the earlier SearchPickerDialog
        // demo now that a proper screen exists (see Modules/Pasien). Other
        // modules are not implemented yet in this scaffold stage.
        if (moduleKey == "rekammedis")
        {
            await OpenPasienAsync();
            return;
        }

        await ShowNotImplementedAsync(label);
    }

    private async System.Threading.Tasks.Task OpenPasienAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Pasien.PasienView view)
        {
            view = new Modules.Pasien.PasienView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async void OnFeatureShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var label = (sender as Button)?.Content?.ToString() ?? "Fitur";

        switch (label)
        {
            case "Registrasi":
                await OpenRegistrasiAsync();
                return;
            case "Rawat Jalan":
                await OpenRawatJalanAsync();
                return;
        }

        _ = ShowNotImplementedAsync(label);
    }

    private async System.Threading.Tasks.Task OpenRegistrasiAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Registrasi.RegistrasiView view)
        {
            view = new Modules.Registrasi.RegistrasiView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenRawatJalanAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.RawatJalan.RawatJalanView view)
        {
            view = new Modules.RawatJalan.RawatJalanView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private void OnTopShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var label = (sender as Button)?.Content?.ToString() ?? "Fitur";
        _ = ShowNotImplementedAsync(label);
    }

    private void OnExitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private async System.Threading.Tasks.Task ShowNotImplementedAsync(string label)
    {
        var dialog = new Window
        {
            Width = 320,
            Height = 140,
            Title = label,
            Content = new TextBlock
            {
                Text = $"\"{label}\" belum diimplementasikan di tahap scaffold ini.",
                Margin = new Thickness(16),
                TextWrapping = TextWrapping.Wrap,
            }
        };
        await dialog.ShowDialog(this);
    }
}
