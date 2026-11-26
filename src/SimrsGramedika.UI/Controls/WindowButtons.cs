using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Path = Avalonia.Controls.Shapes.Path;

namespace SimrsGramedika.UI.Controls;

/// <summary>
/// Tombol kecilkan, ubah ukuran, dan tutup untuk jendela tanpa bilah judul bawaan.
/// Dipakai layar login dan jendela utama; gayanya ada di App.axaml (Button.caption).
/// </summary>
public sealed class WindowButtons : StackPanel
{
    private const string IkonKecilkan = "M0,5 H10";
    private const string IkonJendela = "M2.5,2.5 V0.5 H9.5 V7.5 H7.5 M0.5,2.5 H7.5 V9.5 H0.5 Z";
    private const string IkonPenuh = "M0.5,0.5 H9.5 V9.5 H0.5 Z";
    private const string IkonTutup = "M0.5,0.5 L9.5,9.5 M9.5,0.5 L0.5,9.5";

    private readonly Button _kecilkan;
    private readonly Button _ukuran;
    private readonly Button _tutup;
    private Window? _window;

    /// <summary>Warna gelap untuk latar terang; bawaan untuk bilah gelap.</summary>
    public bool Terang
    {
        get => _tutup.Classes.Contains("terang");
        set
        {
            foreach (var tombol in new[] { _kecilkan, _ukuran, _tutup }) tombol.Classes.Set("terang", value);
        }
    }

    public WindowButtons()
    {
        Orientation = Orientation.Horizontal;
        _kecilkan = Tombol("MinimizeButton", IkonKecilkan, "Kecilkan");
        _ukuran = Tombol("FullscreenButton", IkonJendela, "Ukuran jendela biasa");
        _tutup = Tombol("CloseButton", IkonTutup, "Tutup");
        _tutup.Classes.Add("close");
        _kecilkan.Click += (_, _) => { if (_window is not null) _window.WindowState = WindowState.Minimized; };
        _ukuran.Click += (_, _) => { if (_window is not null) UbahUkuran(_window); };
        _tutup.Click += (_, _) => _window?.Close();
        Children.AddRange([_kecilkan, _ukuran, _tutup]);
    }

    public static void UbahUkuran(Window window) =>
        window.WindowState = window.WindowState == WindowState.Normal ? WindowState.FullScreen : WindowState.Normal;

    /// <summary>Area kosong bilah judul: geser untuk memindahkan jendela, klik dua kali untuk mengubah ukuran.</summary>
    public static void TanganiBilahJudul(Window window, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) UbahUkuran(window);
        else if (window.WindowState == WindowState.Normal) window.BeginMoveDrag(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is null) return;
        _window.PropertyChanged += OnWindowChanged;
        Perbarui();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window is not null) _window.PropertyChanged -= OnWindowChanged;
        _window = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty) Perbarui();
    }

    private void Perbarui()
    {
        var biasa = _window?.WindowState == WindowState.Normal;
        Atur(_ukuran, biasa ? IkonPenuh : IkonJendela, biasa ? "Layar penuh" : "Ukuran jendela biasa");
    }

    private static Button Tombol(string nama, string ikon, string label)
    {
        var tombol = new Button { Name = nama, Classes = { "caption" } };
        var gambar = new Path { Width = 10, Height = 10, StrokeThickness = 1 };
        gambar[!Shape.StrokeProperty] = tombol[!Button.ForegroundProperty];
        tombol.Content = gambar;
        Atur(tombol, ikon, label);
        return tombol;
    }

    private static void Atur(Button tombol, string ikon, string label)
    {
        ((Path)tombol.Content!).Data = Avalonia.Media.Geometry.Parse(ikon);
        ToolTip.SetTip(tombol, label);
        AutomationProperties.SetName(tombol, label);
    }
}
