using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatInap;

internal sealed class TindakanRanapWindow : Window
{
    private static readonly string[] NamaJenis = ["Dokter", "Petugas", "Dokter dan petugas"];

    private readonly TindakanRanapInputService _service;
    private readonly ComboBox _jenis = new() { ItemsSource = NamaJenis, SelectedIndex = 0 };
    private readonly ComboBox _dokter = new() { PlaceholderText = "Pilih dokter", MinWidth = 180, DisplayMemberBinding = new Binding(nameof(PegawaiPilihan.Nama)) };
    private readonly ComboBox _petugas = new() { PlaceholderText = "Pilih petugas", MinWidth = 180, DisplayMemberBinding = new Binding(nameof(PegawaiPilihan.Nama)) };
    private readonly CalendarDatePicker _tanggal = new() { SelectedDate = DateTime.Today };
    private readonly TextBox _jam = new() { Text = DateTime.Now.ToString("HH:mm:ss"), Width = 100 };
    private readonly TextBox _cari = new() { PlaceholderText = "Cari tindakan", Width = 245 };
    private readonly DataGrid _tarif = Ui.Grid(new("kode", "Kode", 105), new("nama", "Tindakan", 290),
        new("kategori", "Kategori", 145), new("tarif", "Tarif", 110));
    private readonly DataGrid _riwayat = Ui.Grid(new("waktu", "Waktu", 150), new("nama", "Tindakan", 270),
        new("dokter", "Dokter", 180), new("petugas", "Petugas", 180), new("biaya", "Biaya", 100));
    private readonly TextBlock _mode = Ui.Hint("");
    private readonly Button _simpan;
    private readonly Button _batalUbah;
    private readonly TabControl _tabs;
    private readonly TextBlock _status = Ui.Status();
    private readonly Grid _layout;
    private BarisTindakanRanap? _diubah;
    private bool _busy;
    private TindakanRalanJenis Jenis => (TindakanRalanJenis)_jenis.SelectedIndex;

    public TindakanRanapWindow(RawatInapListItem visit, AuthenticatedUser user)
    {
        _service = new(DatabaseSettings.Load(), visit.Key, visit.NoRekamMedis, user);
        Title = "Tindakan Rawat Inap";
        if (KunciTanggal.Aktif(DatabaseSettings.Load(), user.IsAdminUtama)) _tanggal.IsEnabled = _jam.IsEnabled = false;
        Width = 960;
        Height = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _tarif.SelectionMode = DataGridSelectionMode.Extended;
        _riwayat.SelectionMode = DataGridSelectionMode.Extended;
        _petugas.IsVisible = false;
        _mode.IsVisible = false;
        _jenis.SelectionChanged += async (_, _) =>
        {
            _dokter.IsVisible = Jenis != TindakanRalanJenis.Paramedis;
            _petugas.IsVisible = Jenis != TindakanRalanJenis.Dokter;
            await JalankanAsync(CariAsync);
        };
        var identitas = Ui.Hint($"{visit.NamaPasien} · No. RM {visit.NoRekamMedis} · No. Rawat {visit.NoRawat} · Kamar {visit.KodeKamar}");
        var input = Ui.Bar(_jenis, _dokter, _petugas);
        var waktu = Ui.Bar(new TextBlock { Text = "Waktu tindakan" }, _tanggal, _jam);
        var cari = Ui.Bar(_cari, Ui.Btn("Cari", async (_, _) => await JalankanAsync(CariAsync)), Ui.Hint("Ctrl+klik untuk memilih beberapa tindakan."));
        _simpan = Ui.Btn("Simpan Tindakan", async (_, _) => await JalankanAsync(_diubah is null ? SimpanAsync : SimpanPerubahanAsync));
        _batalUbah = Ui.Btn("Batal Ubah", async (_, _) => { SelesaiUbah(); await JalankanAsync(CariAsync); });
        _batalUbah.IsVisible = false;
        var formulir = Susun("Auto,Auto,Auto,Auto,*,Auto", _mode, input, waktu, cari, _tarif, Ui.Bar(_simpan, _batalUbah));
        var riwayat = Susun("*,Auto", _riwayat, Ui.Bar(
            Ui.Btn("Ubah Terpilih", async (_, _) => await MulaiUbahAsync()),
            Ui.Btn("Hapus Terpilih", async (_, _) => await JalankanAsync(HapusAsync)),
            Ui.Btn("Muat Ulang", async (_, _) => await JalankanAsync(MuatRiwayatAsync)),
            Ui.Hint("Hapus dan ubah mencatat jurnal pembalik.")));
        _tabs = new TabControl
        {
            ItemsSource = new[] { new TabItem { Header = "Input", Content = formulir }, new TabItem { Header = "Riwayat", Content = riwayat } },
            SelectedIndex = visit.BelumPulang ? 0 : 1,
        };
        _layout = Susun("Auto,Auto,*,Auto,Auto", Ui.Title(Title), identitas, _tabs, Ui.Bar(Ui.Btn("Tutup", (_, _) => Close())), _status);
        _layout.Margin = new Thickness(14);
        Content = _layout;
        Closing += (_, args) => args.Cancel = _busy;
        Loaded += async (_, _) => await JalankanAsync(async () =>
        {
            var (dokter, petugas) = await _service.PelaksanaAsync();
            _dokter.ItemsSource = dokter;
            _petugas.ItemsSource = petugas;
            _dokter.SelectedItem = dokter.FirstOrDefault(p => p.Nik == user.UserId);
            _petugas.SelectedItem = petugas.FirstOrDefault(p => p.Nik == user.UserId);
            await MuatRiwayatAsync();
            if (visit.BelumPulang) await CariAsync();
            else Ui.Info(_status, "Segmen kamar ini sudah ditutup: tindakan baru tidak bisa ditambahkan, riwayat masih bisa dikoreksi.");
        });
    }

