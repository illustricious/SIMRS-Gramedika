using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatJalan;

public partial class TindakanRalanView : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;
    public event EventHandler<TindakanRalanSnapshot>? UbahTindakanRequested;

    /// <summary>"Pemeriksaan" — opens the existing vitals/exam form (RawatJalanView).</summary>
    public event EventHandler? PemeriksaanRequested;

    private static readonly string[] GridNames = ["DokterGrid", "ParamedisGrid", "DokterParamedisGrid"];
    private readonly int[] _counts = new int[3];
    private bool _ready;
    private readonly string? _noRawat;

    public TindakanRalanView() : this(null) { }

    internal TindakanRalanView(string? noRawat)
    {
        _noRawat = noRawat;
        InitializeComponent();
        if (noRawat is null)
            foreach (var name in GridNames)
                PatientContextMenu.Attach<TindakanRalanRow>(this.FindControl<DataGrid>(name)!,
                    row => new(row.NoRawat, row.NoRm, row.NamaPasien),
                    request => PatientMenuRequested?.Invoke(this, request));
        else
        {
            foreach (var name in new[] { "Heading", "Filters", "Actions" })
                this.FindControl<Control>(name)!.IsVisible = false;
            this.FindControl<Control>("VisitActions")!.IsVisible = AppSession.CurrentUser?.HasPermission(TindakanRalanInputService.Izin) == true;
        }

        // Default = today only — same lesson as Registrasi: don't default to a wide range on big tables.
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        _ready = true;
    }

    /// <summary>Hanya daftar satu jenis penanganan, untuk tab di halaman kunjungan.</summary>
    internal void HanyaJenis(TindakanRalanJenis jenis)
    {
        var tabs = this.FindControl<TabControl>("Tabs")!;
        for (var i = 0; i < tabs.Items.Count; i++) ((TabItem)tabs.Items[i]!).IsVisible = i == (int)jenis;
        tabs.SelectedIndex = (int)jenis;
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var from = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate ?? DateTime.Today);
        var noRm = this.FindControl<TextBox>("NoRmBox")!.Text;
        var keyword = this.FindControl<TextBox>("KeywordBox")!.Text;

        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new TindakanRalanRepository(db);
            for (var i = 0; i < 3; i++)
            {
                var rows = await repo.GetAsync((TindakanRalanJenis)i, from, to, noRm, keyword, noRawat: _noRawat);
                this.FindControl<DataGrid>(GridNames[i])!.ItemsSource = rows;
                _counts[i] = rows.Count;
            }
            this.FindControl<TextBlock>("StatusText")!.IsVisible = false;
        }
        catch (Exception ex)
        {
            var status = this.FindControl<TextBlock>("StatusText")!;
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal memuat data tindakan: {ex.Message}";
            status.IsVisible = true;
        }
        UpdateRecord();
    }

    private void UpdateRecord()
    {
        var idx = Math.Max(0, this.FindControl<TabControl>("Tabs")!.SelectedIndex);
        this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {_counts[idx]}";
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged fires once during InitializeComponent, before the name scope exists.
        if (_ready && e.Source is TabControl) UpdateRecord();
    }

    private async void OnFilterKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await ReloadAsync();
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<TextBox>("NoRmBox")!.Text = string.Empty;
        this.FindControl<TextBox>("KeywordBox")!.Text = string.Empty;
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        await ReloadAsync();
    }

    private void OnPemeriksaanClick(object? sender, RoutedEventArgs e) => PemeriksaanRequested?.Invoke(this, EventArgs.Empty);

    private async void OnUbahTindakanClick(object? sender, RoutedEventArgs e)
    {
        var user = AppSession.CurrentUser;
        var index = this.FindControl<TabControl>("Tabs")!.SelectedIndex;
        if (_noRawat is null || user?.HasPermission(TindakanRalanInputService.Izin) != true || index < 0 || index >= GridNames.Length) return;
        if (this.FindControl<DataGrid>(GridNames[index])!.SelectedItem is not TindakanRalanRow row || row.NoRawat != _noRawat ||
            !DateTime.TryParseExact(row.TglRawat, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var tanggal) || !TimeSpan.TryParse(row.JamRawat, out var jam))
        {
            await Dialogs.InfoAsync(this, "Ubah tindakan", "Pilih satu tindakan dengan tanggal dan jam yang valid.");
            return;
        }
        var button = this.FindControl<Button>("UbahTindakanButton")!;
        button.IsEnabled = false;
        try
        {
            var snapshot = await new TindakanRalanInputService(DatabaseSettings.Load(), _noRawat, user)
                .MuatTindakanAsync((TindakanRalanJenis)index, row.Kode, row.KodeDokter, row.Nip, tanggal + jam);
            UbahTindakanRequested?.Invoke(this, snapshot);
        }
        catch (Exception) { await Dialogs.InfoAsync(this, "Ubah tindakan", "Tindakan tidak dapat dimuat. Muat ulang daftar."); }
        finally { button.IsEnabled = true; }
    }

    private async void OnHapusTindakanClick(object? sender, RoutedEventArgs e)
    {
        var user = AppSession.CurrentUser;
        var index = this.FindControl<TabControl>("Tabs")!.SelectedIndex;
        if (_noRawat is null || user?.HasPermission(TindakanRalanInputService.Izin) != true || index < 0 || index >= GridNames.Length) return;
        if (this.FindControl<DataGrid>(GridNames[index])!.SelectedItem is not TindakanRalanRow row ||
            row.NoRawat != _noRawat || !DateTime.TryParseExact(row.TglRawat, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var tanggal) || !TimeSpan.TryParse(row.JamRawat, out var jam))
        {
            await Dialogs.InfoAsync(this, "Hapus tindakan", "Pilih satu tindakan dengan tanggal dan jam yang valid.");
            return;
        }
        var button = this.FindControl<Button>("HapusTindakanButton")!;
        button.IsEnabled = false;
        try
        {
            var service = new TindakanRalanInputService(DatabaseSettings.Load(), _noRawat, user);
            var snapshot = await service.MuatTindakanAsync((TindakanRalanJenis)index, row.Kode, row.KodeDokter, row.Nip, tanggal + jam);
            if (!await Dialogs.ConfirmAsync(this, "Hapus tindakan", $"Hapus {row.Tindakan} pada {row.TglRawat} {row.JamRawat}? Jurnal pembalik akan dicatat.")) return;
            await service.HapusAsync(snapshot);
            await ReloadAsync();
            await Dialogs.InfoAsync(this, "Hapus tindakan", "Tindakan dihapus dan jurnal pembalik dicatat.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            await Dialogs.InfoAsync(this, "Hapus tindakan", ex.Message);
        }
        catch (Exception)
        {
            await Dialogs.InfoAsync(this, "Hapus tindakan", "Tindakan belum dapat dihapus. Periksa koneksi dan muat ulang daftar.");
        }
        finally { button.IsEnabled = true; }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
