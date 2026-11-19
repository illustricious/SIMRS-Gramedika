using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Kasir;

public partial class KasirView : UserControl
{
    public event EventHandler? BackRequested;
    private KasirBillItem? _current;
    private bool _loading;
    private bool _paying;

    public KasirView() => InitializeComponent();

    public Task ReloadAsync() => Task.CompletedTask;

    public async Task SearchForAsync(string noRawat)
    {
        this.FindControl<TextBox>("NoRawatBox")!.Text = noRawat;
        await CariAsync(noRawat);
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await CariInputAsync();

    private async void OnNoRawatKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await CariInputAsync();
    }

    private Task CariInputAsync() => CariAsync(this.FindControl<TextBox>("NoRawatBox")!.Text?.Trim());

    private static bool BolehAkses(bool ranap)
    {
        var user = AppSession.CurrentUser;
        return ranap ? user?.HasPermission("billing_ranap") == true
            : user?.HasPermission("kasir_ralan") == true || user?.HasPermission("billing_ralan") == true;
    }

    private async Task CariAsync(string? noRawat)
    {
        if (_loading || _paying) return;
        _current = null;
        var panel = this.FindControl<StackPanel>("BillPanel")!;
        panel.IsVisible = false;
        if (string.IsNullOrWhiteSpace(noRawat)) { Status("Masukkan No. Rawat dulu."); return; }

        var cari = this.FindControl<Button>("CariButton")!;
        cari.IsEnabled = false;
        _loading = true;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new KasirRepository(db);
            var bill = await repo.GetBillAsync(noRawat);
            if (bill is null) { Status($"No. Rawat '{noRawat}' tidak ditemukan."); return; }
            if (!BolehAkses(bill.Ranap)) { Status("Akun ini tidak memiliki hak akses billing untuk jenis kunjungan ini."); return; }

            var lunas = bill.StatusBayar == "Sudah Bayar";
            this.FindControl<Button>("BuktiButton")!.IsVisible = BisaCetakBukti(bill);
            var akun = this.FindControl<ComboBox>("AkunBayarBox")!;
            var akunRow = this.FindControl<StackPanel>("AkunBayarRow")!;
            akunRow.IsVisible = bill.Ranap && !lunas && bill.AlasanBelumBisaBayar is null;
            var daftarAkun = akunRow.IsVisible ? await repo.AkunBayarRanapAsync() : [];
            akun.ItemsSource = daftarAkun;
            akun.SelectedIndex = -1;

            _current = bill;
            panel.IsVisible = true;
            this.FindControl<TextBlock>("PatientText")!.Text = $"{bill.NamaPasien} · No. RM {bill.NoRekamMedis} · No. Rawat {bill.NoRawat}";
            this.FindControl<DataGrid>("RincianGrid")!.ItemsSource = bill.Rincian;
            this.FindControl<TextBlock>("TotalText")!.Text = Rupiah(bill.Total);
            this.FindControl<TextBlock>("StatusBayarText")!.Text = bill.NoNota is { } nota
                ? $"{bill.StatusBayar ?? "-"} · Nota {nota}" : bill.StatusBayar ?? "-";
            var bayar = this.FindControl<Button>("BayarButton")!;
            bayar.IsEnabled = !lunas && bill.AlasanBelumBisaBayar is null;
            bayar.Content = lunas ? "Sudah Lunas" : bill.Ranap ? "Catat Pelunasan" : "Tandai Lunas";
            var catatan = bill.AlasanBelumBisaBayar;
            if (akunRow.IsVisible && daftarAkun.Count == 0)
            {
                catatan = "Belum ada akun bayar tanpa biaya admin yang memiliki rekening.";
                bayar.IsEnabled = false;
            }
            Status(catatan);
        }
        catch (Exception ex)
        {
            Status($"Gagal memuat tagihan: {ex.Message}");
        }
        finally
        {
            _loading = false;
            cari.IsEnabled = true;
        }
    }

    private async void OnBayarClick(object? sender, RoutedEventArgs e)
    {
        if (_paying || _current is not { } bill || !BolehAkses(bill.Ranap)) return;
        var akun = this.FindControl<ComboBox>("AkunBayarBox")!.SelectedItem as string;
        if (bill.Ranap && akun is null) { Status("Pilih akun bayar terlebih dahulu."); return; }

        _paying = true;
        var tombol = this.FindControl<Button>("BayarButton")!;
        tombol.IsEnabled = false;
        var selesai = false;
        try
        {
            var pesan = bill.Ranap
                ? $"Catat pembayaran {Rupiah(bill.Total)} untuk {bill.NamaPasien} melalui {akun}?"
                : $"Tandai tagihan {bill.NamaPasien} senilai {Rupiah(bill.Total)} sebagai lunas?";
            if (!await Dialogs.ConfirmAsync(this, "Konfirmasi Pelunasan", pesan)) return;
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new KasirRepository(db);
            if (bill.Ranap)
                await repo.BayarRanapAsync(bill, akun!, AppSession.Kode);
            else
                await repo.MarkPaidAsync(bill.NoRawat);
            selesai = true;
        }
        catch (Exception ex)
        {
            Status($"Gagal mencatat pelunasan: {ex.Message}");
        }
        finally
        {
            _paying = false;
            tombol.IsEnabled = !selesai;
        }
        if (selesai) await CariAsync(bill.NoRawat);
    }

    private static string Rupiah(decimal nilai) => $"Rp {nilai.ToString("#,0.####", CultureInfo.GetCultureInfo("id-ID"))}";

    private static bool BisaCetakBukti(KasirBillItem bill) =>
        bill.Ranap && bill.StatusBayar == "Sudah Bayar" && !string.IsNullOrWhiteSpace(bill.NoNota)
        && bill.RincianTersimpan && bill.Rincian.Count > 0;

    private async void OnBuktiClick(object? sender, RoutedEventArgs e)
    {
        if (_paying || _loading || _current is not { } bill || !BisaCetakBukti(bill) || !BolehAkses(ranap: true)
            || TopLevel.GetTopLevel(this) is not { } top) return;
        var tombol = this.FindControl<Button>("BuktiButton")!;
        if (!tombol.IsEnabled) return;
        tombol.IsEnabled = false;
        try
        {
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Simpan Bukti Pelunasan",
                SuggestedFileName = "Bukti-Pelunasan-" + string.Join("-", bill.NoNota!.Split(Path.GetInvalidFileNameChars())),
                DefaultExtension = "pdf",
                FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }]
            });
            if (file is null) return;
            QuestPDF.Settings.License = LicenseType.Community;
            var pdf = Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.Header().Column(header =>
                {
                    header.Spacing(4);
                    header.Item().Text("Bukti Pelunasan Rawat Inap").FontSize(18).Bold();
                    header.Item().Text($"No. Nota {bill.NoNota}   |   No. Rawat {bill.NoRawat}");
                    header.Item().Text($"{bill.NamaPasien}   |   No. RM {bill.NoRekamMedis}");
                });
                page.Content().PaddingTop(14).Column(body =>
                {
                    body.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                        });
                        table.Header(header =>
                        {
                            foreach (var title in new[] { "Jenis", "Rincian", "Jumlah Tagihan" })
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(title).Bold();
                        });
                        foreach (var row in bill.Rincian)
                        {
                            table.Cell().Padding(5).Text(row.Jenis);
                            table.Cell().Padding(5).Text(row.Nama);
                            table.Cell().Padding(5).AlignRight().Text(Rupiah(row.Total));
                        }
                    });
                    body.Item().PaddingTop(10).AlignRight().Text($"Total tagihan lunas  {Rupiah(bill.Total)}").Bold();
                });
            })).GeneratePdf();
            await using var output = await file.OpenWriteAsync();
            if (output.CanSeek) output.SetLength(0);
            await output.WriteAsync(pdf);
            Status("Bukti pelunasan PDF tersimpan.");
        }
        catch (Exception ex) { Status($"Gagal menyimpan PDF: {ex.Message}"); }
        finally { tombol.IsEnabled = true; }
    }

    private void Status(string? text)
    {
        var status = this.FindControl<TextBlock>("NotFoundText")!;
        status.Text = text;
        status.IsVisible = !string.IsNullOrWhiteSpace(text);
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
