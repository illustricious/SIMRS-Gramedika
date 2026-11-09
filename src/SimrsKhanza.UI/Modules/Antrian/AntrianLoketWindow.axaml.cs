using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Antrian;

public partial class AntrianLoketWindow : Window
{
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(5) };
    private AntrianLoketCall? _last;
    private bool _ready;
    private bool _busy;

    public AntrianLoketWindow()
    {
        InitializeComponent();
        _ready = true;

        var combo = this.FindControl<ComboBox>("LoketCombo")!;
        combo.ItemsSource = Enumerable.Range(1, 9).Select(i => i.ToString()).ToList();
        combo.SelectedIndex = 0;

        _poll.Tick += async (_, _) => await PollAsync();
        Opened += async (_, _) =>
        {
            _poll.Start();
            await PollAsync();
        };
        Closed += (_, _) => _poll.Stop();
    }

    private async System.Threading.Tasks.Task PollAsync()
    {
        if (!_ready || _busy) return;
        _busy = true;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var call = await new AntrianRepository(db).GetLoketAsync();
            if (call is null) return;

            this.FindControl<TextBlock>("NomorText")!.Text = call.Antrian.ToString();
            this.FindControl<TextBlock>("LoketText")!.Text = call.Loket.ToString();

            // Same as the legacy display: after showing a call the operator's counter moves to the next number.
            this.FindControl<TextBox>("NomorBox")!.Text = (call.Antrian + 1).ToString();

            if (_last != call) AntrianBoards.PlayBell(1);
            _last = call;
        }
        catch (Exception ex)
        {
            this.FindControl<TextBlock>("StatusText")!.Text = $"Gagal membaca antrian: {ex.Message}";
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnAntriClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        if (!int.TryParse(this.FindControl<TextBox>("NomorBox")!.Text, out var antrian) || antrian < 0)
        {
            status.Text = "No. antrian harus berupa angka.";
            return;
        }

        var loket = int.Parse((string)this.FindControl<ComboBox>("LoketCombo")!.SelectedItem!);
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new AntrianRepository(db).SetLoketAsync(loket, antrian);
            status.Text = $"Loket {loket} memanggil nomor {antrian}.";
        }
        catch (Exception ex)
        {
            status.Text = $"Gagal memanggil: {ex.Message}";
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
        => this.FindControl<TextBox>("NomorBox")!.Text = "1";

    private async void OnStopClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new AntrianRepository(db).ClearLoketAsync();
            _last = null;
            this.FindControl<TextBlock>("NomorText")!.Text = "-";
            this.FindControl<TextBlock>("LoketText")!.Text = "-";
            status.Text = "Panggilan dihentikan.";
        }
        catch (Exception ex)
        {
            status.Text = $"Gagal menghentikan: {ex.Message}";
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
        else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen) WindowState = WindowState.Normal;
    }
}
