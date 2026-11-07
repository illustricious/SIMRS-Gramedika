using Avalonia.Controls;

namespace SimrsKhanza.UI.Modules.Shared;

/// <summary>
/// One item from the legacy right-click patient menu (user shared a
/// screenshot: Data Rekam Medis / Permintaan / Kamar Inap / Tindakan &amp;
/// Pemeriksaan / Obat / Billing-Pembayaran Pasien / Laporan / Grafik Analisa
/// / Surat-Surat / Check List Kelengkapan Pendaftaran / Lembar Periksa
/// Pasien / Label &amp; Barcode / Rujukan / Bridging / Input Data / Urutkan
/// Data Berdasar / Hapus &amp; Ubah Data). Only a handful route to a real
/// screen so far — the rest show "belum diimplementasikan", same convention
/// as every other unbuilt menu item in this app. Add real screens over time
/// and move items from NotImplemented to a real case in
/// MainWindow.OnPatientMenuRequested as they're built — see HANDOVER.md.
/// </summary>
public enum PatientMenuAction
{
    DataRekamMedis,
    Permintaan,
    KamarInap,
    TindakanDanPemeriksaan,
    Obat,
    BillingPembayaran,
    Laporan,
    GrafikAnalisa,
    SuratSurat,
    ChecklistKelengkapanPendaftaran,
    LembarPeriksaPasien,
    LabelBarcode,
    Rujukan,
    Bridging,
    InputData,
    UrutkanDataBerdasar,
    HapusDanUbahData,
}

/// <summary>Context captured for whichever patient row was right-clicked.</summary>
public record PatientMenuRequest(string NoRawat, string NoRekamMedis, string NamaPasien, PatientMenuAction Action);

/// <summary>
/// Builds the shared right-click menu for any DataGrid row representing a
/// patient visit (Registrasi, IGD, Rawat Inap, Rawat Jalan all use this —
/// one menu definition instead of 4 copies). `getSelected` reads whichever
/// row is currently selected in the caller's grid at click time (v1: this is
/// the grid's current SelectionChanged state, not literally "the row under
/// the cursor" — right-click doesn't reselect first; acceptable since users
/// normally select-then-right-click, documented as a known simplification).
/// </summary>
public static class PatientContextMenu
{
    public static ContextMenu Build(Func<(string NoRawat, string NoRekamMedis, string NamaPasien)?> getSelected, Action<PatientMenuRequest> onSelect)
    {
        var menu = new ContextMenu();
        var items = new List<Control>();

        void Add(PatientMenuAction action, string label)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) =>
            {
                var selected = getSelected();
                if (selected is null) return;
                onSelect(new PatientMenuRequest(selected.Value.NoRawat, selected.Value.NoRekamMedis, selected.Value.NamaPasien, action));
            };
            items.Add(item);
        }

        void Sep() => items.Add(new Separator());

        Add(PatientMenuAction.DataRekamMedis, "📋 Data Rekam Medis");
        Add(PatientMenuAction.Permintaan, "🧪 Permintaan");
        Add(PatientMenuAction.KamarInap, "🛏 Kamar Inap");
        Add(PatientMenuAction.TindakanDanPemeriksaan, "🩺 Tindakan & Pemeriksaan");
        Add(PatientMenuAction.Obat, "💊 Obat");
        Add(PatientMenuAction.BillingPembayaran, "💰 Billing/Pembayaran Pasien");
        Sep();
        Add(PatientMenuAction.Laporan, "📊 Laporan");
        Add(PatientMenuAction.GrafikAnalisa, "📈 Grafik Analisa");
        Add(PatientMenuAction.SuratSurat, "✉ Surat-Surat");
        Add(PatientMenuAction.ChecklistKelengkapanPendaftaran, "☑ Check List Kelengkapan Pendaftaran");
        Add(PatientMenuAction.LembarPeriksaPasien, "📄 Lembar Periksa Pasien");
        Add(PatientMenuAction.LabelBarcode, "🏷 Label & Barcode");
        Sep();
        Add(PatientMenuAction.Rujukan, "↪ Rujukan");
        Add(PatientMenuAction.Bridging, "🔗 Bridging");
        Sep();
        Add(PatientMenuAction.InputData, "⌨ Input Data");
        Add(PatientMenuAction.UrutkanDataBerdasar, "↕ Urutkan Data Berdasar");
        Add(PatientMenuAction.HapusDanUbahData, "🗑 Hapus & Ubah Data");

        menu.ItemsSource = items;
        return menu;
    }
}
