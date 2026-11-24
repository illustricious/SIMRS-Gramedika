using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatJalan;

internal sealed class TindakanRalanInputView : UserControl
{
    private readonly TindakanRalanInputService _service;
    private readonly TindakanRalanSnapshot? _asal;
    private readonly ComboBox _jenis = new()
    {
        ItemsSource = new[] { "Dokter", "Petugas", "Dokter dan Petugas" },
        SelectedIndex = 0,
        MinWidth = 190,
    };
    private readonly ComboBox _dokter = new() { MinWidth = 210 };
    private readonly ComboBox _petugas = new() { MinWidth = 210 };
    private readonly CalendarDatePicker _tanggal = new() { SelectedDate = DateTime.Today };
    private readonly TextBox _jam = new() { Width = 105, PlaceholderText = "HH:mm:ss", Text = DateTime.Now.ToString("HH:mm:ss") };
    private readonly TextBox _cari = new() { MinWidth = 220, PlaceholderText = "Kode, nama, atau kategori" };
    private readonly DataGrid _tarif = Ui.Grid(new("kode", "Kode", 95), new("nama", "Tindakan", 240),
        new("kategori", "Kategori", 150), new("tarif", "Tarif (Rp)", 100));
    private readonly TextBlock _status = Ui.Status();
    private readonly TextBlock _total = Ui.Hint("0 tindakan dipilih");
    private readonly Button _simpan;
    private readonly Grid _layout;
    private bool _busy;

    public TindakanRalanInputView(string noRawat, AuthenticatedUser user, TindakanRalanSnapshot? asal = null)
    {
        _service = new(DatabaseSettings.Load(), noRawat, user);
        _asal = asal;
        if (asal is not null)
        {
            _jenis.SelectedIndex = (int)asal.Jenis;
            _jenis.IsEnabled = false;
            _tanggal.SelectedDate = asal.Waktu.Date;
            _jam.Text = asal.Waktu.ToString("HH:mm:ss");
            _cari.Text = asal.Kode;
            _tarif.SelectionMode = DataGridSelectionMode.Single;
        }
        else _tarif.SelectionMode = DataGridSelectionMode.Extended;
        _simpan = Ui.Btn(asal is null ? "Simpan Tindakan" : "Simpan Perubahan", async (_, _) => await JalankanAsync(SimpanAsync), false);
        _simpan.Classes.Add("accent");
        _tarif.SelectionChanged += (_, _) => PerbaruiPilihan();
        _jenis.SelectionChanged += async (_, _) =>
        {
            AturJenis();
            await JalankanAsync(CariTarifAsync);
        };
        _cari.KeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await JalankanAsync(CariTarifAsync);
        };

        var form = Ui.Form(("Jenis", _jenis), ("Tanggal dan jam", Ui.Bar(_tanggal, _jam)),
            ("Pelaksana", Ui.Bar(_dokter, _petugas)),
            ("Cari tarif", Ui.Bar(_cari, Ui.Btn("Cari", async (_, _) => await JalankanAsync(CariTarifAsync)))));
        _dokter.PlaceholderText = "Pilih dokter";
        _petugas.PlaceholderText = "Pilih petugas";
        _layout = new Grid { RowDefinitions = new("Auto,Auto,*,Auto,Auto"), RowSpacing = 8, Margin = new Thickness(12) };
        var controls = new Control[] { Ui.Title(asal is null ? "Input Tindakan" : $"Ubah Tindakan {asal.Kode} — pilih tarif pengganti"), form, _tarif, Ui.Bar(_simpan, _total), _status };
        for (var i = 0; i < controls.Length; i++)
        {
            Grid.SetRow(controls[i], i);
            _layout.Children.Add(controls[i]);
        }
        Content = _layout;
        AturJenis();
        Loaded += async (_, _) =>
        {
            if (_dokter.ItemsSource is not null) return;
            await JalankanAsync(async () =>
            {
                var (dokter, petugas) = await _service.PelaksanaAsync();
                _dokter.ItemsSource = dokter;
                _petugas.ItemsSource = petugas;
                _dokter.SelectedItem = dokter.FirstOrDefault(p => p.Nik == (asal?.Dokter ?? user.UserId));
                _petugas.SelectedItem = petugas.FirstOrDefault(p => p.Nik == (asal?.Petugas ?? user.UserId));
                await CariTarifAsync();
            });
        };
    }

    private TindakanRalanJenis Jenis => (TindakanRalanJenis)_jenis.SelectedIndex;

    private void AturJenis()
    {
        _dokter.IsVisible = Jenis != TindakanRalanJenis.Paramedis;
        _petugas.IsVisible = Jenis != TindakanRalanJenis.Dokter;
    }

    private List<TarifTindakanRalan> Pilihan() =>
        _tarif.SelectedItems.OfType<GridRow>().Select(r => r.Tag).OfType<TarifTindakanRalan>().ToList();

    private void PerbaruiPilihan()
    {
        var pilihan = Pilihan();
        _simpan.IsEnabled = pilihan.Count != 0;
        _total.Text = $"{pilihan.Count} tindakan dipilih · Rp {Ui.Money(pilihan.Sum(t => t.Total))}";
    }

    private async Task CariTarifAsync()
    {
        _tarif.ItemsSource = null;
        var daftar = await _service.CariTarifAsync(Jenis, _cari.Text ?? "");
        Ui.FillRows(_tarif, daftar.Select(t => new GridRow([t.Kode, t.Nama, t.Kategori, Ui.Money(t.Total)], t)));
        Ui.Info(_status, daftar.Count == 500 ? "500 tarif ditampilkan. Persempit pencarian untuk menemukan tarif lainnya."
            : $"{daftar.Count} tarif. Tahan Ctrl untuk memilih beberapa tindakan.");
    }

    private async Task SimpanAsync()
    {
        if (_tanggal.SelectedDate is not { } tanggal ||
            !TimeOnly.TryParseExact(_jam.Text, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var jam))
            throw new ArgumentException("Isi tanggal dan jam berformat HH:mm:ss.");
        var pilihan = Pilihan();
        var dokter = (_dokter.SelectedItem as PegawaiPilihan)?.Nik ?? "";
        var petugas = (_petugas.SelectedItem as PegawaiPilihan)?.Nik ?? "";
        if (_asal is null)
            await _service.SimpanAsync(Jenis, tanggal.Date + jam.ToTimeSpan(), dokter, petugas, pilihan);
        else
        {
            if (pilihan.Count != 1) throw new ArgumentException("Pilih satu tarif pengganti.");
            await _service.UbahAsync(_asal, tanggal.Date + jam.ToTimeSpan(), dokter, petugas, pilihan[0]);
        }
        _tarif.SelectedItems.Clear();
        Ui.Ok(_status, _asal is null ? $"{pilihan.Count} tindakan dan jurnal tersimpan. Buka Daftar Tindakan untuk melihatnya."
            : "Perubahan dan jurnal tersimpan. Buka Daftar Tindakan untuk melihatnya.");
        if (_asal is not null) _simpan.IsEnabled = false;
    }

    private async Task JalankanAsync(Func<Task> aksi)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try
        {
            await aksi();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Ui.Err(_status, ex.Message);
        }
        catch (Exception)
        {
            Ui.Err(_status, "Operasi tindakan gagal. Periksa koneksi dan data tindakan sebelum mencoba lagi.");
        }
        finally
        {
            _layout.IsEnabled = true;
            _busy = false;
        }
    }
}
