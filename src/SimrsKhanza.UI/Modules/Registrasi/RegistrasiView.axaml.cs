using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Registrasi;

public partial class RegistrasiView : UserControl
{
    /// <summary>Raised when "Kembali ke Menu" is clicked — MainWindow swaps back to the dashboard.</summary>
    public event EventHandler? BackRequested;

    public RegistrasiView()
    {
        InitializeComponent();

        // Periode pickers start empty (no default range) so the grid shows
        // ALL registrations on first load — per user feedback, the bottom
        // list should show every patient/registration, not be hidden behind
        // a date filter the user hasn't touched yet. "Periode" is purely an
        // optional narrowing filter the user applies with "Cari / Semua".
    }

    /// <summary>Called by MainWindow each time this page becomes visible.</summary>
    public async System.Threading.Tasks.Task ReloadAsync() => await LoadAsync();

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

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
