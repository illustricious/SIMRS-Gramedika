using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Pasien;

internal sealed class CatatanPasienWindow : Window
{
    private readonly PatientMenuRequest _patient;
    private readonly AuthenticatedUser _user;
    private readonly TextBox _catatan = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        MinHeight = 240, VerticalContentAlignment = VerticalAlignment.Top, MaxLength = 12000 };
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _save;
    private readonly Button _delete;

    public CatatanPasienWindow(PatientMenuRequest patient, AuthenticatedUser user)
    {
        _patient = patient;
        _user = user;
        Title = "Catatan Untuk Pasien";
        Width = 740;
        Height = 520;
        MinWidth = 520;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _save = new Button { Content = "Simpan Catatan", MinWidth = 120, MinHeight = 34, Classes = { "accent" } };
        _delete = new Button { Content = "Hapus Catatan", MinWidth = 120, MinHeight = 34 };
        var close = new Button { Content = "Tutup", MinWidth = 90, MinHeight = 34 };
        _save.Click += async (_, _) => await SimpanAsync();
        _delete.Click += async (_, _) => await HapusAsync();
        close.Click += (_, _) => Close();

        var layout = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new("Auto,Auto,*,Auto,Auto"),
            RowSpacing = 10,
            Children =
            {
                Ui.Title("Catatan Untuk Pasien"),
                Ui.Hint($"{patient.NamaPasien} | No. RM {patient.NoRekamMedis}"),
                _catatan,
                Ui.Bar(_save, _delete, close),
                _status,
            },
        };
        Grid.SetRow(layout.Children[1], 1);
        Grid.SetRow(_catatan, 2);
        Grid.SetRow(layout.Children[3], 3);
        Grid.SetRow(_status, 4);
        Content = layout;
        // Cegah ketikan atau simpan sebelum catatan lama selesai dimuat.
        _catatan.IsEnabled = _save.IsEnabled = _delete.IsEnabled = false;
        Loaded += async (_, _) => await MuatAsync();
        Closed += (_, _) => _catatan.Clear();
    }

    private async Task MuatAsync()
    {
        try
        {
            _catatan.Text = await new CatatanPasienService(DatabaseSettings.Load()).BacaAsync(_patient.NoRekamMedis);
            _catatan.IsEnabled = _save.IsEnabled = true;
            _delete.IsEnabled = !string.IsNullOrWhiteSpace(_catatan.Text);
            _status.Text = string.IsNullOrWhiteSpace(_catatan.Text) ? "Belum ada catatan untuk pasien ini." : "Catatan pasien dimuat.";
        }
        catch (Exception ex)
        {
            _save.IsEnabled = _delete.IsEnabled = false;
            _status.Text = $"Gagal memuat catatan: {ex.Message}";
        }
    }

    private async Task SimpanAsync()
    {
        if (!_user.HasPermission("catatan_pasien")) return;
        if (string.IsNullOrWhiteSpace(_catatan.Text))
        {
            _status.Text = "Isi catatan masih kosong. Gunakan Hapus Catatan untuk menghapusnya.";
            return;
        }
        _save.IsEnabled = _delete.IsEnabled = false;
        try
        {
            await new CatatanPasienService(DatabaseSettings.Load()).SimpanAsync(_patient.NoRekamMedis, _catatan.Text);
            Close();
        }
        catch (Exception ex)
        {
            _status.Text = $"Gagal menyimpan catatan: {ex.Message}";
        }
        finally
        {
            if (IsVisible) _save.IsEnabled = true;
            if (IsVisible) _delete.IsEnabled = !string.IsNullOrWhiteSpace(_catatan.Text);
        }
    }

    private async Task HapusAsync()
    {
        if (!_user.HasPermission("catatan_pasien") || string.IsNullOrWhiteSpace(_catatan.Text)) return;
        if (!await Dialogs.ConfirmAsync(this, "Hapus Catatan Pasien", $"Hapus catatan untuk {_patient.NamaPasien} (No. RM {_patient.NoRekamMedis})?")) return;
        _save.IsEnabled = _delete.IsEnabled = false;
        try
        {
            await new CatatanPasienService(DatabaseSettings.Load()).HapusAsync(_patient.NoRekamMedis);
            Close();
        }
        catch (Exception ex)
        {
            _status.Text = $"Gagal menghapus catatan: {ex.Message}";
        }
        finally
        {
            if (IsVisible) _save.IsEnabled = true;
            if (IsVisible) _delete.IsEnabled = !string.IsNullOrWhiteSpace(_catatan.Text);
        }
    }
}
