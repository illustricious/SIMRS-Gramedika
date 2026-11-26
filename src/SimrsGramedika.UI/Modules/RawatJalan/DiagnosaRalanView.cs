using Avalonia.Controls;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatJalan;

internal sealed class DiagnosaRalanView : UserControl
{
    private readonly DiagnosaRalanService _service;
    private readonly ComboBox _jenis = new() { ItemsSource = new[] { "Diagnosa ICD-10", "Prosedur ICD-9" }, SelectedIndex = 0 };
    private readonly TextBox _cari = new() { PlaceholderText = "Cari kode atau nama", Width = 240 };
    private readonly TextBox _prioritas = new() { Text = "1", Width = 60 };
    private readonly TextBox _jumlah = new() { Text = "1", Width = 60 };
    private readonly DataGrid _hasil = Ui.Grid(new("kode", "Kode", 100), new("nama", "Nama", 400));
    private readonly DataGrid _tersimpan = Ui.Grid(new("kode", "Kode", 100), new("nama", "Nama", 300), new("prioritas", "Prioritas", 80), new("keterangan", "Status / jumlah", 120));
    private readonly TextBlock _status = Ui.Status();
    private readonly Grid _layout;
    private bool _busy;
    private bool Prosedur => _jenis.SelectedIndex == 1;

    public DiagnosaRalanView(string noRawat, AuthenticatedUser user)
    {
        _service = new(DatabaseSettings.Load(), noRawat, user);
        _jenis.SelectionChanged += async (_, _) => await JalankanAsync(MuatAsync);
        var bar = Ui.Bar(_jenis, _cari, Ui.Btn("Cari", async (_, _) => await JalankanAsync(CariAsync)),
            new TextBlock { Text = "Prioritas" }, _prioritas, new TextBlock { Text = "Jumlah prosedur" }, _jumlah,
            Ui.Btn("Tambah", async (_, _) => await JalankanAsync(TambahAsync)),
            Ui.Btn("Hapus terpilih", async (_, _) => await JalankanAsync(HapusAsync)));
        _layout = new Grid { RowDefinitions = new("Auto,Auto,*,Auto,*,Auto"), RowSpacing = 8, Margin = new Avalonia.Thickness(12) };
        Control[] controls = [Ui.Title("Diagnosa dan Prosedur"), bar, _hasil, Ui.Section("Tercatat pada kunjungan"), _tersimpan, _status];
        for (var i = 0; i < controls.Length; i++) { Grid.SetRow(controls[i], i); _layout.Children.Add(controls[i]); }
        Content = _layout;
        Loaded += async (_, _) => await JalankanAsync(MuatAsync);
    }
    private async Task CariAsync() => Ui.FillRows(_hasil, (await _service.CariAsync(Prosedur, _cari.Text ?? "")).Select(r => new GridRow([r.S("kode"), r.S("nama")], r)));
    private async Task MuatAsync()
    {
        _hasil.ItemsSource = null;
        Ui.FillRows(_tersimpan, (await _service.DaftarAsync(Prosedur)).Select(r => new GridRow([r.S("kode"), r.S("nama"), r.S("prioritas"), r.S("keterangan")], r)));
    }
    private async Task TambahAsync()
    {
        var row = Ui.Selected<Row>(_hasil) ?? throw new InvalidOperationException("Pilih kode dari hasil pencarian.");
        if (!int.TryParse(_prioritas.Text, out var prioritas)) throw new ArgumentException("Isi prioritas berupa angka.");
        await _service.TambahAsync(Prosedur, row.S("kode"), prioritas, _jumlah.Text ?? "");
        await MuatAsync(); Ui.Ok(_status, "Data tersimpan.");
    }
    private async Task HapusAsync()
    {
        var row = Ui.Selected<Row>(_tersimpan) ?? throw new InvalidOperationException("Pilih data yang akan dihapus.");
        if (!int.TryParse(row.S("prioritas"), out var prioritas)) throw new InvalidOperationException("Prioritas data tidak valid.");
        if (!await Dialogs.ConfirmAsync(this, "Hapus data", $"Hapus {row.S("kode")} dari kunjungan ini?")) return;
        await _service.HapusAsync(Prosedur, row.S("kode"), prioritas);
        await MuatAsync(); Ui.Ok(_status, "Data dihapus.");
    }
    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy || _layout is null) return;
        _busy = true; _layout.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Data gagal diproses. Periksa koneksi atau kemungkinan kode duplikat."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }
}
