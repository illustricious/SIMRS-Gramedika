using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Farmasi;

public partial class InputPenjualanView : UserControl
{
    public event EventHandler? BackRequested;

    /// <summary>Same placeholder-NIP situation as RawatJalanView — dev auth doesn't carry a linked pegawai record yet (see docs/audit/HANDOVER.md, LegacyUserRepository section).</summary>
    private const string PlaceholderPetugasNip = "D0000099";

    private List<PenjualanLine> _all = new();

    public InputPenjualanView()
    {
        InitializeComponent();

        this.FindControl<TextBox>("PetugasBox")!.Text = PlaceholderPetugasNip;
        this.FindControl<DatePicker>("TanggalPicker")!.SelectedDate = DateTime.Today;

        var jenisCombo = this.FindControl<ComboBox>("JenisJualCombo")!;
        jenisCombo.ItemsSource = new[] { "Jual Bebas", "Resep Dokter" };
        jenisCombo.SelectedIndex = 0;
        jenisCombo.SelectionChanged += OnJenisJualChanged;

        var akunCombo = this.FindControl<ComboBox>("AkunBayarCombo")!;
        akunCombo.ItemsSource = new[] { "1. Bayar Cash", "2. Bayar Kredit (BPJS/Asuransi)" };
        akunCombo.SelectedIndex = 0;
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var katalog = await new FarmasiRepository(db).GetKatalogPenjualanAsync();
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

    private void OnJenisJualChanged(object? sender, SelectionChangedEventArgs e)
    {
        var combo = (ComboBox)sender!;
        if (combo.SelectedIndex == 1)
            ShowStatus("Untuk penjualan berdasar resep dokter, pakai menu \"Daftar Resep Dokter\" — pengisian otomatis dari resep belum terhubung ke layar ini.", isError: true);
        else
            HideStatus();
    }

    private void OnPaymentFieldChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e) => RecalculateTotals();

    /// <summary>
    /// Recomputes Grand Total (sum of all lines with Jml &gt; 0) plus the
    /// PPN/Ongkos Kirim/Jumlah Bayar/Kembali figures below it. Pure UI-side
    /// arithmetic — nothing here is persisted until Simpan (PPN/Ongkos
    /// Kirim/pembayaran aren't columns on pengeluaran_obat_bhp in the legacy
    /// schema, so only the per-line Total actually gets saved; see
    /// FarmasiRepository.SimpanPenjualanAsync).
    /// </summary>
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
        var lines = _all.Where(l => l.Jml > 0).ToList();
        if (lines.Count == 0)
        {
            ShowStatus("Isi jumlah (Jml) minimal satu barang terlebih dahulu.", isError: true);
            return;
        }

        var catatan = this.FindControl<TextBox>("CatatanBox")!.Text?.Trim();
        if (string.IsNullOrWhiteSpace(catatan))
        {
            ShowStatus("Catatan wajib diisi (jadi keterangan transaksi — kolom NOT NULL di skema legacy).", isError: true);
            return;
        }

        var pasien = this.FindControl<TextBox>("PasienBox")!.Text?.Trim();
        var keterangan = string.IsNullOrWhiteSpace(pasien) ? catatan : $"{catatan} - Pasien: {pasien}";

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var inputs = lines.Select(l => new PenjualanLineInput(
                l.KodeBarang, l.Jml, l.Harga, l.PtgRp, l.TmbRp + l.EmbRp + l.TslRp, l.NoBatch, l.NoFaktur)).ToList();

            var noKeluar = await new FarmasiRepository(db).SimpanPenjualanAsync(inputs, keterangan, PlaceholderPetugasNip);

            ShowStatus($"Transaksi {noKeluar} tersimpan — {lines.Count} jenis barang, total Rp {lines.Sum(l => l.Total):N0}.", isError: false);

            this.FindControl<TextBox>("PasienBox")!.Text = string.Empty;
            this.FindControl<TextBox>("CatatanBox")!.Text = "Jual bebas";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal simpan transaksi: {ex.Message}", isError: true);
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        status.Foreground = isError ? Avalonia.Media.Brush.Parse("#DC2626") : Avalonia.Media.Brushes.Green;
        status.Text = message;
        status.IsVisible = true;
    }

    private void HideStatus() => this.FindControl<TextBlock>("StatusText")!.IsVisible = false;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
