using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Auth;

namespace SimrsGramedika.UI;

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
            // AES 'nur'/'windi'). Settings are re-read per login so the server picked on
            // the login screen takes effect immediately.
            IAuthService authService = new KhanzaLoginService(DatabaseSettings.Load);

            if (!DatabaseSettingsStore.HasSaved && !DatabaseSettings.HasEnvironment)
            {
                // First run on this computer: ask for the database connection before the login screen.
                var setup = new KoneksiDatabaseWindow(firstRun: true);
                desktop.MainWindow = setup;
                setup.Closed += (_, _) =>
                {
                    if (!setup.Saved)
                    {
                        desktop.Shutdown();
                        return;
                    }
                    ShowLogin(desktop, authService, show: true);
                };
            }
            else
            {
                ShowLogin(desktop, authService, show: false);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowLogin(IClassicDesktopStyleApplicationLifetime desktop, IAuthService authService, bool show = true)
    {
        var login = new LoginWindow(authService);
        desktop.MainWindow = login;

        login.Closed += (_, _) =>
        {
            if (ReferenceEquals(desktop.MainWindow, login))
                desktop.Shutdown();
        };
        login.LoginSucceeded += user =>
        {
            var main = new MainWindow(user);
            // "Keluar" returns to the login window: the new login window must become
            // MainWindow before the old one closes, or closing it would end the app.
            main.LogoutRequested += (_, _) =>
            {
                AppSession.CurrentUser = null;
                ShowLogin(desktop, authService, show: true);
                main.Close();
            };
            desktop.MainWindow = main;
            try
            {
                main.Show();
            }
            catch
            {
                desktop.MainWindow = login;
                AppSession.CurrentUser = null;
                main.Close();
                throw;
            }

            // Keep login behind the dashboard while its first frame is being presented.
            main.RequestAnimationFrame(_ => main.RequestAnimationFrame(_ =>
            {
                if (login.IsVisible && ReferenceEquals(desktop.MainWindow, main)) login.Close();
            }));
        };

        if (show) login.Show();
    }
}
