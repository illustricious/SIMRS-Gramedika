using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Auth;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Akun;

public partial class AdminView : UserControl
{
    public event EventHandler? BackRequested;

    private readonly KhanzaAkunRepository _repo = new(DatabaseSettings.Load());

    public AdminView()
    {
        InitializeComponent();
        this.FindControl<ListBox>("AdminList")!.SelectionChanged += (_, _) =>
        {
            if (this.FindControl<ListBox>("AdminList")!.SelectedItem is string s)
                this.FindControl<TextBox>("UsereBox")!.Text = s;
        };
    }

    public async Task ReloadAsync()
    {
        try
        {
            this.FindControl<ListBox>("AdminList")!.ItemsSource = await _repo.ListAdminsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal memuat admin: {ex.Message}", true);
        }
    }

    private (string User, string Pass)? ReadForm()
    {
        var u = this.FindControl<TextBox>("UsereBox")!.Text?.Trim() ?? "";
        var p = this.FindControl<TextBox>("PassBox")!.Text ?? "";
        var p2 = this.FindControl<TextBox>("Pass2Box")!.Text ?? "";
        if (u.Length == 0 || p.Length == 0) { SetStatus("ID admin dan password wajib diisi.", true); return null; }
        if (p != p2) { SetStatus("Ulangi password tidak sama.", true); return null; }
        return (u, p);
    }

    private void ClearPasswords()
    {
        this.FindControl<TextBox>("PassBox")!.Text = "";
        this.FindControl<TextBox>("Pass2Box")!.Text = "";
    }

    private async void OnTambahClick(object? sender, RoutedEventArgs e)
    {
        if (ReadForm() is not { } f) return;
        try
        {
            await _repo.AddAdminAsync(f.User, f.Pass);
            ClearPasswords();
            await ReloadAsync();
            SetStatus($"Admin '{f.User}' ditambahkan.", false);
        }
        catch (Exception ex) { SetStatus($"Gagal: {ex.Message}", true); }
    }

    private async void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (ReadForm() is not { } f) return;
        if (!await Dialogs.ConfirmAsync(this, "Reset Password", $"Ganti password admin '{f.User}'?")) return;
        try
        {
            await _repo.ResetAdminPasswordAsync(f.User, f.Pass);
            ClearPasswords();
            SetStatus($"Password admin '{f.User}' diganti.", false);
        }
        catch (Exception ex) { SetStatus($"Gagal: {ex.Message}", true); }
    }

    private async void OnHapusClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("AdminList")!.SelectedItem is not string u) { SetStatus("Pilih admin dulu.", true); return; }
        if (!await Dialogs.ConfirmAsync(this, "Hapus Admin", $"Hapus admin '{u}'?")) return;
        try
        {
            await _repo.DeleteAdminAsync(u);
            await ReloadAsync();
            SetStatus($"Admin '{u}' dihapus.", false);
        }
        catch (Exception ex) { SetStatus($"Gagal: {ex.Message}", true); }
    }

    private void SetStatus(string text, bool error)
    {
        var s = this.FindControl<TextBlock>("StatusText")!;
        s.Text = text;
        s.Foreground = error ? Brush.Parse("#DC2626") : Brushes.Green;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
