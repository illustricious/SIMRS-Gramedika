using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Farmasi;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Farmasi;

public partial class InputPenjualanView : UserControl
{
    public event EventHandler? BackRequested;

    private List<PenjualanLine> _all = new();
    private bool _saving;

    public InputPenjualanView()
    {
        InitializeComponent();

        this.FindControl<CalendarDatePicker>("TanggalPicker")!.SelectedDate = DateTime.Today;

        var jenisCombo = this.FindControl<ComboBox>("JenisJualCombo")!;
        jenisCombo.ItemsSource = new[] { "Jual Bebas" };
        jenisCombo.SelectedIndex = 0;
    }

    private async System.Threading.Tasks.Task LoadPetugasAsync()
    {
        var combo = this.FindControl<ComboBox>("PetugasCombo")!;
        try
        {
            var petugas = await new FarmasiStokRepository(DatabaseSettings.Load()).PetugasAsync();
            var items = petugas.Select(p => new PetugasItem(p.Kode, p.Nama)).ToList();
            combo.ItemsSource = items;
            var user = AppSession.CurrentUser;
            var (kode, terkunci) = PetugasPilihan.Tentukan(petugas, user?.IsAdminUtama == true, user?.UserId ?? "");
            combo.SelectedItem = kode is null ? combo.SelectedItem : items.First(i => i.Kode == kode);
            combo.IsEnabled = !terkunci;
        }
        catch (Exception)
        {
            // Tidak terhubung: Simpan akan menolak karena petugas belum dipilih.
        }
    }

    private sealed record PetugasItem(string Kode, string Nama)
    {
        public override string ToString() => $"{Kode} - {Nama}";
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        await LoadPetugasAsync();
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var katalog = await new FarmasiRepository(db).GetKatalogPenjualanAsync();
            if (AppSession.CurrentUser is { } user)
                this.FindControl<ComboBox>("AkunBayarCombo")!.ItemsSource = await new PenjualanTunaiService(DatabaseSettings.Load(), user).AkunAsync();
            _all = katalog.Select(k => new PenjualanLine(k)).ToList();
            foreach (var line in _all)
                line.PropertyChanged += (_, _) => RecalculateTotals();
        }
        catch (Exception ex)
        {
            _all = new List<PenjualanLine>();
            ShowStatus($"Gagal memuat katalog barang: {ex.Message}", isError: true);
        }

        ApplyFilter();
        RecalculateTotals();
    }

    private void ApplyFilter()
    {
        var keyword = this.FindControl<TextBox>("KeywordBox")!.Text?.Trim();
        IEnumerable<PenjualanLine> query = _all;

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x =>
                (x.NamaBarang ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.KodeBarang.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        this.FindControl<DataGrid>("DataGrid")!.ItemsSource = query.ToList();
    }

    private void OnKeywordChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e) => ApplyFilter();

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e) => RecalculateTotals();

    private void OnPaymentFieldChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e) => RecalculateTotals();

    private void RecalculateTotals()
    {
        var grandTotal = _all.Where(l => l.Jml > 0).Sum(l => l.Total);
        this.FindControl<TextBlock>("GrandTotalText")!.Text = grandTotal.ToString("N0");

        var ppnPercent = ParseDecimal(this.FindControl<TextBox>("PpnPercentBox")!.Text);
        var ongkosKirim = ParseDecimal(this.FindControl<TextBox>("OngkosKirimBox")!.Text);
        var tagihanPpn = grandTotal + Math.Round(grandTotal * ppnPercent / 100m, 0) + ongkosKirim;
        this.FindControl<TextBlock>("TagihanPpnText")!.Text = tagihanPpn.ToString("N0");

        var jumlahBayar = ParseDecimal(this.FindControl<TextBox>("JumlahBayarBox")!.Text);
        var kembali = jumlahBayar - tagihanPpn;
        this.FindControl<TextBlock>("KembaliText")!.Text = (kembali > 0 ? kembali : 0).ToString("N0");
    }

    private static decimal ParseDecimal(string? text)
        => decimal.TryParse(text, out var v) ? v : 0;

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(PenjualanTunaiService.Izin)) return;
        var lines = _all.Where(l => l.Jml > 0).ToList();
        if (lines.Count == 0)
        {
            ShowStatus("Isi jumlah (Jml) minimal satu barang terlebih dahulu.", isError: true);
            return;
        }

        if (this.FindControl<ComboBox>("PetugasCombo")!.SelectedItem is not PetugasItem petugas)
        {
            ShowStatus(AppSession.CurrentUser is { IsAdminUtama: false }
                ? $"Akun {AppSession.CurrentUser.UserId} tidak terdaftar sebagai petugas aktif, transaksi tidak bisa disimpan."
                : "Pilih petugas terlebih dahulu.", isError: true);
            return;
        }

        var catatan = this.FindControl<TextBox>("CatatanBox")!.Text?.Trim();
        if (string.IsNullOrWhiteSpace(catatan))
        {
            ShowStatus("Catatan wajib diisi (jadi keterangan transaksi — kolom NOT NULL di skema legacy).", isError: true);
            return;
        }

        var pasien = this.FindControl<TextBox>("PasienBox")!.Text?.Trim() ?? "";
        if (this.FindControl<ComboBox>("AkunBayarCombo")!.SelectedItem is not string akun || pasien == "")
        {
            ShowStatus("Isi nomor RM dan pilih akun bayar.", isError: true);
            return;
        }
        var diterima = ParseDecimal(this.FindControl<TextBox>("JumlahBayarBox")!.Text);

        _saving = true;
        var button = this.FindControl<Button>("SimpanButton")!;
        button.IsEnabled = false;
        try
        {
            var inputs = lines.Select(l => new PenjualanLineInput(
                l.KodeBarang, l.Jml, l.Harga, l.PtgRp, l.TmbRp + l.EmbRp + l.TslRp, l.NoBatch, l.NoFaktur)).ToList();
            if (!await Dialogs.ConfirmAsync(this, "Simpan penjualan", $"Catat jual bebas Rp{lines.Sum(l => l.Total):N0} untuk RM {pasien}?")) return;
            var noKeluar = await new PenjualanTunaiService(DatabaseSettings.Load(), user)
                .SimpanAsync(inputs, catatan, petugas.Kode, pasien, "F.A", akun, diterima);

            ShowStatus($"Transaksi {noKeluar} tersimpan — {lines.Count} jenis barang, total Rp {lines.Sum(l => l.Total):N0}.", isError: false);

            this.FindControl<TextBox>("PasienBox")!.Text = string.Empty;
            this.FindControl<TextBox>("CatatanBox")!.Text = "Jual bebas";
            this.FindControl<TextBox>("JumlahBayarBox")!.Text = "0";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal simpan transaksi: {ex.Message}", isError: true);
        }
        finally { _saving = false; button.IsEnabled = true; }
    }

    private void ShowStatus(string message, bool isError)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        status.Foreground = isError ? Avalonia.Media.Brush.Parse("#DC2626") : Avalonia.Media.Brushes.Green;
        status.Text = message;
        status.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
