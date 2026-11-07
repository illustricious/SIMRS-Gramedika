namespace SimrsKhanza.Core.Entities;

/// <summary>
/// Maps to legacy table `databarang` (PK: kode_brng) — drug/BHP (consumable)
/// catalog. Price columns are legacy's per-payer-class tariff table
/// (ralan/kelas1-3/utama/vip/vvip/jualbebas/karyawan) — v1 only reads
/// `JualBebas` (walk-in/OTC price) and `Dasar` (base cost), the rest are left
/// unmapped (not needed yet, would just be dead columns).
/// </summary>
public class DataBarang
{
    public string KodeBarang { get; set; } = string.Empty;
    public string? NamaBarang { get; set; }
    public string KodeSatuanBesar { get; set; } = string.Empty;
    public decimal Dasar { get; set; }
    public decimal? JualBebas { get; set; }
    public decimal? StokMinimal { get; set; }
    public bool Aktif { get; set; }
}

/// <summary>
/// Maps to legacy table `resep_obat` (PK: no_resep) — despite the name this
/// is the prescription HEADER (one per no_rawat/dokter/tanggal); the per-item
/// lines are table `resep_dokter` (→ ResepDokterItem below). Table naming is
/// swapped from what you'd guess — verified against the live schema, not a
/// typo here.
/// </summary>
public class ResepObat
{
    public string NoResep { get; set; } = string.Empty;
    public string NoRawat { get; set; } = string.Empty;
    public RegPeriksa? RegPeriksa { get; set; }
    public string KodeDokter { get; set; } = string.Empty;
    public Dokter? Dokter { get; set; }
    public DateOnly? TanggalPeresepan { get; set; }
    public TimeOnly? JamPeresepan { get; set; }
    public StatusKunjunganPenunjang? Status { get; set; }
}

/// <summary>Maps to legacy table `resep_dokter` (no declared PK in the legacy schema — composite NoResep+KodeBarang used here, read-only via AsNoTracking so a rare duplicate doesn't trip EF's identity map).</summary>
public class ResepDokterItem
{
    public string NoResep { get; set; } = string.Empty;
    public string KodeBarang { get; set; } = string.Empty;
    public decimal? Jumlah { get; set; }
    public string? AturanPakai { get; set; }
}

/// <summary>
/// Maps to legacy table `pengeluaran_obat_bhp` (PK: no_keluar) — stock
/// issuance header. Used here as the v1 "Input/Data Penjualan Obat & BHP"
/// screen's backing table: closest real table to a dispense-to-patient
/// transaction without building a full POS/billing flow for pharmacy (which
/// doesn't exist yet) — see docs/audit/HANDOVER.md for the scope call.
/// </summary>
public class PengeluaranObatBhp
{
    public string NoKeluar { get; set; } = string.Empty;
    public DateOnly Tanggal { get; set; }
    public string Nip { get; set; } = string.Empty;
    public string Keterangan { get; set; } = string.Empty;
    public string? KodeBangsal { get; set; }
}

/// <summary>
/// Maps to legacy table `detail_pengeluaran_obat_bhp` (no declared PK —
/// composite NoKeluar+KodeBarang used here). v1 writes exactly one detail row
/// per header (one item per transaction) — a deliberate simplification, not
/// a port of the legacy multi-line form.
/// </summary>
public class DetailPengeluaranObatBhp
{
    public string NoKeluar { get; set; } = string.Empty;
    public string KodeBarang { get; set; } = string.Empty;
    public string KodeSatuan { get; set; } = string.Empty;
    public string? NoBatch { get; set; }
    public decimal Jumlah { get; set; }
    public decimal HargaBeli { get; set; }
    public decimal Total { get; set; }
    public string NoFaktur { get; set; } = string.Empty;
}
