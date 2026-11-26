using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatInap;

internal sealed class ResumePulangWindow : Window
{
    public ResumePulangWindow(RawatInapListItem visit, AuthenticatedUser user)
    {
        Title = "Resume Pulang Rawat Inap";
        Width = 760;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var service = new ResumePulangRanapService(DatabaseSettings.Load(), visit.NoRawat, visit.NoRekamMedis, user);
        var form = new StackPanel { Spacing = 8, Margin = new Thickness(14), IsEnabled = false };
        var status = Ui.Status();
        var fields = new List<Control>();
        ResumePulangRanap? snapshot = null;
        bool busy = false;
        form.Children.Add(Ui.Hint($"{visit.NamaPasien} · RM {visit.NoRekamMedis} · No. Rawat {visit.NoRawat}"));
        foreach (var item in ResumePulangRanapService.Isian)
        {
            Control field = item.Pilihan is { } options ? new ComboBox { ItemsSource = options } :
                new TextBox { MaxLength = item.Max, AcceptsReturn = item.Max > 100, TextWrapping = TextWrapping.Wrap };
            Tambah(item.Label, field);
            fields.Add(field);
        }
        var tanggalKontrol = new CalendarDatePicker();
        var jamKontrol = new TextBox { PlaceholderText = "HH:mm", Width = 90 };
        Tambah("Jadwal kontrol (opsional)", Ui.Bar(tanggalKontrol, jamKontrol));
        var cetak = Ui.Btn("Simpan PDF", async (_, _) => await JalankanAsync(CetakAsync), false);
        form.Children.Add(Ui.Bar(Ui.Btn("Simpan Resume", async (_, _) => await JalankanAsync(SimpanAsync)),
            cetak, Ui.Btn("Tutup", (_, _) => Close())));
        form.Children.Add(status);
        Content = new ScrollViewer { Content = form };
        Closing += (_, args) => args.Cancel = busy;
        Loaded += async (_, _) =>
        {
            await JalankanAsync(async () =>
            {
                snapshot = await service.MuatAsync();
                for (var i = 0; i < fields.Count; i++)
                {
                    var value = snapshot?.Nilai[i] ?? (i == 0 ? visit.DiagnosaAwal : "");
                    if (fields[i] is TextBox box) box.Text = value;
                    if (fields[i] is ComboBox combo) combo.SelectedItem = value;
                }
                tanggalKontrol.SelectedDate = snapshot?.Kontrol;
                jamKontrol.Text = snapshot?.Kontrol?.ToString("HH:mm");
                cetak.IsEnabled = snapshot is not null && !visit.BelumPulang;
                form.IsEnabled = true;
            });
        };

        void Tambah(string label, Control field)
        {
            Avalonia.Automation.AutomationProperties.SetName(field, label);
            form.Children.Add(new TextBlock { Text = label });
            form.Children.Add(field);
        }

        async Task SimpanAsync()
        {
            DateTime? kontrol = null;
            if (tanggalKontrol.SelectedDate is { } tanggal)
            {
                if (!TimeOnly.TryParseExact(jamKontrol.Text, "HH:mm", out var jam))
                    throw new ArgumentException("Isi jam kontrol dalam format HH:mm.");
                kontrol = DateOnly.FromDateTime(tanggal).ToDateTime(jam);
            }
            else if (!string.IsNullOrWhiteSpace(jamKontrol.Text)) throw new ArgumentException("Isi tanggal kontrol.");
            var values = fields.Select(f => f is TextBox box ? box.Text?.Trim() ?? "" : (f as ComboBox)?.SelectedItem as string ?? "").ToArray();
            await service.SimpanAsync(values, kontrol, snapshot);
            snapshot = await service.MuatAsync();
            cetak.IsEnabled = snapshot is not null && !visit.BelumPulang;
            Ui.Ok(status, "Resume pulang tersimpan.");
        }

        async Task CetakAsync()
        {
            if (visit.BelumPulang || await service.MuatAsync() is not { } resume) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Simpan Resume Pulang", SuggestedFileName = "Resume-Pulang", DefaultExtension = "pdf",
                FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }]
            });
            if (file is null) return;
            QuestPDF.Settings.License = LicenseType.Community;
            var pdf = Document.Create(doc => doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.Header().Column(c =>
                {
                    c.Item().Text("Resume Pulang Rawat Inap").FontSize(17).Bold();
                    c.Item().Text($"{visit.NamaPasien} · RM {visit.NoRekamMedis} · No. Rawat {visit.NoRawat}");
                    c.Item().Text($"Keluar {visit.TglKeluar} {visit.JamKeluar} · Dokter {resume.Dokter}");
                });
                page.Content().PaddingTop(12).Column(c =>
                {
                    c.Spacing(4);
                    if (!string.IsNullOrWhiteSpace(visit.DiagnosaAkhir)) c.Item().Text($"Diagnosis akhir kamar: {visit.DiagnosaAkhir}");
                    foreach (var code in resume.KodeKlinis) c.Item().Text(code);
                    for (var i = 0; i < resume.Nilai.Count; i++)
                        if (resume.Nilai[i].Length > 0)
                            c.Item().Text($"{ResumePulangRanapService.Isian[i].Label}: {resume.Nilai[i]}");
                    if (resume.Kontrol is { } kontrol) c.Item().Text($"Kontrol: {kontrol:dd/MM/yyyy HH:mm}");
                });
            })).GeneratePdf();
            await using var output = await file.OpenWriteAsync();
            if (output.CanSeek) output.SetLength(0);
            await output.WriteAsync(pdf);
            Ui.Ok(status, "PDF resume pulang tersimpan.");
        }

        async Task JalankanAsync(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            var enabled = form.IsEnabled;
            form.IsEnabled = false;
            try { await action(); }
            catch (Exception ex) { Ui.Err(status, ex.Message); }
            finally { if (enabled) form.IsEnabled = true; busy = false; }
        }
    }
}
