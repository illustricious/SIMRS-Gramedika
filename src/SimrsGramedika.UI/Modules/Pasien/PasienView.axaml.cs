using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Pasien;

public partial class PasienView : UserControl
{
    private static readonly string[] ComboReferensi = ["PenjabCombo", "SukuCombo", "BahasaCombo", "CacatCombo", "PerusahaanCombo"];

    private string? _editingNoRekamMedis;
    private int _versi;
    private bool _siap;
    private bool _menyimpan;

    public event EventHandler? BackRequested;
    public event EventHandler<PatientMenuRequest>? PatientMenuRequested;

    public PasienView()
    {
        InitializeComponent();
        PatientContextMenu.Attach<Core.Entities.Pasien>(this.FindControl<DataGrid>("PasienGrid")!,
            row => new("", row.NoRekamMedis, row.NamaPasien),
            request => PatientMenuRequested?.Invoke(this, request), PatientMenuKind.Patient);

        Combo("GolDarahCombo").ItemsSource = PilihanIdentitas.GolDarah;
        Combo("StatusNikahCombo").ItemsSource = PilihanIdentitas.StatusNikah;
        Combo("HubunganPjCombo").ItemsSource = PilihanIdentitas.Hubungan;
        Combo("PendidikanCombo").ItemsSource = PasienBaruService.Pendidikan;
        // Pilihan awal sudah terisi sebelum Baru diklik.
        Isi(PasienBaru());
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        await ClearFormAsync();
        await SearchAsync(string.Empty);
    }

    public async Task SearchForAsync(string noRekamMedis)
    {
        this.FindControl<TextBox>("KeyWordBox")!.Text = noRekamMedis;
        var grid = this.FindControl<DataGrid>("PasienGrid")!;
        try
        {
            using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
            var patient = await db.PasienSet.AsNoTracking().SingleOrDefaultAsync(p => p.NoRekamMedis == noRekamMedis);
            grid.ItemsSource = patient is null ? Array.Empty<Core.Entities.Pasien>() : new[] { patient };
            grid.SelectedItem = patient;
            if (patient is not null) BukaInput();
        }
        catch (Exception)
        {
            grid.ItemsSource = Array.Empty<Core.Entities.Pasien>();
            var status = this.FindControl<TextBlock>("SaveStatusText")!;
            Ui.Err(status, "Identitas pasien tidak dapat dimuat.");
            status.IsVisible = true;
        }
    }

