namespace SimrsKhanza.Core.Entities;

public enum GolonganDarahUtd { A, B, Ab, O }
public enum ResusDarah { Negatif, Positif }
public enum StatusKantongDarah { Ada, Diambil, Dimusnahkan }

/// <summary>Maps to legacy table `utd_stok_darah` (PK: no_kantong) — one row per blood bag.</summary>
public class UtdStokDarah
{
    public string NoKantong { get; set; } = string.Empty;
    public string? KodeKomponen { get; set; }
    public GolonganDarahUtd? GolonganDarah { get; set; }
    public ResusDarah? Resus { get; set; }
    public DateOnly? TanggalAftap { get; set; }
    public DateOnly? TanggalKadaluarsa { get; set; }
    public StatusKantongDarah? Status { get; set; }
}
