using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Farmasi;

/// <summary>
/// One screen for the four farmasi stock transactions (legacy DlgMutasiBarang, DlgInputStok,
/// DlgPengeluaranApotek, DlgPermintaan): header, search stock, line list, save — plus a "Daftar" tab.
/// </summary>
public class StokTransaksiView : PageView
{
    private readonly StokMode _mode;
    private readonly DatabaseSettings _settings = DatabaseSettings.Load();
    private readonly FarmasiStokRepository _repo;
    private readonly MasterRepository _lookup;
    private readonly LookupBox _lokasi = new(), _tujuan = new(), _petugas = new();
    private readonly DateTimeBox _tanggal;
    private readonly TextBox _nomor = new() { IsReadOnly = true };
    private readonly TextBox _ket = Ui.Text(maxLen: 60);
    private readonly TextBox _cari = Ui.Text("Cari kode / nama barang / batch...");
    private readonly DataGrid _hasil = Ui.Grid(new Col("kode", "Kode"), new Col("nama", "Nama Barang"), new Col("satuan", "Satuan"),
        new Col("batch", "No.Batch"), new Col("faktur", "No.Faktur"), new Col("ed", "Kadaluarsa"), new Col("stok", "Stok"), new Col("harga", "HPP"));
    private readonly DataGrid _lines = new()
    {
        AutoGenerateColumns = false, IsReadOnly = false, GridLinesVisibility = DataGridGridLinesVisibility.All, FontSize = 12,
        HeadersVisibility = DataGridHeadersVisibility.Column, CanUserSortColumns = false,
    };
    private readonly ObservableCollection<StokLine> _items = [];
    private readonly TextBlock _total = new() { FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
    private readonly DataGrid _daftar = new() { AutoGenerateColumns = false, IsReadOnly = true, FontSize = 12, GridLinesVisibility = DataGridGridLinesVisibility.All };
    private readonly TextBox _cariDaftar = Ui.Text("Cari...");
    private readonly PeriodPicker _period = new(false, DateTime.Today.AddDays(1 - DateTime.Today.Day));
    private string? _noPermintaan;

    private bool UsesTujuan => _mode is StokMode.Mutasi or StokMode.Permintaan;
    private bool UsesNomor => _mode is StokMode.StokKeluar or StokMode.Permintaan;
    private string Petugas => SimrsGramedika.Core.Auth.AppSession.CurrentUser?.UserId ?? "";
    /// <summary>Legacy akses.getkode() — what riwayat_barang_medis.petugas records.</summary>
    private string KodeLegacy => SimrsGramedika.Core.Auth.AppSession.CurrentUser?.KodeLegacy ?? "";

    private static string TitleOf(StokMode m) => m switch
    {
        StokMode.Mutasi => "Mutasi Obat & BHP",
        StokMode.Opname => "Stok Opname Obat & BHP",
        StokMode.StokKeluar => "Stok Keluar Medis",
        _ => "Permintaan Obat & BHP",
    };

    public StokTransaksiView(StokMode mode)
    {
        _mode = mode;
        _repo = new FarmasiStokRepository(_settings);
        _lookup = new MasterRepository(_settings);
        _tanggal = new DateTimeBox(DateTime.Now, withTime: mode == StokMode.Mutasi);
        _lines.ItemsSource = _items;
        BuildLineColumns();
        _lines.CellEditEnded += (_, _) => UpdateTotal();
        _hasil.DoubleTapped += (_, _) => AddSelected();
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await CariAsync(); };
        _cariDaftar.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await DaftarAsync(); };
        _hasil.Height = 220;

        var header = new List<(string, Control)>();
        if (UsesNomor) header.Add(("No :", _nomor));
        header.Add(("Tanggal :", _tanggal));
        header.Add((mode switch { StokMode.Mutasi => "Dari Lokasi :", StokMode.Permintaan => "Lokasi Peminta :", _ => "Lokasi :" }, _lokasi));
        if (UsesTujuan) header.Add((mode == StokMode.Mutasi ? "Ke Lokasi :" : "Minta Ke (Gudang) :", _tujuan));
        if (UsesNomor) header.Add(("Petugas :", _petugas));
        if (mode != StokMode.Permintaan) header.Add(("Keterangan :", _ket));

