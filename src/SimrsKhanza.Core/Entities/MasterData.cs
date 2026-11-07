namespace SimrsKhanza.Core.Entities;

/// <summary>
/// Maps to legacy table `penjab` (PK: kd_pj) — payer/insurance master (umum, BPJS, etc).
/// </summary>
public class Penjab
{
    public string KodePenjab { get; set; } = string.Empty;
    public string NamaPenanggungJawab { get; set; } = string.Empty;
    public string? NamaPerusahaan { get; set; }
    public bool Aktif { get; set; }
}

/// <summary>
/// Maps to legacy table `poliklinik` (PK: kd_poli).
/// </summary>
public class Poliklinik
{
    public string KodePoli { get; set; } = string.Empty;
    public string NamaPoli { get; set; } = string.Empty;
    public decimal BiayaRegistrasi { get; set; }
    public bool Aktif { get; set; }
}

public enum StatusKamar
{
    Isi,
    Kosong,
    Dibersihkan,
    Dibooking,
    Perbaikan
}

public enum KelasKamar
{
    Kelas1,
    Kelas2,
    Kelas3,
    KelasUtama,
    Vip,
    Vvip
}

/// <summary>
/// Maps to legacy table `bangsal` (PK: kd_bangsal) — inpatient ward master.
/// </summary>
public class Bangsal
{
    public string KodeBangsal { get; set; } = string.Empty;
    public string NamaBangsal { get; set; } = string.Empty;
    public bool Aktif { get; set; }
}

/// <summary>
/// Maps to legacy table `kamar` (PK: kd_kamar, FK -> bangsal).
/// </summary>
public class Kamar
{
    public string KodeKamar { get; set; } = string.Empty;
    public string KodeBangsal { get; set; } = string.Empty;
    public Bangsal? Bangsal { get; set; }
    public decimal TarifKamar { get; set; }
    public StatusKamar Status { get; set; }
    public KelasKamar Kelas { get; set; }
}

/// <summary>
/// Legacy `kamar_inap.stts_pulang` is NOT NULL with no default, so a row must
/// carry a value from the moment the patient is admitted — "-" (BelumPulang)
/// is one of the legacy enum's own values, used here as "not discharged yet".
/// Only the outcomes actually exposed in the discharge form are modeled;
/// anything else from the legacy 14-value enum maps to Lainnya.
/// </summary>
public enum StatusPulang
{
    BelumPulang,
    Sembuh,
    Membaik,
    Rujuk,
    PulangPaksa,
    Meninggal,
    Lainnya,
}

/// <summary>
/// Maps to legacy table `kamar_inap` (composite PK: no_rawat + tgl_masuk + jam_masuk).
/// </summary>
public class KamarInap
{
    public string NoRawat { get; set; } = string.Empty;
    public RegPeriksa? RegPeriksa { get; set; }
    public string KodeKamar { get; set; } = string.Empty;
    public Kamar? Kamar { get; set; }
    public decimal? TarifKamar { get; set; }
    public DateOnly TanggalMasuk { get; set; }
    public TimeOnly JamMasuk { get; set; }
    public DateOnly? TanggalKeluar { get; set; }
    public TimeOnly? JamKeluar { get; set; }
    public string? DiagnosaAwal { get; set; }
    public string? DiagnosaAkhir { get; set; }
    /// <summary>Lama rawat dalam hari — dihitung saat pasien dipulangkan (tgl_keluar - tgl_masuk, minimal 1).</summary>
    public decimal? Lama { get; set; }
    /// <summary>Total biaya kamar (Lama * TarifKamar) — dihitung & ditulis saat dipulangkan, bukan saat masuk.</summary>
    public decimal? TotalBiaya { get; set; }
    public StatusPulang? StatusPulang { get; set; }
}
