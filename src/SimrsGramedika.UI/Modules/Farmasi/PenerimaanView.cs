using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Farmasi;

/// <summary>Legacy DlgPemesanan "Penerimaan Obat &amp; BHP": faktur suplier masuk ke gudang.</summary>
public class PenerimaanView : PageView
{
    private readonly DatabaseSettings _settings = DatabaseSettings.Load();
    private readonly PenerimaanRepository _repo;
    private readonly FarmasiStokRepository _stok;
    private readonly TextBox _noFaktur = Ui.Text("No.Faktur dari suplier", 20), _noOrder = Ui.Text("No.SP (opsional)", 20);
    private readonly LookupBox _suplier = new(), _petugas = new(), _lokasi = new();
    private readonly DateTimeBox _tglTerima = new(DateTime.Now, false), _tglFaktur = new(DateTime.Now, false), _tglTempo = new(DateTime.Today.AddDays(30), false);
    private readonly TextBox _ppn = Ui.Number(11), _meterai = Ui.Number(0);
    private readonly TextBox _cari = Ui.Text("Cari kode / nama barang...");
    private readonly DataGrid _hasil = Ui.Grid(new Col("kode", "Kode"), new Col("nama", "Nama Barang"), new Col("kecil", "Satuan"),
        new Col("besar", "Sat.Besar"), new Col("isi", "Isi"), new Col("hbeli", "H.Beli Terakhir"));
    private readonly DataGrid _lines = new()
    {
        AutoGenerateColumns = false, IsReadOnly = false, GridLinesVisibility = DataGridGridLinesVisibility.All, FontSize = 12,
        HeadersVisibility = DataGridHeadersVisibility.Column, CanUserSortColumns = false,
    };
    private readonly ObservableCollection<PenerimaanLine> _items = [];
    private readonly TextBlock _total = new() { FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _setInfo = Ui.Hint("");
    private readonly DataGrid _daftar = Ui.Grid(new Col("no_faktur", "No.Faktur"), new Col("no_order", "No.Order"), new Col("tgl_pesan", "Tgl.Terima"),
        new Col("tgl_tempo", "Jatuh Tempo"), new Col("nama_suplier", "Suplier"), new Col("nm_bangsal", "Lokasi"), new Col("petugas", "Petugas"),
        new Col("total2", "Total"), new Col("ppn", "PPN"), new Col("meterai", "Meterai"), new Col("tagihan", "Tagihan"), new Col("status", "Status"));
    private readonly TextBox _cariDaftar = Ui.Text("Cari...");
    private readonly PeriodPicker _period = new(false, DateTime.Today.AddDays(1 - DateTime.Today.Day));

    public PenerimaanView()
    {
        _repo = new PenerimaanRepository(_settings);
        _stok = new FarmasiStokRepository(_settings);
        _lines.ItemsSource = _items;
        BuildColumns();
        _lines.CellEditEnded += (_, _) => UpdateTotal();
        _ppn.TextChanged += (_, _) => UpdateTotal();
        _meterai.TextChanged += (_, _) => UpdateTotal();
        _hasil.DoubleTapped += (_, _) => AddSelected();
        _hasil.Height = 180;
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await CariAsync(); };
        _cariDaftar.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await DaftarAsync(); };

