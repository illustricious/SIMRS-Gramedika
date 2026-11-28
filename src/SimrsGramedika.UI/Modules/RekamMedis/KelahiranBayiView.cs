using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Controls;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RekamMedis;

/// <summary>Data kelahiran bayi: daftar di kiri, isian di kanan.</summary>
public sealed class KelahiranBayiView : PageView
{
    private readonly TextBox _cari = Ui.Text("Cari No. RM, bayi, ibu, ayah, No. SKL atau penolong");
    private readonly CalendarDatePicker _dari = new() { SelectedDate = DateTime.Today.AddDays(-30) };
    private readonly CalendarDatePicker _sampai = new() { SelectedDate = DateTime.Today };
    private readonly DataGrid _daftar = Ui.Grid(new("", "Tgl. Lahir", 100), new("", "No. RM", 90), new("", "Bayi", 160),
        new("", "JK", 50), new("", "Ibu", 140), new("", "Berat", 70), new("", "No. SKL", 160), new("", "Penolong", 140));

    private readonly TextBlock _mode = Ui.Section("");
    private readonly TextBox _noRm = new() { IsReadOnly = true, Name = "NoRmBox" };
    private readonly TextBox _nama = Isian("NamaBox", 40), _namaIbu = Isian("NamaIbuBox", 40), _umurIbu = Isian("UmurIbuBox", 8),
        _namaAyah = Isian("NamaAyahBox", 50), _umurAyah = Isian("UmurAyahBox", 8), _alamat = Isian("AlamatBox", 200),
        _jam = Isian("JamBox", 8), _berat = Isian("BeratBox", 10), _panjang = Isian("PanjangBox", 10), _kepala = Isian("KepalaBox", 10),
        _dada = Isian("DadaBox", 10), _perut = Isian("PerutBox", 10), _anakKe = Isian("AnakKeBox", 2), _g = Isian("GBox", 10),
        _p = Isian("PBox", 10), _a = Isian("ABox", 10), _proses = Isian("ProsesBox", 60), _ketuban = Isian("KetubanBox", 60),
        _penyulit = Isian("PenyulitBox", 60), _diagnosa = Isian("DiagnosaBox", 60), _keterangan = Isian("KeteranganBox", 50),
        _resusitasi = Isian("ResusitasiBox", 100), _obat = Isian("ObatBox", 300), _miksi = Isian("MiksiBox", 100),
        _mekonium = Isian("MekoniumBox", 100), _noSkl = Isian("NoSklBox", 30);
    private readonly TextBox _penolong = new() { IsReadOnly = true, PlaceholderText = "Belum dipilih" };
    private readonly ComboBox _jk = Ui.Choice(["LAKI-LAKI", "PEREMPUAN"]);
    private readonly CalendarDatePicker _tglLahir = new() { Name = "TglLahirPicker" }, _tglDaftar = new();

    private static readonly (string Aspek, string Nilai)[] AspekApgar =
    [
        ("Frekuensi Jantung", "0 tidak ada · 1 di bawah 100 · 2 di atas 100"),
        ("Usaha Nafas", "0 tidak ada · 1 lambat tak teratur · 2 menangis kuat"),
        ("Tonus Otot", "0 lumpuh · 1 ekstensi/fleksi sedikit · 2 gerakan aktif"),
        ("Refleks", "0 tidak ada respon · 1 pergerakan sedikit · 2 menangis"),
        ("Warna", "0 biru pucat · 1 tubuh merah, tangan dan kaki biru · 2 kemerahan"),
    ];
    // [menit, aspek]: menit 1, 5 dan 10.
    private readonly ComboBox[,] _apgar = new ComboBox[3, 5];
    private readonly TextBlock[] _totalApgar = [new(), new(), new()];

    private readonly Button _pilihBayi, _simpan, _hapus, _cetak;
    private readonly List<Control> _identitas;
    private KelahiranBayi? _dilihat;
    private string _noRmDipilih = "";
    private string _nik = "";
    private bool _sibuk;
    private int _urutanNomor;

