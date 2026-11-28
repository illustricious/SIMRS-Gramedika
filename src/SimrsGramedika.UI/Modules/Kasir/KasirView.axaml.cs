using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Kasir;

public partial class KasirView : UserControl
{
    public event EventHandler? BackRequested;
    private KasirBillItem? _current;
    private bool _loading;
    private bool _paying;

    public KasirView()
    {
        InitializeComponent();
        PerbaruiTombolPiutang();
    }

    public Task ReloadAsync()
    {
        PerbaruiTombolPiutang();
        return Task.CompletedTask;
    }

    private void PerbaruiTombolPiutang() => this.FindControl<Button>("PiutangButton")!.IsVisible =
        AppSession.CurrentUser?.HasPermission(PiutangPasienService.Izin) == true;

    private async void OnPiutangClick(object? sender, RoutedEventArgs e)
    {
        if (_loading || _paying || AppSession.CurrentUser is not { } user || TopLevel.GetTopLevel(this) is not Window owner) return;
        var noRawat = _current is { StatusPiutang: not null } bill ? bill.NoRawat : null;
        await new PiutangWindow(user, noRawat).ShowDialog(owner);
        if (_current is { } tampil) await CariAsync(tampil.NoRawat);
    }

    private void OnPiutangCheck(object? sender, RoutedEventArgs e)
    {
        var piutang = this.FindControl<CheckBox>("PiutangCheck")!.IsChecked == true;
        this.FindControl<StackPanel>("PiutangIsian")!.IsVisible = piutang;
        this.FindControl<Button>("BayarButton")!.Content = piutang ? "Catat Piutang" : "Catat Pelunasan";
    }

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
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new KasirRepository(db);
            var bill = await repo.GetBillAsync(noRawat);
            if (bill is null) { Status($"No. Rawat '{noRawat}' tidak ditemukan."); return; }
            if (!BolehAkses(bill.Ranap)) { Status("Akun ini tidak memiliki hak akses billing untuk jenis kunjungan ini."); return; }

            var lunas = bill.StatusBayar == "Sudah Bayar";
            this.FindControl<Button>("BuktiButton")!.IsVisible = BisaCetakBukti(bill);
            this.FindControl<Button>("BatalNotaButton")!.IsVisible = !string.IsNullOrWhiteSpace(bill.NoNota)
                && AppSession.CurrentUser is { } petugas && KasirRepository.BolehBatalkanNota(petugas, bill.Ranap);
            var akun = this.FindControl<ComboBox>("AkunBayarBox")!;
            var akunRow = this.FindControl<StackPanel>("AkunBayarRow")!;
            var bisaBayar = bill.StatusBayar == "Belum Bayar" && bill.AlasanBelumBisaBayar is null;
            akunRow.IsVisible = bisaBayar && (bill.HarusDibayar > 0);
            var daftarAkun = akunRow.IsVisible ? await repo.AkunBayarRanapAsync() : [];
            akun.ItemsSource = daftarAkun;
            akun.SelectedIndex = -1;
            var daftarPiutang = akunRow.IsVisible ? await repo.AkunPiutangAsync() : [];
            var akunPiutang = this.FindControl<ComboBox>("AkunPiutangBox")!;
            akunPiutang.ItemsSource = daftarPiutang;
            akunPiutang.SelectedIndex = -1;
            this.FindControl<CheckBox>("PiutangCheck")!.IsChecked = false;
            this.FindControl<TextBox>("UangMukaBox")!.Text = "";
            this.FindControl<CalendarDatePicker>("TempoPicker")!.SelectedDate = DateTime.Today;
            this.FindControl<StackPanel>("PiutangRow")!.IsVisible = daftarPiutang.Count > 0;

