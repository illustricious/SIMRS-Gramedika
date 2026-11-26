using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Registrasi;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Igd;

public partial class IgdView : UserControl
{
    /// <summary>Legacy DlgIGD hardcodes poliklinik 'IGDK' ("Unit IGD") in both its list query and its registration.</summary>
    private const string KodePoliIgd = "IGDK";
    private bool _menyimpan;

    public event EventHandler? BackRequested;
    public event EventHandler? TriaseRequested;

    /// <summary>Raised from the grid's right-click patient menu — see Modules/Shared/PatientContextMenu.</summary>
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    public IgdView()
    {
        InitializeComponent();

        var grid = this.FindControl<DataGrid>("IgdGrid")!;
        PatientContextMenu.Attach<RegistrasiListItem>(grid,
            row => new(row.NoRawat, row.NoRekamMedis, row.NamaPasien, row.StatusRawat == "Ranap"),
            request => PatientMenuRequested?.Invoke(this, request), PatientMenuKind.Emergency);

        // Default period = today ("Registrasi IGD Hari Ini").
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        await LoadComboAsync();
        await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadComboAsync()
    {
        var dokterCombo = this.FindControl<ComboBox>("InputDokterCombo")!;
        var penjabCombo = this.FindControl<ComboBox>("InputPenjabCombo")!;
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new RegistrasiRepository(db);

            var dokter = await repo.GetDokterAsync();
            dokterCombo.ItemsSource = dokter;
            dokterCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Dokter.NamaDokter));
            if (dokter.Count > 0 && dokterCombo.SelectedIndex < 0) dokterCombo.SelectedIndex = 0;

