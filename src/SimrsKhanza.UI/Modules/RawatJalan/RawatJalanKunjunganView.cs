using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Master;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.RawatJalan;

public sealed class RawatJalanKunjunganView : UserControl
{
    private readonly PatientMenuRequest _patient;
    private readonly AuthenticatedUser _user;
    private readonly ContentControl _panel = new();
    private readonly TextBlock _status = Ui.Status();
    private readonly StackPanel _menu = new() { Spacing = 2 };
    private readonly TextBox _search = new() { PlaceholderText = "Cari menu klinis" };
    private readonly WrapPanel _navigation;
    private readonly Grid _body;
    private readonly Border _sidebar;
    private readonly TindakanRalanView _tindakan;
    private readonly Dictionary<string, Control> _alurKunjungan = [];
    private RawatJalanView? _pemeriksaan;
    private CatatanDokterView? _catatan;
    private bool _ready;

    public event EventHandler? BackRequested;

    public RawatJalanKunjunganView(PatientMenuRequest patient, AuthenticatedUser user)
    {
        _patient = patient;
        _user = user;
        _tindakan = new TindakanRalanView(patient.NoRawat);
        _tindakan.UbahTindakanRequested += (_, asal) =>
        {
            if (_ready && _user.HasPermission(TindakanRalanInputService.Izin))
                _panel.Content = new TindakanRalanInputView(_patient.NoRawat, _user, asal);
        };

        var header = Ui.Bar(Ui.Btn("Kembali ke Registrasi", (_, _) => BackRequested?.Invoke(this, EventArgs.Empty)),
            Ui.Title("Tindakan Rawat Jalan"));
        var identity = Ui.Hint($"{patient.NamaPasien} · No. RM {patient.NoRekamMedis} · No. Rawat {patient.NoRawat}");
        identity.TextWrapping = TextWrapping.Wrap;
        _navigation = Ui.Bar(
            Ui.Btn("Daftar Tindakan", async (_, _) => await BukaTindakanAsync()),
            Ui.Btn("Input Tindakan", (_, _) => BukaInputTindakan(), user.HasPermission(TindakanRalanInputService.Izin)),
            Ui.Btn("Pemeriksaan / SOAPIE", async (_, _) => await BukaPemeriksaanAsync()),
            Ui.Btn("Obstetri", async (_, _) => await BukaLaporanAsync(RawatJalanVisitReports.Obstetri)),
            Ui.Btn("Ginekologi", async (_, _) => await BukaLaporanAsync(RawatJalanVisitReports.Ginekologi)),
            Ui.Btn("Input Obstetri", (_, _) => { if (_ready && _user.HasPermission("tindakan_ralan")) _panel.Content = new ObgynRalanView(_patient.NoRawat, _user, true); }, user.HasPermission("tindakan_ralan")),
            Ui.Btn("Input Ginekologi", (_, _) => { if (_ready && _user.HasPermission("tindakan_ralan")) _panel.Content = new ObgynRalanView(_patient.NoRawat, _user, false); }, user.HasPermission("tindakan_ralan")),
            Ui.Btn("Diagnosa", (_, _) => { if (_ready && _user.HasPermission("diagnosa_pasien")) _panel.Content = new DiagnosaRalanView(_patient.NoRawat, _user); }, user.HasPermission("diagnosa_pasien")),
            Ui.Btn("Catatan Dokter", (_, _) => BukaCatatanDokter(), user.HasPermission(CatatanDokterService.Izin)),
            Ui.Btn("Menu Kiri", (_, _) => ToggleMenu()));

        _search.TextChanged += (_, _) => IsiMenu();
        var sidebar = new DockPanel { Children = { _search, new ScrollViewer { Content = _menu } } };
        DockPanel.SetDock(_search, Dock.Top);
        _sidebar = Ui.Themed(new Border { Child = sidebar, Padding = new Thickness(8), CornerRadius = new CornerRadius(4) },
            Border.BackgroundProperty, "ThemeSurfaceBrush");

        _body = new Grid { ColumnDefinitions = new("245,*"), ColumnSpacing = 10, Children = { _sidebar, _panel } };
        Grid.SetColumn(_panel, 1);
        var layout = new Grid { Margin = new Thickness(12), RowSpacing = 8, RowDefinitions = new("Auto,Auto,Auto,*,Auto") };
        var controls = new Control[] { header, identity, _navigation, _body, _status };
        for (var i = 0; i < controls.Length; i++)
        {
            Grid.SetRow(controls[i], i);
            layout.Children.Add(controls[i]);
        }
        Content = layout;
        _navigation.IsEnabled = _body.IsEnabled = false;
        IsiMenu();
    }

