using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;

namespace SimrsKhanza.UI.Modules.Shared;

internal static class DashboardIcons
{
    private static readonly Geometry[] Categories =
    [
        Geometry.Parse("M5,19 V6 H19 V19 M3,19 H21 M12,8 V15 M8.5,11.5 H15.5"),
        Geometry.Parse("M4,5 H2 V19 H4 M20,5 H22 V19 H20 M6,7 V17 M9,7 V17 M13,7 V17 M16,7 V17 M18,7 V17"),
        Geometry.Parse("M12,12 A3,3 0 1 0 12,6 A3,3 0 0 0 12,12 M5,20 V18 C5,15 8,14 12,14 C16,14 19,15 19,18 V20"),
        Geometry.Parse("M4,9 L9,4 A4,4 0 0 1 15,4 L20,9 A4,4 0 0 1 20,15 L15,20 A4,4 0 0 1 9,20 L4,15 A4,4 0 0 1 4,9 Z M7,7 L17,17"),
        Geometry.Parse("M4,7 L12,3 L20,7 V17 L12,21 L4,17 Z M4,7 L12,11 L20,7 M12,11 V21 M8,5 L16,9"),
        Geometry.Parse("M5,4 V9 Q5,12 8,12 Q11,12 11,9 V4 M8,4 V20 M17,20 V4 Q14,7 14,12 H17"),
        Geometry.Parse("M4,20 V7 H20 V20 M2,20 H22 M7,7 V4 H17 V7 M7,11 H9 M11,11 H13 M15,11 H17 M7,15 H9 M15,15 H17 M11,20 V16 H13 V20"),
        Geometry.Parse("M4,21 V3 H20 V21 Z M9,17 V7 H13 A2.5,2.5 0 0 1 13,12 H9"),
        Geometry.Parse("M6,3 H18 V21 L15,19 L12,21 L9,19 L6,21 Z M9,8 H15 M9,12 H15 M9,16 H12"),
        Geometry.Parse("M5,20 V4 H19 V20 Z M8,16 V13 M12,16 V9 M16,16 V11"),
        Geometry.Parse("M4,8 L12,4 L20,8 M4,9 H20 M6,10 V17 M12,10 V17 M18,10 V17 M4,19 H20"),
        Geometry.Parse("M8,8 L6,6 A3,3 0 0 0 2,10 L6,14 A3,3 0 0 0 10,14 L12,12 M12,12 L14,10 A3,3 0 0 1 18,10 L22,14 A3,3 0 0 1 18,18 L16,16"),
        Geometry.Parse("M5,6 H9 L11,8 H20 V20 H4 V6 Z M12,11 V17 M9,14 H15"),
        Geometry.Parse("M12,3 C10,7 5,11 5,15 A7,7 0 0 0 19,15 C19,11 14,7 12,3 Z M9,15 Q9,17 11,18"),
        Geometry.Parse("M4,4 H11 V11 H4 Z M14,4 H20 V11 H14 Z M4,14 H11 V20 H4 Z M14,14 H20 V20 H14 Z"),
        Geometry.Parse("M3,6 H21 V18 H3 Z M3,7 L12,13 L21,7"),
        Geometry.Parse("M12,6 Q8,3 3,5 V18 Q8,16 12,19 Q16,16 21,18 V5 Q16,3 12,6 Z M12,6 V19"),
        Geometry.Parse("M12,3 L20,6 V12 Q20,17 12,21 Q4,17 4,12 V6 Z M8,12 L11,15 L16,9"),
        Geometry.Parse("M4,9 H20 L19,21 H5 Z M8,9 V7 A4,4 0 0 1 16,7 V9"),
        Geometry.Parse("M12,20 L4,12 A4,4 0 0 1 10,6 L12,8 L14,6 A4,4 0 0 1 20,12 Z"),
        Geometry.Parse("M4,6 H20 M4,12 H20 M4,18 H20 M8,4 V8 M16,10 V14 M10,16 V20"),
    ];

