using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.UI.Modules.Pengaturan;

public partial class PengaturanView : UserControl
{
    public event EventHandler? BackRequested;

    public PengaturanView()
    {
        InitializeComponent();
    }

    public System.Threading.Tasks.Task ReloadAsync()
    {
        var settings = DatabaseSettings.Load();
        this.FindControl<TextBlock>("ActiveText")!.Text = $"Profil aktif: {settings.Name}  —  {settings.User}@{settings.Summary}";
        this.FindControl<CheckBox>("BatchBox")!.IsChecked = settings.AktifkanBatchObat;
        this.FindControl<ComboBox>("HppBox")!.SelectedIndex = settings.HppColumn == "h_beli" ? 1 : 0;
        this.FindControl<TextBlock>("StatusText")!.IsVisible = false;
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private TextBlock Status()
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        status.IsVisible = true;
        return status;
    }

    private async void OnTestConnectionClick(object? sender, RoutedEventArgs e)
    {
        var status = Status();
        Shared.Ui.Info(status, "Menghubungkan...");
        var hasil = await KoneksiDatabase.TesAsync(DatabaseSettings.Load());
        if (hasil.Ok) Shared.Ui.Ok(status, hasil.Pesan);
        else Shared.Ui.Err(status, hasil.Pesan);
    }

    private async void OnKoneksiClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var dlg = new KoneksiDatabaseWindow();
        await dlg.ShowDialog(owner);
        await ReloadAsync();
        if (dlg.Saved)
            Shared.Ui.Ok(Status(), "Koneksi disimpan. Layar yang dibuka setelah ini memakai server baru; disarankan Log Out lalu login ulang.");
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var status = Status();
        try
        {
            var current = DatabaseSettings.Load();
            DatabaseSettingsStore.Save(current.With(
                batch: this.FindControl<CheckBox>("BatchBox")!.IsChecked == true,
                hpp: this.FindControl<ComboBox>("HppBox")!.SelectedIndex == 1 ? "h_beli" : "dasar"));
            Shared.Ui.Ok(status, $"Tersimpan untuk profil '{current.Name}'.");
        }
        catch (Exception ex)
        {
            Shared.Ui.Err(status, $"Gagal menyimpan: {ex.Message}");
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
