using Avalonia.Controls;
using Avalonia.Layout;

namespace SimrsKhanza.UI.Modules.Shared;

/// <summary>
/// Builds evenly sized, full-width option cards for submenu screens.
/// </summary>
public static class MenuTileBuilder
{
    public static void Build<TAction>(Grid panel, (TAction Action, string Icon, string Label)[] tiles, Action<TAction> onClick)
    {
        panel.ColumnDefinitions.Clear();
        for (var i = 0; i < Math.Max(1, tiles.Length); i++)
            panel.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        panel.Children.Clear();
        panel.RowDefinitions.Clear();
        panel.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        panel.ColumnSpacing = 8;
        foreach (var (action, icon, label) in tiles)
        {
            var button = new Button
            {
                Classes = { "labTile" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                [Grid.ColumnProperty] = panel.Children.Count,
                Content = $"{icon}\n{label}",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            button.Click += (_, _) => onClick(action);
            panel.Children.Add(button);
        }
    }
}
