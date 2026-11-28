using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Farmasi;

/// <summary>Legacy inventory/DlgPembelian: paid supplier purchase with stock and journal posting.</summary>
public sealed class PembelianView : PageView
{
    private readonly DatabaseSettings _settings = DatabaseSettings.Load();
    private readonly PembelianRepository _repo;
    private readonly TextBox _noFaktur = Ui.Text("Nomor faktur", 20), _ppn = Ui.Number(11), _cari = Ui.Text("Cari kode / nama barang...");
    private readonly LookupBox _suplier = new(), _petugas = new(), _lokasi = new();
    private readonly DateTimeBox _tanggal = new(DateTime.Today, false);
    private readonly ComboBox _akun = new() { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly DataGrid _hasil = Ui.Grid(new Col("kode", "Kode"), new Col("nama", "Nama Barang"), new Col("satuan", "Satuan"), new Col("besar", "Satuan Besar"), new Col("isi", "Isi"), new Col("harga", "H.Beli"));
    private readonly DataGrid _items = new()
    {
        AutoGenerateColumns = false, IsReadOnly = false, GridLinesVisibility = DataGridGridLinesVisibility.All,
        HeadersVisibility = DataGridHeadersVisibility.Column, CanUserSortColumns = false, FontSize = 12,
    };
    private readonly ObservableCollection<PembelianLine> _lines = [];
    private readonly DataGrid _daftar = Ui.Grid(new Col("no_faktur", "No.Faktur"), new Col("tgl_beli", "Tanggal"), new Col("nama_suplier", "Suplier"),
        new Col("nm_bangsal", "Lokasi"), new Col("petugas", "Petugas"), new Col("total1", "Subtotal"), new Col("potongan", "Diskon"),
        new Col("total2", "Total"), new Col("ppn", "PPN"), new Col("tagihan", "Tagihan"), new Col("nama_bayar", "Akun Bayar"));
    private readonly TextBox _cariDaftar = Ui.Text("Cari faktur / suplier / lokasi...");
    private readonly PeriodPicker _period = new(false, DateTime.Today.AddDays(1 - DateTime.Today.Day));
    private readonly TextBlock _total = new() { FontWeight = Avalonia.Media.FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private List<AkunPembelian> _akunRows = [];

    public PembelianView()
    {
        _repo = new PembelianRepository(_settings);
        _items.ItemsSource = _lines;
        BuildColumns();
        _items.CellEditEnded += (_, _) => HitungTotal();
        _items.CurrentCellChanged += (_, _) => HitungTotal();
        _ppn.TextChanged += (_, _) => HitungTotal();
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await CariAsync(); };
        _cariDaftar.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await DaftarAsync(); };
        _hasil.DoubleTapped += (_, _) => TambahTerpilih();
        _hasil.Height = 170;

        var nomor = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children =
            { _noFaktur, Ui.Btn("Nomor otomatis", async (_, _) => await NomorOtomatisAsync()) } };
        var kiri = Ui.Form(("No.Faktur :", nomor), ("Suplier :", _suplier), ("Petugas :", _petugas), ("Lokasi :", _lokasi));
        var kanan = Ui.Form(("Tanggal :", _tanggal), ("Akun Bayar :", _akun), ("PPN (%) :", _ppn));
        var kepala = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("*,24,*"), MaxWidth = 1150, HorizontalAlignment = HorizontalAlignment.Left };
        kepala.Children.Add(kiri);
        Avalonia.Controls.Grid.SetColumn(kanan, 2);
        kepala.Children.Add(kanan);

        var inputAtas = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                kepala,
                Ui.Bar(_cari, Ui.Btn("Cari Barang", async (_, _) => await CariAsync()), Ui.Btn("Tambah", (_, _) => TambahTerpilih())),
                Ui.Hint(_settings.AktifkanBatchObat ? "Batch aktif: nomor batch dan tanggal kadaluarsa wajib." : "Pembelian langsung: stok masuk ke lokasi dan dibayar lewat akun terpilih."),
                _hasil,
                Ui.Bar(Ui.Btn("Hapus Baris", (_, _) => { if (_items.SelectedItem is PembelianLine l) _lines.Remove(l); HitungTotal(); }),
                    Ui.Btn("Kosongkan", (_, _) => { _lines.Clear(); HitungTotal(); }), Ui.Btn("Simpan Pembelian", async (_, _) => await SimpanAsync()), _total),
            },
        };
        var input = new DockPanel();
        DockPanel.SetDock(inputAtas, Dock.Top);
        input.Children.Add(inputAtas);
        input.Children.Add(_items);

        var daftar = new DockPanel();
        var bar = Ui.Bar(_cariDaftar, _period, Ui.Btn("Tampilkan", async (_, _) => await DaftarAsync()));
        DockPanel.SetDock(bar, Dock.Top);
        daftar.Children.Add(bar);
        daftar.Children.Add(_daftar);
        Build("Pembelian Obat & BHP", new TabControl
        {
            Items = { new TabItem { Header = "Input", Content = input }, new TabItem { Header = "Daftar", Content = daftar } },
        }, "Pembelian langsung dari suplier: simpan faktur, tambah stok, catat batch bila aktif, dan jurnal Persediaan / PPN Masukan / Akun Bayar.");
    }

    private void BuildColumns()
    {
        DataGridTextColumn Read(string title, string path) => new() { Header = title, Binding = new Binding(path), IsReadOnly = true };
        DataGridTextColumn Edit(string title, string path, double width) => new() { Header = title, Binding = new Binding(path) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(width) };
        DataGridTextColumn Money(string title, string path) => new() { Header = title, Binding = new Binding(path) { StringFormat = "N0" }, IsReadOnly = true };
        _items.Columns.Add(Read("Kode", nameof(PembelianLine.Kode)));
        _items.Columns.Add(Read("Nama Barang", nameof(PembelianLine.Nama)));
        _items.Columns.Add(Edit("Jumlah", nameof(PembelianLine.Jumlah), 80));
        _items.Columns.Add(new DataGridCheckBoxColumn { Header = "Sat.Besar", Binding = new Binding(nameof(PembelianLine.PakaiSatuanBesar)) { Mode = BindingMode.TwoWay } });
        _items.Columns.Add(Read("Satuan", nameof(PembelianLine.SatuanBeli)));
        _items.Columns.Add(Read("Jumlah Kecil", nameof(PembelianLine.JumlahKecil)));
        _items.Columns.Add(Edit("Harga/Satuan", nameof(PembelianLine.Harga), 110));
        _items.Columns.Add(Edit("Diskon %", nameof(PembelianLine.DiskonPersen), 75));
        _items.Columns.Add(Money("Subtotal", nameof(PembelianLine.Subtotal)));
        _items.Columns.Add(Money("Diskon", nameof(PembelianLine.BesarDiskon)));
        _items.Columns.Add(Money("Total", nameof(PembelianLine.Total)));
        _items.Columns.Add(Edit("No.Batch", nameof(PembelianLine.NoBatch), 110));
        _items.Columns.Add(Edit("Kadaluarsa (dd/MM/yyyy)", nameof(PembelianLine.Kadaluarsa), 150));
        _items.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Ubah Harga", Binding = new Binding(nameof(PembelianLine.UbahHarga)) { Mode = BindingMode.TwoWay },
            IsReadOnly = AppSession.CurrentUser?.HasPermission("obat") != true,
        });
    }

    public override async Task ReloadAsync()
    {
        await RunAsync(async () =>
        {
            var refs = await _repo.ReferensiAsync();
            _suplier.SetItems(refs.Suplier);
            _petugas.SetItems(refs.Petugas);
            _lokasi.SetItems(refs.Lokasi);
            _akunRows = refs.Akun;
            _akun.ItemsSource = _akunRows.Select(x => x.Nama).ToList();
            var user = AppSession.CurrentUser;
            _petugas.Kode = user?.KodeLegacy ?? "";
            _ppn.Text = "11";
            _noFaktur.Text = await _repo.NomorBerikutnyaAsync(_tanggal.Value);
        });
        await DaftarAsync();
    }

    private async Task CariAsync() => await RunAsync(async () =>
    {
        var list = await _repo.CariBarangAsync(_cari.Text);
        Ui.FillRows(_hasil, list.Select(l => new GridRow([l.Kode, l.Nama, l.NamaSatKecil, l.NamaSatBesar, l.Isi.ToString("0.##"), Ui.Money(l.Harga)], l)));
        Ui.Info(StatusText, $"{list.Count} barang ditemukan");
    });

    private async Task NomorOtomatisAsync() => await RunAsync(async () =>
        _noFaktur.Text = await _repo.NomorBerikutnyaAsync(_tanggal.Value));

    private void TambahTerpilih()
    {
        if ((_hasil.SelectedItem as GridRow)?.Tag is not PembelianLine barang) return;
        var line = new PembelianLine
        {
            Kode = barang.Kode, Nama = barang.Nama, KdJns = barang.KdJns,
            SatKecil = barang.SatKecil, NamaSatKecil = barang.NamaSatKecil, SatBesar = barang.SatBesar, NamaSatBesar = barang.NamaSatBesar,
            Isi = barang.Isi, HargaLama = barang.HargaLama, Harga = barang.Harga,
        };
        line.PropertyChanged += (_, _) => HitungTotal();
        _lines.Add(line);
        _items.SelectedItem = line;
        HitungTotal();
    }

    private void HitungTotal()
    {
        double ppn;
        try { ppn = Ui.ParseNumber(_ppn.Text); } catch { ppn = 0; }
        var t = PembelianRepository.Totals(_lines, ppn);
        _total.Text = $"Subtotal {Ui.Money(t.Subtotal)}  •  Diskon {Ui.Money(t.Diskon)}  •  Total {Ui.Money(t.Total)}  •  PPN {Ui.Money(t.Ppn)}  •  Tagihan {Ui.Money(t.Tagihan)}";
    }

    private async Task SimpanAsync()
    {
        var active = _lines.Where(l => l.Jumlah > 0).ToList();
        if (active.Count == 0) { Ui.Err(StatusText, "Belum ada barang dengan jumlah positif."); return; }
        if (_akun.SelectedItem is not string namaAkun) { Ui.Err(StatusText, "Pilih akun pembayaran."); return; }
        double ppn;
        try { ppn = Ui.ParseNumber(_ppn.Text); }
        catch { Ui.Err(StatusText, "PPN tidak valid."); return; }
        var totals = PembelianRepository.Totals(active, ppn);
        if (!await Dialogs.ConfirmAsync(this, "Simpan Pembelian", $"Simpan faktur {_noFaktur.Text} ({active.Count} baris), tagihan {Ui.Money(totals.Tagihan)}?")) return;
        await RunAsync(async () =>
        {
            var user = AppSession.CurrentUser;
            var header = new PembelianHeader((_noFaktur.Text ?? "").Trim(), _suplier.Kode, _petugas.Kode, _tanggal.Value,
                _lokasi.Kode, _lokasi.Nama, namaAkun, ppn);
            await _repo.SimpanAsync(header, active, user?.HasPermission("obat") == true, user?.KodeLegacy ?? "");
            var no = header.NoFaktur;
            _lines.Clear();
            _noFaktur.Text = await _repo.NomorBerikutnyaAsync(_tanggal.Value);
            HitungTotal();
            await DaftarAsync();
            Ui.Ok(StatusText, $"Pembelian {no} tersimpan.");
        });
    }

    private async Task DaftarAsync() => await RunAsync(async () => Ui.Fill(_daftar,
        await _repo.DaftarAsync(_period.From, _period.To, _cariDaftar.Text)));
}
