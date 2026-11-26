using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI;

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
        Closed += (_, _) => _clockTimer.Stop();
        UpdateClock();

        this.FindControl<ToggleButton>("ThemeToggle")!.Content =
            Application.Current?.RequestedThemeVariant == ThemeVariant.Dark ? "Mode Terang" : "Mode Gelap";

        if (user is not null)
        {
            SetupMenuCategoryCombo(user);
        }

        this.FindControl<ContentControl>("PageHost")!.PropertyChanged += (_, args) =>
        {
            if (args.Property == ContentControl.ContentProperty) UpdateShortcutAppearance();
        };
        ShowPage(Page.Menu);
    }

    private void UpdateClock()
    {
        var clock = this.FindControl<TextBlock>("ClockText");
        if (clock is not null)
        {
            clock.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss", System.Globalization.CultureInfo.GetCultureInfo("id-ID"));
        }
    }

    private void OnThemeToggleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is null) return;

        var goingDark = Application.Current.RequestedThemeVariant != ThemeVariant.Dark;
        Application.Current.RequestedThemeVariant = goingDark ? ThemeVariant.Dark : ThemeVariant.Light;

        if (sender is ToggleButton toggle)
        {
            toggle.Content = goingDark ? "Mode Terang" : "Mode Gelap";
        }
    }

    private void OnMenuShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowPage(Page.Menu);
    }

    /// <summary>
    /// The dashboard and feature views share the same window.
    /// </summary>
    private enum Page { Menu, Feature }

    private void ShowPage(Page page)
    {
        var menu = this.FindControl<Grid>("MenuPanel")!;
        var pageHost = this.FindControl<ContentControl>("PageHost")!;

        menu.IsVisible = page == Page.Menu;
        pageHost.IsVisible = page == Page.Feature;
        UpdateShortcutAppearance();
    }

    private void UpdateShortcutAppearance()
    {
        var pageHost = this.FindControl<ContentControl>("PageHost")!;
        var module = pageHost.IsVisible ? pageHost.Content?.GetType().Namespace : null;
        var active = module switch
        {
            "SimrsGramedika.UI.Modules.Registrasi" => "Registrasi",
            "SimrsGramedika.UI.Modules.Igd" => "IGD/UGD",
            "SimrsGramedika.UI.Modules.Laboratorium" => "Laboratorium",
            "SimrsGramedika.UI.Modules.Radiologi" => "Radiologi",
            "SimrsGramedika.UI.Modules.Farmasi" => "Farmasi",
            "SimrsGramedika.UI.Modules.RawatInap" => "Rawat Inap",
            "SimrsGramedika.UI.Modules.RawatJalan" => "Rawat Jalan",
            _ => pageHost.IsVisible ? null : "Menu Utama",
        };
        pageHost.Classes.Set("biruTegas", (active is not null and not "Menu Utama") ||
            module is "SimrsGramedika.UI.Modules.Informasi" or "SimrsGramedika.UI.Modules.About");
        foreach (var button in this.FindControl<WrapPanel>("FeatureShortcuts")!.Children.OfType<Button>())
            button.Classes.Set("current", active is not null && Equals(button.Tag, active));
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

    private void BuildDashboard()
    {
        var panel = this.FindControl<UniformGrid>("DashboardPanel");
        var combo = this.FindControl<ComboBox>("MenuCategoryCombo");
        if (panel is null || combo is null || combo.SelectedIndex < 0) return;

        panel.Children.Clear();

        // Legacy frmUtama TCari: a search term looks across every category.
        var cari = this.FindControl<TextBox>("MenuSearchBox")?.Text?.Trim() ?? "";
        var source = cari.Length == 0
            ? LegacyMenuCatalog.Tiles[combo.SelectedIndex].Select(t => (t.Key, t.Label, Category: combo.SelectedIndex))
            : LegacyMenuCatalog.Tiles.SelectMany((tiles, category) => tiles.Select(t => (t.Key, t.Label, Category: category)))
                .Where(t => t.Label.Contains(cari, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(t => t.Key);

        var shown = 0;
        foreach (var (key, label, category) in source)
        {
            // Same per-user hak akses filter as legacy frmUtama (akses.getXxx()).
            if (_user is not null && !LegacyTilePermissions.CanSeeTile(_user, key)) continue;
            shown++;

            var icon = DashboardIcons.Create(category, key);
            var caption = new TextBlock
            {
                Text = label, TextWrapping = TextWrapping.Wrap,
            };
            var tileBody = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(12),
                Children = { icon, caption },
            };
            var button = new Button
            {
                Classes = { "menuTile" },
                Content = tileBody,
            };
            Avalonia.Automation.AutomationProperties.SetName(button, label);
            button.Click += (_, _) => OnLegacyTileClick(key, label);
            panel.Children.Add(button);
        }

        this.FindControl<TextBlock>("MenuCountText")!.Text = $"{shown} menu";
        var empty = this.FindControl<TextBlock>("EmptyMenuText")!;
        empty.Text = cari.Length > 0 ? "Tidak ada menu yang cocok dengan pencarian." : "Tidak ada menu yang bisa diakses di kategori ini.";
        empty.IsVisible = shown == 0;

        LayoutDashboardTiles();
    }

    private void LayoutDashboardTiles()
    {
        var panel = this.FindControl<UniformGrid>("DashboardPanel");
        var scroll = this.FindControl<ScrollViewer>("DashboardScroll");
        if (panel is null || scroll is null || scroll.Bounds.Width <= 0) return;
        panel.Columns = Math.Clamp((int)(scroll.Bounds.Width / 188), 2, 7);
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
            case "btnLabKeslingPelanggan": await OpenPageAsync(new Modules.Shared.MasterView(SimrsGramedika.Data.LabKesling.LabKeslingMasters.Pelanggan)); return;
            case "btnLabKeslingSampelBakuMutu": await OpenPageAsync(new Modules.Shared.MasterView(SimrsGramedika.Data.LabKesling.LabKeslingMasters.Sampel)); return;
            case "btnLabKeslingParameterPengujian": await OpenPageAsync(new Modules.Shared.MasterView(SimrsGramedika.Data.LabKesling.LabKeslingMasters.Parameter)); return;
            case "btnLabKeslingNilaiNormalBakuMutu": await OpenPageAsync(new Modules.Shared.MasterView(SimrsGramedika.Data.LabKesling.LabKeslingMasters.NilaiNormal)); return;
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
            case LegacyTilePermissions.DatabaseBackupTile:
                if (_user is not null && ReferenceEquals(_user, AppSession.CurrentUser))
                    await OpenPageAsync(new Modules.Pengaturan.DatabaseBackupView());
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
            case "btnKedatanganPasienPerJam":
                await OpenPageAsync(new Modules.Shared.ReportView(SimrsGramedika.Data.Master.VisitReportSpecs.HourlyArrivals));
                return;
            case "btnSensusHarianPoli":
                await OpenPageAsync(new Modules.Shared.ReportView(SimrsGramedika.Data.Master.VisitReportSpecs.DailyClinicCensus));
                return;
            case "btnKunjunganRalan":
                await OpenPageAsync(new Modules.Shared.ReportView(SimrsGramedika.Data.Master.VisitReportSpecs.OutpatientVisits));
                return;
            case "btnKunjunganRanap":
                await OpenPageAsync(new Modules.Shared.ReportView(SimrsGramedika.Data.Master.VisitReportSpecs.InpatientVisits));
                return;
            case "btnRekapKunjungan":
                await OpenPageAsync(new Modules.Shared.ReportView(SimrsGramedika.Data.Master.VisitReportSpecs.MonthlyVisitSummary));
                return;
        }

        if (SimrsGramedika.Data.Pengaturan.MenuUSpecs.Tiles.TryGetValue(key, out var menuU))
        {
            await OpenMenuUAsync(label, menuU);
            return;
        }

        if (SimrsGramedika.Data.MenuA.MenuASpecs.Tiles.TryGetValue(key, out var menuA))
        {
            var page = new Modules.Informasi.MenuAView(menuA);
            page.SesiKfrRequested += async (_, noRawat) =>
            {
                var sesi = new Modules.Informasi.MenuAView(SimrsGramedika.Data.MenuA.MenuASpecs.SesiKfr, noRawat);
                sesi.BackRequested += async (_, _) => await OpenFeatureAsync(page, page.ReloadAsync);
                await OpenFeatureAsync(sesi, sesi.ReloadAsync);
            };
            await OpenPageAsync(page);
            return;
        }

        // [D] Farmasi stock transactions; its masters & reports are data (MenuDSpecs) like [C].
        var stokMode = key switch
        {
            "btnMutasiBarang" => SimrsGramedika.Data.Farmasi.StokMode.Mutasi,
            "btnOpname" => SimrsGramedika.Data.Farmasi.StokMode.Opname,
            "btnStokKeluarMedis" => SimrsGramedika.Data.Farmasi.StokMode.StokKeluar,
            "btnPermintaanMedis" => (SimrsGramedika.Data.Farmasi.StokMode?)SimrsGramedika.Data.Farmasi.StokMode.Permintaan,
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
        if (SimrsGramedika.Data.Farmasi.MenuDLists.Masters.TryGetValue(key, out var listD))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(listD));
            return;
        }
        if (SimrsGramedika.Data.Farmasi.MenuDLists.Reports.TryGetValue(key, out var listReportD))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(listReportD));
            return;
        }
        if (SimrsGramedika.Data.Farmasi.MenuDSpecs.Masters.TryGetValue(key, out var masterD))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterD));
            return;
        }
        if (SimrsGramedika.Data.Farmasi.MenuDSpecs.Reports.TryGetValue(key, out var reportD))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportD));
            return;
        }

        if (SimrsGramedika.Data.Kepegawaian.MenuCSpecs.Masters.TryGetValue(key, out var master))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(master));
            return;
        }
        if (SimrsGramedika.Data.Kepegawaian.MenuCSpecs.Reports.TryGetValue(key, out var report))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(report));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuJSpecs.Masters.TryGetValue(key, out var masterJ))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterJ));
            return;
        }
        if (SimrsGramedika.Data.Master.MenuJSpecs.Reports.TryGetValue(key, out var reportJ))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportJ));
            return;
        }

        if (SimrsGramedika.Data.Inventaris.MenuESpecs.Masters.TryGetValue(key, out var logisticsMaster)
            || SimrsGramedika.Data.Inventaris.MenuFSpecs.Masters.TryGetValue(key, out logisticsMaster))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(logisticsMaster));
            return;
        }
        if (SimrsGramedika.Data.Inventaris.MenuESpecs.Reports.TryGetValue(key, out var logisticsReport)
            || SimrsGramedika.Data.Inventaris.MenuFSpecs.Reports.TryGetValue(key, out logisticsReport))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(logisticsReport));
            return;
        }

        if (SimrsGramedika.Data.Inventaris.MenuGSpecs.Masters.TryGetValue(key, out var masterG))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterG));
            return;
        }
        if (SimrsGramedika.Data.Inventaris.MenuGSpecs.Reports.TryGetValue(key, out var reportG))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportG));
            return;
        }

        if (SimrsGramedika.Data.Parkir.MenuHSpecs.Masters.TryGetValue(key, out var masterH))
        {
            await OpenPageAsync(key == SimrsGramedika.Data.Parkir.MenuHSpecs.JenisParkirKey
                ? new Modules.Parkir.JenisParkirView()
                : new Modules.Shared.MasterView(masterH));
            return;
        }
        if (key == SimrsGramedika.Data.Parkir.MenuHSpecs.ParkirMasukKey)
        {
            await OpenPageAsync(new Modules.Parkir.ParkirMasukView());
            return;
        }

        if (SimrsGramedika.Data.Master.MenuIReports.Reports.TryGetValue(key, out var reportI)
            || SimrsGramedika.Data.Master.MenuIAdditionalReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIDrugReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIBillingReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIPaymentReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIClinicianReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIProcedureReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIPaymentPointReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIDailyReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIPatientFeesReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIAccountPaymentReports.Reports.TryGetValue(key, out reportI)
            || SimrsGramedika.Data.Master.MenuIFinalReports.Reports.TryGetValue(key, out reportI))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportI));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuTSpecs.Masters.TryGetValue(key, out var masterT))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterT));
            return;
        }

        if (SimrsGramedika.Data.Inventaris.MenuSSpecs.Masters.TryGetValue(key, out var masterS))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterS));
            return;
        }
        if (SimrsGramedika.Data.Inventaris.MenuSSpecs.Reports.TryGetValue(key, out var reportS))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportS));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuKSpecs.Masters.TryGetValue(key, out var masterK))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterK));
            return;
        }
        if (SimrsGramedika.Data.Master.MenuKSpecs.Reports.TryGetValue(key, out var reportK))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportK));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuKExtraSpecs.Masters.TryGetValue(key, out var masterK2))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterK2));
            return;
        }
        if (SimrsGramedika.Data.Master.MenuKPayablesSpecs.Reports.TryGetValue(key, out var reportK2))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportK2));
            return;
        }

        if (key == SimrsGramedika.Data.Master.MenuKFinanceSpecs.PostingJurnalKey)
        {
            await OpenPageAsync(new Modules.Keuangan.PostingJurnalView());
            return;
        }
        if (SimrsGramedika.Data.Master.MenuKFinanceSpecs.Reports.TryGetValue(key, out var reportK3))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportK3));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuMSpecs.Masters.TryGetValue(key, out var masterM))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterM));
            return;
        }
        if (SimrsGramedika.Data.Master.MenuMSpecs.Reports.TryGetValue(key, out var reportM))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportM));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuNRSpecs.Masters.TryGetValue(key, out var masterNR))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterNR));
            return;
        }
        if (SimrsGramedika.Data.Master.MenuNRSpecs.Reports.TryGetValue(key, out var reportNR))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportNR));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuQSpecs.Masters.TryGetValue(key, out var masterQ))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterQ));
            return;
        }
        if (SimrsGramedika.Data.Master.MenuQSpecs.Reports.TryGetValue(key, out var reportQ))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportQ));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuPSpecs.Masters.TryGetValue(key, out var masterP))
        {
            await OpenPageAsync(new Modules.Shared.MasterView(masterP));
            return;
        }
        if (SimrsGramedika.Data.Master.MenuPSpecs.Reports.TryGetValue(key, out var reportP))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportP));
            return;
        }

        if (SimrsGramedika.Data.Master.MenuOSpecs.Reports.TryGetValue(key, out var reportO))
        {
            await OpenPageAsync(new Modules.Shared.ReportView(reportO));
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

    private async Task OpenMenuUAsync(string label, SimrsGramedika.Data.Master.MasterSpec[] sections)
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
            view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async void OnFeatureShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var label = (sender as Button)?.Tag as string ?? "Fitur";

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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
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
            case Modules.Farmasi.FarmasiAction.DataPenyerahanDarah:
            {
                var spec = SimrsGramedika.Data.Master.MenuNRSpecs.Reports["btnUTDPenyerahanDarah"]
                    with { Title = "Data Penyerahan Darah" };
                var view = new Modules.Shared.ReportView(spec);
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
            case Modules.Farmasi.FarmasiAction.ResepPulang:
            {
                if (_user?.HasPermission(SimrsGramedika.Data.MenuA.ResepPulangRepository.IzinFarmasi) != true) return;
                var view = new Modules.Informasi.MenuAView(SimrsGramedika.Data.MenuA.MenuASpecs.ResepPulang);
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
                await ShowNotImplementedAsync($"Farmasi ({action})");
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
            view.TileClicked += async (_, action) => await OpenLabTileAsync(action);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
    }

    private static SimrsGramedika.Data.LabCategory LabCategoryFor(Modules.Laboratorium.LabAction action) => action switch
    {
        Modules.Laboratorium.LabAction.PermintaanPa or Modules.Laboratorium.LabAction.PeriksaPa => SimrsGramedika.Data.LabCategory.PA,
        Modules.Laboratorium.LabAction.PermintaanMb or Modules.Laboratorium.LabAction.PeriksaMb => SimrsGramedika.Data.LabCategory.MB,
        _ => SimrsGramedika.Data.LabCategory.PK,
    };

    private async System.Threading.Tasks.Task OpenLabTileAsync(Modules.Laboratorium.LabAction action)
    {
        var category = LabCategoryFor(action);
        if (Modules.Laboratorium.LabMenuView.IsPermintaan(action)) await OpenPermintaanLabAsync(category);
        else await OpenPeriksaLabAsync(category);
    }

    private async System.Threading.Tasks.Task OpenPermintaanLabAsync(SimrsGramedika.Data.LabCategory category)
    {
        var view = new Modules.Laboratorium.PermintaanLabListView(category)
        {
            IsAdminUtama = _user?.IsAdminUtama == true,
        };
        view.BackRequested += (_, _) => ShowPage(Page.Menu);
        view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
        view.HasilRequested += async (_, req) =>
            await OpenPeriksaLabFormAsync(req.Category, req.NoRawat, req.NoOrder, () => OpenPermintaanLabAsync(req.Category));

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenPeriksaLabAsync(SimrsGramedika.Data.LabCategory category)
    {
        var view = new Modules.Laboratorium.PeriksaLabListView(category);
        view.BackRequested += (_, _) => ShowPage(Page.Menu);
        view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
        view.InputRequested += async (_, noRawat) =>
            await OpenPeriksaLabFormAsync(category, noRawat, null, () => OpenPeriksaLabAsync(category));

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenPeriksaLabFormAsync(
        SimrsGramedika.Data.LabCategory category, string? noRawat, string? noOrder, Func<System.Threading.Tasks.Task> back)
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
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
        view.BackRequested += (_, _) => ShowPage(Page.Menu);
        view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
        view.HasilRequested += async (_, req) =>
            await OpenPeriksaRadiologiFormAsync(req.NoRawat, req.NoOrder, OpenPermintaanRadiologiAsync);

        this.FindControl<ContentControl>("PageHost")!.Content = view;
        ShowPage(Page.Feature);
        await view.ReloadAsync();
    }

    private async System.Threading.Tasks.Task OpenPeriksaRadiologiAsync()
    {
        var view = new Modules.Radiologi.PeriksaRadiologiListView();
        view.BackRequested += (_, _) => ShowPage(Page.Menu);
        view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
            view.PatientMenuRequested += async (_, request) => await HandlePatientMenuAsync(request);
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
            view.TampilkanAlurKunjungan();
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
            case "Presensi Pegawai":
                var category = this.FindControl<ComboBox>("MenuCategoryCombo")!;
                if (category.ItemsSource is not null)
                    category.SelectedIndex = Array.FindIndex(LegacyMenuCatalog.Categories,
                        name => name.StartsWith("[C]", StringComparison.Ordinal));
                ShowPage(Page.Menu);
                return;

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
        void Board(SimrsGramedika.Data.AntrianBoardKind kind) => Modules.Antrian.AntrianBoards.Open(kind);

        var apotek = new MenuItem { Header = "Antrian Apotek" };
        apotek.Items.Add(Item("Antrian Apotek", () => Pending("Antrian Apotek")));
        apotek.Items.Add(Item("Antrian Apotek 2", () => Board(SimrsGramedika.Data.AntrianBoardKind.Apotek2)));
        apotek.Items.Add(Item("Antrian Apotek 2 Racikan", () => Board(SimrsGramedika.Data.AntrianBoardKind.Apotek2Racikan)));
        apotek.Items.Add(Item("Antrian Apotek 2 Non Racikan", () => Board(SimrsGramedika.Data.AntrianBoardKind.Apotek2NonRacikan)));

        var laborat = new MenuItem { Header = "Antrian Laborat" };
        laborat.Items.Add(Item("Laborat Patologi Klinis", () => Board(SimrsGramedika.Data.AntrianBoardKind.LabPk)));
        laborat.Items.Add(Item("Laborat Patologi Anatomi", () => Board(SimrsGramedika.Data.AntrianBoardKind.LabPa)));
        laborat.Items.Add(Item("Laborat Mikrobiologi & Bio Molekuler", () => Board(SimrsGramedika.Data.AntrianBoardKind.LabMb)));

        var menu = new ContextMenu();
        menu.Items.Add(Item("Anjungan Registrasi Mandiri", () => Pending("Anjungan Registrasi Mandiri")));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Cetak Antrian Loket", () => Pending("Cetak Antrian Loket")));
        menu.Items.Add(Item("Antrian Loket", Modules.Antrian.AntrianBoards.OpenLoket));
        menu.Items.Add(Item("Antrian Loket 2", () => Pending("Antrian Loket 2")));
        menu.Items.Add(Item("Antrian Unit/Poliklinik", () => Pending("Antrian Unit/Poliklinik")));
        menu.Items.Add(Item("Antrian Unit/Poliklinik 2", () => Board(SimrsGramedika.Data.AntrianBoardKind.JadwalPoli)));
        menu.Items.Add(apotek);
        menu.Items.Add(laborat);
        menu.Items.Add(Item("Antrian Radiologi", () => Board(SimrsGramedika.Data.AntrianBoardKind.Radiologi)));
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
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
            view.BackRequested += (_, _) => ShowPage(Page.Menu);
            pageHost.Content = view;
        }

        ShowPage(Page.Feature);
    }

    /// <summary>Raised by the toolbar "Keluar" button — App swaps in the login window instead of quitting.</summary>
    public event EventHandler? LogoutRequested;

    private void OnExitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LogoutRequested is null) Close();
        else LogoutRequested.Invoke(this, EventArgs.Empty);
    }


    private async Task HandlePatientMenuAsync(PatientMenuRequest request)
    {
        var host = this.FindControl<ContentControl>("PageHost")!;
        var source = host.Content;
        void Back(object? sender, EventArgs e)
        {
            host.Content = source;
            ShowPage(Page.Feature);
        }

        switch (request.Action)
        {
            case PatientMenuAction.Report when request.TileKey is { } reportKey && _user is { } reportUser &&
                LegacyTilePermissions.CanSeeTile(reportUser, reportKey) &&
                Modules.Shared.ContextReports.TryGet(reportKey, out var contextReport):
                var reportView = new Modules.Shared.ReportView(contextReport,
                    contextReport.VisitSql is null || string.IsNullOrWhiteSpace(request.NoRawat) ? null : request.NoRawat,
                    Modules.Shared.ContextReports.IsPatientReport(reportKey) ? request.NoRekamMedis : null);
                reportView.BackRequested += Back;
                await OpenFeatureAsync(reportView, reportView.ReloadAsync);
                return;

            case PatientMenuAction.CatatanPasien when _user?.HasPermission("catatan_pasien") == true:
                await new Modules.Pasien.CatatanPasienWindow(request, _user).ShowDialog(this);
                return;

            case PatientMenuAction.Skrining when request.TileKey is { } key && _user is not null &&
                !string.IsNullOrWhiteSpace(request.NoRawat) && LegacyTilePermissions.CanSeeTile(_user, key) &&
                SimrsGramedika.Data.Master.MenuMSpecs.Reports.TryGetValue(key, out var report) && report.VisitSql is not null:
                var screening = new Modules.Shared.ReportView(report, request.NoRawat);
                screening.BackRequested += Back;
                await OpenFeatureAsync(screening, screening.ReloadAsync);
                return;

            case PatientMenuAction.DataRekamMedis when _user?.HasPermission("pasien") == true:
                var pasien = new Modules.Pasien.PasienView();
                pasien.BackRequested += Back;
                pasien.PatientMenuRequested += async (_, next) => await HandlePatientMenuAsync(next);
                await OpenFeatureAsync(pasien, () => pasien.SearchForAsync(request.NoRekamMedis));
                return;

            case PatientMenuAction.RiwayatPerawatan when _user?.HasPermission("resume_pasien") == true:
                await new Modules.Pasien.PatientHistoryWindow(request).ShowDialog(this);
                return;

            case PatientMenuAction.KamarInap when _user?.HasPermission("kamar_inap") == true:
                var ranap = new Modules.RawatInap.RawatInapView();
                ranap.BackRequested += Back;
                ranap.PatientMenuRequested += async (_, next) => await HandlePatientMenuAsync(next);
                await OpenFeatureAsync(ranap, async () =>
                {
                    await ranap.PrefillAdmitAsync(request.NoRawat);
                    await ranap.ReloadAsync();
                });
                return;

            case PatientMenuAction.TindakanRawatJalan when _user is { } user && user.HasPermission("tindakan_ralan"):
                var klinis = new Modules.RawatJalan.RawatJalanKunjunganView(request, user);
                klinis.BackRequested += Back;
                await OpenFeatureAsync(klinis, klinis.LoadAsync);
                return;

            case PatientMenuAction.TindakanDanPemeriksaan when _user?.HasPermission("tindakan_ralan") == true:
                var ralan = new Modules.RawatJalan.RawatJalanView();
                ralan.TampilkanAlurKunjungan();
                ralan.BackRequested += Back;
                ralan.PatientMenuRequested += async (_, next) => await HandlePatientMenuAsync(next);
                await OpenFeatureAsync(ralan, () => ralan.SelectVisitAsync(request.NoRawat));
                return;

            case PatientMenuAction.BillingPembayaran:
                var kasir = new Modules.Kasir.KasirView();
                kasir.BackRequested += Back;
                await OpenFeatureAsync(kasir, () => kasir.SearchForAsync(request.NoRawat));
                return;

            case PatientMenuAction.PeriksaLaboratorium when _user?.HasPermission("periksa_lab") == true &&
                !string.IsNullOrWhiteSpace(request.NoRawat):
                await OpenRegistrationLabAsync(SimrsGramedika.Data.LabCategory.PK, request.NoRawat, Back);
                return;

            case PatientMenuAction.PeriksaLaboratoriumPa when _user?.HasPermission("pemeriksaan_lab_pa") == true &&
                !string.IsNullOrWhiteSpace(request.NoRawat):
                await OpenRegistrationLabAsync(SimrsGramedika.Data.LabCategory.PA, request.NoRawat, Back);
                return;

            case PatientMenuAction.PeriksaLaboratoriumMb when _user?.HasPermission("pemeriksaan_lab_mb") == true &&
                !string.IsNullOrWhiteSpace(request.NoRawat):
                await OpenRegistrationLabAsync(SimrsGramedika.Data.LabCategory.MB, request.NoRawat, Back);
                return;

            case PatientMenuAction.PeriksaRadiologi when _user?.HasPermission("periksa_radiologi") == true &&
                !string.IsNullOrWhiteSpace(request.NoRawat):
                var radiologi = new Modules.Radiologi.PeriksaRadiologiFormView();
                radiologi.BackRequested += Back;
                radiologi.SavedRequested += Back;
                await OpenFeatureAsync(radiologi, () => radiologi.PrefillAsync(request.NoRawat, null));
                return;

            case PatientMenuAction.PermintaanLab when _user is { } labUser && labUser.HasPermission("permintaan_lab") &&
                !string.IsNullOrWhiteSpace(request.NoRawat):
                await ShowRegistrationOrderAsync(request, labUser, SimrsGramedika.Data.JenisOrderRalan.LabPk, "Permintaan Laboratorium");
                return;

            case PatientMenuAction.PermintaanRadiologi when _user is { } radUser && radUser.HasPermission("permintaan_radiologi") &&
                !string.IsNullOrWhiteSpace(request.NoRawat):
                await ShowRegistrationOrderAsync(request, radUser, SimrsGramedika.Data.JenisOrderRalan.Radiologi, "Permintaan Radiologi");
                return;

            case PatientMenuAction.ResepDokter when _user is { } resepUser && resepUser.HasPermission("resep_dokter") &&
                !string.IsNullOrWhiteSpace(request.NoRawat):
                await ShowRegistrationOrderAsync(request, resepUser, SimrsGramedika.Data.JenisOrderRalan.Resep, "Input Resep Dokter");
                return;
        }
    }

    private Task OpenRegistrationLabAsync(SimrsGramedika.Data.LabCategory category, string noRawat, EventHandler back)
    {
        var form = new Modules.Laboratorium.PeriksaLabFormView(category);
        form.BackRequested += back;
        form.SavedRequested += back;
        return OpenFeatureAsync(form, () => form.PrefillAsync(noRawat, null));
    }

    private Task ShowRegistrationOrderAsync(PatientMenuRequest request, AuthenticatedUser user,
        SimrsGramedika.Data.JenisOrderRalan jenis, string title)
    {
        var header = new TextBlock
        {
            Text = $"{request.NamaPasien}  ·  No. RM {request.NoRekamMedis}  ·  No. Rawat {request.NoRawat}",
            Classes = { "pageSubtitle" },
            Margin = new Avalonia.Thickness(10, 10, 10, 0),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
        DockPanel.SetDock(header, Dock.Top);
        var window = new Window
        {
            Title = title,
            Width = 980,
            Height = 710,
            MinWidth = 670,
            MinHeight = 530,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ContentControl
            {
                Classes = { "biruTegas" },
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                Content = new DockPanel
                {
                    Children = { header, new Modules.RawatJalan.OrderRalanView(request.NoRawat, request.NoRekamMedis, user, jenis) },
                },
            },
        };
        return window.ShowDialog(this);
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
