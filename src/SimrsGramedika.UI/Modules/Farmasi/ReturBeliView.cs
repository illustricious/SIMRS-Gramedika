using System.Collections.ObjectModel;
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

/// <summary>Return purchased medicine and supplies to the supplier from an existing invoice.</summary>
public sealed class ReturBeliView : PageView
{
    private readonly ReturBeliRepository _repo = new(DatabaseSettings.Load());
    private readonly TextBox _noRetur = Ui.Text("Nomor retur", 20), _cariFaktur = Ui.Text("No. faktur / suplier...");
    private readonly LookupBox _petugas = new();
    private readonly DateTimeBox _tanggal = new(DateTime.Today, false);
    private readonly DataGrid _faktur = Ui.Grid(new Col("no_faktur", "Faktur Asal"), new Col("tgl_beli", "Tanggal Beli"),
        new Col("nama_suplier", "Suplier"), new Col("nm_bangsal", "Lokasi"), new Col("petugas", "Petugas"));
    private readonly TextBlock _asal = new() { FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly DataGrid _items = new()
    {
        AutoGenerateColumns = false, IsReadOnly = false, GridLinesVisibility = DataGridGridLinesVisibility.All,
        HeadersVisibility = DataGridHeadersVisibility.Column, CanUserSortColumns = false, FontSize = 12,
    };
    private readonly ObservableCollection<ReturBeliLine> _lines = [];
    private readonly TextBox _cariDaftar = Ui.Text("Cari no retur / suplier / lokasi...");
    private readonly PeriodPicker _period = new(false, DateTime.Today.AddDays(1 - DateTime.Today.Day));
    private readonly DataGrid _daftar = Ui.Grid(new Col("no_retur_beli", "No.Retur"), new Col("tgl_retur", "Tanggal"),
        new Col("nama_suplier", "Suplier"), new Col("nm_bangsal", "Lokasi"), new Col("petugas", "Petugas"),
        new Col("jumlah_item", "Baris"), new Col("total", "Total"));
    private readonly TextBlock _total = new() { FontWeight = Avalonia.Media.FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
    private Row? _selectedInvoice;

    public ReturBeliView()
    {
        _items.ItemsSource = _lines;
        BuildColumns();
        _items.CellEditEnded += (_, _) => HitungTotal();
        _items.CurrentCellChanged += (_, _) => HitungTotal();
        _faktur.DoubleTapped += async (_, _) => await PilihFakturAsync();
        _cariFaktur.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await CariFakturAsync(); };
        _cariDaftar.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await DaftarAsync(); };

        var noRetur = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children =
            { _noRetur, Ui.Btn("Nomor otomatis", async (_, _) => await NomorOtomatisAsync()) } };
        var header = Ui.Form(("No.Retur :", noRetur), ("Petugas :", _petugas), ("Tanggal :", _tanggal));
        var pilihFaktur = new DockPanel();
        var cariBar = Ui.Bar(_cariFaktur, Ui.Btn("Cari Faktur Pembelian", async (_, _) => await CariFakturAsync()));
        DockPanel.SetDock(cariBar, Dock.Top);
        pilihFaktur.Children.Add(cariBar);
        pilihFaktur.Children.Add(_faktur);
        _faktur.Height = 150;

        var form = new DockPanel();
        var atas = new StackPanel { Spacing = 7, Children =
        {
            header,
            pilihFaktur,
            _asal,
            Ui.Hint("Pilih satu faktur asal. Jumlah diisi dalam satuan terkecil; retur dibatasi oleh jumlah yang pernah dibeli dan stok yang masih tersedia."),
            Ui.Bar(Ui.Btn("Simpan Retur", async (_, _) => await SimpanAsync(), AppSession.CurrentUser?.HasPermission(ReturBeliRepository.Permission) == true), _total),
        }};
        DockPanel.SetDock(atas, Dock.Top);
        form.Children.Add(atas);
        form.Children.Add(_items);

