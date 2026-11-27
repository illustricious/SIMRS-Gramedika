using Avalonia.Controls;
using Avalonia.Layout;
using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Registrasi;

/// <summary>Formulir dan cetak surat sakit satu kunjungan, padanan SuratSakit Khanza.</summary>
internal sealed class SuratSakitWindow : FormDialog
{
    private const string Baru = "Surat baru";

    private readonly SuratSakitService _service;
    private readonly string _noRawat;
    private readonly ComboBox _pilihan = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _nomor = new() { MaxLength = 17, PlaceholderText = "Otomatis saat disimpan" };
    private readonly CalendarDatePicker _awal = new() { HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 160 };
    private readonly CalendarDatePicker _akhir = new() { HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 160 };
    private readonly TextBox _lama = new() { MaxLength = 20 };
    private readonly Button _cetak = new() { Content = "Cetak Surat Cuti Sakit", IsEnabled = false };
    private readonly StackPanel _body;
    private IReadOnlyList<SuratSakitData> _tersimpan = [];
    private bool _mengisi;

    public SuratSakitWindow(SuratSakitService service, string noRawat, string namaPasien) : base("Surat Sakit", 560)
    {
        _service = service;
        _noRawat = noRawat;
        _cetak.Click += async (_, _) => await CetakAsync();
        _pilihan.SelectionChanged += (_, _) =>
        {
            Isi(Terpilih);
            _cetak.IsEnabled = Terpilih is not null;
        };
        // Lama sakit mengikuti tanggal, tetapi tetap bisa ditulis ulang.
        _awal.SelectedDateChanged += (_, _) => HitungLama();
        _akhir.SelectedDateChanged += (_, _) => HitungLama();

        _body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Ui.Hint($"{namaPasien} | No. Rawat {noRawat}"),
                Ui.Form(("Data", _pilihan), ("No. Surat", _nomor), ("Tanggal awal", _awal), ("Tanggal akhir", _akhir),
                    ("Lama sakit", _lama)),
                _cetak,
                Ui.Hint("Surat dicetak dari data tersimpan. Surat baru: simpan dulu, lalu buka kembali."),
            },
        };
        SetBody(_body);
        _body.IsEnabled = false;
        Loaded += async (_, _) =>
        {
            try
            {
                _tersimpan = await _service.DaftarAsync(_noRawat);
                _pilihan.ItemsSource = _tersimpan.Select(s => $"{s.NoSurat} - {s.LamaSakit}").Prepend(Baru).ToList();
                // Kunjungan yang sudah punya surat dibuka pada surat terbaru agar tidak dibuat dua kali.
                _pilihan.SelectedIndex = _tersimpan.Count > 0 ? 1 : 0;
                _body.IsEnabled = true;
            }
            catch (Exception ex) { ErrorText.Text = $"Data surat sakit tidak dapat dimuat: {ex.Message}"; }
        };
    }

    private SuratSakitData? Terpilih => _pilihan.SelectedIndex > 0 ? _tersimpan[_pilihan.SelectedIndex - 1] : null;

    private void Isi(SuratSakitData? lama)
    {
        _mengisi = true;
        _nomor.Text = lama?.NoSurat ?? "";
        _nomor.IsReadOnly = lama is not null;
        _awal.SelectedDate = lama?.TanggalAwal ?? DateTime.Today;
        _akhir.SelectedDate = lama?.TanggalAkhir ?? DateTime.Today;
        _lama.Text = lama?.LamaSakit ?? SuratSakitService.LamaSakit(DateTime.Today, DateTime.Today);
        _mengisi = false;
    }

    private void HitungLama()
    {
        if (_mengisi || _awal.SelectedDate is not { } awal || _akhir.SelectedDate is not { } akhir) return;
        _lama.Text = SuratSakitService.LamaSakit(awal, akhir);
    }

    private async Task CetakAsync()
    {
        if (Terpilih is not { } surat) return;
        _cetak.IsEnabled = false;
        ErrorText.Text = "";
        try
        {
            var isi = await _service.CetakAsync(_noRawat, surat.NoSurat);
            var path = SuratSakitReport.GeneratePdf(isi, await KopInstansi.MuatAsync(DatabaseSettings.Load()));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { ErrorText.Text = $"Gagal membuat atau membuka surat: {ex.Message}"; }
        finally { _cetak.IsEnabled = Terpilih is not null; }
    }

    protected override Task OnSaveAsync()
    {
        if (!_body.IsEnabled) throw new InvalidOperationException("Data surat belum dimuat. Tutup lalu buka kembali.");
        if (_awal.SelectedDate is not { } awal || _akhir.SelectedDate is not { } akhir)
            throw new ArgumentException("Tanggal awal dan akhir wajib diisi.");
        return _service.SimpanAsync(_noRawat, new(_nomor.Text ?? "", awal, akhir, _lama.Text ?? ""), Terpilih?.NoSurat);
    }
}
