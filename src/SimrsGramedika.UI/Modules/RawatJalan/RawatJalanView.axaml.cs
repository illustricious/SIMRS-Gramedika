using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Controls;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatJalan;

/// <summary>Satu baris pemeriksaan_ralan di daftar pemeriksaan kunjungan.</summary>
public sealed record SoapItem(string Tanggal, string Jam, string Tensi, string Penilaian, string Pemeriksa, PemeriksaanRalanInput Data)
{
    public static SoapItem From(Row r)
    {
        var tanggal = r.Dt("tgl_perawatan") ?? DateTime.Today;
        var jam = r["jam_rawat"] is TimeSpan t ? t : TimeSpan.Zero;
        var data = new PemeriksaanRalanInput(r.S("no_rawat"), tanggal.Date, jam, r.S("suhu_tubuh"), r.S("tensi"), r.S("nadi"), r.S("respirasi"),
            r.S("tinggi"), r.S("berat"), r.S("spo2"), r.S("gcs"), r.S("kesadaran"), r.S("keluhan"), r.S("pemeriksaan"), r.S("alergi"),
            r.S("lingkar_perut"), r.S("rtl"), r.S("penilaian"), r.S("instruksi"), r.S("evaluasi"), r.S("nip"));
        var pemeriksa = r.S("nama_pegawai");
        return new(tanggal.ToString("yyyy-MM-dd"), jam.ToString(@"hh\:mm\:ss"), data.Tensi, data.Penilaian,
            pemeriksa.Length > 0 ? pemeriksa : data.Nip, data);
    }
}

public partial class RawatJalanView : UserControl
{
    public event EventHandler? BackRequested;

    /// <summary>Raised from the grid's right-click patient menu — see Modules/Shared/PatientContextMenu.</summary>
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    public void TampilkanAlurKunjungan() =>
        this.FindControl<Button>("AlurKunjunganButton")!.IsVisible = true;

    private void OnAlurKunjunganClick(object? sender, RoutedEventArgs e)
    {
        if (_selected is { } visit)
            PatientMenuRequested?.Invoke(this, new(visit.NoRawat, visit.NoRekamMedis, visit.Pasien, PatientMenuAction.TindakanRawatJalan));
    }

    private RawatJalanKunjungan? _selected;
    private SoapItem? _soap;
    private IReadOnlyList<PegawaiPilihan> _pegawai = [];
    private readonly bool _embedded;

    private static RawatJalanAkses Akses() =>
        RawatJalanAkses.DariPengguna(AppSession.CurrentUser);

    private RawatJalanService Service => new(DatabaseSettings.Load());

    public RawatJalanView() : this(false) { }

    internal RawatJalanView(bool embedded)
    {
        _embedded = embedded;
        InitializeComponent();

        PatientContextMenu.Attach<RawatJalanKunjungan>(this.FindControl<DataGrid>("QueueGrid")!,
            row => new(row.NoRawat, row.NoRekamMedis, row.Pasien),
            request => PatientMenuRequested?.Invoke(this, request));

        var today = DateTime.Today;
        this.FindControl<CalendarDatePicker>("DariPicker")!.SelectedDate = today;
        this.FindControl<CalendarDatePicker>("SampaiPicker")!.SelectedDate = today;
        this.FindControl<ComboBox>("KesadaranCombo")!.ItemsSource = RawatJalanService.Kesadaran;
        this.FindControl<Button>("TemplateButton")!.IsVisible = Akses().TemplateDiizinkan;
        ClearForm();
        // Panel input terlipat memberi seluruh tinggi kepada daftar kunjungan.
        var toggle = this.FindControl<ToggleButton>("InputDataToggle")!;
        toggle.IsCheckedChanged += (_, _) => this.FindControl<Grid>("WorkArea")!.RowDefinitions[0].Height =
            toggle.IsChecked == true ? new GridLength(3, GridUnitType.Star) : GridLength.Auto;
        if (embedded)
        {
            foreach (var name in new[] { "PageHeading", "QueueFilters", "QueueArea", "PageFooter" })
                this.FindControl<Control>(name)!.IsVisible = false;
            // Tertanam di alur kunjungan: hanya form pemeriksaan dan tombolnya.
            var rows = this.FindControl<Grid>("WorkArea")!.RowDefinitions;
            rows[0].Height = new GridLength(1, GridUnitType.Star);
            rows[1].Height = new GridLength(0);
            this.FindControl<ToggleButton>("InputDataToggle")!.IsVisible = false;
        }
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        await LoadPegawaiAsync();
        await LoadQueueAsync();
    }

