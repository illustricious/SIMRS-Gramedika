namespace SimrsKhanza.Core.Entities;

/// <summary>
/// `permintaan_lab`/`permintaan_radiologi`.`status` — legacy stores this
/// lowercase ('ralan'/'ranap'), unlike `reg_periksa.status_lanjut`
/// ('Ralan'/'Ranap') or `kamar_inap`/`periksa_radiologi` ('Ralan'/'Ranap'
/// capitalized) — three different casings for the same concept across the
/// legacy schema, so this gets its own enum + conversion rather than reusing
/// StatusLanjut.
/// </summary>
public enum StatusKunjunganPenunjang
{
    RawatJalan,
    RawatInap,
}

/// <summary>
/// Lab category — legacy Khanza splits the whole lab menu into PK
/// (Patologi Klinik), PA (Patologi Anatomi) and MB (Mikrobiologi), each with
/// its own "Permintaan"/"Periksa" shortcut pair. The real schema carries this
/// on `jns_perawatan_lab.kategori`/`periksa_lab.kategori` (the test-catalog
/// and billing tables), neither of which v1 implements (see HasilLab's doc
/// comment). `permintaan_lab` itself has NO kategori column at all, so this
/// is tagged onto `PermintaanLab.InformasiTambahan` with a "[PK]"/"[PA]"/"[MB]"
/// prefix (see PenunjangRepository) rather than requiring a schema change —
/// a deliberate workaround, not a faithful port of the legacy category model.
/// </summary>
public enum KategoriLab
{
    PatologiKlinik,
    PatologiAnatomi,
    Mikrobiologi,
}

/// <summary>
/// Maps to legacy table `permintaan_lab` (PK: noorder) — a doctor's order for
/// a lab test on a visit. v1 deliberately skips the "ambil sampel" workflow
/// (tgl_sampel/jam_sampel are stamped at request time, not tracked as a
/// separate step) — see docs/audit/HANDOVER.md for what's deferred.
/// </summary>
public class PermintaanLab
{
    public string NoOrder { get; set; } = string.Empty;
    public string NoRawat { get; set; } = string.Empty;
    public RegPeriksa? RegPeriksa { get; set; }
    public DateOnly TanggalPermintaan { get; set; }
    public TimeOnly JamPermintaan { get; set; }
    public DateOnly TanggalSampel { get; set; }
    public TimeOnly JamSampel { get; set; }
    public DateOnly TanggalHasil { get; set; }
    public TimeOnly JamHasil { get; set; }
    public string DokterPerujuk { get; set; } = string.Empty;
    public StatusKunjunganPenunjang Status { get; set; }
    public string? InformasiTambahan { get; set; }
    public string? DiagnosaKlinis { get; set; }
}

/// <summary>Maps to legacy table `permintaan_radiologi` (PK: noorder) — same shape/role as PermintaanLab, radiologi side.</summary>
public class PermintaanRadiologi
{
    public string NoOrder { get; set; } = string.Empty;
    public string NoRawat { get; set; } = string.Empty;
    public RegPeriksa? RegPeriksa { get; set; }
    public DateOnly TanggalPermintaan { get; set; }
    public TimeOnly JamPermintaan { get; set; }
    public DateOnly TanggalSampel { get; set; }
    public TimeOnly JamSampel { get; set; }
    public DateOnly TanggalHasil { get; set; }
    public TimeOnly JamHasil { get; set; }
    public string DokterPerujuk { get; set; } = string.Empty;
    public StatusKunjunganPenunjang Status { get; set; }
    public string? InformasiTambahan { get; set; }
    public string? DiagnosaKlinis { get; set; }
}

/// <summary>
/// Maps to legacy table `saran_kesan_lab` (composite PK: no_rawat+tgl_periksa+jam)
/// — used here as the v1 lab "hasil" (narrative impression/suggestion),
/// deliberately simpler than the fully structured per-parameter
/// `detail_periksa_lab` (one row per test parameter with nilai/nilai_rujukan)
/// which is deferred — see docs/audit/HANDOVER.md.
/// </summary>
public class HasilLab
{
    public string NoRawat { get; set; } = string.Empty;
    public DateOnly TanggalPeriksa { get; set; }
    public TimeOnly Jam { get; set; }
    public string? Saran { get; set; }
    public string? Kesan { get; set; }
}

/// <summary>Maps to legacy table `hasil_radiologi` (composite PK: no_rawat+tgl_periksa+jam) — single narrative result text, matches the legacy schema's own simplicity for this table.</summary>
public class HasilRadiologi
{
    public string NoRawat { get; set; } = string.Empty;
    public DateOnly TanggalPeriksa { get; set; }
    public TimeOnly Jam { get; set; }
    public string Hasil { get; set; } = string.Empty;
}
