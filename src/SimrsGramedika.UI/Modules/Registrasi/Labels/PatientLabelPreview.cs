using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SimrsGramedika.UI.Modules.Shared;
using SkiaSharp;

namespace SimrsGramedika.UI.Modules.Registrasi.Labels;

internal sealed class PatientLabelPreview : Window
{
    private static readonly int[] ZoomLevels = [25, 50, 75, 100, 125, 150, 200, 300, 400];
    private bool _printing;

    private PatientLabelPreview(PatientLabel label, string title)
    {
        Title = title;
        Width = 900;
        Height = 640;
        MinWidth = 490;
        MinHeight = 290;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        using var rendered = label.Render(6);
        using var png = rendered.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = png.AsStream();
        var bitmap = new Bitmap(stream);
        var page = new Image { Source = bitmap, Stretch = Stretch.Fill };
        Closed += (_, _) => bitmap.Dispose();

        var canvas = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new Border
            {
                Margin = new Thickness(18), Background = Brushes.White, BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top, Child = page,
            },
        };
        var print = new Button { Content = OperatingSystem.IsWindows() ? "Cetak" : "Simpan PDF", MinWidth = 68 };
        var smaller = new Button { Content = "−" };
        var larger = new Button { Content = "+" };
        var zoom = new ComboBox { ItemsSource = ZoomLevels.Select(p => $"{p}%").ToArray(), MinWidth = 87 };
        ToolTip.SetTip(smaller, "Perkecil");
        ToolTip.SetTip(larger, "Perbesar");
        zoom.SelectionChanged += (_, _) =>
        {
            if (zoom.SelectedIndex < 0) return;
            var scale = ZoomLevels[zoom.SelectedIndex] / 100.0 * 96 / 72;
            page.Width = label.Width * scale;
            page.Height = label.Height * scale;
            smaller.IsEnabled = zoom.SelectedIndex > 0;
            larger.IsEnabled = zoom.SelectedIndex < ZoomLevels.Length - 1;
        };
        smaller.Click += (_, _) => zoom.SelectedIndex--;
        larger.Click += (_, _) => zoom.SelectedIndex++;
        zoom.SelectedIndex = Array.IndexOf(ZoomLevels, 100);
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12),
            Children = { print, smaller, zoom, larger, new TextBlock
            {
                Text = $"{label.Width * 25.4 / 72:0.#} × {label.Height * 25.4 / 72:0.#} mm",
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
            } },
        };
        DockPanel.SetDock(toolbar, Dock.Top);
        Content = new DockPanel { Children = { toolbar, canvas } };

        print.Click += async (_, _) => await PrintAsync(label, title);
        KeyDown += async (_, e) =>
        {
            if (e.Key != Key.P || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            e.Handled = true;
            await PrintAsync(label, title);
        };
    }

    public static async void Open(Control source, PatientMenuTarget patient, PatientLabelFormat format)
    {
        var owner = TopLevel.GetTopLevel(source) as Window;
        try
        {
            var label = await PatientLabel.LoadAsync(patient, format);
            if (owner?.IsVisible == true && TopLevel.GetTopLevel(source) == owner)
                new PatientLabelPreview(label, format.Title).Show(owner);
        }
        catch (Exception ex)
        {
            if (owner?.IsVisible == true)
                await Dialogs.InfoAsync(owner, format.Title, $"Gagal membuka pratinjau. {ex.Message}");
        }
    }

    private async Task PrintAsync(PatientLabel label, string title)
    {
        if (_printing) return;
        _printing = true;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsLabelPrinting.Print(TryGetPlatformHandle()!.Handle, label, title);
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Simpan PDF untuk dicetak", SuggestedFileName = $"{title}.pdf",
                FileTypeChoices = [FilePickerFileTypes.Pdf],
            });
            if (file is null) return;
            await using var output = await file.OpenWriteAsync();
            output.SetLength(0);
            using var document = SKDocument.CreatePdf(output);
            label.Draw(document.BeginPage(label.Width, label.Height));
            document.EndPage();
            document.Close();
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(this, "Cetak", $"Gagal mencetak label. {ex.Message}");
        }
        finally
        {
            _printing = false;
        }
    }
}
