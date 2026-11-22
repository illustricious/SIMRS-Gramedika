using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Shared;

public enum PatientMenuAction { DataRekamMedis, RiwayatPerawatan, KamarInap, TindakanDanPemeriksaan, BillingPembayaran }
public enum PatientMenuKind { Visit, Registration, Emergency, Patient, Diagnostic }

public record PatientMenuRequest(string NoRawat, string NoRekamMedis, string NamaPasien, PatientMenuAction Action);
public record PatientMenuTarget(string NoRawat, string NoRekamMedis, string NamaPasien, bool IsInpatient = false);

public static class PatientContextMenu
{
    public static ContextMenu Attach<T>(DataGrid grid, Func<T, PatientMenuTarget?> getPatient, Action<PatientMenuRequest> execute,
        PatientMenuKind kind = PatientMenuKind.Visit, Action<T>? edit = null) where T : class
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
            MenuItem ActionItem(string text, PatientMenuAction action, bool allowed) =>
                Entry(text, allowed ? () => Run(() => execute(new(patient.NoRawat, patient.NoRekamMedis, patient.NamaPasien, action))) : null);

            var history = ActionItem("Riwayat Perawatan", PatientMenuAction.RiwayatPerawatan, Can("resume_pasien"));
            var identity = ActionItem("Identitas Pasien", PatientMenuAction.DataRekamMedis, Can("pasien"));
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
                    Group("Data Rekam Medis", identity, Entry("Resume Pasien"), history),
                    Group("Permintaan", Entry("Jadwal Operasi"), Entry("Permintaan Lab"),
                        Entry("Permintaan Radiologi"), Entry("Permintaan Rawat Inap")),
                    ActionItem("Kamar Inap", PatientMenuAction.KamarInap, hasVisit && !patient.IsInpatient && Can("kamar_inap")),
                    Group("Tindakan & Pemeriksaan",
                        ActionItem("Pemeriksaan Rawat Jalan", PatientMenuAction.TindakanDanPemeriksaan,
                            hasVisit && !patient.IsInpatient && Can("tindakan_ralan")),
                        Entry("Periksa Laboratorium"), Entry("Periksa Radiologi"), Entry("Operasi")),
                    Group("Obat", Entry("Pemberian Obat"), Entry("No. Resep"), Entry("Resep Dokter"), Entry("Copy Resep")),
                    Group("Billing/Pembayaran Pasien", Entry("Billing Parsial"),
                        ActionItem("Billing Total", PatientMenuAction.BillingPembayaran, hasVisit &&
                            (patient.IsInpatient ? Can("billing_ranap") : Can("kasir_ralan") || Can("billing_ralan")))),
                    new Separator(),
                    Entry("Laporan"), Entry("Grafik Analisa"), Entry("Surat-Surat"),
                    .. (kind == PatientMenuKind.Emergency
                        ? new[] { Entry("Gelang"), Entry("Lembar Periksa Pasien"), Entry("Check List Kelengkapan Pendaftaran") }
                        : new[] { Entry("Check List Kelengkapan Pendaftaran"), Entry("Lembar Periksa Pasien"), Entry("Label & Barcode") }),
                    new Separator(),
                    Entry("Rujukan"), Entry("Bridging"),
                    Group("Input Data", Entry("Catatan Pasien"), Entry("Berkas Digital"), Entry("Insiden Keselamatan Pasien")),
                    .. (kind == PatientMenuKind.Registration ? new[] { SortMenu(grid) } : Array.Empty<MenuItem>()),
                    Group("Hapus & Ubah Data",
                        Entry("Ubah Data Registrasi", edit is not null && Can("registrasi") ? () => Run(() =>
                        {
                            grid.SelectedItem = clicked;
                            edit(clicked);
                        }) : null),
                        Entry("Hapus Data")),
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
