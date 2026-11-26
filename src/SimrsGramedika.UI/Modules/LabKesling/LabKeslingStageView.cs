using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.LabKesling;

public enum LabKeslingStage
{
    Permintaan, TidakDilayani, Dilayani, Penugasan, Hasil, Verifikasi, Validasi, Pembayaran, RekapPelayanan, RekapPembayaran,
}

/// <summary>
/// One screen per Lab Kesling workflow tile: period/status/search filter, the stage's list,
/// a detail grid for the selected row, and the actions legacy offers on that stage
/// (create next stage, cancel/delete with status rollback).
/// </summary>
public class LabKeslingStageView : PageView
{
    private record Action(string Text, Func<Row?, Task> Run, bool NeedsRow = true);

    private readonly LabKeslingStage _stage;
    private readonly DataGrid _grid, _detail;
    private readonly CalendarDatePicker _from = new() { SelectedDate = DateTime.Today.AddDays(-30) };
    private readonly CalendarDatePicker _to = new() { SelectedDate = DateTime.Today };
    private readonly ComboBox _status;
    private readonly TextBox _cari = Ui.Text("Cari...");
    private readonly TextBlock _summary = new() { FontWeight = FontWeight.Medium, Margin = new Thickness(0, 4) };
    private readonly string[] _statuses;
    private readonly Func<DateTime, DateTime, string?, string?, Task<List<Row>>> _load;
    private readonly Func<Row, Task<List<Row>>>? _loadDetail;

    private static readonly Col[] PermintaanCols =
    [
        new("no_permintaan", "No.Permintaan"), new("waktu_diterima", "Diterima"), new("nama_pelanggan", "Pelanggan"), new("nama_sampel", "Sampel"),
        new("jenis_sampel", "Jenis"), new("lokasi_sampling", "Lokasi Sampling"), new("nama_petugas", "Petugas"), new("status", "Status"),
    ];
    private static readonly Col[] ParamCols =
        [new("kode_parameter", "Kode"), new("nama_parameter", "Parameter"), new("metode_pengujian", "Metode"), new("satuan", "Satuan"), new("kategori", "Kategori"), new("total", "Tarif", Money: true)];

