using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private AuthenticatedUser? _user;
    private bool _pemeriksaanFromTindakan;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(AuthenticatedUser? user)
    {
        InitializeComponent();
        _componentReady = true;
        this.FindControl<Button>("MaximizeButton")!.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
        _user = user;
        AppSession.CurrentUser = user;

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

        ShowPage(Page.Menu);
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
        // The legacy home screen IS the Menu Utama grid, so "home" (Hero) now resolves to it.
        if (page == Page.Hero) page = Page.Menu;

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
        combo.ItemsSource = LegacyMenuCatalog.Categories;
        combo.SelectedIndex = 0;
        combo.SelectionChanged += (_, _) => BuildDashboard();
        this.FindControl<TextBox>("MenuSearchBox")!.TextChanged += (_, _) => BuildDashboard();
        this.FindControl<ScrollViewer>("DashboardScroll")!.SizeChanged += (_, _) => LayoutDashboardTiles();

        BuildDashboard();
    }

    private const int DashboardColumns = 5;

    /// <summary>Builds the tile grid for the selected legacy category (A–U). Icons are placeholders (initials) for now.</summary>
    private void BuildDashboard()
    {
        var panel = this.FindControl<UniformGrid>("DashboardPanel");
        var combo = this.FindControl<ComboBox>("MenuCategoryCombo");
        if (panel is null || combo is null || combo.SelectedIndex < 0) return;

        panel.Children.Clear();

        // Legacy frmUtama TCari: a search term looks across every category.
        var cari = this.FindControl<TextBox>("MenuSearchBox")?.Text?.Trim() ?? "";
        var source = cari.Length == 0
            ? LegacyMenuCatalog.Tiles[combo.SelectedIndex]
            : LegacyMenuCatalog.Tiles.SelectMany(t => t)
                .Where(t => t.Label.Contains(cari, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(t => t.Key)
                .ToArray();

        var shown = 0;
        foreach (var (key, label) in source)
        {
            // Same per-user hak akses filter as legacy frmUtama (akses.getXxx()).
            if (_user is not null && !LegacyTilePermissions.CanSeeTile(_user, key)) continue;
            shown++;

            var icon = new Border
            {
                Width = 48, Height = 48, CornerRadius = new CornerRadius(12),
                Child = new TextBlock
                {
                    Text = Initials(label), Foreground = Brushes.White, FontWeight = FontWeight.Bold, FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                },
            };
            icon.Bind(Border.BackgroundProperty, this.GetResourceObservable("ThemePrimaryLightBrush"));
            var caption = new TextBlock
            {
                Text = label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 12,
                MaxWidth = 220,
            };

            var button = new Button
            {
                Classes = { "menuTile" },
                Width = double.NaN, Height = double.NaN, MinHeight = 120,
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Content = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Children = { icon, caption } },
            };
            var k = key;
            var l = label;
            button.Click += (_, _) => OnLegacyTileClick(k, l);
            panel.Children.Add(button);
        }

        var count = this.FindControl<TextBlock>("MenuCountText");
        if (count is not null)
            count.Text = shown == 0
                ? (cari.Length > 0 ? "Tidak ada menu yang cocok / tidak punya akses" : "Tidak ada menu yang bisa diakses di kategori ini")
                : $"{shown} menu";

        LayoutDashboardTiles();
    }

    // Tiles always span the full width (5 columns); rows stretch toward the viewport height
    // (clamped 120–220px) so a small category fills the area instead of leaving a void.
    private void LayoutDashboardTiles()
    {
        var panel = this.FindControl<UniformGrid>("DashboardPanel");
        var scroll = this.FindControl<ScrollViewer>("DashboardScroll");
        if (panel is null || scroll is null || panel.Children.Count == 0) return;

        var rows = (int)Math.Ceiling(panel.Children.Count / (double)DashboardColumns);
        var tileHeight = Math.Clamp(scroll.Bounds.Height / rows, 120, 220);
        foreach (var child in panel.Children)
        {
            if (child is Button b) b.Height = tileHeight - b.Margin.Top - b.Margin.Bottom;
        }
    }

    private static string Initials(string label)
    {
        var words = label.Split([' ', '/', '&', '-', '(', ')', '.', ','], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "?";
        return words.Length == 1
            ? words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant()
            : (words[0][..1] + words[1][..1]).ToUpperInvariant();
    }

    /// <summary>
    /// Routes a legacy Menu Utama tile (by legacy button field name) to the screen
    /// that exists for it; everything else is a placeholder for now.
    /// </summary>
    private async void OnLegacyTileClick(string key, string label)
    {
        switch (key)
        {
            case "btnAnalisaKamar": await OpenMenuAInformasiAsync(false); return;
            case "BtnJadwal": await OpenMenuAInformasiAsync(true); return;
            case "btnBookingPeriksa": await OpenBookingMenuAAsync("Booking Periksa"); return;
            case "btnBookingRegistrasi": await OpenBookingMenuAAsync("Booking Registrasi"); return;
            case "btnBookingMCUPerusahaan": await OpenBookingMenuAAsync("Booking MCU Perusahaan"); return;
            case "BtnDpjp": await OpenRanapMenuAsync("DPJP Ranap"); return;
            case "btnRanap": await OpenRanapMenuAsync("Tindakan Ranap"); return;
            case "btnTagihanOperasi": await OpenRanapMenuAsync("Operasi/VK"); return;
            case "btnRujukPasien": await OpenRanapMenuAsync("Rujukan Keluar"); return;
            case "btnRujukMasuk": await OpenRanapMenuAsync("Rujukan Masuk"); return;
            case "btnBeriObat": await OpenRanapMenuAsync("Beri Obat/BHP"); return;
            case "btnRegistrasi": await OpenRegistrasiAsync(); return;
            case "btnIGD": await OpenIgdAsync(); return;
            case "btnRalan": await OpenTindakanRalanAsync(); return;
            case "btnKamarInap": await OpenRawatInapAsync(); return;
            case "btnPasien": await OpenPasienAsync(); return;
            case "btnKasir": await OpenKasirAsync(); return;

            case "btnPermintaanLab": await OpenLabTileAsync(Modules.Laboratorium.LabAction.PermintaanPk); return;
            case "btnPermintaanLabPA": await OpenLabTileAsync(Modules.Laboratorium.LabAction.PermintaanPa); return;
            case "btnPermintaanLabMB": await OpenLabTileAsync(Modules.Laboratorium.LabAction.PermintaanMb); return;
            case "btnLaboratorium": await OpenLabTileAsync(Modules.Laboratorium.LabAction.PeriksaPk); return;
            case "btnLaboratoriumPA": await OpenLabTileAsync(Modules.Laboratorium.LabAction.PeriksaPa); return;
            case "btnLaboratoriumMB": await OpenLabTileAsync(Modules.Laboratorium.LabAction.PeriksaMb); return;

            case "btnPermintaanRadiologi": await OpenRadiologiTileAsync(Modules.Radiologi.RadiologiAction.Permintaan); return;
            case "btnPeriksaRadiologi": await OpenRadiologiTileAsync(Modules.Radiologi.RadiologiAction.Periksa); return;

            case "btnPenjualan": await OpenFarmasiTileAsync(Modules.Farmasi.FarmasiAction.InputPenjualan); return;
            case "btnDaftarPermintaanResep": await OpenFarmasiTileAsync(Modules.Farmasi.FarmasiAction.DaftarResepDokter); return;
            case "btnResepObat": await OpenFarmasiTileAsync(Modules.Farmasi.FarmasiAction.NoResep); return;

            // [B] Tindakan & Obat via Barcode, Lab Kesehatan Lingkungan
            case "btnBarcodeRalan": await OpenPageAsync(new Modules.Barcode.BarcodeView(ranap: false)); return;
            case "btnBarcodeRanap": await OpenPageAsync(new Modules.Barcode.BarcodeView(ranap: true)); return;
            case "btnLabKeslingPelanggan": await OpenPageAsync(new Modules.Shared.MasterView(SimrsKhanza.Data.LabKesling.LabKeslingMasters.Pelanggan)); return;
            case "btnLabKeslingSampelBakuMutu": await OpenPageAsync(new Modules.Shared.MasterView(SimrsKhanza.Data.LabKesling.LabKeslingMasters.Sampel)); return;
            case "btnLabKeslingParameterPengujian": await OpenPageAsync(new Modules.Shared.MasterView(SimrsKhanza.Data.LabKesling.LabKeslingMasters.Parameter)); return;
            case "btnLabKeslingNilaiNormalBakuMutu": await OpenPageAsync(new Modules.Shared.MasterView(SimrsKhanza.Data.LabKesling.LabKeslingMasters.NilaiNormal)); return;
            case "btnLabKeslingPermintaanPengujianSampel": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.Permintaan); return;
            case "btnLabKeslingPengujianSampelTidakDapatDilayani": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.TidakDilayani); return;
            case "btnLabKeslingPengujianSampelDapatDilayani": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.Dilayani); return;
            case "btnLabKeslingPenugasanPengujianSampel": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.Penugasan); return;
            case "btnLabKeslingHasilPengujianSampel": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.Hasil); return;
            case "btnLabKeslingVerifikasiPengujianSampel": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.Verifikasi); return;
            case "btnLabKeslingValidasiPengujianSampel": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.Validasi); return;
            case "btnLabKeslingRekapPelayanan": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.RekapPelayanan); return;
            case "btnLabKeslingPembyaranPengujianSampel": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.Pembayaran); return;
            case "btnLabKeslingRekapPembayaran": await OpenStageAsync(Modules.LabKesling.LabKeslingStage.RekapPembayaran); return;

            case "btnSetupAplikasi": await OpenPengaturanAsync(); return;
            case "btnDisplay": await OpenPageAsync(new Modules.Pengaturan.MenuUDisplayView(apotek: false)); return;
            case "btnDisplayApotek": await OpenPageAsync(new Modules.Pengaturan.MenuUDisplayView(apotek: true)); return;
            case "btnPasswordAsuransi": await OpenPageAsync(new Modules.Pengaturan.MenuUKredensialView(eksekutif: false)); return;
            case "btnEEksekutif":
                if (await RequireAdminUtamaAsync(label))
                    await OpenPageAsync(new Modules.Pengaturan.MenuUKredensialView(eksekutif: true));
                return;
            case "btnVakum":
                if (await RequireAdminUtamaAsync(label))
                    await OpenPageAsync(new Modules.Pengaturan.MenuUVakumView());
                return;
            case "btnUser":
            {
                if (!await RequireAdminUtamaAsync(label)) return;
                var v = new Modules.Akun.ManajemenUserView();
                v.BackRequested += (_, _) => ShowPage(Page.Menu);
                await OpenFeatureAsync(v, v.ReloadAsync);
                return;
            }
            case "btnAdmin":
            {
                if (!await RequireAdminUtamaAsync(label)) return;
                var v = new Modules.Akun.AdminView();
                v.BackRequested += (_, _) => ShowPage(Page.Menu);
                await OpenFeatureAsync(v, v.ReloadAsync);
                return;
            }
            case "btnTracker":
            {
                var v = new Modules.Akun.TrackerLoginView();
                v.BackRequested += (_, _) => ShowPage(Page.Menu);
                await OpenFeatureAsync(v, v.ReloadAsync);
                return;
            }

            // [C] Presensi, Manajemen & Penggajian Pegawai (most tiles are data — see MenuCSpecs below)
            case "btnPemesanan": await OpenPageAsync(new Modules.Farmasi.PenerimaanView()); return;
            case "btnPenggajian": await OpenPageAsync(new Modules.Kepegawaian.PenggajianView()); return;
            case "btnSKPPenilaianPegawai": await OpenPageAsync(new Modules.Kepegawaian.SkpPenilaianView()); return;
            case "btnSKPRekapitulasiPenilaian": await OpenPageAsync(new Modules.Kepegawaian.SkpRekapView()); return;

            // Our own report screens (kunjungan/pendapatan/penjualan farmasi) have no 1:1 legacy tile; hang the menu here.
            case "btnLaporanBulananIRJ": await OpenLaporanMenuAsync(); return;
        }

        if (SimrsKhanza.Data.Pengaturan.MenuUSpecs.Tiles.TryGetValue(key, out var menuU))
        {
            await OpenMenuUAsync(label, menuU);
            return;
        }

        if (SimrsKhanza.Data.MenuA.MenuASpecs.Tiles.TryGetValue(key, out var menuA))
        {
            var page = new Modules.Informasi.MenuAView(menuA);
            page.SesiKfrRequested += async (_, noRawat) =>
            {
                var sesi = new Modules.Informasi.MenuAView(SimrsKhanza.Data.MenuA.MenuASpecs.SesiKfr, noRawat);
                sesi.BackRequested += async (_, _) => await OpenFeatureAsync(page, page.ReloadAsync);
                await OpenFeatureAsync(sesi, sesi.ReloadAsync);
            };
            await OpenPageAsync(page);
            return;
        }

        // [D] Farmasi stock transactions; its masters & reports are data (MenuDSpecs) like [C].
        var stokMode = key switch
        {
            "btnMutasiBarang" => SimrsKhanza.Data.Farmasi.StokMode.Mutasi,
            "btnOpname" => SimrsKhanza.Data.Farmasi.StokMode.Opname,
            "btnStokKeluarMedis" => SimrsKhanza.Data.Farmasi.StokMode.StokKeluar,
            "btnPermintaanMedis" => (SimrsKhanza.Data.Farmasi.StokMode?)SimrsKhanza.Data.Farmasi.StokMode.Permintaan,
            _ => null,
        };
        if (stokMode is { } sm)
        {
            await OpenPageAsync(new Modules.Farmasi.StokTransaksiView(sm));
            return;
        }
        if (key == "btnPermintaanStokObatPasien")
        {
            await OpenPageAsync(new Modules.Farmasi.PermintaanStokPasienView());
            return;
        }
        if (SimrsKhanza.Data.Farmasi.MenuDLists.Masters.TryGetValue(key, out var listD))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(listD));
            return;
        }
        if (SimrsKhanza.Data.Farmasi.MenuDLists.Reports.TryGetValue(key, out var listReportD))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(listReportD));
            return;
        }
        if (SimrsKhanza.Data.Farmasi.MenuDSpecs.Masters.TryGetValue(key, out var masterD))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterD));
            return;
        }
        if (SimrsKhanza.Data.Farmasi.MenuDSpecs.Reports.TryGetValue(key, out var reportD))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportD));
            return;
        }

        if (SimrsKhanza.Data.Kepegawaian.MenuCSpecs.Masters.TryGetValue(key, out var master))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(master));
            return;
        }
        if (SimrsKhanza.Data.Kepegawaian.MenuCSpecs.Reports.TryGetValue(key, out var report))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(report));
            return;
        }

        await ShowNotImplementedAsync(label);
    }

    private async System.Threading.Tasks.Task OpenFeatureAsync(Control view, Func<System.Threading.Tasks.Task> reload)
    {
        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await reload();
    }

    private async System.Threading.Tasks.Task OpenPageAsync(Modules.Shared.PageView view)
    {
        view.BackRequested += (_, _) => ShowPage(Page.Menu);
        await OpenFeatureAsync(view, view.ReloadAsync);
    }

    private System.Threading.Tasks.Task OpenStageAsync(Modules.LabKesling.LabKeslingStage stage)
        => OpenPageAsync(new Modules.LabKesling.LabKeslingStageView(stage));

    private async Task OpenMenuUAsync(string label, SimrsKhanza.Data.Master.MasterSpec[] sections)
    {
        if (sections.Length == 1)
        {
            await OpenPageAsync(new Modules.Pengaturan.MenuUMasterView(sections[0]));
            return;
        }

        var hub = new Modules.Pengaturan.MenuUHubView(label, sections);
        hub.SectionRequested += async (_, spec) =>
        {
            var page = new Modules.Pengaturan.MenuUMasterView(spec);
            page.BackRequested += (_, _) =>
            {
                this.FindControl<ContentControl>("PageHost")!.Content = hub;
                ShowPage(Page.Feature);
            };
            await OpenFeatureAsync(page, page.ReloadAsync);
        };
        await OpenPageAsync(hub);
    }

    private async System.Threading.Tasks.Task<bool> RequireAdminUtamaAsync(string label)
    {
        if (_user?.IsAdminUtama == true) return true;
        await Modules.Shared.Dialogs.InfoAsync(this, label, "Menu ini khusus Admin Utama (akun dari tabel admin).");
        return false;
    }

    private async System.Threading.Tasks.Task OpenMenuAInformasiAsync(bool jadwal)
    {
        var view = new Modules.Informasi.MenuAInformasiView(jadwal);
        view.BackRequested += (_, _) => ShowPage(Page.Menu);
        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenBookingMenuAAsync(string jenis)
    {
        var view = new Modules.Informasi.BookingMenuAView(jenis);
        view.BackRequested += (_, _) => ShowPage(Page.Menu);
        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenRanapMenuAsync(string jenis)
    {
        var view = new Modules.Informasi.RanapMenuView(jenis);
        view.BackRequested += (_, _) => ShowPage(Page.Menu);
        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
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
        // Defense in depth: Setup Aplikasi is already hidden for non-admins
        // (LegacyTilePermissions.AdminOnlyTiles), re-check in case it is reachable another way.
        if (_user is null || !_user.IsAdminUtama)
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
                // Legacy DlgReg — registration list/input.
                await OpenRegistrasiAsync();
                return;

            case "Rawat Jalan":
                // Legacy DlgRawatJalan — "Perawatan/Tindakan Rawat Jalan", a
                // different screen from Registrasi. Its "Pemeriksaan" button
                // leads to the vitals/exam form (RawatJalanView).
                await OpenTindakanRalanAsync();
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
            case Modules.Farmasi.FarmasiAction.NoResep:
            {
                var view = new Modules.Farmasi.NoResepView();
                view.BackRequested += async (_, _) => await OpenFarmasiMenuAsync();
                pageHost.Content = view;
                await view.LoadDokterAsync();
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

    private static SimrsKhanza.Data.LabCategory LabCategoryFor(Modules.Laboratorium.LabAction action) => action switch
    {
        Modules.Laboratorium.LabAction.PermintaanPa or Modules.Laboratorium.LabAction.PeriksaPa => SimrsKhanza.Data.LabCategory.PA,
        Modules.Laboratorium.LabAction.PermintaanMb or Modules.Laboratorium.LabAction.PeriksaMb => SimrsKhanza.Data.LabCategory.MB,
        _ => SimrsKhanza.Data.LabCategory.PK,
    };

    private async System.Threading.Tasks.Task OpenLabTileAsync(Modules.Laboratorium.LabAction action)
    {
        var category = LabCategoryFor(action);
        if (Modules.Laboratorium.LabMenuView.IsPermintaan(action)) await OpenPermintaanLabAsync(category);
        else await OpenPeriksaLabAsync(category);
    }

    private async System.Threading.Tasks.Task OpenPermintaanLabAsync(SimrsKhanza.Data.LabCategory category)
    {
        var view = new Modules.Laboratorium.PermintaanLabListView(category)
        {
            IsAdminUtama = _user?.IsAdminUtama == true,
        };
        view.BackRequested += (_, _) => ShowPage(Page.Hero);
        view.HasilRequested += async (_, req) =>
            await OpenPeriksaLabFormAsync(req.Category, req.NoRawat, req.NoOrder, () => OpenPermintaanLabAsync(req.Category));

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenPeriksaLabAsync(SimrsKhanza.Data.LabCategory category)
    {
        var view = new Modules.Laboratorium.PeriksaLabListView(category);
        view.BackRequested += (_, _) => ShowPage(Page.Hero);
        view.InputRequested += async (_, noRawat) =>
            await OpenPeriksaLabFormAsync(category, noRawat, null, () => OpenPeriksaLabAsync(category));

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenPeriksaLabFormAsync(
        SimrsKhanza.Data.LabCategory category, string? noRawat, string? noOrder, Func<System.Threading.Tasks.Task> back)
    {
        var view = new Modules.Laboratorium.PeriksaLabFormView(category);
        view.BackRequested += async (_, _) => await back();
        view.SavedRequested += async (_, _) => await OpenPeriksaLabAsync(category);

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        if (noRawat is not null) await view.PrefillAsync(noRawat, noOrder);
        else await view.ReloadAsync();
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
        if (action == Modules.Radiologi.RadiologiAction.Permintaan) await OpenPermintaanRadiologiAsync();
        else await OpenPeriksaRadiologiAsync();
    }

    private async System.Threading.Tasks.Task OpenPermintaanRadiologiAsync()
    {
        var view = new Modules.Radiologi.PermintaanRadiologiListView
        {
            IsAdminUtama = _user?.IsAdminUtama == true,
        };
        view.BackRequested += (_, _) => ShowPage(Page.Hero);
        view.HasilRequested += async (_, req) =>
            await OpenPeriksaRadiologiFormAsync(req.NoRawat, req.NoOrder, OpenPermintaanRadiologiAsync);

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenPeriksaRadiologiAsync()
    {
        var view = new Modules.Radiologi.PeriksaRadiologiListView();
        view.BackRequested += (_, _) => ShowPage(Page.Hero);
        view.InputRequested += async (_, noRawat) =>
            await OpenPeriksaRadiologiFormAsync(noRawat, null, OpenPeriksaRadiologiAsync);

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenPeriksaRadiologiFormAsync(
        string? noRawat, string? noOrder, Func<System.Threading.Tasks.Task> back)
    {
        var view = new Modules.Radiologi.PeriksaRadiologiFormView();
        view.BackRequested += async (_, _) => await back();
        view.SavedRequested += async (_, _) => await OpenPeriksaRadiologiAsync();

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        if (noRawat is not null) await view.PrefillAsync(noRawat, noOrder);
        else await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenIgdAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.Igd.IgdView view)
        {
            view = new Modules.Igd.IgdView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.TriaseRequested += async (_, _) => await OpenIgdTriaseAsync();
            view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenIgdTriaseAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        var view = new Modules.Igd.IgdTriaseView();
        view.BackRequested += async (_, _) => await OpenIgdAsync();
        view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
        pageHost.Content = view;

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
            view.PeriksaRequested += async (_, _) => { _pemeriksaanFromTindakan = false; await OpenRawatJalanAsync(); };
            view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenTindakanRalanAsync()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        if (pageHost.Content is not Modules.RawatJalan.TindakanRalanView view)
        {
            view = new Modules.RawatJalan.TindakanRalanView();
            view.BackRequested += (_, _) => ShowPage(Page.Hero);
            view.PemeriksaanRequested += async (_, _) => { _pemeriksaanFromTindakan = true; await OpenRawatJalanAsync(); };
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
            view.BackRequested += async (_, _) => { if (_pemeriksaanFromTindakan) await OpenTindakanRalanAsync(); else await OpenRegistrasiAsync(); };
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

            case "Anjungan & Antrian":
                if (sender is Control anchor) ShowAntrianMenu(anchor);
                return;

            case "Program":
                if (sender is Control programAnchor) ShowProgramMenu(programAnchor);
                return;
        }

        _ = ShowNotImplementedAsync(label);
    }

    /// <summary>Legacy "Program" menu: Ganti Password, Hak Akses Saya, Log Out, Keluar.</summary>
    private void ShowProgramMenu(Control anchor)
    {
        MenuItem Item(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            return item;
        }

        var menu = new ContextMenu();
        menu.Items.Add(Item("🔑 Ganti Password", () =>
        {
            if (_user is not null) _ = new Modules.Akun.GantiPasswordWindow(_user).ShowDialog(this);
        }));
        menu.Items.Add(Item("🛡 Hak Akses Saya", () => _ = ShowHakAksesSayaAsync()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("↩ Log Out", () => OnExitClick(null, new Avalonia.Interactivity.RoutedEventArgs())));
        menu.Items.Add(Item("✕ Keluar Aplikasi", Close));
        menu.Open(anchor);
    }

    private async System.Threading.Tasks.Task ShowHakAksesSayaAsync()
    {
        if (_user is null) return;
        var lines = new List<string>();
        for (var c = 0; c < LegacyMenuCatalog.Tiles.Length; c++)
        {
            var tiles = LegacyMenuCatalog.Tiles[c].Where(t => LegacyTilePermissions.CanSeeTile(_user, t.Key)).Select(t => t.Label).ToList();
            if (tiles.Count > 0) lines.Add($"{LegacyMenuCatalog.Categories[c]}  ({tiles.Count})\n   " + string.Join(", ", tiles));
        }
        var total = LegacyMenuCatalog.Tiles.Sum(t => t.Count(x => LegacyTilePermissions.CanSeeTile(_user, x.Key)));
        var win = new Window
        {
            Title = "Hak Akses Saya",
            Width = 760, Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Thickness(12),
                Children =
                {
                    new TextBlock
                    {
                        [DockPanel.DockProperty] = Dock.Top, Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeight.Medium,
                        Text = $"{_user.DisplayName} — {(_user.IsAdminUtama ? "Admin Utama (semua akses)" : "Pegawai")} — {total} menu",
                    },
                    new ScrollViewer
                    {
                        Content = new SelectableTextBlock { Text = string.Join("\n\n", lines), TextWrapping = TextWrapping.Wrap },
                    },
                },
            },
        };
        await win.ShowDialog(this);
    }

    /// <summary>Mirrors the legacy "Anjungan & Antrian" dropdown; entries without a native version yet say so.</summary>
    private void ShowAntrianMenu(Control anchor)
    {
        MenuItem Item(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            return item;
        }

        void Pending(string name) => _ = ShowNotImplementedAsync(name);
        void Board(SimrsKhanza.Data.AntrianBoardKind kind) => Modules.Antrian.AntrianBoards.Open(kind);

        var apotek = new MenuItem { Header = "Antrian Apotek" };
        apotek.Items.Add(Item("Antrian Apotek", () => Pending("Antrian Apotek")));
        apotek.Items.Add(Item("Antrian Apotek 2", () => Board(SimrsKhanza.Data.AntrianBoardKind.Apotek2)));
        apotek.Items.Add(Item("Antrian Apotek 2 Racikan", () => Board(SimrsKhanza.Data.AntrianBoardKind.Apotek2Racikan)));
        apotek.Items.Add(Item("Antrian Apotek 2 Non Racikan", () => Board(SimrsKhanza.Data.AntrianBoardKind.Apotek2NonRacikan)));

        var laborat = new MenuItem { Header = "Antrian Laborat" };
        laborat.Items.Add(Item("Laborat Patologi Klinis", () => Board(SimrsKhanza.Data.AntrianBoardKind.LabPk)));
        laborat.Items.Add(Item("Laborat Patologi Anatomi", () => Board(SimrsKhanza.Data.AntrianBoardKind.LabPa)));
        laborat.Items.Add(Item("Laborat Mikrobiologi & Bio Molekuler", () => Board(SimrsKhanza.Data.AntrianBoardKind.LabMb)));

        var menu = new ContextMenu();
        menu.Items.Add(Item("Anjungan Registrasi Mandiri", () => Pending("Anjungan Registrasi Mandiri")));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Cetak Antrian Loket", () => Pending("Cetak Antrian Loket")));
        menu.Items.Add(Item("Antrian Loket", Modules.Antrian.AntrianBoards.OpenLoket));
        menu.Items.Add(Item("Antrian Loket 2", () => Pending("Antrian Loket 2")));
        menu.Items.Add(Item("Antrian Unit/Poliklinik", () => Pending("Antrian Unit/Poliklinik")));
        menu.Items.Add(Item("Antrian Unit/Poliklinik 2", () => Board(SimrsKhanza.Data.AntrianBoardKind.JadwalPoli)));
        menu.Items.Add(apotek);
        menu.Items.Add(laborat);
        menu.Items.Add(Item("Antrian Radiologi", () => Board(SimrsKhanza.Data.AntrianBoardKind.Radiologi)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Verifikasi Digital Klaim", () => Pending("Verifikasi Digital Klaim")));

        menu.Open(anchor);
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

    private bool _componentReady;

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // Buttons mark their own presses handled, so this only fires on the empty strip:
    // drag to move (the OS move loop gives edge snapping), double-click to maximize/restore.
    private void OnTitleBarPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) ToggleMaximize();
        else BeginMoveDrag(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // WindowState is set from XAML during InitializeComponent, before the name scope exists.
        if (change.Property == WindowStateProperty && _componentReady)
            this.FindControl<Button>("MaximizeButton")!.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
    }

    /// <summary>Raised by the toolbar "Keluar" button — App swaps in the login window instead of quitting.</summary>
    public event EventHandler? LogoutRequested;

    private void OnExitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LogoutRequested is null) Close();
        else LogoutRequested.Invoke(this, EventArgs.Empty);
    }


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
