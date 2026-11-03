namespace SimrsKhanza.Core.Entities;

public enum StatusRegistrasi
{
    Belum,
    Sudah,
    Batal,
    BerkasDiterima,
    Dirujuk,
    Meninggal,
    Dirawat,
    PulangPaksa
}

public enum StatusLanjut
{
    RawatJalan,
    RawatInap
}

/// <summary>
/// Maps to legacy table `reg_periksa` (PK: no_rawat) — one patient visit/encounter.
/// </summary>
public class RegPeriksa
{
    public string NoRawat { get; set; } = string.Empty;
    public DateOnly? TanggalRegistrasi { get; set; }
    public TimeOnly? JamRegistrasi { get; set; }
    public StatusRegistrasi? Status { get; set; }
    public StatusLanjut StatusLanjut { get; set; }

    public string NoRekamMedis { get; set; } = string.Empty;
    public Pasien? Pasien { get; set; }

    public string? KodePoli { get; set; }
    public Poliklinik? Poliklinik { get; set; }

    public string? KodeDokter { get; set; }
    public Dokter? Dokter { get; set; }

    public string KodePenjab { get; set; } = string.Empty;
    public Penjab? Penjab { get; set; }
}
