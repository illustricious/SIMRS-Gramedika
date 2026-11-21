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
        (FarmasiAction.InputPenjualan, "btnBeriObat", "Input Penjualan Obat & BHP"),
        (FarmasiAction.DataPenjualan, "btnRegistrasi", "Data Penjualan Obat & BHP"),
        (FarmasiAction.DataPenyerahanDarah, "btnPenyerahanDarah", "Data Penyerahan Darah"),
        (FarmasiAction.DaftarResepDokter, "btnResepObat", "Daftar Resep Dokter"),
        (FarmasiAction.NoResep, "btnBarcodeRalan", "No. Resep"),
    ];

    public FarmasiMenuView()
    {
        InitializeComponent();
        MenuTileBuilder.Build(this.FindControl<Grid>("TilePanel")!, Tiles, action => TileClicked?.Invoke(this, action),
            key => DashboardIcons.Create(0, key));
    }

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);
}
