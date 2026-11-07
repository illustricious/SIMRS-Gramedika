using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatJalan;

public partial class RawatJalanView : UserControl
{
    public event EventHandler? BackRequested;

    /// <summary>Raised from the grid's right-click patient menu — see Modules/Shared/PatientContextMenu.</summary>
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    /// <summary>
    /// Placeholder for "the logged-in user's linked employee record" — the
    /// in-memory dev auth doesn't carry a NIP yet (see docs/audit/HANDOVER.md,
    /// LegacyUserRepository section), so examinations saved from this
    /// dev build are attributed to the one seeded pegawai row.
    /// </summary>
    private const string PlaceholderExaminerNip = "D0000099";

    private RawatJalanQueueItem? _selected;

    public RawatJalanView()
    {
        InitializeComponent();

        this.FindControl<DataGrid>("QueueGrid")!.ContextMenu = PatientContextMenu.Build(
            () => this.FindControl<DataGrid>("QueueGrid")!.SelectedItem is RawatJalanQueueItem i
                ? (i.NoRawat, i.NoRekamMedis, i.NamaPasien) : null,
            request => PatientMenuRequested?.Invoke(this, request));

        var kesadaran = this.FindControl<ComboBox>("KesadaranCombo")!;
        kesadaran.ItemsSource = Enum.GetValues<TingkatKesadaran>();
        kesadaran.SelectedItem = TingkatKesadaran.ComposMentis;
    }

    public async System.Threading.Tasks.Task ReloadAsync() => await LoadQueueAsync();

    private async System.Threading.Tasks.Task LoadQueueAsync()
    {
        var grid = this.FindControl<DataGrid>("QueueGrid")!;

        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new RawatJalanRepository(db);
            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
            var to = DateOnly.FromDateTime(DateTime.Today);
            grid.ItemsSource = await repo.GetQueueAsync(from, to);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<RawatJalanQueueItem>();
        }
    }

    private void OnQueueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = (this.FindControl<DataGrid>("QueueGrid")!.SelectedItem) as RawatJalanQueueItem;

        var header = this.FindControl<TextBlock>("SelectedPatientText")!;
        var simpan = this.FindControl<Button>("SimpanButton")!;

        header.Text = _selected is null
            ? "(belum ada pasien dipilih dari antrian)"
            : $"{_selected.NamaPasien}  —  No. RM {_selected.NoRekamMedis}  —  No. Rawat {_selected.NoRawat}  —  {_selected.NamaPoli}";
        simpan.IsEnabled = _selected is not null;
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        var tensi = this.FindControl<TextBox>("TensiBox")!.Text ?? string.Empty;
        var spo2 = this.FindControl<TextBox>("Spo2Box")!.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(tensi) || string.IsNullOrWhiteSpace(spo2))
        {
            status.Text = "Tensi dan SpO2 wajib diisi (kolom NOT NULL di skema legacy pemeriksaan_ralan).";
            status.IsVisible = true;
            return;
        }

        var examination = new PemeriksaanRalan
        {
            NoRawat = _selected.NoRawat,
            TanggalPerawatan = DateOnly.FromDateTime(DateTime.Now),
            JamRawat = TimeOnly.FromDateTime(DateTime.Now),
            Tensi = tensi,
            Spo2 = spo2,
            Nadi = EmptyToNull(this.FindControl<TextBox>("NadiBox")!.Text),
            SuhuTubuh = EmptyToNull(this.FindControl<TextBox>("SuhuBox")!.Text),
            Respirasi = EmptyToNull(this.FindControl<TextBox>("RespirasiBox")!.Text),
            Tinggi = EmptyToNull(this.FindControl<TextBox>("TinggiBox")!.Text),
            Berat = EmptyToNull(this.FindControl<TextBox>("BeratBox")!.Text),
            Kesadaran = (TingkatKesadaran)this.FindControl<ComboBox>("KesadaranCombo")!.SelectedItem!,
            Keluhan = EmptyToNull(this.FindControl<TextBox>("KeluhanBox")!.Text),
            Pemeriksaan = EmptyToNull(this.FindControl<TextBox>("PemeriksaanBox")!.Text),
            Rtl = this.FindControl<TextBox>("RtlBox")!.Text ?? string.Empty,
            Penilaian = this.FindControl<TextBox>("PenilaianBox")!.Text ?? string.Empty,
            Instruksi = this.FindControl<TextBox>("InstruksiBox")!.Text ?? string.Empty,
            Evaluasi = this.FindControl<TextBox>("EvaluasiBox")!.Text ?? string.Empty,
            Nip = PlaceholderExaminerNip,
        };

        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new RawatJalanRepository(db);
            await repo.SaveExaminationAsync(examination);

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Tersimpan — pemeriksaan untuk {_selected.NamaPasien} pukul {examination.JamRawat:HH:mm}.";
            status.IsVisible = true;

            await LoadQueueAsync(); // examined patient drops out of the queue
            _selected = null;
            this.FindControl<TextBlock>("SelectedPatientText")!.Text = "(belum ada pasien dipilih dari antrian)";
            this.FindControl<Button>("SimpanButton")!.IsEnabled = false;
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal simpan — tidak konek DB atau error: {ex.Message}";
            status.IsVisible = true;
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