    private static readonly Geometry Car = Geometry.Parse("M4,12 L6,7 H18 L20,12 V18 H4 Z M4,12 H20 M7,15 H8 M16,15 H17 M6,18 V20 M18,18 V20");
    private static readonly Geometry Bed = Geometry.Parse("M3,7 V20 M21,20 V15 Q21,12 18,12 H10 V17 M3,17 H21 M6,12 A2,2 0 1 0 6,8 A2,2 0 0 0 6,12 Z");
    private static readonly Geometry Calendar = Geometry.Parse("M4,6 H20 V20 H4 Z M8,4 V8 M16,4 V8 M4,10 H20 M8,14 H10 M14,14 H16 M8,17 H10");
    private static readonly Geometry Pulse = Geometry.Parse("M3,13 H7 L10,6 L14,18 L17,11 H21");
    private static readonly Geometry Lab = Geometry.Parse("M9,4 H15 M10,4 V10 L5,18 Q4,20 7,20 H17 Q20,20 19,18 L14,10 V4 M8,15 H16");
    private static readonly Geometry Microscope = Geometry.Parse("M8,3 H12 V6 H8 Z M9,6 V12 H11 V6 M12,8 Q18,8 18,14 Q18,18 13,18 H8 M5,21 H20 M10,16 H5");
    private static readonly Geometry Imaging = Geometry.Parse("M4,8 V4 H8 M16,4 H20 V8 M20,16 V20 H16 M8,20 H4 V16 M8,8 L16,16 M16,8 L8,16");
    private static readonly Geometry ReferralOut = Geometry.Parse("M12,5 H4 V19 H12 M8,12 H21 M17,8 L21,12 L17,16");
    private static readonly Geometry ReferralIn = Geometry.Parse("M12,5 H20 V19 H12 M16,12 H3 M7,8 L3,12 L7,16");
    private static readonly Geometry Clipboard = Geometry.Parse("M8,5 H5 V21 H19 V5 H16 M8,3 H16 V7 H8 Z M8,12 H16 M8,16 H13");

    public static Border Create(int category, string key)
    {
        var geometry = key switch
        {
            "btnBarcodeParkir" or "btnBarcodeRalan" => Categories[1],
            "btnParkirMasuk" => Car,
            "btnAnalisaKamar" or "btnPermintaanRanap" or "btnKamarInap" => Bed,
            "BtnJadwal" or "btnJadwalOperasi" or "btnBookingPeriksa" or "btnBookingRegistrasi" or "btnBookingMCUPerusahaan" => Calendar,
            "btnIGD" or "btnRalan" or "btnRanap" or "btnTagihanOperasi" or "btnPermintaanLayananProgramKFR" => Pulse,
            "btnPermintaanLab" or "btnPermintaanLabPA" or "btnPermintaanLabMB" => Lab,
            "btnLaboratorium" or "btnLaboratoriumPA" or "btnLaboratoriumMB" => Microscope,
            "btnPermintaanRadiologi" or "btnPeriksaRadiologi" => Imaging,
            "btnRujukPasien" => ReferralOut,
            "btnRujukMasuk" => ReferralIn,
            "btnRegistrasi" or "btnSkriningRawatJalan" or "btnDeteksiDiniCorona" => Clipboard,
            "btnBeriObat" or "btnResepPulang" or "btnResepObat" => Categories[3],
            "BtnDpjp" => Categories[2],
            "btnDiet" => Categories[5],
            "btnPenyerahanDarah" => Categories[13],
            "btnKasir" or "btnDeposit" or "btnPerkiraanBiayaRanap" => Categories[8],
            _ => Categories[category],
        };
        var path = Ui.Themed(new Path
        {
            Data = geometry, Width = 21, Height = 21, StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        }, Shape.StrokeProperty, "ThemeIconBrush");
        return Ui.Themed(new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Left, Child = path,
        }, Border.BackgroundProperty, "ThemeIconBackgroundBrush");
    }
}
