using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using SimrsKhanza.Core.Auth;

namespace SimrsKhanza.UI;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private AuthenticatedUser? _user;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(AuthenticatedUser? user)
    {
        InitializeComponent();
        _user = user;

        var loginLabel = this.FindControl<TextBlock>("LoggedInUserText");
        if (loginLabel is not null)
        {
            loginLabel.Text = user is null
                ? "Login sebagai: (belum terhubung)"
                : $"Login sebagai: {user.DisplayName}";
        }

        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        if (user is not null)
        {
            SetupMenuCategoryCombo(user);
        }
    }

    private void UpdateClock()
    {
        var clock = this.FindControl<TextBlock>("ClockText");
        if (clock is not null)
        {
            clock.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }
    }

    private void OnThemeToggleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is null) return;

        var goingDark = Application.Current.RequestedThemeVariant != ThemeVariant.Dark;
        Application.Current.RequestedThemeVariant = goingDark ? ThemeVariant.Dark : ThemeVariant.Light;

        if (sender is ToggleButton toggle)
        {
            toggle.Content = goingDark ? "☀  Mode Terang" : "🌙  Mode Gelap";
        }
    }

    private void OnMenuShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var menu = this.FindControl<Grid>("MenuPanel")!;
        // "Menu" toggles between the hero/welcome panel and the icon-grid
        // dashboard, mirroring the legacy app's behavior where clicking
        // "Menu" opens the module grid over the home screen.
        ShowPage(menu.IsVisible ? Page.Hero : Page.Menu);
    }

    /// <summary>
    /// Single-window navigation: exactly one of Hero / Menu / PageHost (feature
    /// views like Registrasi) is visible at a time. Avoids popping separate
    /// Windows per feature, per the user's feedback that the legacy app stays
    /// in one window and swaps the view in place.
    /// </summary>
    private enum Page { Hero, Menu, Feature }

    private void ShowPage(Page page)
    {
        var hero = this.FindControl<StackPanel>("HeroPanel")!;
        var menu = this.FindControl<Grid>("MenuPanel")!;
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        hero.IsVisible = page == Page.Hero;
        menu.IsVisible = page == Page.Menu;
        pageHost.IsVisible = page == Page.Feature;
    }

    private void SetupMenuCategoryCombo(AuthenticatedUser user)
    {
        var combo = this.FindControl<ComboBox>("MenuCategoryCombo")!;
        // Single category for now — the legacy app groups ~1200 features into
        // lettered categories (A, B, C, ...); our catalog only has the 6
        // top-level modules so far, so there is one category until more are added.
        combo.ItemsSource = new List<string> { "Menu Utama — Modul Tersedia" };
        combo.SelectedIndex = 0;

        BuildDashboard(user);
    }

    private void BuildDashboard(AuthenticatedUser user)
    {
        IMenuPermissionService menuService = new MenuPermissionService(new DefaultMenuCatalog());
        var visibleItems = menuService.GetVisibleMenu(user);

        var panel = this.FindControl<WrapPanel>("DashboardPanel");
        if (panel is null) return;

        panel.Children.Clear();
        foreach (var item in visibleItems)
        {
            var button = new Button
            {
                Classes = { "menuTile" },
                Content = item.Label,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            var key = item.Key;
            button.Click += (_, _) => OnModuleTileClick(key, item.Label);
            panel.Children.Add(button);
        }
    }

    private async void OnModuleTileClick(string moduleKey, string label)
    {
        // Rekam Medis opens the Pasien (patient master data) screen — the real
        // starting point of the module, replacing the earlier SearchPickerDialog
        // demo now that a proper screen exists (see Modules/Pasien). Other
        // modules are not implemented yet in this scaffold stage.
        if (moduleKey == "rekammedis")
        {
            await OpenPasienAsync();
            return;
        }

        if (moduleKey == "pengaturan")
        {
            await OpenPengaturanAsync();
            return;
        }

        if (moduleKey == "keuangan")
        {
            await OpenKasirAsync();
            return;
        }

        if (moduleKey == "laporan")
        {
            await OpenLaporanMenuAsync();
            return;
        }

        await ShowNotImplementedAsync(label);
    }

    private async System.Threading.Tasks.Task OpenLaporanMenuAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Laporan.LaporanMenuView view)
        {
            view = new Modules.Laporan.LaporanMenuView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.TileClicked += async (_, action) => await OpenLaporanTileAsync(action);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
    }

    private async System.Threading.Tasks.Task OpenLaporanTileAsync(Modules.Laporan.LaporanAction action)
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        switch (action)
        {
            case Modules.Laporan.LaporanAction.Kunjungan:
            {
                var view = new Modules.Laporan.LaporanKunjunganView();
                view.BackRequested += async (_, _) => await OpenLaporanMenuAsync();
                pageHost.Content = view;
                await view.ReloadAsync();
                break;
            }
            case Modules.Laporan.LaporanAction.Pendapatan:
            {
                var view = new Modules.Laporan.LaporanPendapatanView();
                view.BackRequested += async (_, _) => await OpenLaporanMenuAsync();
                pageHost.Content = view;
                await view.ReloadAsync();
                break;
            }
            default:
            {
                var view = new Modules.Laporan.LaporanPenjualanFarmasiView();
                view.BackRequested += async (_, _) => await OpenLaporanMenuAsync();
                pageHost.Content = view;
                await view.ReloadAsync();
                break;
            }
        }

        ShowPage(Page.Feature);
    }

    private async System.Threading.Tasks.Task OpenPasienAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Pasien.PasienView view)
        {
            view = new Modules.Pasien.PasienView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenKasirAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Kasir.KasirView view)
        {
            view = new Modules.Kasir.KasirView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenPengaturanAsync()
    {
        // Defense in depth: the "Pengaturan" dashboard tile is already hidden
        // for non-admin users (DefaultMenuCatalog requires "pengaturan.admin"),
        // but re-check here too in case this is ever reachable another way.
        if (_user is null || !_user.HasPermission("pengaturan.admin"))
        {
            await ShowNotImplementedAsync("Pengaturan (akses ditolak — khusus admin)");
            return;
        }

        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Pengaturan.PengaturanView view)
        {
            view = new Modules.Pengaturan.PengaturanView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async void OnFeatureShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var label = (sender as Button)?.Content?.ToString() ?? "Fitur";

        switch (label)
        {
            case "Registrasi":
            case "Rawat Jalan":
                // Both shortcuts open the same "Daftar Pasien Rawat Jalan" /
                // Registrasi screen — the user corrected an earlier mix-up
                // where we'd assumed the rich list (Kode Dokter/Dokter
                // Dituju/Biaya Reg/... columns) belonged to "Registrasi";
                // it's actually reached from the legacy app's "Rawat Jalan"
                // menu. RegistrasiView's "🩺 Periksa" button is the path to
                // the vitals/examination form (RawatJalanView) for a
                // selected patient — see OpenPemeriksaanAsync below.
                await OpenRegistrasiAsync();
                return;

            case "Rawat Inap":
                await OpenRawatInapAsync();
                return;

            case "IGD/UGD":
                await OpenIgdAsync();
                return;

            case "Laboratorium":
                await OpenLabMenuAsync();
                return;

            case "Radiologi":
                await OpenRadiologiMenuAsync();
                return;

            case "Farmasi":
                await OpenFarmasiMenuAsync();
                return;
        }

        _ = ShowNotImplementedAsync(label);
    }

    private async System.Threading.Tasks.Task OpenFarmasiMenuAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Farmasi.FarmasiMenuView view)
        {
            view = new Modules.Farmasi.FarmasiMenuView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.TileClicked += async (_, action) => await OpenFarmasiTileAsync(action);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
    }

    private async System.Threading.Tasks.Task OpenFarmasiTileAsync(Modules.Farmasi.FarmasiAction action)
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        switch (action)
        {
            case Modules.Farmasi.FarmasiAction.InputPenjualan:
            {
                var view = new Modules.Farmasi.InputPenjualanView();
                view.BackRequested += async (_, _) => await OpenFarmasiMenuAsync();
                pageHost.Content = view;
                await view.ReloadAsync();
                break;
            }
            case Modules.Farmasi.FarmasiAction.DataPenjualan:
            {
                var view = new Modules.Farmasi.DataPenjualanView();
                view.BackRequested += async (_, _) => await OpenFarmasiMenuAsync();
                pageHost.Content = view;
                await view.ReloadAsync();
                break;
            }
            case Modules.Farmasi.FarmasiAction.DaftarResepDokter:
            {
                var view = new Modules.Farmasi.DaftarResepView();
                view.BackRequested += async (_, _) => await OpenFarmasiMenuAsync();
                pageHost.Content = view;
                await view.ReloadAsync();
                break;
            }
            default:
                // "Data Penyerahan Darah" / "No. Resep" — deliberately deferred, see
                // FarmasiMenuView.axaml's doc comment and docs/audit/HANDOVER.md.
                await ShowNotImplementedAsync(action == Modules.Farmasi.FarmasiAction.DataPenyerahanDarah
                    ? "Data Penyerahan Darah" : "No. Resep");
                return;
        }

        ShowPage(Page.Feature);
    }

    private async System.Threading.Tasks.Task OpenLabMenuAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Laboratorium.LabMenuView view)
        {
            view = new Modules.Laboratorium.LabMenuView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.TileClicked += async (_, action) => await OpenLabTileAsync(action);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
    }

    private async System.Threading.Tasks.Task OpenLabTileAsync(Modules.Laboratorium.LabAction action)
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;
        var kategori = Modules.Laboratorium.LabMenuView.KategoriFor(action);

        if (Modules.Laboratorium.LabMenuView.IsPermintaan(action))
        {
            var view = new Modules.Laboratorium.LabPermintaanView();
            view.SetKategori(kategori);
            view.BackRequested += async (_, _) => await OpenLabMenuAsync();
            pageHost.Content = view;
        }
        else
        {
            var view = new Modules.Laboratorium.LabPeriksaView();
            view.SetKategori(kategori);
            view.BackRequested += async (_, _) => await OpenLabMenuAsync();
            pageHost.Content = view;
            await view.ReloadAsync();
        }

        ShowPage(Page.Feature);
    }

    private async System.Threading.Tasks.Task OpenRadiologiMenuAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Radiologi.RadiologiMenuView view)
        {
            view = new Modules.Radiologi.RadiologiMenuView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.TileClicked += async (_, action) => await OpenRadiologiTileAsync(action);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
    }

    private async System.Threading.Tasks.Task OpenRadiologiTileAsync(Modules.Radiologi.RadiologiAction action)
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (action == Modules.Radiologi.RadiologiAction.Permintaan)
        {
            var view = new Modules.Radiologi.PermintaanRadiologiView();
            view.BackRequested += async (_, _) => await OpenRadiologiMenuAsync();
            pageHost.Content = view;
            await view.ReloadAsync();
        }
        else
        {
            var view = new Modules.Radiologi.PeriksaRadiologiView();
            view.BackRequested += async (_, _) => await OpenRadiologiMenuAsync();
            pageHost.Content = view;
            await view.ReloadAsync();
        }

        ShowPage(Page.Feature);
    }

    private async System.Threading.Tasks.Task OpenIgdAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Igd.IgdView view)
        {
            view = new Modules.Igd.IgdView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenRawatInapAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.RawatInap.RawatInapView view)
        {
            view = new Modules.RawatInap.RawatInapView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenRegistrasiAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Registrasi.RegistrasiView view)
        {
            view = new Modules.Registrasi.RegistrasiView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.PeriksaRequested += async (_, _) => await OpenRawatJalanAsync();
            view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenRawatJalanAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.RawatJalan.RawatJalanView view)
        {
            view = new Modules.RawatJalan.RawatJalanView();
            // "Kembali" from the exam form returns to the patient list it was
            // opened from, not all the way home — matches how "🩺 Periksa" got here.
            view.BackRequested += async (_, _) => await OpenRegistrasiAsync();
            view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private void OnTopShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var label = (sender as Button)?.Content?.ToString() ?? "Fitur";

        switch (label)
        {
            case "Informasi":
                _ = OpenInformasiAsync();
                return;

            case "Tentang Program":
                OpenAbout();
                return;
        }

        _ = ShowNotImplementedAsync(label);
    }

    private async System.Threading.Tasks.Task OpenInformasiAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Informasi.InformasiView view)
        {
            view = new Modules.Informasi.InformasiView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private void OpenAbout()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.About.AboutView view)
        {
            view = new Modules.About.AboutView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
    }

    private void OnExitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    /// <summary>
    /// Handles the right-click patient menu shared by Registrasi/IGD/Rawat
    /// Inap/Rawat Jalan (Modules/Shared/PatientContextMenu) — a handful of
    /// items route to a real screen (prefilled with that patient's
    /// No.Rawat/No.RM), the rest are still "belum diimplementasikan" like
    /// every other unbuilt menu item in this app. Add real cases here as
    /// more screens are built — see docs/audit/HANDOVER.md.
    /// </summary>
    private async System.Threading.Tasks.Task HandlePatientMenuAsync(Modules.Shared.PatientMenuRequest request)
    {
        switch (request.Action)
        {
            case Modules.Shared.PatientMenuAction.DataRekamMedis:
                await OpenPasienAsync();
                if (this.FindControl<ContentControl>("PageHost")!.Content is Modules.Pasien.PasienView pasienView)
                    await pasienView.SearchForAsync(request.NoRekamMedis);
                return;

            case Modules.Shared.PatientMenuAction.KamarInap:
                await OpenRawatInapAsync();
                if (this.FindControl<ContentControl>("PageHost")!.Content is Modules.RawatInap.RawatInapView rawatInapView)
                    rawatInapView.PrefillAdmit(request.NoRawat);
                return;

            case Modules.Shared.PatientMenuAction.TindakanDanPemeriksaan:
                await OpenRawatJalanAsync();
                return;

            case Modules.Shared.PatientMenuAction.BillingPembayaran:
                await OpenKasirAsync();
                if (this.FindControl<ContentControl>("PageHost")!.Content is Modules.Kasir.KasirView kasirView)
                    await kasirView.SearchForAsync(request.NoRawat);
                return;
        }

        await ShowNotImplementedAsync($"\"{request.Action}\" untuk {request.NamaPasien}");
    }

    private async System.Threading.Tasks.Task ShowNotImplementedAsync(string label)
    {
        var dialog = new Window
        {
            Width = 320,
            Height = 140,
            Title = label,
            Content = new TextBlock
            {
                Text = $"\"{label}\" belum diimplementasikan di tahap scaffold ini.",
                Margin = new Thickness(16),
                TextWrapping = TextWrapping.Wrap,
            }
        };
        await dialog.ShowDialog(this);
    }
}
