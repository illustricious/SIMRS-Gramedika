using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI;

/// <summary>
/// "Koneksi Database": saved server profiles (host, port, database, user, password,
/// farmasi options), connection test and database picker. Shown before the login
/// screen on first run, and from the login screen's ⚙ button any time after.
/// Replaces legacy setting/database.xml.
/// </summary>
public sealed class KoneksiDatabaseWindow : Window
{
    private readonly List<DatabaseSettings> _items;
    private readonly ListBox _list = new() { MinHeight = 200 };
    private readonly TextBox _name = Ui.Text("mis. Server Utama", 40);
    private readonly TextBox _host = Ui.Text("alamat IP / nama server", 100);
    private readonly TextBox _port = Ui.Text("3306", 5);
    private readonly TextBox _user = Ui.Text("user database", 64);
    private readonly TextBox _pass = new() { PasswordChar = '•', PlaceholderText = "password database", MinWidth = 160 };
    private readonly TextBox _db = Ui.Text("nama database, mis. sik", 64);
    private readonly ComboBox _dbList = new() { MinWidth = 150, IsVisible = false, PlaceholderText = "pilih database" };
    private readonly CheckBox _batch = new() { Content = "Aktifkan batch obat (stok dipotong per No.Batch/No.Faktur)" };
    private readonly ComboBox _hpp = Ui.Choice(["dasar", "h_beli"]);
    private readonly TextBox _web = Ui.Text("http://host/webapps");
    private readonly TextBlock _status = Ui.Status();
    private readonly Button _test, _save, _ambil;
    private int _index = -1;
    private bool _loading;

    /// <summary>True when the user saved; the saved profile is then the active one.</summary>
    public bool Saved { get; private set; }

