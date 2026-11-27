using Avalonia;
using Avalonia.Controls;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatInap;

internal sealed class RanapGabungWindow : Window
{
    private readonly RawatGabungService _service;
    private readonly RawatInapListItem _ibu;
    private readonly DataGrid _bayi = Ui.Grid(new("rawat", "No. Rawat bayi", 170), new("rm", "No. RM", 110),
        new("nama", "Nama", 300), new("transaksi", "Transaksi", 100));
    private readonly DataGrid _pasien = Ui.Grid(new("rm", "No. RM", 120), new("nama", "Nama pasien", 340), new("lahir", "Tgl. lahir", 120));
    private readonly TextBox _cari = new() { PlaceholderText = "Cari No. RM atau nama bayi", Width = 280 };
    private readonly TextBlock _status = Ui.Status();
    private readonly Grid _layout;
    private bool _busy;

    public RanapGabungWindow(RawatInapListItem ibu, AuthenticatedUser user)
    {
        _ibu = ibu;
        _service = new(DatabaseSettings.Load(), user);
        Title = "Rawat Gabung Ibu dan Bayi";
        Width = 780;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var identitas = Ui.Hint($"Ibu: {ibu.NamaPasien} · No. RM {ibu.NoRekamMedis} · No. Rawat {ibu.NoRawat} · Kamar {ibu.KodeKamar}");
        _layout = new Grid { Margin = new Thickness(14), RowSpacing = 8, RowDefinitions = new("Auto,Auto,Auto,110,Auto,Auto,Auto,*,Auto,Auto,Auto") };
        Control[] controls =
        [
            Ui.Title(Title), identitas, Ui.Section("Bayi yang digabung"), _bayi,
            Ui.Bar(Ui.Btn("Lepas Gabung", async (_, _) => await JalankanAsync(LepasAsync))),
            Ui.Section("Gabungkan bayi (daftarkan dulu sebagai pasien bila belum ada)"),
            Ui.Bar(_cari, Ui.Btn("Cari", async (_, _) => await JalankanAsync(CariAsync))), _pasien,
            Ui.Bar(Ui.Btn("Gabungkan Pasien Terpilih", async (_, _) => await JalankanAsync(GabungAsync)), Ui.Btn("Tutup", (_, _) => Close())),
            Ui.Hint("Bayi memakai kamar ibu dan tagihannya digabung ke tagihan ibu."), _status,
        ];
        for (var index = 0; index < controls.Length; index++)
        {
            Grid.SetRow(controls[index], index);
            _layout.Children.Add(controls[index]);
        }
        Content = _layout;
        Closing += (_, args) => args.Cancel = _busy;
        Loaded += async (_, _) => await JalankanAsync(MuatAsync);
    }

    private async Task MuatAsync()
    {
        var rows = await _service.DaftarAsync(_ibu.NoRawat);
        Ui.FillRows(_bayi, rows.Select(b => new GridRow([b.NoRawat, b.NoRekamMedis, b.Nama, b.JumlahTransaksi.ToString()], b)));
    }

    private async Task CariAsync()
    {
        if (string.IsNullOrWhiteSpace(_cari.Text)) throw new ArgumentException("Isi No. RM atau nama bayi.");
        var rows = await _service.CariPasienAsync(_cari.Text);
        Ui.FillRows(_pasien, rows.Select(p => new GridRow([p.NoRekamMedis, p.Nama, p.TanggalLahir], p)));
        Ui.Info(_status, $"{rows.Count} pasien ditemukan.");
    }

    private async Task GabungAsync()
    {
        var pasien = Ui.Selected<PasienCalonBayi>(_pasien) ?? throw new ArgumentException("Pilih pasien bayi di hasil pencarian.");
        if (!await Dialogs.ConfirmAsync(this, "Rawat gabung",
                $"Gabungkan {pasien.Nama} (No. RM {pasien.NoRekamMedis}, lahir {pasien.TanggalLahir}) sebagai bayi dari {_ibu.NamaPasien}?")) return;
        var noRawat = await _service.GabungAsync(_ibu.NoRawat, pasien.NoRekamMedis);
        await MuatAsync();
        Ui.Ok(_status, $"Bayi digabung dengan No. Rawat {noRawat}.");
    }

    private async Task LepasAsync()
    {
        var bayi = Ui.Selected<BayiGabung>(_bayi) ?? throw new ArgumentException("Pilih bayi yang akan dilepas.");
        var peringatan = bayi.JumlahTransaksi == 0 ? ""
            : $" Bayi sudah punya {bayi.JumlahTransaksi} transaksi: sesudah dilepas transaksi itu tidak ikut tagihan ibu, " +
              "sedangkan registrasinya tetap bertanda Sudah Bayar.";
        if (!await Dialogs.ConfirmAsync(this, "Lepas rawat gabung",
                $"Lepas {bayi.Nama} ({bayi.NoRawat})? Registrasi bayi menjadi rawat jalan.{peringatan}")) return;
        await _service.LepasAsync(_ibu.NoRawat, bayi.NoRawat);
        await MuatAsync();
        Ui.Ok(_status, "Rawat gabung dilepas.");
    }

    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Rawat gabung gagal diproses. Periksa koneksi lalu muat ulang."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }
}
