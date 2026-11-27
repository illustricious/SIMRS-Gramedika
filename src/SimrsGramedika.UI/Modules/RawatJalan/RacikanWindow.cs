using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatJalan;

/// <summary>Mengisi satu racikan resep dokter. Hasilnya RacikanOrder, atau null bila dibatalkan.</summary>
internal sealed class RacikanWindow : Window
{
    private readonly OrderRalanService _service;
    private readonly TextBox _nama = new() { PlaceholderText = "Nama racikan", MaxLength = 100, Width = 220 };
    private readonly ComboBox _metode = new() { MinWidth = 150 };
    private readonly TextBox _jumlahRacik = new() { PlaceholderText = "Jml racik", Width = 90 };
    private readonly TextBox _aturan = new() { PlaceholderText = "Aturan pakai", MaxLength = 150, Width = 260 };
    private readonly TextBox _keterangan = new() { PlaceholderText = "Keterangan", MaxLength = 50, Width = 220 };
    private readonly TextBox _cari = new() { PlaceholderText = "Cari kode atau nama obat", Width = 260 };
    private readonly TextBox _p1 = new() { Text = "1", Width = 70 };
    private readonly TextBox _p2 = new() { Text = "1", Width = 70 };
    private readonly TextBox _kandungan = new() { PlaceholderText = "mg atau %", MaxLength = 10, Width = 110 };
    private readonly TextBox _jumlah = new() { PlaceholderText = "Jumlah obat", Width = 115 };
    private readonly DataGrid _obat = Ui.Grid(new("kode", "Kode", 100), new("nama", "Nama", 300), new("kapasitas", "Kapasitas", 90));
    private readonly DataGrid _isi = Ui.Grid(new("nama", "Obat", 250), new("kapasitas", "Kapasitas", 85), new("pecahan", "P1/P2", 70),
        new("kandungan", "Kandungan", 95), new("jumlah", "Jumlah", 75));
    private readonly TextBlock _status = Ui.Status();
    private readonly List<BahanRacikan> _bahan = [];
    private bool _busy;

