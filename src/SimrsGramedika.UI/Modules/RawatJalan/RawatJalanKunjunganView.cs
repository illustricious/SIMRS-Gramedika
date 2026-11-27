using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.RekamMedis;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatJalan;

/// <summary>
/// Halaman "Tagihan/Tindakan Rawat Jalan" seperti DlgRawatJalan: tab pelayanan di tengah dan menu klinis di kiri.
/// Isi tab baru dibuat saat pertama dibuka.
/// </summary>
public sealed class RawatJalanKunjunganView : UserControl
{
    /// <summary>Urutan tab DlgRawatJalan.</summary>
    internal static readonly string[] NamaTab =
    [
        "Penanganan Dokter", "Penanganan Petugas", "Penanganan Dokter & Petugas", "Pemeriksaan",
        "Pemeriksaan Obstetri", "Pemeriksaan Ginekologi", "Diagnosa", "Catatan Dokter",
    ];

    // Khanza kembali ke tab Pemeriksaan saat formulir dikosongkan; di sini menjadi tab pembuka.
    private const int TabPemeriksaan = 3;

    private readonly PatientMenuRequest _patient;
    private readonly AuthenticatedUser _user;
    private readonly TabControl _tab = new();
    private readonly ContentControl _isi = new();
    private readonly ContentControl _panel = new();
    private readonly Control _panelMenu;
    private readonly TextBlock _judulMenu = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = Ui.Status();
    private readonly StackPanel _menu = new() { Spacing = 2 };
    private readonly TextBox _search = new() { PlaceholderText = "Cari menu klinis" };
    private readonly Grid _body;
    private readonly Border _sidebar;
    private readonly Dictionary<string, Control> _alurKunjungan = [];
    private readonly Dictionary<string, Modules.RekamMedis.FormKlinisView> _formulir = [];
    private bool _ready;

    public event EventHandler? BackRequested;

    public RawatJalanKunjunganView(PatientMenuRequest patient, AuthenticatedUser user)
    {
        _patient = patient;
        _user = user;

        var header = new DockPanel();
        var tombol = Ui.Bar(
            Ui.Btn("Menu Kiri", (_, _) => ToggleMenu()),
            Ui.Btn("Kembali ke Registrasi", (_, _) => BackRequested?.Invoke(this, EventArgs.Empty)));
        DockPanel.SetDock(tombol, Dock.Right);
        header.Children.Add(tombol);
        header.Children.Add(Ui.Title("Tagihan/Tindakan Rawat Jalan"));
        var identity = Ui.Hint($"{patient.NamaPasien} · No. RM {patient.NoRekamMedis} · No. Rawat {patient.NoRawat}");
        identity.TextWrapping = TextWrapping.Wrap;

        foreach (var nama in NamaTab) _tab.Items.Add(new TabItem { Header = nama });
        Tab(6).IsEnabled = user.HasPermission("diagnosa_pasien");
        Tab(7).IsEnabled = user.HasPermission(CatatanDokterService.Izin);
        _tab.SelectedIndex = TabPemeriksaan;
        _tab.SelectionChanged += async (_, e) =>
        {
            // Pilihan di tabel dan combo di dalam tab ikut merambat ke sini.
            if (ReferenceEquals(e.Source, _tab)) await IsiTabAsync();
        };
        _isi.Content = _tab;

        var kembali = Ui.Btn("Kembali ke Tab", (_, _) => TampilkanTab());
        var barisMenu = Ui.Bar(kembali, _judulMenu);
        var panelMenu = new DockPanel { Children = { barisMenu, _panel } };
        DockPanel.SetDock(barisMenu, Dock.Top);
        _panelMenu = panelMenu;

        _search.TextChanged += (_, _) => IsiMenu();
        var sidebar = new DockPanel { Children = { _search, new ScrollViewer { Content = _menu } } };
        DockPanel.SetDock(_search, Dock.Top);
        _sidebar = Ui.Themed(new Border { Child = sidebar, Padding = new Thickness(8), CornerRadius = new CornerRadius(4) },
            Border.BackgroundProperty, "ThemeSurfaceBrush");

        _body = new Grid { ColumnDefinitions = new("245,*"), ColumnSpacing = 10, Children = { _sidebar, _isi } };
        Grid.SetColumn(_isi, 1);
        var layout = new Grid { Margin = new Thickness(12), RowSpacing = 8, RowDefinitions = new("Auto,Auto,*,Auto") };
        var controls = new Control[] { header, identity, _body, _status };
        for (var i = 0; i < controls.Length; i++)
        {
            Grid.SetRow(controls[i], i);
            layout.Children.Add(controls[i]);
        }
        Content = layout;
        _body.IsEnabled = false;
        IsiMenu();
    }

