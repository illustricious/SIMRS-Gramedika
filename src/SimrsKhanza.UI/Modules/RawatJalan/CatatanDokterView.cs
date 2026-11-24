using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatJalan;

internal sealed class CatatanDokterView : UserControl
{
    private readonly CatatanDokterService _service;
    private readonly AuthenticatedUser _user;
    private readonly DataGrid _grid = Ui.Grid(new("waktu", "Waktu", 160), new("dokter", "Dokter", 205), new("isi", "Catatan", 320));
    private readonly CalendarDatePicker _date = new();
    private readonly TextBox _time = new() { Width = 110, PlaceholderText = "HH:mm:ss" };
    private readonly ComboBox _dokter = new() { MinWidth = 180 };
    private readonly TextBox _isi = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90,
        MaxLength = CatatanDokterService.PanjangMaksimal, PlaceholderText = "Isi catatan dokter" };
    private readonly TextBlock _status = Ui.Status();
    private readonly Button _hapus;
    private readonly Button _simpan;
    private readonly Grid _layout;
    private CatatanDokter? _selected;
    private bool _busy;

    public CatatanDokterView(string noRawat, AuthenticatedUser user)
    {
        _service = new(DatabaseSettings.Load(), noRawat, user);
        _user = user;
        _dokter.IsEnabled = user.IsAdminUtama;
        _hapus = Ui.Btn("Hapus", async (_, _) => await JalankanAsync(HapusAsync), false);
        _simpan = Ui.Btn("Simpan Catatan Baru", async (_, _) => await JalankanAsync(SimpanAsync));
        _simpan.Classes.Add("accent");
        _grid.SelectionChanged += (_, _) =>
        {
            _selected = Ui.Selected<CatatanDokter>(_grid);
            IsiForm(_selected, user);
        };
        var editor = new StackPanel { Spacing = 6, Children =
        {
            Ui.Form(("Tanggal dan jam", Ui.Bar(_date, _time)), ("Dokter", _dokter), ("Catatan", _isi)),
            Ui.Bar(Ui.Btn("Baru", (_, _) => ResetForm(user)), _simpan, _hapus,
                Ui.Btn("Muat Ulang", async (_, _) => await JalankanAsync(MuatDaftarAsync))),
            _status,
        }};
        _layout = new Grid { RowDefinitions = new("Auto,*,Auto"), RowSpacing = 10, Margin = new Thickness(12) };
        var controls = new Control[] { Ui.Title("Catatan Dokter"), _grid, new ScrollViewer { MaxHeight = 310, Content = editor } };
        for (var i = 0; i < controls.Length; i++)
        {
            Grid.SetRow(controls[i], i);
            _layout.Children.Add(controls[i]);
        }
        Content = _layout;

        async Task InisialisasiAsync()
        {
            _dokter.ItemsSource = await _service.DokterAsync();
            ResetForm(user);
            await MuatDaftarAsync();
        }
        Loaded += async (_, _) =>
        {
            if (_dokter.ItemsSource is null) await JalankanAsync(InisialisasiAsync);
        };
        ResetForm(user);
    }

    private void IsiForm(CatatanDokter? catatan, AuthenticatedUser user)
    {
        var waktu = catatan is null ? DateTime.Now : catatan.Waktu;
        _date.SelectedDate = waktu?.Date;
        _time.Text = waktu?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _isi.Text = catatan?.Isi;
        _hapus.IsEnabled = catatan is not null;
        _simpan.Content = catatan is null ? "Simpan Catatan Baru" : "Simpan Perubahan";
        _dokter.SelectedItem = (_dokter.ItemsSource as IEnumerable<PegawaiPilihan>)?.FirstOrDefault(
            d => string.Equals(d.Nik, user.IsAdminUtama ? catatan?.KodeDokter : user.UserId, StringComparison.OrdinalIgnoreCase));
    }

    private void ResetForm(AuthenticatedUser user)
    {
        _grid.SelectedItem = null;
        _selected = null;
        IsiForm(null, user);
    }

    private async Task MuatDaftarAsync()
    {
        var notes = await _service.DaftarAsync();
        Ui.FillRows(_grid, notes.Select(c => new GridRow(
            [c.Waktu?.ToString("dd/MM/yyyy HH:mm:ss") ?? "", $"{c.KodeDokter} {c.Dokter}", c.Isi], c)));
        Ui.Info(_status, $"{notes.Count} catatan");
    }

    private async Task SimpanAsync()
    {
        if (_date.SelectedDate is not { } tanggal ||
            !TimeOnly.TryParseExact(_time.Text, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var jam))
            throw new ArgumentException("Isi tanggal dan jam berformat HH:mm:ss.");
        if (_dokter.SelectedItem is not PegawaiPilihan dokter) throw new ArgumentException("Pilih dokter atau periksa NIK akun.");
        await _service.SimpanAsync(tanggal.Date + jam.ToTimeSpan(), dokter.Nik, _isi.Text ?? "", _selected);
        await MuatSesudahPerubahanAsync("Catatan tersimpan.");
    }

    private async Task HapusAsync()
    {
        if (_selected is not { } note) return;
        if (!await Dialogs.ConfirmAsync(this, "Hapus Catatan Dokter", $"Hapus catatan {note.Waktu:dd/MM/yyyy HH:mm:ss} untuk No. Rawat {note.NoRawat}?")) return;
        await _service.HapusAsync(note);
        await MuatSesudahPerubahanAsync("Catatan dihapus.");
    }

    private async Task MuatSesudahPerubahanAsync(string pesan)
    {
        ResetForm(_user);
        _grid.ItemsSource = null;
        try
        {
            await MuatDaftarAsync();
            Ui.Ok(_status, pesan);
        }
        catch (Exception)
        {
            Ui.Err(_status, $"{pesan} Daftar belum dapat dimuat ulang.");
        }
    }

    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try { await action(); }
        catch (ArgumentException ex) { Ui.Err(_status, ex.Message); }
        catch (InvalidOperationException ex) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Catatan gagal diproses. Periksa koneksi dan muat ulang daftar."); }
        finally
        {
            _busy = false;
            _layout.IsEnabled = true;
        }
    }
}
