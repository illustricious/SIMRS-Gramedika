namespace SimrsKhanza.Core.Entities;

public enum TingkatKesadaran
{
    ComposMentis,
    Somnolence,
    Sopor,
    Coma,
    Alert,
    Confusion,
    Voice,
    Pain,
    Unresponsive,
    Apatis,
    Delirium,
    Meninggal,
}

/// <summary>
/// Maps to legacy table `pemeriksaan_ralan` (composite PK: no_rawat + tgl_perawatan
/// + jam_rawat) — a doctor's outpatient examination record: vitals + clinical notes.
/// </summary>
public class PemeriksaanRalan
{
    public string NoRawat { get; set; } = string.Empty;
    public DateOnly TanggalPerawatan { get; set; }
    public TimeOnly JamRawat { get; set; }

    public string? SuhuTubuh { get; set; }
    public string Tensi { get; set; } = string.Empty;
    public string? Nadi { get; set; }
    public string? Respirasi { get; set; }
    public string? Tinggi { get; set; }
    public string? Berat { get; set; }
    public string Spo2 { get; set; } = string.Empty;
    public string? Gcs { get; set; }
    public TingkatKesadaran Kesadaran { get; set; }

    public string? Keluhan { get; set; }
    public string? Pemeriksaan { get; set; }
    public string? Alergi { get; set; }

    /// <summary>Rencana tindak lanjut.</summary>
    public string Rtl { get; set; } = string.Empty;
    public string Penilaian { get; set; } = string.Empty;
    public string Instruksi { get; set; } = string.Empty;
    public string Evaluasi { get; set; } = string.Empty;

    /// <summary>NIP of the examining doctor/staff — FK to Pegawai.Nik.</summary>
    public string Nip { get; set; } = string.Empty;
}
