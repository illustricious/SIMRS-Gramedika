using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatInap;

public partial class RawatInapView : UserControl
{
    public event EventHandler? BackRequested;

    /// <summary>Raised from the grid's right-click patient menu — see Modules/Shared/PatientContextMenu.</summary>
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    private enum ActionMode { None, Masuk, Pulang, Pindah, Ganti }

    private static readonly StatusPulang[] DischargeOptions =
    [
        StatusPulang.AtasPersetujuanDokter, StatusPulang.Sembuh, StatusPulang.Membaik, StatusPulang.Sehat,
        StatusPulang.Rujuk, StatusPulang.Aps, StatusPulang.AtasPermintaanSendiri, StatusPulang.PulangPaksa,
        StatusPulang.Meninggal, StatusPulang.Isoman, StatusPulang.Lainnya,
    ];

    private static readonly string[] StatusBayarOptions = ["Semua", "Sudah Bayar", "Belum Bayar"];

    private ActionMode _mode = ActionMode.None;
    private bool _ready;
    private IReadOnlyList<Penjab> _penjab = Array.Empty<Penjab>();

    public RawatInapView()
    {
        InitializeComponent();

        var grid = Grid();
        this.FindControl<Button>("BillingButton")!.IsVisible = AppSession.CurrentUser?.HasPermission("billing_ranap") == true;
        PatientContextMenu.Attach<RawatInapListItem>(grid,
            row => new(row.NoRawat, row.NoRekamMedis, row.NamaPasien, IsInpatient: true),
            request => PatientMenuRequested?.Invoke(this, request));

        var statusBayar = this.FindControl<ComboBox>("StatusBayarCombo")!;
        statusBayar.ItemsSource = StatusBayarOptions;
        statusBayar.SelectedIndex = 0;

        var statusPulang = this.FindControl<ComboBox>("StatusPulangCombo")!;
        statusPulang.ItemsSource = DischargeOptions.Select(s => s.ToLabel()).ToList();
        statusPulang.SelectedIndex = 0;

        foreach (var name in new[] { "MasukFromPicker", "MasukToPicker", "PulangFromPicker", "PulangToPicker" })
            this.FindControl<CalendarDatePicker>(name)!.SelectedDate = DateTime.Today;

        _ready = true;
    }

    private DataGrid Grid() => this.FindControl<DataGrid>("RawatInapGrid")!;

    private RawatInapListItem? Selected => Grid().SelectedItem as RawatInapListItem;