    private static Grid Susun(string baris, params Control[] controls)
    {
        var grid = new Grid { RowSpacing = 8, RowDefinitions = new(baris) };
        for (var index = 0; index < controls.Length; index++)
        {
            Grid.SetRow(controls[index], index);
            grid.Children.Add(controls[index]);
        }
        return grid;
    }

    private async Task CariAsync()
    {
        _tarif.ItemsSource = null;
        try
        {
            var rows = await _service.CariAsync(Jenis, _cari.Text ?? "");
            Ui.FillRows(_tarif, rows.Select(row => new GridRow([row.Kode, row.Nama, row.Kategori, $"{row.Total:N2}"], row)));
        }
        catch (InvalidOperationException) when (_diubah is not null)
        {
            Ui.Info(_status, "Tindakan tidak bisa diganti karena segmen kamar ini sudah ditutup. Waktu dan pelaksana masih bisa diubah.");
        }
    }

    private async Task MuatRiwayatAsync()
    {
        var rows = await _service.DaftarAsync();
        Ui.FillRows(_riwayat, rows.Select(b => new GridRow(
            [$"{b.Isi.Waktu:yyyy-MM-dd HH:mm:ss}", b.Nama, b.NamaDokter, b.NamaPetugas, $"{b.Isi.Biaya:N2}"], b)));
    }

    private async Task SimpanAsync()
    {
        var waktu = Waktu();
        var rows = Pilihan();
        if (rows.Length == 0) throw new ArgumentException("Pilih tindakan yang akan disimpan.");
        if (!await Dialogs.ConfirmAsync(this, "Simpan tindakan", $"Catat {rows.Length} tindakan dengan tarif Rp{rows.Sum(r => r.Total):N2}?")) return;
        await _service.SimpanAsync(Jenis, waktu, Dokter, Petugas, rows);
        _tarif.SelectedItems.Clear();
        await MuatRiwayatAsync();
        Ui.Ok(_status, $"{rows.Length} tindakan dan jurnal tersimpan.");
    }

