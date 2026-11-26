using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Kepegawaian;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Kepegawaian;

/// <summary>Legacy SKPPenilaianPegawai: one pengkajian = header + Ya/Tidak per kriteria SKP.</summary>
public class SkpPenilaianView : PageView
{
    private readonly SkpRepository _repo = new(DatabaseSettings.Load());
    private readonly MasterRepository _lookup = new(DatabaseSettings.Load());
    private readonly DataGrid _grid = Ui.Grid(new Col("nomor_penilaian", "No.Pengkajian"), new Col("tanggal", "Tanggal"), new Col("dinilai", "Yang Dinilai"),
        new Col("penilai", "Penilai"), new Col("keterangan", "Keterangan"), new Col("status", "Status"), new Col("nilai", "Nilai (%)"));
    private readonly TextBox _cari = Ui.Text("Cari...");
    private readonly PeriodPicker _period = new(false, DateTime.Today.AddDays(1 - DateTime.Today.Day));
    private readonly TextBox _nomor = new() { IsReadOnly = true };
    private readonly DateTimeBox _tanggal = new(DateTime.Now);
    private readonly LookupBox _dinilai = new(), _penilai = new();
    private readonly TextBox _ket = Ui.Text(maxLen: 100);
    private readonly ComboBox _status = Ui.Choice(SkpRepository.StatusPenilaian);
    private readonly StackPanel _kriteriaPanel = new() { Spacing = 2 };
    private readonly TextBlock _skor = new() { FontWeight = FontWeight.Bold };
    private readonly List<(SkpKriteria K, ComboBox Jawab)> _kriteria = [];
    private string _editing = "";

    public SkpPenilaianView()
    {
        _grid.SelectionChanged += async (_, _) => { if (Ui.Selected<Row>(_grid) is { } r) await EditAsync(r.S("nomor_penilaian")); };
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await LoadAsync(); };

