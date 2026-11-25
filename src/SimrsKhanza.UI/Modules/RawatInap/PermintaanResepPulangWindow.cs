using SimrsKhanza.UI.Controls;
using Avalonia.Controls;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.Data.MenuA;
using SimrsKhanza.Data.Sql;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatInap;

internal sealed class PermintaanResepPulangWindow : FormDialog
{
    private readonly RawatInapListItem _visit;
    private readonly AuthenticatedUser _user;
    private readonly ResepPulangRepository _repository = new(DatabaseSettings.Load());
    private readonly TextBlock _dokterLabel = new() { Text = "Dokter belum dipilih" };
    private readonly StackPanel _daftar = new() { Spacing = 8 };
    private readonly List<BarisObat> _obat = [];
    private readonly ResepPulangRepository.DrafResep? _awal;
    private string _dokter = "";
    public string Nomor { get; private set; } = "";

    public PermintaanResepPulangWindow(RawatInapListItem visit, AuthenticatedUser user,
        ResepPulangRepository.DrafResep? awal = null)
        : base(awal is null ? "Buat Resep Pulang" : $"Ubah Resep Pulang · {awal.Nomor}", 700)
    {
        _visit = visit;
        _user = user;
        _awal = awal;
        if (awal is not null)
        {
            _dokter = awal.KodeDokter;
            _dokterLabel.Text = awal.NamaDokter;
            foreach (var item in awal.Obat)
                TambahBaris(item.Isi.Kode, item.Nama, item.Satuan, item.Isi.Jumlah, item.Isi.Dosis);
        }
        SetBody(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Ui.Hint($"{visit.NamaPasien} · RM {visit.NoRekamMedis} · No. Rawat {visit.NoRawat}"),
                Ui.Bar(Ui.Btn("Pilih Dokter", async (_, _) => await PilihAsync(PilihDokterAsync)), _dokterLabel),
                Ui.Btn("Tambah Obat", async (_, _) => await PilihAsync(TambahObatAsync)),
                _daftar,
            },
        }, awal is null ? "Kirim ke Farmasi" : "Simpan Perubahan");
    }

    private async Task PilihAsync(Func<Task> action)
    {
        ErrorText.Text = "";
        try { await action(); }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }

    private async Task PilihDokterAsync()
    {
        var dokter = await SearchPickerDialog.ShowAsync<Row>(this, "Pilih Dokter",
            q => _repository.CariDokterAsync(_user, q), r => $"{r.S("kode")} · {r.S("nama")}");
        if (dokter is null) return;
        _dokter = dokter.S("kode");
        _dokterLabel.Text = dokter.S("nama");
    }

    private async Task TambahObatAsync()
    {
        var obat = await SearchPickerDialog.ShowAsync<Row>(this, "Pilih Obat",
            q => _repository.CariObatAsync(_user, q), r => $"{r.S("kode")} · {r.S("nama")} · {r.S("satuan")}");
        if (obat is null) return;

        var kode = obat.S("kode");
        if (_obat.Any(x => x.Kode == kode))
        {
            ErrorText.Text = "Obat sudah ada. Ubah jumlah atau aturan pakai pada barisnya.";
            return;
        }

        ErrorText.Text = "";
        TambahBaris(kode, obat.S("nama"), obat.S("satuan"), 1, "").Focus();
    }

    private TextBox TambahBaris(string kode, string nama, string satuan, double nilai, string aturan)
    {
        var jumlah = new NumericUpDown { Value = double.IsFinite(nilai) && nilai is >= 0 and <= 99999 ? (decimal)nilai : null, Minimum = 0, Maximum = 99999, Increment = 1, FormatString = "0.###", Width = 115 };
        var dosis = new TextBox { MaxLength = 150, PlaceholderText = "Aturan pakai", Width = 320, Text = aturan };
        Avalonia.Automation.AutomationProperties.SetName(jumlah, $"Jumlah {nama}");
        Avalonia.Automation.AutomationProperties.SetName(dosis, $"Aturan pakai {nama}");
        var hapus = new Button { Content = "Hapus" };
        var row = new StackPanel
        {
            Spacing = 4,
            Children = { new TextBlock { Text = $"{nama} ({satuan})" }, Ui.Bar(jumlah, dosis, hapus) },
        };
        var item = new BarisObat(kode, jumlah, dosis);
        _obat.Add(item);
        _daftar.Children.Add(row);
        hapus.Click += (_, _) =>
        {
            _obat.Remove(item);
            _daftar.Children.Remove(row);
        };
        return dosis;
    }

    protected override async Task OnSaveAsync()
    {
        if (_dokter.Length == 0) throw new InvalidOperationException("Pilih dokter penulis resep.");
        var obat = _obat.Select(x => new ResepPulangRepository.ObatDiminta(
            x.Kode, (double)(x.Jumlah.Value ?? 0), x.Dosis.Text ?? "")).ToArray();
        if (_awal is null)
        {
            Nomor = await _repository.BuatPermintaanAsync(_user, _visit.NoRawat, _dokter, obat);
            return;
        }
        await _repository.UbahPermintaanAsync(_user, _visit.NoRawat, _awal, _dokter, obat);
        Nomor = _awal.Nomor;
    }

    private sealed record BarisObat(string Kode, NumericUpDown Jumlah, TextBox Dosis);
}
