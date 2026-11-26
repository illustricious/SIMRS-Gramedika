using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SimrsGramedika.UI.Modules.Shared;

/// <summary>Small modal helpers (info / confirm) so feature views don't each build their own Window.</summary>
public static class Dialogs
{
    private static Window? Owner(Control? from) => from is null ? null : TopLevel.GetTopLevel(from) as Window;

    public static async Task InfoAsync(Control from, string title, string message)
    {
        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80 };
        var win = Build(title, message, ok);
        ok.Click += (_, _) => win.Close();
        await Show(win, from);
    }

    public static async Task<bool> ConfirmAsync(Control from, string title, string message)
    {
        var yes = new Button { Content = "Ya", MinWidth = 80 };
        var no = new Button { Content = "Batal", MinWidth = 80 };
        var win = Build(title, message, new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
            Children = { yes, no },
        });
        var result = false;
        yes.Click += (_, _) => { result = true; win.Close(); };
        no.Click += (_, _) => win.Close();
        await Show(win, from);
        return result;
    }

    private static Window Build(string title, string message, Control buttons) => new()
    {
        Title = title,
        Width = 400,
        SizeToContent = SizeToContent.Height,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Content = new StackPanel
        {
            Margin = new Thickness(16), Spacing = 16,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, buttons },
        },
    };

    private static async Task Show(Window win, Control from)
    {
        var owner = Owner(from);
        if (owner is not null) await win.ShowDialog(owner);
        else win.Show();
    }
}
