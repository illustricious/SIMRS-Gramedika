using SimrsGramedika.Core.Entities;

namespace SimrsGramedika.UI.Modules.Pasien;

/// <summary>Satu pilihan combo: nilai yang disimpan dan tulisan seperti di Khanza.</summary>
internal sealed record Opsi<T>(T Nilai, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Pilihan tetap identitas pasien (kolom enum di tabel pasien), dipakai layar Pasien dan dialog Pasien Baru.</summary>
internal static class PilihanIdentitas
{
    public static readonly IReadOnlyList<Opsi<GolonganDarah>> GolDarah =
    [
        new(GolonganDarah.TidakDiketahui, "-"), new(GolonganDarah.A, "A"), new(GolonganDarah.B, "B"),
        new(GolonganDarah.AB, "AB"), new(GolonganDarah.O, "O"),
    ];

    public static readonly IReadOnlyList<Opsi<StatusPernikahan>> StatusNikah =
    [
        new(StatusPernikahan.Menikah, "MENIKAH"), new(StatusPernikahan.BelumMenikah, "BELUM MENIKAH"),
        new(StatusPernikahan.Janda, "JANDA"), new(StatusPernikahan.Duda, "DUDHA"),
    ];

    public static readonly IReadOnlyList<Opsi<HubunganPenanggungJawab>> Hubungan =
    [
        new(HubunganPenanggungJawab.DiriSendiri, "DIRI SENDIRI"), new(HubunganPenanggungJawab.Ayah, "AYAH"),
        new(HubunganPenanggungJawab.Ibu, "IBU"), new(HubunganPenanggungJawab.Istri, "ISTRI"),
        new(HubunganPenanggungJawab.Suami, "SUAMI"), new(HubunganPenanggungJawab.Saudara, "SAUDARA"),
        new(HubunganPenanggungJawab.Anak, "ANAK"), new(HubunganPenanggungJawab.LainLain, "LAIN-LAIN"),
    ];

    public static readonly IReadOnlyList<string> Agama =
        ["ISLAM", "KRISTEN", "KATOLIK", "HINDU", "BUDHA", "KONG HU CHU", "-"];
}
