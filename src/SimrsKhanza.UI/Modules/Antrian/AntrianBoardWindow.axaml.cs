using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SimrsKhanza.Config;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Pengaturan;

namespace SimrsKhanza.UI.Modules.Antrian;

public partial class AntrianBoardWindow : Window
{
    private static readonly CultureInfo Id = new("id-ID");

    private readonly AntrianBoardKind _kind;
    private readonly string? _kdPoli;
    private readonly string? _kdDokter;
    private readonly DispatcherTimer _refresh = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    private IReadOnlyList<AntrianBoardRow> _rows = Array.Empty<AntrianBoardRow>();
    private string _builtColumns = "";
    private bool _ready;
    private bool _loading;
    private DateTime _nextBannerRefresh;
    private byte[]? _bannerBytes;
    private Bitmap? _bannerBitmap;

    public AntrianBoardWindow() : this(AntrianBoardKind.Radiologi)
    {
    }

    public AntrianBoardWindow(AntrianBoardKind kind, string? kdPoli = null, string? kdDokter = null)
    {
        _kind = kind;
        _kdPoli = kdPoli;
        _kdDokter = kdDokter;

        InitializeComponent();
        _ready = true;

        // poli.php refreshes every 60s, antrian.php every 10s; the table/judul pages poll in seconds.
        _refresh.Interval = kind switch
        {
            AntrianBoardKind.JadwalPoli => TimeSpan.FromSeconds(60),
            AntrianBoardKind.PoliDokter => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(4),
        };
        _refresh.Tick += async (_, _) => await LoadAsync();
        _clock.Tick += (_, _) => UpdateClock();

        UpdateClock();
        Opened += async (_, _) =>
        {
            _clock.Start();
            _refresh.Start();
            await LoadAsync();
        };
        Closed += (_, _) =>
        {
            _clock.Stop();
            _refresh.Stop();
            _bannerBitmap?.Dispose();
            _ready = false;
        };
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        this.FindControl<TextBlock>("ClockText")!.Text = now.ToString("HH:mm:ss", Id);
        this.FindControl<TextBlock>("DateText")!.Text = now.ToString("dddd, dd MMMM yyyy", Id);
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (!_ready || _loading) return;
        _loading = true;
        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var snap = await new AntrianRepository(db).LoadAsync(_kind, _kdPoli, _kdDokter);
            Apply(snap);
            this.FindControl<TextBlock>("StatusText")!.Text =
                $"Diperbarui {DateTime.Now:HH:mm:ss} — otomatis tiap {(int)_refresh.Interval.TotalSeconds} detik";
            await MuatBannerAsync(settings);
        }
        catch (Exception ex)
        {
            this.FindControl<TextBlock>("StatusText")!.Text = $"Gagal memuat data: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task MuatBannerAsync(DatabaseSettings settings)
    {
        var apotek = _kind is AntrianBoardKind.Apotek2 or AntrianBoardKind.Apotek2Racikan or AntrianBoardKind.Apotek2NonRacikan;
        if ((!apotek && _kind is not (AntrianBoardKind.JadwalPoli or AntrianBoardKind.PoliDokter))
            || DateTime.UtcNow < _nextBannerRefresh) return;
        _nextBannerRefresh = DateTime.UtcNow.AddMinutes(1);
        var panel = this.FindControl<Border>("BannerPanel")!;
        try
        {
            var banner = await new MenuURepository(settings).MuatDisplayAsync(apotek);
            if (!_ready) return;
            var image = this.FindControl<Image>("BannerImage")!;
            var bytes = banner?.Aktif == true ? banner.Gambar ?? [] : [];
            if (_bannerBytes is null || !_bannerBytes.AsSpan().SequenceEqual(bytes))
            {
                _bannerBitmap?.Dispose();
                _bannerBitmap = null;
                if (bytes.Length > 0)
                {
                    try { _bannerBitmap = new Bitmap(new MemoryStream(bytes, writable: false)); }
                    catch (ArgumentException) { /* Some legacy display images are invalid. Keep the announcement text. */ }
                }
                _bannerBytes = bytes;
            }
            image.Source = _bannerBitmap;
            image.IsVisible = _bannerBitmap is not null;
            var text = this.FindControl<TextBlock>("BannerText")!;
            text.Text = banner?.Teks;
            panel.IsVisible = banner?.Aktif == true && (_bannerBitmap is not null || !string.IsNullOrWhiteSpace(banner.Teks));
        }
        catch
        {
            // Optional banner settings must not interrupt live queue updates.
            panel.IsVisible = false;
        }
    }

    private void Apply(AntrianBoardSnapshot snap)
    {
        Title = snap.Title;
        this.FindControl<TextBlock>("InstansiText")!.Text = snap.Instansi;
        this.FindControl<TextBlock>("AlamatText")!.Text = snap.AlamatInstansi;
        this.FindControl<TextBlock>("TitleText")!.Text = snap.Title;
        this.FindControl<TextBlock>("SubtitleText")!.Text = snap.Subtitle;

        var grid = this.FindControl<DataGrid>("BoardGrid")!;
        var signature = string.Join("|", snap.Columns);
        if (signature != _builtColumns)
        {
            grid.Columns.Clear();
            for (var i = 0; i < snap.Columns.Count; i++)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = snap.Columns[i],
                    Binding = new Binding($"[{i}]"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                });
            }
            _builtColumns = signature;
        }

        _rows = snap.Rows;
        grid.ItemsSource = snap.Rows.Select(r => r.Cells.ToArray()).ToList();
        this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {snap.Rows.Count}";

        this.FindControl<ItemsControl>("CallItems")!.ItemsSource = snap.Calls;
        this.FindControl<Border>("CallPanel")!.IsVisible = snap.Calls.Count > 0;

        foreach (var call in snap.Calls.Where(c => c.Ring))
            AntrianBoards.PlayBell(call.Sound);
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_kind != AntrianBoardKind.JadwalPoli) return;
        var idx = this.FindControl<DataGrid>("BoardGrid")!.SelectedIndex;
        if (idx < 0 || idx >= _rows.Count) return;
        var row = _rows[idx];
        if (row.Key1 is not null && row.Key2 is not null)
            AntrianBoards.OpenPoliDokter(row.Key1, row.Key2);
    }

    private void OnFullscreenClick(object? sender, RoutedEventArgs e) => ToggleFullscreen();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) ToggleFullscreen();
        else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen) WindowState = WindowState.Normal;
    }

    private void ToggleFullscreen()
        => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
}