            _current = bill;
            panel.IsVisible = true;
            this.FindControl<TextBlock>("PatientText")!.Text = $"{bill.NamaPasien} · No. RM {bill.NoRekamMedis} · No. Rawat {bill.NoRawat}";
            this.FindControl<DataGrid>("RincianGrid")!.ItemsSource = bill.Rincian;
            this.FindControl<TextBlock>("TotalText")!.Text = Rupiah(bill.Total);
            var status = bill.NoNota is { } nota ? $"{bill.StatusBayar ?? "-"} · Nota {nota}" : bill.StatusBayar ?? "-";
            this.FindControl<TextBlock>("StatusBayarText")!.Text =
                bill.StatusPiutang is { } piutang ? $"{status} · Piutang {piutang}" : status;
            TampilkanDeposit(bill);
            TampilkanTombolPenyesuaian(bill);
            var bayar = this.FindControl<Button>("BayarButton")!;
            bayar.IsEnabled = bisaBayar;
            bayar.Content = bill.StatusPiutang is not null ? "Tercatat sebagai Piutang" : lunas ? "Sudah Lunas" : "Catat Pelunasan";
            var catatan = bill.AlasanBelumBisaBayar;
            if (akunRow.IsVisible && daftarAkun.Count == 0)
            {
                catatan = "Belum ada akun bayar tanpa biaya admin yang memiliki rekening.";
                bayar.IsEnabled = daftarPiutang.Count > 0; // piutang tanpa uang muka tidak memakai akun bayar
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
        if (_paying || _current is not { AlasanBelumBisaBayar: null, StatusBayar: "Belum Bayar" } bill || !BolehAkses(bill.Ranap) || AppSession.CurrentUser is not { } user) return;
        var akun = this.FindControl<ComboBox>("AkunBayarBox")!.SelectedItem as string;
        KasirPiutang? piutang = null;
        if (this.FindControl<CheckBox>("PiutangCheck")!.IsChecked == true)
        {
            var teks = this.FindControl<TextBox>("UangMukaBox")!.Text?.Trim();
            var uangMuka = 0m;
            if (!string.IsNullOrEmpty(teks) && !decimal.TryParse(teks, NumberStyles.Number, CultureInfo.GetCultureInfo("id-ID"), out uangMuka))
            { Status("Uang muka harus angka (desimal memakai koma)."); return; }
            if (this.FindControl<ComboBox>("AkunPiutangBox")!.SelectedItem is not string akunPiutang
                || this.FindControl<CalendarDatePicker>("TempoPicker")!.SelectedDate is not { } tempo)
            { Status("Pilih akun piutang dan jatuh tempo."); return; }
            piutang = new(akunPiutang, uangMuka, DateOnly.FromDateTime(tempo));
        }
        if (akun is null && bill.HarusDibayar > 0 && (piutang is null || piutang.UangMuka > 0))
        { Status("Pilih akun bayar terlebih dahulu."); return; }

        _paying = true;
        var tombol = this.FindControl<Button>("BayarButton")!;
        tombol.IsEnabled = false;
        var selesai = false;
        try
        {
            if (!await Dialogs.ConfirmAsync(this, piutang is null ? "Konfirmasi Pelunasan" : "Konfirmasi Piutang",
                    PesanKonfirmasi(bill, akun, piutang))) return;
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new KasirRepository(db);
            if (bill.Ranap)
                await repo.BayarRanapAsync(bill, akun ?? "", user, piutang: piutang);
            else
                await repo.BayarRalanAsync(bill, akun ?? "", user, piutang: piutang);
            selesai = true;
        }
        catch (Exception ex)
        {
            Status($"Gagal menyimpan pembayaran: {ex.Message}");
        }
        finally
        {
            _paying = false;
            tombol.IsEnabled = !selesai;
        }
        if (selesai) await CariAsync(bill.NoRawat);
    }

    private static string PesanKonfirmasi(KasirBillItem bill, string? akun, KasirPiutang? piutang)
    {
        var deposit = bill.Deposit > 0 ? $" Tagihan {Rupiah(bill.Total)}, deposit {Rupiah(bill.Deposit)}." : "";
        if (piutang is not null)
            return $"Simpan tagihan {Rupiah(bill.Total)} untuk {bill.NamaPasien}?{deposit} Uang muka {Rupiah(piutang.UangMuka)}" +
                (piutang.UangMuka > 0 ? $" melalui {akun}" : "") +
                $", piutang {Rupiah(bill.HarusDibayar - piutang.UangMuka)} pada {piutang.AkunPiutang}, jatuh tempo {piutang.JatuhTempo:dd/MM/yyyy}.";
        if (bill.HarusDibayar > 0)
            return $"Catat pembayaran {Rupiah(bill.HarusDibayar)} untuk {bill.NamaPasien} melalui {akun}?{deposit}";
        var sisa = bill.SisaDeposit > 0
            ? $" Sisa deposit {Rupiah(bill.SisaDeposit)} dicatat sebagai pengembalian; serahkan kepada pasien." : "";
        return $"Selesaikan tagihan {bill.NamaPasien} tanpa pembayaran baru?{deposit}{sisa}";
    }