    public async Task SelectVisitAsync(string noRawat)
    {
        var grid = this.FindControl<DataGrid>("QueueGrid")!;
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        try
        {
            await LoadPegawaiAsync();
            var visit = await Service.KunjunganAsync(noRawat);
            grid.ItemsSource = visit is null ? Array.Empty<RawatJalanKunjungan>() : new[] { visit };
            grid.SelectedItem = visit;
            // Tabel antrean tersembunyi pada tampilan tertanam tidak memunculkan peristiwa pilih.
            if (!ReferenceEquals(_selected, visit)) await PakaiKunjunganAsync(visit);
            this.FindControl<TextBlock>("QueueCountText")!.Text = visit is null ? "Kunjungan Rawat Jalan" : "1 kunjungan";
            if (visit is null) Show(status, "Kunjungan ini bukan kunjungan rawat jalan (poli non-IGD).", error: true);
        }
        catch (Exception)
        {
            Show(status, "Kunjungan tidak dapat dimuat.", error: true);
        }
    }

    private async System.Threading.Tasks.Task LoadPegawaiAsync()
    {
        var combo = this.FindControl<ComboBox>("PegawaiCombo")!;
        try
        {
            _pegawai = await Service.PegawaiAsync(Akses());
            combo.ItemsSource = _pegawai;
            var user = AppSession.CurrentUser;
            if (user is { IsAdminUtama: false })
            {
                // Non-Admin Utama hanya menyimpan atas namanya sendiri (akses.getkode().equals(KdPeg)).
                combo.SelectedItem = _pegawai.FirstOrDefault(p => p.Nik == user.UserId);
                combo.IsEnabled = false;
            }
            else
            {
                combo.IsEnabled = true;
            }
        }
        catch (Exception)
        {
            // Tidak terhubung: Simpan akan gagal dengan pesan jelas.
        }
    }

