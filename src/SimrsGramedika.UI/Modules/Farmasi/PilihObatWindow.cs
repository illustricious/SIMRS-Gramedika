using Avalonia.Controls;
using Avalonia.Input;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Farmasi;

/// <summary>Mencari obat berstok di depo resep untuk ditambahkan atau dipakai mengganti.</summary>
internal sealed class PilihObatWindow : FormDialog
{
    private readonly Func<string, Task<IReadOnlyList<BarisValidasi>>> _cariObat;
    private readonly TextBox _cari = new() { PlaceholderText = "Kode atau nama obat, lalu Enter" };
    private readonly DataGrid _hasil = Ui.Grid(new Col("kode", "Kode"), new Col("nama", "Nama Obat", 260), new Col("satuan", "Satuan"),
        new Col("stok", "Stok"), new Col("harga", "Harga"), new Col("batch", "No. Batch"));

    public BarisValidasi? Dipilih { get; private set; }

    public PilihObatWindow(string judul, Func<string, Task<IReadOnlyList<BarisValidasi>>> cariObat) : base(judul, 760)
    {
        _cariObat = cariObat;
        _hasil.Height = 320;
        _cari.KeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await CariAsync();
        };
        var tombol = new Button { Content = "Cari" };
        tombol.Click += async (_, _) => await CariAsync();
        var atas = new DockPanel();
        DockPanel.SetDock(tombol, Dock.Right);
        atas.Children.Add(tombol);
        atas.Children.Add(_cari);
        SetBody(new StackPanel { Spacing = 8, Children = { atas, _hasil } }, "Pilih");
        Loaded += async (_, _) =>
        {
            _cari.Focus();
            await CariAsync();
        };
    }

    private async Task CariAsync()
    {
        ErrorText.Text = "";
        try
        {
            var daftar = await _cariObat(_cari.Text ?? "");
            Ui.FillRows(_hasil, daftar.Select(b => new GridRow(
                [b.Kode, b.Nama, b.Satuan, b.Stok.ToString("0.##"), Ui.Money(b.Harga), b.NoBatch], b)));
            if (daftar.Count == 0) ErrorText.Text = "Tidak ada obat berstok yang cocok di depo ini.";
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }

    protected override Task OnSaveAsync()
    {
        Dipilih = Ui.Selected<BarisValidasi>(_hasil) ?? throw new InvalidOperationException("Pilih obat pada tabel.");
        return Task.CompletedTask;
    }
}
