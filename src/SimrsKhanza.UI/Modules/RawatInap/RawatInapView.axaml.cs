using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatInap;

public partial class RawatInapView : UserControl
{
    public event EventHandler? BackRequested;

    /// <summary>Raised from the grid's right-click patient menu — see Modules/Shared/PatientContextMenu.</summary>
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    private static readonly StatusPulang[] DischargeOptions =
        [StatusPulang.Sembuh, StatusPulang.Membaik, StatusPulang.Rujuk, StatusPulang.PulangPaksa, StatusPulang.Meninggal, StatusPulang.Lainnya];

    private RawatInapAktifItem? _selected;

    public RawatInapView()
    {
        InitializeComponent();

        this.FindControl<DataGrid>("AktifGrid")!.ContextMenu = PatientContextMenu.Build(
            () => this.FindControl<DataGrid>("AktifGrid")!.SelectedItem is RawatInapAktifItem i
                ? (i.NoRawat, i.NoRekamMedis, i.NamaPasien) : null,
            request => PatientMenuRequested?.Invoke(this, request));

        var statusCombo = this.FindControl<ComboBox>("StatusPulangCombo")!;
        statusCombo.ItemsSource = DischargeOptions;
        statusCombo.SelectedItem = StatusPulang.Sembuh;
    }

    /// <summary>Called from the right-click "Kamar Inap" patient menu (Modules/Shared/PatientContextMenu) — prefills the Admit form's No.Rawat and opens the input panel.</summary>
    public void PrefillAdmit(string noRawat)
    {
        this.FindControl<TextBox>("AdmitNoRawatBox")!.Text = noRawat;
        this.FindControl<ToggleButton>("InputToggle")!.IsChecked = true;
        this.FindControl<Border>("InputPanel")!.IsVisible = true;
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        await LoadAktifAsync();
        await LoadKamarKosongAsync();
    }

    private async System.Threading.Tasks.Task LoadAktifAsync()
    {
        var grid = this.FindControl<DataGrid>("AktifGrid")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            grid.ItemsSource = await new RawatInapRepository(db).GetActiveInpatientsAsync();
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<RawatInapAktifItem>();
        }
    }

    private async System.Threading.Tasks.Task LoadKamarKosongAsync()
    {
        var combo = this.FindControl<ComboBox>("KamarCombo")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var kamar = await new RawatInapRepository(db).GetAvailableKamarAsync();
            combo.ItemsSource = kamar;
            combo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(KamarTersediaItem.KodeKamar));
            if (kamar.Count > 0) combo.SelectedIndex = 0;
        }
        catch (Exception)
        {
            combo.ItemsSource = new List<KamarTersediaItem>();
        }
    }

    private void OnAktifSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = this.FindControl<DataGrid>("AktifGrid")!.SelectedItem as RawatInapAktifItem;

        var header = this.FindControl<TextBlock>("SelectedPatientText")!;
        header.Text = _selected is null
            ? "(belum ada pasien dipilih dari daftar)"
            : $"{_selected.NamaPasien}  —  No. Rawat {_selected.NoRawat}  —  Kamar {_selected.KodeKamar}";
        this.FindControl<Button>("PulangkanButton")!.IsEnabled = _selected is not null;

        if (_selected is not null)
        {
            this.FindControl<ToggleButton>("InputToggle")!.IsChecked = true;
            this.FindControl<Border>("InputPanel")!.IsVisible = true;
        }
    }

    private void OnToggleInput(object? sender, RoutedEventArgs e)
    {
        var toggle = this.FindControl<ToggleButton>("InputToggle")!;
        this.FindControl<Border>("InputPanel")!.IsVisible = toggle.IsChecked == true;
    }

    private async void OnAdmitClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("AdmitStatusText")!;
        var noRawat = this.FindControl<TextBox>("AdmitNoRawatBox")!.Text?.Trim();
        var kamar = this.FindControl<ComboBox>("KamarCombo")!.SelectedItem as KamarTersediaItem;
        var diagnosa = EmptyToNull(this.FindControl<TextBox>("DiagnosaAwalBox")!.Text);

        if (string.IsNullOrWhiteSpace(noRawat) || kamar is null)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = "No. Rawat dan Kamar wajib diisi.";
            status.IsVisible = true;
            return;
        }

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new RawatInapRepository(db).AdmitAsync(noRawat, kamar.KodeKamar, diagnosa);

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Pasien {noRawat} berhasil di-admit ke kamar {kamar.KodeKamar}.";
            status.IsVisible = true;

            this.FindControl<TextBox>("AdmitNoRawatBox")!.Text = string.Empty;
            this.FindControl<TextBox>("DiagnosaAwalBox")!.Text = string.Empty;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal admit: {ex.Message}";
            status.IsVisible = true;
        }
    }

    private async void OnPulangkanClick(object? sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        var status = this.FindControl<TextBlock>("PulangkanStatusText")!;
        var statusPulang = (StatusPulang)this.FindControl<ComboBox>("StatusPulangCombo")!.SelectedItem!;
        var diagnosaAkhir = EmptyToNull(this.FindControl<TextBox>("DiagnosaAkhirBox")!.Text);

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var total = await new RawatInapRepository(db).DischargeAsync(_selected.NoRawat, statusPulang, diagnosaAkhir);

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"{_selected.NamaPasien} dipulangkan. Biaya kamar: Rp {total:N0} (lihat Kasir untuk total tagihan).";
            status.IsVisible = true;

            _selected = null;
            this.FindControl<TextBlock>("SelectedPatientText")!.Text = "(belum ada pasien dipilih dari daftar)";
            this.FindControl<Button>("PulangkanButton")!.IsEnabled = false;
            this.FindControl<TextBox>("DiagnosaAkhirBox")!.Text = string.Empty;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal memulangkan: {ex.Message}";
            status.IsVisible = true;
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
