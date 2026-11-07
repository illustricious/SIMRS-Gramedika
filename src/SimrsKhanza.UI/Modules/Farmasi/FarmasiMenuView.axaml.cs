using Avalonia.Controls;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Farmasi;

public enum FarmasiAction
{
    InputPenjualan,
    DataPenjualan,
    DataPenyerahanDarah,
    DaftarResepDokter,
    NoResep,
}

public partial class FarmasiMenuView : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler<FarmasiAction>? TileClicked;

    private static readonly (FarmasiAction Action, string Icon, string Label)[] Tiles =
    [
        (FarmasiAction.InputPenjualan, "💊", "Input Penjualan Obat & BHP"),
        (FarmasiAction.DataPenjualan, "📋", "Data Penjualan Obat & BHP"),
        (FarmasiAction.DataPenyerahanDarah, "🩸", "Data Penyerahan Darah"),
        (FarmasiAction.DaftarResepDokter, "📝", "Daftar Resep Dokter"),
        (FarmasiAction.NoResep, "🔢", "No. Resep"),
    ];

    public FarmasiMenuView()
    {
        InitializeComponent();
        MenuTileBuilder.Build(this.FindControl<WrapPanel>("TilePanel")!, Tiles, action => TileClicked?.Invoke(this, action));
    }

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);
}
