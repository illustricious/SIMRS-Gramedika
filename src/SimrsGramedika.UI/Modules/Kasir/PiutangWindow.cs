using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Kasir;

/// <summary>Daftar piutang pasien, pembayaran per akun piutang dan pembatalannya.</summary>
internal sealed class PiutangWindow : Window
{
    private readonly PiutangPasienService _service;
    private readonly TextBox _cari = new() { PlaceholderText = "No. Rawat, No. RM, atau nama", Width = 280 };
    private readonly CheckBox _belumLunas = new() { Content = "Belum lunas saja", IsChecked = true };
    private readonly DataGrid _daftar = Ui.Grid(new("rawat", "No. Rawat", 160), new("rm", "No. RM", 90), new("nama", "Nama", 200),
        new("tanggal", "Tanggal", 90), new("total", "Tagihan", 105), new("muka", "Uang muka", 100), new("sisa", "Sisa", 105),
        new("status", "Status", 95));
    private readonly DataGrid _akun = Ui.Grid(new("nama", "Akun piutang", 260), new("total", "Piutang", 120), new("sisa", "Sisa", 120),
        new("tempo", "Jatuh tempo", 110));
    private readonly DataGrid _riwayat = Ui.Grid(new("tanggal", "Tanggal", 90), new("rekening", "Diterima di", 170),
        new("cicilan", "Cicilan", 100), new("diskon", "Diskon", 90), new("tak", "Tidak terbayar", 110), new("catatan", "Keterangan", 260));
    private readonly ComboBox _bayar = new() { MinWidth = 200, PlaceholderText = "Akun bayar" };
    private readonly TextBox _cicilan = new() { PlaceholderText = "Cicilan", Width = 130 };
    private readonly TextBox _diskon = new() { PlaceholderText = "Diskon", Width = 110 };
    private readonly TextBox _tidakTerbayar = new() { PlaceholderText = "Tidak terbayar", Width = 130 };
    private readonly TextBox _catatan = new() { PlaceholderText = "Keterangan pembayaran", MaxLength = 100, Width = 280 };
    private readonly TextBlock _status = Ui.Status();
    private readonly Grid _layout;
    private string _rawat = "";
    private bool _busy;

    public PiutangWindow(AuthenticatedUser user, string? noRawat = null)
    {
        _service = new(DatabaseSettings.Load(), user);
        Title = "Piutang Pasien";
        Width = 1040;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _cari.Text = noRawat ?? "";
        if (noRawat is not null) _belumLunas.IsChecked = false;

        _layout = new Grid
        {
            Margin = new Thickness(14), RowSpacing = 6,
            RowDefinitions = new("Auto,Auto,*,Auto,100,Auto,Auto,Auto,Auto,*,Auto,Auto"),
        };
        Control[] isi =
        [
            Ui.Title(Title),
            Ui.Bar(_cari, _belumLunas, Ui.Btn("Cari", async (_, _) => await JalankanAsync(CariAsync))),
            _daftar,
            Ui.Section("Akun piutang kunjungan terpilih"), _akun,
            Ui.Section("Catat pembayaran bertanggal hari ini"),
            Ui.Bar(_bayar, _cicilan, _diskon, _tidakTerbayar, Ui.Btn("Isi sisa", (_, _) => IsiSisa())),
            Ui.Bar(_catatan, Ui.Btn("Catat Pembayaran", async (_, _) => await JalankanAsync(BayarAsync))),
            Ui.Section("Riwayat pembayaran"), _riwayat,
            Ui.Bar(Ui.Btn("Batalkan Pembayaran Terpilih", async (_, _) => await JalankanAsync(BatalkanAsync)),
                Ui.Btn("Tutup", (_, _) => Close())),
            _status,
        ];
        for (var i = 0; i < isi.Length; i++) { Grid.SetRow(isi[i], i); _layout.Children.Add(isi[i]); }
        _daftar.SelectionChanged += async (_, _) =>
        {
            if (!_busy && Ui.Selected<PiutangPasien>(_daftar) is { } p) await JalankanAsync(() => MuatAsync(p.NoRawat));
        };
        Content = _layout;
        Closing += (_, args) => args.Cancel = _busy;
        Loaded += async (_, _) => await JalankanAsync(async () =>
        {
            _bayar.ItemsSource = await _service.AkunBayarAsync();
            await CariAsync();
        });
    }

    private static string Angka(decimal nilai) => nilai.ToString("#,0.####", Ui.Id);

    private static string Tanggal(DateTime? tanggal) => tanggal?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "-";

    private static decimal Baca(TextBox kotak, string nama)
    {
        if (string.IsNullOrWhiteSpace(kotak.Text)) return 0;
        return decimal.TryParse(kotak.Text.Trim(), NumberStyles.Number, Ui.Id, out var nilai)
            ? nilai : throw new ArgumentException($"{nama} harus angka (desimal memakai koma).");
    }

