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
    /// <summary>Raised when "Kembali ke Menu" is clicked — MainWindow swaps back to the dashboard.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Raised when "🩺 Periksa" is clicked — MainWindow opens RawatJalanView (the vitals/exam form).</summary>
    public event EventHandler? PeriksaRequested;

    /// <summary>Raised from the grid's right-click patient menu — see Modules/Shared/PatientContextMenu.</summary>
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    public RegistrasiView()
    {
        InitializeComponent();

        this.FindControl<DataGrid>("RegistrasiGrid")!.ContextMenu = PatientContextMenu.Build(
            () => this.FindControl<DataGrid>("RegistrasiGrid")!.SelectedItem is RegistrasiListItem i
                ? (i.NoRawat, i.NoRekamMedis, i.NamaPasien) : null,
            request => PatientMenuRequested?.Invoke(this, request));

        // Periode defaults to "today" (matches the legacy app's "Registrasi
        // Periksa Hari Ini" screen). An earlier revision defaulted to "all
        // registrations, no date filter" — fine against a handful of dev-seed
        // rows, but against the real production DB (169k+ reg_periksa rows)
        // that meant loading/rendering the entire table on every open, which
        // is what caused the "app lags on every screen" + "looks like no
        // data" reports (it wasn't empty, it just never finished loading).
        // "Cari / Semua" still lets the user widen the range deliberately.
        var today = DateTime.Today;
        this.FindControl<DatePicker>("PeriodeFromPicker")!.SelectedDate = today;
        this.FindControl<DatePicker>("PeriodeToPicker")!.SelectedDate = today;

        // "Input Data" starts COLLAPSED (toggle IsChecked="False") — per user
        // feedback this is the correct default, matching the legacy app:
        // the form only appears once the user explicitly clicks the toggle.
        // Set explicitly here (rather than relying on the XAML default) so
        // it stays correct regardless of IsChecked's XAML value — see
        // OnToggleInputData for the click handler that flips it.
        var toggle = this.FindControl<ToggleButton>("InputDataToggle")!;
        this.FindControl<Border>("InputDataPanel")!.IsVisible = toggle.IsChecked == true;
    }

    /// <summary>Called by MainWindow each time this page becomes visible.</summary>
    public async System.Threading.Tasks.Task ReloadAsync()
    {
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
            dokterCombo.ItemsSource = dokter;
            dokterCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Dokter.NamaDokter));
            if (dokter.Count > 0) dokterCombo.SelectedIndex = 0;

            var poli = await repo.GetPoliklinikAsync();
            poliCombo.ItemsSource = poli;
            poliCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Poliklinik.NamaPoli));
            if (poli.Count > 0) poliCombo.SelectedIndex = 0;

            var penjab = await repo.GetPenjabAsync();
            penjabCombo.ItemsSource = penjab;
            penjabCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Penjab.NamaPenanggungJawab));
            if (penjab.Count > 0) penjabCombo.SelectedIndex = 0;
        }
        catch (Exception)
        {
            // Not connected — Input Data combos stay empty, Simpan will just fail with a clear message.
        }
    }

    private void OnToggleInputData(object? sender, RoutedEventArgs e)
    {
        var panel = this.FindControl<Border>("InputDataPanel")!;
        var toggle = (ToggleButton)sender!;
        panel.IsVisible = toggle.IsChecked == true;
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e) => await LoadAsync();

    private async System.Threading.Tasks.Task LoadAsync()
    {
        var grid = this.FindControl<DataGrid>("RegistrasiGrid")!;
        var recordCount = this.FindControl<TextBlock>("RecordCountText")!;
        var fromPicker = this.FindControl<DatePicker>("PeriodeFromPicker")!;
        var toPicker = this.FindControl<DatePicker>("PeriodeToPicker")!;
        var keyWord = this.FindControl<TextBox>("KeyWordBox")!.Text?.Trim();

        var from = fromPicker.SelectedDate is { } f ? DateOnly.FromDateTime(f.Date) : DateOnly.MinValue;
        var to = toPicker.SelectedDate is { } t ? DateOnly.FromDateTime(t.Date) : DateOnly.FromDateTime(DateTime.Today);

        var filter = new RegistrasiSearchFilter(
            NoRawat: EmptyToNull(this.FindControl<TextBox>("SearchNoRawatBox")!.Text),
            NoReg: EmptyToNull(this.FindControl<TextBox>("SearchNoRegBox")!.Text),
            NoRekamMedis: EmptyToNull(this.FindControl<TextBox>("SearchNoRmBox")!.Text),
            NamaPasien: EmptyToNull(this.FindControl<TextBox>("SearchNamaBox")!.Text));

        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new RegistrasiRepository(db);
            var items = await repo.GetByPeriodeAsync(from, to, filter);

            if (!string.IsNullOrWhiteSpace(keyWord))
            {
                items = items.Where(i => i.NamaPasien.Contains(keyWord, StringComparison.OrdinalIgnoreCase)
                                       || i.NoRekamMedis.Contains(keyWord, StringComparison.OrdinalIgnoreCase))
                              .ToList();
            }

            grid.ItemsSource = items;
            recordCount.Text = $"Record : {items.Count}";
        }
        catch (Exception)
        {
            // No reachable MySQL server — show an empty grid rather than crash.
            grid.ItemsSource = new List<RegistrasiListItem>();
            recordCount.Text = "Record : 0 (tidak konek DB)";
        }
    }

    private async void OnInputSimpanClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("InputStatusText")!;
        var noRekamMedis = this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text?.Trim();
        var poli = this.FindControl<ComboBox>("InputPoliCombo")!.SelectedItem as Poliklinik;
        var penjab = this.FindControl<ComboBox>("InputPenjabCombo")!.SelectedItem as Penjab;
        var dokter = this.FindControl<ComboBox>("InputDokterCombo")!.SelectedItem as Dokter;

        if (string.IsNullOrWhiteSpace(noRekamMedis) || poli is null || penjab is null)
        {
            ShowInputError(status, "No.Rekam Medik, Unit/Poli, dan Jenis Bayar wajib diisi.");
            return;
        }

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var noRawat = await new RegistrasiRepository(db).CreateAsync(
                noRekamMedis, dokter?.KodeDokter, poli.KodePoli, penjab.KodePenjab,
                EmptyToNull(this.FindControl<TextBox>("InputPenanggungJawabBox")!.Text),
                EmptyToNull(this.FindControl<TextBox>("InputAlamatPjBox")!.Text));

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Registrasi tersimpan — No.Rawat {noRawat}.";
            status.IsVisible = true;

            ClearInputForm();
            await LoadAsync(); // new registration shows up in the grid below
        }
        catch (Exception ex)
        {
            ShowInputError(status, $"Gagal simpan registrasi: {ex.Message}");
        }
    }

    private void OnInputBaruClick(object? sender, RoutedEventArgs e) => ClearInputForm();

    private void ClearInputForm()
    {
        this.FindControl<TextBox>("InputNoRekamMedisBox")!.Text = string.Empty;
        this.FindControl<TextBox>("InputPenanggungJawabBox")!.Text = string.Empty;
        this.FindControl<TextBox>("InputAlamatPjBox")!.Text = string.Empty;
        this.FindControl<TextBlock>("InputStatusText")!.IsVisible = false;
    }

    private static void ShowInputError(TextBlock block, string message)
    {
        block.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
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

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
