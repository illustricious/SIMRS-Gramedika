using Avalonia.Controls;
using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.UI;

public partial class MainWindow : Window
{
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
    }
}