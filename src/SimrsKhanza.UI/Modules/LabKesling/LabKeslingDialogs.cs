using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.LabKesling;
using SimrsKhanza.Data.Sql;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.LabKesling;

internal static class Lk
{
    public static LabKeslingRepository Repo() => new(DatabaseSettings.Load());

    public static async Task<LookupBox> PetugasBox(string? kode = null)
    {
        var b = new LookupBox();
        b.SetItems(await Repo().PetugasAsync());
        if (kode is not null) b.Kode = kode;
        return b;
    }

    public static TextBlock Info(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.Medium, Margin = new Thickness(0, 0, 0, 8) };
}

/// <summary>Legacy LabKeslingPermintaanPengujianSampel (input form).</summary>
public class PermintaanDialog : FormDialog
{
    private readonly LookupBox _pelanggan = new(), _sampel = new(), _petugas = new();
    private readonly DateTimeBox _sampling = new(), _diterima = new();
    private readonly TextBox _lokasi = Ui.Text(maxLen: 100), _deskripsi = Ui.Text(maxLen: 100), _jenis = Ui.Text(maxLen: 30),
        _jumlah = Ui.Text(maxLen: 20), _oleh = Ui.Text(maxLen: 30), _volume = Ui.Text(maxLen: 20), _wadah = Ui.Text(maxLen: 30), _kondisi = Ui.Text(maxLen: 40);
    private readonly StackPanel _params = new() { Spacing = 2 };
    public string NoPermintaan { get; private set; } = "";

    public PermintaanDialog() : base("Permintaan Pengujian Sampel Lab Kesling", 720)
    {
        _sampel.SelectionChanged += async (_, _) => await LoadParamsAsync();
        SetBody(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ui.Form(("Pelanggan :", _pelanggan), ("Petugas Penerima :", _petugas), ("Waktu Sampling :", _sampling), ("Waktu Diterima :", _diterima),
                    ("Lokasi Sampling :", _lokasi), ("Deskripsi Sampel :", _deskripsi), ("Jenis Sampel :", _jenis), ("Jumlah Sampel :", _jumlah),
                    ("Sampling Oleh :", _oleh), ("Volume Sampel :", _volume), ("Wadah Sampel :", _wadah), ("Kondisi Wadah :", _kondisi),
                    ("Sampel / Baku Mutu :", _sampel)),
                Ui.Hint("Parameter pengujian (yang punya nilai normal untuk sampel terpilih):"),
                Ui.Box(new ScrollViewer { Content = _params, MaxHeight = 220, Padding = new Thickness(6) }),
            },
        });
    }

    public async Task InitAsync()
    {
        var repo = Lk.Repo();
        _pelanggan.SetItems(await repo.PelangganAsync());
        _sampel.SetItems(await repo.SampelAsync());
        _petugas.SetItems(await repo.PetugasAsync());
    }

    private async Task LoadParamsAsync()
    {
        _params.Children.Clear();
        if (_sampel.Kode.Length == 0) return;
        try
        {
            foreach (var p in await Lk.Repo().ParameterUntukSampelAsync(_sampel.Kode))
                _params.Children.Add(new CheckBox
                {
                    IsChecked = true, Tag = p.S("kode_parameter"),
                    Content = $"{p.S("kode_parameter")}  {p.S("nama_parameter")}  [{p.S("kategori")} · {p.S("metode_pengujian")}]  normal: {p.S("nilai_normal")} {p.S("satuan")}  · Rp {Ui.Money(p.D("total"))}",
                });
            if (_params.Children.Count == 0) _params.Children.Add(Ui.Hint("Belum ada nilai normal baku mutu untuk sampel ini (isi di menu Nilai Normal Baku Mutu)."));
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }

    protected override async Task OnSaveAsync()
    {
        var kode = _params.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToList();
        NoPermintaan = await Lk.Repo().CreatePermintaanAsync(new PermintaanInput(
            _pelanggan.Kode, _petugas.Kode, _sampling.Value, _diterima.Value, _lokasi.Text ?? "", _deskripsi.Text ?? "", _jenis.Text ?? "",
            _jumlah.Text ?? "", _oleh.Text ?? "", _volume.Text ?? "", _wadah.Text ?? "", _kondisi.Text ?? "", _sampel.Kode, kode));
    }
}

