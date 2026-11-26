using System.Globalization;
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
using SimrsGramedika.UI.Controls;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatInap;

internal sealed class PemeriksaanRanapWindow : Window
{
    private readonly PemeriksaanRanapService _service;
    private readonly RawatInapListItem _visit;
    private readonly AuthenticatedUser _user;
    private readonly DataGrid _history = Ui.Grid(
        new("waktu", "Waktu pemeriksaan", 165), new("pegawai", "Dokter / petugas", 220),
        new("keluhan", "Keluhan", 290), new("asesmen", "Asesmen", 290));
    private readonly CalendarDatePicker _date = new();
    private readonly TextBox _time = new() { PlaceholderText = "HH:mm:ss", Width = 110 };
    private readonly ComboBox _pegawai = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    private readonly ComboBox _kesadaran = new() { ItemsSource = RawatJalanService.Kesadaran };
    private readonly TextBox[] _fields = PemeriksaanRanapService.Isian.Select(f => new TextBox
    {
        MaxLength = f.Maks, AcceptsReturn = f.Maks == 2000,
        TextWrapping = f.Maks == 2000 ? TextWrapping.Wrap : TextWrapping.NoWrap,
        MinHeight = f.Maks == 2000 ? 58 : 0,
    }).ToArray();
    private readonly TextBlock _status = Ui.Status();
    private readonly TextBlock _editorTitle = Ui.Section("Catatan baru");
    private readonly Grid _content;
    private SoapRanap? _editing;
    private bool _busy;

    public PemeriksaanRanapWindow(RawatInapListItem visit, AuthenticatedUser user)
    {
        _visit = visit;
        _user = user;
        _service = new PemeriksaanRanapService(DatabaseSettings.Load(), visit.NoRawat, user);
        Title = "SOAPIE Rawat Inap";
        Width = 1020;
        Height = 740;
        MinWidth = 560;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var form = Ui.Form(
            [("Tanggal dan jam", Ui.Bar(_date, _time)), ("Dokter / petugas", _pegawai), ("Kesadaran", _kesadaran),
             .. PemeriksaanRanapService.Isian.Select((f, i) => (f.Label, (Control)_fields[i]))]);
        var header = new StackPanel
        {
            Spacing = 6,
            Children = { Ui.Title("SOAPIE Rawat Inap"), Ui.Hint($"{visit.NamaPasien}  ·  No. RM {visit.NoRekamMedis}  ·  No. Rawat {visit.NoRawat}") },
        };
        var editor = new StackPanel { Spacing = 8, Children = { _editorTitle, form } };
        var commands = Ui.Bar(
            Ui.Btn("Catatan Baru", (_, _) => IsiForm(null)),
            Ui.Btn("Buka Catatan", (_, _) =>
            {
                if (Ui.Selected<SoapRanap>(_history) is { } note) IsiForm(note);
                else Ui.Err(_status, "Pilih catatan di riwayat.");
            }),
            Ui.Btn("Muat Ulang Riwayat", async (_, _) => await JalankanAsync(MuatRiwayatAsync)),
            Ui.Btn("Simpan SOAPIE PDF", async (_, _) => await JalankanAsync(SimpanPdfAsync)),
            Ui.Btn("5 SOAPIE Terakhir", async (_, _) => await JalankanAsync(PilihRiwayatTerakhirAsync)),
            Ui.Btn("Salin Narasi Terpilih", async (_, _) => await JalankanAsync(SalinRiwayatTerpilihAsync)));
        var footer = new StackPanel
        {
            Children =
            {
                Ui.Bar(Ui.Btn("Simpan", async (_, _) => await JalankanAsync(SimpanAsync)),
                    Ui.Btn("Hapus Catatan Terbuka", async (_, _) => await JalankanAsync(HapusAsync)),
                    Ui.Btn("Tutup", (_, _) => Close())),
                _status,
            },
        };
        _content = new Grid { RowDefinitions = new("Auto,180,Auto,*,Auto"), RowSpacing = 8, Margin = new Thickness(16) };
        var controls = new Control[] { header, _history, commands, new ScrollViewer { Content = editor }, footer };
        for (var i = 0; i < controls.Length; i++)
        {
            Grid.SetRow(controls[i], i);
            _content.Children.Add(controls[i]);
        }
        Content = _content;
        _pegawai.IsEnabled = user.IsAdminUtama;
        Closing += (_, e) => e.Cancel = _busy;
        IsiForm(null);
        Opened += async (_, _) => await JalankanAsync(async () =>
        {
            _pegawai.ItemsSource = await _service.PegawaiAsync();
            IsiForm(null);
            await MuatRiwayatAsync();
        });
    }

