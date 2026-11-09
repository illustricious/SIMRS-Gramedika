using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Antrian;

/// <summary>
/// Entry points for the native antrian display boards (replacement for the legacy webapps/
/// PHP pages). Each board opens in its own window so it can sit on a second monitor; opening
/// the same board again just brings the existing window forward.
/// </summary>
public static class AntrianBoards
{
    /// <summary>Menu entries in legacy order ("Antrian Unit/Poliklinik 2", Apotek, Laborat, Radiologi submenus flattened).</summary>
    public static IReadOnlyList<(string Menu, AntrianBoardKind Kind)> Items { get; } =
    [
        ("Antrian Unit/Poliklinik 2", AntrianBoardKind.JadwalPoli),
        ("Antrian Apotek — Apotek 2", AntrianBoardKind.Apotek2),
        ("Antrian Apotek — Apotek 2 Racikan", AntrianBoardKind.Apotek2Racikan),
        ("Antrian Apotek — Apotek 2 Non Racikan", AntrianBoardKind.Apotek2NonRacikan),
        ("Antrian Laborat — Laborat Patologi Klinis", AntrianBoardKind.LabPk),
        ("Antrian Laborat — Laborat Patologi Anatomi", AntrianBoardKind.LabPa),
        ("Antrian Laborat — Laborat Mikrobiologi & Bio Molekuler", AntrianBoardKind.LabMb),
        ("Antrian Radiologi", AntrianBoardKind.Radiologi),
    ];

    private static readonly Dictionary<string, AntrianBoardWindow> Open_ = new();

    public static void Open(AntrianBoardKind kind) => Show($"{kind}", () => new AntrianBoardWindow(kind));

    public static void OpenPoliDokter(string kdPoli, string kdDokter)
        => Show($"{AntrianBoardKind.PoliDokter}:{kdPoli}:{kdDokter}",
                () => new AntrianBoardWindow(AntrianBoardKind.PoliDokter, kdPoli, kdDokter));

    private static AntrianLoketWindow? _loket;

    /// <summary>Native "Antrian Loket" (legacy antrianloket.jar): operator panel + display in one window.</summary>
    public static void OpenLoket()
    {
        if (_loket is not null)
        {
            _loket.Activate();
            return;
        }

        _loket = new AntrianLoketWindow();
        _loket.Closed += (_, _) => _loket = null;
        _loket.Show();
    }

    /// <summary>bell.wav (1) / bell2.wav (2) copied from webapps; Windows only, failures are swallowed.</summary>
    public static void PlayBell(int sound)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var name = sound == 2 ? "bell2.wav" : "bell.wav";
            using var asset = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://SimrsKhanza.UI/Assets/{name}"));
            var ms = new MemoryStream();
            asset.CopyTo(ms);
            ms.Position = 0;
            new System.Media.SoundPlayer(ms).Play();
        }
        catch
        {
            // A missing audio device must never break a board.
        }
    }

    private static void Show(string key, Func<AntrianBoardWindow> create)
    {
        if (Open_.TryGetValue(key, out var existing))
        {
            existing.Activate();
            return;
        }

        var window = create();
        Open_[key] = window;
        window.Closed += (_, _) => Open_.Remove(key);
        window.Show();
    }
}
