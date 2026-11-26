namespace SimrsGramedika.Core.Entities;

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

/// <summary>Legacy column `keluarga` — the patient's relation to their penanggung jawab (PJ).</summary>
public enum HubunganPenanggungJawab
{
    Ayah,
    Ibu,
    Istri,
    Suami,
    Saudara,
    Anak,
    DiriSendiri,
    LainLain,
}

/// <summary>
/// Maps to legacy table `pasien` (PK: no_rkm_medis). Field set here covers the
/// "Identitas Pasien" core of the legacy DlgPasien.form; the TNI/POLRI
/// sub-panel, BPJS/Dukcapil/SatuSehat web lookups, print-letter shortcuts and
/// cascading kelurahan/kecamatan/kabupaten/propinsi pickers are deferred (see
/// docs/handover/04-HISTORY.md) — those FK columns are still populated with a
/// placeholder "-"/0 row so inserts succeed against the real legacy schema.
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
    public string? Email { get; set; }

    public string KodePenjab { get; set; } = string.Empty;
    public Penjab? Penjab { get; set; }

    // Penanggung jawab (PJ) — who to contact / who's responsible for this patient.
    public HubunganPenanggungJawab Keluarga { get; set; } = HubunganPenanggungJawab.DiriSendiri;
    public string NamaKeluarga { get; set; } = string.Empty;
    public string PekerjaanPj { get; set; } = "-";
    public string AlamatPj { get; set; } = "-";

    // --- Deferred-feature columns ---
    // These exist in the legacy `pasien` table as NOT NULL with no DB default
    // and FK-reference master tables (kelurahan/kecamatan/kabupaten/propinsi/
    // suku_bangsa/bahasa_pasien/cacat_fisik/perusahaan_pasien) that this
    // scaffold only seeds a single "-"/0 placeholder row into (see
    // docs/handover/04-HISTORY.md). They're on the entity so inserts satisfy the
    // real legacy schema, but PasienView doesn't expose them as fields yet —
    // that's the cascading-picker work noted as deferred for v2.
    public string Umur { get; set; } = "-";
    public string Pendidikan { get; set; } = "TS";
    public int KodeKelurahan { get; set; }
    public int KodeKecamatan { get; set; }
    public int KodeKabupaten { get; set; }
    public int KodePropinsi { get; set; }
    public string KelurahanPj { get; set; } = "-";
    public string KecamatanPj { get; set; } = "-";
    public string KabupatenPj { get; set; } = "-";
    public string PropinsiPj { get; set; } = "-";
    public string PerusahaanPasien { get; set; } = "-";
    public int SukuBangsa { get; set; }
    public int BahasaPasien { get; set; }
    public int CacatFisik { get; set; }
    public string Nip { get; set; } = "-";
    public string NoPeserta { get; set; } = "-";
}
