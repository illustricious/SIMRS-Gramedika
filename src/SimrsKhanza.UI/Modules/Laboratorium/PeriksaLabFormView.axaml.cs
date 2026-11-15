using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SimrsKhanza.Config;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Laboratorium;

public abstract class ObservableRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

/// <summary>A jns_perawatan_lab row in the left list with its "P" tick.</summary>
public class TarifPick(LabTarifRow tarif) : ObservableRow
{
    public LabTarifRow Tarif { get; } = tarif;
    public string Kode => Tarif.KdJenisPrw;
    public string Nama => Tarif.Nama;
    public string BiayaText => Tarif.Biaya.ToString("N0");
    private bool _dipilih;
    public bool Dipilih { get => _dipilih; set => Set(ref _dipilih, value); }
}

/// <summary>One line of the PK/MB detail grid: either a pemeriksaan header or a template item to fill in.</summary>
public class LabDetailRow : ObservableRow
{
    public bool IsHeader { get; init; }
    public bool IsItem => !IsHeader;
    public string KdJenisPrw { get; init; } = "";
    public LabTemplateRow? Template { get; init; }
    public string Nama { get; init; } = "";
    public string Satuan { get; init; } = "";
    public string Metode { get; init; } = "";
    public string BiayaText => Template is null ? "" : Template.BiayaItem.ToString("N0");

    private bool _dipilih;
    public bool Dipilih { get => _dipilih; set => Set(ref _dipilih, value); }

    private string _hasil = "";
    // Legacy ticks every row that has a result typed in (at save time); doing it live keeps the checkbox honest.
    public string Hasil
    {
        get => _hasil;
        set { if (Set(ref _hasil, value ?? "") && !string.IsNullOrEmpty(_hasil)) Dipilih = true; }
    }

    private string _nilaiRujukan = "";
    public string NilaiRujukan { get => _nilaiRujukan; set => Set(ref _nilaiRujukan, value ?? ""); }

    /// <summary>The value we last filled automatically — lets a Dewasa/Anak switch refresh only untouched cells.</summary>
    public string AutoRujukan { get; set; } = "";

    private string _keterangan = "";
    public string Keterangan { get => _keterangan; set => Set(ref _keterangan, value ?? ""); }
}

/// <summary>One PA pemeriksaan: PA results are free-text narrative (detail_periksa_labpa), not template items.</summary>
public class PaRow
{
    public string KdJenisPrw { get; init; } = "";
    public string Nama { get; init; } = "";
    public string DiagnosaKlinik { get; set; } = "";
    public string Makroskopik { get; set; } = "";
    public string Mikroskopik { get; set; } = "";
    public string Kesimpulan { get; set; } = "";
    public string Kesan { get; set; } = "";
}

public partial class PeriksaLabFormView : UserControl
{
    private readonly LabCategory _category;
    private bool _ready, _syncing, _syncAgain, _suppressSync, _lookupsLoaded;
    private LabRegInfo? _reg;
    private string? _noOrder;
    private HashSet<int>? _orderTemplates;
    private IReadOnlyList<LabPick> _dokter = Array.Empty<LabPick>();
    private IReadOnlyList<LabPick> _petugas = Array.Empty<LabPick>();
    private string _defaultPj = "";
    private readonly ObservableCollection<TarifPick> _tarif = new();
    private readonly ObservableCollection<LabDetailRow> _detail = new();
    private readonly ObservableCollection<PaRow> _pa = new();

    public event EventHandler? BackRequested;

    /// <summary>Raised after a successful Simpan so the parent can go back to the list.</summary>
    public event EventHandler? SavedRequested;

