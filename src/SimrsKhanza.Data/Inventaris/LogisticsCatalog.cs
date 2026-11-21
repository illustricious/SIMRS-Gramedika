namespace SimrsKhanza.Data.Inventaris;

internal sealed record LogisticsCatalog(string Prefix, string Label, string RequestSuffix, string ProposalSuffix, string? CategoryTable)
{
    internal static readonly LogisticsCatalog Nonmedical = new("ipsrs", "Non Medis", "non_medis", "nonmedis", "ipsrsjenisbarang");
    internal static readonly LogisticsCatalog Kitchen = new("dapur", "Dapur", "dapur", "dapur", null);

    public string Items => Prefix + "barang";
    public string Suppliers => Prefix + "suplier";
    public string Stocktake => Prefix + "opname";
    public string Purchase => Prefix + "pembelian";
    public string PurchaseItems => Prefix + "detailbeli";
    public string Receipt => Prefix + "pemesanan";
    public string ReceiptItems => Prefix + "detailpesan";
    public string Issue => Prefix + "pengeluaran";
    public string IssueItems => Prefix + "detailpengeluaran";
    public string Order => "surat_pemesanan_" + RequestSuffix;
    public string Category => CategoryTable is null ? "b.jenis" : "j.nm_jenis";
    public string CategoryJoin => CategoryTable is null ? "" : $"LEFT JOIN {CategoryTable} j ON j.kd_jenis=b.jenis";
}
