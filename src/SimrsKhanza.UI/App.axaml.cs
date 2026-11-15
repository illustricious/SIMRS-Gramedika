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
            // Same accounts & hak akses as original SIMRS Khanza (`admin` / `user` tables,
            // AES 'nur'/'windi'). Settings are re-read per login so a change saved in
            // Pengaturan takes effect without restarting. Dev seed accounts work only
            // while the database is unreachable (see KhanzaLoginService).
            IAuthService authService = new KhanzaLoginService(
                Config.DatabaseSettings.Load,
                new AuthService(new InMemoryUserRepository()));

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
