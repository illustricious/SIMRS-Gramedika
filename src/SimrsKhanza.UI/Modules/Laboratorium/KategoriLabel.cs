using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.UI.Modules.Laboratorium;

/// <summary>Short PK/PA/MB label for titles — shared by LabPermintaanView/LabPeriksaView.</summary>
public static class KategoriLabel
{
    public static string Short(KategoriLab kategori) => kategori switch
    {
        KategoriLab.PatologiKlinik => "PK",
        KategoriLab.PatologiAnatomi => "PA",
        KategoriLab.Mikrobiologi => "MB",
        _ => "PK",
    };
}