/// <summary>Keterangan for Dapat / Tidak Dapat Dilayani.</summary>
public class KeteranganDialog : FormDialog
{
    private readonly TextBox _ket = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 100, MaxLength = 1000 };
    private readonly string _no;
    private readonly bool _dapat;

    public KeteranganDialog(string noPermintaan, bool dapat) : base(dapat ? "Sampel Dapat Dilayani" : "Sampel Tidak Dapat Dilayani", 480)
    {
        _no = noPermintaan;
        _dapat = dapat;
        SetBody(new StackPanel { Spacing = 6, Children = { Lk.Info($"No.Permintaan {noPermintaan}"), Ui.L("Keterangan :"), _ket } });
    }

    protected override Task OnSaveAsync() => Lk.Repo().SetDapatDilayaniAsync(_no, _dapat, _ket.Text ?? "");
}

/// <summary>Legacy LabKeslingPenugasanPengujianSampel.</summary>
public class PenugasanDialog : FormDialog
{
    private readonly string _no;
    private LookupBox _pj = new(), _pelaksana = new();
    private readonly DateTimeBox _tanggal = new();
    private readonly TextBox _catatan = Ui.Text(maxLen: 50);
    private readonly StackPanel _params = new() { Spacing = 2 };
    public string NoPenugasan { get; private set; } = "";

    public PenugasanDialog(Row permintaan) : base("Penugasan Pengujian Sampel", 640)
    {
        _no = permintaan.S("no_permintaan");
        Tag = permintaan;
    }

    public async Task InitAsync()
    {
        var p = (Row)Tag!;
        _pj = await Lk.PetugasBox();
        _pelaksana = await Lk.PetugasBox();
        foreach (var d in await Lk.Repo().DetailPermintaanAsync(_no))
            _params.Children.Add(new CheckBox { IsChecked = true, Tag = d.S("kode_parameter"), Content = $"{d.S("kode_parameter")}  {d.S("nama_parameter")}  [{d.S("metode_pengujian")}]" });
        SetBody(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Lk.Info($"{_no} · {p.S("nama_pelanggan")} · {p.S("nama_sampel")}"),
                Ui.Form(("PJ Pengujian :", _pj), ("Pelaksana/Analis :", _pelaksana), ("Tanggal :", _tanggal), ("Catatan :", _catatan)),
                Ui.Hint("Parameter yang ditugaskan:"),
                Ui.Box(new ScrollViewer { Content = _params, MaxHeight = 220, Padding = new Thickness(6) }),
            },
        });
    }

    protected override async Task OnSaveAsync()
    {
        var kode = _params.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToList();
        NoPenugasan = await Lk.Repo().CreatePenugasanAsync(_no, _pj.Kode, _pelaksana.Kode, _tanggal.Value, _catatan.Text ?? "", kode);
    }
}

/// <summary>Legacy LabKeslingHasilPengujianSampel: one hasil per assigned parameter.</summary>
public class HasilDialog : FormDialog
{
    private readonly string _noPenugasan;
    private readonly DateTimeBox _tanggal = new();
    private readonly List<(string Kode, TextBox Box)> _inputs = [];

    public HasilDialog(string noPenugasan) : base("Input Hasil Pengujian Sampel", 720) => _noPenugasan = noPenugasan;