    /// <summary>Called from the right-click "Kamar Inap" patient menu — opens the Masuk panel with No.Rawat prefilled.</summary>
    public async Task PrefillAdmitAsync(string noRawat)
    {
        ShowAction(ActionMode.Masuk);
        this.FindControl<TextBox>("AdmitNoRawatBox")!.Text = noRawat;
        await LoadKamarKosongAsync("KamarCombo", noRawat);
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var grid = Grid();
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var items = await new RawatInapRepository(db).GetDaftarAsync(BuildFilter());
            grid.ItemsSource = items;
            this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {items.Count}";
        }
        catch (Exception ex)
        {
            grid.ItemsSource = new List<RawatInapListItem>();
            this.FindControl<TextBlock>("RecordText")!.Text = "Record : 0";
            ShowStatus($"Gagal memuat daftar rawat inap: {ex.Message}", isError: true);
        }
    }

    private RawatInapFilter BuildFilter()
    {
        var mode = this.FindControl<RadioButton>("ModeMasuk")!.IsChecked == true ? RawatInapMode.TanggalMasuk
                 : this.FindControl<RadioButton>("ModePulang")!.IsChecked == true ? RawatInapMode.Pulang
                 : RawatInapMode.BelumPulang;

        var (from, to) = mode == RawatInapMode.Pulang
            ? (PickDate("PulangFromPicker"), PickDate("PulangToPicker"))
            : (PickDate("MasukFromPicker"), PickDate("MasukToPicker"));

        StatusBayar? statusBayar = this.FindControl<ComboBox>("StatusBayarCombo")!.SelectedIndex switch
        {
            1 => StatusBayar.SudahBayar,
            2 => StatusBayar.BelumBayar,
            _ => null,
        };

        return new RawatInapFilter(
            mode, from, to, statusBayar,
            Text("NoRawatFilterBox"), Text("NoRmFilterBox"), Text("NamaFilterBox"), Text("KamarFilterBox"), Text("KeywordBox"));
    }

    private DateOnly PickDate(string name)
        => DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>(name)!.SelectedDate ?? DateTime.Today);

    private string? Text(string name)
    {
        var t = this.FindControl<TextBox>(name)!.Text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private async void OnFilterChanged(object? sender, RoutedEventArgs e)
    {
        if (_ready) await ReloadAsync();
    }

    private async void OnFilterKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await ReloadAsync();
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        foreach (var name in new[] { "NoRawatFilterBox", "NoRmFilterBox", "NamaFilterBox", "KamarFilterBox", "KeywordBox" })
            this.FindControl<TextBox>(name)!.Text = string.Empty;
        _ready = false;
        this.FindControl<ComboBox>("StatusBayarCombo")!.SelectedIndex = 0;
        this.FindControl<RadioButton>("ModeBelumPulang")!.IsChecked = true;
        _ready = true;
        await ReloadAsync();
    }

    // ---- action panel (Masuk / Pulang / Pindah / Ganti) ----

    private void ShowAction(ActionMode mode)
    {
        _mode = mode;
        HideStatus();
        this.FindControl<Border>("ActionPanel")!.IsVisible = mode != ActionMode.None;
        this.FindControl<StackPanel>("MasukFields")!.IsVisible = mode == ActionMode.Masuk;
        this.FindControl<StackPanel>("PulangFields")!.IsVisible = mode == ActionMode.Pulang;
        this.FindControl<StackPanel>("PindahFields")!.IsVisible = mode == ActionMode.Pindah;
        this.FindControl<StackPanel>("GantiFields")!.IsVisible = mode == ActionMode.Ganti;

        var title = this.FindControl<TextBlock>("ActionTitle")!;
        var subtitle = this.FindControl<TextBlock>("ActionSubtitle")!;
        var ok = this.FindControl<Button>("ActionOkButton")!;
        var s = Selected;
        var who = s is null ? "" : $"{s.NamaPasien} — No. Rawat {s.NoRawat} — Kamar {s.Kamar}";

        switch (mode)
        {
            case ActionMode.Masuk:
                title.Text = "Masuk — Admit Pasien ke Kamar Inap";
                subtitle.Text = "";
                ok.Content = "🛏 Admit";
                break;
            case ActionMode.Pulang:
                title.Text = "Pulang — Pulangkan Pasien & Hitung Biaya Kamar";
                subtitle.Text = who;
                ok.Content = "🚪 Pulangkan";
                break;
            case ActionMode.Pindah:
                title.Text = "Pindah — Pindah Kamar";
                subtitle.Text = who;
                ok.Content = "🔀 Pindahkan";
                break;
            case ActionMode.Ganti:
                title.Text = "Ganti — Jenis Bayar / Diagnosa";
                subtitle.Text = who;
                ok.Content = "💾 Simpan Perubahan";
                break;
        }
    }

    private async void OnMasukClick(object? sender, RoutedEventArgs e)
    {
        ShowAction(ActionMode.Masuk);
        if (Text("AdmitNoRawatBox") is { } noRawat)
            await LoadKamarKosongAsync("KamarCombo", noRawat);
    }

    private void OnPulangClick(object? sender, RoutedEventArgs e)
    {
        if (!RequireSelected(mustBeActive: true)) return;
        ShowAction(ActionMode.Pulang);
        this.FindControl<TextBox>("DiagnosaAkhirBox")!.Text = Selected!.DiagnosaAkhir;
    }

    private async void OnPindahClick(object? sender, RoutedEventArgs e)
    {
        if (!RequireSelected(mustBeActive: true)) return;
        ShowAction(ActionMode.Pindah);
        await LoadKamarKosongAsync("PindahKamarCombo", Selected!.NoRawat);
    }

    private async void OnCariKamarClick(object? sender, RoutedEventArgs e)
        => await CariKamarMasukAsync();

    private async void OnAdmitNoRawatKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await CariKamarMasukAsync();
    }

    private void OnAdmitNoRawatChanged(object? sender, TextChangedEventArgs e)
    {
        if (this.FindControl<ComboBox>("KamarCombo") is { } combo) combo.ItemsSource = null;
    }

    private async Task CariKamarMasukAsync()
    {
        if (Text("AdmitNoRawatBox") is not { } noRawat)
        {
            ShowStatus("Isi No. Rawat untuk melihat tarif sesuai cara bayar.", isError: true);
            return;
        }
        await LoadKamarKosongAsync("KamarCombo", noRawat);
    }

    private void OnBillingClick(object? sender, RoutedEventArgs e)
    {
        if (!RequireSelected(mustBeActive: false) || Selected is not { } s) return;
        PatientMenuRequested?.Invoke(this, new(s.NoRawat, s.NoRekamMedis, s.NamaPasien, PatientMenuAction.BillingPembayaran));
    }

    private async void OnGantiClick(object? sender, RoutedEventArgs e)
    {
        if (!RequireSelected(mustBeActive: false)) return;
        ShowAction(ActionMode.Ganti);
        var s = Selected!;
        this.FindControl<TextBox>("GantiDiagnosaAwalBox")!.Text = s.DiagnosaAwal;
        this.FindControl<TextBox>("GantiDiagnosaAkhirBox")!.Text = s.DiagnosaAkhir;

        var combo = this.FindControl<ComboBox>("GantiPenjabCombo")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            _penjab = await new RegistrasiRepository(db).GetPenjabAsync();
            combo.ItemsSource = _penjab.Select(p => p.NamaPenanggungJawab).ToList();
            combo.SelectedItem = _penjab.FirstOrDefault(p => p.KodePenjab == s.KodePenjab)?.NamaPenanggungJawab;
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal memuat jenis bayar: {ex.Message}", isError: true);
        }
    }

    private bool RequireSelected(bool mustBeActive)
    {
        var s = Selected;
        if (s is null)
        {
            ShowStatus("Pilih dulu satu baris pasien di tabel.", isError: true);
            return false;
        }
        if (mustBeActive && !s.BelumPulang)
        {
            ShowStatus("Baris ini sudah pulang/pindah — pilih pasien yang masih dirawat (Stts.Pulang \"-\").", isError: true);
            return false;
        }
        return true;
    }

    private async System.Threading.Tasks.Task LoadKamarKosongAsync(string comboName, string? noRawat = null)
    {
        var combo = this.FindControl<ComboBox>(comboName)!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new RawatInapRepository(db);
            var kamar = noRawat is null ? await repo.GetAvailableKamarAsync()
                : await repo.GetAvailableKamarForVisitAsync(noRawat);
            if (comboName == "KamarCombo" && noRawat != Text("AdmitNoRawatBox")) return;
            combo.ItemsSource = kamar;
            combo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(KamarTersediaItem.Label));
            if (kamar.Count > 0) combo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            combo.ItemsSource = new List<KamarTersediaItem>();
            ShowStatus($"Gagal memuat kamar kosong: {ex.Message}", isError: true);
        }
    }

    private void OnActionCancelClick(object? sender, RoutedEventArgs e) => ShowAction(ActionMode.None);

    private async void OnActionOkClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new RawatInapRepository(db);
            string message;

            switch (_mode)
            {
                case ActionMode.Masuk:
                {
                    var noRawat = Text("AdmitNoRawatBox");
                    var kamar = this.FindControl<ComboBox>("KamarCombo")!.SelectedItem as KamarTersediaItem;
                    if (noRawat is null || kamar is null)
                    {
                        ShowStatus("No. Rawat dan Kamar wajib diisi.", isError: true);
                        return;
                    }
                    await repo.AdmitAsync(noRawat, kamar.KodeKamar, Text("DiagnosaAwalBox"));
                    message = $"Pasien {noRawat} berhasil di-admit ke kamar {kamar.KodeKamar}.";
                    this.FindControl<TextBox>("AdmitNoRawatBox")!.Text = string.Empty;
                    this.FindControl<TextBox>("DiagnosaAwalBox")!.Text = string.Empty;
                    break;
                }
                case ActionMode.Pulang:
                {
                    var s = Selected!;
                    var idx = this.FindControl<ComboBox>("StatusPulangCombo")!.SelectedIndex;
                    var total = await repo.DischargeAsync(s.Key, DischargeOptions[Math.Max(0, idx)], Text("DiagnosaAkhirBox"));
                    message = $"{s.NamaPasien} dipulangkan. Biaya kamar: Rp {total:N0} (lihat Kasir untuk total tagihan).";
                    break;
                }
                case ActionMode.Pindah:
                {
                    var s = Selected!;
                    var kamar = this.FindControl<ComboBox>("PindahKamarCombo")!.SelectedItem as KamarTersediaItem;
                    if (kamar is null)
                    {
                        ShowStatus("Pilih kamar tujuan.", isError: true);
                        return;
                    }
                    var total = await repo.PindahKamarAsync(s.Key, kamar.KodeKamar);
                    message = $"{s.NamaPasien} dipindah ke kamar {kamar.KodeKamar}. Biaya kamar sebelumnya: Rp {total:N0}.";
                    break;
                }
                case ActionMode.Ganti:
                {
                    var s = Selected!;
                    var penjabName = this.FindControl<ComboBox>("GantiPenjabCombo")!.SelectedItem as string;
                    var penjab = _penjab.FirstOrDefault(p => p.NamaPenanggungJawab == penjabName);
                    if (penjab is not null && penjab.KodePenjab != s.KodePenjab)
                        await repo.GantiJenisBayarAsync(s.NoRawat, penjab.KodePenjab);
                    await repo.GantiDiagnosaAsync(s.Key, Text("GantiDiagnosaAwalBox"), Text("GantiDiagnosaAkhirBox"));
                    message = $"Data {s.NamaPasien} diperbarui.";
                    break;
                }
                default:
                    return;
            }

            ShowAction(ActionMode.None);
            await ReloadAsync();
            ShowStatus(message, isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal: {ex.Message}", isError: true);
        }
    }

    // ---- cetak ----

    private void OnCetakClick(object? sender, RoutedEventArgs e)
    {
        var items = (Grid().ItemsSource as IEnumerable<RawatInapListItem>)?.ToList() ?? new List<RawatInapListItem>();
        if (items.Count == 0)
        {
            ShowStatus("Tidak ada data yang bisa dicetak.", isError: true);
            return;
        }

        try
        {
            var path = RawatInapReport.GeneratePdf(items, "Data Kamar Inap Pasien");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal membuat/membuka PDF: {ex.Message}", isError: true);
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
