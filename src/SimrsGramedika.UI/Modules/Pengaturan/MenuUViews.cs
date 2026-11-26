using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.Pengaturan;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Antrian;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Pengaturan;

internal sealed class MenuUMasterView : MasterView
{
    public MenuUMasterView(MasterSpec spec) : base(spec)
    {
        if (spec.Table is "setpenjualanumum" or "setpenjualan" or "setpenjualanperbarang")
            AddCommand("Terapkan Margin Tersimpan ke Harga Jual", () => TerapkanAsync(spec));
    }

    protected override double ParseNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            double.TryParse(value, NumberStyles.Float, Ui.Id, out number))
            return double.IsFinite(number) ? number : throw new FormatException("Masukkan angka yang valid.");
        throw new FormatException("Masukkan angka tanpa pemisah ribuan.");
    }

    private async Task TerapkanAsync(MasterSpec spec)
    {
        var keyColumn = spec.Keys.SingleOrDefault()?.Column;
        var key = keyColumn is null ? null : SelectedRow?.S(keyColumn);
        if (keyColumn is not null && string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Pilih margin jenis atau barang dari daftar.");
        var target = key is null ? "semua barang" : $"barang dengan kode {key}";
        if (!await Dialogs.ConfirmAsync(this, "Terapkan Margin",
                $"Hitung ulang harga jual {target} berdasarkan harga beli dan margin yang sudah disimpan?")) return;
        var count = await new MenuURepository(DatabaseSettings.Load()).TerapkanMarginAsync(spec.Table, key);
        Ui.Ok(StatusText, $"Harga jual diperbarui untuk {count} barang.");
    }
}

internal sealed class MenuUHubView : PageView
{
    public event EventHandler<MasterSpec>? SectionRequested;

    public MenuUHubView(string title, IReadOnlyList<MasterSpec> sections)
    {
        var links = sections.Select(s => Ui.Btn(s.Title, (_, _) => SectionRequested?.Invoke(this, s))).ToArray();
        Build(title, Ui.Bar(links));
    }

    public override Task ReloadAsync() => Task.CompletedTask;
}

internal sealed class MenuUKredensialView : PageView
{
    private readonly bool _eksekutif;
    private readonly MenuURepository _repo = new(DatabaseSettings.Load());
    private readonly LookupBox _penjamin = new();
    private readonly TextBox _id = Ui.Text(maxLen: 512);
    private readonly TextBox _password = new() { PasswordChar = '•', MaxLength = 512 };
    private readonly TextBox _ulang = new() { PasswordChar = '•', MaxLength = 512 };
    private readonly DataGrid _daftar = Ui.Grid(new Col("kd_pj", "Kode Cara Bayar"), new Col("png_jawab", "Cara Bayar"), new Col("status", "Status"));
    private readonly WrapPanel _actions;

    public MenuUKredensialView(bool eksekutif)
    {
        _eksekutif = eksekutif;
        var fields = new List<(string, Control)>();
        if (!eksekutif) fields.Add(("Cara Bayar", _penjamin));
        fields.AddRange([("ID Baru", _id), ("Kata Sandi Baru", _password), ("Ulangi Kata Sandi Baru", _ulang)]);
        var simpan = Ui.Btn("Simpan Kredensial", async (_, _) => await SimpanAsync());
        _actions = Ui.Bar(simpan, Ui.Btn("Hapus Kredensial", async (_, _) => await HapusAsync()));
        _actions.IsEnabled = false;

        var form = new StackPanel { Spacing = 8, MaxWidth = 580, Children = { Ui.Form(fields.ToArray()), _actions } };
        var body = new DockPanel();
        DockPanel.SetDock(form, Dock.Top);
        body.Children.Add(form);
        if (!eksekutif)
        {
            body.Children.Add(_daftar);
            _daftar.SelectionChanged += (_, _) =>
            {
                if (Ui.Selected<Row>(_daftar) is { } row) _penjamin.Kode = row.S("kd_pj");
                ResetCredentials();
            };
        }
        Build(eksekutif ? "Set E-Eksekutif" : "Password BPJS", body);
    }

    public override async Task ReloadAsync() => await RunAsync(async () =>
    {
        await LoadAsync();
        _actions.IsEnabled = true;
    });

