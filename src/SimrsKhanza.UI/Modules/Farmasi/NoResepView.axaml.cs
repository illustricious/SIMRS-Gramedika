using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Farmasi;

public partial class NoResepView : UserControl
{
    public event EventHandler? BackRequested;
    public NoResepView()
    {
        InitializeComponent();
        this.FindControl<CalendarDatePicker>("TanggalBox")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("FromBox")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToBox")!.SelectedDate = DateTime.Today;
        this.FindControl<TextBox>("JamBox")!.Text = DateTime.Now.ToString("HH:mm:ss");
    }
    private void TampilkanPesan(string message) => this.FindControl<TextBlock>("Pesan")!.Text = message;
    public async Task ReloadAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var from = this.FindControl<CalendarDatePicker>("FromBox")!.SelectedDate ?? DateTime.Today;
            var to = this.FindControl<CalendarDatePicker>("ToBox")!.SelectedDate ?? DateTime.Today;
            if (from.Date > to.Date) { TampilkanPesan("Periode awal harus sebelum periode akhir."); return; }
            var rows = await new NoResepRepository(db).CariAsync(from, to, this.FindControl<TextBox>("CariBox")!.Text?.Trim() ?? "");
            this.FindControl<DataGrid>("ResepGrid")!.ItemsSource = rows;
            TampilkanPesan($"Record: {rows.Count}");
        }
        catch (Exception ex) { TampilkanPesan($"Gagal memuat: {ex.Message}"); }
    }
    public async Task LoadDokterAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var combo = this.FindControl<ComboBox>("DokterBox")!;
            combo.ItemsSource = await new RegistrasiRepository(db).GetDokterAsync();
            combo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Dokter.NamaDokter));
        }
        catch (Exception ex) { TampilkanPesan($"Gagal memuat dokter: {ex.Message}"); }
    }
    private async void OnCari(object? sender, RoutedEventArgs e) => await ReloadAsync();
    private async void OnPilih(object? sender, SelectionChangedEventArgs e)
    {
        var grid = this.FindControl<DataGrid>("ObatGrid")!;
        grid.ItemsSource = null;
        if (this.FindControl<DataGrid>("ResepGrid")!.SelectedItem is not NoResepRow row) return;
        try { using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load()); grid.ItemsSource = await new NoResepRepository(db).ObatAsync(row); }
        catch (Exception ex) { TampilkanPesan($"Gagal memuat obat: {ex.Message}"); }
    }
    private async void OnSimpan(object? sender, RoutedEventArgs e)
    {
        var rawat = this.FindControl<TextBox>("RawatBox")!.Text?.Trim() ?? "";
        var dokter = this.FindControl<ComboBox>("DokterBox")!.SelectedItem as Dokter;
        var tanggal = this.FindControl<CalendarDatePicker>("TanggalBox")!.SelectedDate;
        if (rawat.Length == 0 || dokter is null || tanggal is null) { TampilkanPesan("Isi nomor rawat, dokter, dan tanggal."); return; }
        if (!TimeSpan.TryParse(this.FindControl<TextBox>("JamBox")!.Text, out var jam) || jam < TimeSpan.Zero || jam >= TimeSpan.FromDays(1)) { TampilkanPesan("Jam harus berformat HH:mm:ss."); return; }
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            if (!await db.RegPeriksaSet.AnyAsync(r => r.NoRawat == rawat)) { TampilkanPesan("Nomor rawat tidak ditemukan."); return; }
            var status = this.FindControl<ComboBox>("StatusBox")!.SelectedIndex == 1 ? "ranap" : "ralan";
            var nomor = await new NoResepRepository(db).SimpanAsync(rawat, dokter.KodeDokter, tanggal.Value, jam, status);
            await ReloadAsync(); TampilkanPesan($"Nomor resep {nomor} berhasil disimpan.");
        }
        catch (Exception ex) { TampilkanPesan($"Gagal menyimpan: {ex.Message}"); }
    }
    private void OnBack(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
