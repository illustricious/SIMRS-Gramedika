using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Barcode;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Barcode;

/// <summary>
/// Legacy viabarcode/DlgBarcodeRalan &amp; DlgBarcodeRanap: enter/scan the No.Rawat, then scan
/// tindakan and obat/BHP codes (Enter on an exact code adds it straight to the list, a
/// repeated scan adds +1), adjust quantities and save everything in one go.
/// </summary>
public class BarcodeView : PageView
{
    private sealed class ObatCart(ObatItem obat)
    {
        public ObatItem Obat { get; } = obat;
        public double Jumlah { get; set; } = 1;
        public bool Kapasitas { get; set; }
        public double Embalase { get; set; }
        public double Tuslah { get; set; }
        public double Jml => Kapasitas ? Jumlah / Obat.Kapasitas : Jumlah;
        public double Total => Embalase + Tuslah + Obat.Harga * Jml;
    }

    private readonly bool _ranap;
    private readonly BarcodeRepository _repo = new(DatabaseSettings.Load());
    private BarcodeContext? _ctx;

    private readonly TextBox _noRawat = new() { PlaceholderText = "Scan / ketik No.Rawat lalu Enter", Width = 260 };
    private readonly TextBlock _pasien = new() { FontWeight = FontWeight.Medium, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _cariTindakan = new() { PlaceholderText = "Scan kode / cari tindakan + Enter" };
    private readonly TextBox _cariObat = new() { PlaceholderText = "Scan kode / cari obat-BHP + Enter" };
    private readonly DataGrid _hasilTindakan, _hasilObat, _cartTindakan, _cartObat;
    private readonly List<TindakanItem> _tindakan = [];
    private readonly List<ObatCart> _obat = [];
    private readonly TextBox _jumlah = Ui.Number(1), _emb = Ui.Number(), _tus = Ui.Number();
    private readonly CheckBox _kapasitas = new() { Content = "Jumlah dlm satuan kapasitas" };
    private readonly TextBlock _total = new() { FontWeight = FontWeight.Bold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    private bool _syncing;

    public BarcodeView(bool ranap)
    {
        _ranap = ranap;
        _hasilTindakan = Ui.Grid(new Col("", "Kode", 110), new Col("", "Nama Tindakan"), new Col("", "Kategori"), new Col("", "Tarif"));
        _cartTindakan = Ui.Grid(new Col("", "Kode", 110), new Col("", "Tindakan Dipilih"), new Col("", "Tarif"));
        _hasilObat = Ui.Grid(new Col("", "Kode", 110), new Col("", "Nama Obat/BHP"), new Col("", "Sat"), new Col("", "Harga"), new Col("", "Stok"), new Col("", "Batch"));
        _cartObat = Ui.Grid(new Col("", "Kode", 110), new Col("", "Obat/BHP Dipilih"), new Col("", "Jml"), new Col("", "Harga"), new Col("", "Total"));

        _noRawat.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await LoadPasienAsync(); };
        _cariTindakan.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await CariTindakanAsync(); };
        _cariObat.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await CariObatAsync(); };
        _hasilTindakan.DoubleTapped += (_, _) => { if (Ui.Selected<TindakanItem>(_hasilTindakan) is { } t) AddTindakan(t); };
        _hasilObat.DoubleTapped += (_, _) => { if (Ui.Selected<ObatItem>(_hasilObat) is { } o) AddObat(o); };
        _cartObat.SelectionChanged += (_, _) => SyncEditor();
        foreach (var tb in new[] { _jumlah, _emb, _tus }) tb.TextChanged += (_, _) => ApplyEditor();
        _kapasitas.IsCheckedChanged += (_, _) => ApplyEditor();

