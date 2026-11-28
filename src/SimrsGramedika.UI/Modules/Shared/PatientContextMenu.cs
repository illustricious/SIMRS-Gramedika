using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Registrasi.Labels;

namespace SimrsGramedika.UI.Modules.Shared;

public enum PatientMenuAction
{
    DataRekamMedis, RiwayatPerawatan, KamarInap, TindakanDanPemeriksaan, BillingPembayaran, Skrining,
    TindakanRawatJalan, PeriksaLaboratorium, PeriksaRadiologi, PermintaanLab, PermintaanRadiologi,
    ResepDokter, PeriksaLaboratoriumPa, PeriksaLaboratoriumMb, Report, CatatanPasien,
}
public enum PatientMenuKind { Visit, Registration, Emergency, Patient, Diagnostic, EmergencyTriage }

/// <param name="Ranap">Pasien pada baris yang diklik sedang berstatus rawat inap.</param>
public record PatientMenuRequest(string NoRawat, string NoRekamMedis, string NamaPasien, PatientMenuAction Action, string? TileKey = null,
    bool Ranap = false);
public record PatientMenuTarget(string NoRawat, string NoRekamMedis, string NamaPasien, bool IsInpatient = false);

public static class PatientContextMenu
{
    public static ContextMenu Attach<T>(DataGrid grid, Func<T, PatientMenuTarget?> getPatient, Action<PatientMenuRequest> execute,
        PatientMenuKind kind = PatientMenuKind.Visit, Action<T>? edit = null, Action<T>? rujukInternal = null,
        Action<T>? rujukMasuk = null, Action<T>? delete = null, Action<T, string>? setStatus = null,
        Action<T, string>? setStatusPoli = null, Action<T>? rujukKeluar = null, Action<T>? suratSakit = null) where T : class
    {
        var menu = new ContextMenu();
        grid.ContextRequested += (_, e) =>
        {
            var pointer = e.TryGetPosition(grid, out Avalonia.Point _);
            var row = (e.Source as Avalonia.Visual)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
            var item = pointer ? row?.DataContext : grid.SelectedItem;
            if (item is not T clicked) return;

            var patient = getPatient(clicked);
            if (patient is null || string.IsNullOrWhiteSpace(patient.NoRekamMedis)) return;
            var source = grid.ItemsSource;
            grid.SelectedItem = clicked;

            void Run(Action action)
            {
                if (ReferenceEquals(source, grid.ItemsSource)) action();
            }

            menu.Placement = pointer ? PlacementMode.Pointer : PlacementMode.Bottom;
            var user = AppSession.CurrentUser;
            bool Can(string permission) => user?.HasPermission(permission) == true;
            var hasVisit = !string.IsNullOrWhiteSpace(patient.NoRawat);
            var registrasiRalan = kind == PatientMenuKind.Registration && hasVisit && !patient.IsInpatient;
            MenuItem ActionItem(string text, PatientMenuAction action, bool allowed) =>
                Entry(text, allowed ? () => Run(() => execute(
                    new(patient.NoRawat, patient.NoRekamMedis, patient.NamaPasien, action, Ranap: patient.IsInpatient))) : null);

            // Order dibuka dari daftar registrasi untuk rawat jalan, dan dari daftar mana pun untuk pasien rawat inap.
            var bisaOrder = hasVisit && (patient.IsInpatient || kind == PatientMenuKind.Registration);

            MenuItem Screen(string label, string key) => Entry(label,
                hasVisit && user is not null && LegacyTilePermissions.CanSeeTile(user, key)
                    ? () => Run(() => execute(new(patient.NoRawat, patient.NoRekamMedis, patient.NamaPasien, PatientMenuAction.Skrining, key)))
                    : null);

            MenuItem PatientReport(string label, string key) => Entry(label,
                user is not null && LegacyTilePermissions.CanSeeTile(user, key)
                    ? () => Run(() => execute(new(patient.NoRawat, patient.NoRekamMedis, patient.NamaPasien, PatientMenuAction.Report, key)))
                    : null);

            MenuItem ReportLink(string label, string permissionKey, string reportKey) => Entry(label,
                user is not null && LegacyTilePermissions.CanSeeTile(user, permissionKey)
                    ? () => Run(() => execute(new(patient.NoRawat, patient.NoRekamMedis, patient.NamaPasien, PatientMenuAction.Report, reportKey)))
                    : null);

            MenuItem PatientNote() => ActionItem("Catatan Pasien", PatientMenuAction.CatatanPasien, Can("catatan_pasien"));

            MenuItem StatusItem(string label, string status, Action<T, string>? ubah) => Entry(label,
                ubah is not null && (Can("edit_registrasi") || user?.IsAdminUtama == true)
                    ? () => Run(() => ubah(clicked, status)) : null);

            MenuItem PrintFormatGroup(string title, string templatePrefix) => Group(title,
                PatientLabel.Formats.Where(format => format.Template.StartsWith(templatePrefix, StringComparison.Ordinal))
                    .Select(format => Entry(format.Title, hasVisit && Can("registrasi")
                        ? () => Run(() => PatientLabelPreview.Open(grid, patient, format)) : null)).ToArray());

            // Bertambah sendiri mengikuti formulir yang sudah bisa diisi.
            MenuItem FormulirMenu() => Group("Formulir Rekam Medis",
                [.. SimrsGramedika.Data.RekamMedis.FormKlinisKatalog.Semua.GroupBy(f => f.Kelompok)
                    .Select(g => Group(g.Key, [.. g.Select(f => Screen(f.Judul, f.Tile))]))]);

            MenuItem ScreeningMenu() => Group("Skrining",
                    Screen("Skrining Merokok Usia Sekolah & Remaja", "btnSkriningMerokokUsiaSekolah"),
                    Screen("Skrining Kekerasan Pada Perempuan", "btnSkriningKekerasanPadaPerempuan"),
                    Screen("Skrining Obesitas", "btnSkriningObesitas"),
                    Group("Skrining Kanker",
                        Screen("Skrining Risiko Kanker Payudara", "btnSkriningRisikoKankerPayudara"),
                        Screen("Skrining Risiko Kanker Paru", "btnSkriningRisikoKankerParu"),
                        Screen("Skrining Risiko Kanker Serviks", "btnSkriningRisikoKankerServiks"),
                        Screen("Skrining Kanker Kolorektal", "btnSkriningKankerKolorektal")),
                    Group("Skrining Kesehatan Gigi & Mulut",
                        Screen("Usia Remaja", "btnSkriningKesehatanGigiMulutRemaja"),
                        Screen("Usia Balita", "btnSkriningKesehatanGigiMulutBalita"),
                        Screen("Usia Dewasa", "btnSkriningKesehatanGigiMulutDewasa"),
                        Screen("Usia Lansia", "btnSkriningKesehatanGigiMulutLansia")),
                    Group("Skrining Paru",
                        Screen("Skrining TBC", "btnSkriningTBC"),
                        Screen("Skrining PUMA", "btnSkriningPUMA"),
                        Screen("Skrining Pneumonia Severity Index", "btnSkriningPneumoniaSeverityIndex"),
                        Screen("Skrining CURB-65", "btnSkriningCURB65")),
                    Screen("Skrining Adiksi Nikotin", "btnSkriningAdiksiNikotin"),
                    Screen("Skrining Thalassemia", "btnSkriningThalassemia"),
                    Group("Skrining Instrumen",
                        Screen("SDQ", "btnSkriningInstrumenSDQ"),
                        Screen("SRQ", "btnSkriningInstrumenSRQ"),
                        Screen("ACRS", "btnSkriningInstrumenACRS"),
                        Screen("Mental Emosional", "btnSkriningInstrumenMentalEmosional"),
                        Screen("AMT", "btnSkriningInstrumenAMT"),
                        Screen("ESAT", "btnSkriningInstrumenESAT")),
                    Screen("Skrining Diabetes Melitus", "btnSkriningDiabetesMelitus"),
                    Screen("Skrining Anemia", "btnSkriningAnemia"),
                    Screen("Skrining Hipertensi", "btnSkriningHipertensi"),
                    Screen("Skrining Kesehatan Penglihatan", "btnSkriningKesehatanPenglihatan"),
                    Screen("Skrining Indra Pendengaran", "btnSkriningIndraPendengaran"),
                    Screen("Skrining Frailty Syndrome", "btnSkriningFrailtySyndrome"),
                    Screen("Skrining TOLAC", "btnSkriningTOLAC"));

            var history = ActionItem("Riwayat Perawatan", PatientMenuAction.RiwayatPerawatan, Can("resume_pasien"));
            var identity = ActionItem("Identitas Pasien", PatientMenuAction.DataRekamMedis, Can("pasien"));
            var manageItems = new List<MenuItem>
            {
                Entry("Ubah Data Registrasi", edit is not null && Can("registrasi") ? () => Run(() =>
                {
                    grid.SelectedItem = clicked;
                    edit(clicked);
                }) : null),
                Entry("Hapus Registrasi", delete is not null && Can("registrasi") ? () => Run(() =>
                {
                    grid.SelectedItem = clicked;
                    delete(clicked);
                }) : null),
            };
            if (kind == PatientMenuKind.Registration)
                manageItems.Add(Group("Set Status",
                    StatusItem("Berkas R.M. Diterima", "Berkas Diterima", setStatus),
                    StatusItem("Sudah Periksa", "Sudah", setStatus), StatusItem("Belum Periksa", "Belum", setStatus),
                    StatusItem("Batal Periksa", "Batal", setStatus), StatusItem("Dirujuk", "Dirujuk", setStatus),
                    StatusItem("Dirawat", "Dirawat", setStatus), StatusItem("Meninggal", "Meninggal", setStatus),
                    StatusItem("Pulang Paksa", "Pulang Paksa", setStatus),
                    Group("Status Poli", StatusItem("Status Poli Baru", "Baru", setStatusPoli),
                        StatusItem("Status Poli Lama", "Lama", setStatusPoli))));
            menu.ItemsSource = kind == PatientMenuKind.Diagnostic
                ? new Control[] { identity, history }
                : kind == PatientMenuKind.Patient
                ? (Control[])
                [
                    Group("Kartu Pasien", Entry("Cetak Kartu Pasien")),
                    Group("Barcode", Entry("Barcode Pasien")),
                    Group("Identitas Pasien", Entry("Cetak Identitas Pasien")),
                    Entry("Kartu Status"), Group("BPJS", Entry("Cek Peserta")),
                    Entry("Kelahiran Bayi"), Entry("Registrasi"), history, Entry("Catatan Pasien"), Entry("Gabung No. RM"),
                ]
                :
                [
                    Group("Data Rekam Medis", [.. (kind is PatientMenuKind.Registration or PatientMenuKind.Emergency ? new[] { ScreeningMenu() } : Array.Empty<MenuItem>()), identity, Entry("Resume Pasien"), history, FormulirMenu()]),
                    Group("Permintaan", Entry("Jadwal Operasi"),
                        ActionItem("Permintaan Lab", PatientMenuAction.PermintaanLab, bisaOrder && Can("permintaan_lab")),
                        ActionItem("Permintaan Radiologi", PatientMenuAction.PermintaanRadiologi,
                            bisaOrder && Can("permintaan_radiologi")),
                        Entry("Permintaan Rawat Inap")),
                    ActionItem("Kamar Inap", PatientMenuAction.KamarInap, hasVisit && !patient.IsInpatient && Can("kamar_inap")),
                    Group("Tindakan & Pemeriksaan", [
                        ActionItem(kind == PatientMenuKind.Registration ? "Tindakan Rawat Jalan" : "Pemeriksaan Rawat Jalan",
                            kind == PatientMenuKind.Registration ? PatientMenuAction.TindakanRawatJalan : PatientMenuAction.TindakanDanPemeriksaan,
                            hasVisit && !patient.IsInpatient && Can("tindakan_ralan")),
                        ActionItem("Periksa Laboratorium", PatientMenuAction.PeriksaLaboratorium,
                            registrasiRalan && Can("periksa_lab")),
                        .. (kind == PatientMenuKind.Registration ? new[]
                        {
                            ActionItem("Periksa Laboratorium Patologi Anatomi", PatientMenuAction.PeriksaLaboratoriumPa,
                                registrasiRalan && Can("pemeriksaan_lab_pa")),
                            ActionItem("Periksa Laboratorium Mikrobiologi", PatientMenuAction.PeriksaLaboratoriumMb,
                                registrasiRalan && Can("pemeriksaan_lab_mb")),
                        } : Array.Empty<MenuItem>()),
                        ActionItem("Periksa Radiologi", PatientMenuAction.PeriksaRadiologi,
                            registrasiRalan && Can("periksa_radiologi")),
                        Entry("Operasi")]),
                    Group("Obat", Entry("Pemberian Obat"), Entry("No. Resep"),
                        ActionItem("Resep Dokter", PatientMenuAction.ResepDokter, bisaOrder && Can("resep_dokter")),
                        Entry("Copy Resep")),
                    Group("Billing/Pembayaran Pasien", Entry("Billing Parsial"),
                        ActionItem("Billing Total", PatientMenuAction.BillingPembayaran, hasVisit &&
                            (patient.IsInpatient ? Can("billing_ranap") : Can("kasir_ralan") || Can("billing_ralan")))),
                    new Separator(),
                    Group("Laporan",
                        ReportLink("Kunjungan Rawat Jalan", "btnKunjunganRalan", "btnKunjunganRalan"),
                        ReportLink("Kunjungan Rawat Inap", "btnKunjunganRanap", "btnKunjunganRanap"),
                        ReportLink("Sensus Harian Poli", "btnSensusHarianPoli", "btnSensusHarianPoli"),
                        ReportLink("Rekap Kunjungan", "btnRekapKunjungan", "btnRekapKunjungan"),
                        Group("Laporan RL & Diagnosa",
                            ReportLink("RL 3.2 Rawat Darurat", "btnRl32", "btnRl32"),
                            ReportLink("Frekuensi Penyakit Rawat Jalan", "btnFrekuensiRalan", "btnFrekuensiRalan"),
                            ReportLink("Registrasi Poli Per Tanggal", "btnRegistrasiPoliPerTanggal", "btnRegistrasiPoliPerTanggal"))),
                    Group("Grafik Analisa",
                        ReportLink("Registrasi Per Poli", "btnGrafikKunjunganPoli", "btnGrafikKunjunganPoli"),
                        ReportLink("Registrasi Per Dokter", "btnGrafikKunjunganPerDokter", "btnGrafikKunjunganPerDokter"),
                        ReportLink("Registrasi Per Tahun", "btnGrafikKunjunganPerTahun", "btnGrafikKunjunganPerTahun"),
                        ReportLink("Registrasi Per Bulan", "btnGrafikKunjunganPerBulan", "btnGrafikKunjunganPerBulan"),
                        ReportLink("Registrasi Per Tanggal", "btnGrafikKunjunganPerTanggal", "btnGrafikKunjunganPerTanggal"),
                        ReportLink("Demografi Pendaftar", "btnGrafikDemografiRegistrasi", "btnGrafikDemografiRegistrasi")),
                    Group("Surat-Surat",
                        [.. suratSakit is null ? [] : new[] { Entry("Surat Cuti Sakit", hasVisit && Can(SuratSakitService.Izin)
                                ? () => Run(() => suratSakit(clicked)) : null) },
                            PrintFormatGroup("Administrasi - Bukti Register", "rptBuktiRegister")]),
                    .. (kind is PatientMenuKind.Emergency or PatientMenuKind.EmergencyTriage
                        ? new[] { Entry("Gelang"), Entry("Lembar Periksa Pasien"), Entry("Check List Kelengkapan Pendaftaran") }
                        : new[] { PrintFormatGroup("Check List Kelengkapan Pendaftaran", "rptCheckList"),
                            PrintFormatGroup("Lembar Periksa Pasien", "rptLembarPeriksa"),
                            kind == PatientMenuKind.Registration
                                ? Group("Label & Barcode", PatientLabel.Formats.Where(format => !format.Template.StartsWith("rptCheckList", StringComparison.Ordinal)
                                        && !format.Template.StartsWith("rptLembarPeriksa", StringComparison.Ordinal)
                                        && !format.Template.StartsWith("rptBuktiRegister", StringComparison.Ordinal)).Select(format => Entry(format.Title,
                                    hasVisit && Can("registrasi")
                                        ? () => Run(() => PatientLabelPreview.Open(grid, patient, format))
                                        : null)).ToArray())
                                : Entry("Label & Barcode") }),
                    new Separator(),
                    rujukInternal is null ? Entry("Rujukan") : Group("Rujukan",
                        Entry("Masuk", hasVisit && rujukMasuk is not null && RujukanMasukService.BisaMengelola(user)
                            ? () => Run(() => rujukMasuk(clicked)) : null),
                        Entry("Poli Internal", hasVisit && RujukanInternalService.BisaMengelola(user)
                            ? () => Run(() => rujukInternal(clicked)) : null),
                        Entry("Keluar", hasVisit && rujukKeluar is not null && Can(RujukanKeluarService.Izin)
                            ? () => Run(() => rujukKeluar(clicked)) : null)),
                    Entry("Bridging"),
                    Group("Input Data",
                        PatientNote(),
                        PatientReport("Berkas Digital Perawatan", "btnBerkasDigitalPerawatan"),
                        PatientReport("Insiden Keselamatan Pasien", "btnInsidenKeselamatanPasien")),
                    .. (kind == PatientMenuKind.Registration ? new[] { SortMenu(grid) } : Array.Empty<MenuItem>()),
                    Group("Hapus & Ubah Data", [.. manageItems, Entry("Hapus Data")]),
                ];
            e.Handled = true;
            menu.Open(pointer ? grid : row ?? (Control)grid);
        };
        return menu;
    }

