using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Laboratorium;

public enum LabRowKind { Header, CaraBayar, Pemeriksaan, Item, Ringkasan, Total }

/// <summary>One line of the nested PK/MB grid — columns are reused by row kind exactly like the legacy table model.</summary>
public class LabDisplayRow
{
    public LabRowKind Kind { get; init; }
    public LabPeriksaKey? Key { get; init; }
    public string C0 { get; init; } = "";
    public string C1 { get; init; } = "";
    public string C2 { get; init; } = "";
    public string C3 { get; init; } = "";
    public string C4 { get; init; } = "";
    public string C5 { get; init; } = "";
    public string C6 { get; init; } = "";
    public string[] Cells => [C0, C1, C2, C3, C4, C5, C6];
}

public class LabItemRow
{
    public string NoRawat { get; init; } = "";
    public string Pasien { get; init; } = "";
    public string Tgl { get; init; } = "";
    public string Jam { get; init; } = "";
    public string Pemeriksaan { get; init; } = "";
    public string Item { get; init; } = "";
    public string Hasil { get; init; } = "";
    public string Satuan { get; init; } = "";
    public string NilaiRujukan { get; init; } = "";
    public string Keterangan { get; init; } = "";
    public string Ruang { get; init; } = "";
    public string Petugas { get; init; } = "";
    public string DokterPerujuk { get; init; } = "";
    public string DokterPj { get; init; } = "";
    public string Metode { get; init; } = "";
}

public partial class PeriksaLabListView : UserControl
{
    private readonly LabCategory _category;
    private bool _ready;
    private List<LabPeriksaGroup> _groups = new();
    private List<LabDisplayRow> _rows = new();
    private IReadOnlyList<LabPeriksaPaRow> _pa = Array.Empty<LabPeriksaPaRow>();
    private bool _petugasLoaded;

    public event EventHandler? BackRequested;

    /// <summary>"Periksa Baru" — arg is the selected/filtered no_rawat (or null) to prefill the entry form.</summary>
    public event EventHandler<string?>? InputRequested;

    public PeriksaLabListView(LabCategory category)
    {
        _category = category;
        InitializeComponent();

        this.FindControl<TextBlock>("TitleText")!.Text = $":: [ Data Pemeriksaan Laboratorium {LabUi.Title(category)} ] ::";
        var pa = category == LabCategory.PA;
        this.FindControl<DataGrid>("NestedGrid")!.IsVisible = !pa;
        this.FindControl<DataGrid>("PaGrid")!.IsVisible = pa;
        this.FindControl<TabItem>("ItemTab")!.IsVisible = !pa;
        this.FindControl<Border>("KesanPanel")!.IsVisible = !pa;
        this.FindControl<Border>("PaPanel")!.IsVisible = pa;

        // Default period = today (perf lesson from Registrasi: never default to a wide range on lab tables).
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<DataGrid>("NestedGrid")!.LoadingRow += OnLoadingRow;
        _ready = true;
    }

    // Avoids "no parameterless ctor" for tools/designers; the app always passes a category.
    public PeriksaLabListView() : this(LabCategory.PK) { }