        var left = Ui.Form(("No.Faktur :", _noFaktur), ("No.Order/SP :", _noOrder), ("Suplier :", _suplier), ("Petugas :", _petugas), ("Lokasi Terima :", _lokasi));
        var right = Ui.Form(("Tgl.Terima :", _tglTerima), ("Tgl.Faktur :", _tglFaktur), ("Jatuh Tempo :", _tglTempo), ("PPN (%) :", _ppn), ("Meterai :", _meterai));
        var header = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("*,24,*"), MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Left };
        header.Children.Add(left);
        Avalonia.Controls.Grid.SetColumn(right, 2);
        header.Children.Add(right);

        var top = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                header,
                Ui.Bar(_cari, Ui.Btn("Cari Barang", async (_, _) => await CariAsync()), Ui.Btn("Tambah", (_, _) => AddSelected())),
                _setInfo,
                _hasil,
                Ui.Bar(Ui.Btn("Hapus Baris", (_, _) => { if (_lines.SelectedItem is PenerimaanLine l) { _items.Remove(l); UpdateTotal(); } }),
                    Ui.Btn("Kosongkan", (_, _) => { _items.Clear(); UpdateTotal(); }),
                    Ui.Btn("Simpan Penerimaan", async (_, _) => await SaveAsync()), _total),
            },
        };
        var input = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        input.Children.Add(top);
        input.Children.Add(_lines);

        var daftar = new DockPanel();
        var bar = Ui.Bar(_cariDaftar, _period, Ui.Btn("Tampilkan", async (_, _) => await DaftarAsync()));
        DockPanel.SetDock(bar, Dock.Top);
        daftar.Children.Add(bar);
        daftar.Children.Add(_daftar);

        Build("Penerimaan Obat & BHP", new TabControl
        {
            Items = { new TabItem { Header = "Input", Content = input }, new TabItem { Header = "Daftar", Content = daftar } },
        }, "Faktur suplier: stok masuk ke lokasi terima, tercatat di riwayat & data batch, dan dijurnal Persediaan / Hutang Usaha. " +
           "Harga = per satuan beli. Centang \"Ubah Harga\" untuk menghitung ulang harga jual barang dari pengaturan harga obat.");
    }

    private void BuildColumns()
    {
        DataGridTextColumn Ro(string h, string p) => new() { Header = h, Binding = new Binding(p), IsReadOnly = true };
        DataGridTextColumn Ed(string h, string p, double w) => new() { Header = h, Binding = new Binding(p) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(w) };
        DataGridTextColumn Money(string h, string p) => new() { Header = h, Binding = new Binding(p) { StringFormat = "N0" }, IsReadOnly = true };
        _lines.Columns.Add(Ro("Kode", nameof(PenerimaanLine.Kode)));
        _lines.Columns.Add(Ro("Nama Barang", nameof(PenerimaanLine.Nama)));
        _lines.Columns.Add(Ed("Jumlah", nameof(PenerimaanLine.Jumlah), 80));
        _lines.Columns.Add(new DataGridCheckBoxColumn { Header = "Sat.Besar", Binding = new Binding(nameof(PenerimaanLine.PakaiSatuanBesar)) { Mode = BindingMode.TwoWay } });
        _lines.Columns.Add(Ro("Satuan Beli", nameof(PenerimaanLine.SatuanBeli)));
        _lines.Columns.Add(Ro("Jml Kecil", nameof(PenerimaanLine.JumlahKecil)));
        _lines.Columns.Add(Ed("Harga/Sat.Beli", nameof(PenerimaanLine.Harga), 110));
        _lines.Columns.Add(Ed("Disk %", nameof(PenerimaanLine.DiskonPersen), 70));
        _lines.Columns.Add(Money("Subtotal", nameof(PenerimaanLine.Subtotal)));
        _lines.Columns.Add(Money("Diskon", nameof(PenerimaanLine.BesarDiskon)));
        _lines.Columns.Add(Money("Total", nameof(PenerimaanLine.Total)));
        _lines.Columns.Add(Ed("No.Batch", nameof(PenerimaanLine.NoBatch), 110));
        _lines.Columns.Add(Ed("Kadaluarsa (dd/MM/yyyy)", nameof(PenerimaanLine.Kadaluarsa), 150));
        _lines.Columns.Add(new DataGridCheckBoxColumn { Header = "Ubah Harga", Binding = new Binding(nameof(PenerimaanLine.UbahHarga)) { Mode = BindingMode.TwoWay } });
    }

    public override async Task ReloadAsync()
    {
        await RunAsync(async () =>
        {
            _suplier.SetItems(await _repo.SuplierAsync());
            _petugas.SetItems(await _stok.PetugasAsync());
            _petugas.Kode = SimrsGramedika.Core.Auth.AppSession.CurrentUser?.UserId ?? "";
            _lokasi.SetItems(await _stok.LokasiAsync());
            var s = await _repo.SettingAsync();
            _setInfo.Text = $"Pengaturan harga obat: {s.Mode}, dasar {(s.DasarDiskon ? "harga diskon" : "harga beli")}, PPN {(s.PakaiPpn ? "masuk harga" : "tidak masuk harga")}" +
                            (_settings.AktifkanBatchObat ? " · Batch obat aktif: No.Batch & kadaluarsa wajib." : "");
        });
        await DaftarAsync();
    }

    private async Task CariAsync() => await RunAsync(async () =>
    {
        var list = await _repo.CariBarangAsync(_cari.Text);
        Ui.FillRows(_hasil, list.Select(l => new GridRow([l.Kode, l.Nama, l.NamaSatKecil, l.NamaSatBesar, l.Isi.ToString("0.##"), Ui.Money(l.Harga)], l)));
        Ui.Info(StatusText, $"{list.Count} barang");
    });

    private void AddSelected()
    {
        if ((_hasil.SelectedItem as GridRow)?.Tag is not PenerimaanLine l) return;
        // A fresh line object each time (the same barang may arrive with two batches).
        var n = new PenerimaanLine
        {
            Kode = l.Kode, Nama = l.Nama, KdJns = l.KdJns, SatKecil = l.SatKecil, NamaSatKecil = l.NamaSatKecil, SatBesar = l.SatBesar,
            NamaSatBesar = l.NamaSatBesar, Isi = l.Isi, HargaLama = l.HargaLama, Harga = l.Harga,
        };
        _items.Add(n);
        _lines.SelectedItem = n;
        UpdateTotal();
    }

    private (double Ppn, double Meterai) Pajak()
    {
        try { return (Ui.ParseNumber(_ppn.Text), Ui.ParseNumber(_meterai.Text)); } catch { return (0, 0); }
    }

    private void UpdateTotal()
    {
        var (ppn, meterai) = Pajak();
        var t = PenerimaanRepository.Totals(_items, ppn, meterai);
        _total.Text = $"Subtotal {Ui.Money(t.Subtotal)} · Diskon {Ui.Money(t.Diskon)} · Total {Ui.Money(t.Total)} · PPN {Ui.Money(t.Ppn)} · Tagihan {Ui.Money(t.Tagihan)}";
    }

    private async Task SaveAsync()
    {
        if (_items.Count == 0) { Ui.Err(StatusText, "Belum ada barang."); return; }
        var (ppn, meterai) = Pajak();
        var t = PenerimaanRepository.Totals(_items, ppn, meterai);
        if (!await Dialogs.ConfirmAsync(this, "Simpan Penerimaan", $"Simpan faktur {_noFaktur.Text} ({_items.Count} baris), tagihan {Ui.Money(t.Tagihan)}?")) return;
        await RunAsync(async () =>
        {
            var user = SimrsGramedika.Core.Auth.AppSession.CurrentUser;
            await _repo.SimpanAsync(new PenerimaanHeader((_noFaktur.Text ?? "").Trim(), _noOrder.Text ?? "", _suplier.Kode, _petugas.Kode,
                    _tglTerima.Value, _tglFaktur.Value, _tglTempo.Value, _lokasi.Kode, _lokasi.Nama, ppn, meterai),
                _items.ToList(), user?.HasPermission("obat") == true, user?.KodeLegacy ?? "");
            var no = _noFaktur.Text;
            _items.Clear();
            _noFaktur.Text = _noOrder.Text = "";
            UpdateTotal();
            await DaftarAsync();
            Ui.Ok(StatusText, $"Penerimaan {no} tersimpan.");
        });
    }

    private async Task DaftarAsync() => await RunAsync(async () =>
        Ui.Fill(_daftar, await _repo.DaftarAsync(_period.From, _period.To, _cariDaftar.Text)));
}