        var list = new DockPanel();
        var daftarBar = Ui.Bar(_cariDaftar, _period, Ui.Btn("Tampilkan", async (_, _) => await DaftarAsync()),
            Ui.Btn("Batalkan Retur Terpilih", async (_, _) => await HapusTerpilihAsync(),
                AppSession.CurrentUser?.HasPermission(ReturBeliRepository.Permission) == true));
        DockPanel.SetDock(daftarBar, Dock.Top);
        list.Children.Add(daftarBar);
        list.Children.Add(_daftar);
        Build("Retur Pembelian Farmasi", new TabControl
        {
            Items = { new TabItem { Header = "Input Retur", Content = form }, new TabItem { Header = "Daftar Retur", Content = list } },
        }, "Retur mengacu faktur pembelian asal, mengurangi stok lokasi/batch, mencatat riwayat dan jurnal retur supplier.");
    }

    private void BuildColumns()
    {
        DataGridTextColumn Read(string title, string path, string? format = null) => new()
        {
            Header = title, Binding = new Binding(path) { StringFormat = format }, IsReadOnly = true,
        };
        _items.Columns.Add(Read("Kode", nameof(ReturBeliLine.Kode)));
        _items.Columns.Add(Read("Nama Barang", nameof(ReturBeliLine.Nama)));
        _items.Columns.Add(Read("Batch", nameof(ReturBeliLine.NoBatch)));
        _items.Columns.Add(Read("Kadaluarsa", nameof(ReturBeliLine.Kadaluarsa), "dd/MM/yyyy"));
        _items.Columns.Add(Read("Satuan", nameof(ReturBeliLine.Satuan)));
        _items.Columns.Add(Read("Dibeli", nameof(ReturBeliLine.JumlahBeli), "N2"));
        _items.Columns.Add(Read("Sudah Retur", nameof(ReturBeliLine.JumlahSudahDiretur), "N2"));
        _items.Columns.Add(Read("Sisa Retur", nameof(ReturBeliLine.SisaRetur), "N2"));
        _items.Columns.Add(Read("Stok", nameof(ReturBeliLine.Stok), "N2"));
        _items.Columns.Add(new DataGridTextColumn
        {
            Header = "Jumlah Retur", Binding = new Binding(nameof(ReturBeliLine.JumlahRetur)) { Mode = BindingMode.TwoWay, StringFormat = "N2" },
            Width = new DataGridLength(110),
        });
        _items.Columns.Add(Read("Harga", nameof(ReturBeliLine.HargaKecil), "N0"));
        _items.Columns.Add(Read("Subtotal", nameof(ReturBeliLine.Subtotal), "N0"));
    }

    public override async Task ReloadAsync()
    {
        await RunAsync(async () =>
        {
            var petugas = await _repo.PetugasAsync();
            _petugas.SetItems(petugas);
            _petugas.Kode = AppSession.CurrentUser?.KodeLegacy ?? "";
            _noRetur.Text = await _repo.NomorBerikutnyaAsync(_tanggal.Value);
        });
        await DaftarAsync();
    }

    private async Task NomorOtomatisAsync() => await RunAsync(async () =>
        _noRetur.Text = await _repo.NomorBerikutnyaAsync(_tanggal.Value));

    private async Task CariFakturAsync() => await RunAsync(async () =>
    {
        var rows = await _repo.CariFakturAsync(_cariFaktur.Text);
        Ui.FillRows(_faktur, rows.Select(r => new GridRow([r.S("no_faktur"), r.S("tgl_beli"), r.S("nama_suplier"), r.S("nm_bangsal"), r.S("petugas")], r)));
        Ui.Info(StatusText, $"{rows.Count} faktur ditemukan. Klik dua kali untuk memuat rincian.");
    });

    private async Task PilihFakturAsync()
    {
        if (Ui.Selected<Row>(_faktur) is not { } invoice) return;
        await RunAsync(async () =>
        {
            var items = await _repo.BarangTersediaAsync(invoice.S("no_faktur"));
            _selectedInvoice = invoice;
            _lines.Clear();
            foreach (var line in items)
            {
                line.PropertyChanged += (_, _) => HitungTotal();
                _lines.Add(line);
            }
            _asal.Text = $"Faktur {invoice.S("no_faktur")} • {invoice.S("nama_suplier")} • {invoice.S("nm_bangsal")}";
            HitungTotal();
            Ui.Info(StatusText, $"{items.Count} barang masih dapat diretur; baris yang stoknya habis tidak ditampilkan.");
        });
    }

    private void HitungTotal()
    {
        var total = _lines.Sum(l => l.Subtotal);
        _total.Text = $"Total retur: {Ui.Money(total)}";
    }

    private async Task SimpanAsync()
    {
        if (_selectedInvoice is null) { Ui.Err(StatusText, "Pilih faktur pembelian asal terlebih dahulu."); return; }
        if (AppSession.CurrentUser?.HasPermission(ReturBeliRepository.Permission) != true)
        { Ui.Err(StatusText, "Akun ini tidak punya hak retur ke supplier."); return; }
        await RunAsync(async () =>
        {
            var header = new ReturBeliHeader(_noRetur.Text ?? "", _petugas.Kode, _tanggal.Value.Date);
            await _repo.SimpanAsync(header, _lines.ToList(), AppSession.CurrentUser?.KodeLegacy ?? "");
            _lines.Clear();
            _selectedInvoice = null;
            _asal.Text = "";
            _noRetur.Text = await _repo.NomorBerikutnyaAsync(_tanggal.Value);
            await DaftarAsync();
        }, "Retur berhasil disimpan; stok dan jurnal sudah dicatat.");
    }

    private async Task DaftarAsync() => await RunAsync(async () =>
    {
        var rows = await _repo.DaftarAsync(_period.From, _period.To, _cariDaftar.Text);
        Ui.FillRows(_daftar, rows.Select(r => new GridRow([r.S("no_retur_beli"), r.S("tgl_retur"), r.S("nama_suplier"),
            r.S("nm_bangsal"), r.S("petugas"), r.S("jumlah_item"), Ui.Money(r.D("total"))], r)));
    });

    private async Task HapusTerpilihAsync()
    {
        if (AppSession.CurrentUser?.HasPermission(ReturBeliRepository.Permission) != true)
        { Ui.Err(StatusText, "Akun ini tidak punya hak retur ke supplier."); return; }
        if (Ui.Selected<Row>(_daftar) is not { } selected)
        { Ui.Err(StatusText, "Pilih dokumen retur dari daftar."); return; }
        var no = selected.S("no_retur_beli");
        if (!await Dialogs.ConfirmAsync(this, "Batalkan Retur Supplier",
                $"Batalkan retur {no}? Stok akan dikembalikan dan jurnal asal dibalik.")) return;
        await RunAsync(async () =>
        {
            await _repo.HapusAsync(no, AppSession.CurrentUser?.KodeLegacy ?? "");
            await DaftarAsync();
        }, "Retur dibatalkan; stok dikembalikan dan jurnal pembalik dicatat.");
    }
}
