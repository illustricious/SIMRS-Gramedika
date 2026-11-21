using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsKhanza.Config;
using SimrsKhanza.Data;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.UI.Modules.Informasi;

public partial class MenuAInformasiView : UserControl
{
    private readonly bool _jadwal;
    public event EventHandler? BackRequested;
    public MenuAInformasiView(bool jadwal)
    {
        InitializeComponent();
        _jadwal = jadwal;
        this.FindControl<WrapPanel>("FormJadwal")!.IsVisible = jadwal;
        this.FindControl<TextBlock>("Judul")!.Text = jadwal ? "Jadwal Praktek" : "Informasi Kamar";
    }
    public async Task ReloadAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new MenuARepository(db);
            if (_jadwal && this.FindControl<ComboBox>("Dokter")!.ItemsSource is null)
            {
                var lookup = new RegistrasiRepository(db);
                var dokter = this.FindControl<ComboBox>("Dokter")!;
                dokter.ItemsSource = await lookup.GetDokterAsync();
                dokter.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(SimrsKhanza.Core.Entities.Dokter.NamaDokter));
                var poli = this.FindControl<ComboBox>("Poli")!;
                poli.ItemsSource = await lookup.GetPoliklinikAsync();
                poli.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Poliklinik.NamaPoli));
            }
            var keyword = this.FindControl<TextBox>("KataKunci")!.Text?.Trim() ?? "";
            var grid = this.FindControl<DataGrid>("Tabel")!;
            if (_jadwal) { var rows = await repo.JadwalAsync(keyword); grid.ItemsSource = rows; this.FindControl<TextBlock>("Status")!.Text = $"Record: {rows.Count}"; }
            else { var rows = await repo.KamarAsync(keyword); grid.ItemsSource = rows; this.FindControl<TextBlock>("Status")!.Text = $"Record: {rows.Count}; Isi: {rows.Count(x => x.Status == "ISI")}; Kosong: {rows.Count(x => x.Status == "KOSONG")}"; }
        }
        catch (Exception ex) { this.FindControl<TextBlock>("Status")!.Text = $"Gagal memuat: {ex.Message}"; }
    }
    private async void OnCari(object? sender, RoutedEventArgs e) => await ReloadAsync();
    private async void OnSimpan(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("Status")!;
        var dokter = this.FindControl<ComboBox>("Dokter")!.SelectedItem as Dokter;
        var poli = this.FindControl<ComboBox>("Poli")!.SelectedItem as Poliklinik;
        var hari = (this.FindControl<ComboBox>("Hari")!.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (dokter is null || poli is null || hari is null || !TimeSpan.TryParse(this.FindControl<TextBox>("Mulai")!.Text, out var mulai) || !TimeSpan.TryParse(this.FindControl<TextBox>("Selesai")!.Text, out var selesai) || mulai < TimeSpan.Zero || selesai < TimeSpan.Zero || mulai >= TimeSpan.FromDays(1) || selesai >= TimeSpan.FromDays(1) || !int.TryParse(this.FindControl<TextBox>("Kuota")!.Text, out var kuota) || kuota < 0)
        { status.Text = "Lengkapi dokter, poli, hari, jam dan kuota yang valid."; return; }
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new MenuARepository(db).TambahJadwalAsync(dokter.KodeDokter, hari, mulai, selesai, poli.KodePoli, kuota);
            await ReloadAsync(); status.Text = "Jadwal berhasil disimpan.";
        }
        catch (Exception ex) { status.Text = $"Gagal menyimpan: {ex.Message}"; }
    }
    private void OnBack(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