    public LabKeslingStageView(LabKeslingStage stage)
    {
        _stage = stage;
        var r = Lk.Repo();
        string title, hint;
        Col[] cols, detailCols = [];
        List<Action> actions = [];

        switch (stage)
        {
            case LabKeslingStage.Permintaan:
                title = "Permintaan Pengujian Sampel Lab Kesling";
                hint = "Permintaan baru → tandai Dapat / Tidak Dapat Dilayani.";
                _statuses = ["Semua", "Permintaan Baru", "Dapat Dilayani", "Tidak Dapat Dilayani"];
                cols = PermintaanCols;
                _load = r.ListPermintaanAsync;
                detailCols = ParamCols;
                _loadDetail = x => r.DetailPermintaanAsync(x.S("no_permintaan"));
                actions =
                [
                    new("➕ Permintaan Baru", async _ => { var d = new PermintaanDialog(); await d.InitAsync(); if (await d.ShowForAsync(this)) await AfterAsync($"Permintaan {d.NoPermintaan} tersimpan."); }, false),
                    new("✅ Dapat Dilayani", x => Keterangan(x!, true)),
                    new("⛔ Tidak Dapat Dilayani", x => Keterangan(x!, false)),
                    new("🗑 Hapus", x => Confirm($"Hapus permintaan {x!.S("no_permintaan")}?", () => r.DeletePermintaanAsync(x.S("no_permintaan")), "Permintaan dihapus.")),
                ];
                break;

            case LabKeslingStage.TidakDilayani:
            case LabKeslingStage.Dilayani:
                var dapat = stage == LabKeslingStage.Dilayani;
                title = dapat ? "Sampel Pengujian Lab Kesling Dapat Dilayani" : "Sampel Pengujian Lab Kesling Tidak Dapat Dilayani";
                hint = dapat ? "Buat penugasan untuk permintaan yang dapat dilayani." : "Hapus = kembalikan permintaan ke status 'Permintaan Baru'.";
                _statuses = dapat ? ["Semua", "Belum Ada Penugasan", "Sudah Ada Penugasan"] : ["Semua"];
                cols = [new("no_permintaan", "No.Permintaan"), new("waktu_diterima", "Diterima"), new("nama_pelanggan", "Pelanggan"), new("nama_sampel", "Sampel"),
                    new("jenis_sampel", "Jenis"), new("lokasi_sampling", "Lokasi"), new("keterangan", "Keterangan"), new("status", "Status")];
                _load = async (f, t, s, c) => (await r.ListDilayaniAsync(dapat, f, t, c)).Where(x => s is null || x.S("status") == s).ToList();
                detailCols = ParamCols;
                _loadDetail = x => r.DetailPermintaanAsync(x.S("no_permintaan"));
                if (dapat)
                    actions.Add(new("📋 Buat Penugasan", async x =>
                    {
                        var d = new PenugasanDialog(x!);
                        await d.InitAsync();
                        if (await d.ShowForAsync(this)) await AfterAsync($"Penugasan {d.NoPenugasan} tersimpan.");
                    }));
                actions.Add(new("🗑 Hapus / Batalkan", x => Confirm($"Kembalikan {x!.S("no_permintaan")} ke 'Permintaan Baru'?",
                    () => r.DeleteDilayaniAsync(x.S("no_permintaan"), dapat), "Status dikembalikan ke Permintaan Baru.")));
                break;

            case LabKeslingStage.Penugasan:
                title = "Data Penugasan Pengujian Sampel Lab Kesling";
                hint = "Input hasil per penugasan.";
                _statuses = ["Semua", "Belum Keluar Hasil", "Sudah Keluar Hasil"];
                cols = [new("no_penugasan", "No.Penugasan"), new("no_permintaan", "No.Permintaan"), new("tanggal", "Tanggal"), new("nama_pelanggan", "Pelanggan"),
                    new("nama_sampel", "Sampel"), new("nama_pj", "PJ Pengujian"), new("nama_pelaksana", "Pelaksana"), new("catatan", "Catatan"), new("status", "Status")];
                _load = r.ListPenugasanAsync;
                detailCols = [new("kode_parameter", "Kode"), new("nama_parameter", "Parameter"), new("metode_pengujian", "Metode"), new("satuan", "Satuan"),
                    new("nilai_normal", "Nilai Normal"), new("hasil_pengujian", "Hasil"), new("status_hasil", "Status Hasil")];
                _loadDetail = x => r.DetailPenugasanAsync(x.S("no_penugasan"));
                actions =
                [
                    new("🧪 Input Hasil", async x => { var d = new HasilDialog(x!.S("no_penugasan")); await d.InitAsync(); if (await d.ShowForAsync(this)) await AfterAsync("Hasil pengujian tersimpan."); }),
                    new("🗑 Hapus Penugasan", x => Confirm($"Hapus penugasan {x!.S("no_penugasan")}?", () => r.DeletePenugasanAsync(x.S("no_penugasan")), "Penugasan dihapus.")),
                ];
                break;

            case LabKeslingStage.Hasil:
                title = "Data Hasil Pengujian Sampel Lab Kesling";
                hint = "Satu baris per parameter. Hapus hanya untuk hasil yang belum diverifikasi.";
                _statuses = ["Semua", "Belum Diverifikasi", "Sudah Diverifikasi"];
                cols = [new("no_penugasan", "No.Penugasan"), new("no_permintaan", "No.Permintaan"), new("tanggal", "Tanggal"), new("nama_pelanggan", "Pelanggan"),
                    new("nama_sampel", "Sampel"), new("nama_parameter", "Parameter"), new("satuan", "Satuan"), new("nilai_normal", "Nilai Normal"),
                    new("hasil_pengujian", "Hasil"), new("nama_pelaksana", "Analis"), new("status", "Status")];
                _load = r.ListHasilAsync;
                actions =
                [
                    new("✏ Ubah Hasil Penugasan", async x => { var d = new HasilDialog(x!.S("no_penugasan")); await d.InitAsync(); if (await d.ShowForAsync(this)) await AfterAsync("Hasil pengujian tersimpan."); }),
                    new("🗑 Hapus Hasil", x => Confirm($"Hapus hasil {x!.S("nama_parameter")} ({x.S("no_penugasan")})?",
                        () => r.DeleteHasilAsync(x.S("no_penugasan"), x.S("kode_parameter")), "Hasil dihapus.")),
                ];
                break;

            case LabKeslingStage.Verifikasi:
                title = "Data Verifikasi Pengujian Sampel Lab Kesling";
                hint = "➕ Verifikasi Baru memilih permintaan yang hasilnya sudah lengkap & belum diverifikasi.";
                _statuses = ["Semua", "Belum Divalidasi", "Sudah Divalidasi"];
                cols = [new("no_verifikasi", "No.Verifikasi"), new("no_permintaan", "No.Permintaan"), new("tanggal", "Tanggal"), new("nama_pelanggan", "Pelanggan"),
                    new("nama_sampel", "Sampel"), new("nama_pj", "PJ Verifikasi"), new("mulai_pengujian", "Mulai Uji"), new("selesai_pengujian", "Selesai Uji"),
                    new("catatan", "Catatan"), new("status", "Status")];
                _load = r.ListVerifikasiAsync;
                detailCols = [new("no_penugasan", "No.Penugasan"), new("kode_parameter", "Kode"), new("nama_parameter", "Parameter"), new("satuan", "Satuan"),
                    new("nilai_normal", "Nilai Normal"), new("hasil_pengujian", "Hasil"), new("keterangan", "Keterangan")];
                _loadDetail = x => r.DetailVerifikasiAsync(x.S("no_verifikasi"));
                actions =
                [
                    new("➕ Verifikasi Baru", async _ =>
                    {
                        var pick = await PickAsync("Pilih Permintaan untuk Diverifikasi", await r.SiapVerifikasiAsync(),
                            new Col("no_permintaan", "No.Permintaan"), new Col("waktu_diterima", "Diterima"), new Col("nama_pelanggan", "Pelanggan"),
                            new Col("nama_sampel", "Sampel"), new Col("jml_hasil", "Jml Hasil"));
                        if (pick is null) return;
                        var d = new VerifikasiDialog(pick);
                        await d.InitAsync();
                        if (await d.ShowForAsync(this)) await AfterAsync($"Verifikasi {d.NoVerifikasi} tersimpan.");
                    }, false),
                    new("🗑 Hapus Verifikasi", x => Confirm($"Hapus verifikasi {x!.S("no_verifikasi")}? Hasil kembali 'Belum Diverifikasi'.",
                        () => r.DeleteVerifikasiAsync(x.S("no_verifikasi")), "Verifikasi dihapus.")),
                ];
                break;

            case LabKeslingStage.Validasi:
                title = "Data Validasi Pengujian Sampel Lab Kesling";
                hint = "Validasi memposting jurnal pelayanan; hapus validasi memposting jurnal pembatalan.";
                _statuses = ["Semua", "Belum Bayar", "Sudah Bayar"];
                cols = [new("no_validasi", "No.Validasi"), new("no_permintaan", "No.Permintaan"), new("tanggal", "Tanggal"), new("nama_pelanggan", "Pelanggan"),
                    new("nama_sampel", "Sampel"), new("nama_pj", "PJ Lab"), new("nama_verifikator", "Verifikator"), new("total", "Total", Money: true),
                    new("catatan", "Catatan"), new("status", "Status")];
                _load = r.ListValidasiAsync;
                detailCols = [new("kode_parameter", "Kode"), new("nama_parameter", "Parameter"), new("satuan", "Satuan"), new("nilai_normal", "Normal"),
                    new("hasil_pengujian", "Hasil"), new("keterangan", "Ket"), new("nama_pjpengujian", "PJ Uji"), new("nama_analis", "Analis"),
                    new("jasa_sarana", "J.Sarana", Money: true), new("paket_bhp", "BHP", Money: true), new("jasa_pj_lab", "J.PJ Lab", Money: true),
                    new("jasa_pj_pengujian", "J.PJ Uji", Money: true), new("jasa_verifikator", "J.Verif", Money: true), new("jasa_petugas", "J.Analis", Money: true),
                    new("kso", "KSO", Money: true), new("jasa_menejemen", "J.Menej", Money: true), new("total", "Total", Money: true)];
                _loadDetail = x => r.DetailValidasiAsync(x.S("no_validasi"));
                actions =
                [
                    new("➕ Validasi Baru", async _ =>
                    {
                        var pick = await PickAsync("Pilih Verifikasi untuk Divalidasi", await r.SiapValidasiAsync(),
                            new Col("no_verifikasi", "No.Verifikasi"), new Col("no_permintaan", "No.Permintaan"), new Col("tanggal", "Tanggal"),
                            new Col("nama_pelanggan", "Pelanggan"), new Col("nama_sampel", "Sampel"), new Col("nama_verifikator", "Verifikator"));
                        if (pick is null) return;
                        var d = new ValidasiDialog(pick);
                        await d.InitAsync();
                        if (await d.ShowForAsync(this)) await AfterAsync($"Validasi {d.NoValidasi} tersimpan & jurnal diposting.");
                    }, false),
                    new("🗑 Hapus Validasi", x => Confirm($"Hapus validasi {x!.S("no_validasi")}? Jurnal pembatalan akan diposting.",
                        () => r.DeleteValidasiAsync(x.S("no_validasi"), AppSession.Kode), "Validasi dihapus, jurnal pembatalan diposting.")),
                ];
                break;

            case LabKeslingStage.Pembayaran:
                title = "Pembayaran Pengujian Sampel Lab Kesling";
                hint = "Daftar tagihan hasil validasi. Pilih lalu klik Bayar.";
                _statuses = ["Belum Bayar", "Sudah Bayar", "Semua"];
                cols = [new("no_permintaan", "No.Permintaan"), new("no_validasi", "No.Validasi"), new("tanggal", "Tgl Validasi"), new("nama_pelanggan", "Pelanggan"),
                    new("nama_sampel", "Sampel"), new("total", "Tagihan", Money: true), new("status", "Status")];
                _load = r.ListValidasiAsync;
                detailCols = [new("kode_parameter", "Kode"), new("nama_parameter", "Parameter"), new("hasil_pengujian", "Hasil"), new("total", "Tarif", Money: true)];
                _loadDetail = x => r.DetailValidasiAsync(x.S("no_validasi"));
                actions =
                [
                    new("💳 Bayar", async x =>
                    {
                        if (x!.S("status") != "Belum Bayar") { Ui.Err(StatusText, "Tagihan ini sudah dibayar."); return; }
                        var d = new BayarDialog(x);
                        await d.InitAsync();
                        if (await d.ShowForAsync(this)) await AfterAsync($"Pembayaran {d.NoBayar} tersimpan & jurnal diposting.");
                    }),
                ];
                break;

            case LabKeslingStage.RekapPelayanan:
                title = "Rekap Pelayanan Lab Kesling";
                hint = "Rincian jasa per parameter dari validasi dalam periode.";
                _statuses = ["Semua", "Belum Bayar", "Sudah Bayar"];
                cols = [new("no_validasi", "No.Validasi"), new("tanggal", "Tanggal"), new("no_permintaan", "No.Permintaan"), new("nama_pelanggan", "Pelanggan"),
                    new("nama_sampel", "Sampel"), new("nama_parameter", "Parameter"), new("nama_pjpengujian", "PJ Uji"), new("nama_analis", "Analis"),
                    new("jasa_sarana", "J.Sarana", Money: true), new("paket_bhp", "BHP", Money: true), new("jasa_pj_lab", "J.PJ Lab", Money: true),
                    new("jasa_pj_pengujian", "J.PJ Uji", Money: true), new("jasa_verifikator", "J.Verif", Money: true), new("jasa_petugas", "J.Analis", Money: true),
                    new("kso", "KSO", Money: true), new("jasa_menejemen", "J.Menej", Money: true), new("total", "Total", Money: true), new("status", "Status")];
                _load = async (f, t, s, c) => (await r.RekapPelayananAsync(f, t, c)).Where(x => s is null || x.S("status") == s).ToList();
                break;

            default: // RekapPembayaran
                title = "Rekap Pembayaran Lab Kesling";
                hint = "Hapus pembayaran memposting jurnal pembatalan dan mengembalikan tagihan ke 'Belum Bayar'.";
                _statuses = ["Semua"];
                cols = [new("no_pembayaran", "No.Bayar/Nota"), new("tanggal", "Tanggal"), new("no_permintaan", "No.Permintaan"), new("nama_pelanggan", "Pelanggan"),
                    new("nama_sampel", "Sampel"), new("dibayar_oleh", "Dibayar Oleh"), new("total", "Total", Money: true), new("ppn", "PPN", Money: true)];
                _load = (f, t, _, c) => r.ListPembayaranAsync(f, t, c);
                detailCols = [new("nama_bayar", "Cara Bayar"), new("kd_rek", "Rekening"), new("besar_bayar", "Bayar", Money: true), new("besarppn", "PPN", Money: true)];
                _loadDetail = x => r.DetailPembayaranAsync(x.S("no_pembayaran"));
                actions =
                [
                    new("🗑 Hapus Pembayaran", x => Confirm($"Batalkan pembayaran {x!.S("no_pembayaran")}? Jurnal pembatalan akan diposting.",
                        () => r.DeletePembayaranAsync(x.S("no_pembayaran"), AppSession.Kode), "Pembayaran dibatalkan.")),
                ];
                break;
        }

        // Unpaid bills can be old: open the payment list on a full year.
        if (stage == LabKeslingStage.Pembayaran) _from.SelectedDate = DateTime.Today.AddYears(-1);
        _status = Ui.Choice(_statuses);
        _status.MinWidth = 180;
        _grid = Ui.Grid(cols);
        _detail = Ui.Grid(detailCols);
        _grid.SelectionChanged += async (_, _) => await LoadDetailAsync();
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await LoadAsync(); };
        _status.SelectionChanged += async (_, _) => await LoadAsync();

