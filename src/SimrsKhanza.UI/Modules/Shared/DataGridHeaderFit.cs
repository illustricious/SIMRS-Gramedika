using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace SimrsKhanza.UI.Modules.Shared;

/// <summary>
/// Headers were being cut off ("No.Raw…") because many star-sized columns have a MinWidth
/// smaller than their header text + sort glyph. Registered once at startup: whenever any
/// DataGrid loads, each column's MinWidth is raised to fit its header (the grid scrolls
/// horizontally instead of truncating).
/// </summary>
public static class DataGridHeaderFit
{
    private const double ExtraForPaddingAndSortGlyph = 30;

    public static void Register()
        => DataGrid.LoadedEvent.AddClassHandler<DataGrid>((grid, _) => Fit(grid));

    private static void Fit(DataGrid grid)
    {
        var typeface = new Typeface(grid.FontFamily, FontStyle.Normal, FontWeight.SemiBold);
        foreach (var column in grid.Columns)
        {
            if (column.Header is not string text || text.Length == 0) continue;

            var width = new FormattedText(
                text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, grid.FontSize, Brushes.Black).Width;
            var needed = Math.Ceiling(width + ExtraForPaddingAndSortGlyph);
            if (column.MinWidth < needed) column.MinWidth = needed;
        }
    }
}
