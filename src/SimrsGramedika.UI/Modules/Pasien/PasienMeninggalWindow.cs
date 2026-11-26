using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Pasien;

/// <summary>Formulir data pasien meninggal, dibuka setelah Set Status "Meninggal" seperti DlgPasienMati.</summary>
internal sealed class PasienMeninggalWindow : FormDialog
{
    private readonly PasienMeninggalService _service;
    private readonly string _noRm;
    private readonly CalendarDatePicker _tanggal = new() { HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 160 };
    private readonly TextBox _jam = new() { Width = 100, HorizontalAlignment = HorizontalAlignment.Left, PlaceholderText = "00:00:00" };
    private readonly ComboBox _tempat = Ui.Choice(PasienMeninggalService.Tempat.ToArray());
    private readonly TextBox _keterangan = new() { MaxLength = 100 };
    private readonly TextBox[] _icd = Enumerable.Range(0, 4).Select(_ => new TextBox { MaxLength = 20 }).ToArray();
    private readonly ComboBox _dokter = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _body;

    public PasienMeninggalWindow(PasienMeninggalService service, string noRm, string namaPasien,
        IReadOnlyList<Dokter> dokter, string? kodeDokterKunjungan) : base("Data Pasien Meninggal", 620)
    {
        _service = service;
        _noRm = noRm;
        _dokter.DisplayMemberBinding = new Binding(nameof(Dokter.NamaDokter));
        _dokter.ItemsSource = dokter;
        _dokter.SelectedItem = dokter.FirstOrDefault(d => d.KodeDokter == kodeDokterKunjungan);
        _tempat.HorizontalAlignment = HorizontalAlignment.Stretch;
        _tempat.SelectedItem = "-";
        var sekarang = DateTime.Now;
        _tanggal.SelectedDate = sekarang.Date;
        _jam.Text = sekarang.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        _body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Ui.Hint($"{namaPasien} | No. RM {noRm}"),
                Ui.Form(
                    ("Tgl. meninggal", _tanggal), ("Jam", _jam), ("Di", _tempat), ("Penyebab kematian", _keterangan),
                    // Pasangan kolom mengikuti posisi di DlgPasienMati: icd4 langsung, icd1 dasar.
                    ("ICD-X langsung", _icd[3]), ("ICD-X antara #1", _icd[1]), ("ICD-X antara #2", _icd[2]),
                    ("ICD-X dasar", _icd[0]), ("Dokter DPJP", _dokter)),
            },
        };
        SetBody(_body);
        // Cegah menimpa data lama sebelum selesai dimuat.
        _body.IsEnabled = false;
        Loaded += async (_, _) =>
        {
            try
            {
                if (await _service.MuatAsync(_noRm) is { } lama)
                {
                    Isi(lama);
                    ErrorText.Text = "Pasien ini sudah tercatat meninggal. Menyimpan akan memperbarui data lama.";
                }
                _body.IsEnabled = true;
            }
            catch (Exception ex) { ErrorText.Text = $"Data lama tidak dapat dimuat: {ex.Message}"; }
        };
    }

    private void Isi(PasienMeninggalData d)
    {
        _tanggal.SelectedDate = d.Waktu.Date;
        _jam.Text = d.Waktu.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _tempat.SelectedItem = d.TempatMeninggal;
        _keterangan.Text = d.Keterangan;
        _icd[0].Text = d.Icd1;
        _icd[1].Text = d.Icd2;
        _icd[2].Text = d.Icd3;
        _icd[3].Text = d.Icd4;
        // Dokter lama yang sudah tidak aktif dibiarkan kosong agar dipilih ulang.
        _dokter.SelectedItem = (_dokter.ItemsSource as IEnumerable<Dokter>)?.FirstOrDefault(x => x.KodeDokter == d.KodeDokter);
    }

    protected override Task OnSaveAsync()
    {
        if (!_body.IsEnabled) throw new InvalidOperationException("Data lama belum dimuat. Tutup lalu buka kembali.");
        if (_tanggal.SelectedDate is not { } tanggal) throw new ArgumentException("Tanggal meninggal wajib diisi.");
        if (!TimeSpan.TryParseExact(_jam.Text?.Trim(), [@"hh\:mm\:ss", @"hh\:mm"], CultureInfo.InvariantCulture, out var jam))
            throw new ArgumentException("Jam ditulis 00:00:00 sampai 23:59:59.");
        return _service.SimpanAsync(_noRm, new(
            tanggal.Date + jam, _keterangan.Text ?? "", _tempat.SelectedItem as string ?? "-",
            (_dokter.SelectedItem as Dokter)?.KodeDokter ?? "",
            _icd[0].Text ?? "", _icd[1].Text ?? "", _icd[2].Text ?? "", _icd[3].Text ?? ""));
    }
}
