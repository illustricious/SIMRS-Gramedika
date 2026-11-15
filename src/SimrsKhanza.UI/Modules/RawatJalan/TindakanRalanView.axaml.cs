using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.RawatJalan;

public partial class TindakanRalanView : UserControl
{
    public event EventHandler? BackRequested;

    /// <summary>"Pemeriksaan" — opens the existing vitals/exam form (RawatJalanView).</summary>
    public event EventHandler? PemeriksaanRequested;

    private static readonly string[] GridNames = ["DokterGrid", "ParamedisGrid", "DokterParamedisGrid"];
    private readonly int[] _counts = new int[3];
    private bool _ready;

    public TindakanRalanView()
    {
        InitializeComponent();
        // Default = today only — same lesson as Registrasi: don't default to a wide range on big tables.
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        _ready = true;
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        var from = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate ?? DateTime.Today);
        var noRm = this.FindControl<TextBox>("NoRmBox")!.Text;
        var keyword = this.FindControl<TextBox>("KeywordBox")!.Text;

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new TindakanRalanRepository(db);
            for (var i = 0; i < 3; i++)
            {
                var rows = await repo.GetAsync((TindakanRalanJenis)i, from, to, noRm, keyword);
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

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