    private async System.Threading.Tasks.Task LoadQueueAsync()
    {
        var grid = this.FindControl<DataGrid>("QueueGrid")!;
        var count = this.FindControl<TextBlock>("QueueCountText")!;
        var dari = this.FindControl<CalendarDatePicker>("DariPicker")!.SelectedDate ?? DateTime.Today;
        var sampai = this.FindControl<CalendarDatePicker>("SampaiPicker")!.SelectedDate ?? DateTime.Today;
        var selectedNoRawat = _selected?.NoRawat;
        try
        {
            var items = await Service.AntrianAsync(dari, sampai, this.FindControl<TextBox>("CariBox")!.Text);
            grid.ItemsSource = items;
            count.Text = $"{items.Count} kunjungan rawat jalan";
            if (selectedNoRawat is not null) grid.SelectedItem = items.FirstOrDefault(i => i.NoRawat == selectedNoRawat);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<RawatJalanKunjungan>();
            count.Text = "Database tidak terhubung";
        }
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await LoadQueueAsync();

    private async void OnCariKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter) await LoadQueueAsync();
    }

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<TextBox>("CariBox")!.Text = string.Empty;
        await LoadQueueAsync();
    }

    private async void OnQueueSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        await PakaiKunjunganAsync(this.FindControl<DataGrid>("QueueGrid")!.SelectedItem as RawatJalanKunjungan);

    private async Task PakaiKunjunganAsync(RawatJalanKunjungan? kunjungan)
    {
        _selected = kunjungan;
        this.FindControl<Button>("AlurKunjunganButton")!.IsEnabled = _selected is not null;
        this.FindControl<TextBlock>("SelectedPatientText")!.Text = _selected is null
            ? "(belum ada kunjungan dipilih)"
            : $"{_selected.Pasien}  —  No. RM {_selected.NoRekamMedis}  —  No. Rawat {_selected.NoRawat}  —  {_selected.Poli}";
        this.FindControl<Button>("SimpanButton")!.IsEnabled = _selected is not null;
        this.FindControl<Button>("TemplateButton")!.IsEnabled = _selected is not null;
        ClearForm();
        await LoadSoapAsync();
    }

    private async System.Threading.Tasks.Task LoadSoapAsync()
    {
        var grid = this.FindControl<DataGrid>("SoapGrid")!;
        if (_selected is null)
        {
            grid.ItemsSource = Array.Empty<SoapItem>();
            return;
        }
        var noRawat = _selected.NoRawat;
        try
        {
            var rows = (await Service.PemeriksaanAsync(noRawat, Akses())).Select(SoapItem.From).ToList();
            if (_selected?.NoRawat == noRawat) grid.ItemsSource = rows;
        }
        catch (Exception)
        {
            grid.ItemsSource = Array.Empty<SoapItem>();
        }
    }

    private void OnSoapSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _soap = this.FindControl<DataGrid>("SoapGrid")!.SelectedItem as SoapItem;
        this.FindControl<Button>("UbahButton")!.IsEnabled = _soap is not null;
        this.FindControl<Button>("HapusButton")!.IsEnabled = _soap is not null;
        this.FindControl<Button>("CetakSoapButton")!.IsEnabled = _soap is not null;
        if (_soap is null) return;

        var d = _soap.Data;
        this.FindControl<CalendarDatePicker>("TanggalPicker")!.SelectedDate = d.Tanggal;
        SetText("JamBox", d.Jam.ToString(@"hh\:mm\:ss"));
        SetText("TensiBox", d.Tensi); SetText("NadiBox", d.Nadi); SetText("SuhuBox", d.Suhu); SetText("RespirasiBox", d.Respirasi);
        SetText("TinggiBox", d.Tinggi); SetText("BeratBox", d.Berat); SetText("Spo2Box", d.Spo2); SetText("GcsBox", d.Gcs);
        SetText("AlergiBox", d.Alergi); SetText("LingkarPerutBox", d.LingkarPerut); SetText("KeluhanBox", d.Keluhan);
        SetText("PemeriksaanBox", d.Pemeriksaan); SetText("PenilaianBox", d.Penilaian); SetText("RtlBox", d.Rtl);
        SetText("InstruksiBox", d.Instruksi); SetText("EvaluasiBox", d.Evaluasi);
        this.FindControl<ComboBox>("KesadaranCombo")!.SelectedItem = d.Kesadaran;
        var pegawai = this.FindControl<ComboBox>("PegawaiCombo")!;
        if (AppSession.CurrentUser?.IsAdminUtama != false) pegawai.SelectedItem = _pegawai.FirstOrDefault(p => p.Nik == d.Nip);
    }

    private void OnBaruClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<DataGrid>("SoapGrid")!.SelectedItem = null;
        ClearForm();
    }

    private async void OnLimaTerakhirClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        if (_selected is not { } kunjungan || _soap is not null)
        {
            Show(status, "Pilih kunjungan dan buka draf baru sebelum menyalin SOAPIE.", error: true);
            return;
        }
        if (this.FindControl<ComboBox>("PegawaiCombo")!.SelectedItem is not PegawaiPilihan pegawai)
        {
            Show(status, "Pilih dokter atau paramedis terlebih dahulu.", error: true);
            return;
        }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        try
        {
            var akses = Akses();
            var ranap = sender is Button { Tag: "Ranap" };
            var note = await SearchPickerDialog.ShowAsync<SoapItem>(owner, ranap ? "5 SOAPIE Terakhir Rawat Inap" : "5 SOAPIE Terakhir Rawat Jalan", async cari =>
                (await Service.LimaTerakhirAsync(kunjungan.NoRawat, pegawai.Nik, cari, akses, ranap)).Select(SoapItem.From).ToList(),
                item => $"{item.Tanggal} {item.Jam} | {item.Data.NoRawat} | {item.Data.Keluhan.ReplaceLineEndings(" ")}");
            if (note is null || _selected?.NoRawat != kunjungan.NoRawat || _soap is not null) return;
            (string Box, string Value)[] narasi =
            [
                ("KeluhanBox", note.Data.Keluhan), ("PemeriksaanBox", note.Data.Pemeriksaan), ("PenilaianBox", note.Data.Penilaian),
                ("RtlBox", note.Data.Rtl), ("InstruksiBox", note.Data.Instruksi), ("EvaluasiBox", note.Data.Evaluasi),
            ];
            if (narasi.Any(field => !string.IsNullOrWhiteSpace(GetText(field.Box))) &&
                !await Dialogs.ConfirmAsync(this, "Ganti narasi draf", "Ganti keenam isian narasi draf dengan catatan yang dipilih?"))
                return;
            if (_selected?.NoRawat != kunjungan.NoRawat || _soap is not null) return;
            foreach (var field in narasi) SetText(field.Box, field.Value);
            Show(status, "Narasi SOAPIE disalin ke draf. Periksa isinya sebelum menyimpan.", error: false);
        }
        catch (Exception)
        {
            Show(status, "Riwayat SOAPIE tidak dapat dimuat.", error: true);
        }
    }

    private async void OnTemplateClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        if (_selected is not { } kunjungan)
        {
            Show(status, "Pilih kunjungan terlebih dahulu.", error: true);
            return;
        }
        if (_soap is not null)
        {
            Show(status, "Pilih Baru sebelum menerapkan template agar catatan tersimpan tidak tertimpa.", error: true);
            return;
        }
        if (this.FindControl<ComboBox>("PegawaiCombo")!.SelectedItem is not PegawaiPilihan dokter)
        {
            Show(status, "Pilih dokter terlebih dahulu.", error: true);
            return;
        }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        try
        {
            var akses = Akses();
            var template = await SearchPickerDialog.ShowAsync<TemplatePemeriksaanDokter>(owner, "Template Pemeriksaan Dokter",
                cari => Service.TemplatePemeriksaanAsync(dokter.Nik, cari, akses),
                item => $"{item.Nomor} | {item.Keluhan.ReplaceLineEndings(" ")}");
            if (template is null || _selected?.NoRawat != kunjungan.NoRawat || _soap is not null) return;

            var narasi = new[] { "KeluhanBox", "PemeriksaanBox", "PenilaianBox", "RtlBox", "InstruksiBox", "EvaluasiBox" };
            if (narasi.Any(name => !string.IsNullOrWhiteSpace(GetText(name))) &&
                !await Dialogs.ConfirmAsync(this, "Ganti narasi draf", "Ganti keenam isian narasi draf dengan template yang dipilih?"))
                return;
            if (_selected?.NoRawat != kunjungan.NoRawat || _soap is not null) return;

            SetText("KeluhanBox", template.Keluhan);
            SetText("PemeriksaanBox", template.Pemeriksaan);
            SetText("PenilaianBox", template.Penilaian);
            SetText("RtlBox", template.Rencana);
            SetText("InstruksiBox", template.Instruksi);
            SetText("EvaluasiBox", template.Evaluasi);
            Show(status, $"Template {template.Nomor} diterapkan ke draf. Tinjau isian sebelum menyimpan.", error: false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Show(status, ex.Message, error: true);
        }
        catch (Exception)
        {
            Show(status, "Template pemeriksaan tidak dapat dimuat.", error: true);
        }
    }

    private void ClearForm()
    {
        _soap = null;
        this.FindControl<Button>("UbahButton")!.IsEnabled = false;
        this.FindControl<Button>("HapusButton")!.IsEnabled = false;
        this.FindControl<Button>("CetakSoapButton")!.IsEnabled = false;
        var now = DateTime.Now;
        this.FindControl<CalendarDatePicker>("TanggalPicker")!.SelectedDate = now.Date;
        SetText("JamBox", now.ToString("HH:mm:ss"));
        foreach (var name in new[] { "TensiBox", "NadiBox", "SuhuBox", "RespirasiBox", "TinggiBox", "BeratBox", "Spo2Box", "GcsBox", "AlergiBox",
                     "LingkarPerutBox", "KeluhanBox", "PemeriksaanBox", "PenilaianBox", "RtlBox", "InstruksiBox", "EvaluasiBox" })
            SetText(name, "");
        this.FindControl<ComboBox>("KesadaranCombo")!.SelectedItem = RawatJalanService.Kesadaran[0];
    }

    private void SetText(string name, string? value) => this.FindControl<TextBox>(name)!.Text = value ?? string.Empty;
    private string GetText(string name) => this.FindControl<TextBox>(name)!.Text ?? string.Empty;

    private PemeriksaanRalanInput? ReadForm(TextBlock status)
    {
        if (_selected is null) { Show(status, "Pilih kunjungan pasien dari daftar.", error: true); return null; }
        if (!TimeSpan.TryParse(GetText("JamBox").Trim(), out var jam) || jam < TimeSpan.Zero || jam >= TimeSpan.FromDays(1))
        {
            Show(status, "Jam harus berformat HH:mm:ss.", error: true);
            return null;
        }
        var tanggal = this.FindControl<CalendarDatePicker>("TanggalPicker")!.SelectedDate ?? DateTime.Today;
        var pegawai = this.FindControl<ComboBox>("PegawaiCombo")!.SelectedItem as PegawaiPilihan;
        if (pegawai is null)
        {
            Show(status, AppSession.CurrentUser is { IsAdminUtama: false }
                ? $"Akun {AppSession.CurrentUser.UserId} tidak ditemukan di data pegawai, pemeriksaan tidak bisa disimpan."
                : "Dokter/Paramedis masih kosong...!!", error: true);
            return null;
        }
        return new(_selected.NoRawat, tanggal.Date, jam, GetText("SuhuBox"), GetText("TensiBox"), GetText("NadiBox"), GetText("RespirasiBox"),
            GetText("TinggiBox"), GetText("BeratBox"), GetText("Spo2Box"), GetText("GcsBox"),
            this.FindControl<ComboBox>("KesadaranCombo")!.SelectedItem as string ?? RawatJalanService.Kesadaran[0],
            GetText("KeluhanBox"), GetText("PemeriksaanBox"), GetText("AlergiBox"), GetText("LingkarPerutBox"),
            GetText("RtlBox"), GetText("PenilaianBox"), GetText("InstruksiBox"), GetText("EvaluasiBox"), pegawai.Nik);
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        if (ReadForm(status) is not { } input) return;
        try
        {
            await Service.SimpanAsync(input, Akses());
            Show(status, $"Pemeriksaan {_selected!.Pasien} pukul {input.Jam:hh\\:mm} tersimpan.", error: false);
            await RefreshAfterWriteAsync();
        }
        catch (Exception ex)
        {
            Show(status, $"Gagal simpan: {ex.Message}", error: true);
        }
    }

    private async void OnUbahClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        if (_soap is null) { Show(status, "Pilih pemeriksaan yang akan diganti.", error: true); return; }
        if (ReadForm(status) is not { } input) return;
        try
        {
            await Service.UbahAsync(_soap.Data, input, Akses());
            Show(status, "Perubahan pemeriksaan tersimpan.", error: false);
            await RefreshAfterWriteAsync();
        }
        catch (Exception ex)
        {
            Show(status, $"Gagal ubah: {ex.Message}", error: true);
        }
    }

    private async void OnHapusClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        if (_soap is not { } soap) { Show(status, "Pilih pemeriksaan yang akan dihapus.", error: true); return; }
        if (!await Dialogs.ConfirmAsync(this, "Hapus Pemeriksaan", $"Hapus pemeriksaan {soap.Tanggal} {soap.Jam}?")) return;
        try
        {
            await Service.HapusAsync(soap.Data, Akses());
            Show(status, "Pemeriksaan dihapus.", error: false);
            await RefreshAfterWriteAsync();
        }
        catch (Exception ex)
        {
            Show(status, $"Gagal hapus: {ex.Message}", error: true);
        }
    }

    private async void OnCetakSoapClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        if (_soap is not { } soap || _selected is not { } kunjungan)
        {
            Show(status, "Pilih pemeriksaan yang akan dicetak.", error: true);
            return;
        }
        if (TopLevel.GetTopLevel(this) is not { } top) return;

        try
        {
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Simpan SOAPIE sebagai PDF",
                SuggestedFileName = $"SOAPIE-{kunjungan.NoRawat}-{soap.Tanggal}.pdf",
                FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
            });
            if (file is null) return;

            var d = soap.Data;
            QuestPDF.Settings.License = LicenseType.Community;
            using var pdf = new MemoryStream();
            Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10));
                page.Header().Text("CATATAN PEMERIKSAAN RAWAT JALAN (SOAPIE)").Bold().FontSize(15);
                page.Content().PaddingTop(16).Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text($"Pasien: {kunjungan.Pasien}    No. RM: {kunjungan.NoRekamMedis}");
                    column.Item().Text($"No. Rawat: {kunjungan.NoRawat}    Poli: {kunjungan.Poli}");
                    column.Item().Text($"Tanggal/Jam: {soap.Tanggal} {soap.Jam}    Pemeriksa: {soap.Pemeriksa}");
                    column.Item().PaddingTop(8).Text("TANDA VITAL").Bold();
                    column.Item().Text($"Tensi: {d.Tensi}    Nadi: {d.Nadi}    Suhu: {d.Suhu} °C    Respirasi: {d.Respirasi}");
                    column.Item().Text($"Tinggi: {d.Tinggi} cm    Berat: {d.Berat} kg    SpO2: {d.Spo2}%    GCS: {d.Gcs}");
                    column.Item().Text($"Kesadaran: {d.Kesadaran}    Alergi: {d.Alergi}    Lingkar perut: {d.LingkarPerut} cm");
                    Narasi(column, "S — Keluhan", d.Keluhan);
                    Narasi(column, "O — Pemeriksaan", d.Pemeriksaan);
                    Narasi(column, "A — Penilaian / Diagnosa", d.Penilaian);
                    Narasi(column, "P — Rencana tindak lanjut", d.Rtl);
                    Narasi(column, "I — Instruksi", d.Instruksi);
                    Narasi(column, "E — Evaluasi", d.Evaluasi);
                });
                page.Footer().AlignRight().Text("Dicetak dari SIMRS").FontSize(8);
            })).GeneratePdf(pdf);
            pdf.Position = 0;
            await using var target = await file.OpenWriteAsync();
            if (target.CanSeek) target.SetLength(0);
            await pdf.CopyToAsync(target);
            Show(status, "SOAPIE tersimpan sebagai PDF.", error: false);
        }
        catch (Exception)
        {
            Show(status, "PDF SOAPIE gagal dibuat atau disimpan.", error: true);
        }
    }

    private static void Narasi(QuestPDF.Fluent.ColumnDescriptor column, string judul, string? isi)
    {
        column.Item().PaddingTop(8).Text(judul).Bold();
        column.Item().Text(string.IsNullOrWhiteSpace(isi) ? "—" : isi);
    }

    private async System.Threading.Tasks.Task RefreshAfterWriteAsync()
    {
        this.FindControl<DataGrid>("SoapGrid")!.SelectedItem = null;
        ClearForm();
        await LoadSoapAsync();
        if (!_embedded) await LoadQueueAsync();
    }

    private static void Show(TextBlock status, string message, bool error)
    {
        status.Foreground = error ? Avalonia.Media.Brush.Parse("#DC2626") : Avalonia.Media.Brushes.Green;
        status.Text = message;
        status.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