    private void TampilkanDeposit(KasirBillItem bill)
    {
        var teks = this.FindControl<TextBlock>("DepositText")!;
        teks.IsVisible = this.FindControl<TextBlock>("DepositLabel")!.IsVisible = bill.Deposit > 0;
        teks.Text = bill.SisaDeposit > 0
            ? $"{Rupiah(bill.Deposit)} · sisa dikembalikan {Rupiah(bill.SisaDeposit)}"
            : $"{Rupiah(bill.Deposit)} · kekurangan dibayar {Rupiah(bill.HarusDibayar)}";
    }

    private void TampilkanTombolPenyesuaian(KasirBillItem bill)
    {
        var bisaDiubah = bill is { StatusBayar: "Belum Bayar", RincianTersimpan: false, NoNota: null, NoRawatIbu: null };
        var tambahan = Boleh(JenisPenyesuaian.Tambahan);
        var potongan = Boleh(JenisPenyesuaian.Potongan);
        this.FindControl<Button>("TambahanButton")!.IsVisible = tambahan;
        this.FindControl<Button>("PotonganButton")!.IsVisible = potongan;
        this.FindControl<StackPanel>("PenyesuaianRow")!.IsVisible = bisaDiubah && (tambahan || potongan);

        static bool Boleh(JenisPenyesuaian jenis) =>
            AppSession.CurrentUser?.HasPermission(KasirRepository.IzinPenyesuaian(jenis)) == true;
    }

    private async void OnTambahanClick(object? sender, RoutedEventArgs e) => await UbahBiayaAsync(JenisPenyesuaian.Tambahan);

    private async void OnPotonganClick(object? sender, RoutedEventArgs e) => await UbahBiayaAsync(JenisPenyesuaian.Potongan);

    private async Task UbahBiayaAsync(JenisPenyesuaian jenis)
    {
        if (_loading || _paying || _current is not { } bill || AppSession.CurrentUser is not { } user
            || TopLevel.GetTopLevel(this) is not Window owner) return;
        await new PenyesuaianWindow(user, bill.NoRawat, bill.NamaPasien, jenis).ShowDialog(owner);
        await CariAsync(bill.NoRawat);
    }

    private async void OnBatalNotaClick(object? sender, RoutedEventArgs e)
    {
        if (_paying || _loading || _current is not { NoNota.Length: > 0 } bill || AppSession.CurrentUser is not { } user) return;
        _paying = true;
        var tombol = this.FindControl<Button>("BatalNotaButton")!;
        tombol.IsEnabled = false;
        RencanaBatalNota? dibatalkan = null;
        try
        {
            var settings = DatabaseSettings.Load();
            RencanaBatalNota rencana;
            using (var db = SimrsGramedikaDbContextFactory.Create(settings))
                rencana = await new KasirRepository(db).RencanaBatalNotaAsync(bill.NoRawat, user);
            if (rencana.NoNota != bill.NoNota) { Status("Nota berubah. Muat ulang tagihan."); return; }
            if (!await Dialogs.ConfirmAsync(this, "Batalkan Nota", PesanBatalNota(rencana))) return;
            using (var db = SimrsGramedikaDbContextFactory.Create(settings))
                await new KasirRepository(db).BatalkanNotaAsync(rencana, user);
            dibatalkan = rencana;
        }
        catch (Exception ex)
        {
            Status($"Nota tidak dibatalkan: {ex.Message}");
        }
        finally
        {
            _paying = false;
            tombol.IsEnabled = true;
        }
        if (dibatalkan is null) return;
        await CariAsync(bill.NoRawat);
        Status($"Nota {dibatalkan.NoNota} dibatalkan. {_current?.AlasanBelumBisaBayar}".TrimEnd());
    }

    private static string PesanBatalNota(RencanaBatalNota r)
    {
        var pesan = $"Batalkan nota {(r.Piutang ? "piutang" : "pelunasan")} {r.NoNota} atas nama {r.NamaPasien}? " +
            $"Nota, rincian billing{(r.Piutang ? " dan piutangnya" : "")} dihapus{(r.NoJurnal is null ? "" : ", jurnalnya dibalik")}, " +
            "dan tagihan terbuka lagi.";
        if (r.Tunai > 0) pesan += $" Uang {Rupiah(r.Tunai)} yang tercatat diterima harus diselesaikan dengan pasien.";
        if (r.Deposit > 0) pesan += $" Deposit {Rupiah(r.Deposit)} tetap tercatat untuk tagihan berikutnya.";
        if (r.PengembalianDeposit > 0)
            pesan += $" Catatan pengembalian deposit {Rupiah(r.PengembalianDeposit)} dihapus; pastikan uangnya diperhitungkan lagi.";
        return pesan;
    }

