using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Kasir;

/// <summary>Tambahan atau potongan biaya satu kunjungan; setiap perubahan langsung disimpan.</summary>
internal sealed class PenyesuaianWindow : Window
{
    private readonly AuthenticatedUser _user;
    private readonly string _noRawat;
    private readonly JenisPenyesuaian _jenis;
    private readonly DataGrid _daftar = Ui.Grid(new("nama", "Nama biaya", 340), new("besar", "Besar (Rp)", 150));
    private readonly TextBox _nama = new() { PlaceholderText = "Nama biaya", MaxLength = 60, Width = 300 };
    private readonly TextBox _besar = new() { PlaceholderText = "Besar", Width = 150 };
    private readonly TextBlock _status = Ui.Status();
    private readonly Grid _layout;
    private bool _busy;

    public PenyesuaianWindow(AuthenticatedUser user, string noRawat, string pasien, JenisPenyesuaian jenis)
    {
        _user = user;
        _noRawat = noRawat;
        _jenis = jenis;
        Title = $"{jenis} Biaya";
        Width = 640;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _layout = new Grid { Margin = new Thickness(14), RowSpacing = 6, RowDefinitions = new("Auto,Auto,*,Auto,Auto,Auto") };
        Control[] isi =
        [
            Ui.Title(Title),
            Ui.Hint($"{pasien} · No. Rawat {noRawat}"),
            _daftar,
            Ui.Bar(_nama, _besar, Ui.Btn("Tambahkan", async (_, _) => await JalankanAsync(TambahAsync))),
            Ui.Bar(Ui.Btn("Hapus Terpilih", async (_, _) => await JalankanAsync(HapusAsync)), Ui.Btn("Tutup", (_, _) => Close())),
            _status,
        ];
        for (var i = 0; i < isi.Length; i++) { Grid.SetRow(isi[i], i); _layout.Children.Add(isi[i]); }
        Content = _layout;
        Closing += (_, args) => args.Cancel = _busy;
        Loaded += async (_, _) => await JalankanAsync(MuatAsync);
    }

    private static string Angka(decimal nilai) => nilai.ToString("#,0.####", Ui.Id);

    private async Task DenganKasirAsync(Func<KasirRepository, Task> aksi)
    {
        using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
        await aksi(new KasirRepository(db));
    }

    private Task MuatAsync() => DenganKasirAsync(async kasir =>
    {
        var rows = await kasir.DaftarPenyesuaianAsync(_noRawat, _jenis);
        Ui.FillRows(_daftar, rows.Select(r => new GridRow([r.Nama, Angka(r.Besar)], r)));
        Ui.Info(_status, $"{rows.Count} baris, jumlah Rp {Angka(rows.Sum(r => r.Besar))}.");
    });

    private async Task TambahAsync()
    {
        if (!decimal.TryParse(_besar.Text?.Trim(), NumberStyles.Number, Ui.Id, out var besar))
            throw new ArgumentException("Besar biaya harus angka (desimal memakai koma).");
        var nama = _nama.Text?.Trim() ?? "";
        if (!await Dialogs.ConfirmAsync(this, Title!, $"Simpan {_jenis.ToString().ToLower(Ui.Id)} \"{nama}\" sebesar Rp {Angka(besar)}?"))
            return;
        await DenganKasirAsync(kasir => kasir.TambahPenyesuaianAsync(_noRawat, _jenis, nama, besar, _user));
        _nama.Text = _besar.Text = "";
        await MuatAsync();
    }

    private async Task HapusAsync()
    {
        var baris = Ui.Selected<PenyesuaianBiaya>(_daftar) ?? throw new ArgumentException("Pilih baris yang akan dihapus.");
        if (!await Dialogs.ConfirmAsync(this, Title!, $"Hapus \"{baris.Nama}\" sebesar Rp {Angka(baris.Besar)}?")) return;
        await DenganKasirAsync(kasir => kasir.HapusPenyesuaianAsync(_noRawat, _jenis, baris.Nama, _user));
        await MuatAsync();
    }

    private async Task JalankanAsync(Func<Task> aksi)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try { await aksi(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Data biaya gagal diproses. Periksa koneksi lalu coba lagi."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }
}