    private async Task LoadAsync()
    {
        ResetCredentials();
        if (_eksekutif)
        {
            Ui.Info(StatusText, await _repo.AdaAkunEksekutifAsync() ? "Kredensial E-Eksekutif tersimpan." : "Kredensial E-Eksekutif belum diatur.");
            return;
        }
        var rows = await _repo.DaftarPenjaminAsync();
        _penjamin.SetItems(rows.Select(r => (r.S("kd_pj"), r.S("png_jawab"))));
        Ui.Fill(_daftar, rows);
        Ui.Info(StatusText, $"{rows.Count(r => r.S("status") == "Tersimpan")} cara bayar sudah diatur.");
    }

    private void ResetCredentials()
    {
        _id.Text = "";
        _password.Text = "";
        _ulang.Text = "";
    }

    private async Task UbahAsync(Func<Task> write, string done)
    {
        if (!_actions.IsEnabled) return;
        _actions.IsEnabled = false;
        try
        {
            await RunAsync(async () =>
            {
                await write();
                ResetCredentials();
                await LoadAsync();
            }, done);
        }
        finally { _actions.IsEnabled = true; }
    }

    private Task SimpanAsync() => UbahAsync(async () =>
    {
        var password = _password.Text ?? "";
        if (password != _ulang.Text) throw new ArgumentException("Kata sandi dan ulangannya harus sama.");
        if (_eksekutif) await _repo.SimpanAkunEksekutifAsync(_id.Text?.Trim() ?? "", password);
        else await _repo.SimpanAkunAsuransiAsync(_penjamin.Kode, _id.Text?.Trim() ?? "", password);
    }, "Kredensial tersimpan.");

    private async Task HapusAsync()
    {
        var kode = _penjamin.Kode;
        if (!_eksekutif && kode.Length == 0) { Ui.Err(StatusText, "Pilih cara bayar."); return; }
        var nama = _eksekutif ? "E-Eksekutif" : _penjamin.Nama;
        if (!await Dialogs.ConfirmAsync(this, "Hapus Kredensial", $"Hapus kredensial untuk {nama}?")) return;
        await UbahAsync(() => _eksekutif ? _repo.HapusAkunEksekutifAsync() : _repo.HapusAkunAsuransiAsync(kode), "Kredensial dihapus.");
    }
}

internal sealed class MenuUDisplayView : PageView
{
    private const int MaxImageBytes = 8 * 1024 * 1024;
    private readonly bool _apotek;
    private readonly MenuURepository _repo = new(DatabaseSettings.Load());
    private readonly CheckBox _aktif = new() { Content = "Tampilkan pengumuman pada papan antrian" };
    private readonly TextBox _teks = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 100 };
    private readonly TextBlock _gambarStatus = Ui.L("");
    private readonly WrapPanel _actions;
    private byte[]? _gambarBaru;

    public MenuUDisplayView(bool apotek)
    {
        _apotek = apotek;
        _actions = Ui.Bar(Ui.Btn("Simpan Display", async (_, _) => await SimpanAsync()),
            Ui.Btn("Pilih Gambar", async (_, _) => await RunAsync(PilihGambarAsync)),
            Ui.Btn("Hapus Gambar", (_, _) => { _gambarBaru = []; _gambarStatus.Text = "Gambar akan dihapus saat disimpan."; }));
        _actions.IsEnabled = false;
        var boardLinks = apotek
            ? Ui.Bar(Ui.Btn("Buka Papan Apotek", (_, _) => AntrianBoards.Open(AntrianBoardKind.Apotek2)),
                Ui.Btn("Papan Racikan", (_, _) => AntrianBoards.Open(AntrianBoardKind.Apotek2Racikan)),
                Ui.Btn("Papan Nonracikan", (_, _) => AntrianBoards.Open(AntrianBoardKind.Apotek2NonRacikan)))
            : Ui.Bar(Ui.Btn("Buka Papan Poli", (_, _) => AntrianBoards.Open(AntrianBoardKind.JadwalPoli)));
        Build(apotek ? "Display Antrian Apotek" : "Display Antrian Registrasi & Poli",
            new StackPanel { Spacing = 8, MaxWidth = 760, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Children = { _aktif, Ui.Form(("Teks Pengumuman", _teks)), _gambarStatus, _actions, boardLinks } });
    }

    public override async Task ReloadAsync() => await RunAsync(async () =>
    {
        var settings = await _repo.MuatDisplayAsync(_apotek);
        _aktif.IsChecked = settings?.Aktif ?? false;
        _teks.Text = settings?.Teks ?? "";
        _gambarBaru = null;
        _gambarStatus.Text = settings?.Gambar is { Length: > 0 } ? "Gambar tersimpan." : "Gambar belum diatur.";
        _actions.IsEnabled = true;
    });

    private async Task PilihGambarAsync()
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pilih gambar display", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PNG atau JPEG") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }]
        });
        if (files.Count == 0) return;
        await using var stream = await files[0].OpenReadAsync();
        using var data = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int length;
        while ((length = await stream.ReadAsync(buffer.AsMemory())) > 0)
        {
            if (data.Length + length > MaxImageBytes) throw new ArgumentException("Ukuran gambar maksimal 8 MB.");
            data.Write(buffer, 0, length);
        }
        data.Position = 0;
        using var preview = new Bitmap(data);
        _gambarBaru = data.ToArray();
        _gambarStatus.Text = $"{files[0].Name} · siap disimpan";
    }

    private async Task SimpanAsync()
    {
        if (!_actions.IsEnabled) return;
        _actions.IsEnabled = false;
        try
        {
            await RunAsync(async () =>
            {
                await _repo.SimpanDisplayAsync(_apotek, _teks.Text?.Trim() ?? "", _aktif.IsChecked == true, _gambarBaru);
                if (_gambarBaru is not null)
                    _gambarStatus.Text = _gambarBaru.Length > 0 ? "Gambar tersimpan." : "Gambar belum diatur.";
                _gambarBaru = null;
            }, "Pengaturan display tersimpan.");
        }
        finally { _actions.IsEnabled = true; }
    }
}

