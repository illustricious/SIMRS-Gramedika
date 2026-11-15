using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

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
        this.FindControl<TextBox>("HostBox")!.Text = settings.Host;
        this.FindControl<TextBox>("PortBox")!.Text = settings.Port.ToString();
        this.FindControl<TextBox>("DatabaseBox")!.Text = settings.Database;
        this.FindControl<TextBox>("UserBox")!.Text = settings.User;
        this.FindControl<TextBox>("PasswordBox")!.Text = settings.Password;
        this.FindControl<CheckBox>("BatchBox")!.IsChecked = settings.AktifkanBatchObat;
        this.FindControl<ComboBox>("HppBox")!.SelectedIndex = settings.HppColumn == "h_beli" ? 1 : 0;
        this.FindControl<TextBlock>("StatusText")!.IsVisible = false;
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private DatabaseSettings ReadFormValues() => new()
    {
        Host = this.FindControl<TextBox>("HostBox")!.Text ?? "localhost",
        Port = int.TryParse(this.FindControl<TextBox>("PortBox")!.Text, out var p) ? p : 3306,
        Database = this.FindControl<TextBox>("DatabaseBox")!.Text ?? "sik",
        User = this.FindControl<TextBox>("UserBox")!.Text ?? "root",
        Password = this.FindControl<TextBox>("PasswordBox")!.Text ?? "",
        AktifkanBatchObat = this.FindControl<CheckBox>("BatchBox")!.IsChecked == true,
        HppFarmasi = this.FindControl<ComboBox>("HppBox")!.SelectedIndex == 1 ? "h_beli" : "dasar",
    };

    private async void OnTestConnectionClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        status.IsVisible = true;
        status.Foreground = Avalonia.Media.Brushes.Gray;
        status.Text = "Menghubungkan...";

        try
        {
            var settings = ReadFormValues();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var ok = await db.Database.CanConnectAsync();
            status.Foreground = ok ? Avalonia.Media.Brushes.Green : Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = ok ? "Berhasil terhubung ke database." : "Tidak bisa konek — cek host/port/user/password.";
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal konek: {ex.Message}";
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        try
        {
            DatabaseSettingsStore.Save(ReadFormValues());
            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = "Tersimpan. Pengaturan ini akan dipakai mulai layar berikutnya yang konek DB.";
            status.IsVisible = true;
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal menyimpan: {ex.Message}";
            status.IsVisible = true;
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