    private static string Rupiah(decimal nilai) => $"Rp {nilai.ToString("#,0.####", CultureInfo.GetCultureInfo("id-ID"))}";

    private static bool BisaCetakBukti(KasirBillItem bill) =>
        bill.StatusBayar == "Sudah Bayar" && !string.IsNullOrWhiteSpace(bill.NoNota)
        && bill.RincianTersimpan && bill.Rincian.Count > 0;

    private async void OnBuktiClick(object? sender, RoutedEventArgs e)
    {
        if (_paying || _loading || _current is not { } bill || !BisaCetakBukti(bill) || !BolehAkses(bill.Ranap)
            || TopLevel.GetTopLevel(this) is not { } top) return;
        var tombol = this.FindControl<Button>("BuktiButton")!;
        if (!tombol.IsEnabled) return;
        tombol.IsEnabled = false;
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var kuitansi = await new KasirRepository(db).MuatKuitansiAsync(bill.NoRawat);
            if (kuitansi is null || kuitansi.Tagihan.NoNota != bill.NoNota || kuitansi.Tagihan.Ranap != bill.Ranap)
            {
                Status("Kuitansi berubah atau data pembayaran tidak lengkap. Muat ulang tagihan.");
                return;
            }
            bill = kuitansi.Tagihan;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Simpan Kuitansi Pelunasan",
                SuggestedFileName = (kuitansi.Piutang.Count > 0 ? "Nota-" : "Kuitansi-") + string.Join("-", bill.NoNota!.Split(Path.GetInvalidFileNameChars())),
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
                    var jenis = kuitansi.Piutang.Count > 0 ? "Nota Piutang" : "Kuitansi Pelunasan";
                    header.Item().Text($"{jenis} {(bill.Ranap ? "Rawat Inap" : "Rawat Jalan")}").FontSize(18).Bold();
                    header.Item().Text($"{(kuitansi.Piutang.Count > 0 ? "No. Nota" : "No. Kuitansi")} {bill.NoNota}   |   No. Rawat {bill.NoRawat}");
                    header.Item().Text($"{(kuitansi.Piutang.Count > 0 ? "Dicatat" : "Dibayar")} {kuitansi.WaktuBayar.ToString("dd MMMM yyyy HH:mm:ss", CultureInfo.GetCultureInfo("id-ID"))}");
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
                    var judulTotal = kuitansi.Piutang.Count > 0 ? "Total tagihan" : "Total tagihan lunas";
                    body.Item().PaddingTop(10).AlignRight().Text($"{judulTotal}  {Rupiah(bill.Total)}").Bold();
                    if (bill.Deposit > 0)
                        body.Item().AlignRight().Text($"Deposit diperhitungkan  {Rupiah(bill.Deposit)}");
                    if (bill.SisaDeposit > 0)
                        body.Item().AlignRight().Text($"Sisa deposit dikembalikan  {Rupiah(bill.SisaDeposit)}");
                    foreach (var pembayaran in kuitansi.Pembayaran)
                    {
                        body.Item().AlignRight().Text($"{pembayaran.AkunBayar}  {Rupiah(pembayaran.Jumlah)}");
                        if (pembayaran.BiayaAdmin != 0)
                            body.Item().AlignRight().Text($"Biaya admin  {Rupiah(pembayaran.BiayaAdmin)}");
                    }
                    foreach (var piutang in kuitansi.Piutang)
                        body.Item().AlignRight().Text($"Piutang {piutang.AkunPiutang}  {Rupiah(piutang.Jumlah)} " +
                            $"(jatuh tempo {piutang.JatuhTempo:dd/MM/yyyy}, sisa saat dicetak {Rupiah(piutang.Sisa)})");
                });
            })).GeneratePdf();
            await using var output = await file.OpenWriteAsync();
            if (output.CanSeek) output.SetLength(0);
            await output.WriteAsync(pdf);
            Status("PDF tersimpan.");
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