    private async Task MulaiUbahAsync()
    {
        if (_busy) return;
        var terpilih = Terpilih();
        if (terpilih.Length != 1) { Ui.Err(_status, "Pilih satu tindakan di riwayat untuk diubah."); return; }
        var baris = terpilih[0];
        SelesaiUbah();
        _diubah = baris;
        _mode.Text = $"Mengubah {baris.Nama}, {baris.Isi.Waktu:yyyy-MM-dd HH:mm:ss}. Biarkan daftar tindakan tidak dipilih bila tindakannya tetap.";
        _mode.IsVisible = _batalUbah.IsVisible = true;
        _simpan.Content = "Simpan Perubahan";
        _tarif.SelectionMode = DataGridSelectionMode.Single;
        _jenis.IsEnabled = false;
        _tanggal.SelectedDate = baris.Isi.Waktu.Date;
        _jam.Text = baris.Isi.Waktu.ToString("HH:mm:ss");
        var gantiJenis = _jenis.SelectedIndex != (int)baris.Isi.Jenis;
        _dokter.SelectedItem = (_dokter.ItemsSource as IEnumerable<PegawaiPilihan>)?.FirstOrDefault(p => p.Nik == baris.Isi.Dokter);
        _petugas.SelectedItem = (_petugas.ItemsSource as IEnumerable<PegawaiPilihan>)?.FirstOrDefault(p => p.Nik == baris.Isi.Petugas);
        _tabs.SelectedIndex = 0;
        // Mengganti jenis memuat ulang daftar tarif lewat SelectionChanged.
        if (gantiJenis) _jenis.SelectedIndex = (int)baris.Isi.Jenis;
        else await JalankanAsync(CariAsync);
    }

    private async Task SimpanPerubahanAsync()
    {
        var asal = _diubah!;
        var waktu = Waktu();
        var tarif = Pilihan().SingleOrDefault();
        var ganti = tarif is null ? "Tindakan tetap." : $"Tindakan menjadi {tarif.Nama} dengan tarif Rp{tarif.Total:N2}.";
        if (!await Dialogs.ConfirmAsync(this, "Ubah tindakan", $"Simpan perubahan {asal.Nama}? {ganti}")) return;
        await _service.UbahAsync(asal.Isi, waktu, Dokter, Petugas, tarif);
        SelesaiUbah();
        await MuatRiwayatAsync();
        _tabs.SelectedIndex = 1;
        Ui.Ok(_status, "Tindakan diubah dan jurnal tercatat.");
    }

    private void SelesaiUbah()
    {
        if (_diubah is null) return;
        _diubah = null;
        _mode.IsVisible = _batalUbah.IsVisible = false;
        _simpan.Content = "Simpan Tindakan";
        _tarif.SelectedItems.Clear();
        _tarif.SelectionMode = DataGridSelectionMode.Extended;
        _jenis.IsEnabled = true;
        _tanggal.SelectedDate = DateTime.Today;
        _jam.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private async Task HapusAsync()
    {
        var rows = Terpilih();
        if (rows.Length == 0) throw new ArgumentException("Pilih tindakan di riwayat yang akan dihapus.");
        if (!await Dialogs.ConfirmAsync(this, "Hapus tindakan",
                $"Hapus {rows.Length} tindakan senilai Rp{rows.Sum(r => r.Isi.Biaya):N2}? Jurnal pembalik akan dicatat.")) return;
        await _service.HapusAsync(rows.Select(r => r.Isi).ToArray());
        SelesaiUbah();
        await MuatRiwayatAsync();
        Ui.Ok(_status, $"{rows.Length} tindakan dihapus dan jurnal pembalik tercatat.");
    }

    private DateTime Waktu()
    {
        if (_tanggal.SelectedDate is not { } tanggal || !TimeOnly.TryParseExact(_jam.Text, "HH:mm:ss", out var jam))
            throw new ArgumentException("Isi tanggal dan jam tindakan dalam format JJ:mm:dd.");
        return DateOnly.FromDateTime(tanggal).ToDateTime(jam);
    }

    private string Dokter => (_dokter.SelectedItem as PegawaiPilihan)?.Nik ?? "";
    private string Petugas => (_petugas.SelectedItem as PegawaiPilihan)?.Nik ?? "";
    private TarifTindakanRanap[] Pilihan() => _tarif.SelectedItems.OfType<GridRow>().Select(r => r.Tag).OfType<TarifTindakanRanap>().ToArray();
    private BarisTindakanRanap[] Terpilih() => _riwayat.SelectedItems.OfType<GridRow>().Select(r => r.Tag).OfType<BarisTindakanRanap>().ToArray();

    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Tindakan gagal diproses. Periksa koneksi dan rekening jurnal."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }
}