            var penjab = await repo.GetPenjabAsync();
            penjabCombo.ItemsSource = penjab;
            penjabCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Penjab.NamaPenanggungJawab));
            if (penjab.Count > 0 && penjabCombo.SelectedIndex < 0) penjabCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal memuat data dokter/jenis bayar: {ex.Message}", isError: true);
        }
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        var grid = this.FindControl<DataGrid>("IgdGrid")!;
        var from = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate ?? DateTime.Today);
        var keyword = this.FindControl<TextBox>("KeywordBox")!.Text?.Trim();

        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            IReadOnlyList<RegistrasiListItem> items =
                await new RegistrasiRepository(db).GetByPeriodeAsync(from, to, null, default, KodePoliIgd);

            // Same fields the legacy Key Word box matches (DlgIGD.tampil).
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                bool Has(string? s) => s?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
                items = items.Where(i => Has(i.NoReg) || Has(i.NoRawat) || Has(i.KodeDokter) || Has(i.NamaDokter)
                    || Has(i.NoRekamMedis) || Has(i.NamaPasien) || Has(i.NamaPoli) || Has(i.PenanggungJawab)
                    || Has(i.AlamatPj) || Has(i.HubunganPj) || Has(i.JenisBayar)
                    || i.TanggalRegistrasi?.ToString("yyyy-MM-dd").Contains(keyword) == true).ToList();
            }

            grid.ItemsSource = items;
            this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {items.Count}";
        }
        catch (Exception ex)
        {
            grid.ItemsSource = new List<RegistrasiListItem>();
            this.FindControl<TextBlock>("RecordText")!.Text = "Record : 0";
            ShowStatus($"Gagal memuat registrasi IGD: {ex.Message}", isError: true);
        }
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await LoadAsync();

    private async void OnKeywordKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await LoadAsync();
    }

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<TextBox>("KeywordBox")!.Text = string.Empty;
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        await LoadAsync();
    }

    private void OnToggleInput(object? sender, RoutedEventArgs e)
    {
        var toggle = this.FindControl<ToggleButton>("InputToggle")!;
        this.FindControl<Border>("InputPanel")!.IsVisible = toggle.IsChecked == true;
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        var noRm = this.FindControl<TextBox>("InputNoRmBox")!.Text?.Trim();
        var dokter = this.FindControl<ComboBox>("InputDokterCombo")!.SelectedItem as Dokter;
        var penjab = this.FindControl<ComboBox>("InputPenjabCombo")!.SelectedItem as Penjab;

        if (string.IsNullOrWhiteSpace(noRm) || dokter is null || penjab is null)
        {
            ShowInputStatus("No.Rekam Medik, Dokter, dan Jenis Bayar wajib diisi.", isError: true);
            return;
        }
        if (_menyimpan) return;

        _menyimpan = true;
        try
        {
            var service = new RegistrasiService(DatabaseSettings.Load());
            var pasien = await service.CariPasienAsync(noRm)
                ?? throw new InvalidOperationException($"Pasien {noRm} tidak ditemukan.");
            var user = AppSession.CurrentUser;
            // Isian P.J. yang kosong mengikuti data pasien, seperti isPas() di DlgIGD.
            static string Isi(string? isian, string bawaan) => string.IsNullOrWhiteSpace(isian) ? bawaan : isian.Trim();
            var noRawat = await service.DaftarAsync(
                new RegistrasiBaru(pasien.NoRekamMedis, dokter.KodeDokter, KodePoliIgd, penjab.KodePenjab,
                    Isi(this.FindControl<TextBox>("InputPjBox")!.Text, pasien.NamaKeluarga),
                    Isi(this.FindControl<TextBox>("InputAlamatPjBox")!.Text, pasien.Alamat),
                    pasien.Keluarga, DateTime.Now),
                new RegistrasiAkses(user?.IsAdminUtama == true, user?.HasPermission("edit_registrasi") == true));

            ShowInputStatus($"Registrasi IGD tersimpan — No.Rawat {noRawat}.", isError: false);
            ClearInput();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowInputStatus($"Gagal simpan registrasi IGD: {ex.Message}", isError: true);
        }
        finally
        {
            _menyimpan = false;
        }
    }

    private void OnBaruClick(object? sender, RoutedEventArgs e) => ClearInput();

    private void ClearInput()
    {
        this.FindControl<TextBox>("InputNoRmBox")!.Text = string.Empty;
        this.FindControl<TextBox>("InputPjBox")!.Text = string.Empty;
        this.FindControl<TextBox>("InputAlamatPjBox")!.Text = string.Empty;
    }

    private async void OnCetakClick(object? sender, RoutedEventArgs e)
    {
        var items = (this.FindControl<DataGrid>("IgdGrid")!.ItemsSource as IEnumerable<RegistrasiListItem>)?.ToList()
                    ?? new List<RegistrasiListItem>();
        if (items.Count == 0)
        {
            ShowStatus("Tidak ada data yang bisa dicetak.", isError: true);
            return;
        }

        try
        {
            var kop = await KopInstansi.MuatAsync(DatabaseSettings.Load());
            var path = RegistrasiReport.GeneratePdf(items, "Registrasi IGD", kop);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal membuat/membuka PDF: {ex.Message}", isError: true);
        }
    }

    private void OnPlaceholderClick(object? sender, RoutedEventArgs e)
        => ShowStatus($"\"{(sender as Button)?.Content}\" belum diimplementasikan — mengubah/menghapus registrasi yang sudah ada sengaja ditunda (menyentuh data pasien asli).", isError: true);

    private void OnTriaseClick(object? sender, RoutedEventArgs e) => TriaseRequested?.Invoke(this, EventArgs.Empty);

    private void ShowInputStatus(string message, bool isError)
    {
        var s = this.FindControl<TextBlock>("InputStatusText")!;
        s.Foreground = isError ? Avalonia.Media.Brush.Parse("#DC2626") : Avalonia.Media.Brushes.Green;
        s.Text = message;
        s.IsVisible = true;
    }

    private void ShowStatus(string message, bool isError)
    {
        var s = this.FindControl<TextBlock>("StatusText")!;
        s.Foreground = isError ? Avalonia.Media.Brush.Parse("#DC2626") : Avalonia.Media.Brushes.Green;
        s.Text = message;
        s.IsVisible = true;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
