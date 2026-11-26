using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace SimrsGramedika.UI.Modules.Shared;

/// <summary>
/// Builds evenly sized, full-width option cards for submenu screens.
/// </summary>
public static class MenuTileBuilder
{
    public static void Build<TAction>(Grid panel, (TAction Action, string Icon, string Label)[] tiles, Action<TAction> onClick,
        Func<string, Control>? createIcon = null)
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
                Classes = { createIcon is null ? "labTile" : "clinicalTile" },
                [AutomationProperties.NameProperty] = label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                [Grid.ColumnProperty] = panel.Children.Count,
                Content = $"{icon}\n{label}",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            if (createIcon is not null)
            {
                var symbol = createIcon(icon);
                symbol.HorizontalAlignment = HorizontalAlignment.Center;
                button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                button.Content = new StackPanel
                {
                    Spacing = 9,
                    Children =
                    {
                        symbol,
                        new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                    },
                };
            }
            button.Click += (_, _) => onClick(action);
            panel.Children.Add(button);
        }
    }
}
