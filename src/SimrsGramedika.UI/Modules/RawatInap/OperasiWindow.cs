using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatInap;

internal sealed class OperasiWindow : Window
{
    private static readonly PegawaiPilihan Kosong = new("-", "(tidak ada)");

    private readonly OperasiService _service;
    private readonly CalendarDatePicker _tglMulai = new() { SelectedDate = DateTime.Today };
    private readonly TextBox _jamMulai = new() { PlaceholderText = "HH:mm:ss", Width = 95 };
    private readonly CalendarDatePicker _tglSelesai = new() { SelectedDate = DateTime.Today };
    private readonly TextBox _jamSelesai = new() { PlaceholderText = "HH:mm:ss", Width = 95 };
    private readonly TextBox _anestesi = new() { PlaceholderText = "Jenis anestesi", MaxLength = 8, Width = 130 };
    private readonly ComboBox _kategori = new() { ItemsSource = OperasiService.Kategori, SelectedIndex = 0, MinWidth = 170 };
    private readonly TextBox _cariPaket = new() { PlaceholderText = "Cari paket operasi", Width = 250 };
    private readonly DataGrid _paket = Ui.Grid(new("kode", "Kode", 110), new("nama", "Paket", 340),
        new("kategori", "Kategori", 110), new("tarif", "Total tarif", 130));
    private readonly Dictionary<string, ComboBox> _tim = [];
    private readonly TextBox _cariObat = new() { PlaceholderText = "Cari obat/BHP OK", Width = 220 };
    private readonly TextBox _jumlah = new() { Text = "1", Width = 70 };
    private readonly DataGrid _obat = Ui.Grid(new("kode", "Kode", 110), new("nama", "Obat/BHP", 330), new("satuan", "Satuan", 90), new("harga", "Harga", 110));
    private readonly DataGrid _dipakai = Ui.Grid(new("nama", "Dipakai", 330), new("jumlah", "Jumlah", 90), new("total", "Total", 130));
    private readonly List<PakaiObatOperasi> _pakai = [];
    private readonly TextBox _preop = new() { MaxLength = 100 };
    private readonly TextBox _postop = new() { MaxLength = 100 };
    private readonly TextBox _jaringan = new() { MaxLength = 100 };
    private readonly TextBox _implan = new() { MaxLength = 50 };
    private readonly CheckBox _pa = new() { Content = "Jaringan dikirim ke Patologi Anatomi" };
    private readonly TextBox _isiLaporan = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 160 };
    private readonly TextBlock _modeLaporan = Ui.Hint("");
    private readonly Button _simpanLaporan;
    private readonly Button _batalLaporan;
    private readonly DataGrid _riwayat = Ui.Grid(new("waktu", "Waktu", 150), new("paket", "Paket", 330), new("status", "Rawat", 70),
        new("tindakan", "Biaya operasi", 130), new("obat", "Obat/BHP", 110));
    private readonly Button _simpan;
    private readonly TabControl _tabs;
    private readonly TextBlock _status = Ui.Status();
    private readonly Grid _layout;
    private CatatanOperasi? _laporanUntuk;
    private bool _busy;

    public OperasiWindow(RawatInapListItem visit, AuthenticatedUser user)
    {
        _service = new(DatabaseSettings.Load(), visit.NoRawat, user);
        Title = "Operasi / VK";
        Width = 960;
        Height = 740;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _paket.SelectionMode = DataGridSelectionMode.Extended;

        var identitas = Ui.Hint($"{visit.NamaPasien} · No. RM {visit.NoRekamMedis} · No. Rawat {visit.NoRawat} · Kamar {visit.KodeKamar}");
        var waktu = Ui.Bar(new TextBlock { Text = "Mulai" }, _tglMulai, _jamMulai, new TextBlock { Text = "Selesai" }, _tglSelesai, _jamSelesai,
            _anestesi, _kategori);

        var tabPaket = Susun("Auto,*", Ui.Bar(_cariPaket, Ui.Btn("Cari", async (_, _) => await JalankanAsync(CariPaketAsync)),
            Ui.Hint("Ctrl+klik untuk memilih beberapa paket.")), _paket);

        var tim = OperasiService.Komponen.Where(k => k.Peran is not null).Select(k =>
        {
            var pilih = new ComboBox { DisplayMemberBinding = new Binding(nameof(PegawaiPilihan.Nama)), MinWidth = 320 };
            _tim[k.Peran!] = pilih;
            return (k.Label, (Control)pilih);
        }).ToArray();
        var tabTim = new ScrollViewer { Content = Ui.Form(tim) };

        var tabObat = Susun("Auto,*,Auto,150,Auto",
            Ui.Bar(_cariObat, Ui.Btn("Cari", async (_, _) => await JalankanAsync(CariObatAsync)), new TextBlock { Text = "Jumlah" }, _jumlah,
                Ui.Btn("Tambahkan", async (_, _) => await JalankanAsync(TambahObatAsync))),
            _obat, Ui.Section("Obat/BHP yang dipakai"), _dipakai,
            Ui.Bar(Ui.Btn("Buang Terpilih", async (_, _) => await JalankanAsync(BuangObatAsync))));

        _simpanLaporan = Ui.Btn("Simpan Laporan Operasi Terpilih", async (_, _) => await JalankanAsync(SimpanLaporanAsync));
        _batalLaporan = Ui.Btn("Tutup Laporan", (_, _) => KosongkanLaporan());
        _simpanLaporan.IsVisible = _batalLaporan.IsVisible = _modeLaporan.IsVisible = false;
        var tabLaporan = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    _modeLaporan,
                    Ui.Form(("Diagnosis pre-operasi", _preop), ("Diagnosis post-operasi", _postop), ("Jaringan dieksisi", _jaringan),
                        ("Nomor implan", _implan), ("", _pa), ("Laporan", _isiLaporan)),
                    Ui.Hint("Laporan ikut tersimpan bersama operasi bila kolom Laporan diisi. Waktu selesai memakai isian di atas."),
                    Ui.Bar(_simpanLaporan, _batalLaporan),
                },
            },
        };

        var tabRiwayat = Susun("*,Auto", _riwayat, Ui.Bar(
            Ui.Btn("Buka Laporan", async (_, _) => await JalankanAsync(BukaLaporanAsync)),
            Ui.Btn("Hapus Operasi Terpilih", async (_, _) => await JalankanAsync(HapusAsync)),
            Ui.Btn("Muat Ulang", async (_, _) => await JalankanAsync(MuatRiwayatAsync))));

        _tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "Paket", Content = tabPaket }, new TabItem { Header = "Tim", Content = tabTim },
                new TabItem { Header = "Obat & BHP", Content = tabObat }, new TabItem { Header = "Laporan", Content = tabLaporan },
                new TabItem { Header = "Riwayat", Content = tabRiwayat },
            },
        };
        _simpan = Ui.Btn("Simpan Operasi", async (_, _) => await JalankanAsync(SimpanAsync));
        _layout = Susun("Auto,Auto,Auto,*,Auto,Auto", Ui.Title(Title), identitas, waktu, _tabs,
            Ui.Bar(_simpan, Ui.Btn("Tutup", (_, _) => Close())), _status);
        _layout.Margin = new Thickness(14);
        Content = _layout;
        Closing += (_, args) => args.Cancel = _busy;
        Loaded += async (_, _) => await JalankanAsync(async () =>
        {
            var (dokter, petugas) = await _service.PelaksanaAsync();
            foreach (var komponen in OperasiService.Komponen.Where(k => k.Peran is not null))
            {
                _tim[komponen.Peran!].ItemsSource = (komponen.Dokter ? dokter : petugas).Prepend(Kosong).ToList();
                _tim[komponen.Peran!].SelectedIndex = 0;
            }
            await MuatRiwayatAsync();
            await CariPaketAsync();
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

    private async Task CariPaketAsync()
    {
        _paket.ItemsSource = null;
        var rows = await _service.CariPaketAsync(_cariPaket.Text ?? "");
        Ui.FillRows(_paket, rows.Select(p => new GridRow([p.Kode, p.Nama, p.Kategori, $"{p.Total:N2}"], p)));
    }

    private async Task CariObatAsync()
    {
        var rows = await _service.CariObatAsync(_cariObat.Text ?? "");
        Ui.FillRows(_obat, rows.Select(o => new GridRow([o.Kode, o.Nama, o.Satuan, $"{o.Harga:N2}"], o)));
    }

    private Task TambahObatAsync()
    {
        var obat = Ui.Selected<ObatOperasi>(_obat) ?? throw new ArgumentException("Pilih obat di hasil pencarian.");
        if (!double.TryParse(_jumlah.Text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var jumlah) || jumlah <= 0)
            throw new ArgumentException("Jumlah obat harus angka lebih dari nol.");
        _pakai.RemoveAll(p => p.Obat.Kode == obat.Kode);
        _pakai.Add(new(obat, jumlah));
        TampilkanPakai();
        return Task.CompletedTask;
    }

    private Task BuangObatAsync()
    {
        var pakai = Ui.Selected<PakaiObatOperasi>(_dipakai) ?? throw new ArgumentException("Pilih obat yang akan dibuang dari daftar.");
        _pakai.Remove(pakai);
        TampilkanPakai();
        return Task.CompletedTask;
    }

    private void TampilkanPakai() => Ui.FillRows(_dipakai,
        _pakai.Select(p => new GridRow([p.Obat.Nama, $"{p.Jumlah:0.###}", $"{p.Obat.Harga * p.Jumlah:N2}"], p)));

    private async Task MuatRiwayatAsync()
    {
        var rows = await _service.DaftarAsync();
        Ui.FillRows(_riwayat, rows.Select(c => new GridRow(
            [$"{c.Waktu:yyyy-MM-dd HH:mm:ss}", c.Paket, c.Status, $"{c.BiayaTindakan:N2}", $"{c.BiayaObat:N2}"], c)));
    }

    private async Task SimpanAsync()
    {
        var mulai = Waktu(_tglMulai, _jamMulai, "mulai");
        var selesai = Waktu(_tglSelesai, _jamSelesai, "selesai");
        var paket = _paket.SelectedItems.OfType<GridRow>().Select(r => r.Tag).OfType<PaketOperasi>().ToArray();
        if (paket.Length == 0) throw new ArgumentException("Pilih paket operasi di tab Paket.");
        var tim = _tim.ToDictionary(t => t.Key, t => (t.Value.SelectedItem as PegawaiPilihan)?.Nik ?? "-");
        var obat = _pakai.Sum(p => p.Obat.Harga * p.Jumlah);
        var kosong = OperasiService.Komponen.Select((k, i) => (k, i))
            .Where(x => x.k.Peran is not null && tim[x.k.Peran] == "-" && paket.Any(p => p.Biaya[x.i] > 0)).Select(x => x.k.Label).ToArray();
        var peringatan = kosong.Length == 0 ? "" : $" Peran bertarif yang belum diisi: {string.Join(", ", kosong)}.";
        if (!await Dialogs.ConfirmAsync(this, "Simpan operasi",
                $"Catat {paket.Length} paket (Rp{paket.Sum(p => p.Total):N2}) dan obat/BHP Rp{obat:N2}?{peringatan}")) return;
        var laporan = _laporanUntuk is null && !string.IsNullOrWhiteSpace(_isiLaporan.Text) ? Laporan(selesai) : null;
        await _service.SimpanAsync(new(mulai, selesai, _anestesi.Text ?? "", _kategori.SelectedItem as string ?? "-", tim, paket,
            _pakai.ToArray(), laporan));
        _paket.SelectedItems.Clear();
        _pakai.Clear();
        TampilkanPakai();
        KosongkanLaporan();
        await MuatRiwayatAsync();
        _tabs.SelectedIndex = 4;
        Ui.Ok(_status, "Operasi dan jurnal tersimpan.");
    }

    private async Task BukaLaporanAsync()
    {
        var catatan = Ui.Selected<CatatanOperasi>(_riwayat) ?? throw new ArgumentException("Pilih operasi di riwayat.");
        var laporan = await _service.MuatLaporanAsync(catatan.Waktu);
        KosongkanLaporan();
        _laporanUntuk = catatan;
        _preop.Text = laporan?.DiagnosaPreOp;
        _postop.Text = laporan?.DiagnosaPostOp;
        _jaringan.Text = laporan?.Jaringan;
        _implan.Text = laporan?.NomorImplan;
        _pa.IsChecked = laporan?.PermintaanPa == true;
        _isiLaporan.Text = laporan?.Isi;
        var selesai = laporan?.Selesai ?? catatan.Waktu;
        _tglSelesai.SelectedDate = selesai.Date;
        _jamSelesai.Text = selesai.ToString("HH:mm:ss");
        _modeLaporan.Text = $"Laporan operasi {catatan.Waktu:yyyy-MM-dd HH:mm:ss}. Waktu selesai ada pada isian Selesai di atas.";
        _modeLaporan.IsVisible = _simpanLaporan.IsVisible = _batalLaporan.IsVisible = true;
        _simpan.IsEnabled = false;
        _tabs.SelectedIndex = 3;
    }

    private async Task SimpanLaporanAsync()
    {
        var catatan = _laporanUntuk!;
        await _service.SimpanLaporanAsync(catatan.Waktu, Laporan(Waktu(_tglSelesai, _jamSelesai, "selesai")));
        KosongkanLaporan();
        _tabs.SelectedIndex = 4;
        Ui.Ok(_status, "Laporan operasi tersimpan.");
    }

    private LaporanOperasi Laporan(DateTime selesai) => new(_preop.Text ?? "", _postop.Text ?? "", _jaringan.Text ?? "", selesai,
        _pa.IsChecked == true, _implan.Text ?? "", _isiLaporan.Text ?? "");

    private void KosongkanLaporan()
    {
        _laporanUntuk = null;
        _preop.Text = _postop.Text = _jaringan.Text = _implan.Text = _isiLaporan.Text = "";
        _pa.IsChecked = false;
        _modeLaporan.IsVisible = _simpanLaporan.IsVisible = _batalLaporan.IsVisible = false;
        _tglSelesai.SelectedDate = _tglMulai.SelectedDate;
        _jamSelesai.Text = "";
        _simpan.IsEnabled = true;
    }

    private async Task HapusAsync()
    {
        var catatan = Ui.Selected<CatatanOperasi>(_riwayat) ?? throw new ArgumentException("Pilih operasi di riwayat.");
        if (!await Dialogs.ConfirmAsync(this, "Hapus operasi",
                $"Hapus operasi {catatan.Waktu:yyyy-MM-dd HH:mm:ss} ({catatan.Paket}) beserta obat dan laporannya? Jurnal pembalik akan dicatat.")) return;
        await _service.HapusAsync(catatan);
        if (_laporanUntuk?.Waktu == catatan.Waktu) KosongkanLaporan();
        await MuatRiwayatAsync();
        Ui.Ok(_status, "Operasi dihapus dan jurnal pembalik tercatat.");
    }

    private static DateTime Waktu(CalendarDatePicker tanggal, TextBox jam, string nama)
    {
        if (tanggal.SelectedDate is not { } hari || !TimeOnly.TryParseExact(jam.Text, "HH:mm:ss", out var waktu))
            throw new ArgumentException($"Isi tanggal dan jam {nama} dalam format JJ:mm:dd.");
        return DateOnly.FromDateTime(hari).ToDateTime(waktu);
    }

    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Operasi gagal diproses. Periksa koneksi dan rekening jurnal."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }
}