        var actions = new List<Control>
        {
            Ui.Btn("Hapus Baris", (_, _) => { if (_lines.SelectedItem is StokLine l) { _items.Remove(l); UpdateTotal(); } }),
            Ui.Btn("Kosongkan", (_, _) => Clear()),
        };
        if (mode is StokMode.Mutasi or StokMode.StokKeluar) actions.Add(Ui.Btn("Ambil dari Permintaan", async (_, _) => await AmbilPermintaanAsync()));
        actions.Add(Ui.Btn("Simpan", async (_, _) => await SaveAsync()));
        actions.Add(_total);

        var input = new DockPanel();
        var top = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new Border { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.Form(header.ToArray()) },
                Ui.Bar(_cari, Ui.Btn("Cari Barang", async (_, _) => await CariAsync()), Ui.Btn("Tambah", (_, _) => AddSelected())),
                Ui.Hint(mode == StokMode.Opname
                    ? "Klik dua kali barang untuk menambah; isi kolom Real dengan hasil hitung fisik."
                    : "Klik dua kali barang untuk menambah, lalu isi kolom Jumlah."),
                _hasil,
                Ui.Bar(actions.ToArray()),
            },
        };
        DockPanel.SetDock(top, Dock.Top);
        input.Children.Add(top);
        input.Children.Add(_lines);

        var daftar = new DockPanel();
        var bar = Ui.Bar(_cariDaftar, _period, Ui.Btn("Tampilkan", async (_, _) => await DaftarAsync()));
        DockPanel.SetDock(bar, Dock.Top);
        daftar.Children.Add(bar);
        daftar.Children.Add(_daftar);

        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Input", Content = input },
                new TabItem { Header = "Daftar", Content = daftar },
            },
        };
        Build(TitleOf(mode), tabs, mode switch
        {
            StokMode.Mutasi => "Memindah stok antar lokasi/depo; stok asal berkurang, tujuan bertambah, keduanya tercatat di riwayat barang.",
            StokMode.Opname => "Stok di lokasi diganti dengan hasil hitung fisik; selisih hilang/lebih dicatat di tabel opname.",
            StokMode.StokKeluar => "Barang keluar non-resep (rusak, pemakaian unit, dll); stok berkurang dan dijurnal ke akun Stok Keluar Medis.",
            _ => "Permintaan barang dari depo/unit ke gudang; dipenuhi lewat Mutasi atau Stok Keluar (\"Ambil dari Permintaan\").",
        } + (_settings.AktifkanBatchObat ? " Mode batch obat aktif." : ""));
    }

    private void BuildLineColumns()
    {
        DataGridTextColumn Ro(string header, string path) => new() { Header = header, Binding = new Binding(path), IsReadOnly = true };
        _lines.Columns.Add(Ro("Kode", "Item.Kode"));
        _lines.Columns.Add(Ro("Nama Barang", "Item.Nama"));
        _lines.Columns.Add(Ro("Satuan", "Item.Satuan"));
        if (_mode != StokMode.Permintaan)
        {
            _lines.Columns.Add(Ro("No.Batch", "Item.NoBatch"));
            _lines.Columns.Add(Ro("No.Faktur", "Item.NoFaktur"));
        }
        _lines.Columns.Add(Ro(_mode == StokMode.Opname ? "Stok Sistem" : "Stok", "Item.Stok"));
        _lines.Columns.Add(new DataGridTextColumn
        {
            Header = _mode == StokMode.Opname ? "Real (hitung fisik)" : "Jumlah",
            Binding = new Binding(nameof(StokLine.Jumlah)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(130),
        });
        if (_mode == StokMode.Permintaan)
            _lines.Columns.Add(new DataGridTextColumn { Header = "Keterangan", Binding = new Binding(nameof(StokLine.Keterangan)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(220) });
        else
            _lines.Columns.Add(Ro("HPP", "Item.Harga"));
    }

    public override async Task ReloadAsync()
    {
        await RunAsync(async () =>
        {
            var lokasi = await _repo.LokasiAsync();
            _lokasi.SetItems(lokasi);
            _tujuan.SetItems(lokasi);
            if (_mode == StokMode.StokKeluar) _petugas.SetItems(await _repo.PetugasAsync());
            else if (_mode == StokMode.Permintaan) _petugas.SetItems(await _lookup.LookupAsync(MasterLookups.PegawaiNik));
            _petugas.Kode = Petugas;
            await RenumberAsync();
        });
        await DaftarAsync();
    }

    private async Task RenumberAsync()
    {
        if (!UsesNomor) return;
        try { _nomor.Text = await _repo.NextNomorAsync(_mode, _tanggal.Value) + " (otomatis)"; } catch { _nomor.Text = "(otomatis)"; }
    }

    private async Task CariAsync() => await RunAsync(async () =>
    {
        var lokasi = _mode == StokMode.Permintaan ? _tujuan.Kode : _lokasi.Kode;
        if (_mode != StokMode.Permintaan && lokasi.Length == 0) throw new ArgumentException("Pilih lokasi dulu.");
        var list = _mode == StokMode.Permintaan
            ? await _repo.CariBarangAsync(lokasi, _cari.Text)
            : await _repo.CariStokAsync(lokasi, _cari.Text, includeZero: _mode == StokMode.Opname);
        Ui.FillRows(_hasil, list.Select(i => new GridRow(
            [i.Kode, i.Nama, i.Satuan, i.NoBatch, i.NoFaktur, i.Kadaluarsa?.ToString("dd/MM/yyyy") ?? "", Num(i.Stok), Ui.Money(i.Harga)], i)));
        Ui.Info(StatusText, list.Count == 500 ? "500 barang pertama — persempit pencarian." : $"{list.Count} barang");
    });

    private static string Num(double v) => v.ToString("0.##", Ui.Id);

    private void AddSelected()
    {
        if ((_hasil.SelectedItem as GridRow)?.Tag is not StokItem it) return;
        if (_items.Any(l => l.Item.Kode == it.Kode && l.Item.NoBatch == it.NoBatch && l.Item.NoFaktur == it.NoFaktur))
        {
            Ui.Info(StatusText, $"{it.Nama} sudah ada di daftar.");
            return;
        }
        _items.Add(new StokLine { Item = it, Jumlah = _mode == StokMode.Opname ? it.Stok : 1 });
        _lines.SelectedItem = _items[^1];
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        if (_mode == StokMode.Opname)
        {
            var hilang = _items.Sum(l => Math.Max(l.Item.Stok - l.Jumlah, 0) * l.Item.Harga);
            var lebih = _items.Sum(l => Math.Max(l.Jumlah - l.Item.Stok, 0) * l.Item.Harga);
            _total.Text = $"{_items.Count} barang · Nilai hilang {Ui.Money(hilang)} · lebih {Ui.Money(lebih)}";
        }
        else
            _total.Text = $"{_items.Count} barang · Total {Ui.Money(_items.Sum(l => l.Jumlah * l.Item.Harga))}";
    }

    private void Clear()
    {
        _items.Clear();
        _noPermintaan = null;
        UpdateTotal();
    }

    private async Task AmbilPermintaanAsync()
    {
        if (_lokasi.Kode.Length == 0) { Ui.Err(StatusText, "Pilih lokasi (gudang yang memenuhi permintaan) dulu."); return; }
        List<Row> list = [];
        await RunAsync(async () => list = await _repo.PermintaanBaruAsync(_lokasi.Kode));
        if (list.Count == 0) { Ui.Info(StatusText, $"Tidak ada permintaan baru ke {_lokasi.Nama}."); return; }

        var grid = Ui.Grid(new Col("no_permintaan", "No.Permintaan"), new Col("tanggal", "Tanggal"), new Col("peminta", "Peminta"),
            new Col("petugas", "Petugas"), new Col("item", "Item"));
        Ui.Fill(grid, list);
        Row? chosen = null;
        var win = new Window { Title = "Pilih Permintaan", Width = 720, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var ok = Ui.Btn("Ambil", (_, _) => { chosen = Ui.Selected<Row>(grid); win.Close(); });
        grid.DoubleTapped += (_, _) => { chosen = Ui.Selected<Row>(grid); win.Close(); };
        var root = new DockPanel { Margin = new Thickness(12) };
        var bottom = Ui.Bar(ok, Ui.Btn("Batal", (_, _) => win.Close()));
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        win.Content = root;
        if (TopLevel.GetTopLevel(this) is Window owner) await win.ShowDialog(owner);
        if (chosen is null) return;

        await RunAsync(async () =>
        {
            var (lines, kurang) = await _repo.AmbilPermintaanAsync(chosen.S("no_permintaan"), _lokasi.Kode);
            _items.Clear();
            foreach (var l in lines) _items.Add(l);
            if (_mode == StokMode.Mutasi) _tujuan.Kode = chosen.S("kd_bangsal");
            _ket.Text = $"Permintaan {chosen.S("no_permintaan")}";
            _noPermintaan = chosen.S("no_permintaan");
            UpdateTotal();
            if (kurang.Count > 0) Ui.Err(StatusText, "Stok tidak cukup: " + string.Join("; ", kurang));
            else Ui.Ok(StatusText, $"{lines.Count} baris diambil dari {chosen.S("no_permintaan")} (batch kadaluarsa terdekat lebih dulu).");
        });
    }

    private async Task SaveAsync()
    {
        if (_items.Count == 0) { Ui.Err(StatusText, "Belum ada barang yang dimasukkan."); return; }
        if (!await Dialogs.ConfirmAsync(this, "Simpan", $"Simpan {TitleOf(_mode).ToLowerInvariant()} ({_items.Count} barang)?")) return;
        await RunAsync(async () =>
        {
            var lines = _items.ToList();
            var ket = (_ket.Text ?? "").Trim();
            string msg;
            switch (_mode)
            {
                case StokMode.Mutasi:
                    await _repo.SimpanMutasiAsync(_lokasi.Kode, _lokasi.Nama, _tujuan.Kode, _tujuan.Nama, _tanggal.Value, ket, lines, KodeLegacy, _noPermintaan);
                    msg = "Mutasi tersimpan.";
                    break;
                case StokMode.Opname:
                    await _repo.SimpanOpnameAsync(_lokasi.Kode, _tanggal.Value, ket, lines, KodeLegacy);
                    msg = "Stok opname tersimpan.";
                    break;
                case StokMode.StokKeluar:
                    msg = $"Stok keluar {await _repo.SimpanStokKeluarAsync(_lokasi.Kode, _tanggal.Value, ket, lines, _petugas.Kode, _noPermintaan)} tersimpan.";
                    break;
                default:
                    msg = $"Permintaan {await _repo.SimpanPermintaanAsync(_lokasi.Kode, _tujuan.Kode, _tanggal.Value, lines, _petugas.Kode)} tersimpan.";
                    break;
            }
            Clear();
            _hasil.ItemsSource = null;
            await RenumberAsync();
            await DaftarAsync();
            Ui.Ok(StatusText, msg);
        });
    }

    private static readonly Dictionary<StokMode, Col[]> DaftarCols = new()
    {
        [StokMode.Mutasi] = [new("tanggal", "Tanggal"), new("kode_brng", "Kode"), new("nama_brng", "Nama Barang"), new("jumlah", "Jumlah"), new("harga", "Harga"),
            new("total", "Total"), new("dari", "Dari"), new("ke", "Ke"), new("no_batch", "No.Batch"), new("keterangan", "Keterangan")],
        [StokMode.Opname] = [new("tanggal", "Tanggal"), new("nm_bangsal", "Lokasi"), new("kode_brng", "Kode"), new("nama_brng", "Nama Barang"), new("stok", "Stok"),
            new("real", "Real"), new("selisih", "Hilang"), new("nomihilang", "Nilai Hilang"), new("lebih", "Lebih"), new("nomilebih", "Nilai Lebih"),
            new("no_batch", "No.Batch"), new("keterangan", "Keterangan")],
        [StokMode.StokKeluar] = [new("no_keluar", "No.Keluar"), new("tanggal", "Tanggal"), new("nm_bangsal", "Lokasi"), new("petugas", "Petugas"),
            new("kode_brng", "Kode"), new("nama_brng", "Nama Barang"), new("jumlah", "Jumlah"), new("harga_beli", "Harga"), new("total", "Total"),
            new("no_batch", "No.Batch"), new("keterangan", "Keterangan")],
        [StokMode.Permintaan] = [new("no_permintaan", "No.Permintaan"), new("tanggal", "Tanggal"), new("peminta", "Peminta"), new("tujuan", "Tujuan"),
            new("petugas", "Petugas"), new("status", "Status"), new("kode_brng", "Kode"), new("nama_brng", "Nama Barang"), new("jumlah", "Jumlah"),
            new("keterangan", "Keterangan")],
    };

    private async Task DaftarAsync() => await RunAsync(async () =>
    {
        if (_daftar.Tag is null) Ui.SetColumns(_daftar, DaftarCols[_mode]);
        var rows = await _repo.DaftarAsync(_mode, _period.From, _period.To, _cariDaftar.Text);
        Ui.Fill(_daftar, rows);
    });
}