        Control Section(string title, TextBox cari, DataGrid hasil, Button add, DataGrid cart, Control extra)
        {
            var g = new Avalonia.Controls.Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,*,Auto") };
            void Put(Control c, int r) { Avalonia.Controls.Grid.SetRow(c, r); g.Children.Add(c); }
            Put(new TextBlock { Text = title, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 4) }, 0);
            var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(add, Dock.Right);
            bar.Children.Add(add);
            bar.Children.Add(cari);
            Put(bar, 1);
            Put(hasil, 2);
            Put(new TextBlock { Text = "Dipilih:", Margin = new Thickness(0, 6, 0, 2) }, 3);
            Put(cart, 4);
            Put(extra, 5);
            return g;
        }

        var tindakan = Section("Tindakan", _cariTindakan, _hasilTindakan,
            Ui.Btn("➕ Tambah", (_, _) => { if (Ui.Selected<TindakanItem>(_hasilTindakan) is { } t) AddTindakan(t); }), _cartTindakan,
            Ui.Bar(Ui.Btn("🗑 Hapus Tindakan", (_, _) => { if (Ui.Selected<TindakanItem>(_cartTindakan) is { } t) { _tindakan.Remove(t); Refresh(); } })));
        var obat = Section("Obat / BHP", _cariObat, _hasilObat,
            Ui.Btn("➕ Tambah", (_, _) => { if (Ui.Selected<ObatItem>(_hasilObat) is { } o) AddObat(o); }), _cartObat,
            Ui.Bar(Ui.L("Jumlah"), _jumlah, _kapasitas, Ui.L("Embalase"), _emb, Ui.L("Tuslah"), _tus,
                Ui.Btn("🗑 Hapus Obat", (_, _) => { if (_cartObat.SelectedItem is GridRow { Tag: ObatCart c }) { _obat.Remove(c); Refresh(); } })));

        var cols = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        Avalonia.Controls.Grid.SetColumn(obat, 2);
        cols.Children.Add(tindakan);
        cols.Children.Add(obat);

        var body = new DockPanel();
        var top = Ui.Bar(Ui.L("No.Rawat :"), _noRawat, Ui.Btn("🔎 Tampilkan", async (_, _) => await LoadPasienAsync()), _pasien);
        var bottom = Ui.Bar(Ui.Btn("💾 Simpan Semua", async (_, _) => await SaveAsync()), Ui.Btn("🧹 Bersihkan", (_, _) => Clear()), _total);
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        body.Children.Add(top);
        body.Children.Add(bottom);
        body.Children.Add(cols);

        Build(ranap ? "Tindakan & Obat Rawat Inap Via Barcode" : "Tindakan & Obat Rawat Jalan Via Barcode", body,
            "Scan kode lalu Enter: kode yang persis sama langsung masuk daftar (scan ulang = +1). Double-click hasil pencarian untuk menambah.");
    }

    public override Task ReloadAsync()
    {
        _noRawat.Focus();
        return Task.CompletedTask;
    }

    private async Task LoadPasienAsync()
    {
        var no = _noRawat.Text?.Trim() ?? "";
        if (no.Length == 0) return;
        await RunAsync(async () =>
        {
            _ctx = await _repo.LoadAsync(no, _ranap);
            _tindakan.Clear();
            _obat.Clear();
            Refresh();
            var unit = _ranap ? $"{_ctx.Unit} ({_ctx.Kelas})" : _ctx.Unit;
            _pasien.Text = $"{_ctx.NoRm} · {_ctx.NamaPasien} · {unit} · Dr. {_ctx.NamaDokter} · Depo: {(_ctx.LokasiStok.Length == 0 ? "-" : _ctx.LokasiStok)}";
            await CariTindakanAsync(silent: true);
            await CariObatAsync(silent: true);
            _cariTindakan.Focus();
        });
    }

    private async Task CariTindakanAsync(bool silent = false)
    {
        if (_ctx is null) { Ui.Err(StatusText, "Isi No.Rawat dulu."); return; }
        var q = _cariTindakan.Text?.Trim() ?? "";
        await RunAsync(async () =>
        {
            var list = await _repo.CariTindakanAsync(_ctx, q);
            var exact = list.FirstOrDefault(t => q.Length > 0 && string.Equals(t.Kode, q, StringComparison.OrdinalIgnoreCase));
            if (exact is not null && !silent) { AddTindakan(exact); _cariTindakan.Text = ""; return; }
            Ui.FillRows(_hasilTindakan, list.Select(t => new GridRow([t.Kode, t.Nama, t.Kategori, Ui.Money(t.Tarif)], t)));
        });
    }

    private async Task CariObatAsync(bool silent = false)
    {
        if (_ctx is null) { Ui.Err(StatusText, "Isi No.Rawat dulu."); return; }
        var q = _cariObat.Text?.Trim() ?? "";
        await RunAsync(async () =>
        {
            var list = await _repo.CariObatAsync(_ctx, q);
            var exact = list.Where(o => q.Length > 0 && string.Equals(o.Kode, q, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1 && !silent) { AddObat(exact[0]); _cariObat.Text = ""; return; }
            Ui.FillRows(_hasilObat, list.Select(o => new GridRow(
                [o.Kode, o.Nama, o.Satuan, Ui.Money(o.Harga), o.Stok.ToString("0.##"), o.NoBatch.Length == 0 ? "-" : $"{o.NoBatch}/{o.NoFaktur}"], o)));
        });
    }

    private void AddTindakan(TindakanItem t)
    {
        _tindakan.Add(t);
        Refresh();
        Ui.Ok(StatusText, $"+ {t.Nama}");
    }

    private void AddObat(ObatItem o)
    {
        var existing = _obat.FirstOrDefault(c => c.Obat.Kode == o.Kode && c.Obat.NoBatch == o.NoBatch && c.Obat.NoFaktur == o.NoFaktur);
        if (existing is not null) existing.Jumlah += 1;
        else _obat.Add(new ObatCart(o));
        Refresh();
        Ui.Ok(StatusText, $"+ {o.Nama}");
    }

    private void Refresh()
    {
        var selected = (_cartObat.SelectedItem as GridRow)?.Tag;
        Ui.FillRows(_cartTindakan, _tindakan.Select(t => new GridRow([t.Kode, t.Nama, Ui.Money(t.Tarif)], t)));
        var rows = _obat.Select(c => new GridRow([c.Obat.Kode, c.Obat.Nama, c.Jml.ToString("0.###") + (c.Kapasitas ? " (kap)" : ""),
            Ui.Money(c.Obat.Harga), Ui.Money(c.Total)], c)).ToList();
        Ui.FillRows(_cartObat, rows);
        _cartObat.SelectedItem = rows.FirstOrDefault(r => r.Tag == selected);
        _total.Text = $"Total tindakan Rp {Ui.Money(_tindakan.Sum(t => t.Tarif))} · obat/BHP Rp {Ui.Money(_obat.Sum(c => c.Total))}";
    }

    private void SyncEditor()
    {
        if (_cartObat.SelectedItem is not GridRow { Tag: ObatCart c }) return;
        _syncing = true;
        _jumlah.Text = c.Jumlah.ToString("0.###");
        _emb.Text = c.Embalase.ToString("0.##");
        _tus.Text = c.Tuslah.ToString("0.##");
        _kapasitas.IsChecked = c.Kapasitas;
        _syncing = false;
    }

    private void ApplyEditor()
    {
        if (_syncing || _cartObat.SelectedItem is not GridRow { Tag: ObatCart c }) return;
        try
        {
            c.Jumlah = Ui.ParseNumber(_jumlah.Text);
            c.Embalase = Ui.ParseNumber(_emb.Text);
            c.Tuslah = Ui.ParseNumber(_tus.Text);
            c.Kapasitas = _kapasitas.IsChecked == true;
            Refresh();
        }
        catch (FormatException) { /* still typing */ }
    }

    private void Clear()
    {
        _tindakan.Clear();
        _obat.Clear();
        Refresh();
        StatusText.Text = "";
    }

    private async Task SaveAsync()
    {
        if (_ctx is null) { Ui.Err(StatusText, "Isi No.Rawat dulu."); return; }
        if (!await Dialogs.ConfirmAsync(this, "Simpan", $"Simpan {_tindakan.Count} tindakan dan {_obat.Count} obat/BHP untuk {_ctx.NamaPasien}?")) return;
        await RunAsync(async () =>
        {
            await _repo.SaveAsync(_ctx, _tindakan.ToList(),
                _obat.Select(c => new ObatInput(c.Obat, c.Jumlah, c.Kapasitas, c.Embalase, c.Tuslah)).ToList(), AppSession.Kode);
            _tindakan.Clear();
            _obat.Clear();
            _ctx = null;
            _noRawat.Text = "";
            _pasien.Text = "";
            Refresh();
            Ui.FillRows(_hasilTindakan, []);
            Ui.FillRows(_hasilObat, []);
            _noRawat.Focus();
        }, "Proses selesai — tindakan, obat, stok & jurnal tersimpan.");
    }
}
