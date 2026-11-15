using System.ComponentModel;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Akun;

/// <summary>One checkbox in the hak-akses editor (= one enum column of legacy `user`).</summary>
public class AksesItem(string column, string label, string kategori) : INotifyPropertyChanged
{
    public string Column { get; } = column;
    public string Label { get; } = label;
    public string Kategori { get; } = kategori;

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Human labels for hak-akses columns: the Menu Utama tile label(s) that column unlocks
/// (via LegacyTilePermissions), else the column name prettified.
/// </summary>
public static class AksesLabels
{
    private static readonly Lazy<Dictionary<string, (string Label, string Kategori)>> Map = new(Build);

    private static Dictionary<string, (string, string)> Build()
    {
        var tileInfo = new Dictionary<string, (string Label, string Kategori)>(StringComparer.Ordinal);
        for (var c = 0; c < LegacyMenuCatalog.Tiles.Length; c++)
        {
            var kat = LegacyMenuCatalog.Categories[c];
            var shortKat = kat.Length > 3 && kat[0] == '[' ? kat[..3] : kat;
            foreach (var (key, label) in LegacyMenuCatalog.Tiles[c])
                tileInfo.TryAdd(key, (label, shortKat));
        }

        var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tile, cols) in LegacyTilePermissions.TileColumns)
        {
            if (!tileInfo.TryGetValue(tile, out var info)) continue;
            foreach (var col in cols) result.TryAdd(col, info);
        }
        return result;
    }

    public static AksesItem Create(string column)
    {
        var pretty = Pretty(column);
        if (Map.Value.TryGetValue(column, out var info))
        {
            var label = string.Equals(info.Label, pretty, StringComparison.OrdinalIgnoreCase)
                ? info.Label
                : $"{info.Label}  ({column})";
            return new AksesItem(column, label, info.Kategori);
        }
        return new AksesItem(column, pretty, "[-]");
    }

    private static string Pretty(string column)
    {
        var words = column.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => w is "lab" or "rm" or "bpjs" or "ok" or "igd" or "rl"
            ? w.ToUpperInvariant()
            : char.ToUpperInvariant(w[0]) + w[1..]));
    }
}
