using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.Data.MenuA;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatInap;

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
    private RawatInapListItem? _actionPatient;
    private bool _saving;
    private bool _ready;

    public RawatInapView()
    {
        InitializeComponent();

        var grid = Grid();
        this.FindControl<Button>("BillingButton")!.IsVisible = AppSession.CurrentUser?.HasPermission("billing_ranap") == true;
        this.FindControl<Button>("FormulirButton")!.IsVisible = Modules.RekamMedis.PilihFormulir.Ada(AppSession.CurrentUser);
        this.FindControl<Button>("ResepDokterButton")!.IsVisible = AppSession.CurrentUser?.HasPermission("resep_dokter") == true;
        this.FindControl<Button>("PermintaanLabButton")!.IsVisible = AppSession.CurrentUser?.HasPermission("permintaan_lab") == true;
        this.FindControl<Button>("PermintaanRadButton")!.IsVisible =
            AppSession.CurrentUser?.HasPermission("permintaan_radiologi") == true;
        this.FindControl<Button>("PemeriksaanButton")!.IsVisible =
            AppSession.CurrentUser?.HasPermission(PemeriksaanRanapService.Izin) == true;
        this.FindControl<Button>("TindakanButton")!.IsVisible =
            this.FindControl<Button>("ObgynButton")!.IsVisible =
            AppSession.CurrentUser?.HasPermission(TindakanRanapInputService.Izin) == true;
        this.FindControl<Button>("PulangButton")!.IsVisible = this.FindControl<Button>("HapusDataSalahButton")!.IsVisible =
            this.FindControl<Button>("RanapGabungButton")!.IsVisible =
            AppSession.CurrentUser?.HasPermission("kamar_inap") == true;
        this.FindControl<Button>("OperasiButton")!.IsVisible = AppSession.CurrentUser?.HasPermission(OperasiService.Izin) == true;
        this.FindControl<Button>("ResumePulangButton")!.IsVisible = AppSession.CurrentUser?.HasPermission(ResumePulangRanapService.Izin) == true;
        this.FindControl<Button>("BuatResepPulangButton")!.IsVisible =
            this.FindControl<Button>("ResepPulangButton")!.IsVisible =
            AppSession.CurrentUser?.HasPermission(ResepPulangRepository.IzinPermintaan) == true;
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
        if (_saving) return;
        ShowAction(ActionMode.Masuk);
        this.FindControl<TextBox>("AdmitNoRawatBox")!.Text = noRawat;
        await LoadKamarKosongAsync("KamarCombo", noRawat);
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var grid = Grid();
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
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

    private void ShowAction(ActionMode mode, RawatInapListItem? patient = null)
    {
        _mode = mode;
        _actionPatient = patient;
        HideStatus();
        this.FindControl<Border>("ActionPanel")!.IsVisible = mode != ActionMode.None;
        this.FindControl<StackPanel>("MasukFields")!.IsVisible = mode == ActionMode.Masuk;
        this.FindControl<StackPanel>("PulangFields")!.IsVisible = mode == ActionMode.Pulang;
        this.FindControl<StackPanel>("PindahFields")!.IsVisible = mode == ActionMode.Pindah;
        this.FindControl<StackPanel>("GantiFields")!.IsVisible = mode == ActionMode.Ganti;

        var title = this.FindControl<TextBlock>("ActionTitle")!;
        var subtitle = this.FindControl<TextBlock>("ActionSubtitle")!;
        var ok = this.FindControl<Button>("ActionOkButton")!;
        var s = patient;
        var who = s is null ? "" : $"{s.NamaPasien} — No. Rawat {s.NoRawat} — Kamar {s.Kamar}";

        switch (mode)
        {
            case ActionMode.Masuk:
                title.Text = "Masuk — Admit Pasien ke Kamar Inap";
                subtitle.Text = "";
                ok.Content = "Admit";
                break;
            case ActionMode.Pulang:
                title.Text = "Pulang — Pulangkan Pasien & Hitung Biaya Kamar";
                subtitle.Text = who;
                ok.Content = "Pulangkan";
                break;
            case ActionMode.Pindah:
                title.Text = "Pindah — Pindah Kamar";
                subtitle.Text = who;
                ok.Content = "Pindahkan";
                break;
            case ActionMode.Ganti:
                title.Text = "Ganti — Jenis Bayar / Diagnosa";
                subtitle.Text = who;
                ok.Content = "Simpan Perubahan";
                break;
        }
    }

    private async void OnMasukClick(object? sender, RoutedEventArgs e)
    {
        if (_saving) return;
        ShowAction(ActionMode.Masuk);
        if (Text("AdmitNoRawatBox") is { } noRawat)
            await LoadKamarKosongAsync("KamarCombo", noRawat);
    }

    private void OnPulangClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser?.HasPermission("kamar_inap") != true || !RequireSelected(mustBeActive: true) || !RequireBarisIbu()) return;
        var patient = Selected!;
        ShowAction(ActionMode.Pulang, patient);
        this.FindControl<TextBox>("DiagnosaAkhirBox")!.Text = patient.DiagnosaAkhir;
        this.FindControl<ComboBox>("StatusPulangCombo")!.SelectedIndex = -1;
        var now = DateTime.Now;
        this.FindControl<CalendarDatePicker>("TanggalKeluarPicker")!.SelectedDate = now.Date;
        this.FindControl<TextBox>("JamKeluarBox")!.Text = now.ToString("HH:mm:ss");
    }

    private async void OnResumePulangClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(ResumePulangRanapService.Izin)
            || !RequireSelected(mustBeActive: false) || Selected is not { } visit || TopLevel.GetTopLevel(this) is not Window owner) return;
        if (visit.StatusPulang == StatusPulang.PindahKamar.ToLabel())
        {
            ShowStatus("Pilih segmen aktif atau segmen pulang terakhir untuk membuka resume.", isError: true);
            return;
        }
        await new ResumePulangWindow(visit, user).ShowDialog(owner);
    }

    private async void OnBuatResepPulangClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(ResepPulangRepository.IzinPermintaan)
            || !RequireSelected(mustBeActive: false) || Selected is not { } visit
            || TopLevel.GetTopLevel(this) is not Window owner) return;
        if (visit.StatusPulang == StatusPulang.PindahKamar.ToLabel())
        {
            ShowStatus("Pilih segmen aktif atau segmen pulang terakhir untuk membuat permintaan.", isError: true);
            return;
        }

        var form = new PermintaanResepPulangWindow(visit, user);
        await form.ShowDialog(owner);
        if (form.Saved) ShowStatus($"Permintaan {form.Nomor} sudah masuk antrean Farmasi.", isError: false);
    }

    private async void OnResepPulangClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(ResepPulangRepository.IzinPermintaan)
            || !RequireSelected(mustBeActive: false) || Selected is not { } visit
            || TopLevel.GetTopLevel(this) is not Window owner) return;

        var button = this.FindControl<Button>("ResepPulangButton")!;
        button.IsEnabled = false;
        try
        {
            var repo = new ResepPulangRepository(DatabaseSettings.Load());
            var grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Single };
            async Task ReloadAsync()
            {
                var rows = await repo.RiwayatPermintaanAsync(user, visit.NoRawat);
                grid.ItemsSource = rows.Select(row => new StatusObatPulang(
                    row.S("no_permintaan"), row.S("tgl_permintaan"), row.S("jam"), row.S("status") == "Belum",
                    row["waktu"] is not null ? "Sudah diserahkan"
                        : row.S("status") == "Sudah" ? "Sudah divalidasi" : "Menunggu validasi",
                    row.S("nama_brng"), row.S("jml"), row.S("dosis"),
                    row.S("waktu"), row.S("petugas"), row.S("penerima"), row["waktu"] is not null)).ToList();
            }

            await ReloadAsync();
            var title = $"Status Resep Pulang · {visit.NamaPasien} · {visit.NoRawat}";
            foreach (var (header, property) in new[]
            {
                ("No. Permintaan", "Nomor"), ("Tanggal", "Tanggal"), ("Status Farmasi", "Status"),
                ("Obat", "Obat"), ("Jumlah", "Jumlah"), ("Aturan Pakai", "Dosis"),
                ("Waktu Serah", "Waktu"), ("Petugas", "Petugas"), ("Penerima", "Penerima"),
            })
                grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Avalonia.Data.Binding(property) });

            var status = Ui.Status();
            var batal = new Button { Content = "Batalkan Permintaan", IsEnabled = false };
            var ubah = new Button { Content = "Ubah Permintaan", IsEnabled = false, Classes = { "accent" } };
            var tutup = new Button { Content = "Tutup" };
            var bukti = new Button { Content = "Simpan Bukti Serah PDF", IsEnabled = false };
            var actions = Ui.Bar(ubah, batal, bukti, tutup, status);
            actions.Margin = new Avalonia.Thickness(0, 8, 0, 0);
            var root = new DockPanel { Margin = new Avalonia.Thickness(14) };
            DockPanel.SetDock(actions, Dock.Bottom);
            var pasien = Ui.Hint($"{visit.NamaPasien} · RM {visit.NoRekamMedis} · {visit.NoRawat}");
            pasien.Margin = new Avalonia.Thickness(0, 0, 0, 8);
            DockPanel.SetDock(pasien, Dock.Top);
            root.Children.Add(pasien);
            root.Children.Add(actions);
            root.Children.Add(grid);
            var dialog = new Window
            {
                Title = title, Width = 840, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = root,
            };
            tutup.Click += (_, _) => dialog.Close();
            dialog.Closing += (_, args) => args.Cancel = !grid.IsEnabled;
            void UpdateButtons()
            {
                ubah.IsEnabled = batal.IsEnabled = grid.IsEnabled && grid.SelectedItem is StatusObatPulang { Menunggu: true };
                bukti.IsEnabled = grid.IsEnabled && grid.SelectedItem is StatusObatPulang { Diserahkan: true };
            }

            bukti.Click += async (_, _) =>
            {
                if (grid.SelectedItem is not StatusObatPulang { Diserahkan: true } item) return;
                grid.IsEnabled = false;
                UpdateButtons();
                try
                {
                    var data = await repo.MuatBuktiSerahAsync(user, visit.NoRawat, item.Nomor);
                    var file = await dialog.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Simpan Bukti Serah Terima Obat Pulang",
                        SuggestedFileName = $"Bukti-Serah-{item.Nomor}", DefaultExtension = "pdf",
                        FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
                    });
                    if (file is null) return;
                    QuestPDF.Settings.License = LicenseType.Community;
                    var pdf = Document.Create(doc => doc.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(36);
                        page.Content().Column(c =>
                        {
                            c.Spacing(8);
                            c.Item().Text("Bukti Serah Terima Obat Pulang").FontSize(18).Bold();
                            c.Item().Text($"Pasien: {data.S("nm_pasien")} · RM {data.S("no_rkm_medis")}");
                            c.Item().Text($"No. Rawat: {data.S("no_rawat")} · Permintaan: {data.S("no_permintaan")}");
                            var waktu = data.Dt("waktu");
                            c.Item().Text($"Waktu serah terima: {waktu:dd/MM/yyyy HH:mm:ss}");
                            c.Item().Text($"Petugas Farmasi: {data.S("petugas")}");
                            c.Item().Text($"Penerima: {data.S("penerima")}");
                        });
                    })).GeneratePdf();
                    await using var output = await file.OpenWriteAsync();
                    if (output.CanSeek) output.SetLength(0);
                    await output.WriteAsync(pdf);
                    Ui.Ok(status, "Bukti serah terima tersimpan.");
                }
                catch (Exception ex) { Ui.Err(status, ex.Message); }
                finally
                {
                    grid.IsEnabled = true;
                    UpdateButtons();
                }
            };

            grid.SelectionChanged += (_, _) => UpdateButtons();
            ubah.Click += async (_, _) =>
            {
                if (grid.SelectedItem is not StatusObatPulang { Menunggu: true } item) return;
                grid.IsEnabled = false;
                UpdateButtons();
                try
                {
                    var awal = await repo.MuatDrafAsync(user, visit.NoRawat, item.Nomor, item.Tanggal, item.Jam);
                    var form = new PermintaanResepPulangWindow(visit, user, awal);
                    await form.ShowDialog(dialog);
                    if (form.Saved) Ui.Ok(status, $"Permintaan {form.Nomor} diperbarui.");
                }
                catch (Exception ex) { Ui.Err(status, ex.Message); }
                finally { await RefreshActionsAsync(); }
            };

            async Task RefreshActionsAsync()
            {
                try { await ReloadAsync(); }
                catch (Exception ex) { Ui.Err(status, ex.Message); }
                grid.IsEnabled = true;
                UpdateButtons();
            }
            batal.Click += async (_, _) =>
            {
                if (grid.SelectedItem is not StatusObatPulang { Menunggu: true } item) return;
                grid.IsEnabled = false;
                UpdateButtons();
                try
                {
                    if (!await Dialogs.ConfirmAsync(batal, "Batalkan Resep Pulang",
                            $"Batalkan seluruh permintaan {item.Nomor} untuk {visit.NamaPasien}?")) return;
                    await repo.BatalkanPermintaanAsync(user, visit.NoRawat, item.Nomor, item.Tanggal, item.Jam);
                    Ui.Ok(status, $"Permintaan {item.Nomor} dibatalkan.");
                }
                catch (Exception ex) { Ui.Err(status, ex.Message); }
                finally { await RefreshActionsAsync(); }
            };
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal memuat status resep pulang. {ex.Message}", isError: true);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private sealed record StatusObatPulang(string Nomor, string Tanggal, string Jam, bool Menunggu,
        string Status, string Obat, string Jumlah, string Dosis, string Waktu, string Petugas, string Penerima, bool Diserahkan);

    private async void OnPindahClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || !RequireSelected(mustBeActive: true) || !RequireBarisIbu()) return;
        var patient = Selected!;
        ShowAction(ActionMode.Pindah, patient);
        await LoadKamarKosongAsync("PindahKamarCombo", patient.NoRawat, patient);
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
        // Tagihan bayi rawat gabung ada pada No. Rawat ibu.
        if (s.NoRawatIbu is { } noRawatIbu)
            s = (Grid().ItemsSource as IEnumerable<RawatInapListItem>)?.FirstOrDefault(r => r.NoRawat == noRawatIbu) ?? s;
        PatientMenuRequested?.Invoke(this, new(s.NoRawat, s.NoRekamMedis, s.NamaPasien, PatientMenuAction.BillingPembayaran));
    }

    private async void OnPemeriksaanClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(PemeriksaanRanapService.Izin))
            return;
        if (!RequireSelected(mustBeActive: false) || Selected is not { } visit || TopLevel.GetTopLevel(this) is not Window owner)
            return;
        await new PemeriksaanRanapWindow(visit, user).ShowDialog(owner);
    }

    private async void OnFormulirClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user) return;
        if (!RequireSelected(mustBeActive: false) || Selected is not { } visit || TopLevel.GetTopLevel(this) is not Window owner)
            return;
        if (await Modules.RekamMedis.PilihFormulir.TampilkanAsync(owner, user, visit.NamaPasien) is { } form)
            PatientMenuRequested?.Invoke(this,
                new(visit.NoRawat, visit.NoRekamMedis, visit.NamaPasien, PatientMenuAction.Skrining, form.Tile));
    }

    private void OnResepDokterClick(object? sender, RoutedEventArgs e) =>
        MintaOrder(PatientMenuAction.ResepDokter, "resep_dokter");

    private void OnPermintaanLabClick(object? sender, RoutedEventArgs e) =>
        MintaOrder(PatientMenuAction.PermintaanLab, "permintaan_lab");

    private void OnPermintaanRadClick(object? sender, RoutedEventArgs e) =>
        MintaOrder(PatientMenuAction.PermintaanRadiologi, "permintaan_radiologi");

    /// <summary>Order untuk pasien yang masih dirawat; bayi rawat gabung memakai No. Rawat bayi.</summary>
    private void MintaOrder(PatientMenuAction aksi, string izin)
    {
        if (_saving || AppSession.CurrentUser?.HasPermission(izin) != true) return;
        if (!RequireSelected(mustBeActive: true) || Selected is not { } visit) return;
        PatientMenuRequested?.Invoke(this, new(visit.NoRawat, visit.NoRekamMedis, visit.NamaPasien, aksi, Ranap: true));
    }

    private async void OnTindakanClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(TindakanRanapInputService.Izin))
            return;
        if (!RequireSelected(mustBeActive: false) || Selected is not { } visit || TopLevel.GetTopLevel(this) is not Window owner)
            return;
        await new TindakanRanapWindow(visit, user).ShowDialog(owner);
    }

    private async void OnObgynClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(TindakanRanapInputService.Izin))
            return;
        if (!RequireSelected(mustBeActive: false) || Selected is not { } visit || TopLevel.GetTopLevel(this) is not Window owner)
            return;
        await new ObgynRanapWindow(visit, user).ShowDialog(owner);
    }

    private async void OnOperasiClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(OperasiService.Izin)) return;
        if (!RequireSelected(mustBeActive: false) || Selected is not { } visit || TopLevel.GetTopLevel(this) is not Window owner)
            return;
        await new OperasiWindow(visit, user).ShowDialog(owner);
    }

    private async void OnRanapGabungClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission(RawatGabungService.Izin)) return;
        if (!RequireSelected(mustBeActive: true) || !RequireBarisIbu() || Selected is not { } visit ||
            TopLevel.GetTopLevel(this) is not Window owner) return;
        ShowAction(ActionMode.None);
        await new RanapGabungWindow(visit, user).ShowDialog(owner);
        await ReloadAsync();
    }

    /// <summary>Kamar, pulang dan jenis bayar bayi rawat gabung mengikuti ibunya.</summary>
    private bool RequireBarisIbu()
    {
        if (Selected is not { BayiGabung: true } bayi) return true;
        ShowStatus($"Ini baris bayi rawat gabung. Lakukan pada baris ibu (No. Rawat {bayi.NoRawatIbu}).", isError: true);
        return false;
    }

    private async void OnHapusDataSalahClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || AppSession.CurrentUser is not { } user || !user.HasPermission("kamar_inap")) return;
        if (!RequireSelected(mustBeActive: false) || !RequireBarisIbu() || Selected is not { } s ||
            TopLevel.GetTopLevel(this) is not Window owner) return;
        ShowAction(ActionMode.None);
        _saving = true;
        try
        {
            if (!await Dialogs.ConfirmAsync(owner, "Hapus data salah",
                    $"Hapus baris kamar {s.Kamar} milik {s.NamaPasien} ({s.NoRawat}), masuk {s.TglMasuk} {s.JamMasukText}? " +
                    "Baris kamar sebelumnya tidak dibuka kembali. Bila ini satu-satunya baris, kunjungan kembali menjadi rawat jalan.")) return;
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var jadiRalan = await new RawatInapRepository(db).HapusDataSalahAsync(s.Key, user.IsAdminUtama);
            await ReloadAsync();
            ShowStatus(jadiRalan ? $"Data kamar dihapus. Kunjungan {s.NoRawat} kembali menjadi rawat jalan."
                : $"Satu baris kamar {s.NoRawat} dihapus.", isError: false);
        }
        catch (Exception ex) { ShowStatus($"Gagal: {ex.Message}", isError: true); }
        finally { _saving = false; }
    }

    private async void OnGantiClick(object? sender, RoutedEventArgs e)
    {
        if (_saving || !RequireSelected(mustBeActive: false) || !RequireBarisIbu()) return;
        var s = Selected!;
        ShowAction(ActionMode.Ganti, s);
        this.FindControl<TextBox>("GantiDiagnosaAwalBox")!.Text = s.DiagnosaAwal;
        this.FindControl<TextBox>("GantiDiagnosaAkhirBox")!.Text = s.DiagnosaAkhir;

        var combo = this.FindControl<ComboBox>("GantiPenjabCombo")!;
        combo.ItemsSource = null;
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var penjab = await new RegistrasiRepository(db).GetPenjabAsync();
            if (_mode != ActionMode.Ganti || !ReferenceEquals(_actionPatient, s)) return;
            combo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Penjab.NamaPenanggungJawab));
            combo.ItemsSource = penjab;
            combo.SelectedItem = penjab.FirstOrDefault(p => p.KodePenjab == s.KodePenjab);
        }
        catch (Exception ex)
        {
            if (_mode == ActionMode.Ganti && ReferenceEquals(_actionPatient, s))
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

    private async Task LoadKamarKosongAsync(string comboName, string noRawat, RawatInapListItem? patient = null)
    {
        var combo = this.FindControl<ComboBox>(comboName)!;
        combo.ItemsSource = null;
        bool IsCurrent() => patient is null
            ? _mode == ActionMode.Masuk && noRawat == Text("AdmitNoRawatBox")
            : _mode == ActionMode.Pindah && ReferenceEquals(_actionPatient, patient);

        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new RawatInapRepository(db);
            var kamar = await repo.GetAvailableKamarForVisitAsync(noRawat, includeBooking: patient is null);
            if (!IsCurrent()) return;
            combo.ItemsSource = kamar;
            combo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(KamarTersediaItem.Label));
            if (kamar.Count > 0) combo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            if (IsCurrent()) ShowStatus($"Gagal memuat kamar kosong: {ex.Message}", isError: true);
        }
    }

    private void OnActionCancelClick(object? sender, RoutedEventArgs e) => ShowAction(ActionMode.None);

    private async void OnActionOkClick(object? sender, RoutedEventArgs e)
    {
        if (_saving) return;
        var panel = this.FindControl<Border>("ActionPanel")!;
        _saving = true;
        panel.IsEnabled = false;
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new RawatInapRepository(db);
            string message;
            RawatInapKey? dischargeKey = null;

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
                    var s = _actionPatient!;
                    var idx = this.FindControl<ComboBox>("StatusPulangCombo")!.SelectedIndex;
                    if (AppSession.CurrentUser?.HasPermission("kamar_inap") != true) return;
                    if (idx < 0 || this.FindControl<CalendarDatePicker>("TanggalKeluarPicker")!.SelectedDate is not { } tanggal ||
                        !TimeOnly.TryParseExact(Text("JamKeluarBox"), "HH:mm:ss", out var jam))
                    {
                        ShowStatus("Pilih status pulang dan isi waktu keluar dalam format HH:mm:ss.", isError: true);
                        return;
                    }
                    var waktu = DateOnly.FromDateTime(tanggal).ToDateTime(jam);
                    var status = DischargeOptions[idx];
                    var lanjutan = status switch
                    {
                        StatusPulang.Rujuk => " Lengkapi dokumen rujukan keluar di Khanza.",
                        StatusPulang.Meninggal => " Lengkapi pencatatan pasien meninggal di Khanza.",
                        _ => ""
                    };
                    if (!await Dialogs.ConfirmAsync(this, "Konfirmasi pulang", $"Catat {s.NamaPasien} keluar pada {waktu:dd/MM/yyyy HH:mm:ss} dengan status {status.ToLabel()}?{lanjutan}")) return;
                    if (AppSession.CurrentUser?.HasPermission("kamar_inap") != true) return;
                    var total = await repo.DischargeAsync(s.Key, status, Text("DiagnosaAkhirBox"), waktuKeluar: waktu);
                    message = $"Pulang tersimpan untuk {s.NamaPasien}. Biaya kamar Rp {total:N0}. Buka Resume Pulang atau Billing / Kasir dari baris terpilih.{lanjutan}";
                    dischargeKey = s.Key;
                    _ready = false;
                    try
                    {
                        this.FindControl<CalendarDatePicker>("PulangFromPicker")!.SelectedDate = waktu.Date;
                        this.FindControl<CalendarDatePicker>("PulangToPicker")!.SelectedDate = waktu.Date;
                        this.FindControl<ComboBox>("StatusBayarCombo")!.SelectedIndex = 0;
                        this.FindControl<RadioButton>("ModePulang")!.IsChecked = true;
                        this.FindControl<TextBox>("NoRawatFilterBox")!.Text = s.NoRawat;
                    }
                    finally { _ready = true; }
                    break;
                }
                case ActionMode.Pindah:
                {
                    var s = _actionPatient!;
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
                    var s = _actionPatient!;
                    var penjab = this.FindControl<ComboBox>("GantiPenjabCombo")!.SelectedItem as Penjab;
                    await repo.GantiDataKamarAsync(s, penjab?.KodePenjab ?? s.KodePenjab,
                        Text("GantiDiagnosaAwalBox"), Text("GantiDiagnosaAkhirBox"));
                    message = $"Data {s.NamaPasien} diperbarui.";
                    break;
                }
                default:
                    return;
            }

            ShowAction(ActionMode.None);
            await ReloadAsync();
            if (dischargeKey is { } key && Grid().ItemsSource is IEnumerable<RawatInapListItem> rows)
                Grid().SelectedItem = rows.FirstOrDefault(r => r.Key == key);
            ShowStatus(message, isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal: {ex.Message}", isError: true);
        }
        finally
        {
            _saving = false;
            panel.IsEnabled = true;
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
