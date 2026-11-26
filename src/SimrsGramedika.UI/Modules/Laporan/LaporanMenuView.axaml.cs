using Avalonia.Controls;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Laporan;

public enum LaporanAction { Kunjungan, Pendapatan, PenjualanFarmasi }

public partial class LaporanMenuView : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler<LaporanAction>? TileClicked;

    private static readonly (LaporanAction Action, string Icon, string Label)[] Tiles =
    [
        (LaporanAction.Kunjungan, "📅", "Laporan Kunjungan"),
        (LaporanAction.Pendapatan, "💰", "Laporan Pendapatan"),
        (LaporanAction.PenjualanFarmasi, "💊", "Laporan Penjualan Farmasi"),
    ];

    public LaporanMenuView()
    {
        InitializeComponent();
        MenuTileBuilder.Build(this.FindControl<Grid>("TilePanel")!, Tiles, action => TileClicked?.Invoke(this, action));
    }

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);
}
