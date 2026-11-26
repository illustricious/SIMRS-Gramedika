using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Pasien;

/// <summary>Dialog pendaftaran pasien baru dari Registrasi. Hasil dialog: No. RM, atau null bila dibatalkan.</summary>
internal sealed class PasienBaruWindow : Window
{
    private readonly PasienBaruService _service;

    private readonly TextBox _noRm = new() { IsReadOnly = true };
    private readonly TextBox _nama = Teks(40), _ktp = Teks(20), _tempatLahir = Teks(15), _namaIbu = Teks(40),
        _alamat = Teks(200), _kelurahan = Teks(60), _kecamatan = Teks(60), _kabupaten = Teks(60), _propinsi = Teks(30),
        _pekerjaan = Teks(60), _telepon = Teks(40), _email = Teks(50), _peserta = Teks(25),
        _namaPj = Teks(50), _pekerjaanPj = Teks(35), _alamatPj = Teks(100),
        _kelurahanPj = Teks(60), _kecamatanPj = Teks(60), _kabupatenPj = Teks(60), _propinsiPj = Teks(30);
    private readonly CalendarDatePicker _tanggalLahir = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _jk = Pilihan(new[] { "LAKI-LAKI", "PEREMPUAN" });
    private readonly ComboBox _golDarah = Pilihan(PilihanIdentitas.GolDarah);
    private readonly ComboBox _nikah = Pilihan(PilihanIdentitas.StatusNikah);
    private readonly ComboBox _agama = Pilihan(PilihanIdentitas.Agama);
    private readonly ComboBox _pendidikan = Pilihan(PasienBaruService.Pendidikan);
    private readonly ComboBox _keluarga = Pilihan(PilihanIdentitas.Hubungan);
    private readonly ComboBox _penjab = Pilihan(), _suku = Pilihan(), _bahasa = Pilihan(), _cacat = Pilihan(), _perusahaan = Pilihan();
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _simpan = new() { Content = "Simpan Pasien", MinWidth = 130, MinHeight = 34, Classes = { "accent" }, IsEnabled = false };
    private bool _menyimpan;

    public PasienBaruWindow(PasienBaruService service)
    {
        _service = service;
        Title = "Pasien Baru";
        Width = 900;
        Height = 680;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var isian = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 18 };
        var pasien = Kolom("Identitas Pasien",
            ("No. RM (perkiraan)", _noRm), ("Nama Pasien *", _nama), ("No. KTP/SIM", _ktp), ("Jenis Kelamin", _jk),
            ("Tempat Lahir", _tempatLahir), ("Tanggal Lahir *", _tanggalLahir), ("Nama Ibu", _namaIbu),
            ("Alamat", _alamat), ("Kelurahan", _kelurahan), ("Kecamatan", _kecamatan), ("Kabupaten", _kabupaten),
            ("Propinsi", _propinsi), ("Gol. Darah", _golDarah), ("Status Nikah", _nikah), ("Agama", _agama),
            ("Pendidikan", _pendidikan), ("Pekerjaan", _pekerjaan), ("No. Telepon", _telepon), ("Email", _email));
        var penanggung = Kolom("Pembayaran & Penanggung Jawab",
            ("Jenis Bayar *", _penjab), ("No. Peserta", _peserta), ("Hubungan P.J.", _keluarga), ("Nama P.J.", _namaPj),
            ("Pekerjaan P.J.", _pekerjaanPj), ("Alamat P.J.", _alamatPj), ("Kelurahan P.J.", _kelurahanPj),
            ("Kecamatan P.J.", _kecamatanPj), ("Kabupaten P.J.", _kabupatenPj), ("Propinsi P.J.", _propinsiPj),
            ("Suku/Bangsa *", _suku), ("Bahasa *", _bahasa), ("Cacat Fisik *", _cacat), ("Perusahaan *", _perusahaan));
        penanggung.Children.Add(Ui.Hint("Bila hubungan P.J. \"DIRI SENDIRI\", isian P.J. yang kosong mengikuti data pasien."));
        Grid.SetColumn(penanggung, 1);
        isian.Children.Add(pasien);
        isian.Children.Add(penanggung);

        var batal = new Button { Content = "Batal", MinWidth = 90, MinHeight = 34 };
        _simpan.Click += async (_, _) => await SimpanAsync();
        batal.Click += (_, _) => Close(null);

