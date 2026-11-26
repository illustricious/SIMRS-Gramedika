using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.Data.MenuA;
using SimrsGramedika.UI.Controls;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Registrasi;

public partial class RegistrasiView : UserControl
{
    private RegistrasiListItem? _selected;
    private bool _newMode;
    private bool _savingRegistration;
    private RegistrasiPasien? _pasienBaru;
    private RujukanMasukData? _drafRujukan;
    private int _loadVersion;
    private const string KodePoliIgd = "IGDK";

    private static RegistrasiAkses Akses() =>
        new(AppSession.CurrentUser?.IsAdminUtama == true, AppSession.CurrentUser?.HasPermission("edit_registrasi") == true);
    /// <summary>Raised when "Kembali ke Menu" is clicked — MainWindow swaps back to the dashboard.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Raised when "🩺 Periksa" is clicked — MainWindow opens RawatJalanView (the vitals/exam form).</summary>
    public event EventHandler? PeriksaRequested;

    /// <summary>Raised from the grid's right-click patient menu — see Modules/Shared/PatientContextMenu.</summary>
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    public RegistrasiView()
    {
        InitializeComponent();

        PatientContextMenu.Attach<RegistrasiListItem>(this.FindControl<DataGrid>("RegistrasiGrid")!,
            row => new(row.NoRawat, row.NoRekamMedis, row.NamaPasien, row.StatusRawat == "Ranap"),
            request => PatientMenuRequested?.Invoke(this, request), PatientMenuKind.Registration, OpenSelectedInput,
            BukaRujukanInternal, BukaRujukanMasuk, HapusDariContext, SetStatusDariContext);

        // Periode defaults to "today" (matches the legacy app's "Registrasi
        // Periksa Hari Ini" screen). An earlier revision defaulted to "all
        // registrations, no date filter" — fine against a handful of dev-seed
        // rows, but against the real production DB (169k+ reg_periksa rows)
        // that meant loading/rendering the entire table on every open, which
        // is what caused the "app lags on every screen" + "looks like no
        // data" reports (it wasn't empty, it just never finished loading).
        // "Cari / Semua" still lets the user widen the range deliberately.
        var today = DateTime.Today;
        this.FindControl<CalendarDatePicker>("PeriodeFromPicker")!.SelectedDate = today;
        this.FindControl<CalendarDatePicker>("PeriodeToPicker")!.SelectedDate = today;
    }

    /// <summary>Called by MainWindow each time this page becomes visible.</summary>
    public async System.Threading.Tasks.Task ReloadAsync()
    {
        this.FindControl<ToggleButton>("InputDataToggle")!.IsChecked = false;
        await LoadAsync();
        await LoadInputLookupsAsync();
    }