    public KelahiranBayiView()
    {
        _identitas = [_nama, _jk, _tglLahir, _tglDaftar, _namaIbu, _alamat];
        _pilihBayi = Ui.Btn("Pilih Pasien Bayi…", async (_, _) => await PilihBayiAsync());
        _pilihBayi.Name = "PilihBayiButton";
        _simpan = Ui.Btn("Simpan", async (_, _) => await SimpanAsync());
        _simpan.Name = "SimpanButton";
        _hapus = Ui.Btn("Hapus", async (_, _) => await HapusAsync());
        _hapus.Name = "HapusButton";
        _cetak = Ui.Btn("Cetak Surat Keterangan Lahir", async (_, _) => await CetakAsync());
        var pilihIbu = Ui.Btn("Pilih Ibu…", async (_, _) => await PilihIbuAsync());
        var pilihPenolong = Ui.Btn("Pilih Penolong…", async (_, _) => await PilihPenolongAsync());

        var apgar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 10, RowSpacing = 4,
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
        };
        void Taruh(Control c, int baris, int kolom) { Grid.SetRow(c, baris); Grid.SetColumn(c, kolom); apgar.Children.Add(c); }
        foreach (var (menit, kolom) in new[] { ("Menit 1", 1), ("Menit 5", 2), ("Menit 10", 3) })
            Taruh(new TextBlock { Text = menit, FontWeight = FontWeight.Bold }, 0, kolom);
        for (var a = 0; a < 5; a++)
        {
            Taruh(new StackPanel
            {
                Children = { new TextBlock { Text = AspekApgar[a].Aspek }, Kecil(AspekApgar[a].Nilai) },
            }, a + 1, 0);
            for (var m = 0; m < 3; m++)
            {
                var pilihan = new ComboBox { ItemsSource = new[] { "0", "1", "2" }, MinWidth = 76, Name = $"Apgar{m}{a}Box" };
                pilihan.SelectionChanged += (_, _) => HitungApgar();
                _apgar[m, a] = pilihan;
                Taruh(pilihan, a + 1, m + 1);
            }
        }
        Taruh(new TextBlock { Text = "Nilai APGAR", FontWeight = FontWeight.Bold }, 6, 0);
        for (var m = 0; m < 3; m++)
        {
            _totalApgar[m].Name = $"TotalApgar{m}Text";
            _totalApgar[m].FontWeight = FontWeight.Bold;
            Taruh(_totalApgar[m], 6, m + 1);
        }

