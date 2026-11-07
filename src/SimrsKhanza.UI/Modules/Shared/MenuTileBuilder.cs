using Avalonia.Controls;
using Avalonia.Layout;

namespace SimrsKhanza.UI.Modules.Shared;

/// <summary>
/// Builds the icon-tile grid shared by every submenu screen (LabMenuView,
/// RadiologiMenuView, FarmasiMenuView, LaporanMenuView, …). Previously each
/// one hardcoded the same 150x110 "labTile" size regardless of how many
/// tiles it had — fine for 6 tiles, but left 2-tile screens (Radiologi)
/// looking tiny and lost in a mostly-empty panel. Tile size now scales with
/// item count instead.
/// </summary>
public static class MenuTileBuilder
{
    public static void Build<TAction>(WrapPanel panel, (TAction Action, string Icon, string Label)[] tiles, Action<TAction> onClick)
    {
        var (width, height, fontSize) = tiles.Length switch
        {
            <= 2 => (260d, 190d, 16d),
            <= 4 => (190d, 150d, 14d),
            _ => (150d, 110d, 13d),
        };

        panel.Children.Clear();
        foreach (var (action, icon, label) in tiles)
        {
            var button = new Button
            {
                Classes = { "labTile" },
                Width = width,
                Height = height,
                FontSize = fontSize,
                Content = $"{icon}\n{label}",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            button.Click += (_, _) => onClick(action);
            panel.Children.Add(button);
        }
    }
}
