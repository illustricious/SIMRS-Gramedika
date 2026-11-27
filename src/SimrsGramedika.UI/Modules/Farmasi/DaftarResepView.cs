using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Farmasi;

/// <summary>
/// Legacy DlgDaftarPermintaanResep: daftar resep dokter. Farmasi memeriksa obat dan racikan, menambah atau
/// mengganti obat, memvalidasi (stok dan jurnal) atau membatalkan validasi, lalu menandai penyerahan.
/// </summary>
public class DaftarResepView : PageView
{
    private const string SemuaStatus = "Semua status";

    /// <summary>
    /// Satu obat yang akan divalidasi; Jumlah, Embalase dan Tuslah dapat diubah, Aturan hanya untuk obat tambahan.
    /// </summary>
    public sealed class BarisResep : INotifyPropertyChanged
    {
        private string _jumlah, _embalase, _tuslah, _aturan;

        public BarisResep(BarisValidasi asal, double jumlah, double embalase, double tuslah, string kelompok = "")
        {
            Asal = asal;
            Kelompok = kelompok;
            _jumlah = Angka(jumlah);
            _embalase = Angka(embalase);
            _tuslah = Angka(tuslah);
            _aturan = asal.Aturan;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public BarisValidasi Asal { get; }
        public string Kelompok { get; }
        public string Obat => Asal.Nama + (Asal.Tambahan ? " (tambahan)" : !Asal.Aktif ? " (tidak aktif)" : "");
        public string Satuan => Asal.Satuan;
        public string Resep => Asal.Tambahan ? "-" : Angka(Asal.JumlahResep);
        public string Stok => Angka(Asal.Stok);
        public string Batch => Asal.NoBatch;
        public string Harga => Ui.Money(Asal.Harga);

        public string Jumlah { get => _jumlah; set => Ubah(ref _jumlah, value); }
        public string Embalase { get => _embalase; set => Ubah(ref _embalase, value); }
        public string Tuslah { get => _tuslah; set => Ubah(ref _tuslah, value); }

        public string Aturan
        {
            get => _aturan;
            // Aturan pakai obat resep tetap seperti ditulis dokter.
            set => Ubah(ref _aturan, Asal.Tambahan && Asal.Racik.Length == 0 ? value : _aturan);
        }

        public string Subtotal
        {
            get
            {
                try { return Ui.Money(Nilai()); }
                catch (FormatException) { return "?"; }
            }
        }

        public double Nilai()
        {
            var jumlah = Ui.ParseNumber(Jumlah);
            return jumlah > 0
                ? ValidasiResepService.Total(Asal.Harga, jumlah, Ui.ParseNumber(Embalase), Ui.ParseNumber(Tuslah))
                : 0;
        }

        public InputValidasi KeInput() => new(Asal.Kode, Ui.ParseNumber(Jumlah), Ui.ParseNumber(Embalase),
            Ui.ParseNumber(Tuslah), Asal.Harga, Asal.NoBatch, Asal.NoFaktur, Asal.Racik, Aturan);

        private static string Angka(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private void Ubah(ref string field, string? value)
        {
            field = value ?? "";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    private readonly ValidasiResepService _service = new(DatabaseSettings.Load(), AppSession.CurrentUser ?? new AuthenticatedUser());
    private readonly DataGrid _daftar = Ui.Grid(new Col("no_resep", "No. Resep"), new Col("status", "Status"), new Col("diresepkan", "Diresepkan"),
        new Col("no_rawat", "No. Rawat"), new Col("no_rm", "No. RM"), new Col("pasien", "Pasien"), new Col("dokter", "Dokter"),
        new Col("poli", "Poli"), new Col("bayar", "Cara Bayar"), new Col("rawat", "Rawat"), new Col("divalidasi", "Divalidasi"),
        new Col("diserahkan", "Diserahkan"));
    private readonly DataGrid _obat = new()
    {
        Name = "ResepObatGrid", AutoGenerateColumns = false, IsReadOnly = false, CanUserSortColumns = false,
        CanUserResizeColumns = true, GridLinesVisibility = DataGridGridLinesVisibility.All,
        HeadersVisibility = DataGridHeadersVisibility.Column,
    };
    private readonly DataGrid _diberikan = Ui.Grid(new Col("nama", "Obat Diberikan", 260), new Col("jumlah", "Jumlah"),
        new Col("satuan", "Satuan"), new Col("aturan", "Aturan Pakai", 150), new Col("batch", "No. Batch"),
        new Col("harga", "Harga"), new Col("embalase", "Embalase"), new Col("tuslah", "Tuslah"), new Col("total", "Total"));
    private readonly ObservableCollection<BarisResep> _baris = [];
    private readonly TextBox _cari = Ui.Text("Cari no resep / pasien / dokter / poli...");
    private readonly PeriodPicker _periode = new(false, DateTime.Today);
    private readonly ComboBox _status = Ui.Choice(
        [SemuaStatus, ResepFarmasi.Menunggu, ResepFarmasi.Tervalidasi, ResepFarmasi.Selesai], ResepFarmasi.Menunggu);
    private readonly TextBlock _info = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _total = new() { FontWeight = Avalonia.Media.FontWeight.Bold };
    private readonly Button _tambah, _ganti, _hapus, _validasi, _batal, _serahkan;
    private RincianResep? _rincian;
    private int _muatan;
    private bool _sibuk;

    public DaftarResepView()
    {
        Kolom("Racikan", nameof(BarisResep.Kelompok), 150);
        Kolom("Obat", nameof(BarisResep.Obat), 240);
        Kolom("Resep", nameof(BarisResep.Resep), 70);
        Kolom("Jumlah ✎", nameof(BarisResep.Jumlah), 90, true);
        Kolom("Satuan", nameof(BarisResep.Satuan), 80);
        Kolom("Aturan Pakai", nameof(BarisResep.Aturan), 150, true);
        Kolom("Stok", nameof(BarisResep.Stok), 70);
        Kolom("No. Batch", nameof(BarisResep.Batch), 100);
        Kolom("Harga", nameof(BarisResep.Harga), 90);
        Kolom("Embalase ✎", nameof(BarisResep.Embalase), 105, true);
        Kolom("Tuslah ✎", nameof(BarisResep.Tuslah), 95, true);
        Kolom("Subtotal", nameof(BarisResep.Subtotal), 100);
        _obat.ItemsSource = _baris;
        _obat.BeginningEdit += (_, e) =>
        {
            if (e.Column.Header as string == "Aturan Pakai" && e.Row.DataContext is BarisResep { Asal: var a } &&
                !(a.Tambahan && a.Racik.Length == 0)) e.Cancel = true;
        };
        _obat.SelectionChanged += (_, _) => AturTombolObat();

        _tambah = Ui.Btn("Tambah Obat", async (_, _) => await PilihObatAsync(null), false);
        _ganti = Ui.Btn("Ganti Obat", async (_, _) => await PilihObatAsync(_obat.SelectedItem as BarisResep), false);
        _hapus = Ui.Btn("Hapus Tambahan", (_, _) => HapusTambahan(), false);
        _validasi = Ui.Btn("Validasi Resep", async (_, _) => await ValidasiAsync(), false);
        _batal = Ui.Btn("Batal Validasi", async (_, _) => await BatalAsync(), false);
        _serahkan = Ui.Btn("Serahkan Obat", async (_, _) => await SerahkanAsync(), false);
        _daftar.Name = "ResepGrid";
        _diberikan.Name = "DiberikanGrid";
        _daftar.SelectionChanged += async (_, _) => await RincianAsync();
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await ReloadAsync(); };

        var atas = Ui.Bar(_cari, _periode, _status, Ui.Btn("Tampilkan", async (_, _) => await ReloadAsync()));
        var tabelObat = new Panel { Children = { _obat, _diberikan } };
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,2*,Auto,3*,Auto"), RowSpacing = 6 };
        Control[] isi = [atas, _daftar, _info, tabelObat, Ui.Bar(_tambah, _ganti, _hapus, _validasi, _batal, _serahkan, _total)];
        for (var i = 0; i < isi.Length; i++)
        {
            Grid.SetRow(isi[i], i);
            body.Children.Add(isi[i]);
        }
        Build("Daftar Resep Dokter", body,
            "Validasi memotong stok depo, mencatat pemberian obat dan jurnal. Isi jumlah 0 untuk obat yang tidak diberikan.");
        Kosongkan();
    }

    private void Kolom(string judul, string properti, double lebar, bool dapatDiubah = false) =>
        _obat.Columns.Add(new DataGridTextColumn
        {
            Header = judul,
            Binding = new Binding(properti) { Mode = dapatDiubah ? BindingMode.TwoWay : BindingMode.OneWay },
            IsReadOnly = !dapatDiubah,
            Width = new DataGridLength(lebar),
        });

    public override async Task ReloadAsync() => await MuatDaftarAsync(null);

    private async Task MuatDaftarAsync(string? pilih) => await RunAsync(async () =>
    {
        var status = _status.SelectedItem as string;
        var daftar = await _service.DaftarAsync(_periode.From, _periode.To, _cari.Text ?? "",
            status == SemuaStatus ? null : status);
        Kosongkan();
        var baris = daftar.Select(r => new GridRow(
        [
            r.NoResep, r.Status, Waktu(r.Diresepkan), r.NoRawat, r.NoRm, r.Pasien, r.Dokter, r.Poli, r.CaraBayar,
            r.Ranap ? "Inap" : "Jalan", Waktu(r.Divalidasi), Waktu(r.Diserahkan),
        ], r)).ToList();
        Ui.FillRows(_daftar, baris);
        if (pilih is not null)
            _daftar.SelectedItem = baris.FirstOrDefault(b => ((ResepFarmasi)b.Tag!).NoResep == pilih);
        Ui.Info(StatusText, $"{daftar.Count} resep");
    });

    private static string Waktu(DateTime? t) => t?.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture) ?? "";

    private void Kosongkan()
    {
        _muatan++;
        _rincian = null;
        _baris.Clear();
        _diberikan.ItemsSource = null;
        _diberikan.IsVisible = false;
        _obat.IsVisible = true;
        _info.Text = "Pilih resep untuk melihat obatnya.";
        _total.Text = "";
        foreach (var b in new[] { _tambah, _ganti, _hapus, _validasi, _batal, _serahkan }) b.IsEnabled = false;
    }

    private async Task RincianAsync()
    {
        Kosongkan();
        if (Ui.Selected<ResepFarmasi>(_daftar) is not { } resep) return;
        var muatan = _muatan;
        await RunAsync(async () =>
        {
            var rincian = await _service.RincianAsync(resep.NoResep);
            // Pilihan sudah berpindah selagi menunggu database.
            if (muatan != _muatan) return;
            _rincian = rincian;
            var r = rincian.Resep;
            var judul = $"Resep {r.NoResep} · {r.Pasien} · {r.CaraBayar}" +
                        (r.Ranap ? $" · Rawat inap {rincian.Kelas}" : "");
            if (r.Status != ResepFarmasi.Menunggu)
            {
                TampilkanDiberikan(rincian, judul);
                return;
            }

            // Obat tidak aktif tidak ikut diberikan sampai diganti.
            foreach (var b in rincian.Baris)
                Tambahkan(new BarisResep(b, b.Aktif ? b.JumlahResep : 0, rincian.Embalase, rincian.Tuslah, Kelompok(b.Racik)));
            var catatan = rincian.Halangan.ToList();
            if (!_service.BolehValidasi) catatan.Add("Hak akses Pemberian Obat diperlukan untuk memvalidasi.");
            var kurang = rincian.Baris.Where(b => b.Aktif && b.Stok < b.JumlahResep).Select(b => b.Nama).Distinct().ToList();
            if (kurang.Count > 0) catatan.Add($"Stok kurang: {string.Join(", ", kurang)}.");
            var racikan = rincian.Racikan.Select(x =>
                $"Racikan {x.NoRacik}: {x.Nama}, {x.Jumlah} {x.Metode}, aturan {x.Aturan}" +
                (x.Keterangan.Length > 0 ? $" ({x.Keterangan})" : ""));
            _info.Text = string.Join("\n", new[] { $"{judul} · Stok dari {rincian.NamaDepo}" }
                .Concat(racikan)
                .Append(string.Join(" ", catatan))
                .Where(t => t.Length > 0));
            var bisa = _service.BolehValidasi && rincian.Halangan.Count == 0;
            _obat.IsReadOnly = !bisa;
            _tambah.IsEnabled = _validasi.IsEnabled = bisa;
            AturTombolObat();
            Hitung();
        });
    }

    private void TampilkanDiberikan(RincianResep rincian, string judul)
    {
        var r = rincian.Resep;
        _obat.IsVisible = false;
        _diberikan.IsVisible = true;
        Ui.FillRows(_diberikan, rincian.Diberikan.Select(o => new GridRow(
        [
            o.Nama, o.Jumlah.ToString("0.##", CultureInfo.InvariantCulture), o.Satuan, o.Aturan, o.NoBatch,
            Ui.Money(o.Harga), Ui.Money(o.Embalase), Ui.Money(o.Tuslah), Ui.Money(o.Total),
        ], o)));
        _info.Text = $"{judul} · {r.Status}" +
                     (rincian.Diberikan.Count == 0 ? "\nTidak ada catatan pemberian obat pada waktu validasi ini." : "");
        _total.Text = $"Total obat: Rp {Ui.Money(rincian.Diberikan.Sum(o => o.Total))}";
        var belumSerah = _service.BolehValidasi && r.Status == ResepFarmasi.Tervalidasi;
        _batal.IsEnabled = belumSerah;
        _serahkan.IsEnabled = belumSerah && !r.Ranap;
    }

    private string Kelompok(string racik) =>
        _rincian?.Racikan.FirstOrDefault(x => x.NoRacik == racik) is { } r ? $"{r.NoRacik}. {r.Nama}" : "";

    private void Tambahkan(BarisResep baris, int posisi = -1)
    {
        baris.PropertyChanged += (_, _) => Hitung();
        if (posisi < 0) _baris.Add(baris);
        else _baris.Insert(posisi, baris);
    }

    private void AturTombolObat()
    {
        var bisa = _tambah.IsEnabled;
        var pilih = _obat.SelectedItem as BarisResep;
        _ganti.IsEnabled = bisa && pilih is not null;
        _hapus.IsEnabled = bisa && pilih?.Asal.Tambahan == true;
    }

    /// <summary>
    /// Tanpa baris asal: obat tambahan. Dengan baris asal: jumlah baris itu menjadi 0 dan penggantinya masuk
    /// pada kelompok yang sama dengan jumlah dan aturan pakai yang sama.
    /// </summary>
    private async Task PilihObatAsync(BarisResep? diganti)
    {
        if (_sibuk || _rincian is not { } rincian) return;
        _obat.CommitEdit();
        var muatan = _muatan;
        var jendela = new PilihObatWindow(diganti is null ? "Tambah Obat" : $"Ganti {diganti.Asal.Nama}",
            cari => _service.CariObatAsync(rincian.Resep.NoResep, cari));
        if (!await jendela.ShowForAsync(this) || jendela.Dipilih is not { } obat || muatan != _muatan) return;
        PasangObat(obat, diganti);
    }

    /// <summary>Memasukkan hasil pilihan ke tabel; dipisah dari dialog agar dapat diuji.</summary>
    public void PasangObat(BarisValidasi obat, BarisResep? diganti)
    {
        if (_rincian is not { } rincian) return;
        var racik = diganti?.Asal.Racik ?? "";
        if (_baris.FirstOrDefault(b => b.Asal.Racik == racik && b.Asal.Kode == obat.Kode) is { } ada)
        {
            Ui.Err(StatusText, $"{ada.Asal.Nama} sudah ada pada kelompok ini. Ubah jumlahnya pada tabel.");
            return;
        }

        var jumlah = 1d;
        if (diganti is not null)
        {
            var lama = Angka(diganti.Jumlah);
            jumlah = lama > 0 ? lama : diganti.Asal.JumlahResep;
            diganti.Jumlah = "0";
        }
        var aturan = diganti is null || racik.Length > 0 ? "" : diganti.Aturan;
        var baru = new BarisResep(obat with { Racik = racik, Aturan = aturan, Tambahan = true }, jumlah, rincian.Embalase,
            rincian.Tuslah, Kelompok(racik));
        Tambahkan(baru, diganti is null ? -1 : _baris.IndexOf(diganti) + 1);
        _obat.SelectedItem = baru;
        Ui.Info(StatusText, racik.Length == 0 && aturan.Length == 0
            ? "Isi jumlah dan aturan pakai obat tambahan."
            : "Periksa jumlah obat pengganti.");
        Hitung();

        static double Angka(string teks)
        {
            try { return Ui.ParseNumber(teks); }
            catch (FormatException) { return 0; }
        }
    }

    private void HapusTambahan()
    {
        if (_obat.SelectedItem is not BarisResep { Asal.Tambahan: true } baris) return;
        _obat.CommitEdit();
        _baris.Remove(baris);
        AturTombolObat();
        Hitung();
    }

    private void Hitung()
    {
        if (_rincian?.Resep.Status != ResepFarmasi.Menunggu) return;
        try { _total.Text = $"Total obat: Rp {Ui.Money(_baris.Sum(b => b.Nilai()))}"; }
        catch (FormatException ex) { _total.Text = ex.Message; }
    }

    private async Task ValidasiAsync()
    {
        if (_sibuk || _rincian is not { } rincian) return;
        _obat.CommitEdit();
        List<InputValidasi> input;
        double total;
        try
        {
            input = _baris.Select(b => b.KeInput()).ToList();
            total = _baris.Sum(b => b.Nilai());
        }
        catch (FormatException ex)
        {
            Ui.Err(StatusText, ex.Message);
            return;
        }

        var berbeda = _baris
            .Where(b => b.Asal.Tambahan ? Ui.ParseNumber(b.Jumlah) > 0 : Ui.ParseNumber(b.Jumlah) != b.Asal.JumlahResep)
            .Select(b => b.Asal.Tambahan ? $"Tambahan {b.Asal.Nama}: {b.Jumlah}" : $"{b.Asal.Nama}: {b.Jumlah} dari {b.Resep}")
            .ToList();
        var pesan = $"Validasi resep {rincian.Resep.NoResep} untuk {rincian.Resep.Pasien} dari {rincian.NamaDepo}?\n" +
                    $"Total obat Rp {Ui.Money(total)}." +
                    (berbeda.Count > 0 ? "\nBerbeda dari resep dokter:\n" + string.Join("\n", berbeda) : "");
        await KonfirmasiAsync("Validasi Resep", pesan, rincian.Resep.NoResep, async () =>
        {
            var dibebankan = await _service.ValidasiAsync(rincian.Resep.NoResep, input);
            return $"Resep {rincian.Resep.NoResep} divalidasi, total obat Rp {Ui.Money(dibebankan)}.";
        });
    }

    private async Task BatalAsync()
    {
        if (_sibuk || _rincian is not { } rincian) return;
        await KonfirmasiAsync("Batal Validasi",
            $"Batalkan validasi resep {rincian.Resep.NoResep} untuk {rincian.Resep.Pasien}?\n" +
            $"{rincian.Diberikan.Count} obat dikembalikan ke stok dan jurnal Rp {Ui.Money(rincian.Diberikan.Sum(o => o.Total))} dibalik.",
            rincian.Resep.NoResep, async () =>
            {
                await _service.BatalAsync(rincian.Resep.NoResep);
                return $"Validasi resep {rincian.Resep.NoResep} dibatalkan.";
            });
    }

    private async Task SerahkanAsync()
    {
        if (_sibuk || _rincian is not { } rincian) return;
        await KonfirmasiAsync("Serahkan Obat",
            $"Tandai obat resep {rincian.Resep.NoResep} sudah diterima {rincian.Resep.Pasien}?",
            rincian.Resep.NoResep, async () =>
            {
                await _service.SerahkanAsync(rincian.Resep.NoResep);
                return $"Resep {rincian.Resep.NoResep} ditandai sudah diserahkan.";
            });
    }

    private async Task KonfirmasiAsync(string judul, string pesan, string noResep, Func<Task<string>> kerjakan)
    {
        _sibuk = true;
        try
        {
            if (!await Dialogs.ConfirmAsync(this, judul, pesan)) return;
            await RunAsync(async () =>
            {
                var berhasil = await kerjakan();
                // Resep pindah status; tampilkan semua supaya tetap terlihat.
                _status.SelectedItem = SemuaStatus;
                await MuatDaftarAsync(noResep);
                Ui.Ok(StatusText, berhasil);
            });
        }
        finally { _sibuk = false; }
    }
}
