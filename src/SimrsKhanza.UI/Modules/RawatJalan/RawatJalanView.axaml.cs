using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Sql;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatJalan;

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

    private RawatJalanKunjungan? _selected;
    private SoapItem? _soap;
    private IReadOnlyList<PegawaiPilihan> _pegawai = [];

    private static RawatJalanAkses Akses() =>
        new(AppSession.CurrentUser?.IsAdminUtama == true, AppSession.CurrentUser?.KodeLegacy ?? "-");

    private RawatJalanService Service => new(DatabaseSettings.Load());

    public RawatJalanView()
    {
        InitializeComponent();

        PatientContextMenu.Attach<RawatJalanKunjungan>(this.FindControl<DataGrid>("QueueGrid")!,
            row => new(row.NoRawat, row.NoRekamMedis, row.Pasien),
            request => PatientMenuRequested?.Invoke(this, request));

        var today = DateTime.Today;
        this.FindControl<CalendarDatePicker>("DariPicker")!.SelectedDate = today;
        this.FindControl<CalendarDatePicker>("SampaiPicker")!.SelectedDate = today;
        this.FindControl<ComboBox>("KesadaranCombo")!.ItemsSource = RawatJalanService.Kesadaran;
        ClearForm();
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
            _pegawai = await Service.PegawaiAsync();
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

    private async void OnQueueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = this.FindControl<DataGrid>("QueueGrid")!.SelectedItem as RawatJalanKunjungan;
        this.FindControl<TextBlock>("SelectedPatientText")!.Text = _selected is null
            ? "(belum ada kunjungan dipilih)"
            : $"{_selected.Pasien}  —  No. RM {_selected.NoRekamMedis}  —  No. Rawat {_selected.NoRawat}  —  {_selected.Poli}";
        this.FindControl<Button>("SimpanButton")!.IsEnabled = _selected is not null;
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
            var rows = (await Service.PemeriksaanAsync(noRawat)).Select(SoapItem.From).ToList();
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

    private void ClearForm()
    {
        _soap = null;
        this.FindControl<Button>("UbahButton")!.IsEnabled = false;
        this.FindControl<Button>("HapusButton")!.IsEnabled = false;
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
            await Service.UbahAsync(_soap.Data.NoRawat, _soap.Data.Tanggal, _soap.Data.Jam, input, Akses());
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
            await Service.HapusAsync(soap.Data.NoRawat, soap.Data.Tanggal, soap.Data.Jam, Akses());
            Show(status, "Pemeriksaan dihapus.", error: false);
            await RefreshAfterWriteAsync();
        }
        catch (Exception ex)
        {
            Show(status, $"Gagal hapus: {ex.Message}", error: true);
        }
    }

    private async System.Threading.Tasks.Task RefreshAfterWriteAsync()
    {
        this.FindControl<DataGrid>("SoapGrid")!.SelectedItem = null;
        ClearForm();
        await LoadSoapAsync();
        await LoadQueueAsync();
    }

    private static void Show(TextBlock status, string message, bool error)
    {
        status.Foreground = error ? Avalonia.Media.Brush.Parse("#DC2626") : Avalonia.Media.Brushes.Green;
        status.Text = message;
        status.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