    private async System.Threading.Tasks.Task SearchAsync(string keyword)
    {
        var grid = this.FindControl<DataGrid>("PasienGrid")!;
        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsGramedikaDbContextFactory.Create(settings);
            var repo = new PasienRepository(db);
            grid.ItemsSource = await repo.SearchAsync(keyword);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<Core.Entities.Pasien>();
        }
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e)
        => await SearchAsync(this.FindControl<TextBox>("KeyWordBox")!.Text ?? string.Empty);

    private async void OnSearchKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.Enter) return;
        e.Handled = true;
        await SearchAsync(this.FindControl<TextBox>("KeyWordBox")!.Text ?? string.Empty);
    }

    private async void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (this.FindControl<DataGrid>("PasienGrid")!.SelectedItem is not Core.Entities.Pasien p) return;

        var versi = ++_versi;
        _editingNoRekamMedis = p.NoRekamMedis;
        _siap = false;
        Kotak("NoRmBox").Text = p.NoRekamMedis;
        this.FindControl<Button>("SavePatientButton")!.Content = "Simpan Perubahan";
        SembunyikanStatus();
        try
        {
            var service = Layanan();
            await MuatReferensiAsync(service);
            var data = await service.MuatAsync(p.NoRekamMedis);
            // Hasil yang terlambat tidak boleh menimpa pilihan yang lebih baru.
            if (versi != _versi) return;
            Isi(data);
            _siap = true;
        }
        catch (Exception ex)
        {
            if (versi != _versi) return;
            Isi(PasienBaru());
            Galat($"Data pasien tidak dapat dimuat: {ex.Message}");
        }
    }

    private async void OnBaruClick(object? sender, RoutedEventArgs e)
    {
        BukaInput();
        await ClearFormAsync();
    }

    private void BukaInput() => this.FindControl<ToggleButton>("InputDataToggle")!.IsChecked = true;

    private async System.Threading.Tasks.Task ClearFormAsync()
    {
        var versi = ++_versi;
        _editingNoRekamMedis = null;
        _siap = false;
        Isi(PasienBaru());
        Kotak("NoRmBox").Text = string.Empty;
        this.FindControl<Button>("SavePatientButton")!.Content = "Simpan Pasien";
        this.FindControl<DataGrid>("PasienGrid")!.SelectedItem = null;
        SembunyikanStatus();

        try
        {
            var referensi = await MuatReferensiAsync(Layanan());
            if (versi != _versi) return;
            Kotak("NoRmBox").Text = referensi.PerkiraanNoRm;
            // Pilih otomatis hanya baris "-"; referensi lain harus dipilih petugas.
            foreach (var nama in ComboReferensi)
            {
                var combo = Combo(nama);
                combo.SelectedItem = (combo.ItemsSource as IEnumerable<PilihanPasien>)?.FirstOrDefault(x => x.Nama.Trim() == "-");
            }
            _siap = true;
        }
        catch (Exception ex)
        {
            if (versi == _versi) Galat($"Data pendukung tidak dapat dimuat: {ex.Message}");
        }
    }

    /// <summary>Pilihan awal pasien baru, sama dengan Khanza.</summary>
    private static PasienBaruData PasienBaru() => new() { StatusPernikahan = StatusPernikahan.Menikah, Agama = "ISLAM" };

    private static PasienBaruService Layanan() =>
        new(DatabaseSettings.Load(), AppSession.CurrentUser ?? new AuthenticatedUser());

    private async System.Threading.Tasks.Task<ReferensiPasienBaru> MuatReferensiAsync(PasienBaruService service)
    {
        var referensi = await service.ReferensiAsync();
        Combo("PenjabCombo").ItemsSource = referensi.Penjab;
        Combo("SukuCombo").ItemsSource = referensi.SukuBangsa;
        Combo("BahasaCombo").ItemsSource = referensi.Bahasa;
        Combo("CacatCombo").ItemsSource = referensi.CacatFisik;
        Combo("PerusahaanCombo").ItemsSource = referensi.Perusahaan;
        return referensi;
    }

    private void Isi(PasienBaruData d)
    {
        foreach (var (nama, nilai) in new[]
                 {
                     ("NamaBox", d.Nama), ("AlamatBox", d.Alamat), ("TelpBox", d.NoTelepon), ("KtpBox", d.NoKtp),
                     ("PekerjaanBox", d.Pekerjaan), ("EmailBox", d.Email), ("NamaIbuBox", d.NamaIbu),
                     ("TempatLahirBox", d.TempatLahir), ("PesertaBox", d.NoPeserta),
                     ("KelurahanBox", d.Wilayah.Kelurahan), ("KecamatanBox", d.Wilayah.Kecamatan),
                     ("KabupatenBox", d.Wilayah.Kabupaten), ("PropinsiBox", d.Wilayah.Propinsi),
                     ("NamaPjBox", d.NamaKeluarga), ("PekerjaanPjBox", d.PekerjaanPj), ("AlamatPjBox", d.AlamatPj),
                     ("KelurahanPjBox", d.WilayahPj.Kelurahan), ("KecamatanPjBox", d.WilayahPj.Kecamatan),
                     ("KabupatenPjBox", d.WilayahPj.Kabupaten), ("PropinsiPjBox", d.WilayahPj.Propinsi),
                 })
            Kotak(nama).Text = nilai;
        this.FindControl<CalendarDatePicker>("TglLahirPicker")!.SelectedDate = d.TanggalLahir is { } lahir
            ? new DateTime(lahir.Year, lahir.Month, lahir.Day)
            : null;
        this.FindControl<RadioButton>("JkLakiRadio")!.IsChecked = d.JenisKelamin == JenisKelamin.LakiLaki;
        this.FindControl<RadioButton>("JkPerempuanRadio")!.IsChecked = d.JenisKelamin == JenisKelamin.Perempuan;
        Combo("GolDarahCombo").SelectedItem = PilihanIdentitas.GolDarah.FirstOrDefault(o => o.Nilai == d.GolonganDarah);
        Combo("StatusNikahCombo").SelectedItem = PilihanIdentitas.StatusNikah.FirstOrDefault(o => o.Nilai == d.StatusPernikahan);
        Combo("HubunganPjCombo").SelectedItem = PilihanIdentitas.Hubungan.FirstOrDefault(o => o.Nilai == d.Keluarga);
        // Agama lama di luar daftar tetap tampil, agar tidak berubah saat disimpan.
        var agama = PilihanIdentitas.Agama.FirstOrDefault(a => a.Equals(d.Agama, StringComparison.OrdinalIgnoreCase)) ?? d.Agama;
        Combo("AgamaCombo").ItemsSource = PilihanIdentitas.Agama.Contains(agama) ? PilihanIdentitas.Agama : [agama, .. PilihanIdentitas.Agama];
        Combo("AgamaCombo").SelectedItem = agama;
        Combo("PendidikanCombo").SelectedItem = d.Pendidikan;
        foreach (var (nama, kode) in ComboReferensi.Zip(new[] { d.KodePenjab, d.SukuBangsa, d.Bahasa, d.CacatFisik, d.Perusahaan }))
        {
            var combo = Combo(nama);
            combo.SelectedItem = (combo.ItemsSource as IEnumerable<PilihanPasien>)?.FirstOrDefault(x => x.Kode == kode);
        }
    }

    private PasienBaruData Baca()
    {
        string T(string nama) => Kotak(nama).Text?.Trim() ?? "";
        string K(string nama) => (Combo(nama).SelectedItem as PilihanPasien)?.Kode ?? "";
        return new PasienBaruData
        {
            Nama = T("NamaBox"), NoKtp = T("KtpBox"),
            JenisKelamin = this.FindControl<RadioButton>("JkPerempuanRadio")!.IsChecked == true
                ? JenisKelamin.Perempuan : JenisKelamin.LakiLaki,
            TempatLahir = T("TempatLahirBox"),
            TanggalLahir = this.FindControl<CalendarDatePicker>("TglLahirPicker")!.SelectedDate is { } lahir
                ? DateOnly.FromDateTime(lahir) : null,
            NamaIbu = T("NamaIbuBox"), Alamat = T("AlamatBox"),
            Wilayah = new(T("KelurahanBox"), T("KecamatanBox"), T("KabupatenBox"), T("PropinsiBox")),
            GolonganDarah = (Combo("GolDarahCombo").SelectedItem as Opsi<GolonganDarah>)?.Nilai ?? GolonganDarah.TidakDiketahui,
            StatusPernikahan = (Combo("StatusNikahCombo").SelectedItem as Opsi<StatusPernikahan>)?.Nilai ?? StatusPernikahan.Menikah,
            Agama = Combo("AgamaCombo").SelectedItem as string is { Length: > 0 } agama ? agama : "-",
            Pendidikan = Combo("PendidikanCombo").SelectedItem as string ?? "-",
            Pekerjaan = T("PekerjaanBox"), NoTelepon = T("TelpBox"), Email = T("EmailBox"),
            KodePenjab = K("PenjabCombo"), NoPeserta = T("PesertaBox"),
            Keluarga = (Combo("HubunganPjCombo").SelectedItem as Opsi<HubunganPenanggungJawab>)?.Nilai ?? HubunganPenanggungJawab.DiriSendiri,
            NamaKeluarga = T("NamaPjBox"), PekerjaanPj = T("PekerjaanPjBox"), AlamatPj = T("AlamatPjBox"),
            WilayahPj = new(T("KelurahanPjBox"), T("KecamatanPjBox"), T("KabupatenPjBox"), T("PropinsiPjBox")),
            SukuBangsa = K("SukuCombo"), Bahasa = K("BahasaCombo"), CacatFisik = K("CacatCombo"),
            Perusahaan = K("PerusahaanCombo"),
        };
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        if (_menyimpan) return;
        // Isian yang gagal dimuat tidak boleh menimpa data pasien.
        if (!_siap)
        {
            Galat("Isian belum selesai dimuat. Klik Baru atau pilih pasien lagi.");
            return;
        }

        _menyimpan = true;
        try
        {
            var data = Baca();
            var noRm = _editingNoRekamMedis;
            var ubah = noRm is not null;
            if (ubah) await Layanan().UbahAsync(noRm!, data);
            else noRm = await Layanan().SimpanAsync(data);

            await ClearFormAsync();
            await SearchAsync(string.Empty);
            var status = this.FindControl<TextBlock>("SaveStatusText")!;
            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = ubah
                ? $"Perubahan pasien tersimpan — No. RM {noRm}, {data.Nama}."
                : $"Tersimpan — No. RM {noRm}, {data.Nama}.";
            status.IsVisible = true;
        }
        catch (Exception ex)
        {
            Galat($"Gagal simpan: {(ex.InnerException ?? ex).Message}");
        }
        finally
        {
            _menyimpan = false;
        }
    }

    private TextBox Kotak(string nama) => this.FindControl<TextBox>(nama)!;

    private ComboBox Combo(string nama) => this.FindControl<ComboBox>(nama)!;

    private void SembunyikanStatus() => this.FindControl<TextBlock>("SaveStatusText")!.IsVisible = false;

    private void Galat(string pesan)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        Ui.Err(status, pesan);
        status.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
