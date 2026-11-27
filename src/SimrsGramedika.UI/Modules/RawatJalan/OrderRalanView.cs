using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.UI.Modules.Shared;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.UI.Modules.RawatJalan;

internal sealed class OrderRalanView : UserControl
{
    private readonly DatabaseSettings _settings = DatabaseSettings.Load();
    private readonly string _noRawat, _noRm;
    private readonly AuthenticatedUser _user;
    private readonly ComboBox _kategori = new() { ItemsSource = new[] { "Patologi Klinik", "Patologi Anatomi", "Mikrobiologi" }, SelectedIndex = 0 };
    private readonly TextBox _cari = new() { PlaceholderText = "Cari kode atau nama", Width = 235 };
    private readonly TextBox _jumlah = new() { PlaceholderText = "Jumlah", Width = 90 };
    private readonly TextBox _aturan = new() { PlaceholderText = "Aturan pakai", MaxLength = 150, MinWidth = 175 };
    private readonly TextBox _diagnosa = new() { PlaceholderText = "Diagnosis klinis", MaxLength = 80, MinWidth = 200 };
    private readonly TextBox _informasi = new() { PlaceholderText = "Informasi tambahan", MaxLength = 60, MinWidth = 190 };
    private readonly DataGrid _hasil = Ui.Grid(new("kode", "Kode", 100), new("nama", "Nama", 300));
    private readonly DataGrid _draf = Ui.Grid(new("kode", "Kode", 100), new("nama", "Nama", 240), new("detail", "Rincian", 220));
    private readonly DataGrid _riwayat = Ui.Grid(new("nomor", "Nomor", 145), new("tanggal", "Tanggal", 105),
        new("nama", "Item", 215), new("detail", "Rincian", 200), new("status", "Status", 130));
    private readonly TextBlock _status = Ui.Status();
    private readonly List<BarisOrderRalan> _baris = [];
    private readonly Grid _layout;
    private readonly JenisOrderRalan _jenis;
    private BahanPaRalan? _bahanPa;
    private bool _busy;
    private DrafOrderRalan? _diubah;
    private readonly Button _kirim, _batalUbah;

    private bool Resep => _jenis == JenisOrderRalan.Resep;
    private OrderRalanService Service => new(_settings, _noRawat, _noRm, _user,
        _jenis == JenisOrderRalan.LabPk ? _kategori.SelectedIndex switch
        {
            1 => JenisOrderRalan.LabPa,
            2 => JenisOrderRalan.LabMb,
            _ => JenisOrderRalan.LabPk
        } : _jenis);

    public OrderRalanView(string noRawat, string noRm, AuthenticatedUser user, JenisOrderRalan jenis)
    {
        _noRawat = noRawat;
        _noRm = noRm;
        _user = user;
        _jenis = jenis;
        _kategori.IsVisible = jenis == JenisOrderRalan.LabPk;
        _kategori.SelectionChanged += async (_, _) => await JalankanAsync(async () =>
        {
            SelesaiUbah();
            await MuatAsync();
        });
        var input = Resep ? Ui.Bar(_jumlah, _aturan) : Ui.Bar(_diagnosa, _informasi);
        if (!Resep && jenis == JenisOrderRalan.LabPk)
        {
            var bahan = Ui.Btn("Bahan PA", async (_, _) => await JalankanAsync(async () => { await IsiBahanPaAsync(); }));
            bahan.IsVisible = false;
            _kategori.SelectionChanged += (_, _) => bahan.IsVisible = _kategori.SelectedIndex == 1;
            input.Children.Add(bahan);
        }
        var toolbar = Ui.Bar(_kategori, _cari, Ui.Btn("Cari", async (_, _) => await JalankanAsync(CariAsync)),
            Ui.Btn("Tambah item", async (_, _) => await JalankanAsync(TambahAsync)));
        _kirim = Ui.Btn("Kirim", async (_, _) => await JalankanAsync(SimpanAsync));
        _batalUbah = Ui.Btn("Batal ubah", (_, _) => { SelesaiUbah(); Ui.Ok(_status, "Perubahan dibatalkan."); });
        _batalUbah.IsVisible = false;
        var actions = Ui.Bar(Ui.Btn("Lepas item", (_, _) =>
        {
            if (Ui.Selected<BarisOrderRalan>(_draf) is { } row) { _baris.Remove(row); IsiDraf(); }
        }), _kirim, _batalUbah);
        if (Resep)
            actions.Children.Insert(1, Ui.Btn("Ganti jumlah & aturan", async (_, _) => await JalankanAsync(GantiBarisAsync)));
        _layout = new Grid { Margin = new Thickness(10), RowSpacing = 6, RowDefinitions = new("Auto,Auto,Auto,*,Auto,*,Auto,*,Auto") };
        var judulRiwayat = Ui.Bar(Ui.Section("Riwayat kunjungan"),
            Ui.Btn("Ubah order terpilih", async (_, _) => await JalankanAsync(UbahTerpilihAsync)));
        Control[] controls = [Ui.Title(Resep ? "Input Resep" : jenis == JenisOrderRalan.Radiologi ? "Permintaan Radiologi" : "Permintaan Laboratorium"),
            toolbar, input, _hasil, actions, _draf, judulRiwayat, _riwayat, _status];
        for (var i = 0; i < controls.Length; i++) { Grid.SetRow(controls[i], i); _layout.Children.Add(controls[i]); }
        Content = _layout;
        Loaded += async (_, _) => await JalankanAsync(MuatAsync);
    }

