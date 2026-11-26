using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SimrsGramedika.UI.Modules.About;

public partial class AboutView : UserControl
{
    public event EventHandler? BackRequested;

    public AboutView() => InitializeComponent();

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
