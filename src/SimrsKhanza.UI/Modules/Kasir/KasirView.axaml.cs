using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Kasir;

public partial class KasirView : UserControl
{
    public event EventHandler? BackRequested;

    private KasirBillItem? _current;

    public KasirView() => InitializeComponent();

    public System.Threading.Tasks.Task ReloadAsync() => System.Threading.Tasks.Task.CompletedTask;

    /// <summary>Called from the right-click "Billing/Pembayaran Pasien" patient menu (Modules/Shared/PatientContextMenu) — prefills No.Rawat and searches immediately.</summary>
    public async System.Threading.Tasks.Task SearchForAsync(string noRawat)
    {
        this.FindControl<TextBox>("NoRawatBox")!.Text = noRawat;
        await CariAsync(noRawat);
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e)
        => await CariAsync(this.FindControl<TextBox>("NoRawatBox")!.Text?.Trim());

    private async System.Threading.Tasks.Task CariAsync(string? noRawat)
    {
        var notFound = this.FindControl<TextBlock>("NotFoundText")!;
        var panel = this.FindControl<StackPanel>("BillPanel")!;

        if (string.IsNullOrWhiteSpace(noRawat))
        {
            panel.IsVisible = false;
            notFound.IsVisible = true;
            notFound.Text = "Masukkan No. Rawat dulu.";
            return;
        }

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            _current = await new KasirRepository(db).GetBillAsync(noRawat);

            if (_current is null)
            {
                panel.IsVisible = false;
                notFound.IsVisible = true;
                notFound.Text = $"No. Rawat '{noRawat}' tidak ditemukan.";
                return;
            }

            notFound.IsVisible = false;
            panel.IsVisible = true;
            this.FindControl<TextBlock>("PatientText")!.Text = $"{_current.NamaPasien}  —  No.RM {_current.NoRekamMedis}  —  No.Rawat {_current.NoRawat}";
            this.FindControl<TextBlock>("BiayaRegText")!.Text = $"Rp {_current.BiayaReg:N0}";
            this.FindControl<TextBlock>("BiayaKamarText")!.Text = $"Rp {_current.BiayaKamar:N0}";
            this.FindControl<TextBlock>("TotalText")!.Text = $"Rp {_current.Total:N0}";
            this.FindControl<TextBlock>("StatusBayarText")!.Text = _current.StatusBayar ?? "-";

            var bayarButton = this.FindControl<Button>("BayarButton")!;
            bayarButton.IsEnabled = _current.StatusBayar != "Sudah Bayar";
            bayarButton.Content = _current.StatusBayar == "Sudah Bayar" ? "✔ Sudah Lunas" : "💰 Tandai Lunas";
        }
        catch (Exception ex)
        {
            panel.IsVisible = false;
            notFound.IsVisible = true;
            notFound.Text = $"Gagal mengambil tagihan: {ex.Message}";
        }
    }

    private async void OnBayarClick(object? sender, RoutedEventArgs e)
    {
        if (_current is null) return;

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new KasirRepository(db).MarkPaidAsync(_current.NoRawat);

            this.FindControl<TextBlock>("StatusBayarText")!.Text = "Sudah Bayar";
            var bayarButton = this.FindControl<Button>("BayarButton")!;
            bayarButton.IsEnabled = false;
            bayarButton.Content = "✔ Sudah Lunas";
        }
        catch (Exception ex)
        {
            this.FindControl<TextBlock>("NotFoundText")!.Text = $"Gagal menandai lunas: {ex.Message}";
            this.FindControl<TextBlock>("NotFoundText")!.IsVisible = true;
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