        var bawah = new StackPanel { Spacing = 8, Children = { _status, Ui.Bar(_simpan, batal) } };
        var layout = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(bawah, Dock.Bottom);
        layout.Children.Add(bawah);
        layout.Children.Add(new ScrollViewer { Content = isian, Padding = new Thickness(0, 0, 12, 8) });
        Content = layout;
        Loaded += async (_, _) => await MuatAsync();
        // Jangan tutup saat transaksi berjalan: Registrasi perlu menerima No. RM hasilnya.
        Closing += (_, e) => e.Cancel = _menyimpan;
    }

    private static TextBox Teks(int maks) => new() { MaxLength = maks };

    /// <summary>Daftar tetap langsung memilih baris pertama; daftar dari database diisi kemudian.</summary>
    private static ComboBox Pilihan(System.Collections.IEnumerable? daftar = null) => new()
    {
        ItemsSource = daftar,
        SelectedIndex = daftar is null ? -1 : 0,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private static StackPanel Kolom(string judul, params (string Label, Control Isian)[] baris)
    {
        var panel = new StackPanel { Spacing = 5 };
        panel.Children.Add(new TextBlock { Text = judul, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        foreach (var (label, isian) in baris)
        {
            var grid = new Grid { ColumnDefinitions = new("128,*") };
            grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            Avalonia.Automation.AutomationProperties.SetName(isian, label);
            Grid.SetColumn(isian, 1);
            grid.Children.Add(isian);
            panel.Children.Add(grid);
        }
        return panel;
    }

    private async Task MuatAsync()
    {
        try
        {
            var referensi = await _service.ReferensiAsync();
            _noRm.Text = referensi.PerkiraanNoRm;
            // Pilih otomatis hanya baris "-"; referensi lain harus dipilih petugas.
            static void Isi(ComboBox combo, IReadOnlyList<PilihanPasien> daftar)
            {
                combo.ItemsSource = daftar;
                combo.SelectedItem = daftar.FirstOrDefault(p => p.Nama.Trim() == "-");
            }
            Isi(_penjab, referensi.Penjab);
            Isi(_suku, referensi.SukuBangsa);
            Isi(_bahasa, referensi.Bahasa);
            Isi(_cacat, referensi.CacatFisik);
            Isi(_perusahaan, referensi.Perusahaan);
            _simpan.IsEnabled = true;
            _nama.Focus();
        }
        catch (Exception ex)
        {
            _status.Text = $"Data pendukung tidak dapat dimuat: {ex.Message}";
        }
    }

    private PasienBaruData Baca()
    {
        static string T(TextBox box) => box.Text?.Trim() ?? "";
        static string K(ComboBox combo) => (combo.SelectedItem as PilihanPasien)?.Kode ?? "";
        return new PasienBaruData
        {
            Nama = T(_nama), NoKtp = T(_ktp),
            JenisKelamin = _jk.SelectedIndex == 1 ? JenisKelamin.Perempuan : JenisKelamin.LakiLaki,
            TempatLahir = T(_tempatLahir),
            TanggalLahir = _tanggalLahir.SelectedDate is { } lahir ? DateOnly.FromDateTime(lahir) : null,
            NamaIbu = T(_namaIbu), Alamat = T(_alamat),
            Wilayah = new(T(_kelurahan), T(_kecamatan), T(_kabupaten), T(_propinsi)),
            GolonganDarah = ((Opsi<GolonganDarah>)_golDarah.SelectedItem!).Nilai,
            StatusPernikahan = ((Opsi<StatusPernikahan>)_nikah.SelectedItem!).Nilai,
            Agama = (string)_agama.SelectedItem!, Pendidikan = (string)_pendidikan.SelectedItem!,
            Pekerjaan = T(_pekerjaan), NoTelepon = T(_telepon), Email = T(_email),
            KodePenjab = K(_penjab), NoPeserta = T(_peserta),
            Keluarga = ((Opsi<HubunganPenanggungJawab>)_keluarga.SelectedItem!).Nilai,
            NamaKeluarga = T(_namaPj), PekerjaanPj = T(_pekerjaanPj), AlamatPj = T(_alamatPj),
            WilayahPj = new(T(_kelurahanPj), T(_kecamatanPj), T(_kabupatenPj), T(_propinsiPj)),
            SukuBangsa = K(_suku), Bahasa = K(_bahasa), CacatFisik = K(_cacat), Perusahaan = K(_perusahaan),
        };
    }

    private async Task SimpanAsync()
    {
        if (_menyimpan) return;
        _menyimpan = true;
        _simpan.IsEnabled = false;
        _status.Text = "Menyimpan...";
        try
        {
            var noRm = await _service.SimpanAsync(Baca());
            _menyimpan = false;
            Close(noRm);
        }
        catch (Exception ex)
        {
            _menyimpan = false;
            _simpan.IsEnabled = true;
            _status.Text = $"Gagal menyimpan pasien: {(ex.InnerException ?? ex).Message}";
        }
    }
}
