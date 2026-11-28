using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.RekamMedis;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Controls;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RekamMedis;

/// <summary>Layar isian satu formulir rekam medis untuk satu kunjungan, dibangun dari <see cref="FormKlinis"/>.</summary>
public sealed class FormKlinisView : PageView
{
    private readonly FormKlinis _form;
    private readonly string _noRawat;
    private readonly AuthenticatedUser _user;
    private readonly FormKlinisService _service;
    private readonly DatabaseSettings _settings;
    private readonly Dictionary<string, Control> _editors = new();
    private readonly CalendarDatePicker _tanggal = new() { SelectedDateFormat = CalendarDatePickerFormat.Custom, CustomDateFormatString = "dd/MM/yyyy", Width = 140 };
    private readonly TextBox _jam = new() { PlaceholderText = "HH:mm:ss", MaxLength = 8, Width = 95 };
    private readonly TextBlock _pemilikNama = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly TextBlock _pasien = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _keterangan = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DataGrid _grid;
    private readonly IsianKlinis[] _kolomDaftar;
    private readonly WrapPanel _commands;
    private (string Kode, string Nama)? _akun;
    private KunjunganKlinis? _kunjungan;
    private Dictionary<string, string> _usul = new();
    private string _pemilik = "";
    private bool _mengisi, _sibuk, _siap;

