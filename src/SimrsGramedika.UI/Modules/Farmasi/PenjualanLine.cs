using System.ComponentModel;
using System.Runtime.CompilerServices;
using SimrsGramedika.Data;

namespace SimrsGramedika.UI.Modules.Farmasi;

/// <summary>
/// One editable row of the "Input Penjualan" catalog grid — one per active
/// databarang item, with Jml/Ptg/Tmb/Emb/Tsl/No.Batch/No.Faktur editable
/// in-grid like the legacy form (you type a quantity directly into the
/// catalog row, there's no separate "add item" step). Rows with Jml == 0
/// are skipped when saving.
///
/// AturanPakai is kept editable here for visual parity with the legacy
/// screen but is NOT persisted — detail_pengeluaran_obat_bhp has no such
/// column (that's a resep_dokter concept); see HANDOVER.md.
/// </summary>
public class PenjualanLine(BarangKatalogItem katalog) : INotifyPropertyChanged
{
    public string KodeBarang { get; } = katalog.KodeBarang;
    public string? NamaBarang { get; } = katalog.NamaBarang;
    public string? Kategori { get; } = katalog.Kategori;
    public string Satuan { get; } = katalog.Satuan;
    public decimal Harga { get; } = katalog.Harga;
    public decimal Stok { get; } = katalog.Stok;

    private decimal _jml;
    public decimal Jml
    {
        get => _jml;
        set { if (_jml == value) return; _jml = value; OnChanged(); OnChanged(nameof(Subtotal)); OnChanged(nameof(PtgRp)); OnChanged(nameof(Total)); }
    }

    private decimal _ptgPercent;
    public decimal PtgPercent
    {
        get => _ptgPercent;
        set { if (_ptgPercent == value) return; _ptgPercent = value; OnChanged(); OnChanged(nameof(PtgRp)); OnChanged(nameof(Total)); }
    }

    private decimal _tmbRp;
    public decimal TmbRp
    {
        get => _tmbRp;
        set { if (_tmbRp == value) return; _tmbRp = value; OnChanged(); OnChanged(nameof(Total)); }
    }

    private decimal _embRp;
    public decimal EmbRp
    {
        get => _embRp;
        set { if (_embRp == value) return; _embRp = value; OnChanged(); OnChanged(nameof(Total)); }
    }

    private decimal _tslRp;
    public decimal TslRp
    {
        get => _tslRp;
        set { if (_tslRp == value) return; _tslRp = value; OnChanged(); OnChanged(nameof(Total)); }
    }

    public string? AturanPakai { get; set; }
    public string? NoBatch { get; set; }
    public string? NoFaktur { get; set; }

    public decimal Subtotal => Harga * Jml;
    public decimal PtgRp => Math.Round(Subtotal * PtgPercent / 100m, 0);
    public decimal Total => Subtotal - PtgRp + TmbRp + EmbRp + TslRp;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
