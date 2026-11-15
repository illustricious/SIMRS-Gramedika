using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SimrsKhanza.Config;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Modules.Laboratorium;

namespace SimrsKhanza.UI.Modules.Radiologi;

/// <summary>A jns_perawatan_radiologi row with its "P" tick and the exposure parameters legacy lets you type per row.</summary>
public class RadPick(RadTarifRow tarif) : ObservableRow
{
    public RadTarifRow Tarif { get; } = tarif;
    public string Kode => Tarif.KdJenisPrw;
    public string Nama => Tarif.Nama;
    public string BiayaText => Tarif.Biaya.ToString("N0");

    private bool _dipilih;
    public bool Dipilih { get => _dipilih; set => Set(ref _dipilih, value); }

    private string _proyeksi = "", _kv = "", _mas = "", _ffd = "", _bsf = "", _inak = "", _jml = "", _dosis = "";
    public string Proyeksi { get => _proyeksi; set => Set(ref _proyeksi, value ?? ""); }
    public string KV { get => _kv; set => Set(ref _kv, value ?? ""); }
    public string MAs { get => _mas; set => Set(ref _mas, value ?? ""); }
    public string Ffd { get => _ffd; set => Set(ref _ffd, value ?? ""); }
    public string Bsf { get => _bsf; set => Set(ref _bsf, value ?? ""); }
    public string Inak { get => _inak; set => Set(ref _inak, value ?? ""); }
    public string JmlPenyinaran { get => _jml; set => Set(ref _jml, value ?? ""); }
    public string Dosis { get => _dosis; set => Set(ref _dosis, value ?? ""); }
}

/// <summary>An ipsrsbarang BHP row; the quantity typed in "Jml" decides whether it is saved (legacy: Jml &gt; 0).</summary>
public class RadBhpPick(RadBhpRow bhp) : ObservableRow
{
    public RadBhpRow Bhp { get; } = bhp;
    public string Kode => Bhp.KodeBrng;
    public string Nama => Bhp.Nama;
    public string Satuan => Bhp.Satuan;
    public string HargaText => Bhp.Harga.ToString("N0");

    private string _jumlahText = "";
    public string JumlahText { get => _jumlahText; set => Set(ref _jumlahText, value ?? ""); }

    public decimal Jumlah
        => decimal.TryParse(_jumlahText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
           || decimal.TryParse(_jumlahText, NumberStyles.Float, CultureInfo.CurrentCulture, out v) ? v : 0m;
}

public partial class PeriksaRadiologiFormView : UserControl
{
    private bool _ready, _lookupsLoaded, _suppress;
    private LabRegInfo? _reg;
    private string? _noOrder;
    private IReadOnlyList<LabPick> _dokter = Array.Empty<LabPick>();
    private IReadOnlyList<LabPick> _petugas = Array.Empty<LabPick>();
    private string _defaultPj = "";
    private readonly ObservableCollection<RadPick> _tarif = new();
    private readonly ObservableCollection<RadBhpPick> _bhp = new();

    public event EventHandler? BackRequested;

    /// <summary>Raised after a successful Simpan so the parent can go back to the list.</summary>
    public event EventHandler? SavedRequested;