        var buttons = actions.Select(a => Ui.Btn(a.Text, async (_, _) =>
        {
            var row = Ui.Selected<Row>(_grid);
            if (a.NeedsRow && row is null) { Ui.Err(StatusText, "Pilih data di tabel dulu."); return; }
            try { await a.Run(row); }
            catch (Exception ex) { Ui.Err(StatusText, ex.Message); }
        })).Cast<Control>().ToArray();

        var filter = Ui.Bar(Ui.L("Periode :"), _from, Ui.L("s.d."), _to, Ui.L("Status :"), _status, _cari, Ui.Btn("Tampilkan", async (_, _) => await LoadAsync()));
        var body = new Avalonia.Controls.Grid { RowDefinitions = new RowDefinitions(_loadDetail is null ? "Auto,Auto,*,Auto" : "Auto,Auto,2*,Auto,*") };
        void Put(Control c, int row) { Avalonia.Controls.Grid.SetRow(c, row); body.Children.Add(c); }
        Put(filter, 0);
        Put(Ui.Bar(buttons), 1);
        Put(_grid, 2);
        Put(_summary, 3);
        if (_loadDetail is not null) Put(_detail, 4);
        Build(title, body, hint);
    }

    public override Task ReloadAsync() => LoadAsync();

    private string? StatusFilter => _status.SelectedItem as string is { } s && s != "Semua" ? s : null;

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var rows = await _load(_from.SelectedDate ?? DateTime.Today, _to.SelectedDate ?? DateTime.Today, StatusFilter, _cari.Text);
        Ui.Fill(_grid, rows);
        _detail.ItemsSource = null;
        _summary.Text = rows.Count == 0 ? "Tidak ada data." :
            $"{rows.Count} data" + (rows.Count > 0 && rows[0]["total"] is not null ? $" · Total Rp {Ui.Money(rows.Sum(x => x.D("total")))}" : "");
    });

    private async Task LoadDetailAsync()
    {
        if (_loadDetail is null || Ui.Selected<Row>(_grid) is not { } row) return;
        try { Ui.Fill(_detail, await _loadDetail(row)); }
        catch (Exception ex) { Ui.Err(StatusText, ex.Message); }
    }

    private async Task AfterAsync(string message)
    {
        await LoadAsync();
        Ui.Ok(StatusText, message);
    }

    private async Task Keterangan(Row x, bool dapat)
    {
        if (x.S("status") != "Permintaan Baru") { Ui.Err(StatusText, "Permintaan ini sudah diproses."); return; }
        if (await new KeteranganDialog(x.S("no_permintaan"), dapat).ShowForAsync(this))
            await AfterAsync(dapat ? "Ditandai Dapat Dilayani." : "Ditandai Tidak Dapat Dilayani.");
    }

    private async Task Confirm(string question, Func<Task> action, string ok)
    {
        if (!await Dialogs.ConfirmAsync(this, "Konfirmasi", question)) return;
        await action();
        await AfterAsync(ok);
    }

    /// <summary>Modal picker over a list of rows; returns the chosen row or null.</summary>
    private async Task<Row?> PickAsync(string title, List<Row> rows, params Col[] cols)
    {
        if (rows.Count == 0) { Ui.Info(StatusText, "Tidak ada data yang siap diproses."); return null; }
        var grid = Ui.Grid(cols);
        grid.Height = 320;
        Ui.Fill(grid, rows);
        Row? chosen = null;
        var win = new Window { Title = title, Width = 760, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var ok = new Button { Content = "Pilih", MinWidth = 90 };
        var cancel = new Button { Content = "Batal", MinWidth = 90 };
        ok.Click += (_, _) => { chosen = Ui.Selected<Row>(grid); if (chosen is not null) win.Close(); };
        cancel.Click += (_, _) => win.Close();
        grid.DoubleTapped += (_, _) => { chosen = Ui.Selected<Row>(grid); if (chosen is not null) win.Close(); };
        win.Content = new StackPanel
        {
            Margin = new Thickness(16), Spacing = 8,
            Children = { grid, new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Children = { ok, cancel } } },
        };
        if (TopLevel.GetTopLevel(this) is Window owner) await win.ShowDialog(owner);
        return chosen;
    }
}