    public async Task LoadAsync()
    {
        Ui.Info(_status, "Memuat kunjungan...");
        try
        {
            var visit = await new RawatJalanService(DatabaseSettings.Load()).KunjunganAsync(_patient.NoRawat);
            if (visit is null || visit.NoRekamMedis != _patient.NoRekamMedis)
            {
                Ui.Err(_status, "Kunjungan rawat jalan tidak ditemukan. Kembali ke Registrasi dan pilih ulang pasien.");
                return;
            }
            _ready = _navigation.IsEnabled = _body.IsEnabled = true;
            _status.Text = "";
            await BukaTindakanAsync();
        }
        catch (Exception)
        {
            Ui.Err(_status, "Kunjungan tidak dapat dimuat. Periksa koneksi lalu buka ulang dari Registrasi.");
        }
    }

    private async Task BukaTindakanAsync()
    {
        if (!_ready || ReferenceEquals(_panel.Content, _tindakan)) return;
        _panel.Content = _tindakan;
        await _tindakan.ReloadAsync();
    }

    private void BukaInputTindakan()
    {
        if (!_ready || !_user.HasPermission(TindakanRalanInputService.Izin) || _panel.Content is TindakanRalanInputView) return;
        _panel.Content = new TindakanRalanInputView(_patient.NoRawat, _user);
    }

    private async Task BukaPemeriksaanAsync()
    {
        if (!_ready) return;
        if (_pemeriksaan is not null)
        {
            _panel.Content = _pemeriksaan;
            return;
        }
        _pemeriksaan = new RawatJalanView(embedded: true);
        _panel.Content = _pemeriksaan;
        await _pemeriksaan.SelectVisitAsync(_patient.NoRawat);
    }

    private async Task BukaLaporanAsync(ReportSpec spec)
    {
        if (!_ready) return;
        var report = new ReportView(spec, _patient.NoRawat);
        report.BackRequested += async (_, _) => await BukaTindakanAsync();
        _panel.Content = report;
        await report.ReloadAsync();
    }

    private void BukaCatatanDokter()
    {
        if (!_ready || !_user.HasPermission(CatatanDokterService.Izin)) return;
        _panel.Content = _catatan ??= new CatatanDokterView(_patient.NoRawat, _user);
    }

    private void IsiMenu()
    {
        _menu.Children.Clear();
        var search = _search.Text?.Trim() ?? "";
        foreach (var item in MenuKlinisRalanCatalog.Items.Where(item => _user.HasPermission(item.Permission) &&
            item.Id != "BtnResepIterasiBPJS" && item.Label.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
        {
            var spec = CariLaporan(item);
            var input = item.Id is "BtnResepObat" or "BtnPermintaanLab" or "BtnPermintaanRad" or "BtnResume";
            var available = input || spec is not null;
            var label = new TextBlock
            {
                Text = available ? item.Label : $"{item.Label} · Belum tersedia",
                TextWrapping = TextWrapping.Wrap,
            };
            var button = Ui.Btn("", async (_, _) =>
            {
                if (input && _ready && _user.HasPermission(item.Permission)) BukaAlurKunjungan(item.Id);
                else if (spec is not null) await BukaLaporanAsync(spec);
            }, available);
            button.Content = label;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            _menu.Children.Add(button);
        }
        if (_menu.Children.Count == 0) _menu.Children.Add(Ui.Hint("Menu tidak ditemukan."));
    }

    private void BukaAlurKunjungan(string id)
    {
        if (!_alurKunjungan.TryGetValue(id, out var view))
        {
            view = id switch
            {
                "BtnResepObat" => new OrderRalanView(_patient.NoRawat, _patient.NoRekamMedis, _user, JenisOrderRalan.Resep),
                "BtnPermintaanLab" => new OrderRalanView(_patient.NoRawat, _patient.NoRekamMedis, _user, JenisOrderRalan.LabPk),
                "BtnPermintaanRad" => new OrderRalanView(_patient.NoRawat, _patient.NoRekamMedis, _user, JenisOrderRalan.Radiologi),
                "BtnResume" => new ResumeRalanView(_patient.NoRawat, _patient.NoRekamMedis, _user),
                _ => throw new ArgumentOutOfRangeException(nameof(id))
            };
            _alurKunjungan[id] = view;
        }
        _panel.Content = view;
    }

    private ReportSpec? CariLaporan(MenuKlinisRalan item)
    {
        return item.Id switch
        {
            "BtnRiwayat" => RawatJalanVisitReports.Riwayat,
            "BtnObatBhp" => RawatJalanVisitReports.Obat,
            _ => item.ReportKey is { } key && MenuMSpecs.Reports.TryGetValue(key, out var spec) && spec.VisitSql is not null
                ? spec : null,
        };
    }

    private void ToggleMenu()
    {
        _sidebar.IsVisible = !_sidebar.IsVisible;
        _body.ColumnDefinitions[0].Width = new GridLength(_sidebar.IsVisible ? 245 : 0);
        _body.ColumnSpacing = _sidebar.IsVisible ? 10 : 0;
    }
}
