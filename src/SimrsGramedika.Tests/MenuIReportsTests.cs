using SimrsGramedika.Data.Master;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.Tests;

public class MenuIReportsTests
{
    [Fact]
    public void ReportsBelongToMenuIAndHaveValidTotalColumns()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        var keys = LegacyMenuCatalog.Tiles[menu].Select(tile => tile.Key).ToHashSet();
        Assert.Equal(3, MenuIReports.Reports.Count);
        foreach (var (key, spec) in MenuIReports.Reports)
        {
            Assert.Contains(key, keys);
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.All(spec.TotalColumns, total => Assert.Contains(spec.Columns, column => column.Column == total));
        }
    }

    [Fact]
    public void AdditionalReportsAreDistinctCatalogTilesWithValidTotals()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        var keys = LegacyMenuCatalog.Tiles[menu].Select(tile => tile.Key).ToHashSet();
        var reports = MenuIAdditionalReports.Reports.Concat(MenuIDrugReports.Reports).Concat(MenuIBillingReports.Reports).ToArray();
        Assert.Equal(21, reports.Length);
        Assert.Equal(21, reports.Select(r => r.Key).Distinct().Count());
        foreach (var (key, spec) in reports)
        {
            Assert.Contains(key, keys);
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.All(spec.TotalColumns, total => Assert.Contains(spec.Columns, column => column.Column == total));
        }
    }

    [Fact]
    public void PaymentReportsAreDistinctMenuITiles()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        var keys = LegacyMenuCatalog.Tiles[menu].Select(tile => tile.Key).ToHashSet();
        Assert.Equal(6, MenuIPaymentReports.Reports.Count);
        foreach (var (key, spec) in MenuIPaymentReports.Reports)
        {
            Assert.Contains(key, keys);
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.All(spec.TotalColumns, total => Assert.Contains(spec.Columns, column => column.Column == total));
        }
    }

    [Fact]
    public void ClinicianReportsHaveCatalogEntriesAndDeclaredTotals()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        var keys = LegacyMenuCatalog.Tiles[menu].Select(tile => tile.Key).ToHashSet();
        Assert.Equal(6, MenuIClinicianReports.Reports.Count);
        foreach (var (key, spec) in MenuIClinicianReports.Reports)
        {
            Assert.Contains(key, keys);
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.All(spec.TotalColumns, total => Assert.Contains(spec.Columns, column => column.Column == total));
        }
    }

    [Fact]
    public void ProcedureReportsHaveCatalogEntriesAndValidTotals()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        Assert.Equal(3, MenuIProcedureReports.Reports.Count);
        foreach (var (key, spec) in MenuIProcedureReports.Reports)
        {
            Assert.Contains(LegacyMenuCatalog.Tiles[menu], tile => tile.Key == key);
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.All(spec.TotalColumns, total => Assert.Contains(spec.Columns, column => column.Column == total));
        }
    }

    [Fact]
    public void PaymentPointReportsHaveDistinctMenuTiles()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        var keys = LegacyMenuCatalog.Tiles[menu].Select(tile => tile.Key).ToHashSet();
        Assert.Equal(2, MenuIPaymentPointReports.Reports.Count);
        foreach (var (key, spec) in MenuIPaymentPointReports.Reports)
        {
            Assert.Contains(key, keys);
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.All(spec.TotalColumns, total => Assert.Contains(spec.Columns, column => column.Column == total));
        }
    }

    [Fact]
    public void DailyReportsUseDistinctCatalogTiles()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        var keys = LegacyMenuCatalog.Tiles[menu].Select(tile => tile.Key).ToHashSet();
        Assert.Equal(4, MenuIDailyReports.Reports.Count);
        foreach (var (key, spec) in MenuIDailyReports.Reports)
        {
            Assert.Contains(key, keys);
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.Empty(spec.TotalColumns);
        }
    }

    [Fact]
    public void PatientFeeReportsHaveValidTilesAndTotals()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        var keys = LegacyMenuCatalog.Tiles[menu].Select(tile => tile.Key).ToHashSet();
        Assert.Equal(2, MenuIPatientFeesReports.Reports.Count);
        foreach (var (key, spec) in MenuIPatientFeesReports.Reports)
        {
            Assert.Contains(key, keys);
            Assert.Equal(ReportPeriod.DateRange, spec.Period);
            Assert.All(spec.TotalColumns, total => Assert.Contains(spec.Columns, column => column.Column == total));
        }
    }

    [Fact]
    public void AccountPaymentReportsHaveCatalogEntriesAndValidTotals()
    {
        var menu = Array.FindIndex(LegacyMenuCatalog.Categories, c => c.StartsWith("[I]", StringComparison.Ordinal));
        Assert.Equal(5, MenuIAccountPaymentReports.Reports.Count);
        foreach (var (key, report) in MenuIAccountPaymentReports.Reports)
        {
            Assert.Contains(LegacyMenuCatalog.Tiles[menu], tile => tile.Key == key);
            Assert.Equal(ReportPeriod.DateRange, report.Period);
            Assert.All(report.TotalColumns, total => Assert.Contains(report.Columns, column => column.Column == total));
        }
    }
}
