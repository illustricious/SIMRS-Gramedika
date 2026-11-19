using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SimrsKhanza.UI.Controls;

/// <summary>
/// Generic search/pick popup. Avalonia's XAML code-behind can't be a generic
/// class directly, so this window works in terms of `object` internally and
/// exposes a generic static <see cref="ShowAsync{T}"/> entry point — callers
/// never see the object-typing.
/// </summary>
public partial class SearchPickerDialog : Window
{
    private Func<string, Task<IReadOnlyList<object>>>? _search;
    private Func<object, string>? _display;
    private object? _selected;

    public SearchPickerDialog()
    {
        InitializeComponent();
    }

    public static async Task<T?> ShowAsync<T>(
        Window owner,
        string title,
        Func<string, Task<IReadOnlyList<T>>> search,
        Func<T, string> display)
    {
        var dialog = new SearchPickerDialog();
        dialog.FindControl<TextBlock>("TitleText")!.Text = title;
        dialog._search = async query =>
        {
            var results = await search(query);
            return results.Cast<object>().ToList();
        };
        dialog._display = item => display((T)item);

        await dialog.RunInitialSearchAsync();
        await dialog.ShowDialog(owner);

        return dialog._selected is T selected ? selected : default;
    }

    private async Task RunInitialSearchAsync() => await RunSearchAsync(string.Empty);

    private async Task RunSearchAsync(string query)
    {
        if (_search is null || _display is null) return;

        var results = await _search(query);
        var list = this.FindControl<ListBox>("ResultsList")!;
        list.ItemsSource = results.Select(r => new PickerRow(r, _display(r))).ToList();
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        var query = this.FindControl<TextBox>("QueryBox")!.Text ?? string.Empty;
        try { await RunSearchAsync(query); }
        catch (Exception ex) { await Modules.Shared.Dialogs.InfoAsync(this, "Pencarian gagal", ex.Message); }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("ResultsList")!.SelectedItem is PickerRow row)
        {
            _selected = row.Value;
        }
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _selected = null;
        Close();
    }

    private void OnResultDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (this.FindControl<ListBox>("ResultsList")!.SelectedItem is PickerRow row)
        {
            _selected = row.Value;
            Close();
        }
    }

    private record PickerRow(object Value, string DisplayText)
    {
        public override string ToString() => DisplayText;
    }
}
