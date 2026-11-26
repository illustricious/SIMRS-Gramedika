using Avalonia.Controls;
using Avalonia.Input;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Parkir;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Parkir;

public sealed class ParkirMasukView : PageView
{
    private readonly ParkirRepository _repo = new(DatabaseSettings.Load());
    private readonly TextBox _barcode = Ui.Text("Pindai atau ketik barcode", 15);
    private readonly TextBox _kendaraan = Ui.Text("Contoh B 1234 ABC", 15);
    private readonly TextBlock _kartu = Ui.L("");
    private readonly LookupBox _jenis = new() { PlaceholderText = "Cari jenis parkir" };
    private readonly LookupBox _petugas = new() { PlaceholderText = "Cari petugas" };
    private readonly TextBox _cari = Ui.Text("Cari plat, kartu, jenis, atau petugas");
    private readonly PeriodPicker _period = new(false);
    private readonly DataGrid _grid = Ui.Grid(
        new("no_kendaraan", "Nomor Kendaraan"), new("nomer_kartu", "Nomor Kartu"),
        new("tgl_masuk", "Tanggal Masuk"), new("jam_masuk", "Jam Masuk"), new("jenis_parkir", "Jenis Parkir"),
        new("petugas", "Petugas"), new("status", "Status"));
    private readonly StackPanel _entry;
    private readonly WrapPanel _filters;
    private readonly AuthenticatedUser? _user = AppSession.CurrentUser;
    private bool _busy;

    public ParkirMasukView()
    {
        _petugas.IsEnabled = _user?.IsAdminUtama == true;
        _barcode.TextChanged += (_, _) => _kartu.Text = "";
        _barcode.KeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await ScanAsync();
        };
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await DisplayAsync(); };

        _entry = new StackPanel
        {
            Children =
            {
                Ui.Bar(Ui.L("Jenis Parkir"), _jenis, Ui.L("Petugas"), _petugas),
                Ui.Bar(Ui.L("Barcode"), _barcode, Ui.Btn("Cek Kartu", async (_, _) => await ScanAsync()), _kartu,
                    Ui.L("Nomor Kendaraan"), _kendaraan, Ui.Btn("Simpan Masuk", async (_, _) => await SaveAsync())),
            },
            IsEnabled = false,
        };
        _filters = Ui.Bar(_cari, _period, Ui.Btn("Tampilkan", async (_, _) => await DisplayAsync()));
        var body = new DockPanel();
        DockPanel.SetDock(_entry, Dock.Top);
        body.Children.Add(_entry);
        body.Children.Add(_grid);
        Build("Parkir Masuk", body, "Pilih jenis parkir, pindai kartu, lalu isi nomor kendaraan.", _filters);
    }

    public override async Task ReloadAsync()
    {
        if (_busy) return;
        var loaded = false;
        await RunBusyAsync(async () =>
        {
            await LoadListAsync();
            var jenis = await _repo.JenisAsync();
            var nip = _user?.IsAdminUtama == true ? null : _user?.KodeLegacy ?? "";
            var petugas = await _repo.PetugasAsync(nip);
            if (nip is not null && petugas.Count == 0)
                throw new InvalidOperationException("Akun ini belum terdaftar di master Petugas.");

            _jenis.SetItems(jenis.Select(j => (j.S("kd_parkir"),
                $"{j.S("jns_parkir")} / Rp{Ui.Money(j.D("biaya"))} / {j.S("jenis")}")));
            _petugas.SetItems(petugas.Select(p => (p.S("nip"), p.S("nama"))));
            if (nip is not null) _petugas.Kode = petugas[0].S("nip");
            loaded = true;
        });
        _entry.IsEnabled = loaded;
        if (loaded) _barcode.Focus();
    }

    private Task DisplayAsync() => _entry.IsEnabled ? RunBusyAsync(LoadListAsync) : ReloadAsync();

    private async Task LoadListAsync()
    {
        var rows = await _repo.ListAsync(_period.From, _period.To, _cari.Text);
        Ui.Fill(_grid, rows);
        Ui.Info(StatusText, $"{rows.Count} kunjungan parkir");
    }

    private async Task ScanAsync()
    {
        var found = false;
        await RunBusyAsync(async () =>
        {
            _kartu.Text = $"Kartu {await _repo.CariKartuAsync(_barcode.Text)}";
            found = true;
        });
        if (found) _kendaraan.Focus();
    }

    private async Task SaveAsync()
    {
        var saved = false;
        await RunBusyAsync(async () =>
        {
            var kartu = await _repo.MasukAsync(_barcode.Text, _kendaraan.Text, _jenis.Kode, _petugas.Kode);
            _barcode.Text = "";
            _kendaraan.Text = "";
            saved = true;
            try
            {
                await LoadListAsync();
                Ui.Ok(StatusText, $"Parkir masuk tersimpan untuk kartu {kartu}.");
            }
            catch (Exception ex)
            {
                Ui.Err(StatusText, $"Parkir masuk kartu {kartu} tersimpan. Daftar gagal dimuat. {ex.Message}");
            }
        });
        if (saved) _barcode.Focus();
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        var entryEnabled = _entry.IsEnabled;
        _entry.IsEnabled = false;
        _filters.IsEnabled = false;
        try { await RunAsync(action); }
        finally
        {
            _entry.IsEnabled = entryEnabled;
            _filters.IsEnabled = true;
            _busy = false;
        }
    }
}
