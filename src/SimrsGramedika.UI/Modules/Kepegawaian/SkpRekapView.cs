using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Kepegawaian;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Kepegawaian;

/// <summary>
/// Legacy SKPRekapitulasiPenilaianPegawai: sums a pegawai's pengkajian in a period into "% Ya"
/// per sasaran (skp_pertama..skp_keenam) plus kesimpulan/rekomendasi, and links the pengkajian used.
/// </summary>
public class SkpRekapView : PageView
{
    private readonly SkpRepository _repo = new(DatabaseSettings.Load());
    private readonly MasterRepository _lookup = new(DatabaseSettings.Load());
    private readonly DataGrid _grid = Ui.Grid(new Col("nomor_rekapitulasi", "No.Rekap"), new Col("tanggal", "Tanggal"), new Col("dinilai", "Yang Dinilai"),
        new Col("penilai", "Penilai"), new Col("skp_pertama", "SKP 1"), new Col("skp_kedua", "SKP 2"), new Col("skp_ketiga", "SKP 3"),
        new Col("skp_keempat", "SKP 4"), new Col("skp_kelima", "SKP 5"), new Col("skp_keenam", "SKP 6"), new Col("kesimpulan", "Kesimpulan"));
    private readonly DataGrid _detail = Ui.Grid(new Col("nomor_penilaian", "No.Pengkajian"), new Col("tanggal", "Tanggal"), new Col("penilai", "Penilai"),
        new Col("keterangan", "Keterangan"), new Col("nilai", "Nilai (%)"));
    private readonly TextBox _cari = Ui.Text("Cari...");
    private readonly PeriodPicker _period = new(false, DateTime.Today.AddDays(1 - DateTime.Today.Day));
    private readonly PeriodPicker _periodeNilai = new(false, DateTime.Today.AddDays(1 - DateTime.Today.Day));
    private readonly TextBox _nomor = new() { IsReadOnly = true };
    private readonly DateTimeBox _tanggal = new(DateTime.Now);
    private readonly LookupBox _dinilai = new(), _penilai = new();
    private readonly TextBox[] _skp = Enumerable.Range(0, 6).Select(_ => new TextBox { MaxLength = 5, Width = 70 }).ToArray();
    private readonly TextBox _kesimpulan = Memo(), _rekomendasi = Memo();
    private List<string> _penilaian = [];
    private string _editing = "";