    private async Task MuatAsync()
    {
        await CariAsync();
        await MuatRiwayatAsync();
    }

    private async Task CariAsync() => Ui.FillRows(_hasil,
        (await Service.CariAsync(_cari.Text ?? "")).Select(row => new GridRow([row.Kode, row.Nama], row)));

    private async Task MuatRiwayatAsync() => Ui.FillRows(_riwayat, (await Service.RiwayatAsync()).Select(row =>
        new GridRow([row.S("nomor"), row.S("tanggal"), row.S("nama"),
            Resep ? $"{row.S("jumlah")} · {row.S("aturan")}" : $"{row.S("diagnosa")} · {row.S("informasi")}", row.S("status")], row)));

    private void IsiDraf()
    {
        Ui.FillRows(_draf, _baris.Select(row => new GridRow([row.Kode, row.Nama,
            Resep ? $"{row.Jumlah} · {row.Aturan}" : row.Template is { Count: > 0 } selected ? $"{selected.Count} rincian" : ""], row)));
        _kategori.IsEnabled = _baris.Count == 0 && _diubah is null;
    }

    private async Task UbahTerpilihAsync()
    {
        var terpilih = Ui.Selected<Row>(_riwayat) ?? throw new ArgumentException("Pilih order pada riwayat kunjungan.");
        if (_diubah is null && _baris.Count > 0 &&
            !await Dialogs.ConfirmAsync(this, "Ubah order", "Draf yang belum dikirim akan dibuang. Lanjutkan?")) return;
        var draf = await Service.MuatOrderAsync(terpilih.S("nomor"));
        _diubah = draf;
        _baris.Clear();
        _baris.AddRange(draf.Baris);
        _bahanPa = draf.Bahan;
        _diagnosa.Text = draf.Diagnosa;
        _informasi.Text = draf.Informasi;
        _kirim.Content = "Simpan perubahan";
        _batalUbah.IsVisible = true;
        IsiDraf();
        Ui.Ok(_status, $"Mengubah order {draf.Nomor}.");
    }

    private void SelesaiUbah()
    {
        _diubah = null;
        _baris.Clear();
        _bahanPa = null;
        _diagnosa.Clear();
        _informasi.Clear();
        _kirim.Content = "Kirim";
        _batalUbah.IsVisible = false;
        IsiDraf();
    }

    private Task GantiBarisAsync()
    {
        var row = Ui.Selected<BarisOrderRalan>(_draf) ?? throw new ArgumentException("Pilih obat pada draf.");
        var (jumlah, aturan) = BacaJumlahAturan();
        _baris[_baris.IndexOf(row)] = row with { Jumlah = jumlah, Aturan = aturan };
        IsiDraf();
        _jumlah.Clear();
        _aturan.Clear();
        return Task.CompletedTask;
    }

    private (decimal Jumlah, string Aturan) BacaJumlahAturan()
    {
        if (!decimal.TryParse(_jumlah.Text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var jumlah)
            || jumlah <= 0 || string.IsNullOrWhiteSpace(_aturan.Text))
            throw new ArgumentException("Isi jumlah positif dan aturan pakai obat.");
        return (jumlah, _aturan.Text.Trim());
    }