    private static MenuItem Entry(string label, Action? action = null)
    {
        var item = new MenuItem { Header = label, IsEnabled = action is not null };
        if (action is not null) item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem Group(string label, params MenuItem[] items) => new() { Header = label, ItemsSource = items };

    private static MenuItem SortMenu(DataGrid grid)
    {
        (string Label, string Property)[] columns =
        [
            ("No. Rawat", nameof(RegistrasiListItem.NoRawat)), ("Tanggal", nameof(RegistrasiListItem.TanggalRegistrasi)),
            ("Dokter", nameof(RegistrasiListItem.NamaDokter)), ("Poli/Unit", nameof(RegistrasiListItem.NamaPoli)),
            ("Cara Bayar", nameof(RegistrasiListItem.JenisBayar)), ("Status", nameof(RegistrasiListItem.Status)),
            ("No. Reg.", nameof(RegistrasiListItem.NoReg)), ("No. RM.", nameof(RegistrasiListItem.NoRekamMedis)),
        ];
        return Group("Urutkan Data Berdasar", columns.SelectMany(field =>
        {
            var column = grid.Columns.OfType<DataGridBoundColumn>().FirstOrDefault(c => (c.Binding as Binding)?.Path == field.Property);
            return new[]
            {
                Entry($"{field.Label} Descending", column is null ? null : () => column.Sort(ListSortDirection.Descending)),
                Entry($"{field.Label} Ascending", column is null ? null : () => column.Sort(ListSortDirection.Ascending)),
            };
        }).ToArray());
    }
}