    private static TextBox Memo() => new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 56, MaxLength = 200 };

    public SkpRekapView()
    {
        _grid.SelectionChanged += async (_, _) => { if (Ui.Selected<Row>(_grid) is { } r) await EditAsync(r.S("nomor_rekapitulasi")); };
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await LoadAsync(); };
        _detail.Height = 150;

        var left = new DockPanel();
        var bar = Ui.Bar(_cari, _period, Ui.Btn("Cari", async (_, _) => await LoadAsync()));
        DockPanel.SetDock(bar, Dock.Top);
        left.Children.Add(bar);
        left.Children.Add(_grid);

        var skpGrid = new WrapPanel();
        for (var i = 0; i < 6; i++)
        {
            ToolTip.SetTip(_skp[i], SkpSasaran.Names[i]);
            skpGrid.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 8, 4), Children = { new TextBlock { Text = $"SKP {i + 1}", FontSize = 11 }, _skp[i] } });
        }

        var form = new StackPanel
        {
            Spacing = 6, Width = 520,
            Children =
            {
                Ui.Form(("No.Rekap :", _nomor), ("Tanggal :", _tanggal), ("Yang Dinilai :", _dinilai), ("Penilai :", _penilai)),
                Ui.Bar(Ui.L("Pengkajian"), _periodeNilai, Ui.Btn("Hitung", async (_, _) => await HitungAsync())),
                _detail,
                skpGrid,
                Ui.Form(("Kesimpulan :", _kesimpulan), ("Rekomendasi :", _rekomendasi)),
                Ui.Bar(Ui.Btn("Baru", async (_, _) => await NewAsync()), Ui.Btn("Simpan", async (_, _) => await SaveAsync()),
                    Ui.Btn("Hapus", async (_, _) => await DeleteAsync())),
            },
        };
        var body = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("*,16,Auto") };
        body.Children.Add(left);
        body.Children.Add(new ScrollViewer { Content = form, [Avalonia.Controls.Grid.ColumnProperty] = 2 });
        Build("Rekapitulasi Pengkajian SKP", body,
            "Pilih pegawai dan periode lalu klik Hitung: nilai SKP 1–6 = persentase \"Ya\" per sasaran dari semua pengkajian di periode itu (bisa dikoreksi sebelum disimpan).");
    }

    public override async Task ReloadAsync()
    {
        await RunAsync(async () =>
        {
            var pegawai = await _lookup.LookupAsync(MasterLookups.PegawaiNik);
            _dinilai.SetItems(pegawai);
            _penilai.SetItems(pegawai);
        });
        await LoadAsync();
        await NewAsync();
    }

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var rows = await _repo.ListRekapAsync(_cari.Text, _period.From, _period.To);
        Ui.Fill(_grid, rows);
        Ui.Info(StatusText, $"{rows.Count} rekapitulasi");
    });

    private async Task NewAsync()
    {
        _editing = "";
        _tanggal.Value = DateTime.Now;
        _dinilai.Kode = "";
        _penilai.Kode = SimrsGramedika.Core.Auth.AppSession.CurrentUser?.UserId ?? "";
        foreach (var t in _skp) t.Text = "";
        _kesimpulan.Text = _rekomendasi.Text = "";
        _penilaian = [];
        _detail.ItemsSource = null;
        _grid.SelectedItem = null;
        try { _nomor.Text = await _repo.NextNomorRekapAsync(_tanggal.Value) + " (otomatis)"; } catch { _nomor.Text = "(otomatis)"; }
    }

    private async Task HitungAsync() => await RunAsync(async () =>
    {
        if (_dinilai.Kode.Length == 0) throw new ArgumentException("Pilih pegawai yang dinilai.");
        var (list, skp) = await _repo.HitungAsync(_dinilai.Kode, _periodeNilai.From, _periodeNilai.To);
        Ui.Fill(_detail, list);
        _penilaian = list.Select(r => r.S("nomor_penilaian")).ToList();
        for (var i = 0; i < 6; i++) _skp[i].Text = skp[i];
        Ui.Info(StatusText, list.Count == 0 ? "Tidak ada pengkajian di periode itu." : $"{list.Count} pengkajian dihitung.");
    });

    private async Task EditAsync(string nomor) => await RunAsync(async () =>
    {
        if (await _repo.GetRekapAsync(nomor) is not { } r) return;
        _editing = nomor;
        _nomor.Text = nomor;
        _tanggal.Value = r.Tanggal;
        _dinilai.Kode = r.NikDinilai;
        _penilai.Kode = r.NikPenilai;
        for (var i = 0; i < 6; i++) _skp[i].Text = r.Skp[i];
        _kesimpulan.Text = r.Kesimpulan;
        _rekomendasi.Text = r.Rekomendasi;
        _penilaian = r.Penilaian.ToList();
        Ui.FillRows(_detail, _penilaian.Select(p => new GridRow([p, "", "", "", ""])));
    });

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        var no = await _repo.SaveRekapAsync(new SkpRekap(_editing, _tanggal.Value, _dinilai.Kode, _penilai.Kode,
            _skp.Select(t => (t.Text ?? "").Trim()).ToArray(), (_kesimpulan.Text ?? "").Trim(), (_rekomendasi.Text ?? "").Trim(), _penilaian));
        await LoadAsync();
        await NewAsync();
        Ui.Ok(StatusText, $"Rekapitulasi {no} tersimpan.");
    });

    private async Task DeleteAsync()
    {
        if (_editing.Length == 0) { Ui.Err(StatusText, "Pilih rekapitulasi yang akan dihapus."); return; }
        if (!await Dialogs.ConfirmAsync(this, "Hapus", $"Hapus rekapitulasi {_editing}?")) return;
        await RunAsync(async () =>
        {
            await _repo.DeleteRekapAsync(_editing);
            await LoadAsync();
            await NewAsync();
            Ui.Ok(StatusText, "Rekapitulasi dihapus.");
        });
    }
}
