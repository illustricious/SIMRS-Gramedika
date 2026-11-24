using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Kepegawaian;
using SimrsKhanza.UI.Modules.Kepegawaian;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.Tests;

/// <summary>
/// Builds every menu [C] screen in a headless Avalonia app (catches runtime layout/constructor errors).
/// With SIMRS_DB_SMOKE=1 the screens also load data; SIMRS_SCREENSHOT_DIR saves a PNG per screen.
/// </summary>
[Collection("DatabaseSettingsStore")] // screens read the saved profile; don't overlap with the store tests
public class MenuCScreensTests
{
    [Fact]
    public void AllMenuCScreens_Build()
    {
        var errors = new List<string>();
        var thread = new Thread(() =>
        {
            AppBuilder.Configure<SimrsKhanza.UI.App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();

            var withDb = Environment.GetEnvironmentVariable("SIMRS_DB_SMOKE") == "1";
            var shots = Environment.GetEnvironmentVariable("SIMRS_SCREENSHOT_DIR");
            var screens = SimrsKhanza.Data.MenuA.MenuASpecs.Tiles
                .Select(kv => ("A_" + kv.Key, (PageView)new SimrsKhanza.UI.Modules.Informasi.MenuAView(kv.Value)))
                .Append(("A_sesiKfr", new SimrsKhanza.UI.Modules.Informasi.MenuAView(SimrsKhanza.Data.MenuA.MenuASpecs.SesiKfr)))
                .Concat(MenuCSpecs.Masters.Select(kv => (kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(MenuCSpecs.Reports.Select(kv => (kv.Key, (PageView)new ReportView(kv.Value))))
                .Append(("btnPenggajian", new PenggajianView()))
                .Append(("btnSKPPenilaianPegawai", new SkpPenilaianView()))
                .Append(("btnSKPRekapitulasiPenilaian", new SkpRekapView()))
                .Concat(SimrsKhanza.Data.Farmasi.MenuDSpecs.Masters.Select(kv => ("D_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Farmasi.MenuDSpecs.Reports.Select(kv => ("D_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Append(("D_btnPemesanan", (PageView)new SimrsKhanza.UI.Modules.Farmasi.PenerimaanView()))
                .Append(("D_btnPermintaanStokObatPasien", (PageView)new SimrsKhanza.UI.Modules.Farmasi.PermintaanStokPasienView()))
                .Concat(SimrsKhanza.Data.Farmasi.MenuDLists.Masters.Select(kv => ("D_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Farmasi.MenuDLists.Reports.Select(kv => ("D_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Inventaris.MenuESpecs.Masters.Select(kv => ("E_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Inventaris.MenuESpecs.Reports.Select(kv => ("E_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Inventaris.MenuFSpecs.Masters.Select(kv => ("F_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Inventaris.MenuFSpecs.Reports.Select(kv => ("F_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Inventaris.MenuGSpecs.Masters.Select(kv => ("G_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Inventaris.MenuGSpecs.Reports.Select(kv => ("G_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Parkir.MenuHSpecs.Masters.Select(kv => ("H_" + kv.Key, (PageView)(
                    kv.Key == SimrsKhanza.Data.Parkir.MenuHSpecs.JenisParkirKey
                        ? new SimrsKhanza.UI.Modules.Parkir.JenisParkirView()
                        : new MasterView(kv.Value)))))
                .Append(("H_btnParkirMasuk", (PageView)new SimrsKhanza.UI.Modules.Parkir.ParkirMasukView()))
                .Concat(SimrsKhanza.Data.Inventaris.MenuSSpecs.Masters.Select(kv => ("S_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Inventaris.MenuSSpecs.Reports.Select(kv => ("S_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuKSpecs.Masters.Select(kv => ("K_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuKSpecs.Reports.Select(kv => ("K_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuKExtraSpecs.Masters.Select(kv => ("K2_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuKPayablesSpecs.Reports.Select(kv => ("K2_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuKFinanceSpecs.Reports.Select(kv => ("K3_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Append(("K3_btnPostingJurnal", (PageView)new SimrsKhanza.UI.Modules.Keuangan.PostingJurnalView()))
                .Concat(SimrsKhanza.Data.Master.MenuNRSpecs.Masters.Select(kv => ("NR_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuNRSpecs.Reports.Select(kv => ("NR_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuQSpecs.Masters.Select(kv => ("Q_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuQSpecs.Reports.Select(kv => ("Q_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuPSpecs.Masters.Select(kv => ("P_" + kv.Key, (PageView)new MasterView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuPSpecs.Reports.Select(kv => ("P_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(SimrsKhanza.Data.Master.MenuOSpecs.Reports.Select(kv => ("O_" + kv.Key, (PageView)new ReportView(kv.Value))))
                .Concat(Enum.GetValues<SimrsKhanza.Data.Farmasi.StokMode>()
                    .Select(m => ("D_" + m, (PageView)new SimrsKhanza.UI.Modules.Farmasi.StokTransaksiView(m))));

            foreach (var (key, view) in screens)
            {
                try
                {
                    var w = new Window { Width = 1400, Height = 820, Content = view };
                    w.Show();
                    if (withDb)
                    {
                        var t = view.ReloadAsync();
                        var until = DateTime.Now.AddSeconds(20);
                        while (!t.IsCompleted && DateTime.Now < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
                        t.GetAwaiter().GetResult();
                    }
                    Dispatcher.UIThread.RunJobs();
                    if (shots is { Length: > 0 })
                    {
                        Directory.CreateDirectory(shots);
                        w.CaptureRenderedFrame()?.Save(Path.Combine(shots, key + ".png"));
                    }
                    w.Close();
                }
                catch (Exception ex) { errors.Add($"{key}: {ex.GetType().Name}: {ex.Message}"); }
            }

            try { VerifyPatientContextMenu(); }
            catch (Exception ex) { errors.Add($"Patient context menu: {ex}"); }
        });
        thread.Start();
        thread.Join();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    private static void VerifyPopupStyle(ContextMenu menu, MenuItem group, string surface, string highlight, string ink)
    {
        static void Color(Avalonia.Media.IBrush? brush, string expected) =>
            Assert.Equal(Avalonia.Media.Color.Parse(expected), Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(brush).Color);

        Color(menu.Background, surface);
        Assert.InRange(group.Bounds.Height, 25d, 28d);
        group.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var popup = group.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>()
                .Single(p => p.Name == "PART_Popup");
            var submenu = Assert.IsType<Border>(popup.Child);
            Color(submenu.Background, surface);
            Assert.Equal(new Thickness(3), submenu.Padding);
            var row = group.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_LayoutRoot");
            Color(row.Background, highlight);
            var chevron = group.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Name == "PART_ChevronPath");
            Color(chevron.Fill, ink);
        }
        finally
        {
            group.IsSubMenuOpen = false;
        }
    }


    private static void VerifyPatientContextMenu()
    {
        var first = new PatientMenuTarget("RW-01", "RM-01", "Pasien A");
        var second = new PatientMenuTarget("RW-02", "RM-02", "Pasien B");
        var grid = Ui.Grid();
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Pasien",
            Width = new DataGridLength(250),
            Binding = new Avalonia.Data.Binding(nameof(PatientMenuTarget.NamaPasien)),
        });
        grid.ItemsSource = new[] { first, second };
        PatientMenuRequest? openedFor = null;
        var menu = PatientContextMenu.Attach<PatientMenuTarget>(grid, row => row, request => openedFor = request);
        var window = new Window { Width = 640, Height = 480, Content = grid };
        var oldUser = AppSession.CurrentUser;
        AppSession.CurrentUser = new() { Permissions = new HashSet<string> { "pasien" } };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            grid.SelectedItem = first;
            var secondRow = grid.GetVisualDescendants().OfType<DataGridRow>()
                .Single(row => ReferenceEquals(row.DataContext, second));
            var point = secondRow.TranslatePoint(new Point(30, secondRow.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            Assert.True(menu.IsOpen);
            Assert.Same(second, grid.SelectedItem);

            var record = Assert.IsType<MenuItem>(menu.Items[0]);
            VerifyPopupStyle(menu, record, "#FFFFFF", "#E8F0FC", "#123F8D");
            var identity = Assert.IsType<MenuItem>(record.Items[0]);
            Assert.True(identity.IsEnabled);
            var billing = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Billing/Pembayaran Pasien"));
            Assert.False(Assert.IsType<MenuItem>(billing.Items[1]).IsEnabled);
            grid.SelectedItem = first;
            identity.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(second.NoRekamMedis, openedFor?.NoRekamMedis);
            menu.Close();

            var empty = new Point(25, 300);
            window.MouseDown(empty, MouseButton.Right);
            window.MouseUp(empty, MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            Assert.False(menu.IsOpen);

            window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            grid.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
            Dispatcher.UIThread.RunJobs();
            Assert.True(menu.IsOpen);
            VerifyPopupStyle(menu, Assert.IsType<MenuItem>(menu.Items[0]), "#1D2533", "#293857", "#ABC5FF");
            menu.Close();
        }
        finally
        {
            window.Close();
            AppSession.CurrentUser = oldUser;
        }
    }
}
