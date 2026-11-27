using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatJalan;

internal sealed class ResumeRalanView : UserControl
{
    private readonly ResumeRalanService _service;
    private readonly TextBox _keluhan = Narasi(), _perjalanan = Narasi(), _penunjang = Narasi(), _lab = Narasi(), _obat = Narasi();
    private readonly ComboBox _kondisi = new() { ItemsSource = new[] { "Hidup", "Meninggal" }, SelectedIndex = -1 };
    private readonly TextBlock _kode = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = Ui.Status();
    private readonly StackPanel _layout = new() { Spacing = 7, Margin = new Thickness(12) };
    private readonly Button _simpan, _cetak;
    private ResumeRalan? _snapshot;
    private bool _busy;

    public ResumeRalanView(string noRawat, string noRm, AuthenticatedUser user)
    {
        _service = new(DatabaseSettings.Load(), noRawat, noRm, user);
        _simpan = Ui.Btn("Simpan Resume", async (_, _) => await JalankanAsync(SimpanAsync), false);
        _cetak = Ui.Btn("Cetak Resume", async (_, _) => await JalankanAsync(CetakAsync), false);
        _cetak.IsEnabled = false;
        _layout.Children.Add(Ui.Title("Resume Medis Rawat Jalan"));
        _layout.Children.Add(Ui.Bar(_simpan, _cetak, Ui.Btn("Muat Ulang", async (_, _) => await JalankanAsync(async () =>
        {
            if (await Dialogs.ConfirmAsync(this, "Muat ulang resume", "Muat ulang data dan buang perubahan yang belum disimpan?"))
                await MuatAsync();
        }))));
        TambahIsian("Keluhan utama", _keluhan);
        TambahIsian("Perjalanan penyakit", _perjalanan);
        TambahIsian("Pemeriksaan penunjang", _penunjang);
        TambahIsian("Hasil laboratorium", _lab);
        TambahIsian("Kondisi pulang", _kondisi);
        TambahIsian("Obat pulang", _obat);
        _layout.Children.Add(Ui.Section("Diagnosis dan prosedur kunjungan"));
        _layout.Children.Add(_kode);
        _layout.Children.Add(_status);
        Content = new ScrollViewer { Content = _layout };
        Loaded += async (_, _) => { if (!_simpan.IsEnabled) await JalankanAsync(MuatAsync); };
    }

    private void TambahIsian(string label, Control field)
    {
        Avalonia.Automation.AutomationProperties.SetName(field, label);
        _layout.Children.Add(new TextBlock { Text = label });
        _layout.Children.Add(field);
    }

    private async Task MuatAsync()
    {
        _simpan.IsEnabled = false;
        var (resume, kode) = await _service.MuatAsync();
        _snapshot = resume;
        _keluhan.Text = resume?.Isi.Keluhan ?? "";
        _perjalanan.Text = resume?.Isi.Perjalanan ?? "";
        _penunjang.Text = resume?.Isi.Penunjang ?? "";
        _lab.Text = resume?.Isi.Laborat ?? "";
        _obat.Text = resume?.Isi.Obat ?? "";
        _kondisi.SelectedItem = resume?.Isi.Kondisi;
        _kode.Text = kode.Count == 0 ? "Belum ada diagnosis atau prosedur pada kunjungan." :
            string.Join(Environment.NewLine, kode.Select(c => $"{(c.Prosedur ? "ICD-9" : "ICD-10")} {c.Prioritas} · {c.Kode} · {c.Nama}"));
        _simpan.IsEnabled = true;
        _cetak.IsEnabled = resume is not null;
    }

    private async Task CetakAsync()
    {
        // Yang dicetak adalah isi tersimpan, jadi perubahan di layar harus disimpan dulu.
        if (_snapshot is null || BacaIsian() != Rapikan(_snapshot.Isi))
            throw new InvalidOperationException("Simpan resume sebelum mencetak.");
        var isi = await _service.CetakAsync();
        try
        {
            var path = ResumeRalanReport.GeneratePdf(isi, await KopInstansi.MuatAsync(DatabaseSettings.Load()));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { Ui.Err(_status, $"Gagal membuat atau membuka resume: {ex.Message}"); }
    }

    private IsiResumeRalan BacaIsian() => Rapikan(new(_keluhan.Text ?? "", _perjalanan.Text ?? "",
        _penunjang.Text ?? "", _lab.Text ?? "", _kondisi.SelectedItem as string ?? "", _obat.Text ?? ""));

    private static IsiResumeRalan Rapikan(IsiResumeRalan d) => new(d.Keluhan.Trim(), d.Perjalanan.Trim(),
        d.Penunjang.Trim(), d.Laborat.Trim(), d.Kondisi, d.Obat.Trim());

    private async Task SimpanAsync()
    {
        await _service.SimpanAsync(BacaIsian(), _snapshot);
        Ui.Ok(_status, "Resume tersimpan.");
        try { await MuatAsync(); }
        catch (Exception) { Ui.Ok(_status, "Resume tersimpan. Muat ulang sebelum mengubah lagi."); }
    }

    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _layout.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Resume gagal diproses. Periksa koneksi database."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }

    private static TextBox Narasi() => new() { AcceptsReturn = true, MinHeight = 55, TextWrapping = TextWrapping.Wrap };
}
