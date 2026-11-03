namespace SimrsKhanza.Core.Entities;

public enum JenisKelamin
{
    LakiLaki,
    Perempuan
}

public enum GolonganDarah
{
    A,
    B,
    O,
    AB,
    TidakDiketahui
}

public enum StatusPernikahan
{
    BelumMenikah,
    Menikah,
    Janda,
    Duda
}

/// <summary>
/// Maps to legacy table `pasien` (PK: no_rkm_medis).
/// </summary>
public class Pasien
{
    public string NoRekamMedis { get; set; } = string.Empty;
    public string NamaPasien { get; set; } = string.Empty;
    public string? NoKtp { get; set; }
    public JenisKelamin JenisKelamin { get; set; }
    public string? TempatLahir { get; set; }
    public DateOnly? TanggalLahir { get; set; }
    public string NamaIbu { get; set; } = string.Empty;
    public string? Alamat { get; set; }
    public GolonganDarah? GolonganDarah { get; set; }
    public string? Pekerjaan { get; set; }
    public StatusPernikahan? StatusPernikahan { get; set; }
    public string? Agama { get; set; }
    public DateOnly? TanggalDaftar { get; set; }
    public string? NoTelepon { get; set; }

    public string KodePenjab { get; set; } = string.Empty;
    public Penjab? Penjab { get; set; }
}
