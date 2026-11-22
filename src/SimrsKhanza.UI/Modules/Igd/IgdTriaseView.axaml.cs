using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Igd;

public partial class IgdTriaseView : UserControl
{
    public event EventHandler? BackRequested;

    /// <summary>Raised from the grid's right-click patient menu — see Modules/Shared/PatientContextMenu.</summary>
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    private IgdAntrianItem? _selected;

    public IgdTriaseView()
    {
        InitializeComponent();

        PatientContextMenu.Attach<IgdAntrianItem>(this.FindControl<DataGrid>("AntrianGrid")!,
            row => new(row.NoRawat, row.NoRekamMedis, row.NamaPasien),
            request => PatientMenuRequested?.Invoke(this, request), PatientMenuKind.EmergencyTriage);

        this.FindControl<ComboBox>("CaraMasukCombo")!.ItemsSource = Enum.GetValues<CaraMasukIgd>();
        this.FindControl<ComboBox>("CaraMasukCombo")!.SelectedItem = CaraMasukIgd.Jalan;

        this.FindControl<ComboBox>("AlatTransportasiCombo")!.ItemsSource = Enum.GetValues<AlatTransportasiIgd>();
        this.FindControl<ComboBox>("AlatTransportasiCombo")!.SelectedItem = AlatTransportasiIgd.Sendiri;

        this.FindControl<ComboBox>("AlasanKedatanganCombo")!.ItemsSource = Enum.GetValues<AlasanKedatanganIgd>();
        this.FindControl<ComboBox>("AlasanKedatanganCombo")!.SelectedItem = AlasanKedatanganIgd.DatangSendiri;
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        await LoadAntrianAsync();
        await LoadKasusAsync();
    }

    private async System.Threading.Tasks.Task LoadAntrianAsync()
    {
        var grid = this.FindControl<DataGrid>("AntrianGrid")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
            var to = DateOnly.FromDateTime(DateTime.Today);
            grid.ItemsSource = await new IgdRepository(db).GetAntrianBelumTriaseAsync(from, to);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<IgdAntrianItem>();
        }
    }

    private async System.Threading.Tasks.Task LoadKasusAsync()
    {
        var combo = this.FindControl<ComboBox>("KasusCombo")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var kasus = await new IgdRepository(db).GetMacamKasusAsync();
            combo.ItemsSource = kasus;
            combo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(MasterTriaseKasus.MacamKasus));
            if (kasus.Count > 0) combo.SelectedIndex = 0;
        }
        catch (Exception)
        {
            combo.ItemsSource = new List<MasterTriaseKasus>();
        }
    }

    private void OnAntrianSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = this.FindControl<DataGrid>("AntrianGrid")!.SelectedItem as IgdAntrianItem;
        this.FindControl<TextBlock>("SelectedPatientText")!.Text = _selected is null
            ? "(belum ada pasien dipilih dari antrian)"
            : $"{_selected.NamaPasien}  —  No.Rawat {_selected.NoRawat}  —  {_selected.NamaPoli}";
        this.FindControl<Button>("SimpanButton")!.IsEnabled = _selected is not null;

        // Picking a patient clearly means "I want to triage them" — auto-open
        // the input panel instead of making the user click the toggle too.
        if (_selected is not null)
        {
            var toggle = this.FindControl<ToggleButton>("InputToggle")!;
            toggle.IsChecked = true;
            this.FindControl<Border>("InputPanel")!.IsVisible = true;
        }
    }

    private void OnToggleInput(object? sender, RoutedEventArgs e)
    {
        var toggle = this.FindControl<ToggleButton>("InputToggle")!;
        this.FindControl<Border>("InputPanel")!.IsVisible = toggle.IsChecked == true;
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        var status = this.FindControl<TextBlock>("StatusText")!;
        var tensi = this.FindControl<TextBox>("TensiBox")!.Text ?? string.Empty;
        var nadi = this.FindControl<TextBox>("NadiBox")!.Text ?? string.Empty;
        var napas = this.FindControl<TextBox>("NapasBox")!.Text ?? string.Empty;
        var suhu = this.FindControl<TextBox>("SuhuBox")!.Text ?? string.Empty;
        var spo2 = this.FindControl<TextBox>("Spo2Box")!.Text ?? string.Empty;
        var nyeri = this.FindControl<TextBox>("NyeriBox")!.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(tensi) || string.IsNullOrWhiteSpace(nadi) || string.IsNullOrWhiteSpace(napas)
            || string.IsNullOrWhiteSpace(suhu) || string.IsNullOrWhiteSpace(spo2) || string.IsNullOrWhiteSpace(nyeri))
        {
            ShowError(status, "Semua tanda vital wajib diisi (kolom NOT NULL di skema legacy data_triase_igd).");
            return;
        }

        var kasus = this.FindControl<ComboBox>("KasusCombo")!.SelectedItem as MasterTriaseKasus;
        if (kasus is null)
        {
            ShowError(status, "Macam Kasus wajib dipilih.");
            return;
        }

        var triase = new TriaseIgd
        {
            NoRawat = _selected.NoRawat,
            TanggalKunjungan = DateTime.Now,
            CaraMasuk = (CaraMasukIgd)this.FindControl<ComboBox>("CaraMasukCombo")!.SelectedItem!,
            AlatTransportasi = (AlatTransportasiIgd)this.FindControl<ComboBox>("AlatTransportasiCombo")!.SelectedItem!,
            AlasanKedatangan = (AlasanKedatanganIgd)this.FindControl<ComboBox>("AlasanKedatanganCombo")!.SelectedItem!,
            KeteranganKedatangan = this.FindControl<TextBox>("KeteranganBox")!.Text ?? string.Empty,
            KodeKasus = kasus.KodeKasus,
            TekananDarah = tensi,
            Nadi = nadi,
            Pernapasan = napas,
            Suhu = suhu,
            SaturasiO2 = spo2,
            Nyeri = nyeri,
        };

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new IgdRepository(db).SaveTriaseAsync(triase);

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Triase {_selected.NamaPasien} tersimpan. Lanjutkan pemeriksaan dokter lewat layar Rawat Jalan.";
            status.IsVisible = true;

            await LoadAntrianAsync(); // triaged patient drops out of this queue
            _selected = null;
            this.FindControl<TextBlock>("SelectedPatientText")!.Text = "(belum ada pasien dipilih dari antrian)";
            this.FindControl<Button>("SimpanButton")!.IsEnabled = false;
            this.FindControl<ToggleButton>("InputToggle")!.IsChecked = false;
            this.FindControl<Border>("InputPanel")!.IsVisible = false;
            foreach (var name in new[] { "TensiBox", "NadiBox", "NapasBox", "SuhuBox", "Spo2Box", "NyeriBox", "KeteranganBox" })
                this.FindControl<TextBox>(name)!.Text = string.Empty;
        }
        catch (Exception ex)
        {
            ShowError(status, $"Gagal simpan triase: {ex.Message}");
        }
    }

    private static void ShowError(TextBlock block, string message)
    {
        block.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
        block.Text = message;
        block.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
