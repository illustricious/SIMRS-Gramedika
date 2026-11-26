using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace SimrsGramedika.UI.Modules.Shared;

/// <summary>
/// Headers were being cut off ("No.Raw…") because many star-sized columns have a MinWidth
/// smaller than their header text + sort glyph. Registered once at startup: whenever any
/// DataGrid loads, each column's MinWidth is raised to fit its header (the grid scrolls
/// horizontally instead of truncating).
/// </summary>
public static class DataGridHeaderFit
{
    private const double ExtraForPaddingAndSortGlyph = 56;

    public static void Register() => DataGrid.LoadedEvent.AddClassHandler<DataGrid>((grid, _) =>
    {
        Fit(grid);
        grid.AutoGeneratingColumn -= FitGeneratedColumn;
        grid.AutoGeneratingColumn += FitGeneratedColumn;
    });

    internal static void Fit(DataGrid grid)
    {
        foreach (var column in grid.Columns) FitColumn(grid, column);
    }

    private static void FitGeneratedColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (sender is DataGrid grid) FitColumn(grid, e.Column);
    }

    private static void FitColumn(DataGrid grid, DataGridColumn column)
    {
        if (column.Header is not string text || text.Length == 0) return;
        var typeface = new Typeface(grid.FontFamily, FontStyle.Normal, FontWeight.SemiBold);
        var width = new FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, grid.FontSize, Brushes.Black).Width;
        var chromeWidth = grid.Classes.Contains("compactColumns") ? 46 : ExtraForPaddingAndSortGlyph;
        var needed = Math.Ceiling(width + chromeWidth);
        if (column.MinWidth < needed) column.MinWidth = needed;
    }
}