    private static readonly IBrush HeaderBrush = Brush.Parse("#331E3A8A");
    private static readonly IBrush SubBrush = Brush.Parse("#22888888");
    private static readonly IBrush SummaryBrush = Brush.Parse("#2200A86B");

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not LabDisplayRow r) return;
        e.Row.Background = r.Kind switch
        {
            LabRowKind.Header => HeaderBrush,
            LabRowKind.CaraBayar => SubBrush,
            LabRowKind.Ringkasan => SummaryBrush,
            LabRowKind.Total => SummaryBrush,
            _ => Brushes.Transparent,
        };
        e.Row.FontWeight = r.Kind is LabRowKind.Header or LabRowKind.Pemeriksaan or LabRowKind.Total ? FontWeight.Bold : FontWeight.Normal;
    }

    private LabPeriksaFilter BuildFilter()
    {
        string? T(string name) { var t = this.FindControl<TextBox>(name)!.Text?.Trim(); return string.IsNullOrEmpty(t) ? null : t; }
        var petugas = this.FindControl<ComboBox>("PetugasCombo")!.SelectedItem as LabPick;
        return new LabPeriksaFilter(
            DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate ?? DateTime.Today),
            DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate ?? DateTime.Today),
            T("NoRawatBox"), T("NoRmBox"), string.IsNullOrEmpty(petugas?.Kode) ? null : petugas.Kode, T("KeywordBox"));
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new LabPeriksaRepository(db);

            if (!_petugasLoaded)
            {
                var list = new List<LabPick> { new() { Kode = "", Nama = "(Semua)" } };
                list.AddRange(await repo.GetPetugasAsync());
                var combo = this.FindControl<ComboBox>("PetugasCombo")!;
                combo.ItemsSource = list;
                combo.SelectedIndex = 0;
                _petugasLoaded = true;
            }

            var filter = BuildFilter();
            if (_category == LabCategory.PA)
            {
                _pa = await repo.GetPeriksaPaAsync(filter);
                this.FindControl<DataGrid>("PaGrid")!.ItemsSource = _pa;
                this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {_pa.Count}";
                this.FindControl<TextBlock>("TotalText")!.Text = $"Total : {_pa.Sum(p => p.Biaya):N0}";
            }
            else
            {
                _groups = (await repo.GetPeriksaGroupsAsync(_category, filter)).ToList();
                _rows = BuildRows(_groups);
                this.FindControl<DataGrid>("NestedGrid")!.ItemsSource = _rows;
                this.FindControl<DataGrid>("ItemGrid")!.ItemsSource = BuildItemRows(_groups);
                this.FindControl<TextBlock>("RecordText")!.Text = $"Record : {_groups.Count}";
                this.FindControl<TextBlock>("TotalText")!.Text = $"Total : {_groups.Sum(g => g.Total):N0}";
            }
            ClearEditors();
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal memuat data pemeriksaan: {ex.Message}", isError: true);
        }
    }

    private static List<LabDisplayRow> BuildRows(IReadOnlyList<LabPeriksaGroup> groups)
    {
        var rows = new List<LabDisplayRow>();
        decimal ttl = 0;
        foreach (var g in groups)
        {
            rows.Add(new LabDisplayRow
            {
                Kind = LabRowKind.Header, Key = g.Key, C0 = g.Key.NoRawat, C1 = $"{g.NoRm} {g.NamaPasien} ({g.KamarPoli})",
                C2 = g.Petugas, C3 = g.Key.Tgl, C4 = g.Key.Jam, C5 = g.DokterPerujuk, C6 = g.DokterPj,
            });
            rows.Add(new LabDisplayRow
            {
                Kind = LabRowKind.CaraBayar, Key = g.Key, C0 = "Cara Bayar : " + g.CaraBayar, C1 = "Pemeriksaan", C2 = "Hasil",
                C3 = "Metode", C4 = "Satuan", C5 = "Nilai Rujukan", C6 = "Keterangan",
            });
            decimal item = 0;
            foreach (var p in g.Pemeriksaan)
            {
                item += p.Biaya;
                rows.Add(new LabDisplayRow { Kind = LabRowKind.Pemeriksaan, Key = g.Key, C1 = $"{p.KdJenisPrw} {p.Nama} {p.Biaya:N0}" });
                foreach (var i in p.Items)
                {
                    item += i.BiayaItem;
                    rows.Add(new LabDisplayRow
                    {
                        Kind = LabRowKind.Item, Key = g.Key, C1 = $"  {i.Pemeriksaan} {i.BiayaItem:N0}", C2 = i.Hasil,
                        C3 = i.Metode, C4 = i.Satuan, C5 = i.NilaiRujukan, C6 = i.Keterangan,
                    });
                }
            }
            ttl += item;
            if (item > 0)
                rows.Add(new LabDisplayRow { Kind = LabRowKind.Ringkasan, Key = g.Key, C2 = $"Biaya Periksa : {item:N0}", C5 = "Kesan : " + g.Kesan, C6 = "Saran : " + g.Saran });
        }
        if (ttl > 0) rows.Add(new LabDisplayRow { Kind = LabRowKind.Total, C0 = ">>", C1 = $"Total : {ttl:N0}" });
        return rows;
    }

    private static List<LabItemRow> BuildItemRows(IReadOnlyList<LabPeriksaGroup> groups)
        => groups.SelectMany(g => g.Pemeriksaan.SelectMany(p => p.Items.Select(i => new LabItemRow
        {
            NoRawat = g.Key.NoRawat, Pasien = $"{g.NoRm} {g.NamaPasien} ({g.CaraBayar})", Tgl = g.Key.Tgl, Jam = g.Key.Jam,
            Pemeriksaan = p.Nama, Item = i.Pemeriksaan, Hasil = i.Hasil, Satuan = i.Satuan, NilaiRujukan = i.NilaiRujukan,
            Keterangan = i.Keterangan, Ruang = g.Ruang, Petugas = g.Petugas, DokterPerujuk = g.DokterPerujuk, DokterPj = g.DokterPj, Metode = i.Metode,
        }))).ToList();

    // ---- selection / editors ----------------------------------------------------------------

    private LabPeriksaGroup? SelectedGroup()
    {
        if (_category == LabCategory.PA) return null;
        var key = (this.FindControl<DataGrid>("NestedGrid")!.SelectedItem as LabDisplayRow)?.Key;
        return key is null ? null : _groups.FirstOrDefault(g => g.Key == key);
    }

    private LabPeriksaPaRow? SelectedPa() => this.FindControl<DataGrid>("PaGrid")!.SelectedItem as LabPeriksaPaRow;

    private void ClearEditors()
    {
        foreach (var n in new[] { "KesanBox", "SaranBox", "PaDiagnosaBox", "PaMakroBox", "PaMikroBox", "PaKesimpulanBox", "PaKesanBox" })
            this.FindControl<TextBox>(n)!.Text = string.Empty;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (_category == LabCategory.PA)
        {
            var r = SelectedPa();
            this.FindControl<TextBox>("PaDiagnosaBox")!.Text = r?.DiagnosaKlinik;
            this.FindControl<TextBox>("PaMakroBox")!.Text = r?.Makroskopik;
            this.FindControl<TextBox>("PaMikroBox")!.Text = r?.Mikroskopik;
            this.FindControl<TextBox>("PaKesimpulanBox")!.Text = r?.Kesimpulan;
            this.FindControl<TextBox>("PaKesanBox")!.Text = r?.Kesan;
        }
        else
        {
            var g = SelectedGroup();
            this.FindControl<TextBox>("KesanBox")!.Text = g?.Kesan;
            this.FindControl<TextBox>("SaranBox")!.Text = g?.Saran;
        }
    }

    private async void OnUpdateKesanClick(object? sender, RoutedEventArgs e)
    {
        var g = SelectedGroup();
        if (g is null) { ShowStatus("Pilih dulu data pemeriksaan di tabel.", isError: true); return; }
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new LabPeriksaRepository(db).SaveKesanSaranAsync(g.Key,
                this.FindControl<TextBox>("SaranBox")!.Text?.Trim() ?? "", this.FindControl<TextBox>("KesanBox")!.Text?.Trim() ?? "");
            var key = g.Key;
            await ReloadAsync();
            ShowStatus("Proses update kesan & saran selesai.", isError: false);
            var idx = _rows.FindIndex(r => r.Key == key);
            if (idx >= 0) this.FindControl<DataGrid>("NestedGrid")!.SelectedIndex = idx;
        }
        catch (Exception ex) { ShowStatus($"Gagal update: {ex.Message}", isError: true); }
    }

    private async void OnUpdatePaClick(object? sender, RoutedEventArgs e)
    {
        var r = SelectedPa();
        if (r is null) { ShowStatus("Pilih dulu data pemeriksaan di tabel.", isError: true); return; }
        string T(string n) => this.FindControl<TextBox>(n)!.Text ?? "";
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new LabPeriksaRepository(db).UpdatePaHasilAsync(r, T("PaDiagnosaBox"), T("PaMakroBox"), T("PaMikroBox"), T("PaKesimpulanBox"), T("PaKesanBox"));
            await ReloadAsync();
            ShowStatus("Proses update berhasil.", isError: false);
        }
        catch (Exception ex) { ShowStatus($"Proses update gagal: {ex.Message}", isError: true); }
    }

    // ---- toolbar ----------------------------------------------------------------------------

    private async void OnFilterKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await ReloadAsync();
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnSemuaClick(object? sender, RoutedEventArgs e)
    {
        foreach (var n in new[] { "NoRawatBox", "NoRmBox", "KeywordBox" }) this.FindControl<TextBox>(n)!.Text = string.Empty;
        this.FindControl<ComboBox>("PetugasCombo")!.SelectedIndex = 0;
        this.FindControl<CalendarDatePicker>("FromPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("ToPicker")!.SelectedDate = DateTime.Today;
        await ReloadAsync();
    }

    private void OnInputClick(object? sender, RoutedEventArgs e)
    {
        var noRawat = _category == LabCategory.PA ? SelectedPa()?.NoRawat : SelectedGroup()?.Key.NoRawat;
        if (string.IsNullOrEmpty(noRawat)) noRawat = this.FindControl<TextBox>("NoRawatBox")!.Text?.Trim();
        InputRequested?.Invoke(this, string.IsNullOrEmpty(noRawat) ? null : noRawat);
    }

    private async void OnHapusClick(object? sender, RoutedEventArgs e)
    {
        LabPeriksaKey? key; string? kd = null; string who;
        if (_category == LabCategory.PA)
        {
            var r = SelectedPa();
            if (r is null) { ShowStatus("Pilih dulu data yang mau dihapus (klik barisnya di tabel).", isError: true); return; }
            key = r.Key; kd = r.KdJenisPrw; who = $"{r.NoRawat} — {r.Pemeriksaan} ({r.Tgl} {r.Jam})";
        }
        else
        {
            var g = SelectedGroup();
            if (g is null) { ShowStatus("Pilih dulu data yang mau dihapus (klik barisnya di tabel).", isError: true); return; }
            key = g.Key; who = $"{g.Key.NoRawat} — {g.NamaPasien} ({g.Key.Tgl} {g.Key.Jam}, {g.Pemeriksaan.Count} pemeriksaan)";
        }

        if (!await LabUi.ConfirmAsync(this, $"Yakin mau menghapus data pemeriksaan ini?\n{who}")) return;
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new LabPeriksaRepository(db).DeleteAsync(_category, key, kd);
            await ReloadAsync();
            ShowStatus("Data pemeriksaan dihapus.", isError: false);
        }
        catch (Exception ex) { ShowStatus(ex.Message, isError: true); }
    }

    private void OnCetakClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path;
            var title = $"Data Pemeriksaan Laboratorium {LabUi.Title(_category)}";
            if (_category == LabCategory.PA)
            {
                if (_pa.Count == 0) { ShowStatus("Tidak ada data yang bisa dicetak.", isError: true); return; }
                path = PeriksaLabReport.GeneratePdf(title,
                    ["No.Rawat", "Pasien", "Petugas", "Tgl", "Jam", "Dokter Perujuk", "Penanggung Jawab", "Pemeriksaan", "Biaya", "Diagnosa Klinis", "Makroskopik", "Mikroskopik", "Kesimpulan", "Kesan"],
                    _pa.Select(p => new[] { p.NoRawat, p.PasienText, p.Petugas, p.Tgl, p.Jam, p.DokterPerujuk, p.DokterPj, p.Pemeriksaan, p.Biaya.ToString("N0"), p.DiagnosaKlinik, p.Makroskopik, p.Mikroskopik, p.Kesimpulan, p.Kesan }).ToList(),
                    $"{_pa.Count} data");
            }
            else
            {
                if (_rows.Count == 0) { ShowStatus("Tidak ada data yang bisa dicetak.", isError: true); return; }
                path = PeriksaLabReport.GeneratePdf(title,
                    ["No.Rawat", "Pasien / Pemeriksaan", "Petugas / Hasil", "Tgl / Metode", "Jam / Satuan", "Dokter Perujuk / Nilai Rujukan", "Penanggung Jawab / Keterangan"],
                    _rows.Select(r => r.Cells).ToList(), $"{_groups.Count} data");
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowStatus($"Gagal membuat/membuka PDF: {ex.Message}", isError: true); }
    }

    private void ShowStatus(string message, bool isError)
    {
        var s = this.FindControl<TextBlock>("StatusText")!;
        s.Foreground = isError ? Brush.Parse("#DC2626") : Brushes.Green;
        s.Text = message;
        s.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
