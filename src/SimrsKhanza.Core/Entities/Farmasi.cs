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
    /// <summary>FK to `jenis.kdjns` — see JenisBarang's doc comment.</summary>
    public string? KodeJenis { get; set; }
}

/// <summary>
/// Maps to legacy table `jenis` (PK: kdjns) — the drug/BHP TYPE lookup (TABLET,
/// ALKES, INJEKSI, BHP, …), joined to `databarang.kdjns`. This is what the
/// legacy "Input Penjualan" screen's "Kategori" column actually shows —
/// verified against the live schema; NOT `kategori_barang` (FORNAS/GENERIK/…,
/// a different classification) despite the similar name.
/// </summary>
public class JenisBarang
{
    public string KodeJenis { get; set; } = string.Empty;
    public string Nama { get; set; } = string.Empty;
}

/// <summary>
/// Maps to legacy table `gudangbarang` (composite PK: kode_brng+kd_bangsal+
/// no_batch+no_faktur) — per-location, per-batch running stock. v1 only reads
/// this (summed per kode_barang, filtered to kd_bangsal "F.A" = INSTALASI
/// FARMASI — the one location the Farmasi screens model) for the "Stok"
/// column on the sale screen; batch/faktur-level detail and other bangsal are
/// not surfaced.
/// </summary>
public class GudangBarang
{
    public string KodeBarang { get; set; } = string.Empty;
    public string KodeBangsal { get; set; } = string.Empty;
    public decimal Stok { get; set; }
    public string NoBatch { get; set; } = string.Empty;
    public string NoFaktur { get; set; } = string.Empty;
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
/// composite NoKeluar+KodeBarang used here). One row per item sold under a
/// PengeluaranObatBhp header — the "Input Penjualan" screen writes one of
/// these per catalog row with Jml > 0, same multi-line shape as the legacy
/// form (see FarmasiRepository.SimpanPenjualanAsync).
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
