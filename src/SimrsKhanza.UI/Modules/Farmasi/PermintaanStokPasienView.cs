using Avalonia.Controls;
using Avalonia.Input;
using SimrsKhanza.Config;
using SimrsKhanza.Data.Farmasi;
using SimrsKhanza.Data.Sql;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Farmasi;

/// <summary>
/// Legacy DlgDaftarPermintaanStokPasien: doctors' requests for inpatient bedside stock. Pharmacy picks a
/// request, chooses the depo, and validates it into stok_obat_pasien.
/// </summary>
public class PermintaanStokPasienView : PageView
{
    private readonly FarmasiStokRepository _repo = new(DatabaseSettings.Load());
    private readonly DataGrid _grid = Ui.Grid(new Col("no_permintaan", "No.Permintaan"), new Col("tgl_permintaan", "Tanggal"), new Col("jam", "Jam"),
        new Col("no_rawat", "No.Rawat"), new Col("no_rkm_medis", "No.RM"), new Col("nm_pasien", "Pasien"), new Col("kamar", "Kamar"),
        new Col("nm_dokter", "Dokter"), new Col("item", "Item"), new Col("status", "Status"), new Col("validasi", "Divalidasi"));
    private readonly DataGrid _detail = Ui.Grid(new Col("kode_brng", "Kode"), new Col("nama_brng", "Nama Barang"), new Col("jml", "Jumlah"),
        new Col("satuan", "Satuan"), new Col("aturan_pakai", "Aturan Pakai"), new Col("jadwal", "Jam Pemberian"));
    private readonly TextBox _cari = Ui.Text("Cari no / pasien / dokter / status...");
    private readonly PeriodPicker _period = new(false, DateTime.Today.AddDays(-7));
    private readonly LookupBox _depo = new();

    public PermintaanStokPasienView()
    {
        _grid.SelectionChanged += async (_, _) => await DetailAsync();
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await ReloadListAsync(); };
        _detail.Height = 200;
        var top = Ui.Bar(_cari, _period, Ui.Btn("🔎 Tampilkan", async (_, _) => await ReloadListAsync()));
        var bottom = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ui.Hint("Rincian permintaan terpilih:"),
                _detail,
                Ui.Bar(Ui.L("Ambil dari Depo"), _depo, Ui.Btn("✔ Validasi & Buat Stok Pasien", async (_, _) => await ValidasiAsync())),
            },
        };
        var body = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        body.Children.Add(top);
        body.Children.Add(bottom);
        body.Children.Add(_grid);
        Build("Permintaan Stok Obat Pasien", body,
            "Validasi mengambil stok dari depo terpilih (batch kadaluarsa terdekat dulu), membuat Stok Obat Pasien dengan jadwal jam dari dokter, dan menandai permintaan \"Sudah\".");
    }

    public override async Task ReloadAsync()
    {
        await RunAsync(async () => _depo.SetItems(await _repo.LokasiAsync()));
        await ReloadListAsync();
    }

    private async Task ReloadListAsync() => await RunAsync(async () =>
    {
        var rows = await _repo.PermintaanStokPasienAsync(_period.From, _period.To, _cari.Text);
        Ui.Fill(_grid, rows);
        Ui.Info(StatusText, $"{rows.Count} permintaan, {rows.Count(r => r.S("status") == "Belum")} belum divalidasi");
    });

    private async Task DetailAsync()
    {
        if (Ui.Selected<Row>(_grid) is not { } r) { _detail.ItemsSource = null; return; }
        await RunAsync(async () => Ui.Fill(_detail, await _repo.DetailStokPasienAsync(r.S("no_permintaan"))));
    }

    private async Task ValidasiAsync()
    {
        if (Ui.Selected<Row>(_grid) is not { } r) { Ui.Err(StatusText, "Pilih permintaan dulu."); return; }
        if (r.S("status") == "Sudah") { Ui.Err(StatusText, "Permintaan ini sudah divalidasi."); return; }
        if (_depo.Kode.Length == 0) { Ui.Err(StatusText, "Pilih depo dulu."); return; }
        if (!await Dialogs.ConfirmAsync(this, "Validasi", $"Validasi permintaan {r.S("no_permintaan")} untuk {r.S("nm_pasien")} dari {_depo.Nama}?")) return;
        await RunAsync(async () =>
        {
            await _repo.ValidasiStokPasienAsync(r.S("no_permintaan"), _depo.Kode, SimrsKhanza.Core.Auth.AppSession.CurrentUser?.KodeLegacy ?? "");
            await ReloadListAsync();
            Ui.Ok(StatusText, $"Permintaan {r.S("no_permintaan")} divalidasi; stok obat pasien dibuat.");
        });
    }
}
