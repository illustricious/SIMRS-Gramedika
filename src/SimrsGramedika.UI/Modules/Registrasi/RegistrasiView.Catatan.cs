using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Data;

namespace SimrsGramedika.UI.Modules.Registrasi;

/// <summary>Catatan pasien yang muncul saat nama pasien diklik di tabel.</summary>
public partial class RegistrasiView
{
    private string? _catatanRm;
    private int _catatanVersi;

    private void PasangCatatanPasien()
    {
        foreach (var nama in new[] { "RegistrasiGrid", "RujukanInternalGrid" })
            this.FindControl<DataGrid>(nama)!.CellPointerPressed += OnSelCatatanDitekan;
    }

    private async void OnSelCatatanDitekan(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        if (e.Column.Header as string != "Pasien" || !e.PointerPressedEventArgs.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
        switch (e.Row.DataContext)
        {
            case RegistrasiListItem r: await TampilkanCatatanAsync(r.NoRekamMedis, r.NamaPasien); break;
            case RujukanInternalItem r: await TampilkanCatatanAsync(r.NoRm, r.Pasien); break;
        }
    }

    internal async Task TampilkanCatatanAsync(string noRekamMedis, string namaPasien)
    {
        TutupCatatan();
        if (string.IsNullOrWhiteSpace(noRekamMedis)) return;
        _catatanRm = noRekamMedis;
        var versi = _catatanVersi;
        string isi;
        try
        {
            isi = await new CatatanPasienService(DatabaseSettings.Load()).UntukDitampilkanAsync(noRekamMedis);
        }
        catch (Exception ex)
        {
            // Catatan bisa berisi peringatan penting, jadi kegagalan tidak boleh tampak seperti "tidak ada catatan".
            isi = $"Catatan pasien tidak dapat dimuat: {ex.Message}";
        }
        if (versi != _catatanVersi || string.IsNullOrWhiteSpace(isi)) return;
        this.FindControl<TextBlock>("CatatanJudul")!.Text = $"Catatan pasien {noRekamMedis} — {namaPasien}";
        this.FindControl<SelectableTextBlock>("CatatanIsi")!.Text = isi;
        this.FindControl<Border>("CatatanBanner")!.IsVisible = true;
    }

    private void TutupCatatan()
    {
        _catatanVersi++;
        _catatanRm = null;
        this.FindControl<Border>("CatatanBanner")!.IsVisible = false;
    }

    private void OnTutupCatatanClick(object? sender, RoutedEventArgs e) => TutupCatatan();
}