    private async Task TambahAsync()
    {
        var row = Ui.Selected<BarisOrderRalan>(_hasil) ?? throw new ArgumentException("Pilih item dari hasil pencarian.");
        if (_baris.Any(r => r.Kode.Equals(row.Kode, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Item sudah ada dalam draf.");
        if (Resep)
        {
            var (jumlah, aturan) = BacaJumlahAturan();
            row = row with { Jumlah = jumlah, Aturan = aturan };
        }
        else
        {
            var templates = await Service.TemplateAsync(row.Kode);
            if (templates.Count > 0 && TopLevel.GetTopLevel(this) is Window owner)
            {
                var pilihan = new StackPanel { Spacing = 4 };
                foreach (var template in templates)
                    pilihan.Children.Add(new CheckBox { Content = template.S("pemeriksaan"), Tag = (int)template.D("id_template") });
                var panel = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
                var dialog = new Window { Title = row.Nama, Width = 495, Height = 440,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
                panel.Children.Add(Ui.Hint("Centang rincian pemeriksaan yang diminta."));
                panel.Children.Add(new ScrollViewer { Content = pilihan, MaxHeight = 290 });
                panel.Children.Add(Ui.Bar(Ui.Btn("Gunakan pilihan", (_, _) => dialog.Close(true)),
                    Ui.Btn("Batal", (_, _) => dialog.Close(false))));
                if (!await dialog.ShowDialog<bool>(owner)) return;
                row = row with { Template = pilihan.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (int)c.Tag!).ToArray() };
            }
        }
        _baris.Add(row);
        IsiDraf();
        _jumlah.Clear();
        _aturan.Clear();
    }

    private async Task SimpanAsync()
    {
        if (_baris.Count == 0) throw new ArgumentException("Tambahkan item sebelum mengirim.");
        var pa = _jenis == JenisOrderRalan.LabPk && _kategori.SelectedIndex == 1;
        if (pa && _bahanPa is null && !await IsiBahanPaAsync()) return;
        if (_diubah is { } awal)
        {
            if (!await Dialogs.ConfirmAsync(this, "Simpan perubahan",
                    $"Ganti isi order {awal.Nomor} menjadi {_baris.Count} item?")) return;
            await Service.UbahAsync(awal, _baris, _diagnosa.Text ?? "", _informasi.Text ?? "", pa ? _bahanPa : null);
            SelesaiUbah();
            Ui.Ok(_status, $"Order {awal.Nomor} diperbarui.");
            try { await MuatRiwayatAsync(); }
            catch (Exception) { Ui.Ok(_status, $"Order {awal.Nomor} diperbarui. Riwayat belum bisa dimuat ulang."); }
            return;
        }
        if (!await Dialogs.ConfirmAsync(this, "Kirim order", $"Kirim {_baris.Count} item untuk kunjungan {_noRawat}?")) return;
        var nomor = await Service.SimpanAsync(_baris, _diagnosa.Text ?? "", _informasi.Text ?? "", pa ? _bahanPa : null);
        _baris.Clear();
        _bahanPa = null;
        IsiDraf();
        _diagnosa.Clear();
        _informasi.Clear();
        Ui.Ok(_status, $"Order {nomor} tersimpan.");
        try { await MuatRiwayatAsync(); }
        catch (Exception) { Ui.Ok(_status, $"Order {nomor} tersimpan. Riwayat belum bisa dimuat ulang."); }
    }

    private async Task<bool> IsiBahanPaAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return false;
        var form = new StackPanel { Spacing = 6, Margin = new Thickness(12) };
        void Tambah(string label, Control control)
        {
            form.Children.Add(new TextBlock { Text = label });
            Avalonia.Automation.AutomationProperties.SetName(control, label);
            form.Children.Add(control);
        }
        TextBox Field(string label, string? value, int limit)
        {
            var field = new TextBox { Text = value, MaxLength = limit };
            Tambah(label, field);
            return field;
        }
        var diambil = new CalendarDatePicker { SelectedDate = _bahanPa?.Diambil };
        Tambah("Tanggal pengambilan bahan", diambil);
        var cara = Field("Diperoleh dengan", _bahanPa?.Cara, 40);
        var lokasi = Field("Lokasi jaringan", _bahanPa?.Lokasi, 40);
        var pengawet = Field("Diawetkan dengan", _bahanPa?.Pengawet, 40);
        var tempat = Field("Tempat PA sebelumnya", _bahanPa?.TempatSebelumnya, 100);
        var tanggal = new CalendarDatePicker { SelectedDate = _bahanPa?.TanggalSebelumnya };
        Tambah("Tanggal PA sebelumnya", tanggal);
        var nomor = Field("Nomor PA sebelumnya", _bahanPa?.NomorSebelumnya, 20);
        var diagnosis = Field("Diagnosis PA sebelumnya", _bahanPa?.DiagnosisSebelumnya, 100);
        var dialog = new Window { Title = "Bahan Patologi Anatomi", Width = 480, Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = form } };
        form.Children.Add(Ui.Bar(Ui.Btn("Gunakan", (_, _) => dialog.Close(true)), Ui.Btn("Batal", (_, _) => dialog.Close(false))));
        if (!await dialog.ShowDialog<bool>(owner)) return false;
        string Value(TextBox field) => field.Text?.Trim() ?? "";
        _bahanPa = new(diambil.SelectedDate, Value(cara), Value(lokasi), Value(pengawet), Value(tempat),
            tanggal.SelectedDate, Value(nomor), Value(diagnosis));
        return true;
    }

    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Data gagal diproses. Periksa koneksi database."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }
}
