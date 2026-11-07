using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Farmasi;

public partial class InputPenjualanView : UserControl
{
    public event EventHandler? BackRequested;

    /// <summary>Same placeholder-NIP situation as RawatJalanView — dev auth doesn't carry a linked pegawai record yet (see docs/audit/HANDOVER.md, LegacyUserRepository section).</summary>
    private const string PlaceholderPetugasNip = "D0000099";

    private DataBarang? _selected;

    public InputPenjualanView() => InitializeComponent();

    private async void OnCariBarangKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var combo = this.FindControl<ComboBox>("BarangCombo")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var keyword = this.FindControl<TextBox>("CariBarangBox")!.Text ?? string.Empty;
            combo.ItemsSource = await new FarmasiRepository(db).SearchBarangAsync(keyword);
        }
        catch (Exception)
        {
            combo.ItemsSource = new List<DataBarang>();
        }
    }

    private void OnBarangSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = this.FindControl<ComboBox>("BarangCombo")!.SelectedItem as DataBarang;
        var hargaText = this.FindControl<TextBlock>("HargaText")!;
        hargaText.Text = _selected is null ? "" : $"Harga dasar: Rp {_selected.Dasar:N0} / {_selected.KodeSatuanBesar}";
        this.FindControl<Button>("SimpanButton")!.IsEnabled = _selected is not null;
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var status = this.FindControl<TextBlock>("StatusText")!;

        if (!decimal.TryParse(this.FindControl<TextBox>("JumlahBox")!.Text, out var jumlah) || jumlah <= 0)
        {
            ShowError(status, "Jumlah harus angka lebih dari 0.");
            return;
        }

        var keterangan = this.FindControl<TextBox>("KeteranganBox")!.Text?.Trim();
        if (string.IsNullOrWhiteSpace(keterangan))
        {
            ShowError(status, "Keterangan/Tujuan wajib diisi (kolom NOT NULL di skema legacy).");
            return;
        }

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var noFaktur = this.FindControl<TextBox>("NoFakturBox")!.Text;
            var noKeluar = await new FarmasiRepository(db).InputPenjualanAsync(
                _selected.KodeBarang, jumlah, keterangan, noFaktur, PlaceholderPetugasNip);

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Transaksi {noKeluar} tersimpan — {_selected.NamaBarang} x{jumlah}.";
            status.IsVisible = true;

            _selected = null;
            this.FindControl<ComboBox>("BarangCombo")!.ItemsSource = null;
            this.FindControl<TextBox>("CariBarangBox")!.Text = string.Empty;
            this.FindControl<TextBlock>("HargaText")!.Text = string.Empty;
            this.FindControl<TextBox>("JumlahBox")!.Text = "1";
            this.FindControl<TextBox>("KeteranganBox")!.Text = string.Empty;
            this.FindControl<TextBox>("NoFakturBox")!.Text = string.Empty;
            this.FindControl<Button>("SimpanButton")!.IsEnabled = false;
        }
        catch (Exception ex)
        {
            ShowError(status, $"Gagal simpan transaksi: {ex.Message}");
        }
    }

    private static void ShowError(TextBlock block, string message)
    {
        block.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
        block.Text = message;
        block.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
