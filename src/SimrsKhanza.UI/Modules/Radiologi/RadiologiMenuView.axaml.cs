using Avalonia.Controls;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Radiologi;

public enum RadiologiAction { Permintaan, Periksa }

public partial class RadiologiMenuView : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler<RadiologiAction>? TileClicked;

    private static readonly (RadiologiAction Action, string Icon, string Label)[] Tiles =
    [
        (RadiologiAction.Permintaan, "📋", "Permintaan Radiologi"),
        (RadiologiAction.Periksa, "🩻", "Periksa Radiologi"),
    ];

    public RadiologiMenuView()
    {
        InitializeComponent();
        MenuTileBuilder.Build(this.FindControl<WrapPanel>("TilePanel")!, Tiles, action => TileClicked?.Invoke(this, action));
    }

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);
}
