using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

public static class MenuFSpecs
{
    private static readonly LogisticsCatalog Catalog = LogisticsCatalog.Kitchen;
    private static readonly LogisticsStockSpecs Stock = new(Catalog);

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnBarangDapur"] = LogisticsMasterSpecs.Items("Barang Dapur", Catalog,
            new("jenis", "Jenis Barang", FieldKind.Choice, Choices: ["Basah", "Kering"])
            { NullWhenEmpty = true, Default = "Kering" }),
        ["btnSuplierDapur"] = LogisticsMasterSpecs.Suppliers(Catalog),
    };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports =
        new Dictionary<string, ReportSpec>(new LogisticsTransactionSpecs(Catalog).KitchenReports()
            .Concat(LogisticsMonitoringSpecs.KitchenReports))
    {
        ["btnRiwayatBarangDapur"] = Stock.History("Riwayat Barang Dapur"),
        ["btnSirkulasiDapur"] = Stock.Circulation("Sirkulasi Barang Dapur"),
        ["btnSirkulasiDapur2"] = Stock.CirculationWithStocktake("Sirkulasi Barang Dapur 2"),
    };
}