    public PeriksaLabFormView(LabCategory category)
    {
        _category = category;
        InitializeComponent();

        var pa = category == LabCategory.PA;
        this.FindControl<TextBlock>("TitleText")!.Text = $":: [ Input Data Hasil Periksa Laboratorium {LabUi.Title(category)} ] ::";
        this.FindControl<DataGrid>("DetailGrid")!.IsVisible = !pa;
        this.FindControl<DataGrid>("PaDetailGrid")!.IsVisible = pa;
        this.FindControl<RadioButton>("RbDewasa")!.IsVisible = !pa;
        this.FindControl<RadioButton>("RbAnak")!.IsVisible = !pa;

        this.FindControl<DataGrid>("TarifGrid")!.ItemsSource = _tarif;
        this.FindControl<DataGrid>("DetailGrid")!.ItemsSource = _detail;
        this.FindControl<DataGrid>("PaDetailGrid")!.ItemsSource = _pa;
        this.FindControl<DataGrid>("DetailGrid")!.LoadingRow += OnDetailLoadingRow;

        this.FindControl<CalendarDatePicker>("TanggalPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<TextBox>("JamBox")!.Text = DateTime.Now.ToString("HH:mm:ss");
        _ready = true;
    }

    public PeriksaLabFormView() : this(LabCategory.PK) { }

    private static readonly IBrush HeaderBrush = Brush.Parse("#331E3A8A");

    private void OnDetailLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        var header = (e.Row.DataContext as LabDetailRow)?.IsHeader == true;
        e.Row.Background = header ? HeaderBrush : Brushes.Transparent;
        e.Row.FontWeight = header ? FontWeight.Bold : FontWeight.Normal;
    }

    // ---- loading ----------------------------------------------------------------------------

    /// <summary>Parent calls this when the form is shown without a prefill (loads petugas / default Dokter P.J.).</summary>
    public async System.Threading.Tasks.Task ReloadAsync() => await EnsureLookupsAsync();

    private async System.Threading.Tasks.Task EnsureLookupsAsync(params string[] extraDokter)
    {
        using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
        var repo = new LabPeriksaRepository(db);
        if (!_lookupsLoaded)
        {
            _petugas = await repo.GetPetugasAsync();
            _defaultPj = await repo.GetDefaultDokterPjAsync(_category);
            this.FindControl<ComboBox>("PetugasCombo")!.ItemsSource = _petugas;
            _lookupsLoaded = true;
        }
        _dokter = await repo.GetDokterAsync(extraDokter.Append(_defaultPj).ToList());
        this.FindControl<ComboBox>("PerujukCombo")!.ItemsSource = _dokter;
        this.FindControl<ComboBox>("PjCombo")!.ItemsSource = _dokter;
        SelectPick("PjCombo", _defaultPj);
    }

    private void SelectPick(string comboName, string? code)
    {
        var combo = this.FindControl<ComboBox>(comboName)!;
        var list = combo.ItemsSource as IEnumerable<LabPick>;
        combo.SelectedItem = list?.FirstOrDefault(p => p.Kode == code);
    }

    /// <summary>
    /// Entry point from the permintaan screen's "Hasil" button: loads the visit and preloads exactly
    /// what the order asked for (pemeriksaan ticked; for PK/MB only the requested template items).
    /// </summary>
    public async System.Threading.Tasks.Task PrefillAsync(string noRawat, string? noOrder)
    {
        this.FindControl<TextBox>("NoRawatBox")!.Text = noRawat;
        await LoadVisitAsync(noRawat, string.IsNullOrWhiteSpace(noOrder) ? null : noOrder);
    }

    private async System.Threading.Tasks.Task LoadVisitAsync(string noRawat, string? noOrder)
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var repo = new LabPeriksaRepository(db);

            var reg = await repo.GetRegInfoAsync(noRawat);
            if (reg is null) { ShowStatus($"No.Rawat '{noRawat}' tidak ditemukan.", isError: true); return; }

            LabOrderInfo? order = null;
            if (noOrder is not null)
            {
                order = await repo.GetOrderAsync(_category, noOrder);
                if (order is null) { ShowStatus($"Permintaan '{noOrder}' tidak ditemukan.", isError: true); return; }
            }

            _reg = reg;
            _noOrder = noOrder;
            _orderTemplates = order is { IdTemplate.Count: > 0 } ? order.IdTemplate.ToHashSet() : null;

