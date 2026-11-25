using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatInap;

internal sealed class TindakanRanapWindow : Window
{
    private readonly TindakanRanapInputService _service;
    private readonly ComboBox _jenis = new() { ItemsSource = new[] { "Dokter", "Petugas", "Dokter dan petugas" }, SelectedIndex = 0 };
    private readonly ComboBox _dokter = new() { PlaceholderText = "Pilih dokter", MinWidth = 180, DisplayMemberBinding = new Binding(nameof(PegawaiPilihan.Nama)) };
    private readonly ComboBox _petugas = new() { PlaceholderText = "Pilih petugas", MinWidth = 180, DisplayMemberBinding = new Binding(nameof(PegawaiPilihan.Nama)) };
    private readonly CalendarDatePicker _tanggal = new() { SelectedDate = DateTime.Today };
    private readonly TextBox _jam = new() { Text = DateTime.Now.ToString("HH:mm:ss"), Width = 100 };
    private readonly TextBox _cari = new() { PlaceholderText = "Cari tindakan", Width = 245 };
    private readonly DataGrid _tarif = Ui.Grid(new("kode", "Kode", 105), new("nama", "Tindakan", 290),
        new("kategori", "Kategori", 145), new("tarif", "Tarif", 110));
    private readonly TextBlock _status = Ui.Status();
    private readonly Grid _layout;
    private bool _busy;
    private TindakanRalanJenis Jenis => (TindakanRalanJenis)_jenis.SelectedIndex;

    public TindakanRanapWindow(RawatInapListItem visit, AuthenticatedUser user)
    {
        _service = new(DatabaseSettings.Load(), visit.Key, visit.NoRekamMedis, user);
        Title = "Input Tindakan Rawat Inap";
        Width = 810;
        Height = 630;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _tarif.SelectionMode = DataGridSelectionMode.Extended;
        _petugas.IsVisible = false;
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
        var actions = Ui.Bar(Ui.Btn("Simpan Tindakan", async (_, _) => await JalankanAsync(SimpanAsync)),
            Ui.Btn("Tutup", (_, _) => Close()));
        _layout = new Grid { Margin = new Thickness(14), RowSpacing = 8, RowDefinitions = new("Auto,Auto,Auto,Auto,Auto,*,Auto,Auto") };
        Control[] controls = [Ui.Title(Title), identitas, input, waktu, cari, _tarif, actions, _status];
        for (var index = 0; index < controls.Length; index++)
        {
            Grid.SetRow(controls[index], index);
            _layout.Children.Add(controls[index]);
        }
        Content = _layout;
        Closing += (_, args) => args.Cancel = _busy;
        Loaded += async (_, _) => await JalankanAsync(async () =>
        {
            var (dokter, petugas) = await _service.PelaksanaAsync();
            _dokter.ItemsSource = dokter;
            _petugas.ItemsSource = petugas;
            _dokter.SelectedItem = dokter.FirstOrDefault(p => p.Nik == user.UserId);
            _petugas.SelectedItem = petugas.FirstOrDefault(p => p.Nik == user.UserId);
            await CariAsync();
        });
    }

    private async Task CariAsync()
    {
        _tarif.ItemsSource = null;
        var rows = await _service.CariAsync(Jenis, _cari.Text ?? "");
        Ui.FillRows(_tarif, rows.Select(row => new GridRow([row.Kode, row.Nama, row.Kategori, $"{row.Total:N2}"], row)));
    }

    private async Task SimpanAsync()
    {
        if (_tanggal.SelectedDate is not { } tanggal || !TimeOnly.TryParseExact(_jam.Text, "HH:mm:ss", out var jam))
            throw new ArgumentException("Isi tanggal dan jam tindakan dalam format JJ:mm:dd.");
        var rows = _tarif.SelectedItems.OfType<GridRow>().Select(r => r.Tag).OfType<TarifTindakanRanap>().ToArray();
        if (rows.Length == 0) throw new ArgumentException("Pilih tindakan yang akan disimpan.");
        if (!await Dialogs.ConfirmAsync(this, "Simpan tindakan", $"Catat {rows.Length} tindakan dengan tarif Rp{rows.Sum(r => r.Total):N2}?")) return;
        await _service.SimpanAsync(Jenis, DateOnly.FromDateTime(tanggal).ToDateTime(jam),
            (_dokter.SelectedItem as PegawaiPilihan)?.Nik ?? "", (_petugas.SelectedItem as PegawaiPilihan)?.Nik ?? "", rows);
        _tarif.SelectedItems.Clear();
        Ui.Ok(_status, $"{rows.Length} tindakan dan jurnal tersimpan.");
    }

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
