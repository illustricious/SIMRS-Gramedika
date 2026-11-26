using Avalonia.Controls;
using Avalonia.Layout;
using SimrsGramedika.Data;

namespace SimrsGramedika.UI.Modules.Laboratorium;

internal static class LabUi
{
    public static string Title(LabCategory c) => c switch
    {
        LabCategory.PA => "Patologi Anatomi",
        LabCategory.MB => "Mikrobiologi & Bio Molekuler",
        _ => "Patologi Klinis",
    };

    /// <summary>Modal Ya/Tidak prompt — the legacy JOptionPane.showConfirmDialog equivalent.</summary>
    public static async System.Threading.Tasks.Task<bool> ConfirmAsync(Control from, string message)
    {
        var owner = TopLevel.GetTopLevel(from) as Window;
        var yes = new Button { Content = "Ya", MinWidth = 70, HorizontalContentAlignment = HorizontalAlignment.Center };
        var no = new Button { Content = "Tidak", MinWidth = 70, HorizontalContentAlignment = HorizontalAlignment.Center };
        var dialog = new Window
        {
            Title = "Konfirmasi", Width = 420, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16), Spacing = 14,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { yes, no } },
                },
            },
        };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
        if (owner is null) return false;
        return await dialog.ShowDialog<bool>(owner);
    }
}