    private void IsiForm(SoapRanap? note)
    {
        _editing = note;
        _editorTitle.Text = note is null ? "Catatan baru" : $"Catatan {note.Waktu:dd/MM/yyyy HH:mm:ss} · {note.Nip} {note.Nama}";
        var waktu = note?.Waktu ?? DateTime.Now;
        _date.SelectedDate = waktu.Date;
        _time.Text = waktu.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _kesadaran.SelectedItem = note?.Kesadaran ?? RawatJalanService.Kesadaran[0];
        _pegawai.SelectedItem = (_pegawai.ItemsSource as IEnumerable<PegawaiPilihan>)?.FirstOrDefault(p =>
            string.Equals(p.Nik, _user.IsAdminUtama ? note?.Nip : _user.UserId, StringComparison.OrdinalIgnoreCase));
        for (var i = 0; i < _fields.Length; i++) _fields[i].Text = note?.Isian[i] ?? "";
    }

    private async Task MuatRiwayatAsync()
    {
        var notes = await _service.DaftarAsync();
        Ui.FillRows(_history, notes.Select(n => new GridRow(
            [$"{n.Waktu:dd/MM/yyyy HH:mm:ss}", $"{n.Nip} {n.Nama}", n.Isian[9], n.Isian[11]], n)));
        Ui.Info(_status, notes.Count == 0 ? "Belum ada pemeriksaan untuk kunjungan ini." : $"{notes.Count} pemeriksaan");
    }

    private async Task PilihRiwayatTerakhirAsync()
    {
        PastikanCatatanBaru();
        if (_pegawai.SelectedItem is not PegawaiPilihan pegawai)
            throw new ArgumentException("Pilih dokter atau petugas terlebih dahulu.");

        var note = await SearchPickerDialog.ShowAsync(this, "5 SOAPIE Terakhir",
            cari => _service.LimaTerakhirAsync(pegawai.Nik, cari),
            n => $"{n.Waktu:dd/MM/yyyy HH:mm:ss} | {n.NoRawat} | {Ringkas(n.Isian[PemeriksaanRanapService.AwalNarasi])}");
        if (note is not null) await SalinNarasiAsync(note);
    }