    public async Task InitAsync()
    {
        var grid = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,160") };
        var i = 0;
        void Add(Control c, int col) { Avalonia.Controls.Grid.SetRow(c, i); Avalonia.Controls.Grid.SetColumn(c, col); c.Margin = new Thickness(0, 2, 8, 2); grid.Children.Add(c); }
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Add(new TextBlock { Text = "Kode", FontWeight = FontWeight.Bold }, 0);
        Add(new TextBlock { Text = "Parameter", FontWeight = FontWeight.Bold }, 1);
        Add(new TextBlock { Text = "Nilai Normal", FontWeight = FontWeight.Bold }, 2);
        Add(new TextBlock { Text = "Hasil", FontWeight = FontWeight.Bold }, 3);
        foreach (var d in await Lk.Repo().DetailPenugasanAsync(_noPenugasan))
        {
            i++;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Add(Ui.L(d.S("kode_parameter")), 0);
            Add(new TextBlock { Text = $"{d.S("nama_parameter")} ({d.S("satuan")})", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }, 1);
            Add(Ui.L(d.S("nilai_normal")), 2);
            var box = new TextBox { Text = d.S("hasil_pengujian"), MaxLength = 30, IsEnabled = d.S("status_hasil") != "Sudah Diverifikasi" };
            Add(box, 3);
            _inputs.Add((d.S("kode_parameter"), box));
        }
        SetBody(new StackPanel { Spacing = 6, Children = { Lk.Info($"No.Penugasan {_noPenugasan}"), Ui.Form(("Tanggal Hasil :", _tanggal)), grid } });
    }

    protected override Task OnSaveAsync() => Lk.Repo().SaveHasilAsync(_noPenugasan, _tanggal.Value,
        _inputs.Where(x => x.Box.IsEnabled).Select(x => new HasilInput(x.Kode, x.Box.Text ?? "")).ToList());
}

/// <summary>Legacy LabKeslingVerifikasiPengujianSampel.</summary>
public class VerifikasiDialog : FormDialog
{
    private readonly Row _permintaan;
    private LookupBox _pj = new();
    private readonly DateTimeBox _tanggal = new(), _mulai = new(), _selesai = new();
    private readonly TextBox _catatan = Ui.Text(maxLen: 100);
    private readonly List<(Row Hasil, TextBox Normal, TextBox Hasil2, TextBox Ket)> _rows = [];
    public string NoVerifikasi { get; private set; } = "";

    public VerifikasiDialog(Row permintaanSiap) : base("Verifikasi Pengujian Sampel", 860) => _permintaan = permintaanSiap;

    public async Task InitAsync()
    {
        _pj = await Lk.PetugasBox();
        var hasil = await Lk.Repo().HasilUntukVerifikasiAsync(_permintaan.S("no_permintaan"));
        if (hasil.Count > 0)
        {
            _mulai.Value = hasil.Min(h => h.Dt("tanggal") ?? DateTime.Now);
            _selesai.Value = hasil.Max(h => h.Dt("tanggal") ?? DateTime.Now);
        }
        var grid = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,130,130,*") };
        var i = 0;
        void Add(Control c, int col) { Avalonia.Controls.Grid.SetRow(c, i); Avalonia.Controls.Grid.SetColumn(c, col); c.Margin = new Thickness(0, 2, 8, 2); grid.Children.Add(c); }
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        foreach (var (h, c) in new[] { ("Penugasan", 0), ("Parameter", 1), ("Nilai Normal", 2), ("Hasil", 3), ("Keterangan", 4) })
            Add(new TextBlock { Text = h, FontWeight = FontWeight.Bold }, c);
        foreach (var r in hasil)
        {
            i++;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Add(Ui.L(r.S("no_penugasan")), 0);
            Add(new TextBlock { Text = $"{r.S("kode_parameter")} {r.S("nama_parameter")} ({r.S("satuan")})", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }, 1);
            var n = new TextBox { Text = r.S("nilai_normal"), MaxLength = 30 };
            var h2 = new TextBox { Text = r.S("hasil_pengujian"), MaxLength = 30 };
            var k = new TextBox { MaxLength = 100 };
            Add(n, 2); Add(h2, 3); Add(k, 4);
            _rows.Add((r, n, h2, k));
        }
        SetBody(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Lk.Info($"{_permintaan.S("no_permintaan")} · {_permintaan.S("nama_pelanggan")} · {_permintaan.S("nama_sampel")}"),
                Ui.Form(("PJ Verifikasi :", _pj), ("Tanggal Verifikasi :", _tanggal), ("Mulai Pengujian :", _mulai), ("Selesai Pengujian :", _selesai), ("Catatan :", _catatan)),
                grid,
            },
        });
    }

    protected override async Task OnSaveAsync()
    {
        NoVerifikasi = await Lk.Repo().CreateVerifikasiAsync(_permintaan.S("no_permintaan"), _pj.Kode, _tanggal.Value, _catatan.Text ?? "",
            _mulai.Value, _selesai.Value,
            _rows.Select(x => new VerifikasiDetailInput(x.Hasil.S("no_penugasan"), x.Hasil.S("kode_parameter"), x.Normal.Text ?? "", x.Hasil2.Text ?? "", x.Ket.Text ?? "")).ToList());
    }
}

