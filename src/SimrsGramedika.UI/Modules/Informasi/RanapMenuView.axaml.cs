using Avalonia.Controls;
using Avalonia.Interactivity;
using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.Core.Entities;
namespace SimrsGramedika.UI.Modules.Informasi;
public partial class RanapMenuView : UserControl
{
    private readonly string _jenis;
    public event EventHandler? BackRequested;
    public RanapMenuView(string jenis)
    {
        InitializeComponent(); _jenis=jenis;
        this.FindControl<TextBlock>("Judul")!.Text=$"{jenis}";
        this.FindControl<CalendarDatePicker>("Dari")!.SelectedDate=DateTime.Today;
        this.FindControl<CalendarDatePicker>("Sampai")!.SelectedDate=DateTime.Today;
        this.FindControl<ComboBox>("Jenis")!.IsVisible=jenis=="Tindakan Ranap";
        this.FindControl<WrapPanel>("FormDpjp")!.IsVisible=jenis=="DPJP Ranap";
    }
    public async Task ReloadAsync()
    {
        var status=this.FindControl<TextBlock>("Status")!;
        var dari=this.FindControl<CalendarDatePicker>("Dari")!.SelectedDate;
        var sampai=this.FindControl<CalendarDatePicker>("Sampai")!.SelectedDate;
        if(dari is null || sampai is null || dari.Value.Date>sampai.Value.Date) { status.Text="Periksa periode tanggal."; return; }
        try
        {
            using var db=SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var repo=new RanapMenuRepository(db); var q=this.FindControl<TextBox>("Cari")!.Text?.Trim() ?? "";
            if (_jenis=="DPJP Ranap" && this.FindControl<ComboBox>("Dokter")!.ItemsSource is null)
            {
                var combo=this.FindControl<ComboBox>("Dokter")!;
                combo.ItemsSource=await new RegistrasiRepository(db).GetDokterAsync();
                combo.DisplayMemberBinding=new Avalonia.Data.Binding(nameof(SimrsGramedika.Core.Entities.Dokter.NamaDokter));
            }
            var grid=this.FindControl<DataGrid>("Tabel")!;
            if(_jenis=="DPJP Ranap") { var rows=await repo.DpjpAsync(dari.Value,sampai.Value,q); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
            else if(_jenis=="Tindakan Ranap") { var rows=await repo.TindakanAsync(dari.Value,sampai.Value,q,this.FindControl<ComboBox>("Jenis")!.SelectedIndex); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
            else if(_jenis=="Operasi/VK") { var rows=await repo.OperasiAsync(dari.Value,sampai.Value,q); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
            else if(_jenis=="Rujukan Keluar") { var rows=await new RujukanObatRepository(db).KeluarAsync(dari.Value,sampai.Value,q); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
            else if(_jenis=="Rujukan Masuk") { var rows=await new RujukanObatRepository(db).MasukAsync(dari.Value,sampai.Value,q); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
            else { var rows=await new RujukanObatRepository(db).ObatAsync(dari.Value,sampai.Value,q); grid.ItemsSource=rows; status.Text=$"Record: {rows.Count}"; }
        }
        catch(Exception ex) { status.Text=$"Gagal memuat: {ex.Message}"; }
    }
    private async void OnCari(object? sender,RoutedEventArgs e)=>await ReloadAsync();
    private void OnPilih(object? sender,SelectionChangedEventArgs e)
    {
        if (this.FindControl<DataGrid>("Tabel")!.SelectedItem is not DpjpRow row) return;
        this.FindControl<TextBox>("Rawat")!.Text=row.NoRawat;
        var combo=this.FindControl<ComboBox>("Dokter")!;
        combo.SelectedItem=(combo.ItemsSource as IEnumerable<Dokter>)?.FirstOrDefault(x=>x.KodeDokter==row.KodeDokter);
        this.FindControl<Border>("Konfirmasi")!.IsVisible=false;
    }
    private async Task SimpanAsync(bool ubah)
    {
        var status=this.FindControl<TextBlock>("Status")!;
        var rawat=this.FindControl<TextBox>("Rawat")!.Text?.Trim() ?? "";
        var dokter=this.FindControl<ComboBox>("Dokter")!.SelectedItem as Dokter;
        var lama=ubah ? this.FindControl<DataGrid>("Tabel")!.SelectedItem as DpjpRow : null;
        if(rawat.Length==0 || dokter is null || (ubah && lama is null)) { status.Text="Isi nomor rawat, pilih dokter dan data yang akan diubah."; return; }
        try { using var db=SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load()); await new RanapMenuRepository(db).SimpanDpjpAsync(rawat,dokter.KodeDokter,lama); await ReloadAsync(); status.Text="DPJP tersimpan."; }
        catch(Exception ex) { status.Text=$"Gagal menyimpan: {ex.Message}"; }
    }
    private async void OnTambah(object? sender,RoutedEventArgs e)=>await SimpanAsync(false);
    private async void OnUbah(object? sender,RoutedEventArgs e)=>await SimpanAsync(true);
    private void OnHapus(object? sender,RoutedEventArgs e) { if(this.FindControl<DataGrid>("Tabel")!.SelectedItem is DpjpRow) this.FindControl<Border>("Konfirmasi")!.IsVisible=true; else this.FindControl<TextBlock>("Status")!.Text="Pilih DPJP yang akan dihapus."; }
    private void OnBatal(object? sender,RoutedEventArgs e)=>this.FindControl<Border>("Konfirmasi")!.IsVisible=false;
    private async void OnHapusYa(object? sender,RoutedEventArgs e)
    {
        if(this.FindControl<DataGrid>("Tabel")!.SelectedItem is not DpjpRow row) return;
        var status=this.FindControl<TextBlock>("Status")!;
        try { using var db=SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load()); await new RanapMenuRepository(db).HapusDpjpAsync(row); OnBatal(sender,e); await ReloadAsync(); status.Text="DPJP dihapus."; }
        catch(Exception ex) { status.Text=$"Gagal menghapus: {ex.Message}"; }
    }
    private void OnBack(object? sender,RoutedEventArgs e)=>BackRequested?.Invoke(this,EventArgs.Empty);
}