        var isian = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                _mode,
                Ui.Bar(_pilihBayi, pilihIbu),
                Ui.Section("Identitas"),
                Ui.FormColumns(
                [
                    ("No. RM", _noRm), ("Nama Bayi *", _nama), ("Jenis Kelamin", _jk), ("Tanggal Lahir *", _tglLahir),
                    ("Jam Lahir *", _jam), ("Tanggal Daftar", _tglDaftar), ("Nama Ibu", _namaIbu), ("Umur Ibu", _umurIbu),
                    ("Nama Ayah", _namaAyah), ("Umur Ayah", _umurAyah), ("Alamat", _alamat), ("No. SKL", _noSkl),
                ], 2),
                Ui.Section("Kelahiran"),
                Ui.FormColumns(
                [
                    ("Berat Badan", _berat), ("Panjang Badan", _panjang), ("Lingkar Kepala", _kepala), ("Lingkar Dada", _dada),
                    ("Lingkar Perut", _perut), ("Anak Ke", _anakKe), ("Gravida (G)", _g), ("Para (P)", _p), ("Abortus (A)", _a),
                    ("Proses Lahir", _proses), ("Ketuban", _ketuban), ("Penyulit Kehamilan", _penyulit), ("Diagnosa", _diagnosa),
                    ("Keterangan", _keterangan),
                ], 2),
                Ui.Form(("Penolong *", _penolong)),
                Ui.Bar(pilihPenolong),
                Ui.Section("APGAR *"),
                apgar,
                Ui.Section("Tindakan dan Kondisi Bayi"),
                Ui.Form(("Resusitasi", _resusitasi), ("Obat Diberikan", _obat), ("Miksi", _miksi), ("Mekonium", _mekonium)),
            },
        };

        var kiri = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 6 };
        var saringan = Ui.Bar(_cari, Ui.L("Lahir"), _dari, Ui.L("s.d."), _sampai,
            Ui.Btn("Tampilkan", async (_, _) => await ReloadAsync()));
        _cari.MinWidth = 300;
        _cari.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) await ReloadAsync(); };
        Grid.SetRow(_daftar, 1);
        kiri.Children.Add(saringan);
        kiri.Children.Add(_daftar);
        var gulir = new ScrollViewer
        {
            Content = isian, Padding = new Avalonia.Thickness(0, 0, 14, 8),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        var badan = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,3*"), ColumnSpacing = 12 };
        Grid.SetColumn(gulir, 1);
        badan.Children.Add(kiri);
        badan.Children.Add(gulir);
        Build("Kelahiran Bayi", badan, "Isian bertanda * wajib. No. RM dan No. SKL dibuat otomatis bila dikosongkan.",
            Ui.Bar(Ui.Btn("Baru", async (_, _) => await BaruAsync()), _simpan, _hapus, _cetak));

        _daftar.SelectionChanged += async (_, _) => await PilihBarisAsync();
        _tglLahir.SelectedDateChanged += async (_, _) => await PerbaruiNomorAsync();
        Kosongkan();
    }

    private static TextBox Isian(string nama, int maks) => new() { Name = nama, MaxLength = maks };

    private static TextBlock Kecil(string teks) => new() { Text = teks, FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap };

    private static KelahiranBayiService Layanan() => new(DatabaseSettings.Load(),
        AppSession.CurrentUser ?? throw new InvalidOperationException("Sesi login berakhir."));

    public override Task ReloadAsync() => RunAsync(MuatDaftarAsync);

    private async Task MuatDaftarAsync()
    {
        var dari = _dari.SelectedDate ?? DateTime.Today.AddDays(-30);
        var sampai = _sampai.SelectedDate ?? DateTime.Today;
        var rows = await Layanan().DaftarAsync(_cari.Text ?? "", DateOnly.FromDateTime(dari), DateOnly.FromDateTime(sampai));
        Ui.FillRows(_daftar, rows.Select(r => new GridRow(
            [r.TanggalLahir?.ToString("dd/MM/yyyy") ?? "", r.NoRekamMedis, r.Nama, r.JenisKelamin, r.NamaIbu, r.BeratBadan, r.NoSkl, r.Penolong], r)));
    }

    private async Task PilihBarisAsync()
    {
        if (_sibuk || Ui.Selected<BarisKelahiran>(_daftar) is not { } baris) return;
        await JalankanAsync(async () => Tampilkan(await Layanan().MuatAsync(baris.NoRekamMedis)));
    }

    /// <summary>Satu perintah pada satu waktu, agar isian tidak berganti di tengah penyimpanan.</summary>
    private async Task JalankanAsync(Func<Task> aksi)
    {
        if (_sibuk) return;
        _sibuk = true;
        try { await RunAsync(aksi); }
        finally { _sibuk = false; }
    }

    private async Task BaruAsync()
    {
        if (_sibuk) return;
        _daftar.SelectedItem = null;
        Kosongkan();
        StatusText.Text = "";
        await PerbaruiNomorAsync();
    }

    private void Kosongkan()
    {
        _dilihat = null;
        _noRmDipilih = "";
        _nik = "";
        foreach (var kotak in new[]
                 {
                     _noRm, _nama, _namaIbu, _umurIbu, _namaAyah, _umurAyah, _alamat, _jam, _berat, _panjang, _kepala, _dada, _perut,
                     _anakKe, _g, _p, _a, _proses, _ketuban, _penyulit, _diagnosa, _keterangan, _resusitasi, _obat, _miksi, _mekonium,
                     _noSkl, _penolong,
                 })
            kotak.Text = "";
        _jk.SelectedIndex = 0;
        _tglLahir.SelectedDate = DateTime.Today;
        _tglDaftar.SelectedDate = DateTime.Today;
        foreach (var pilihan in _apgar) pilihan.SelectedIndex = -1;
        HitungApgar();
        AturMode();
    }

    private void Tampilkan(KelahiranBayi d)
    {
        _dilihat = d;
        _noRmDipilih = d.NoRekamMedis;
        _nik = d.Penolong;
        _noRm.Text = d.NoRekamMedis;
        foreach (var (kotak, nilai) in new[]
                 {
                     (_nama, d.Nama), (_namaIbu, d.NamaIbu), (_umurIbu, d.UmurIbu), (_namaAyah, d.NamaAyah), (_umurAyah, d.UmurAyah),
                     (_alamat, d.Alamat), (_jam, d.JamLahir.ToString("HH:mm:ss", CultureInfo.InvariantCulture)), (_berat, d.BeratBadan),
                     (_panjang, d.PanjangBadan), (_kepala, d.LingkarKepala), (_dada, d.LingkarDada), (_perut, d.LingkarPerut),
                     (_anakKe, d.AnakKe), (_g, d.Gravida), (_p, d.Para), (_a, d.Abortus), (_proses, d.ProsesLahir), (_ketuban, d.Ketuban),
                     (_penyulit, d.PenyulitKehamilan), (_diagnosa, d.Diagnosa), (_keterangan, d.Keterangan), (_resusitasi, d.Resusitasi),
                     (_obat, d.ObatDiberikan), (_miksi, d.Miksi), (_mekonium, d.Mekonium), (_noSkl, d.NoSkl),
                     (_penolong, d.Penolong.Length == 0 ? "" : $"{d.NamaPenolong} ({d.Penolong})"),
                 })
            kotak.Text = nilai;
        _jk.SelectedIndex = d.JenisKelamin == JenisKelamin.Perempuan ? 1 : 0;
        _tglLahir.SelectedDate = d.TanggalLahir == default ? null : d.TanggalLahir.ToDateTime(TimeOnly.MinValue);
        _tglDaftar.SelectedDate = d.TanggalDaftar == default ? null : d.TanggalDaftar.ToDateTime(TimeOnly.MinValue);
        foreach (var (apgar, m) in new[] { d.Menit1, d.Menit5, d.Menit10 }.Select((x, i) => (x, i)))
            for (var a = 0; a < 5; a++)
                _apgar[m, a].SelectedIndex = apgar.Nilai[a] ?? -1;
        HitungApgar();
        AturMode();
    }

    private void AturMode()
    {
        var ubah = _dilihat is not null;
        var terdaftar = !ubah && _noRmDipilih.Length > 0;
        _mode.Text = ubah ? "Mengubah data kelahiran"
            : terdaftar ? "Data baru untuk pasien terdaftar (identitas tidak diubah)" : "Data baru: bayi didaftarkan sebagai pasien baru";
        foreach (var kontrol in _identitas) kontrol.IsEnabled = !terdaftar;
        _pilihBayi.IsEnabled = !ubah;
        _hapus.IsEnabled = ubah;
        _cetak.IsEnabled = ubah;
        _noSkl.PlaceholderText = ubah ? "" : "Otomatis";
    }

    private async Task PerbaruiNomorAsync()
    {
        if (_dilihat is not null || _tglLahir.SelectedDate is not { } lahir || AppSession.CurrentUser is null) return;
        var urutan = ++_urutanNomor;
        try
        {
            var (noRm, noSkl) = await Layanan().PerkiraanNomorAsync(DateOnly.FromDateTime(lahir));
            if (urutan != _urutanNomor || _dilihat is not null) return;
            if (_noRmDipilih.Length == 0)
            {
                _noRm.PlaceholderText = $"Otomatis, perkiraan {noRm}";
                _noSkl.PlaceholderText = $"Otomatis, perkiraan {noSkl}";
            }
        }
        catch (Exception ex) { Ui.Err(StatusText, ex.Message); }
    }

    private void HitungApgar()
    {
        for (var m = 0; m < 3; m++)
            _totalApgar[m].Text = ApgarMenit(m).Total?.ToString() ?? "-";
    }

    private Apgar ApgarMenit(int m)
    {
        int? Nilai(int a) => _apgar[m, a]?.SelectedIndex is { } i && i >= 0 ? i : null;
        return new(Nilai(0), Nilai(1), Nilai(2), Nilai(3), Nilai(4));
    }

    private KelahiranBayi Isian()
    {
        if (_tglLahir.SelectedDate is not { } lahir || _tglDaftar.SelectedDate is not { } daftar)
            throw new ArgumentException("Isi tanggal lahir dan tanggal daftar.");
        if (!TimeOnly.TryParseExact((_jam.Text ?? "").Trim(), ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var jam))
            throw new ArgumentException("Isi jam lahir, contoh 14:35.");
        return new KelahiranBayi
        {
            NoRekamMedis = _noRmDipilih, Nama = _nama.Text ?? "",
            JenisKelamin = _jk.SelectedIndex == 1 ? JenisKelamin.Perempuan : JenisKelamin.LakiLaki,
            TanggalLahir = DateOnly.FromDateTime(lahir), JamLahir = jam, TanggalDaftar = DateOnly.FromDateTime(daftar),
            NamaIbu = _namaIbu.Text ?? "", UmurIbu = _umurIbu.Text ?? "", NamaAyah = _namaAyah.Text ?? "", UmurAyah = _umurAyah.Text ?? "",
            Alamat = _alamat.Text ?? "", BeratBadan = _berat.Text ?? "", PanjangBadan = _panjang.Text ?? "",
            LingkarKepala = _kepala.Text ?? "", LingkarDada = _dada.Text ?? "", LingkarPerut = _perut.Text ?? "",
            AnakKe = _anakKe.Text ?? "", Gravida = _g.Text ?? "", Para = _p.Text ?? "", Abortus = _a.Text ?? "",
            ProsesLahir = _proses.Text ?? "", Ketuban = _ketuban.Text ?? "", PenyulitKehamilan = _penyulit.Text ?? "",
            Diagnosa = _diagnosa.Text ?? "", Keterangan = _keterangan.Text ?? "", Resusitasi = _resusitasi.Text ?? "",
            ObatDiberikan = _obat.Text ?? "", Miksi = _miksi.Text ?? "", Mekonium = _mekonium.Text ?? "",
            Penolong = _nik, NoSkl = _noSkl.Text ?? "", Menit1 = ApgarMenit(0), Menit5 = ApgarMenit(1), Menit10 = ApgarMenit(2),
        };
    }

    private Task SimpanAsync() => JalankanAsync(async () =>
    {
        var layanan = Layanan();
        var isian = Isian();
        var noRm = _dilihat?.NoRekamMedis;
        string pesan;
        if (_dilihat is { } dilihat)
        {
            await layanan.UbahAsync(dilihat, isian);
            pesan = "Perubahan tersimpan.";
        }
        else
        {
            var hasil = await layanan.SimpanAsync(isian);
            noRm = hasil.NoRekamMedis;
            pesan = $"Tersimpan: No. RM {hasil.NoRekamMedis}, No. SKL {hasil.NoSkl}.";
        }
        Tampilkan(await layanan.MuatAsync(noRm!));
        await MuatDaftarAsync();
        Ui.Ok(StatusText, pesan);
    });

    private Task HapusAsync() => JalankanAsync(async () =>
    {
        if (_dilihat is not { } dilihat) return;
        if (!await Dialogs.ConfirmAsync(this, "Hapus Data Kelahiran",
                $"Hapus data kelahiran {dilihat.Nama} (No. RM {dilihat.NoRekamMedis})? Data pasiennya tetap ada.")) return;
        await Layanan().HapusAsync(dilihat);
        Kosongkan();
        await MuatDaftarAsync();
        Ui.Ok(StatusText, "Data kelahiran dihapus.");
    });

    /// <summary>Mencetak data yang tersimpan, bukan isian yang belum disimpan.</summary>
    private Task CetakAsync() => JalankanAsync(async () =>
    {
        if (_dilihat is not { } dilihat) return;
        var tersimpan = await Layanan().MuatAsync(dilihat.NoRekamMedis);
        var path = KelahiranBayiReport.GeneratePdf(tersimpan, await KopInstansi.MuatAsync(DatabaseSettings.Load()),
            DateOnly.FromDateTime(DateTime.Today));
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        Ui.Ok(StatusText, "Surat Keterangan Lahir dibuka dari data yang tersimpan.");
    });

    private async Task PilihBayiAsync()
    {
        if (_sibuk || TopLevel.GetTopLevel(this) is not Window owner) return;
        await JalankanAsync(async () =>
        {
            var layanan = Layanan();
            if (await SearchPickerDialog.ShowAsync<PasienKelahiran>(owner, "Pasien Bayi tanpa Data Kelahiran",
                    layanan.CariBayiAsync, p => p.ToString()) is not { } pasien) return;
            // Identitas pasien terdaftar tidak bisa diisi dari layar ini.
            if (pasien.TanggalLahir is null)
                throw new InvalidOperationException("Pasien ini belum punya tanggal lahir. Lengkapi di layar Pasien lebih dulu.");
            Kosongkan();
            _noRmDipilih = pasien.NoRekamMedis;
            _noRm.Text = pasien.NoRekamMedis;
            _nama.Text = pasien.Nama;
            _jk.SelectedIndex = pasien.JenisKelamin == JenisKelamin.Perempuan ? 1 : 0;
            _tglLahir.SelectedDate = pasien.TanggalLahir?.ToDateTime(TimeOnly.MinValue);
            _tglDaftar.SelectedDate = pasien.TanggalDaftar?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
            _namaIbu.Text = pasien.NamaIbu;
            _alamat.Text = pasien.Alamat;
            if (pasien.Keluarga == "AYAH") _namaAyah.Text = pasien.NamaKeluarga;
            AturMode();
        });
    }

    private async Task PilihIbuAsync()
    {
        if (_sibuk || TopLevel.GetTopLevel(this) is not Window owner) return;
        await JalankanAsync(async () =>
        {
            var layanan = Layanan();
            if (await SearchPickerDialog.ShowAsync<PasienKelahiran>(owner, "Pilih Ibu", layanan.CariIbuAsync, p => p.ToString())
                    is not { } ibu) return;
            if (_namaIbu.IsEnabled)
            {
                _namaIbu.Text = ibu.Nama;
                _alamat.Text = ibu.Alamat;
            }
            if (ibu.TanggalLahir is { } lahir && _tglLahir.SelectedDate is { } bayi)
                _umurIbu.Text = $"{PasienBaruService.HitungUmurLengkap(lahir, DateOnly.FromDateTime(bayi)).Tahun} Th";
            if (ibu.Keluarga == "SUAMI") _namaAyah.Text = ibu.NamaKeluarga;
        });
    }

    private async Task PilihPenolongAsync()
    {
        if (_sibuk || TopLevel.GetTopLevel(this) is not Window owner) return;
        await JalankanAsync(async () =>
        {
            var layanan = Layanan();
            if (await SearchPickerDialog.ShowAsync<PilihanPasien>(owner, "Pilih Penolong", layanan.CariPenolongAsync,
                    p => $"{p.Nama} ({p.Kode})") is not { } pegawai) return;
            _nik = pegawai.Kode;
            _penolong.Text = $"{pegawai.Nama} ({pegawai.Kode})";
        });
    }
}
