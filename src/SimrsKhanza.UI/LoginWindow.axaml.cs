using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Auth;

namespace SimrsKhanza.UI;

public partial class LoginWindow : Window
{
    private readonly IAuthService _authService;
    private bool _loadingServers;

    public AuthenticatedUser? LoggedInUser { get; private set; }

    /// <summary>Design-time/XAML-loader constructor only.</summary>
    public LoginWindow() : this(new KhanzaLoginService(DatabaseSettings.Load))
    {
    }

    public LoginWindow(IAuthService authService)
    {
        _authService = authService;
        InitializeComponent();
        LoadServers();
        Opened += (_, _) => this.FindControl<TextBox>("UserIdBox")!.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) OnLoginClick(this, new RoutedEventArgs());
        };
    }

    /// <summary>Fills the server picker from the saved profiles (or the environment fallback).</summary>
    private void LoadServers()
    {
        _loadingServers = true;
        var box = this.FindControl<ComboBox>("ServerBox")!;
        var all = DatabaseSettingsStore.LoadAll();
        if (all.Items.Count == 0)
        {
            var env = DatabaseSettings.Load();
            box.ItemsSource = new[] { $"(belum diatur) {env.Summary}" };
            box.SelectedIndex = 0;
            box.IsEnabled = false;
        }
        else
        {
            box.ItemsSource = all.Items.Select(i => i.Name).ToList();
            var active = DatabaseSettingsStore.TryLoad();
            box.SelectedIndex = Math.Max(0, all.Items.FindIndex(i => i.Name == active?.Name));
            box.IsEnabled = all.Items.Count > 1;
        }
        _loadingServers = false;
        UpdateServerInfo();
    }

    private void UpdateServerInfo()
        => this.FindControl<TextBlock>("ServerInfo")!.Text = "Terhubung ke: " + DatabaseSettings.Load().Summary;

    private void OnServerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingServers || sender is not ComboBox { SelectedItem: string name }) return;
        DatabaseSettingsStore.SetActive(name);
        UpdateServerInfo();
        this.FindControl<TextBlock>("ErrorText")!.IsVisible = false;
    }

    private async void OnKoneksiClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new KoneksiDatabaseWindow();
        await dlg.ShowDialog(this);
        if (dlg.Saved)
        {
            LoadServers();
            this.FindControl<TextBlock>("ErrorText")!.IsVisible = false;
        }
    }

    private void OnDragPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        var userId = this.FindControl<TextBox>("UserIdBox")!.Text ?? string.Empty;
        var password = this.FindControl<TextBox>("PasswordBox")!.Text ?? string.Empty;
        var errorText = this.FindControl<TextBlock>("ErrorText")!;
        var button = this.FindControl<Button>("LoginButton")!;
        if (!button.IsEnabled) return;

        if (userId.Trim().Length == 0)
        {
            errorText.Text = "User ID wajib diisi.";
            errorText.IsVisible = true;
            return;
        }

        button.IsEnabled = false;
        button.Content = "Memeriksa...";
        LoginResult result;
        try
        {
            result = await _authService.LoginAsync(userId, password);
        }
        finally
        {
            button.IsEnabled = true;
            button.Content = "Masuk";
        }

        if (result.Success)
        {
            LoggedInUser = result.User;
            Close();
            return;
        }

        errorText.Text = result.FailureReason switch
        {
            LoginFailureReason.UserNotFound => "User tidak ditemukan.",
            LoginFailureReason.WrongPassword => "Password salah.",
            LoginFailureReason.AccountInactive => "Akun tidak aktif.",
            LoginFailureReason.DatabaseUnavailable =>
                $"Tidak bisa terhubung ke database {DatabaseSettings.Load().Summary}. Klik ⚙ untuk mengatur koneksi.",
            _ => "Login gagal."
        };
        errorText.IsVisible = true;
    }
}
