using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Auth;

namespace SimrsGramedika.UI;

public partial class LoginWindow : Window
{
    private readonly IAuthService _authService;
    private bool _loadingServers;

    public event Action<AuthenticatedUser>? LoginSucceeded;

    /// <summary>Design-time/XAML-loader constructor only.</summary>
    public LoginWindow() : this(new KhanzaLoginService(DatabaseSettings.Load))
    {
    }

    public LoginWindow(IAuthService authService)
    {
        _authService = authService;
        InitializeComponent();
        LoadServers();
        SizeChanged += (_, e) => UpdateLayoutForWidth(e.NewSize.Width);
        Opened += (_, _) => this.FindControl<TextBox>("UserIdBox")!.Focus();
        var versi = typeof(LoginWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
        this.FindControl<TextBlock>("VersionText")!.Text = $"RSU Gramedika  •  versi {versi}";
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

    private void UpdateLayoutForWidth(double width)
    {
        var compact = width < 740;
        this.FindControl<Border>("PhotoPanel")!.IsVisible = !compact;
        var form = this.FindControl<Grid>("FormSide")!;
        Grid.SetColumn(form, compact ? 0 : 1);
        Grid.SetColumnSpan(form, compact ? 2 : 1);
    }

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
        try
        {
            var result = await _authService.LoginAsync(userId, password);
            if (!IsVisible) return;

            if (result.Success && result.User is { } user)
            {
                button.Content = "Membuka menu…";
                errorText.IsVisible = false;
                this.FindControl<TextBox>("PasswordBox")!.Clear();
                if (LoginSucceeded is { } openMenu)
                {
                    openMenu(user);
                    return;
                }
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
        }
        catch (Exception)
        {
            errorText.Text = "Menu belum dapat dibuka. Coba masuk kembali.";
        }
        button.IsEnabled = true;
        button.Content = "Masuk";
        errorText.IsVisible = true;
    }
}