    public PeriksaRadiologiFormView()
    {
        InitializeComponent();
        this.FindControl<DataGrid>("PemeriksaanGrid")!.ItemsSource = _tarif;
        this.FindControl<DataGrid>("BhpGrid")!.ItemsSource = _bhp;
        this.FindControl<CalendarDatePicker>("TanggalPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<TextBox>("JamBox")!.Text = DateTime.Now.ToString("HH:mm:ss");
        _ready = true;
    }

    // ---- loading ----------------------------------------------------------------------------

    /// <summary>Parent calls this when the form is shown without a prefill (loads petugas / BHP / templates / default Dokter P.J.).</summary>
    public async System.Threading.Tasks.Task ReloadAsync()
    {
        await EnsureLookupsAsync();
        if (_bhp.Count == 0) await LoadBhpAsync();
    }

    private async System.Threading.Tasks.Task EnsureLookupsAsync(params string[] extraDokter)
    {
        using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
        var lab = new LabPeriksaRepository(db);
        var rad = new RadiologiPeriksaRepository(db);
        if (!_lookupsLoaded)
        {
            _petugas = await lab.GetPetugasAsync();
            _defaultPj = await rad.GetDefaultDokterPjAsync();
            this.FindControl<ComboBox>("PetugasCombo")!.ItemsSource = _petugas;
            this.FindControl<ComboBox>("TemplateCombo")!.ItemsSource = await rad.GetHasilTemplatesAsync();
            _lookupsLoaded = true;
        }
        _dokter = await lab.GetDokterAsync(extraDokter.Append(_defaultPj).ToList());
        this.FindControl<ComboBox>("PerujukCombo")!.ItemsSource = _dokter;
        this.FindControl<ComboBox>("PjCombo")!.ItemsSource = _dokter;
        SelectPick("PjCombo", _defaultPj);
    }

    private void SelectPick(string comboName, string? code)
    {
        var combo = this.FindControl<ComboBox>(comboName)!;
        combo.SelectedItem = (combo.ItemsSource as IEnumerable<LabPick>)?.FirstOrDefault(p => p.Kode == code);
    }

    private async System.Threading.Tasks.Task LoadBhpAsync()
    {
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var keep = _bhp.Where(b => b.Jumlah > 0).ToList();
            var found = await new RadiologiPeriksaRepository(db).GetBhpAsync(this.FindControl<TextBox>("CariBhpBox")!.Text);

            // Rows with a quantity survive a re-search (legacy tampil2 re-adds them on top).
            foreach (var b in _bhp) b.PropertyChanged -= OnRowChanged;
            _bhp.Clear();
            foreach (var b in keep) _bhp.Add(b);
            foreach (var r in found.Where(r => keep.All(k => k.Kode != r.KodeBrng)))
            {
                var pick = new RadBhpPick(r);
                pick.PropertyChanged += OnRowChanged;
                _bhp.Add(pick);
            }
            foreach (var b in keep) { b.PropertyChanged -= OnRowChanged; b.PropertyChanged += OnRowChanged; }
        }
        catch (Exception ex) { ShowStatus($"Gagal memuat BHP: {ex.Message}", isError: true); }
    }

    /// <summary>
    /// Entry point from the permintaan screen's "Hasil" button: loads the visit and preloads exactly
    /// what the order asked for (pemeriksaan ticked).
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
            var lab = new LabPeriksaRepository(db);
            var rad = new RadiologiPeriksaRepository(db);

            var reg = await lab.GetRegInfoAsync(noRawat);
            if (reg is null) { ShowStatus($"No.Rawat '{noRawat}' tidak ditemukan.", isError: true); return; }

            RadOrderInfo? order = null;
            if (noOrder is not null)
            {
                order = await rad.GetOrderAsync(noOrder);
                if (order is null) { ShowStatus($"Permintaan '{noOrder}' tidak ditemukan.", isError: true); return; }
            }

            _reg = reg;
            _noOrder = noOrder;

            await EnsureLookupsAsync(reg.KodeDokter, order?.DokterPerujuk ?? "");
            SelectPick("PerujukCombo", order?.DokterPerujuk is { Length: > 0 } p ? p : reg.KodeDokter);

            this.FindControl<TextBlock>("PasienText")!.Text = $"{reg.NoRm}  {reg.NamaPasien}   ({reg.Jk}, {reg.Umur})";
            this.FindControl<TextBlock>("InfoText")!.Text = $"{reg.KamarPoli}   |   Cara Bayar : {reg.CaraBayar}   |   {reg.Alamat}";
            var orderText = this.FindControl<TextBlock>("OrderText")!;
            orderText.IsVisible = order is not null;
            orderText.Text = order is null ? "" : $"Hasil untuk permintaan {noOrder} — pemeriksaan yang diminta sudah dicentang.";

            IReadOnlyList<RadTarifRow> tarif = order is not null
                ? await rad.GetTarifByOrderAsync(noOrder!)
                : await rad.GetTarifAsync(reg, this.FindControl<TextBox>("CariBox")!.Text);
            FillTarif(tarif, preTick: order is not null);
            if (_bhp.Count == 0) await LoadBhpAsync();

