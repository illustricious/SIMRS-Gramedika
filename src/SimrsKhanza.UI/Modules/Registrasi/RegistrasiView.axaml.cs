using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Registrasi;

public partial class RegistrasiView : UserControl
{
    private RegistrasiListItem? _selected;
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
            request => PatientMenuRequested?.Invoke(this, request), PatientMenuKind.Registration, OpenSelectedInput);

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
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
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

    private async System.Threading.Tasks.Task LoadAsync()
    {
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
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new RegistrasiRepository(db);
            var items = await repo.GetByPeriodeAsync(from, to);
            if (!string.IsNullOrEmpty(dokterCode))
                items = items.Where(i => i.KodeDokter == dokterCode).ToList();
            if (!string.IsNullOrEmpty(poliCode))
                items = items.Where(i => i.KodePoli == poliCode).ToList();

            if (!string.IsNullOrWhiteSpace(keyWord))
            {
                items = items.Where(i => i.NamaPasien.Contains(keyWord, StringComparison.OrdinalIgnoreCase)
                                       || i.NoRekamMedis.Contains(keyWord, StringComparison.OrdinalIgnoreCase)
                                       || i.NoRawat.Contains(keyWord, StringComparison.OrdinalIgnoreCase)
                                       || (i.NoReg?.Contains(keyWord, StringComparison.OrdinalIgnoreCase) ?? false))
                              .ToList();
            }

            grid.ItemsSource = items;
            recordCount.Text = $"{items.Count} registrasi";
        }
        catch (Exception)
        {
            // No reachable MySQL server — show an empty grid rather than crash.
            grid.ItemsSource = new List<RegistrasiListItem>();
            recordCount.Text = "Database tidak terhubung";
        }
    }

    private async void OnRegistrasiSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selected = this.FindControl<DataGrid>("RegistrasiGrid")!.SelectedItem as RegistrasiListItem;
        var row = _selected;
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
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
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

    private async void OnInputSimpanClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("InputStatusText")!;
        var noRekamMedis = this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text?.Trim();
        var poli = this.FindControl<ComboBox>("InputPoliCombo")!.SelectedItem as Poliklinik;
        var penjab = this.FindControl<ComboBox>("InputPenjabCombo")!.SelectedItem as Penjab;
        var dokter = this.FindControl<ComboBox>("InputDokterCombo")!.SelectedItem as Dokter;

        if (_selected is null || string.IsNullOrWhiteSpace(noRekamMedis) || dokter is null || poli is null || penjab is null)
        {
            ShowInputError(status, "Pilih pasien pada tabel dan lengkapi dokter, poli, serta jenis bayar.");
            return;
        }

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var noRawat = _selected.NoRawat;
            await new RegistrasiRepository(db).UpdateAsync(noRawat, noRekamMedis, dokter.KodeDokter, poli.KodePoli, penjab.KodePenjab,
                this.FindControl<TextBox>("InputPenanggungJawabBox")!.Text?.Trim() ?? "",
                this.FindControl<TextBox>("InputAlamatPjBox")!.Text?.Trim() ?? "",
                this.FindControl<TextBox>("InputHubunganBox")!.Text?.Trim() ?? "");

            status.Classes.Remove("error");
            status.Classes.Add("saved");
            status.Text = $"Registrasi tersimpan — No.Rawat {noRawat}.";
            status.IsVisible = true;

            await LoadAsync();
            status.Text = $"Perubahan registrasi {noRawat} tersimpan.";
            status.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowInputError(status, $"Gagal simpan registrasi: {ex.Message}");
        }
    }

    private static void ShowInputError(TextBlock block, string message)
    {
        block.Classes.Remove("saved");
        block.Classes.Add("error");
        block.Text = message;
        block.IsVisible = true;
    }

    private void OnPlaceholderClick(object? sender, RoutedEventArgs e)
    {
        var label = (sender as Button)?.Content?.ToString() ?? "Aksi";
        var dialog = new Window
        {
            Width = 300,
            Height = 120,
            Title = label,
            Content = new TextBlock
            {
                Text = $"\"{label}\" belum diimplementasikan di tahap scaffold ini.",
                Margin = new Avalonia.Thickness(16),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            }
        };
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null) _ = dialog.ShowDialog(owner);
        else dialog.Show();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    private void OnPeriksaClick(object? sender, RoutedEventArgs e) => PeriksaRequested?.Invoke(this, EventArgs.Empty);

    private void OnCetakClick(object? sender, RoutedEventArgs e)
    {
        var grid = this.FindControl<DataGrid>("RegistrasiGrid")!;
        var items = (grid.ItemsSource as IEnumerable<RegistrasiListItem>)?.ToList() ?? new List<RegistrasiListItem>();

        if (items.Count == 0)
        {
            OnPlaceholderClick(new Button { Content = "🖨 Cetak (tidak ada data)" }, e);
            return;
        }

        try
        {
            var path = RegistrasiReport.GeneratePdf(items, "Daftar Pasien Rawat Jalan");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            var dialog = new Window
            {
                Width = 360,
                Height = 140,
                Title = "Gagal Cetak",
                Content = new TextBlock
                {
                    Text = $"Gagal membuat/membuka PDF: {ex.Message}",
                    Margin = new Avalonia.Thickness(16),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                }
            };
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is not null) _ = dialog.ShowDialog(owner);
            else dialog.Show();
        }
    }


}
