using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Auth;

namespace SimrsKhanza.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // TODO: replace with a real DI container + MySQL-backed IUserRepository
            // once SimrsKhanza.Data wires up EF Core (see docs/audit/HANDOVER.md).
            IAuthService authService = new AuthService(new InMemoryUserRepository());

            var login = new LoginWindow(authService);
            desktop.MainWindow = login;

            login.Closed += (_, _) =>
            {
                if (login.LoggedInUser is null)
                {
                    desktop.Shutdown();
                    return;
                }

                var main = new MainWindow(login.LoggedInUser);
                desktop.MainWindow = main;
                main.Show();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}