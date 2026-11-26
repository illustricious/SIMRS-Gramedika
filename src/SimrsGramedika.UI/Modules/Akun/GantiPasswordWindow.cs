using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Auth;

namespace SimrsGramedika.UI.Modules.Akun;

/// <summary>Legacy frmUtama MnGantiPassword: the logged-in account changes its own password.</summary>
public class GantiPasswordWindow : Window
{
    public GantiPasswordWindow(AuthenticatedUser user)
    {
        Title = "Ganti Password";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var lama = new TextBox { PasswordChar = '*', PlaceholderText = "Password lama" };
        var baru = new TextBox { PasswordChar = '*', PlaceholderText = "Password baru" };
        var ulang = new TextBox { PasswordChar = '*', PlaceholderText = "Ulangi password baru" };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#DC2626") };
        var simpan = new Button { Content = "💾 Simpan", MinWidth = 90 };
        var batal = new Button { Content = "Batal", MinWidth = 90 };

        Content = new StackPanel
        {
            Margin = new Thickness(16), Spacing = 8,
            Children =
            {
                new TextBlock { Text = $"Akun: {user.DisplayName}", FontWeight = FontWeight.Medium },
                lama, baru, ulang, status,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { simpan, batal } },
            },
        };

        batal.Click += (_, _) => Close();
        simpan.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(baru.Text)) { status.Text = "Password baru wajib diisi."; return; }
            if (baru.Text != ulang.Text) { status.Text = "Ulangi password baru tidak sama."; return; }
            simpan.IsEnabled = false;
            try
            {
                var ok = await new KhanzaAkunRepository(DatabaseSettings.Load())
                    .ChangeOwnPasswordAsync(user.IsAdminUtama, user.UserId, lama.Text ?? "", baru.Text!);
                if (!ok) { status.Text = "Password lama salah."; return; }
                status.Foreground = Brushes.Green;
                status.Text = "Password berhasil diganti (berlaku juga di SIMRS Khanza lama).";
                await Task.Delay(1200);
                Close();
            }
            catch (Exception ex)
            {
                status.Text = $"Gagal: {ex.Message}";
            }
            finally
            {
                simpan.IsEnabled = true;
            }
        };
    }
}
