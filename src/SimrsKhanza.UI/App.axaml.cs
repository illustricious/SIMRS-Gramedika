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
        Modules.Shared.DataGridHeaderFit.Register();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // TODO: replace with a real DI container + MySQL-backed IUserRepository
            // once SimrsKhanza.Data wires up EF Core (see docs/audit/HANDOVER.md).
            IAuthService authService = new AuthService(new InMemoryUserRepository());

            ShowLogin(desktop, authService, show: false);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowLogin(IClassicDesktopStyleApplicationLifetime desktop, IAuthService authService, bool show = true)
    {
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
            // "Keluar" returns to the login window: the new login window must become
            // MainWindow before the old one closes, or closing it would end the app.
            main.LogoutRequested += (_, _) =>
            {
                ShowLogin(desktop, authService, show: true);
                main.Close();
            };
            desktop.MainWindow = main;
            main.Show();
        };

        if (show) login.Show();
    }
}