        var isian = Ui.FormColumns(
        [
            ("No.Pengkajian :", _nomor), ("Tanggal :", _tanggal), ("Yang Dinilai :", _dinilai), ("Penilai :", _penilai),
            ("Keterangan :", _ket), ("Status :", _status), ("Nilai :", _skor),
        ], 2);
        var form = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                isian,
                Ui.Bar(Ui.Btn("Semua Ya", (_, _) => SetAll("Ya")), Ui.Btn("Kosongkan", (_, _) => SetAll(""))),
                Ui.Box(new ScrollViewer { Content = _kriteriaPanel, Height = 240, Padding = new Thickness(6) }),
            },
        };
        var (panel, input) = Ui.InputPanel(new ScrollViewer { Content = form, MaxHeight = 330, Padding = new Thickness(0, 0, 24, 0) });
        var simpan = Ui.Btn("Simpan", async (_, _) => await SaveAsync(), enabled: false);
        simpan.Classes.Add("accent");
        input.IsCheckedChanged += (_, _) => simpan.IsEnabled = input.IsChecked == true;
        var body = new Avalonia.Controls.Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 6 };
        Avalonia.Controls.Grid.SetRow(_grid, 1);
        body.Children.Add(panel);
        body.Children.Add(_grid);
        var bawah = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Bar(_cari, _period, Ui.Btn("Cari", async (_, _) => await LoadAsync())),
                Ui.Bar(Ui.Btn("Baru", async (_, _) => { input.IsChecked = true; await NewAsync(); }), simpan,
                    Ui.Btn("Hapus", async (_, _) => await DeleteAsync())),
            },
        };
        Build("Pengkajian SKP Petugas/Dokter", body, "Pengkajian implementasi Sasaran Keselamatan Pasien. " + SkpSasaran.Hint, bawah);
    }

    public override async Task ReloadAsync()
    {
        await RunAsync(async () =>
        {
            var pegawai = await _lookup.LookupAsync(MasterLookups.PegawaiNik);
            _dinilai.SetItems(pegawai);
            _penilai.SetItems(pegawai);
            BuildKriteria(await _repo.KriteriaAsync());
        });
        await LoadAsync();
        await NewAsync();
    }

    private void BuildKriteria(List<SkpKriteria> list)
    {
        _kriteria.Clear();
        _kriteriaPanel.Children.Clear();
        var sasaran = 0;
        foreach (var k in list)
        {
            if (k.Sasaran != sasaran)
            {
                sasaran = k.Sasaran;
                var name = sasaran is >= 1 and <= 6 ? SkpSasaran.Names[sasaran - 1] : "";
                _kriteriaPanel.Children.Add(new TextBlock { Text = $"Sasaran {sasaran} — {name}", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 2) });
            }
            var jawab = Ui.Choice(["", "Ya", "Tidak"]);
            jawab.MinWidth = 90;
            jawab.SelectionChanged += (_, _) => UpdateSkor();
            var row = new DockPanel { Margin = new Thickness(0, 1) };
            DockPanel.SetDock(jawab, Dock.Right);
            row.Children.Add(jawab);
            row.Children.Add(new TextBlock { Text = $"{k.Kode}  {k.Nama}  ({k.Kategori})", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            _kriteriaPanel.Children.Add(row);
            _kriteria.Add((k, jawab));
        }
        if (list.Count == 0) _kriteriaPanel.Children.Add(Ui.Hint("Belum ada kriteria. Isi menu Kategori & Kriteria Pengkajian SKP dulu."));
    }

    private void SetAll(string v)
    {
        foreach (var (_, j) in _kriteria) j.SelectedItem = v;
    }

    private void UpdateSkor()
    {
        var n = _kriteria.Count(k => k.Jawab.SelectedItem is "Ya" or "Tidak");
        _skor.Text = n == 0 ? "-" : $"{_kriteria.Count(k => k.Jawab.SelectedItem is "Ya") * 100.0 / n:0.##} %  ({n} kriteria dinilai)";
    }

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var rows = await _repo.ListPenilaianAsync(_cari.Text, _period.From, _period.To);
        Ui.Fill(_grid, rows);
        Ui.Info(StatusText, $"{rows.Count} pengkajian");
    });

    private async Task NewAsync()
    {
        _editing = "";
        _tanggal.Value = DateTime.Now;
        _dinilai.Kode = "";
        _penilai.Kode = AppSessionNik();
        _ket.Text = "";
        _status.SelectedIndex = 0;
        SetAll("");
        _grid.SelectedItem = null;
        try { _nomor.Text = await _repo.NextNomorPenilaianAsync(_tanggal.Value) + " (otomatis)"; } catch { _nomor.Text = "(otomatis)"; }
        UpdateSkor();
    }

    private static string AppSessionNik() => SimrsGramedika.Core.Auth.AppSession.CurrentUser?.UserId ?? "";

    private async Task EditAsync(string nomor) => await RunAsync(async () =>
    {
        if (await _repo.GetPenilaianAsync(nomor) is not { } p) return;
        _editing = nomor;
        _nomor.Text = nomor;
        _tanggal.Value = p.Tanggal;
        _dinilai.Kode = p.NikDinilai;
        _penilai.Kode = p.NikPenilai;
        _ket.Text = p.Keterangan;
        _status.SelectedItem = SkpRepository.StatusPenilaian.Contains(p.Status) ? p.Status : SkpRepository.StatusPenilaian[0];
        foreach (var (k, j) in _kriteria) j.SelectedItem = p.Jawaban.TryGetValue(k.Kode, out var v) ? v : "";
        UpdateSkor();
    });

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        var jawaban = _kriteria.Where(k => k.Jawab.SelectedItem is "Ya" or "Tidak").ToDictionary(k => k.K.Kode, k => (string)k.Jawab.SelectedItem!);
        var no = await _repo.SavePenilaianAsync(new SkpPenilaian(_editing, _tanggal.Value, _dinilai.Kode, _penilai.Kode,
            (_ket.Text ?? "").Trim(), _status.SelectedItem as string ?? SkpRepository.StatusPenilaian[0], jawaban));
        await LoadAsync();
        await NewAsync();
        Ui.Ok(StatusText, $"Pengkajian {no} tersimpan.");
    });

    private async Task DeleteAsync()
    {
        if (_editing.Length == 0) { Ui.Err(StatusText, "Pilih pengkajian yang akan dihapus."); return; }
        if (!await Dialogs.ConfirmAsync(this, "Hapus", $"Hapus pengkajian {_editing}?")) return;
        await RunAsync(async () =>
        {
            await _repo.DeletePenilaianAsync(_editing);
            await LoadAsync();
            await NewAsync();
            Ui.Ok(StatusText, "Pengkajian dihapus.");
        });
    }
}
