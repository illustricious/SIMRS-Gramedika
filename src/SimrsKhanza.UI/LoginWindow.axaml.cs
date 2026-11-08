using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Auth;

namespace SimrsKhanza.UI;

public partial class LoginWindow : Window
{
    private readonly IAuthService _authService;

    public AuthenticatedUser? LoggedInUser { get; private set; }

    /// <summary>Design-time/XAML-loader constructor only — uses the dev in-memory store.</summary>
    public LoginWindow() : this(new AuthService(new InMemoryUserRepository()))
    {
    }

    public LoginWindow(IAuthService authService)
    {
        _authService = authService;
        InitializeComponent();
    }

    private void OnDragPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
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

        var result = await _authService.LoginAsync(userId, password);

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
            _ => "Login gagal."
        };
        errorText.IsVisible = true;
    }
}
