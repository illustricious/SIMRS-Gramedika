namespace SimrsGramedika.Core.Entities;

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

public enum StatusPendaftaran
{
    Lama,
    Baru,
}

public enum StatusBayar
{
    SudahBayar,
    BelumBayar,
}

/// <summary>
/// Maps to legacy table `reg_periksa` (PK: no_rawat) — one patient visit/encounter.
/// Field set widened to match the "Daftar Pasien Rawat Jalan" screenshot the
/// user shared (columns: Kode Dokter, Dokter Dituju, No.RM, Pasien, Poliklinik,
/// Penanggung Jawab, Alamat P.J., Hubungan P.J., Biaya Reg, Jenis Bayar,
/// Status, No.Rawat, Tanggal, Jam, No.Reg, Status Bayar, Stts Poli, No.Telp Pasien).
/// </summary>
public class RegPeriksa
{
    public string NoRawat { get; set; } = string.Empty;
    public string? NoReg { get; set; }
    public DateOnly? TanggalRegistrasi { get; set; }
    public TimeOnly? JamRegistrasi { get; set; }
    public StatusRegistrasi? Status { get; set; }
    public StatusLanjut StatusLanjut { get; set; }
    public StatusPendaftaran? StatusPoli { get; set; }
    public StatusBayar? StatusBayar { get; set; }

    public string NoRekamMedis { get; set; } = string.Empty;
    public Pasien? Pasien { get; set; }

    public string? KodePoli { get; set; }
    public Poliklinik? Poliklinik { get; set; }

    public string? KodeDokter { get; set; }
    public Dokter? Dokter { get; set; }

    public string KodePenjab { get; set; } = string.Empty;
    public Penjab? Penjab { get; set; }

    /// <summary>Penanggung jawab biaya untuk kunjungan ini (bisa beda dari PJ di data master pasien).</summary>
    public string? PenanggungJawab { get; set; }
    public string? AlamatPj { get; set; }
    public string? HubunganPj { get; set; }
    public decimal? BiayaReg { get; set; }
}
