using Avalonia.Controls;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.MenuA;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Farmasi;

public enum FarmasiAction
{
    InputPenjualan,
    DataPenjualan,
    DataPenyerahanDarah,
    DaftarResepDokter,
    NoResep,
    ResepPulang,
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
        (FarmasiAction.ResepPulang, "btnResepPulang", "Resep Pulang"),
    ];

    public FarmasiMenuView()
    {
        InitializeComponent();
        var bolehResepPulang = AppSession.CurrentUser?.HasPermission(ResepPulangRepository.IzinFarmasi) == true;
        var tiles = Tiles.Where(tile => bolehResepPulang || tile.Action != FarmasiAction.ResepPulang).ToArray();
        MenuTileBuilder.Build(this.FindControl<Grid>("TilePanel")!, tiles, action => TileClicked?.Invoke(this, action),
            key => DashboardIcons.Create(0, key));
    }

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);
}