            await EnsureLookupsAsync(reg.KodeDokter, order?.DokterPerujuk ?? "");
            SelectPick("PerujukCombo", order?.DokterPerujuk is { Length: > 0 } p ? p : reg.KodeDokter);

            this.FindControl<TextBlock>("PasienText")!.Text = $"{reg.NoRm}  {reg.NamaPasien}   ({reg.Jk}, {reg.Umur})";
            this.FindControl<TextBlock>("InfoText")!.Text = $"{reg.KamarPoli}   |   Cara Bayar : {reg.CaraBayar}   |   {reg.Alamat}";
            var orderText = this.FindControl<TextBlock>("OrderText")!;
            orderText.IsVisible = order is not null;
            orderText.Text = order is null ? "" : $"Hasil untuk permintaan {noOrder} — pemeriksaan dan item yang diminta sudah dicentang.";

            IReadOnlyList<LabTarifRow> tarif = order is not null
                ? await repo.GetTarifByKodeAsync(_category, order.KdJenisPrw)
                : await repo.GetTarifAsync(_category, reg, this.FindControl<TextBox>("CariBox")!.Text);
            FillTarif(tarif, preTick: order is not null);

            if (reg.Stts == "Batal") ShowStatus("Registrasi ini berstatus Batal — data tidak akan bisa disimpan.", isError: true);
            else HideStatus();
            await SyncDetailAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal memuat data: {ex.Message}", isError: true);
        }
    }

    private void FillTarif(IReadOnlyList<LabTarifRow> rows, bool preTick)
    {
        _suppressSync = true;
        foreach (var t in _tarif) t.PropertyChanged -= OnTarifChanged;
        _tarif.Clear();
        _detail.Clear();
        _pa.Clear();
        foreach (var r in rows)
        {
            var pick = new TarifPick(r) { Dipilih = preTick };
            pick.PropertyChanged += OnTarifChanged;
            _tarif.Add(pick);
        }
        _suppressSync = false;
    }

    private async void OnTarifChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSync || e.PropertyName != nameof(TarifPick.Dipilih)) return;
        await SyncDetailAsync();
    }

    /// <summary>Adds/removes detail rows to match the ticked pemeriksaan; rows already filled in are kept (legacy tampil()).</summary>
    private async System.Threading.Tasks.Task SyncDetailAsync()
    {
        if (_syncing) { _syncAgain = true; return; }
        _syncing = true;
        try
        {
            do
            {
                _syncAgain = false;
                var ticked = _tarif.Where(t => t.Dipilih).ToList();
                var keep = ticked.Select(t => t.Kode).ToHashSet();

                if (_category == LabCategory.PA)
                {
                    for (var i = _pa.Count - 1; i >= 0; i--) if (!keep.Contains(_pa[i].KdJenisPrw)) _pa.RemoveAt(i);
                    foreach (var t in ticked.Where(t => _pa.All(p => p.KdJenisPrw != t.Kode)))
                        _pa.Add(new PaRow { KdJenisPrw = t.Kode, Nama = t.Nama });
                }
                else
                {
                    for (var i = _detail.Count - 1; i >= 0; i--) if (!keep.Contains(_detail[i].KdJenisPrw)) _detail.RemoveAt(i);

                    var missing = ticked.Where(t => _detail.All(d => d.KdJenisPrw != t.Kode)).ToList();
                    if (missing.Count > 0)
                    {
                        using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
                        var repo = new LabPeriksaRepository(db);
                        foreach (var t in missing)
                        {
                            var templates = await repo.GetTemplatesAsync(t.Kode, null, _orderTemplates?.ToArray());
                            _detail.Add(new LabDetailRow { IsHeader = true, KdJenisPrw = t.Kode, Nama = t.Nama });
                            foreach (var tpl in templates)
                            {
                                var rujukan = PickRujukan(tpl);
                                _detail.Add(new LabDetailRow
                                {
                                    KdJenisPrw = t.Kode, Template = tpl, Nama = "   " + tpl.Pemeriksaan, Satuan = tpl.Satuan, Metode = tpl.Metode,
                                    NilaiRujukan = rujukan, AutoRujukan = rujukan, Dipilih = _noOrder is not null,
                                });
                            }
                        }
                    }
                }
                RecalcTotal();
            } while (_syncAgain);
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal memuat detail pemeriksaan: {ex.Message}", isError: true);
        }
        finally { _syncing = false; }
    }

    /// <summary>Legacy rule: nilai_rujukan_{ld|la|pd|pa} = (L|P) × (Dewasa|Anak-Anak), from the radio on the form.</summary>
    private string PickRujukan(LabTemplateRow t)
    {
        var dewasa = this.FindControl<RadioButton>("RbDewasa")!.IsChecked == true;
        return (_reg?.Jk, dewasa) switch
        {
            ("L", true) => t.NilaiRujukanLd,
            ("L", false) => t.NilaiRujukanLa,
            ("P", true) => t.NilaiRujukanPd,
            ("P", false) => t.NilaiRujukanPa,
            _ => "",
        };
    }

    private void OnUmurRadioClick(object? sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        foreach (var row in _detail.Where(d => d.IsItem && d.Template is not null))
        {
            if (row.NilaiRujukan != row.AutoRujukan) continue; // user edited it — leave it alone
            var v = PickRujukan(row.Template!);
            row.NilaiRujukan = v;
            row.AutoRujukan = v;
        }
    }

    private void RecalcTotal()
    {
        var total = _tarif.Where(t => t.Dipilih).Sum(t => t.Tarif.Biaya)
                  + _detail.Where(d => d.IsItem && (d.Dipilih || d.Hasil.Length > 0)).Sum(d => d.Template?.BiayaItem ?? 0);
        this.FindControl<TextBlock>("TotalText")!.Text = $"Total Biaya : {total:N0}";
    }

    // ---- toolbar ----------------------------------------------------------------------------

    private async void OnMuatClick(object? sender, RoutedEventArgs e) => await MuatAsync();

    private async void OnNoRawatKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await MuatAsync();
    }

    private async System.Threading.Tasks.Task MuatAsync()
    {
        var noRawat = this.FindControl<TextBox>("NoRawatBox")!.Text?.Trim();
        if (string.IsNullOrEmpty(noRawat)) { ShowStatus("Isi No.Rawat dulu.", isError: true); return; }
        await LoadVisitAsync(noRawat, null);
    }

    private async void OnCariClick(object? sender, RoutedEventArgs e) => await ReloadTarifAsync();

    private async void OnCariKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await ReloadTarifAsync();
    }

    /// <summary>Re-runs the pemeriksaan search; ticked rows survive the refresh (legacy tampiltarif keeps checked rows on top).</summary>
    private async System.Threading.Tasks.Task ReloadTarifAsync()
    {
        if (_reg is null) { ShowStatus("Muat No.Rawat dulu.", isError: true); return; }
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var found = await new LabPeriksaRepository(db).GetTarifAsync(_category, _reg, this.FindControl<TextBox>("CariBox")!.Text);

            _suppressSync = true;
            var ticked = _tarif.Where(t => t.Dipilih).ToList();
            foreach (var t in _tarif.Where(t => !t.Dipilih)) t.PropertyChanged -= OnTarifChanged;
            _tarif.Clear();
            foreach (var t in ticked) _tarif.Add(t);
            foreach (var r in found.Where(r => ticked.All(t => t.Kode != r.KdJenisPrw)))
            {
                var pick = new TarifPick(r);
                pick.PropertyChanged += OnTarifChanged;
                _tarif.Add(pick);
            }
            _suppressSync = false;
        }
        catch (Exception ex) { ShowStatus($"Gagal mencari pemeriksaan: {ex.Message}", isError: true); }
    }

    private void OnSekarangClick(object? sender, RoutedEventArgs e)
        => this.FindControl<TextBox>("JamBox")!.Text = DateTime.Now.ToString("HH:mm:ss");

    private async void OnBaruClick(object? sender, RoutedEventArgs e)
    {
        _noOrder = null;
        _orderTemplates = null;
        this.FindControl<TextBlock>("OrderText")!.IsVisible = false;
        HideStatus();
        if (_reg is null)
        {
            FillTarif(Array.Empty<LabTarifRow>(), false);
            RecalcTotal();
            return;
        }
        await LoadVisitAsync(_reg.NoRawat, null);
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        if (_reg is null) { ShowStatus("Pasien belum dipilih — isi No.Rawat lalu Muat.", isError: true); return; }
        var petugas = this.FindControl<ComboBox>("PetugasCombo")!.SelectedItem as LabPick;
        var perujuk = this.FindControl<ComboBox>("PerujukCombo")!.SelectedItem as LabPick;
        var pj = this.FindControl<ComboBox>("PjCombo")!.SelectedItem as LabPick;
        if (petugas is null) { ShowStatus("Petugas wajib diisi.", isError: true); return; }
        if (perujuk is null) { ShowStatus("Dokter perujuk wajib diisi.", isError: true); return; }
        if (pj is null) { ShowStatus("Dokter penanggung jawab wajib diisi.", isError: true); return; }
        if (!TimeOnly.TryParseExact(this.FindControl<TextBox>("JamBox")!.Text?.Trim(), "HH:mm:ss", out var jam))
        { ShowStatus("Jam harus berformat HH:mm:ss.", isError: true); return; }
        var tgl = DateOnly.FromDateTime(this.FindControl<CalendarDatePicker>("TanggalPicker")!.SelectedDate ?? DateTime.Today);

        var ticked = _tarif.Where(t => t.Dipilih).Select(t => t.Tarif).ToList();
        if (ticked.Count == 0) { ShowStatus("Pilih minimal satu pemeriksaan.", isError: true); return; }

        var items = new List<LabSaveItem>();
        var pa = new List<LabSavePa>();
        int unfilled;
        if (_category == LabCategory.PA)
        {
            // Legacy saves only rows whose makroskopik is filled in.
            pa = _pa.Where(p => p.Makroskopik.Length > 0)
                    .Select(p => new LabSavePa(p.KdJenisPrw, p.DiagnosaKlinik, p.Makroskopik, p.Mikroskopik, p.Kesimpulan, p.Kesan)).ToList();
            unfilled = _pa.Count(p => p.Makroskopik.Length == 0);
        }
        else
        {
            var chosen = _detail.Where(d => d.IsItem && d.Template is not null && (d.Dipilih || d.Hasil.Length > 0)).ToList();
            if (chosen.Count == 0) { ShowStatus("Pilih/isi minimal satu item pemeriksaan.", isError: true); return; }
            unfilled = chosen.Count(d => d.Hasil.Length == 0);
            items = chosen.Select(d => new LabSaveItem(
                d.Template!.KdJenisPrw, d.Template.IdTemplate, d.Hasil, d.NilaiRujukan, d.Keterangan, d.Template.BagianRs, d.Template.Bhp,
                d.Template.BagianPerujuk, d.Template.BagianDokter, d.Template.BagianLaborat, d.Template.Kso, d.Template.Menejemen, d.Template.BiayaItem)).ToList();
        }

        var question = unfilled > 0
            ? "Ada hasil lab yang belum diisi, yakin mau disimpan..??"
            : "Yakin data yang akan disimpan sudah benar?";
        if (!await LabUi.ConfirmAsync(this, question)) return;

        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new LabPeriksaRepository(db).SaveAsync(new LabSaveRequest(
                _category, _reg.NoRawat, petugas.Kode, perujuk.Kode, pj.Kode, tgl, jam, _reg.Status, _noOrder, ticked, items, pa));
            ShowStatus("Proses simpan selesai.", isError: false);
            SavedRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowStatus($"Terjadi kesalahan, transaksi dibatalkan: {ex.Message}", isError: true);
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        var s = this.FindControl<TextBlock>("StatusText")!;
        s.Foreground = isError ? Brush.Parse("#DC2626") : Brushes.Green;
        s.Text = message;
        s.IsVisible = true;
    }

    private void HideStatus() => this.FindControl<TextBlock>("StatusText")!.IsVisible = false;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
