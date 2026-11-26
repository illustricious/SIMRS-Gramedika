using SimrsGramedika.Data.Master;

namespace SimrsGramedika.UI.Modules.Shared;

/// <summary>Laporan yang dapat dibuka dari menu klik kanan pasien.</summary>
internal static class ContextReports
{
    public static bool TryGet(string key, out ReportSpec report)
    {
        if (MenuMSpecs.Reports.TryGetValue(key, out report!)) return true;
        if (MenuJSpecs.Reports.TryGetValue(key, out report!)) return true;
        if (MenuOSpecs.Reports.TryGetValue(key, out report!)) return true;

        report = key switch
        {
            "btnKunjunganRalan" => VisitReportSpecs.OutpatientVisits,
            "btnKunjunganRanap" => VisitReportSpecs.InpatientVisits,
            "btnSensusHarianPoli" => VisitReportSpecs.DailyClinicCensus,
            "btnRekapKunjungan" => VisitReportSpecs.MonthlyVisitSummary,
            _ => null!,
        };
        return report is not null;
    }

    /// <summary>Laporan ini dibatasi ke pasien yang diklik; sisanya rekap umum.</summary>
    public static bool IsPatientReport(string key) =>
        MenuMSpecs.Reports.ContainsKey(key) || key is "btnKunjunganRalan" or "btnKunjunganRanap";
}