    private async System.Threading.Tasks.Task LoadInputLookupsAsync()
    {
        var dokterCombo = this.FindControl<ComboBox>("InputDokterCombo")!;
        var poliCombo = this.FindControl<ComboBox>("InputPoliCombo")!;
        var penjabCombo = this.FindControl<ComboBox>("InputPenjabCombo")!;

        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new RegistrasiRepository(db);

            var dokter = await repo.GetDokterAsync();
            var poli = await repo.GetPoliklinikAsync();
            var penjab = await repo.GetPenjabAsync();

            dokterCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Dokter.NamaDokter));
            dokterCombo.ItemsSource = dokter;
            poliCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Poliklinik.NamaPoli));
            poliCombo.ItemsSource = poli;
            penjabCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Penjab.NamaPenanggungJawab));
            penjabCombo.ItemsSource = penjab;

            var filterDokter = this.FindControl<ComboBox>("FilterDokterCombo")!;
            var dokterCode = (filterDokter.SelectedItem as Dokter)?.KodeDokter;
            filterDokter.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Dokter.NamaDokter));
            filterDokter.ItemsSource = dokter.Prepend(new Dokter { NamaDokter = "Semua dokter" }).ToList();
            filterDokter.SelectedItem = dokter.FirstOrDefault(x => x.KodeDokter == dokterCode);
            if (filterDokter.SelectedIndex < 0) filterDokter.SelectedIndex = 0;

            var filterPoli = this.FindControl<ComboBox>("FilterPoliCombo")!;
            var poliCode = (filterPoli.SelectedItem as Poliklinik)?.KodePoli;
            filterPoli.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Poliklinik.NamaPoli));
            filterPoli.ItemsSource = poli.Prepend(new Poliklinik { NamaPoli = "Semua unit" }).ToList();
            filterPoli.SelectedItem = poli.FirstOrDefault(x => x.KodePoli == poliCode);
            if (filterPoli.SelectedIndex < 0) filterPoli.SelectedIndex = 0;

            if (_selected is { } row) SelectInputLookups(row);
        }
        catch (Exception)
        {
            // Not connected — Input Data combos stay empty, Simpan will just fail with a clear message.
        }
    }

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        var dokter = this.FindControl<ComboBox>("FilterDokterCombo")!;
        if (dokter.ItemCount > 0) dokter.SelectedIndex = 0;
        var poli = this.FindControl<ComboBox>("FilterPoliCombo")!;
        if (poli.ItemCount > 0) poli.SelectedIndex = 0;
        this.FindControl<TextBox>("KeyWordBox")!.Text = string.Empty;
        await LoadAsync();
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e) => await LoadAsync();

    private async void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabs || e.Source != tabs || !IsLoaded) return;
        var awal = tabs.SelectedIndex == 0;
        this.FindControl<WrapPanel>("RegistrasiActions")!.IsEnabled = awal;
        this.FindControl<ToggleButton>("InputDataToggle")!.IsEnabled = awal;
        if (!awal) this.FindControl<ToggleButton>("InputDataToggle")!.IsChecked = false;
        await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        var version = ++_loadVersion;
        var internalPoli = this.FindControl<TabControl>("RegistrasiTabs")!.SelectedIndex == 1;
        var grid = this.FindControl<DataGrid>("RegistrasiGrid")!;
        var recordCount = this.FindControl<TextBlock>("RecordCountText")!;
        var fromPicker = this.FindControl<CalendarDatePicker>("PeriodeFromPicker")!;
        var toPicker = this.FindControl<CalendarDatePicker>("PeriodeToPicker")!;
        var keyWord = this.FindControl<TextBox>("KeyWordBox")!.Text?.Trim();
        var dokterCode = (this.FindControl<ComboBox>("FilterDokterCombo")!.SelectedItem as Dokter)?.KodeDokter;
        var poliCode = (this.FindControl<ComboBox>("FilterPoliCombo")!.SelectedItem as Poliklinik)?.KodePoli;

        var from = fromPicker.SelectedDate is { } f ? DateOnly.FromDateTime(f.Date) : DateOnly.MinValue;
        var to = toPicker.SelectedDate is { } t ? DateOnly.FromDateTime(t.Date) : DateOnly.FromDateTime(DateTime.Today);

        try
        {
            var settings = DatabaseSettings.Load();
            if (internalPoli)
            {
                var user = AppSession.CurrentUser ?? throw new InvalidOperationException("Sesi pengguna tidak tersedia.");
                var referrals = await new RujukanInternalService(settings, user).DaftarAsync(from, to, dokterCode, poliCode, keyWord);
                if (version != _loadVersion) return;
                this.FindControl<DataGrid>("RujukanInternalGrid")!.ItemsSource = referrals;
                this.FindControl<Button>("CetakRujukanButton")!.IsEnabled = referrals.Count > 0;
                recordCount.Text = $"{referrals.Count} rujukan internal";
                return;
            }
            using var db = SimrsGramedikaDbContextFactory.Create(settings);
            var repo = new RegistrasiRepository(db);
            // Seperti DlgReg: kunjungan IGD (poli IGDK) hanya tampil di layar IGD.
            var items = (await repo.GetByPeriodeAsync(from, to)).Where(i => i.KodePoli != KodePoliIgd).ToList();
            if (!string.IsNullOrEmpty(dokterCode))
                items = items.Where(i => i.KodeDokter == dokterCode).ToList();
            if (!string.IsNullOrEmpty(poliCode))
                items = items.Where(i => i.KodePoli == poliCode).ToList();

            if (!string.IsNullOrWhiteSpace(keyWord))
            {
                bool Has(string? text) => text?.Contains(keyWord, StringComparison.OrdinalIgnoreCase) ?? false;
                items = items.Where(i => Has(i.NamaPasien) || Has(i.NoRekamMedis) || Has(i.NoRawat) || Has(i.NoReg)
                                       || Has(i.TanggalRegistrasi?.ToString("yyyy-MM-dd")) || Has(i.TanggalRegistrasi?.ToString("dd/MM/yyyy"))
                                       || Has(i.NamaPoli) || Has(i.NamaDokter) || Has(i.PenanggungJawab) || Has(i.AlamatPj)
                                       || Has(i.JenisBayar) || Has(i.HubunganPj) || Has(i.StatusPoli) || Has(i.Status))
                              .ToList();
            }

            if (version != _loadVersion) return;
            grid.ItemsSource = items;
            recordCount.Text = $"{items.Count} registrasi";
        }
        catch (Exception)
        {
            if (version != _loadVersion) return;
            if (internalPoli)
            {
                this.FindControl<DataGrid>("RujukanInternalGrid")!.ItemsSource = Array.Empty<RujukanInternalItem>();
                this.FindControl<Button>("CetakRujukanButton")!.IsEnabled = false;
            }
            else grid.ItemsSource = new List<RegistrasiListItem>();
            recordCount.Text = "Data tidak dapat dimuat; periksa akses dan koneksi";
        }
    }

    private async void OnRegistrasiSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selected = this.FindControl<DataGrid>("RegistrasiGrid")!.SelectedItem as RegistrasiListItem;
        var row = _selected;
        if (_newMode && row is null) return;
        SetNewMode(false);
        SetInputText("InputNoRegBox", row?.NoReg);
        SetInputText("InputNoRawatBox", row?.NoRawat);
        SetInputText("InputTanggalBox", row?.TanggalRegistrasi?.ToString("dd/MM/yyyy"));
        SetInputText("InputJamBox", row?.JamRegistrasi?.ToString("HH:mm:ss"));
        SetInputText("InputNoRekamMedisBox", row?.NoRekamMedis);
        SetInputText("InputNamaPasienBox", row?.NamaPasien);
        SetInputText("InputBiayaBox", row?.BiayaReg?.ToString("N0",
            System.Globalization.CultureInfo.GetCultureInfo("id-ID")));
        SetInputText("InputPenanggungJawabBox", row?.PenanggungJawab);
        SetInputText("InputAlamatPjBox", row?.AlamatPj);
        SetInputText("InputHubunganBox", row?.HubunganPj);
        SetInputText("InputStatusDaftarBox", null);
        SetInputText("InputNoKartuBox", null);
        SetInputText("InputAsalRujukanBox", null);

        var status = this.FindControl<TextBlock>("InputStatusText")!;
        status.IsVisible = row is not null;
        if (row is null)
        {
            this.FindControl<ComboBox>("InputDokterCombo")!.SelectedIndex = -1;
            this.FindControl<ComboBox>("InputPoliCombo")!.SelectedIndex = -1;
            this.FindControl<ComboBox>("InputPenjabCombo")!.SelectedIndex = -1;
            return;
        }

        SelectInputLookups(row);
        status.Classes.Remove("error");
        status.Classes.Remove("saved");
        status.Text = $"Mengubah {row.NoRawat} — {row.NamaPasien}";
        if (IsInputOpen) await LoadVisitDetailsAsync(row);
    }

    private bool IsInputOpen => this.FindControl<ToggleButton>("InputDataToggle")!.IsChecked == true;

    private async void OpenSelectedInput(RegistrasiListItem row)
    {
        this.FindControl<ToggleButton>("InputDataToggle")!.IsChecked = true;
        await LoadVisitDetailsAsync(row);
    }

    private async void OnInputDataClick(object? sender, RoutedEventArgs e)
    {
        if (IsInputOpen && _selected is { } row)
            await LoadVisitDetailsAsync(row);
    }

    private async Task LoadVisitDetailsAsync(RegistrasiListItem row)
    {
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var details = await new RegistrasiRepository(db).GetInputDetailsAsync(row.NoRawat);
            if (!ReferenceEquals(_selected, row)) return;

            SetInputText("InputStatusDaftarBox", details?.StatusPendaftaran);
            SetInputText("InputNoKartuBox", details?.NoKartu);
            SetInputText("InputAsalRujukanBox", details?.AsalRujukan);
        }
        catch (Exception)
        {
            if (ReferenceEquals(_selected, row))
                this.FindControl<TextBlock>("InputStatusText")!.Text =
                    "Status pendaftaran, nomor kartu, dan asal rujukan tidak dapat dimuat.";
        }
    }

    private void SetInputText(string controlName, string? value) =>
        this.FindControl<TextBox>(controlName)!.Text = value ?? string.Empty;


    private void SelectInputLookups(RegistrasiListItem row)
    {
        var dokter = this.FindControl<ComboBox>("InputDokterCombo")!;
        var poli = this.FindControl<ComboBox>("InputPoliCombo")!;
        var penjab = this.FindControl<ComboBox>("InputPenjabCombo")!;
        dokter.SelectedItem = (dokter.ItemsSource as IEnumerable<Dokter>)?.FirstOrDefault(x => x.KodeDokter == row.KodeDokter);
        poli.SelectedItem = (poli.ItemsSource as IEnumerable<Poliklinik>)?.FirstOrDefault(x => x.KodePoli == row.KodePoli);
        penjab.SelectedItem = (penjab.ItemsSource as IEnumerable<Penjab>)?.FirstOrDefault(x => x.KodePenjab == row.KodePj);
    }

    private void SetNewMode(bool on)
    {
        _newMode = on;
        _drafRujukan = null;
        _pasienBaru = null;
        this.FindControl<TextBox>("InputNoRekamMedisBox")!.IsReadOnly = !on;
        this.FindControl<Button>("PilihPasienButton")!.IsEnabled = on;
        this.FindControl<Button>("PasienBaruButton")!.IsEnabled =
            on && AppSession.CurrentUser?.HasPermission(PasienBaruService.Izin) == true;
        UpdateRujukanMasukButton();
    }

    private void OnBaruClick(object? sender, RoutedEventArgs e)
    {
        _selected = null;
        SetNewMode(true);
        this.FindControl<DataGrid>("RegistrasiGrid")!.SelectedItem = null;
        foreach (var name in new[] { "InputNoRegBox", "InputNoRawatBox", "InputTanggalBox", "InputJamBox", "InputNoRekamMedisBox", "InputNamaPasienBox",
                     "InputBiayaBox", "InputPenanggungJawabBox", "InputAlamatPjBox", "InputHubunganBox", "InputStatusDaftarBox", "InputNoKartuBox", "InputAsalRujukanBox" })
            SetInputText(name, null);
        this.FindControl<ComboBox>("InputDokterCombo")!.SelectedIndex = -1;
        this.FindControl<ComboBox>("InputPoliCombo")!.SelectedIndex = -1;
        this.FindControl<ComboBox>("InputPenjabCombo")!.SelectedIndex = -1;
        this.FindControl<ToggleButton>("InputDataToggle")!.IsChecked = true;
        var status = this.FindControl<TextBlock>("InputStatusText")!;
        status.Classes.Remove("error");
        status.Classes.Remove("saved");
        status.Text = "Registrasi baru: cari pasien atau isi No. Rekam Medik lalu Enter, pilih dokter, unit, dan jenis bayar. No. reg, no. rawat, status, dan biaya diisi otomatis.";
        status.IsVisible = true;
        this.FindControl<TextBox>("InputNoRekamMedisBox")!.Focus();
    }

    private async void OnNoRmLostFocus(object? sender, RoutedEventArgs e) => await LoadPasienBaruAsync();

    private void OnNoRmChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_newMode) return;
        if (_pasienBaru is not null && _pasienBaru.NoRekamMedis == this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text?.Trim()) return;
        _pasienBaru = null;
        _drafRujukan = null;
        SetInputText("InputAsalRujukanBox", null);
        UpdateRujukanMasukButton();
        foreach (var name in new[] { "InputNamaPasienBox", "InputPenanggungJawabBox", "InputHubunganBox", "InputAlamatPjBox", "InputStatusDaftarBox" }) SetInputText(name, null);
        this.FindControl<ComboBox>("InputPenjabCombo")!.SelectedIndex = -1;
    }

    private async void OnPilihPasienClick(object? sender, RoutedEventArgs e)
    {
        if (!_newMode || TopLevel.GetTopLevel(this) is not Window owner) return;
        try
        {
            var queries = new MenuAQueries(DatabaseSettings.Load());
            var row = await SearchPickerDialog.ShowAsync(owner, "Cari pasien", q => queries.CariPasienAsync(q, false),
                r => $"{r.S("no_rkm_medis")}  •  {r.S("nm_pasien")}");
            if (!_newMode || row is null) return;
            SetInputText("InputNoRekamMedisBox", row.S("no_rkm_medis"));
            await LoadPasienBaruAsync();
        }
        catch (Exception ex) { ShowInputError(this.FindControl<TextBlock>("InputStatusText")!, $"Gagal mencari pasien: {ex.Message}"); }
    }

    private async void OnPasienBaruClick(object? sender, RoutedEventArgs e)
    {
        if (!_newMode || TopLevel.GetTopLevel(this) is not Window owner || AppSession.CurrentUser is not { } user) return;
        try
        {
            var noRm = await new Pasien.PasienBaruWindow(new PasienBaruService(DatabaseSettings.Load(), user))
                .ShowDialog<string?>(owner);
            if (!_newMode || string.IsNullOrEmpty(noRm)) return;
            SetInputText("InputNoRekamMedisBox", noRm);
            await LoadPasienBaruAsync();
        }
        catch (Exception ex) { ShowInputError(this.FindControl<TextBlock>("InputStatusText")!, $"Gagal mendaftarkan pasien: {ex.Message}"); }
    }

    private async void OnNoRmKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter) await LoadPasienBaruAsync();
    }

    private async System.Threading.Tasks.Task LoadPasienBaruAsync()
    {
        if (!_newMode) return;
        var status = this.FindControl<TextBlock>("InputStatusText")!;
        var rm = this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text?.Trim();
        if (string.IsNullOrEmpty(rm) || _pasienBaru?.NoRekamMedis == rm) return;
        try
        {
            var pasien = await new RegistrasiService(DatabaseSettings.Load()).CariPasienAsync(rm);
            if (!_newMode || this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text?.Trim() != rm) return;
            _pasienBaru = pasien;
            UpdateRujukanMasukButton();
            if (pasien is null)
            {
                SetInputText("InputNamaPasienBox", null);
                ShowInputError(status, $"Pasien {rm} tidak ditemukan. Gunakan tombol Pasien Baru atau layar Pasien untuk mendaftarkannya.");
                return;
            }
            SetInputText("InputNamaPasienBox", $"{pasien.Nama} ({pasien.Umur})");
            SetInputText("InputPenanggungJawabBox", pasien.NamaKeluarga);
            SetInputText("InputHubunganBox", pasien.Keluarga);
            SetInputText("InputAlamatPjBox", pasien.Alamat);
            SetInputText("InputStatusDaftarBox", pasien.StatusDaftar);
            var penjab = this.FindControl<ComboBox>("InputPenjabCombo")!;
            penjab.SelectedItem = (penjab.ItemsSource as IEnumerable<Penjab>)?.FirstOrDefault(x => x.KodePenjab == pasien.KodePenjab);
            status.Classes.Remove("error");
            status.Text = $"Registrasi baru untuk {pasien.Nama}. Lengkapi dokter dan unit lalu Simpan.";
        }
        catch (Exception ex)
        {
            if (_newMode && this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text?.Trim() == rm)
                ShowInputError(status, $"Gagal memuat pasien: {ex.Message}");
        }
    }

    private async void OnInputSimpanClick(object? sender, RoutedEventArgs e)
    {
        if (_savingRegistration) return;
        var status = this.FindControl<TextBlock>("InputStatusText")!;
        var noRekamMedis = this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text?.Trim();
        var poli = this.FindControl<ComboBox>("InputPoliCombo")!.SelectedItem as Poliklinik;
        var penjab = this.FindControl<ComboBox>("InputPenjabCombo")!.SelectedItem as Penjab;
        var dokter = this.FindControl<ComboBox>("InputDokterCombo")!.SelectedItem as Dokter;
        var pj = this.FindControl<TextBox>("InputPenanggungJawabBox")!.Text?.Trim() ?? "";
        var alamat = this.FindControl<TextBox>("InputAlamatPjBox")!.Text?.Trim() ?? "";
        var hubungan = this.FindControl<TextBox>("InputHubunganBox")!.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(noRekamMedis) || dokter is null || poli is null || penjab is null || (!_newMode && _selected is null))
        {
            ShowInputError(status, _newMode
                ? "Isi No. Rekam Medik pasien dan lengkapi dokter, unit, serta jenis bayar."
                : "Pilih pasien pada tabel dan lengkapi dokter, poli, serta jenis bayar.");
            return;
        }

        _savingRegistration = true;
        try
        {
            var service = new RegistrasiService(DatabaseSettings.Load());
            string noRawat;
            if (_newMode)
            {
                if (_pasienBaru?.NoRekamMedis != noRekamMedis) await LoadPasienBaruAsync();
                if (!_newMode || this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text?.Trim() != noRekamMedis || _pasienBaru?.NoRekamMedis != noRekamMedis)
                { ShowInputError(status, "Data pasien berubah atau tidak ditemukan. Pilih kembali pasien."); return; }
                pj = this.FindControl<TextBox>("InputPenanggungJawabBox")!.Text?.Trim() ?? "";
                alamat = this.FindControl<TextBox>("InputAlamatPjBox")!.Text?.Trim() ?? "";
                hubungan = this.FindControl<TextBox>("InputHubunganBox")!.Text?.Trim() ?? "";
                noRawat = await service.DaftarAsync(new(noRekamMedis, dokter.KodeDokter, poli.KodePoli, penjab.KodePenjab, pj, alamat, hubungan, DateTime.Now),
                    Akses(), rujukan: _drafRujukan, pengguna: AppSession.CurrentUser);
                SetNewMode(false);
                await LoadAsync();
                status.Classes.Remove("error");
                status.Classes.Add("saved");
                status.Text = $"Registrasi baru tersimpan — No.Rawat {noRawat}.";
                status.IsVisible = true;
                return;
            }

            noRawat = _selected!.NoRawat;
            await service.UbahAsync(noRawat, noRekamMedis, dokter.KodeDokter, poli.KodePoli, penjab.KodePenjab, pj, alamat, hubungan, Akses());
            await LoadAsync();
            status.Classes.Remove("error");
            status.Classes.Add("saved");
            status.Text = $"Perubahan registrasi {noRawat} tersimpan.";
            status.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowInputError(status, $"Gagal simpan registrasi: {ex.Message}");
        }
        finally { _savingRegistration = false; }
    }

    private async void OnHapusClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("InputStatusText")!;
        if (_selected is not { } row)
        {
            ShowInputError(status, "Pilih registrasi pada tabel yang akan dihapus.");
            return;
        }
        if (!await Dialogs.ConfirmAsync(this, "Hapus Registrasi", $"Hapus registrasi {row.NoRawat} — {row.NamaPasien}?")) return;
        try
        {
            await new RegistrasiService(DatabaseSettings.Load()).HapusAsync(row.NoRawat, Akses());
            _selected = null;
            await LoadAsync();
            status.Classes.Remove("error");
            status.Classes.Add("saved");
            status.Text = $"Registrasi {row.NoRawat} dihapus.";
            status.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowInputError(status, $"Gagal hapus registrasi: {ex.Message}");
        }
    }

    private void HapusDariContext(RegistrasiListItem row)
    {
        _selected = row;
        this.FindControl<DataGrid>("RegistrasiGrid")!.SelectedItem = row;
        OnHapusClick(this, new RoutedEventArgs());
    }

    private async void SetStatusDariContext(RegistrasiListItem row, string statusBaru)
    {
        _selected = row;
        this.FindControl<DataGrid>("RegistrasiGrid")!.SelectedItem = row;
        if (statusBaru == "Batal" && !await Dialogs.ConfirmAsync(this, "Batalkan Pemeriksaan",
            $"Batalkan pemeriksaan {row.NoRawat} — {row.NamaPasien}? Biaya registrasi akan menjadi Rp0.")) return;

        var status = this.FindControl<TextBlock>("InputStatusText")!;
        try
        {
            await new RegistrasiService(DatabaseSettings.Load()).SetStatusAsync(row.NoRawat, statusBaru, Akses());
            _selected = null;
            await LoadAsync();
            status.Classes.Remove("error");
            status.Classes.Add("saved");
            status.Text = $"Status {row.NoRawat} diperbarui menjadi {statusBaru}.";
            status.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowInputError(status, $"Gagal mengubah status registrasi: {ex.Message}");
        }
    }

    private static void ShowInputError(TextBlock block, string message)
    {
        block.Classes.Remove("saved");
        block.Classes.Add("error");
        block.Text = message;
        block.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    private void OnPeriksaClick(object? sender, RoutedEventArgs e) => PeriksaRequested?.Invoke(this, EventArgs.Empty);

    private async void OnCetakClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path;
            if (ReferenceEquals(sender, this.FindControl<Button>("CetakRujukanButton")))
            {
                if (this.FindControl<DataGrid>("RujukanInternalGrid")!.ItemsSource is not IReadOnlyList<RujukanInternalItem> { Count: > 0 } referrals) return;
                path = RegistrasiReport.GenerateRujukanPdf(referrals);
            }
            else
            {
                if (this.FindControl<DataGrid>("RegistrasiGrid")!.ItemsSource is not IReadOnlyList<RegistrasiListItem> { Count: > 0 } items)
                {
                    await Dialogs.InfoAsync(this, "Cetak Registrasi", "Tidak ada data untuk dicetak.");
                    return;
                }
                path = RegistrasiReport.GeneratePdf(items, "Daftar Pasien Rawat Jalan");
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(this, "Gagal Cetak", $"Gagal membuat atau membuka PDF: {ex.Message}");
        }
    }


}
