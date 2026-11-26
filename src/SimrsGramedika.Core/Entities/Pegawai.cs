namespace SimrsGramedika.Core.Entities;

public enum StatusAktifPegawai
{
    Aktif,
    Cuti,
    Keluar,
    TenagaLuar,
    NonAktif
}

/// <summary>
/// Maps to legacy table `pegawai` (PK: id, unique: nik) — base record for Dokter and Petugas.
/// </summary>
public class Pegawai
{
    public int Id { get; set; }
    public string Nik { get; set; } = string.Empty;
    public string Nama { get; set; } = string.Empty;
    public string Jabatan { get; set; } = string.Empty;
    public StatusAktifPegawai StatusAktif { get; set; }
    public DateOnly MulaiKerja { get; set; }
}

/// <summary>
/// Maps to legacy table `dokter` (PK: kd_dokter, FK -> pegawai.nik).
/// </summary>
public class Dokter
{
    public string KodeDokter { get; set; } = string.Empty;
    public string NamaDokter { get; set; } = string.Empty;
    public string? KodeSpesialis { get; set; }
    public string? NoIjinPraktek { get; set; }
    public bool Aktif { get; set; }

    public Pegawai? Pegawai { get; set; }
}

/// <summary>
/// Maps to legacy table `petugas` (PK: nip, FK -> pegawai.nik) — non-doctor staff (nurse, admin, etc).
/// </summary>
public class Petugas
{
    public string Nip { get; set; } = string.Empty;
    public string Nama { get; set; } = string.Empty;
    public string? KodeJabatan { get; set; }
    public bool Aktif { get; set; }

    public Pegawai? Pegawai { get; set; }
}