    public RacikanWindow(OrderRalanService service, IReadOnlyList<MetodeRacik> metode, RacikanOrder? awal)
    {
        _service = service;
        Title = awal is null ? "Racikan Baru" : "Ubah Racikan";
        Width = 820;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _metode.ItemsSource = metode;
        if (awal is not null)
        {
            _nama.Text = awal.Nama;
            // Metode lama yang sudah dihapus dibiarkan kosong supaya dipilih ulang.
            _metode.SelectedItem = metode.FirstOrDefault(m => m.Kode == awal.KodeMetode);
            _jumlahRacik.Text = awal.Jumlah.ToString(CultureInfo.CurrentCulture);
            _aturan.Text = awal.Aturan;
            _keterangan.Text = awal.Keterangan;
            _bahan.AddRange(awal.Bahan);
        }

        // Urutan mengisi mengikuti Khanza: P1/P2 menentukan kandungan, kandungan atau persen menentukan jumlah obat.
        foreach (var kotak in new[] { _p1, _p2 })
            kotak.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) HitungKandungan(); };
        _kandungan.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) HitungJumlah(); };
        _jumlahRacik.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) HitungSemua(); };
        // Hanya satu daftar yang terpilih, supaya obat yang diubah selalu jelas.
        _obat.SelectionChanged += (_, _) =>
        {
            if (Ui.Selected<BahanRacikan>(_obat) is null) return;
            if (_isi.SelectedItem is not null) { _isi.SelectedItem = null; KosongkanDosis(); }
            HitungKandungan();
            HitungJumlah();
        };

        var layout = new Grid { Margin = new Thickness(12), RowSpacing = 6, RowDefinitions = new("Auto,Auto,Auto,*,Auto,Auto,*,Auto,Auto") };
        Control[] isi =
        [
            Ui.Bar(_nama, _metode, _jumlahRacik),
            Ui.Bar(_aturan, _keterangan),
            Ui.Bar(_cari, Ui.Btn("Cari obat", async (_, _) => await CariAsync())),
            _obat,
            Ui.Bar(new TextBlock { Text = "P1" }, _p1, new TextBlock { Text = "/ P2" }, _p2, _kandungan, _jumlah,
                Ui.Btn("Tambah / ganti obat", (_, _) => Jalankan(Tambah)), Ui.Btn("Lepas obat", (_, _) => Jalankan(Lepas))),
            Ui.Hint("Jumlah obat dihitung dari kandungan per bungkus atau persen, dibulatkan satu desimal, dan boleh diisi sendiri."),
            _isi,
            Ui.Bar(Ui.Btn("Gunakan racikan", (_, _) => Jalankan(Gunakan)), Ui.Btn("Batal", (_, _) => Close(null))),
            _status,
        ];
        for (var i = 0; i < isi.Length; i++) { Grid.SetRow(isi[i], i); layout.Children.Add(isi[i]); }
        _isi.SelectionChanged += (_, _) =>
        {
            if (Ui.Selected<BahanRacikan>(_isi) is not { } bahan) return;
            _obat.SelectedItem = null;
            MuatBahan(bahan);
        };
        Content = layout;
        IsiBahan();
        Loaded += async (_, _) => await CariAsync();
    }

    private int? JumlahRacik => int.TryParse(_jumlahRacik.Text, NumberStyles.None, CultureInfo.CurrentCulture, out var n) && n > 0 ? n : null;

    /// <summary>Obat yang sedang diisi: pilihan pada daftar isi, kalau tidak ada pilihan pada hasil pencarian.</summary>
    private BahanRacikan? Aktif => Ui.Selected<BahanRacikan>(_isi) ?? Ui.Selected<BahanRacikan>(_obat);

    private static string Angka(decimal nilai) => nilai.ToString("0.##########", CultureInfo.InvariantCulture);

    private static decimal AngkaAtau(string? teks, string pesan) =>
        HitungRacikan.Baca(teks ?? "", out var nilai, out var persen) && !persen ? nilai : throw new ArgumentException(pesan);

    private void HitungKandungan()
    {
        if (Aktif is not { } obat || !HitungRacikan.Baca(_p1.Text ?? "", out var p1, out _) ||
            !HitungRacikan.Baca(_p2.Text ?? "", out var p2, out _) || p1 == p2) return;
        if (HitungRacikan.Kandungan(obat.Kapasitas, p1, p2) is { } kandungan) _kandungan.Text = Angka(kandungan);
        else if (obat.Kapasitas <= 0) Ui.Err(_status, "Kapasitas obat masih kosong, isi jumlah obat sendiri.");
    }

    private void HitungJumlah()
    {
        if (Aktif is not { } obat || JumlahRacik is not { } racik || string.IsNullOrWhiteSpace(_kandungan.Text)) return;
        var calon = obat with { Kandungan = _kandungan.Text.Trim(), Jumlah = 0 };
        var lain = _bahan.Where(b => b.Kode != obat.Kode).ToList();
        var hasil = HitungRacikan.Ulang(racik, [.. lain, calon])[^1];
        _jumlah.Text = hasil.Jumlah > 0 ? Angka(hasil.Jumlah) : "";
    }

    private void HitungSemua()
    {
        if (JumlahRacik is not { } racik || _bahan.Count == 0) return;
        var hasil = HitungRacikan.Ulang(racik, _bahan);
        if (hasil.SequenceEqual(_bahan)) return;
        _bahan.Clear();
        _bahan.AddRange(hasil);
        IsiBahan();
        Ui.Ok(_status, "Jumlah obat dihitung ulang mengikuti jumlah racik.");
    }

    private async Task CariAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var hasil = await _service.CariBahanAsync(_cari.Text ?? "");
            Ui.FillRows(_obat, hasil.Select(b => new GridRow([b.Kode, b.Nama, Angka(b.Kapasitas)], b)));
        }
        catch (InvalidOperationException ex) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Data obat gagal dimuat. Periksa koneksi database."); }
        finally { _busy = false; }
    }

    private void MuatBahan(BahanRacikan b)
    {
        _p1.Text = Angka(b.P1);
        _p2.Text = Angka(b.P2);
        _kandungan.Text = b.Kandungan;
        // Terakhir, supaya jumlah tersimpan tidak tertimpa hitungan.
        _jumlah.Text = Angka(b.Jumlah);
    }

    private void Tambah()
    {
        var obat = Aktif ?? throw new ArgumentException("Pilih obat dari hasil pencarian.");
        var p1 = AngkaAtau(_p1.Text, "P1 harus angka.");
        var p2 = AngkaAtau(_p2.Text, "P2 harus angka.");
        var jumlah = AngkaAtau(_jumlah.Text, "Isi jumlah obat.");
        if (jumlah <= 0) throw new ArgumentException("Jumlah obat harus lebih dari 0.");
        var baru = obat with { P1 = p1, P2 = p2, Kandungan = _kandungan.Text?.Trim() ?? "", Jumlah = jumlah };
        var posisi = _bahan.FindIndex(b => b.Kode.Equals(obat.Kode, StringComparison.OrdinalIgnoreCase));
        if (posisi < 0) _bahan.Add(baru);
        else _bahan[posisi] = baru;
        Bersihkan();
        IsiBahan();
    }

    private void Lepas()
    {
        var b = Ui.Selected<BahanRacikan>(_isi) ?? throw new ArgumentException("Pilih obat pada isi racikan.");
        _bahan.Remove(b);
        Bersihkan();
        IsiBahan();
    }

    private void Bersihkan()
    {
        _isi.SelectedItem = null;
        _obat.SelectedItem = null;
        KosongkanDosis();
    }

    private void KosongkanDosis()
    {
        _p1.Text = "1";
        _p2.Text = "1";
        _kandungan.Text = "";
        _jumlah.Text = "";
    }

    private void IsiBahan() => Ui.FillRows(_isi, _bahan.Select(b => new GridRow(
        [b.Nama, Angka(b.Kapasitas), $"{Angka(b.P1)}/{Angka(b.P2)}", b.Kandungan, Angka(b.Jumlah)], b)));

    private void Gunakan()
    {
        var nama = _nama.Text?.Trim() ?? "";
        var aturan = _aturan.Text?.Trim() ?? "";
        if (nama.Length == 0 || _metode.SelectedItem is not MetodeRacik metode || JumlahRacik is not { } racik || aturan.Length == 0)
            throw new ArgumentException("Isi nama racikan, metode, jumlah racik dan aturan pakai.");
        if (_bahan.Count == 0) throw new ArgumentException("Tambahkan paling sedikit satu obat.");
        Close(new RacikanOrder(nama, metode.Kode, metode.Nama, racik, aturan, _keterangan.Text?.Trim() ?? "", _bahan.ToList()));
    }

    private void Jalankan(Action aksi)
    {
        try { aksi(); }
        catch (ArgumentException ex) { Ui.Err(_status, ex.Message); }
    }
}
