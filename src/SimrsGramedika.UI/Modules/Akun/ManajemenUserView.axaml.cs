using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Auth;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Akun;

public partial class ManajemenUserView : UserControl
{
    public event EventHandler? BackRequested;

    private readonly KhanzaAkunRepository _repo = new(DatabaseSettings.Load());
    private List<AksesItem> _akses = [];
    private IReadOnlyList<KhanzaUserRow> _users = [];
    private string? _editingId; // null = user baru

    public ManajemenUserView()
    {
        InitializeComponent();
    }

    private T C<T>(string name) where T : Control => this.FindControl<T>(name)!;

    public async Task ReloadAsync()
    {
        try
        {
            if (_akses.Count == 0)
                _akses = (await _repo.GetAksesColumnsAsync()).Select(AksesLabels.Create).ToList();
            await LoadUsersAsync();
            StartNew();
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal memuat: {ex.Message}", error: true);
        }
    }

    private async Task LoadUsersAsync()
    {
        _users = await _repo.ListUsersAsync(C<TextBox>("CariUserBox").Text);
        C<DataGrid>("UserGrid").ItemsSource = _users;
        C<TextBlock>("JumlahUserText").Text = $"{_users.Count} user · {_akses.Count} jenis hak akses";
        C<ComboBox>("SalinDariCombo").ItemsSource = _users.Select(u => string.IsNullOrEmpty(u.Nama) ? u.IdUser : $"{u.IdUser} - {u.Nama}").ToList();
    }

    private void StartNew()
    {
        _editingId = null;
        C<TextBox>("IdUserBox").Text = "";
        C<TextBox>("PasswordUserBox").Text = "";
        C<TextBlock>("ModeText").Text = "Mode: USER BARU — isi ID (NIK pegawai / kode dokter), password, lalu centang hak aksesnya.";
        foreach (var a in _akses) a.IsChecked = false;
        ApplyFilter();
    }

    private async void OnUserSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (C<DataGrid>("UserGrid").SelectedItem is not KhanzaUserRow row) return;
        try
        {
            var set = await _repo.GetUserAksesAsync(row.IdUser);
            _editingId = row.IdUser;
            C<TextBox>("IdUserBox").Text = row.IdUser;
            C<TextBox>("PasswordUserBox").Text = "";
            C<TextBlock>("ModeText").Text = $"Mode: EDIT '{row.IdUser}' {row.Nama} — kosongkan password bila tidak ingin mengubahnya.";
            foreach (var a in _akses) a.IsChecked = set.Contains(a.Column);
            ApplyFilter();
            SetStatus("", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal memuat hak akses: {ex.Message}", error: true);
        }
    }

    private IEnumerable<AksesItem> Visible()
    {
        var f = C<TextBox>("FilterAksesBox").Text?.Trim() ?? "";
        var onlyChecked = C<CheckBox>("HanyaDicentangBox").IsChecked == true;
        return _akses.Where(a =>
            (!onlyChecked || a.IsChecked) &&
            (f.Length == 0
             || a.Label.Contains(f, StringComparison.OrdinalIgnoreCase)
             || a.Column.Contains(f, StringComparison.OrdinalIgnoreCase)
             || a.Kategori.Contains(f, StringComparison.OrdinalIgnoreCase)));
    }

    private void ApplyFilter() => C<ListBox>("AksesList").ItemsSource = Visible().ToList();

    private void OnFilterAksesChanged(object? sender, RoutedEventArgs e) => ApplyFilter();

    private void OnCentangSemuaClick(object? sender, RoutedEventArgs e)
    {
        foreach (var a in Visible().ToList()) a.IsChecked = true;
    }

    private void OnKosongkanClick(object? sender, RoutedEventArgs e)
    {
        foreach (var a in Visible().ToList()) a.IsChecked = false;
        ApplyFilter();
    }

    private async void OnSalinClick(object? sender, RoutedEventArgs e)
    {
        var idx = C<ComboBox>("SalinDariCombo").SelectedIndex;
        if (idx < 0 || idx >= _users.Count) { SetStatus("Pilih user sumber dulu.", true); return; }
        var src = _users[idx];
        try
        {
            var set = await _repo.GetUserAksesAsync(src.IdUser);
            foreach (var a in _akses) a.IsChecked = set.Contains(a.Column);
            ApplyFilter();
            SetStatus($"Hak akses disalin dari '{src.IdUser}' ({set.Count}). Klik Simpan untuk menerapkan.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal menyalin: {ex.Message}", true);
        }
    }

    private void OnBaruClick(object? sender, RoutedEventArgs e)
    {
        C<DataGrid>("UserGrid").SelectedItem = null;
        StartNew();
        SetStatus("", false);
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        var id = C<TextBox>("IdUserBox").Text?.Trim() ?? "";
        var pw = C<TextBox>("PasswordUserBox").Text ?? "";
        var set = _akses.Where(a => a.IsChecked).Select(a => a.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            await _repo.SaveUserAsync(_editingId, id, pw, set);
            var wasNew = _editingId is null;
            await LoadUsersAsync();
            _editingId = id;
            C<TextBox>("PasswordUserBox").Text = "";
            C<TextBlock>("ModeText").Text = $"Mode: EDIT '{id}'";
            SetStatus(wasNew ? $"User '{id}' dibuat dengan {set.Count} hak akses." : $"User '{id}' disimpan ({set.Count} hak akses).", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal menyimpan: {ex.Message}", true);
        }
    }

    private async void OnHapusClick(object? sender, RoutedEventArgs e)
    {
        if (C<DataGrid>("UserGrid").SelectedItem is not KhanzaUserRow row) { SetStatus("Pilih user yang akan dihapus.", true); return; }
        if (!await Dialogs.ConfirmAsync(this, "Hapus User", $"Hapus user '{row.IdUser}' {row.Nama}? User ini tidak akan bisa login lagi (juga di Khanza lama).")) return;
        try
        {
            await _repo.DeleteUserAsync(row.IdUser);
            await LoadUsersAsync();
            StartNew();
            SetStatus($"User '{row.IdUser}' dihapus.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal menghapus: {ex.Message}", true);
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) => await SafeLoadUsersAsync();

    private async void OnCariUserKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await SafeLoadUsersAsync();
    }

    private async Task SafeLoadUsersAsync()
    {
        try { await LoadUsersAsync(); }
        catch (Exception ex) { SetStatus($"Gagal memuat user: {ex.Message}", true); }
    }

    private void SetStatus(string text, bool error)
    {
        var s = C<TextBlock>("StatusText");
        s.Text = text;
        s.Foreground = error ? Brush.Parse("#DC2626") : Brushes.Green;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