    public KoneksiDatabaseWindow(bool firstRun = false)
    {
        Title = "Koneksi Database — SIMRS Gramedika";
        Width = 820;
        Height = 560;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = firstRun ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        try { Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SimrsGramedika.UI/Assets/SIMRS Gramedika Hospital System Icon.png"))); }
        catch { /* icon is cosmetic */ }
        Ui.Themed(this, BackgroundProperty, "ThemeBackgroundBrush");

        var saved = DatabaseSettingsStore.LoadAll();
        _items = saved.Items.ToList();
        if (_items.Count == 0) _items.Add(DatabaseSettings.FromEnvironment().With(name: "Server Utama"));

        _test = Ui.Btn("🔌 Tes Koneksi", async (_, _) => await TestAsync());
        _ambil = Ui.Btn("📋 Ambil daftar", async (_, _) => await AmbilDaftarAsync());
        _save = Ui.Btn("💾 Simpan & Gunakan", async (_, _) => await SaveAsync());
        _save.Classes.Add("accent");
        var batal = Ui.Btn(firstRun ? "Keluar" : "Batal", (_, _) => Close());

        _list.SelectionChanged += (_, _) =>
        {
            if (_loading || _list.SelectedIndex < 0 || _list.SelectedIndex == _index) return;
            if (!Commit(out var err))
            {
                Ui.Err(_status, err);
                Reselect(_index);
                return;
            }
            Load(_list.SelectedIndex);
        };
        _dbList.SelectionChanged += (_, _) => { if (_dbList.SelectedItem is string s) _db.Text = s; };
        _port.TextChanged += (_, _) => { if (_port.Text is { } t && t.Any(c => !char.IsDigit(c))) _port.Text = new string(t.Where(char.IsDigit).ToArray()); };

        var baru = Ui.Btn("+ Baru", (_, _) => Tambah(DatabaseSettings.FromEnvironment().With(name: UniqueName("Server Baru"))));
        var dup = Ui.Btn("⧉ Duplikat", (_, _) => { if (Commit(out _)) Tambah(_items[_index].With(name: UniqueName(_items[_index].Name + " (salin)"))); });
        var hapus = Ui.Btn("🗑 Hapus", (_, _) => Hapus());

        var left = new DockPanel { Margin = new Thickness(0, 0, 12, 0) };
        var leftTitle = new TextBlock { Text = "Profil Server", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(leftTitle, Dock.Top);
        var leftBar = new WrapPanel { Children = { baru, dup, hapus }, Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(leftBar, Dock.Bottom);
        left.Children.Add(leftTitle);
        left.Children.Add(leftBar);
        left.Children.Add(Ui.Box(_list));

        _db.HorizontalAlignment = HorizontalAlignment.Stretch;
        var dbRow = new DockPanel();
        DockPanel.SetDock(_ambil, Dock.Right);
        DockPanel.SetDock(_dbList, Dock.Right);
        _ambil.Margin = new Thickness(6, 0, 0, 0);
        _dbList.Margin = new Thickness(6, 0, 0, 0);
        dbRow.Children.Add(_ambil);
        dbRow.Children.Add(_dbList);
        dbRow.Children.Add(_db);

        var show = new CheckBox { Content = "Tampilkan" };
        show.IsCheckedChanged += (_, _) => _pass.PasswordChar = show.IsChecked == true ? '\0' : '•';
        var passRow = new DockPanel();
        DockPanel.SetDock(show, Dock.Right);
        show.Margin = new Thickness(8, 0, 0, 0);
        passRow.Children.Add(show);
        passRow.Children.Add(_pass);

        _port.MaxWidth = 120;
        _port.HorizontalAlignment = HorizontalAlignment.Left;
        _hpp.HorizontalAlignment = HorizontalAlignment.Left;
        _hpp.MinWidth = 140;

        var right = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Form(
                    ("Nama Profil", _name),
                    ("Host / IP Server", _host),
                    ("Port", _port),
                    ("User", _user),
                    ("Password", passRow),
                    ("Database", dbRow)),
                Ui.Hint("Sama seperti isian setting/database.xml di SIMRS Khanza lama. Klik 'Ambil daftar' setelah mengisi host, port, user dan password untuk memilih database dari server."),
                new TextBlock { Text = "Farmasi", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 12, 0, 0) },
                _batch,
                Ui.Form(("Kolom HPP farmasi", _hpp)),
                Ui.Hint("Samakan dengan AKTIFKANBATCHOBAT dan HPPFARMASI di Khanza lama supaya pemotongan stok & HPP sama."),
                new TextBlock { Text = "Aplikasi Web Khanza", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 12, 0, 0) },
                Ui.Form(("Alamat web", _web)),
                Ui.Hint("Folder webapps Khanza (HOSTHYBRIDWEB + HYBRIDWEB), dipakai menu Kepegawaian & Gaji. Kosong = http://<host>/webapps."),
            },
        };

        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                Ui.Title("Koneksi Database"),
                Ui.Hint(firstRun
                    ? "Selamat datang di SIMRS Gramedika. Isi dulu koneksi ke database SIMRS Khanza (MySQL/MariaDB), lalu klik Tes Koneksi dan Simpan."
                    : "Pilih atau ubah server database yang dipakai. Profil disimpan di komputer ini untuk user Windows/Linux yang sedang login."),
            },
        };
        header.Children[0].Margin = new Thickness(0, 0, 0, 4);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { _test, _save, batal } };
        _status.Margin = new Thickness(0, 8);

        var body = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("220,*") };
        var scroll = new ScrollViewer { Content = right, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Avalonia.Controls.Grid.SetColumn(scroll, 1);
        body.Children.Add(left);
        body.Children.Add(scroll);

        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(buttons);
        root.Children.Add(_status);
        root.Children.Add(body);
        Content = root;

        RefreshList();
        var active = _items.FindIndex(i => i.Name == saved.Active);
        Load(active >= 0 ? active : 0);
    }

    private void RefreshList()
    {
        _loading = true;
        _list.ItemsSource = _items.Select(i => $"{i.Name}\n{i.Summary}").ToList();
        _loading = false;
    }

    private void Reselect(int i)
    {
        _loading = true;
        _list.SelectedIndex = i;
        _loading = false;
    }

    private void Load(int i)
    {
        _index = i;
        var s = _items[i];
        _name.Text = s.Name;
        _host.Text = s.Host;
        _port.Text = s.Port.ToString();
        _user.Text = s.User;
        _pass.Text = s.Password;
        _db.Text = s.Database;
        _batch.IsChecked = s.AktifkanBatchObat;
        _hpp.SelectedIndex = s.HppColumn == "h_beli" ? 1 : 0;
        _web.Text = s.WebUrl;
        _dbList.IsVisible = false;
        _dbList.ItemsSource = null;
        _status.Text = "";
        Reselect(i);
    }

    private DatabaseSettings? ReadForm(out string error)
    {
        error = "";
        var name = (_name.Text ?? "").Trim();
        var host = (_host.Text ?? "").Trim();
        var db = (_db.Text ?? "").Trim();
        if (name.Length == 0) { error = "Nama profil wajib diisi."; return null; }
        if (_items.Where((_, j) => j != _index).Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { error = $"Nama profil '{name}' sudah dipakai."; return null; }
        if (host.Length == 0) { error = "Host / IP server wajib diisi."; return null; }
        if (!int.TryParse(_port.Text, out var port) || port is < 1 or > 65535) { error = "Port harus angka 1–65535 (MySQL biasanya 3306)."; return null; }
        if (db.Length == 0) { error = "Nama database wajib diisi."; return null; }
        return new DatabaseSettings
        {
            Name = name, Host = host, Port = port, Database = db,
            User = (_user.Text ?? "").Trim(), Password = _pass.Text ?? "",
            AktifkanBatchObat = _batch.IsChecked == true,
            HppFarmasi = _hpp.SelectedIndex == 1 ? "h_beli" : "dasar",
            WebUrl = (_web.Text ?? "").Trim(),
        };
    }

    /// <summary>Writes the form back into the working list.</summary>
    private bool Commit(out string error)
    {
        var s = ReadForm(out error);
        if (s is null) return false;
        _items[_index] = s;
        RefreshList();
        Reselect(_index);
        return true;
    }

    private void Tambah(DatabaseSettings s)
    {
        if (_index >= 0 && !Commit(out var err)) { Ui.Err(_status, err); return; }
        _items.Add(s);
        RefreshList();
        Load(_items.Count - 1);
        _name.Focus();
        _name.SelectAll();
    }

    private void Hapus()
    {
        if (_items.Count <= 1) { Ui.Err(_status, "Minimal harus ada satu profil."); return; }
        _items.RemoveAt(_index);
        RefreshList();
        Load(Math.Min(_index, _items.Count - 1));
        Ui.Info(_status, "Profil dihapus dari daftar (tersimpan permanen setelah klik Simpan).");
    }

    private string UniqueName(string baseName)
    {
        var name = baseName;
        for (var n = 2; _items.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); n++) name = $"{baseName} {n}";
        return name;
    }

    private async Task<T?> BusyAsync<T>(string msg, Func<Task<T>> work)
    {
        _test.IsEnabled = _save.IsEnabled = _ambil.IsEnabled = false;
        Ui.Info(_status, msg);
        try { return await work(); }
        finally { _test.IsEnabled = _save.IsEnabled = _ambil.IsEnabled = true; }
    }

    private async Task<KoneksiDatabase.HasilTes?> TestAsync()
    {
        var s = ReadForm(out var err);
        if (s is null) { Ui.Err(_status, err); return null; }
        var hasil = await BusyAsync("Menghubungkan ke " + s.Summary + " ...", () => KoneksiDatabase.TesAsync(s));
        if (hasil is null) return null;
        if (hasil.Ok) Ui.Ok(_status, "✔ " + hasil.Pesan);
        else Ui.Err(_status, "✖ " + hasil.Pesan);
        return hasil;
    }

    private async Task AmbilDaftarAsync()
    {
        var s = ReadForm(out _) ?? new DatabaseSettings
        {
            Host = (_host.Text ?? "").Trim(), Port = int.TryParse(_port.Text, out var p) ? p : 3306,
            User = (_user.Text ?? "").Trim(), Password = _pass.Text ?? "",
        };
        try
        {
            var list = await BusyAsync("Mengambil daftar database dari " + s.Host + " ...", () => KoneksiDatabase.DaftarDatabaseAsync(s));
            if (list is null || list.Count == 0) { Ui.Err(_status, "Server tidak mengembalikan database apa pun untuk user ini."); return; }
            _dbList.ItemsSource = list;
            _dbList.IsVisible = true;
            var cur = list.IndexOf((_db.Text ?? "").Trim());
            _dbList.SelectedIndex = cur;
            if (cur < 0) _dbList.IsDropDownOpen = true;
            Ui.Ok(_status, $"{list.Count} database ditemukan. Pilih dari daftar di samping kolom Database.");
        }
        catch (MySqlConnector.MySqlException ex)
        {
            Ui.Err(_status, "✖ " + KoneksiDatabase.Jelaskan(ex));
        }
        catch (Exception ex)
        {
            Ui.Err(_status, "✖ Gagal mengambil daftar: " + ex.Message);
        }
    }

    private async Task SaveAsync()
    {
        if (!Commit(out var err)) { Ui.Err(_status, err); return; }
        var hasil = await TestAsync();
        if (hasil is null) return;
        if (!hasil.Ok && !await Dialogs.ConfirmAsync(this, "Koneksi gagal",
                hasil.Pesan + "\n\nTetap simpan profil ini?"))
            return;

        try
        {
            DatabaseSettingsStore.SaveAll(new DatabaseSettingsStore.Profiles { Active = _items[_index].Name, Items = _items.ToList() });
            Saved = true;
            Close();
        }
        catch (Exception ex)
        {
            Ui.Err(_status, "Gagal menyimpan: " + ex.Message);
        }
    }
}