    public FormKlinisView(FormKlinis form, string noRawat, AuthenticatedUser user, DatabaseSettings? settings = null)
    {
        _form = form;
        _noRawat = noRawat;
        _user = user;
        _settings = settings ?? DatabaseSettings.Load();
        _service = new(_settings, user, form, noRawat);

        _kolomDaftar = form.DaftarLengkap ? form.Isian : form.Isian
            .Where(i => i.Kolom.Contains("total") || i.Kolom is "hasil_skrining" or "keputusan" or "indikasi_isolasi")
            .Concat(form.Isian.Where(i => !i.Otomatis)).Distinct().Take(6).ToArray();
        var sebutan = char.ToUpperInvariant(form.SebutanPemilik[0]) + form.SebutanPemilik[1..];
        _grid = Ui.Grid([new("waktu", "Waktu", 145), new("pemilik", sebutan, 160),
            .. _kolomDaftar.Select(i => new Col(i.Kolom, i.Label, 150))]);
        _grid.SelectionChanged += (_, _) => { if (Ui.Selected<CatatanKlinis>(_grid) is { } c) Tampilkan(c); };

        var isi = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 24, 0) };
        isi.Children.Add(_pasien);
        AutomationProperties.SetName(_tanggal, "Tanggal catatan");
        AutomationProperties.SetName(_jam, "Jam catatan");
        var sekarang = Ui.Btn("Sekarang", (_, _) => AturWaktu(DateTime.Now));
        if (_service.TanggalTerkunci) _tanggal.IsEnabled = _jam.IsEnabled = sekarang.IsEnabled = false;
        var kepala = Ui.Bar(Ui.L("Tanggal"), _tanggal, Ui.L("Jam"), _jam, sekarang, Ui.L($"{sebutan} :"), _pemilikNama);
        if (form.TanggalSaja)
            foreach (var jam in kepala.Children.SkipWhile(c => c != _tanggal).Skip(1).Take(2).ToArray()) jam.IsVisible = false;
        if (user.IsAdminUtama)
            kepala.Children.Add(Ui.Btn($"Pilih {sebutan}", async (_, _) => await JalankanAsync(PilihPemilikAsync)));
        isi.Children.Add(kepala);
        SusunIsian(isi);

        var (panel, toggle) = Ui.InputPanel(new ScrollViewer { Content = isi, MaxHeight = 390 });
        toggle.IsChecked = true;
        var body = new Avalonia.Controls.Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 6 };
        // Penjelasan hasil hitungan diletakkan di luar bagian yang digulung agar selalu terlihat.
        _keterangan.IsVisible = form.Keterangan is not null;
        Avalonia.Controls.Grid.SetRow(_keterangan, 1);
        Avalonia.Controls.Grid.SetRow(_grid, 2);
        body.Children.Add(panel);
        body.Children.Add(_keterangan);
        body.Children.Add(_grid);

        _commands = Ui.Bar(
            Ui.Btn("Kosongkan", (_, _) => Kosongkan()),
            Ui.Btn("Simpan Baru", async (_, _) => await JalankanAsync(SimpanAsync)),
            Ui.Btn("Ubah", async (_, _) => await JalankanAsync(UbahAsync)),
            Ui.Btn("Hapus", async (_, _) => await JalankanAsync(HapusAsync)),
            Ui.Btn("Cetak", async (_, _) => await JalankanAsync(CetakAsync)));
        _commands.Margin = default;
        _commands.IsEnabled = false;
        Build(form.Judul, body, null, _commands);
        Kosongkan();
    }

    /// <summary>Data kunjungan sudah dimuat; sebelum itu perintah dinonaktifkan.</summary>
    public bool Siap => _siap;

    public override async Task ReloadAsync() => await JalankanAsync(async () =>
    {
        _akun = await _service.PemilikAkunAsync();
        await MuatAsync();
        _siap = true;
    });

    private void SusunIsian(StackPanel isi)
    {
        var pendek = new List<(string, Control)>();
        void Tuang()
        {
            if (pendek.Count == 0) return;
            isi.Children.Add(Ui.FormColumns(pendek.ToList(), Math.Min(3, pendek.Count)));
            pendek.Clear();
        }

        var perBaris = _form.Isian.Any(f => f.Pilihan is not null && !f.Kolom.StartsWith("penilaian_", StringComparison.Ordinal)
            && f.Label.Length > PanjangLabel);
        for (var i = 0; i < _form.Isian.Length; i++)
        {
            var f = _form.Isian[i];
            // Pilihan yang diikuti nilai hitungannya ditampilkan sebaris, seperti kolom Skor di Khanza.
            if (f.Pilihan is not null && i + 1 < _form.Isian.Length && _form.Isian[i + 1].Otomatis)
            {
                Tuang();
                if (f.Bagian is { } judul) isi.Children.Add(Ui.Section(judul));
                var nilai = _form.Isian[++i];
                isi.Children.Add(Ui.FormColumns([(f.Label + " :", Buat(f)), (nilai.Label + " :", Buat(nilai))], 2));
                continue;
            }
            if (f.Bagian is { } bagian)
            {
                Tuang();
                isi.Children.Add(Ui.Section(bagian));
            }
            var kontrol = Buat(f);
            // Formulir berisi kalimat panjang (check list kriteria) ditulis satu pilihan per baris agar tidak bertumpuk.
            if (f.Pilihan is not null && perBaris)
            {
                Tuang();
                isi.Children.Add(Baris(f.Label, kontrol));
                continue;
            }
            if (!Panjang(f))
            {
                pendek.Add((f.Label + " :", kontrol));
                continue;
            }
            Tuang();
            isi.Children.Add(new TextBlock { Text = f.Label });
            isi.Children.Add(kontrol);
        }
        Tuang();
    }

    private const int PanjangLabel = 45;

    private static Avalonia.Controls.Grid Baris(string label, Control kontrol)
    {
        var baris = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("*,260"), ColumnSpacing = 12 };
        baris.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        Avalonia.Controls.Grid.SetColumn(kontrol, 1);
        baris.Children.Add(kontrol);
        return baris;
    }

    private static bool Panjang(IsianKlinis f) => f.Pilihan is null && f.Maks >= 200;

    private Control Buat(IsianKlinis f)
    {
        Control kontrol;
        if (f.Pilihan is not null)
        {
            var pilihan = new ComboBox { MinWidth = 160, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            pilihan.SelectionChanged += (_, _) => Hitung();
            kontrol = pilihan;
        }
        else
        {
            var teks = new TextBox
            {
                MaxLength = f.Otomatis ? 0 : f.Maks, IsReadOnly = f.Otomatis, MinWidth = 120,
                AcceptsReturn = Panjang(f) && !f.Otomatis, TextWrapping = Panjang(f) ? TextWrapping.Wrap : TextWrapping.NoWrap,
                MinHeight = Panjang(f) ? 52 : 0,
            };
            if ((_form.Hitung is not null || _form.Usul is not null) && !f.Otomatis)
                teks.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Hitung(); };
            kontrol = teks;
        }
        AutomationProperties.SetName(kontrol, f.Label);
        _editors[f.Kolom] = kontrol;
        return kontrol;
    }

    private Dictionary<string, string> BacaIsian() => _form.Isian.ToDictionary(f => f.Kolom, f => _editors[f.Kolom] switch
    {
        ComboBox c => c.SelectedItem as string ?? "",
        TextBox t => t.Text ?? "",
        _ => "",
    });

    private void Atur(IsianKlinis f, string nilai)
    {
        if (_editors[f.Kolom] is ComboBox c)
        {
            // Nilai tersimpan di luar daftar tetap ditampilkan agar tidak berubah diam-diam.
            var daftar = f.Pilihan!.ToList();
            if (nilai.Length > 0 && !daftar.Contains(nilai)) daftar.Add(nilai);
            c.ItemsSource = daftar;
            c.SelectedItem = nilai.Length == 0 ? null : nilai;
        }
        else ((TextBox)_editors[f.Kolom]).Text = nilai;
    }

    private void Hitung()
    {
        if (_mengisi || _editors.Count != _form.Isian.Length) return;
        var nilai = NilaiHitung();
        // Dua putaran: saran pilihan bisa mengubah skor, yang lalu mengubah saran kesimpulan.
        for (var putaran = 0; putaran < 2 && TerapkanUsul(nilai); putaran++) nilai = NilaiHitung();
        foreach (var f in _form.Isian.Where(f => f.Otomatis)) ((TextBox)_editors[f.Kolom]).Text = nilai.GetValueOrDefault(f.Kolom, "");
        _keterangan.Text = _form.Keterangan?.Invoke(nilai) ?? "";
    }

    private Dictionary<string, string> NilaiHitung()
    {
        var nilai = BacaIsian();
        _kunjungan?.IsiKonteks(nilai);
        _form.Hitung?.Invoke(nilai);
        return nilai;
    }

    /// <summary>Menulis saran yang berubah sejak terakhir diterapkan, sehingga ketikan pengguna tidak tertimpa.</summary>
    private bool TerapkanUsul(IReadOnlyDictionary<string, string> nilai)
    {
        if (_form.Usul?.Invoke(nilai) is not { } usul) return false;
        var berubah = usul.Where(u => !_usul.TryGetValue(u.Key, out var lama) || lama != u.Value).ToList();
        _usul = usul;
        _mengisi = true;
        foreach (var (kolom, saran) in berubah) Atur(_form.Isian.First(f => f.Kolom == kolom), saran);
        _mengisi = false;
        return berubah.Count > 0;
    }

    private void AturWaktu(DateTime waktu)
    {
        _tanggal.SelectedDate = waktu.Date;
        _jam.Text = waktu.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private DateTime BacaWaktu()
    {
        if (_tanggal.SelectedDate is not { } tanggal) throw new ArgumentException("Isi tanggal catatan.");
        if (!TimeSpan.TryParseExact((_jam.Text ?? "").Trim(), [@"hh\:mm\:ss", @"hh\:mm"], CultureInfo.InvariantCulture, out var jam))
            throw new ArgumentException("Isi jam dalam format HH:mm atau HH:mm:ss.");
        return tanggal.Date + jam;
    }

    private void AturPemilik(string kode, string nama)
    {
        _pemilik = kode;
        _pemilikNama.Text = kode.Length == 0 ? "(belum dipilih)" : $"{kode} · {nama}";
    }

    private void Kosongkan()
    {
        _grid.SelectedItem = null;
        _mengisi = true;
        foreach (var f in _form.Isian) Atur(f, f.Awal ?? f.Pilihan?[0] ?? "");
        _mengisi = false;
        _usul = new();
        AturWaktu(DateTime.Now);
        AturPemilik(_akun?.Kode ?? "", _akun?.Nama ?? "");
        Hitung();
    }

    private void Tampilkan(CatatanKlinis catatan)
    {
        _mengisi = true;
        foreach (var f in _form.Isian) Atur(f, catatan.Isi.GetValueOrDefault(f.Kolom, ""));
        _mengisi = false;
        // Catatan tersimpan ditampilkan apa adanya; saran baru berlaku sesudah isian sumbernya diubah.
        _usul = _form.Usul?.Invoke(NilaiHitung()) ?? new();
        AturWaktu(catatan.Waktu);
        AturPemilik(catatan.Pemilik, catatan.NamaPemilik);
        _keterangan.Text = _form.Keterangan?.Invoke(catatan.Isi) ?? "";
    }

    private async Task MuatAsync()
    {
        var (kunjungan, catatan) = await _service.MuatAsync();
        _kunjungan = kunjungan;
        _pasien.Text = $"{kunjungan.Pasien} · RM {kunjungan.NoRekamMedis} · No. Rawat {_noRawat}";
        Ui.FillRows(_grid, catatan.Select(c => new GridRow(
            [c.Waktu.ToString(_form.TanggalSaja ? "dd-MM-yyyy" : "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture), c.NamaPemilik,
             .. _kolomDaftar.Select(f => c.Isi.GetValueOrDefault(f.Kolom, ""))], c)));
        Kosongkan();
        if (_form.SatuPerKunjungan && catatan.Count == 1)
            _grid.SelectedItem = _grid.ItemsSource!.Cast<GridRow>().First();
    }

    private async Task PilihPemilikAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var row = await SearchPickerDialog.ShowAsync<Row>(owner, $"Pilih {_form.SebutanPemilik}",
            _service.CariPemilikAsync, r => $"{r.S("kode")} · {r.S("nama")}");
        if (row is not null) AturPemilik(row.S("kode"), row.S("nama"));
    }

    private CatatanKlinis Terpilih() => Ui.Selected<CatatanKlinis>(_grid)
        ?? throw new InvalidOperationException("Pilih catatan pada tabel terlebih dahulu.");

    private async Task SimpanAsync()
    {
        await _service.SimpanAsync(BacaWaktu(), _pemilik, BacaIsian());
        await SelesaiAsync("Catatan tersimpan.");
    }

    private async Task UbahAsync()
    {
        var lama = Terpilih();
        var waktu = BacaWaktu();
        var isi = BacaIsian();
        var pemilik = _pemilik;
        if (!await Dialogs.ConfirmAsync(this, "Ubah catatan",
                $"Ganti catatan {lama.Waktu:dd-MM-yyyy HH:mm:ss} oleh {lama.NamaPemilik}?")) return;
        await _service.UbahAsync(lama, waktu, pemilik, isi);
        await SelesaiAsync("Perubahan tersimpan.");
    }

    private async Task HapusAsync()
    {
        var lama = Terpilih();
        if (!await Dialogs.ConfirmAsync(this, "Hapus catatan",
                $"Hapus catatan {lama.Waktu:dd-MM-yyyy HH:mm:ss} oleh {lama.NamaPemilik}?")) return;
        await _service.HapusAsync(lama);
        await SelesaiAsync("Catatan dihapus.");
    }

    private async Task SelesaiAsync(string pesan)
    {
        try
        {
            await MuatAsync();
            Ui.Ok(StatusText, pesan);
        }
        catch (Exception) { Ui.Ok(StatusText, pesan + " Daftar belum bisa dimuat ulang."); }
    }

    private async Task CetakAsync()
    {
        var cetak = await _service.CetakAsync(Terpilih());
        var path = FormKlinisReport.GeneratePdf(_form, cetak, await KopInstansi.MuatAsync(_settings));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        Ui.Ok(StatusText, "Cetakan dibuka.");
    }

    /// <summary>Satu perintah dalam satu waktu; pesan aturan ditampilkan apa adanya.</summary>
    private async Task JalankanAsync(Func<Task> action)
    {
        if (_sibuk) return;
        _sibuk = true;
        _commands.IsEnabled = false;
        try
        {
            Ui.Info(StatusText, "Memproses...");
            await action();
            if (StatusText.Text == "Memproses...") StatusText.Text = "";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            Ui.Err(StatusText, ex.Message);
        }
        catch (Exception ex) { Ui.Err(StatusText, $"Data gagal diproses: {ex.Message}"); }
        finally
        {
            _sibuk = false;
            _commands.IsEnabled = _siap;
        }
    }
}