    private async Task SimpanPdfAsync()
    {
        if (!_user.HasPermission(PemeriksaanRanapService.Izin)) throw new InvalidOperationException("Akun tidak memiliki hak pemeriksaan rawat inap.");
        var selected = Ui.Selected<SoapRanap>(_history) ?? throw new InvalidOperationException("Pilih catatan di riwayat untuk disimpan sebagai PDF.");
        var note = (await _service.DaftarAsync()).SingleOrDefault(n => n.Waktu == selected.Waktu && n.Nip == selected.Nip)
            ?? throw new InvalidOperationException("Catatan tidak ditemukan lagi. Muat ulang riwayat.");
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Simpan SOAPIE Rawat Inap",
            SuggestedFileName = "SOAPIE-Rawat-Inap.pdf",
            FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
        });
        if (file is null) return;
        QuestPDF.Settings.License = LicenseType.Community;
        using var pdf = new MemoryStream();
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Text("SOAPIE Rawat Inap").Bold().FontSize(15);
            page.Content().PaddingTop(12).Column(col =>
            {
                col.Item().Text($"Nama: {_visit.NamaPasien}   No. RM: {_visit.NoRekamMedis}");
                col.Item().Text($"No. Rawat: {_visit.NoRawat}   Waktu: {note.Waktu:dd/MM/yyyy HH:mm:ss}");
                col.Item().PaddingBottom(10).Text($"Dokter / petugas: {note.Nip} {note.Nama}");
                col.Item().PaddingBottom(5).Text($"Kesadaran: {note.Kesadaran}");
                for (var i = 0; i < PemeriksaanRanapService.Isian.Count; i++)
                {
                    var text = note.Isian[i];
                    if (!string.IsNullOrWhiteSpace(text))
                        col.Item().PaddingBottom(5).Text($"{PemeriksaanRanapService.Isian[i].Label}: {text}");
                }
            });
        })).GeneratePdf(pdf);
        pdf.Position = 0;
        await using var target = await file.OpenWriteAsync();
        if (target.CanSeek) target.SetLength(0);
        await pdf.CopyToAsync(target);
        Ui.Ok(_status, "SOAPIE tersimpan sebagai PDF.");
    }

    private Task SalinRiwayatTerpilihAsync()
    {
        PastikanCatatanBaru();
        var note = Ui.Selected<SoapRanap>(_history) ?? throw new InvalidOperationException("Pilih catatan di riwayat.");
        return SalinNarasiAsync(note);
    }

    private void PastikanCatatanBaru()
    {
        if (_editing is not null)
            throw new InvalidOperationException("Pilih Catatan Baru untuk menyalin narasi SOAPIE.");
    }

    private async Task SalinNarasiAsync(SoapRanap note)
    {
        var start = PemeriksaanRanapService.AwalNarasi;
        if (_fields.Skip(start).Any(field => !string.IsNullOrWhiteSpace(field.Text)) &&
            !await Dialogs.ConfirmAsync(this, "Ganti narasi draf", "Ganti keenam isian narasi draf dengan isi catatan yang dipilih?"))
            return;

        for (var i = start; i < _fields.Length; i++)
            _fields[i].Text = note.Isian[i];
        Ui.Info(_status, $"Narasi dari {note.Waktu:dd/MM/yyyy HH:mm:ss} disalin ke draf. Periksa isinya sebelum menyimpan.");
    }

    private static string Ringkas(string teks)
    {
        var satuBaris = teks.ReplaceLineEndings(" ").Trim();
        return satuBaris.Length <= 90 ? satuBaris : satuBaris[..90] + "…";
    }

    private async Task SimpanAsync()
    {
        if (_date.SelectedDate is not { } tanggal ||
            !TimeOnly.TryParseExact(_time.Text, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var jam))
            throw new ArgumentException("Isi tanggal dan jam dengan format HH:mm:ss.");
        if (_pegawai.SelectedItem is not PegawaiPilihan pegawai)
            throw new ArgumentException(_user.IsAdminUtama ? "Pilih dokter atau petugas." : "NIK akun tidak ditemukan di data pegawai.");
        var note = new SoapRanap(_visit.NoRawat, tanggal.Date + jam.ToTimeSpan(), pegawai.Nik,
            _kesadaran.SelectedItem as string ?? "", _fields.Select(x => x.Text ?? "").ToArray());
        await _service.SimpanAsync(note, _editing);
        IsiForm(null);
        await MuatSesudahPerubahanAsync("Pemeriksaan tersimpan.");
    }

    private async Task HapusAsync()
    {
        if (_editing is not { } note) throw new InvalidOperationException("Buka catatan yang akan dihapus.");
        if (!await Dialogs.ConfirmAsync(this, "Hapus SOAPIE", $"Hapus pemeriksaan {note.Waktu:dd/MM/yyyy HH:mm:ss} untuk No. Rawat {_visit.NoRawat}?"))
            return;
        await _service.HapusAsync(note);
        IsiForm(null);
        await MuatSesudahPerubahanAsync("Pemeriksaan dihapus.");
    }

    private async Task MuatSesudahPerubahanAsync(string message)
    {
        try
        {
            await MuatRiwayatAsync();
            Ui.Ok(_status, message);
        }
        catch (Exception)
        {
            _history.ItemsSource = null;
            Ui.Err(_status, $"{message} Riwayat belum dapat dimuat ulang.");
        }
    }

    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _content.IsEnabled = false;
        try { await action(); }
        catch (ArgumentException ex) { Ui.Err(_status, ex.Message); }
        catch (InvalidOperationException ex) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Pemeriksaan gagal diproses. Periksa koneksi dan muat ulang riwayat."); }
        finally
        {
            _busy = false;
            _content.IsEnabled = true;
        }
    }
}
