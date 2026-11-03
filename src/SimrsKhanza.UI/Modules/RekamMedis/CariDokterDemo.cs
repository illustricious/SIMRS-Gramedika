using Avalonia.Controls;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Controls;

namespace SimrsKhanza.UI.Modules.RekamMedis;

/// <summary>
/// First concrete use of SearchPickerDialog&lt;T&gt;, wired to the real MySQL-backed
/// DokterRepository (EF Core + Pomelo, see SimrsKhanza.Data). Connection settings
/// come from environment variables (DatabaseSettings.FromEnvironment) — nothing
/// hardcoded. If no MySQL server is reachable (e.g. this dev scaffold environment,
/// which has no local MySQL/Docker — see docs/audit/HANDOVER.md), it falls back to
/// a clearly-labeled in-memory demo list instead of crashing.
/// </summary>
public static class CariDokterDemo
{
    public static async Task ShowAsync(Window owner)
    {
        var selected = await SearchPickerDialog.ShowAsync<Dokter>(
            owner,
            "Cari Dokter",
            query => SearchAsync(query),
            d => $"{d.KodeDokter} — {d.NamaDokter}");

        var message = selected is null
            ? "Tidak ada dokter dipilih."
            : $"Dokter terpilih: {selected.NamaDokter} ({selected.KodeDokter})";

        var dialog = new Window
        {
            Width = 320,
            Height = 120,
            Title = "Hasil Pilihan",
            Content = new TextBlock { Text = message, Margin = new Avalonia.Thickness(16), TextWrapping = Avalonia.Media.TextWrapping.Wrap }
        };
        await dialog.ShowDialog(owner);
    }

    private static async Task<IReadOnlyList<Dokter>> SearchAsync(string query)
    {
        try
        {
            var settings = DatabaseSettings.FromEnvironment();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new DokterRepository(db);
            return await repo.SearchAsync(query);
        }
        catch (Exception)
        {
            // No reachable MySQL server in this environment — fall back to a
            // clearly-labeled demo list so the SearchPickerDialog itself can
            // still be exercised end-to-end.
            return new List<Dokter>
            {
                new() { KodeDokter = "DEMO01", NamaDokter = "(demo — tidak konek DB) dr. Contoh Satu" },
                new() { KodeDokter = "DEMO02", NamaDokter = "(demo — tidak konek DB) dr. Contoh Dua" },
            }.Where(d => string.IsNullOrWhiteSpace(query) || d.NamaDokter.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
