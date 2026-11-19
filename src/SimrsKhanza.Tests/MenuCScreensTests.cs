using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
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
        });
        thread.Start();
        thread.Join();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}