            if (reg.Stts == "Batal") ShowStatus("Registrasi ini berstatus Batal — data tidak akan bisa disimpan.", isError: true);
            else HideStatus();
            RecalcTotal();
        }
        catch (Exception ex)
        {
            ShowStatus($"Gagal memuat data: {ex.Message}", isError: true);
        }
    }

    private void FillTarif(IReadOnlyList<RadTarifRow> rows, bool preTick)
    {
        _suppress = true;
        foreach (var t in _tarif) t.PropertyChanged -= OnRowChanged;
        _tarif.Clear();
        foreach (var r in rows)
        {
            var pick = new RadPick(r) { Dipilih = preTick };
            pick.PropertyChanged += OnRowChanged;
            _tarif.Add(pick);
        }
        _suppress = false;
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppress) return;
        if (e.PropertyName is nameof(RadPick.Dipilih) or nameof(RadBhpPick.JumlahText)) RecalcTotal();
    }

    private void RecalcTotal()
    {
        var total = _tarif.Where(t => t.Dipilih).Sum(t => t.Tarif.Biaya) + _bhp.Where(b => b.Jumlah > 0).Sum(b => b.Jumlah * b.Bhp.Harga);
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

    /// <summary>Re-runs the pemeriksaan search; ticked rows (with their parameters) survive the refresh.</summary>
    private async System.Threading.Tasks.Task ReloadTarifAsync()
    {
        if (_reg is null) { ShowStatus("Muat No.Rawat dulu.", isError: true); return; }
        try
        {
            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            var found = await new RadiologiPeriksaRepository(db).GetTarifAsync(_reg, this.FindControl<TextBox>("CariBox")!.Text);

            _suppress = true;
            var ticked = _tarif.Where(t => t.Dipilih).ToList();
            foreach (var t in _tarif.Where(t => !t.Dipilih)) t.PropertyChanged -= OnRowChanged;
            _tarif.Clear();
            foreach (var t in ticked) _tarif.Add(t);
            foreach (var r in found.Where(r => ticked.All(t => t.Kode != r.KdJenisPrw)))
            {
                var pick = new RadPick(r);
                pick.PropertyChanged += OnRowChanged;
                _tarif.Add(pick);
            }
            _suppress = false;
            RecalcTotal();
        }
        catch (Exception ex) { ShowStatus($"Gagal mencari pemeriksaan: {ex.Message}", isError: true); }
    }

    private async void OnCariBhpClick(object? sender, RoutedEventArgs e) => await LoadBhpAsync();

    private async void OnCariBhpKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await LoadBhpAsync();
    }

    private void OnTemplateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (this.FindControl<ComboBox>("TemplateCombo")!.SelectedItem is RadTemplateRow t)
            this.FindControl<TextBox>("HasilBox")!.Text = t.Isi;
    }

    private void OnSekarangClick(object? sender, RoutedEventArgs e)
        => this.FindControl<TextBox>("JamBox")!.Text = DateTime.Now.ToString("HH:mm:ss");

    private async void OnBaruClick(object? sender, RoutedEventArgs e)
    {
        _noOrder = null;
        this.FindControl<TextBlock>("OrderText")!.IsVisible = false;
        this.FindControl<TextBox>("HasilBox")!.Text = string.Empty;
        HideStatus();
        foreach (var b in _bhp) b.JumlahText = "";
        if (_reg is null)
        {
            FillTarif(Array.Empty<RadTarifRow>(), false);
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

        var ticked = _tarif.Where(t => t.Dipilih).ToList();
        if (ticked.Count == 0) { ShowStatus("Pilih minimal satu pemeriksaan.", isError: true); return; }

        if (!await LabUi.ConfirmAsync(this, "Yakin data yang akan disimpan sudah benar?")) return;

        try
        {
            var pemeriksaan = ticked.Select(t => new RadSavePemeriksaan(t.Tarif, t.Proyeksi, t.KV, t.MAs, t.Ffd, t.Bsf, t.Inak, t.JmlPenyinaran, t.Dosis)).ToList();
            var bhp = _bhp.Where(b => b.Jumlah > 0).Select(b => new RadSaveBhp(b.Bhp.KodeBrng, b.Bhp.Satuan, b.Jumlah, b.Bhp.Harga)).ToList();

            using var db = SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load());
            await new RadiologiPeriksaRepository(db).SaveAsync(new RadSaveRequest(
                _reg.NoRawat, petugas.Kode, perujuk.Kode, pj.Kode, tgl, jam, _reg.Status, _noOrder, pemeriksaan, bhp,
                this.FindControl<TextBox>("HasilBox")!.Text ?? ""));
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