internal sealed class MenuUVakumView : PageView
{
    private readonly DatabaseSettings _settings = DatabaseSettings.Load();
    private readonly TextBox _cari = Ui.Text("Cari tabel");
    private readonly DataGrid _tabel = Ui.Grid(new Col("nama", "Tabel"), new Col("mesin", "Engine"), new Col("perkiraan", "Perkiraan Baris"));
    private readonly Button _hapus;

    public MenuUVakumView()
    {
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await ReloadAsync(); };
        _hapus = Ui.Btn("Kosongkan Tabel Terpilih", async (_, _) => await KosongkanAsync());
        var bar = Ui.Bar(_cari, Ui.Btn("Cari", async (_, _) => await ReloadAsync()), _hapus);
        DockPanel.SetDock(bar, Dock.Top);
        Build("Vakum Table", new DockPanel { Children = { bar, _tabel } }, $"Database aktif · {_settings.Summary}");
    }

    public override async Task ReloadAsync() => await RunAsync(async () =>
    {
        var rows = await new MenuURepository(_settings).DaftarTabelAsync(_cari.Text);
        Ui.Fill(_tabel, rows);
        Ui.Info(StatusText, $"{rows.Count} tabel");
    });

    private async Task KosongkanAsync()
    {
        if (Ui.Selected<Row>(_tabel) is not { } row)
        {
            Ui.Err(StatusText, "Pilih satu tabel.");
            return;
        }
        _hapus.IsEnabled = false;
        try
        {
            var form = new KonfirmasiVakum(_settings, row.S("nama"));
            if (!await form.ShowForAsync(this)) return;
            await ReloadAsync();
            Ui.Ok(StatusText, $"{form.Dihapus} baris dihapus dari {row.S("nama")}.");
        }
        finally { _hapus.IsEnabled = true; }
    }

    private sealed class KonfirmasiVakum : FormDialog
    {
        private readonly DatabaseSettings _settings;
        private readonly string _table;
        private readonly TextBox _konfirmasi = Ui.Text();
        public int Dihapus { get; private set; }

        public KonfirmasiVakum(DatabaseSettings settings, string table) : base("Kosongkan Tabel")
        {
            _settings = settings;
            _table = table;
            SetBody(new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Ui.Hint($"Seluruh isi tabel {table} di {settings.Summary} akan dihapus. Baris terkait dapat ikut terhapus sesuai aturan database. Pastikan cadangan tersedia."),
                    Ui.Form(($"Ketik {settings.Database}.{table}", _konfirmasi))
                }
            }, "Hapus Seluruh Isi Tabel");
        }

        protected override async Task OnSaveAsync() =>
            Dihapus = await new MenuURepository(_settings).KosongkanTabelAsync(_table, _konfirmasi.Text ?? "");
    }
}