/// <summary>Legacy LabKeslingValidasiPengujianSampel (posts the pelayanan journal).</summary>
public class ValidasiDialog : FormDialog
{
    private readonly Row _verifikasi;
    private LookupBox _pj = new();
    private readonly DateTimeBox _tanggal = new();
    private readonly TextBox _catatan = Ui.Text(maxLen: 100);
    public string NoValidasi { get; private set; } = "";

    public ValidasiDialog(Row verifikasi) : base("Validasi Pengujian Sampel", 900) => _verifikasi = verifikasi;

    public async Task InitAsync()
    {
        _pj = await Lk.PetugasBox();
        var detail = await Lk.Repo().DetailUntukValidasiAsync(_verifikasi.S("no_verifikasi"));
        var grid = Ui.Grid(new Col("kode_parameter", "Kode"), new Col("nama_parameter", "Parameter"), new Col("nilai_normal", "Normal"),
            new Col("hasil_pengujian", "Hasil"), new Col("keterangan", "Ket"), new Col("jasa_sarana", "J.Sarana", Money: true),
            new Col("paket_bhp", "BHP", Money: true), new Col("jasa_pj_lab", "J.PJ Lab", Money: true), new Col("jasa_pj_pengujian", "J.PJ Uji", Money: true),
            new Col("jasa_verifikator", "J.Verif", Money: true), new Col("jasa_petugas", "J.Analis", Money: true), new Col("kso", "KSO", Money: true),
            new Col("jasa_menejemen", "J.Menej", Money: true), new Col("total", "Total", Money: true));
        grid.Height = 240;
        Ui.Fill(grid, detail);
        SetBody(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Lk.Info($"{_verifikasi.S("no_verifikasi")} · {_verifikasi.S("nama_pelanggan")} · {_verifikasi.S("nama_sampel")} · Verifikator: {_verifikasi.S("nama_verifikator")}"),
                Ui.Form(("PJ Laboratorium :", _pj), ("Tanggal Validasi :", _tanggal), ("Catatan :", _catatan)),
                grid,
                new TextBlock { Text = $"Total tagihan: Rp {Ui.Money(detail.Sum(d => d.D("total")))}", FontWeight = FontWeight.Bold },
                Ui.Hint("Saat disimpan, jurnal 'PELAYANAN LABORATORIUM KESEHATAN LINGKUNGAN' diposting otomatis (rekening dari set_akun2)."),
            },
        }, "✔ Validasi & Posting");
    }

    protected override async Task OnSaveAsync()
        => NoValidasi = await Lk.Repo().CreateValidasiAsync(_verifikasi.S("no_verifikasi"), _pj.Kode, _tanggal.Value, _catatan.Text ?? "", AppSession.Kode);
}

