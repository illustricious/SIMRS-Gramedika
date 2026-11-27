using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Registrasi;

/// <summary>Formulir rujukan keluar satu kunjungan, padanan DlgRujuk.</summary>
internal sealed class RujukanKeluarWindow : FormDialog
{
    private const string Baru = "Rujukan baru";

    private readonly RujukanKeluarService _service;
    private readonly string _noRawat;
    private readonly string? _dokterKunjungan;
    private readonly ComboBox _pilihan = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _nomor = new() { MaxLength = 40, PlaceholderText = "Otomatis saat disimpan" };
    private readonly CalendarDatePicker _tanggal = new() { HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 160 };
    private readonly TextBox _jam = new() { Width = 100, HorizontalAlignment = HorizontalAlignment.Left, PlaceholderText = "00:00:00" };
    private readonly TextBox _tujuan = new() { MaxLength = 150 };
    private readonly TextBox _diagnosa = new() { AcceptsReturn = true, MinHeight = 60, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ComboBox _dokter = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _kategori = Ui.Choice(RujukanKeluarService.Kategori);
    private readonly ComboBox _ambulance = Ui.Choice(RujukanKeluarService.Ambulance);
    private readonly TextBox _keterangan = new() { AcceptsReturn = true, MinHeight = 60, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _cetak = new() { Content = "Cetak Surat Rujukan", IsEnabled = false };
    private readonly StackPanel _body;
    private IReadOnlyList<RujukanKeluarData> _tersimpan = [];

    public RujukanKeluarWindow(RujukanKeluarService service, string noRawat, string namaPasien,
        IReadOnlyList<Dokter> dokter, string? kodeDokterKunjungan) : base("Rujukan Keluar", 620)
    {
        _service = service;
        _cetak.Click += async (_, _) => await CetakAsync();
        _noRawat = noRawat;
        _dokterKunjungan = kodeDokterKunjungan;
        _dokter.DisplayMemberBinding = new Binding(nameof(Dokter.NamaDokter));
        _dokter.ItemsSource = dokter;
        _pilihan.SelectionChanged += (_, _) =>
        {
            Isi(Terpilih);
            _cetak.IsEnabled = Terpilih is not null;
        };

        _body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Ui.Hint($"{namaPasien} | No. Rawat {noRawat}"),
                Ui.Form(
                    ("Data", _pilihan), ("No. Rujuk", _nomor), ("Tgl. rujuk", _tanggal), ("Jam", _jam),
                    ("Rujuk ke", _tujuan), ("Diagnosa", _diagnosa), ("Dokter", _dokter),
                    ("Kategori rujuk", _kategori), ("Ambulance", _ambulance), ("Keterangan", _keterangan)),
                _cetak,
                Ui.Hint("Surat dicetak dari data tersimpan. Rujukan baru: simpan dulu, lalu buka kembali."),
            },
        };
        SetBody(_body);
        // Cegah menyimpan sebelum rujukan lama kunjungan ini diketahui.
        _body.IsEnabled = false;
        Loaded += async (_, _) =>
        {
            try
            {
                _tersimpan = await _service.DaftarAsync(_noRawat);
                _pilihan.ItemsSource = _tersimpan.Select(r => $"{r.NoRujuk} - {r.RujukKe}").Prepend(Baru).ToList();
                // Kunjungan yang sudah dirujuk dibuka pada data terbaru agar tidak tercatat dua kali.
                _pilihan.SelectedIndex = _tersimpan.Count > 0 ? 1 : 0;
                _body.IsEnabled = true;
            }
            catch (Exception ex) { ErrorText.Text = $"Data rujukan tidak dapat dimuat: {ex.Message}"; }
        };
    }

    private RujukanKeluarData? Terpilih =>
        _pilihan.SelectedIndex > 0 ? _tersimpan[_pilihan.SelectedIndex - 1] : null;

    private async Task CetakAsync()
    {
        if (Terpilih is not { } rujukan) return;
        _cetak.IsEnabled = false;
        ErrorText.Text = "";
        try
        {
            var surat = await _service.SuratAsync(_noRawat, rujukan.NoRujuk);
            var kop = await KopInstansi.MuatAsync(DatabaseSettings.Load());
            var path = SuratRujukanReport.GeneratePdf(surat, kop);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { ErrorText.Text = $"Gagal membuat atau membuka surat: {ex.Message}"; }
        finally { _cetak.IsEnabled = Terpilih is not null; }
    }

    private void Isi(RujukanKeluarData? lama)
    {
        var waktu = lama?.Waktu ?? DateTime.Now;
        _nomor.Text = lama?.NoRujuk ?? "";
        _nomor.IsReadOnly = lama is not null;
        _tanggal.SelectedDate = waktu.Date;
        _jam.Text = waktu.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _tujuan.Text = lama?.RujukKe ?? "";
        _diagnosa.Text = lama?.Diagnosa ?? "";
        _keterangan.Text = lama?.Keterangan ?? "";
        _kategori.SelectedItem = lama?.Kategori ?? "-";
        _ambulance.SelectedItem = lama?.Ambulance ?? "-";
        var kode = lama?.KodeDokter ?? _dokterKunjungan;
        // Dokter lama yang sudah tidak aktif dibiarkan kosong agar dipilih ulang.
        _dokter.SelectedItem = (_dokter.ItemsSource as IEnumerable<Dokter>)?.FirstOrDefault(d => d.KodeDokter == kode);
    }

    protected override Task OnSaveAsync()
    {
        if (!_body.IsEnabled) throw new InvalidOperationException("Data rujukan belum dimuat. Tutup lalu buka kembali.");
        if (_tanggal.SelectedDate is not { } tanggal) throw new ArgumentException("Tanggal rujuk wajib diisi.");
        if (!TimeSpan.TryParseExact(_jam.Text?.Trim(), [@"hh\:mm\:ss", @"hh\:mm"], CultureInfo.InvariantCulture, out var jam))
            throw new ArgumentException("Jam ditulis 00:00:00 sampai 23:59:59.");
        return _service.SimpanAsync(_noRawat, new(
            _nomor.Text ?? "", tanggal.Date + jam, _tujuan.Text ?? "", _diagnosa.Text ?? "",
            (_dokter.SelectedItem as Dokter)?.KodeDokter ?? "",
            _kategori.SelectedItem as string ?? "-", _ambulance.SelectedItem as string ?? "-", _keterangan.Text ?? ""),
            Terpilih?.NoRujuk);
    }
}