    private TabItem Tab(int index) => (TabItem)_tab.Items[index]!;

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
            _ready = _body.IsEnabled = true;
            _status.Text = "";
            await IsiTabAsync();
        }
        catch (Exception)
        {
            Ui.Err(_status, "Kunjungan tidak dapat dimuat. Periksa koneksi lalu buka ulang dari Registrasi.");
        }
    }

    private async Task IsiTabAsync()
    {
        var index = _tab.SelectedIndex;
        if (!_ready || index < 0 || Tab(index) is not { IsEnabled: true, Content: null } tab) return;
        switch (index)
        {
            case < TabPemeriksaan:
                tab.Content = TabPenanganan((TindakanRalanJenis)index);
                break;
            case TabPemeriksaan:
                var pemeriksaan = new RawatJalanView(embedded: true);
                tab.Content = pemeriksaan;
                await pemeriksaan.SelectVisitAsync(_patient.NoRawat);
                break;
            case 4 or 5:
                tab.Content = new ObgynRalanView(_patient.NoRawat, _user, index == 4);
                break;
            case 6:
                tab.Content = new DiagnosaRalanView(_patient.NoRawat, _user);
                break;
            default:
                tab.Content = new CatatanDokterView(_patient.NoRawat, _user);
                break;
        }
    }

    /// <summary>Seperti Khanza: pilih tarif di satu tab, lihat yang sudah dilakukan di tab sebelahnya.</summary>
    private Control TabPenanganan(TindakanRalanJenis jenis)
    {
        var boleh = _user.HasPermission(TindakanRalanInputService.Izin);
        Control Baru() => new TindakanRalanInputView(_patient.NoRawat, _user, jenis: jenis);
        var input = new TabItem { Header = "Daftar Tindakan/Tagihan", IsEnabled = boleh, Content = boleh ? Baru() : null };
        var daftar = new TindakanRalanView(_patient.NoRawat);
        daftar.HanyaJenis(jenis);
        var dilakukan = new TabItem { Header = "Tindakan Dilakukan", Content = daftar };
        var dalam = new TabControl { Items = { input, dilakukan }, SelectedIndex = boleh ? 0 : 1 };
        var mengubah = false;
        dalam.SelectionChanged += async (_, e) =>
        {
            if (!ReferenceEquals(e.Source, dalam) || !ReferenceEquals(dalam.SelectedItem, dilakukan)) return;
            // Layar ubah dipakai sekali; kembali ke tab input berikutnya memulai isian baru.
            if (mengubah)
            {
                input.Content = Baru();
                mengubah = false;
            }
            await daftar.ReloadAsync();
        };
        daftar.UbahTindakanRequested += (_, asal) =>
        {
            if (!boleh) return;
            input.Content = new TindakanRalanInputView(_patient.NoRawat, _user, asal);
            mengubah = true;
            dalam.SelectedItem = input;
        };
        if (!boleh) dalam.AttachedToVisualTree += async (_, _) => await daftar.ReloadAsync();
        return dalam;
    }

    private void TampilkanTab() => _isi.Content = _tab;

    private void TampilkanMenu(string judul, Control view)
    {
        _judulMenu.Text = judul;
        _panel.Content = view;
        _isi.Content = _panelMenu;
    }

    private async Task BukaLaporanAsync(string judul, ReportSpec spec)
    {
        if (!_ready) return;
        var report = new ReportView(spec, _patient.NoRawat);
        report.BackRequested += (_, _) => TampilkanTab();
        TampilkanMenu(judul, report);
        await report.ReloadAsync();
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
            var available = input || spec is not null || FormKlinisKatalog.Cari(item.ReportKey) is not null;
            var label = new TextBlock
            {
                Text = available ? item.Label : $"{item.Label} · Belum tersedia",
                TextWrapping = TextWrapping.Wrap,
            };
            var button = Ui.Btn("", async (_, _) =>
            {
                if (input && _ready && _user.HasPermission(item.Permission)) BukaAlurKunjungan(item.Id, item.Label);
                else if (FormKlinisKatalog.Cari(item.ReportKey) is { } form && _ready && _user.HasPermission(form.Izin))
                    await BukaFormulirAsync(form);
                else if (spec is not null) await BukaLaporanAsync(item.Label, spec);
            }, available);
            button.Content = label;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            _menu.Children.Add(button);
        }
        if (_menu.Children.Count == 0) _menu.Children.Add(Ui.Hint("Menu tidak ditemukan."));
    }

    private void BukaAlurKunjungan(string id, string judul)
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
        TampilkanMenu(judul, view);
    }

    private async Task BukaFormulirAsync(FormKlinis form)
    {
        // Layar disimpan agar isian yang belum disimpan tidak hilang saat berpindah menu.
        if (!_formulir.TryGetValue(form.Tile, out var view))
        {
            view = new Modules.RekamMedis.FormKlinisView(form, _patient.NoRawat, _user);
            view.BackRequested += (_, _) => TampilkanTab();
            _formulir[form.Tile] = view;
        }
        TampilkanMenu(form.Judul, view);
        if (!view.Siap) await view.ReloadAsync();
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