/// <summary>Legacy LabKeslingBayarTagihanPengujianSampel.</summary>
public class BayarDialog : FormDialog
{
    private readonly Row _validasi;
    private readonly DateTimeBox _tanggal = new();
    private readonly TextBox _oleh = Ui.Text(maxLen: 50);
    private readonly List<(string Nama, string Rek, double Ppn, TextBox Box, TextBlock PpnText)> _akun = [];
    private readonly TextBlock _ringkas = new() { FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
    private double _total;
    public string NoBayar { get; private set; } = "";

    public BayarDialog(Row validasi) : base("Bayar Tagihan Pengujian Sampel", 640) => _validasi = validasi;

    public async Task InitAsync()
    {
        _total = _validasi.D("total");
        _oleh.Text = _validasi.S("nama_pelanggan");
        var grid = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,150,Auto") };
        var i = 0;
        foreach (var (nama, rek, ppn) in await Lk.Repo().AkunBayarAsync())
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var box = new TextBox { PlaceholderText = "0" };
            var ppnText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
            box.TextChanged += (_, _) => Hitung();
            var l = Ui.L(nama);
            var p = Ui.L(ppn > 0 ? $"PPN {ppn:0.##}%" : "");
            foreach (var (c, col) in new (Control, int)[] { (l, 0), (p, 1), (box, 2), (ppnText, 3) })
            {
                Avalonia.Controls.Grid.SetRow(c, i);
                Avalonia.Controls.Grid.SetColumn(c, col);
                c.Margin = new Thickness(0, 2, 8, 2);
                grid.Children.Add(c);
            }
            _akun.Add((nama, rek, ppn, box, ppnText));
            i++;
        }
        SetBody(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Lk.Info($"{_validasi.S("no_permintaan")} · {_validasi.S("nama_pelanggan")} · {_validasi.S("nama_sampel")}"),
                new TextBlock { Text = $"Tagihan: Rp {Ui.Money(_total)}", FontSize = 16, FontWeight = FontWeight.Bold },
                Ui.Form(("Tanggal Bayar :", _tanggal), ("Dibayar Oleh :", _oleh)),
                Ui.Hint("Isi nominal pada cara bayar yang dipakai:"),
                Ui.Box(new ScrollViewer { Content = grid, MaxHeight = 260, Padding = new Thickness(6) }),
                _ringkas,
            },
        }, "💳 Bayar & Posting");
        Hitung();
    }

    private void Hitung()
    {
        double bayar = 0, ppn = 0;
        foreach (var a in _akun)
        {
            double v;
            try { v = Ui.ParseNumber(a.Box.Text); } catch { v = 0; }
            var p = v > 0 ? LabKeslingRepository.RoundUp100(a.Ppn / 100 * v) : 0;
            a.PpnText.Text = p > 0 ? $"PPN {Ui.Money(p)}" : "";
            bayar += v;
            ppn += p;
        }
        var kembali = bayar - _total;
        _ringkas.Text = $"Dibayar: Rp {Ui.Money(bayar)}   PPN: Rp {Ui.Money(ppn)}   Total + PPN: Rp {Ui.Money(_total + ppn)}   "
                        + (kembali < 0 ? $"Kurang: Rp {Ui.Money(-kembali)}" : $"Kembali: Rp {Ui.Money(kembali)}");
        _ringkas.Foreground = kembali < 0 ? Brush.Parse("#DC2626") : Brushes.Green;
    }

    protected override async Task OnSaveAsync()
    {
        var lines = _akun.Select(a => new BayarInput(a.Nama, a.Rek, a.Ppn, Ui.ParseNumber(a.Box.Text))).ToList();
        NoBayar = await Lk.Repo().CreatePembayaranAsync(_validasi.S("no_permintaan"), _tanggal.Value, _oleh.Text ?? "", lines, AppSession.Kode);
    }
}
