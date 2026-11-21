using Avalonia.Controls;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Laboratorium;

public enum LabAction
{
    PermintaanPk, PermintaanPa, PermintaanMb,
    PeriksaPk, PeriksaPa, PeriksaMb,
}

public partial class LabMenuView : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler<LabAction>? TileClicked;

    private static readonly (LabAction Action, string Icon, string Label)[] Tiles =
    [
        (LabAction.PermintaanPk, "btnPermintaanLab", "Permintaan Lab PK"),
        (LabAction.PermintaanPa, "btnPermintaanLab", "Permintaan Lab PA"),
        (LabAction.PermintaanMb, "btnPermintaanLab", "Permintaan Lab MB"),
        (LabAction.PeriksaPk, "btnLaboratorium", "Periksa Lab PK"),
        (LabAction.PeriksaPa, "btnLaboratorium", "Periksa Lab PA"),
        (LabAction.PeriksaMb, "btnLaboratorium", "Periksa Lab MB"),
    ];

    public LabMenuView()
    {
        InitializeComponent();

        MenuTileBuilder.Build(this.FindControl<Grid>("TilePanel")!, Tiles, action => TileClicked?.Invoke(this, action),
            key => DashboardIcons.Create(0, key));
    }

    public static KategoriLab KategoriFor(LabAction action) => action switch
    {
        LabAction.PermintaanPk or LabAction.PeriksaPk => KategoriLab.PatologiKlinik,
        LabAction.PermintaanPa or LabAction.PeriksaPa => KategoriLab.PatologiAnatomi,
        _ => KategoriLab.Mikrobiologi,
    };

    public static bool IsPermintaan(LabAction action)
        => action is LabAction.PermintaanPk or LabAction.PermintaanPa or LabAction.PermintaanMb;

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);
}