    private void IsiSisa()
    {
        if (Ui.Selected<AkunPiutangPasien>(_akun) is not { } akun) { Ui.Err(_status, "Pilih akun piutang dulu."); return; }
        try
        {
            var sisa = akun.Sisa - Baca(_diskon, "Diskon") - Baca(_tidakTerbayar, "Tidak terbayar");
            _cicilan.Text = Math.Max(0, sisa).ToString("0.####", Ui.Id);
        }
        catch (ArgumentException ex) { Ui.Err(_status, ex.Message); }
    }

    private async Task CariAsync()
    {
        var rows = await _service.DaftarAsync(_cari.Text ?? "", _belumLunas.IsChecked == true);
        Ui.FillRows(_daftar, rows.Select(p => new GridRow([p.NoRawat, p.NoRekamMedis, p.Nama, Tanggal(p.Tanggal), Angka(p.Total),
            Angka(p.UangMuka), Angka(p.Sisa), p.Status], p)));
        _rawat = "";
        Ui.FillRows(_akun, []);
        Ui.FillRows(_riwayat, []);
        Ui.Info(_status, $"{rows.Count} piutang ditampilkan.");
        if (rows.Count == 1) await MuatAsync(rows[0].NoRawat);
    }

    private async Task MuatAsync(string noRawat)
    {
        _rawat = "";
        Ui.FillRows(_akun, []);
        Ui.FillRows(_riwayat, []);
        foreach (var kotak in new[] { _cicilan, _diskon, _tidakTerbayar, _catatan }) kotak.Text = "";
        var akun = await _service.AkunAsync(noRawat);
        var riwayat = await _service.RiwayatAsync(noRawat);
        var baris = akun.Select(a => new GridRow([a.Nama, Angka(a.Total), Angka(a.Sisa), Tanggal(a.JatuhTempo)], a)).ToList();
        Ui.FillRows(_akun, baris);
        if (baris.Count == 1) _akun.SelectedItem = baris[0];
        Ui.FillRows(_riwayat, riwayat.Select(b => new GridRow([Tanggal(b.Tanggal), b.NamaRekeningBayar, Angka(b.Cicilan),
            Angka(b.Diskon), Angka(b.TidakTerbayar), b.Catatan], b)));
        _rawat = noRawat;
        Ui.Info(_status, $"No. Rawat {noRawat}: {riwayat.Count} pembayaran tercatat.");
    }

    private async Task BayarAsync()
    {
        var akun = Ui.Selected<AkunPiutangPasien>(_akun);
        if (_rawat.Length == 0 || akun is null) throw new ArgumentException("Pilih piutang dan akun piutangnya.");
        var bayar = _bayar.SelectedItem as string ?? throw new ArgumentException("Pilih akun bayar.");
        var cicilan = Baca(_cicilan, "Cicilan");
        var diskon = Baca(_diskon, "Diskon");
        var hapus = Baca(_tidakTerbayar, "Tidak terbayar");
        var rawat = _rawat;
        if (!await Dialogs.ConfirmAsync(this, "Bayar piutang",
                $"No. Rawat {rawat}, {akun.Nama}: cicilan {Angka(cicilan)} melalui {bayar}, diskon {Angka(diskon)}, " +
                $"tidak terbayar {Angka(hapus)}. Sisa akun menjadi {Angka(akun.Sisa - cicilan - diskon - hapus)}. Catat?")) return;
        await _service.BayarAsync(rawat, akun, bayar, cicilan, diskon, hapus, _catatan.Text ?? "");
        await MuatUlangAsync(rawat);
        Ui.Ok(_status, "Pembayaran piutang tercatat.");
    }

    private async Task BatalkanAsync()
    {
        var bayar = Ui.Selected<CicilanPiutang>(_riwayat);
        if (_rawat.Length == 0 || bayar is null) throw new ArgumentException("Pilih pembayaran pada riwayat.");
        var rawat = _rawat;
        if (!await Dialogs.ConfirmAsync(this, "Batalkan pembayaran",
                $"Batalkan pembayaran {Tanggal(bayar.Tanggal)} sebesar {Angka(bayar.Total)} pada No. Rawat {rawat}? " +
                "Sisa piutang dikembalikan dan jurnal pembalik diposting.")) return;
        await _service.BatalkanAsync(rawat, bayar);
        await MuatUlangAsync(rawat);
        Ui.Ok(_status, "Pembayaran dibatalkan.");
    }

    /// <summary>Segarkan daftar atas tanpa kehilangan kunjungan yang sedang dikerjakan.</summary>
    private async Task MuatUlangAsync(string noRawat)
    {
        await CariAsync();
        await MuatAsync(noRawat);
    }

    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Piutang gagal diproses. Periksa koneksi lalu muat ulang."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }
}
