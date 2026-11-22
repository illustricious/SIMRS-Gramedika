using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SimrsKhanza.Config;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Pasien;

internal sealed class PatientHistoryWindow : Window
{
    private readonly DataGrid _grid = Ui.Grid(
        new("no_rawat", "No. Rawat", 150), new("tanggal", "Tanggal", 100),
        new("unit", "Poli/Unit", 160), new("dokter", "Dokter", 175),
        new("status", "Status", 110), new("cara_bayar", "Cara Bayar", 125));

    public PatientHistoryWindow(PatientMenuRequest patient)
    {
        Title = "Riwayat Perawatan";
        Width = 900;
        Height = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var status = Ui.Status();
        var close = Ui.Btn("Tutup", (_, _) => Close());
        close.HorizontalAlignment = HorizontalAlignment.Right;
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(16), RowSpacing = 10 };
        layout.Children.Add(Ui.Title($"{patient.NamaPasien} · No. RM {patient.NoRekamMedis}"));
        Grid.SetRow(_grid, 1);
        layout.Children.Add(_grid);
        var footer = new Grid { ColumnDefinitions = new("*,Auto") };
        Grid.SetColumn(close, 1);
        footer.Children.Add(status);
        footer.Children.Add(close);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        Content = layout;

        Opened += async (_, _) =>
        {
            Ui.Info(status, "Memuat riwayat kunjungan…");
            try
            {
                using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
                var visits = await new RegistrasiRepository(db).GetPatientHistoryAsync(patient.NoRekamMedis);
                Ui.FillRows(_grid, visits.Select(r => new GridRow(
                    [r.NoRawat, r.TanggalRegistrasi?.ToString("dd/MM/yyyy") ?? "", r.NamaPoli ?? "",
                     r.NamaDokter ?? "", r.StatusRawat ?? "", r.JenisBayar ?? ""])));
                Ui.Info(status, visits.Count == 0 ? "Belum ada riwayat kunjungan." : $"{visits.Count} kunjungan");
            }
            catch (Exception)
            {
                Ui.Err(status, "Riwayat kunjungan tidak dapat dimuat.");
            }
        };
    }
}
