using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.UI.Modules.Shared;

/// <summary>A DataGrid row: display cells + the object it stands for.</summary>
public class GridRow(string[] cells, object? tag = null)
{
    public string[] Cells { get; } = cells;
    public object? Tag { get; } = tag;
}

/// <summary>One grid column: source column name in a <see cref="Row"/>, header, optional width and money/date formatting.</summary>
public record Col(string Name, string Header, double Width = 0, bool Money = false);

/// <summary>Small code-first UI kit so feature screens stay compact and consistent.</summary>
public static class Ui
{
    public static readonly CultureInfo Id = new("id-ID");

    public static string Money(double v) => v.ToString("N0", Id);

    public static string Fmt(Row r, Col c)
    {
        if (c.Money) return Money(r.D(c.Name));
        return r[c.Name] switch
        {
            DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd/MM/yyyy") : d.ToString("dd/MM/yyyy HH:mm:ss"),
            double or decimal or float => Money(r.D(c.Name)),
            _ => r.S(c.Name),
        };
    }

    public static DataGrid Grid(params Col[] cols)
    {
        var g = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HeadersVisibility = DataGridHeadersVisibility.Column,
            CanUserResizeColumns = true, CanUserSortColumns = true,
        };
        SetColumns(g, cols);
        return g;
    }

    public static void SetColumns(DataGrid g, Col[] cols)
    {
        g.Columns.Clear();
        for (var i = 0; i < cols.Length; i++)
            g.Columns.Add(new DataGridTextColumn
            {
                Header = cols[i].Header,
                Binding = new Binding($"Cells[{i}]"),
                Width = cols[i].Width > 0 ? new DataGridLength(cols[i].Width) : DataGridLength.Auto,
                MinWidth = cols[i].Width > 0 ? Math.Min(72, cols[i].Width) : 72,
            });
        g.Tag = cols;
        if (g.IsLoaded) DataGridHeaderFit.Fit(g);
    }

    public static void Fill(DataGrid g, IEnumerable<Row> rows)
    {
        var cols = (Col[])g.Tag!;
        g.ItemsSource = rows.Select(r => new GridRow(cols.Select(c => Fmt(r, c)).ToArray(), r)).ToList();
    }

    public static void FillRows(DataGrid g, IEnumerable<GridRow> rows) => g.ItemsSource = rows.ToList();

    public static T? Selected<T>(DataGrid g) where T : class => (g.SelectedItem as GridRow)?.Tag as T;

    public static T Themed<T>(T control, AvaloniaProperty property, string resourceKey) where T : Control
    {
        control.Bind(property, control.GetResourceObservable(resourceKey));
        return control;
    }

    public static TextBlock Title(string text) => new()
    {
        Text = text, Classes = { "pageTitle" },
    };

    public static TextBlock Hint(string text) => new()
    {
        Text = text, Classes = { "pageSubtitle" },
    };

    public static TextBlock Section(string text) => new() { Text = text, Classes = { "sectionTitle" } };

    public static TextBlock Status() => new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

    public static void Ok(TextBlock t, string msg) { t.Text = msg; t.Foreground = Brushes.Green; }
    public static void Err(TextBlock t, string msg) { t.Text = msg; t.Foreground = Brush.Parse("#DC2626"); }
    public static void Info(TextBlock t, string msg) { t.Text = msg; t.Foreground = Brushes.Gray; }

    public static Button Btn(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> click, bool enabled = true)
    {
        var b = new Button { Content = text, IsEnabled = enabled, Margin = new Thickness(0, 0, 6, 4) };
        b.Click += click;
        return b;
    }

    public static WrapPanel Bar(params Control[] items)
    {
        var p = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        foreach (var i in items)
        {
            if (i is TextBlock) i.VerticalAlignment = VerticalAlignment.Center;
            if (i.Margin == default) i.Margin = new Thickness(0, 0, 6, 4);
            p.Children.Add(i);
        }
        return p;
    }

    public static TextBlock L(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };

    /// <summary>Two-column label/control form.</summary>
    public static Avalonia.Controls.Grid Form(params (string Label, Control Control)[] rows)
    {
        var g = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        for (var i = 0; i < rows.Length; i++)
        {
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            // Long labels (audit questions) wrap instead of squeezing the input column.
            var l = new TextBlock
            {
                Text = rows[i].Label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 10, 3),
                TextWrapping = TextWrapping.Wrap, MaxWidth = 230,
            };
            Avalonia.Controls.Grid.SetRow(l, i);
            var c = rows[i].Control;
            c.Margin = new Thickness(0, 3);
            Avalonia.Controls.Grid.SetRow(c, i);
            Avalonia.Controls.Grid.SetColumn(c, 1);
            g.Children.Add(l);
            g.Children.Add(c);
        }
        return g;
    }

    public static TextBox Text(string? watermark = null, int maxLen = 0, string? value = null) => new()
    {
        PlaceholderText = watermark, MaxLength = maxLen, Text = value, MinWidth = 160,
    };

    public static TextBox Number(double value = 0) => new() { Text = value.ToString("0.##", CultureInfo.InvariantCulture), MinWidth = 120 };

    public static double ParseNumber(string? s)
    {
        s = (s ?? "").Trim().Replace(" ", "");
        if (s.Length == 0) return 0;
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Any, Id, out v)) return v;
        throw new FormatException($"'{s}' bukan angka.");
    }

    public static ComboBox Choice(IEnumerable<string> items, string? selected = null)
    {
        var list = items.ToList();
        var c = new ComboBox { ItemsSource = list, MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Stretch };
        c.SelectedIndex = selected is null ? (list.Count > 0 ? 0 : -1) : list.IndexOf(selected);
        return c;
    }

    /// <summary>Panel berbingkai seperti panel input dan bar bawah di Registrasi.</summary>
    public static Border Panel(Control child, Thickness padding = default) => Themed(Themed(new Border
    {
        Child = child, Padding = padding, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
    }, Border.BorderBrushProperty, "ThemeBorderBrush"), Border.BackgroundProperty, "ThemePanelBrush");

    /// <summary>Judul layar dengan garis tepi biru, sama dengan Registrasi.</summary>
    public static Border Heading(string title, string? subtitle = null)
    {
        var isi = new StackPanel { Spacing = 1, Children = { Title(title) } };
        if (subtitle is not null) isi.Children.Add(Hint(subtitle));
        return new Border { Child = isi, Classes = { "tegasHeading" } };
    }

    /// <summary>Panel "Input Data" yang bisa dilipat di atas tabel, seperti PanelInput Khanza.</summary>
    public static (Border Panel, ToggleButton Toggle) InputPanel(Control content)
    {
        var body = new Border { Name = "InputDataPanel", Padding = new Thickness(10, 7), Child = content, IsVisible = false };
        var chevron = new Avalonia.Controls.Shapes.Path
        {
            Width = 12, Height = 8, Stretch = Stretch.Uniform, StrokeThickness = 1.7,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, VerticalAlignment = VerticalAlignment.Center,
            Data = Geometry.Parse("M2,2 L7,7 L12,2"),
        };
        chevron.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, chevron.GetResourceObservable("ThemeIconBrush"));
        var toggle = new ToggleButton
        {
            Name = "InputDataToggle", Classes = { "inputData" }, Padding = new Thickness(12, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 7,
                Children = { chevron, new TextBlock { Text = "Input Data", VerticalAlignment = VerticalAlignment.Center } },
            },
        };
        Avalonia.Automation.AutomationProperties.SetName(toggle, "Input Data");
        toggle.IsCheckedChanged += (_, _) => body.IsVisible = toggle.IsChecked == true;
        return (Panel(new StackPanel { Children = { body, toggle } }), toggle);
    }

    /// <summary>Isian dibagi ke beberapa kolom (urut ke bawah, lalu ke kanan) agar form di atas tabel tetap pendek.</summary>
    public static Avalonia.Controls.Grid FormColumns(IReadOnlyList<(string Label, Control Control)> rows, int columns)
    {
        var grid = new Avalonia.Controls.Grid { ColumnSpacing = 18 };
        var perColumn = (rows.Count + columns - 1) / columns;
        for (var i = 0; i < columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var form = Form(rows.Skip(i * perColumn).Take(perColumn).ToArray());
            form.VerticalAlignment = VerticalAlignment.Top;
            Avalonia.Controls.Grid.SetColumn(form, i);
            grid.Children.Add(form);
        }
        return grid;
    }

    /// <summary>Wraps a TextBlock grid row into a bordered, scrollable box.</summary>
    public static Border Box(Control child) => Themed(new Border
    {
        Child = child, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
    }, Border.BorderBrushProperty, "ThemeBorderBrush");
}

/// <summary>Searchable "kode - nama" picker over a lookup list.</summary>
public class LookupBox : AutoCompleteBox
{
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    private List<(string Kode, string Nama)> _items = [];

    public LookupBox()
    {
        FilterMode = AutoCompleteFilterMode.ContainsOrdinal;
        MinimumPrefixLength = 0;
        MinWidth = 240;
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    public void SetItems(IEnumerable<(string Kode, string Nama)> items)
    {
        _items = items.ToList();
        ItemsSource = _items.Select(i => $"{i.Kode} - {i.Nama}").ToList();
    }

    public string Kode
    {
        get
        {
            var t = (SelectedItem as string ?? Text ?? "").Trim();
            var idx = t.IndexOf(" - ", StringComparison.Ordinal);
            var kode = idx >= 0 ? t[..idx] : t;
            return _items.Any(i => i.Kode == kode) ? kode : "";
        }
        set
        {
            var it = _items.FirstOrDefault(i => i.Kode == value);
            Text = it.Kode is null ? "" : $"{it.Kode} - {it.Nama}";
            SelectedItem = it.Kode is null ? null : Text;
        }
    }

    public string Nama => _items.FirstOrDefault(i => i.Kode == Kode).Nama ?? "";
}

/// <summary>Date + time editor returning one DateTime.</summary>
public class DateTimeBox : StackPanel
{
    private readonly CalendarDatePicker _date = new() { SelectedDate = DateTime.Today };
    private readonly TimePicker _time = new() { SelectedTime = DateTime.Now.TimeOfDay, ClockIdentifier = "24HourClock", MinWidth = 0, Width = 150 };

    public DateTimeBox(DateTime? value = null, bool withTime = true)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        Children.Add(_date);
        if (withTime) Children.Add(_time);
        if (value is { } v) Value = v;
    }

    public DateTime Value
    {
        get => (_date.SelectedDate ?? DateTime.Today).Date + (_time.SelectedTime ?? TimeSpan.Zero);
        set
        {
            _date.SelectedDate = value.Date;
            _time.SelectedTime = new TimeSpan(value.Hour, value.Minute, value.Second);
        }
    }

    public DateTime? SelectedValue
    {
        get => _date.SelectedDate.HasValue ? Value : null;
        set
        {
            if (value is { } date) Value = date;
            else
            {
                _date.SelectedDate = null;
                _time.SelectedTime = null;
            }
        }
    }
}

/// <summary>Base for the code-built feature pages: title + back button + status line.</summary>
public abstract class PageView : UserControl
{
    public event EventHandler? BackRequested;
    protected readonly TextBlock StatusText = Ui.Status();

    /// <summary>Susunan seperti Registrasi: judul, isi, lalu bar bawah (filter dan aksi, status, kembali).</summary>
    protected void Build(string title, Control body, string? hint = null, Control? footer = null)
    {
        var back = new Button { Content = "Kembali", VerticalAlignment = VerticalAlignment.Bottom };
        back.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);

        // Baris status kosong tidak memakan tinggi bar.
        StatusText.IsVisible = !string.IsNullOrEmpty(StatusText.Text);
        StatusText.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty) StatusText.IsVisible = !string.IsNullOrEmpty(StatusText.Text);
        };
        var left = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Bottom };
        if (footer is not null) left.Children.Add(footer);
        left.Children.Add(StatusText);
        var bottom = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        Avalonia.Controls.Grid.SetColumn(back, 1);
        bottom.Children.Add(left);
        bottom.Children.Add(back);

        var root = new Avalonia.Controls.Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 6, Margin = new Thickness(16, 12),
        };
        var panel = Ui.Panel(bottom, new Thickness(9, 6));
        Avalonia.Controls.Grid.SetRow(body, 1);
        Avalonia.Controls.Grid.SetRow(panel, 2);
        root.Children.Add(Ui.Heading(title, hint));
        root.Children.Add(body);
        root.Children.Add(panel);
        Content = root;
        this.Bind(BackgroundProperty, this.GetResourceObservable("TegasPageBrush"));
    }

    public abstract Task ReloadAsync();

    protected async Task RunAsync(Func<Task> action, string? okMessage = null)
    {
        try
        {
            Ui.Info(StatusText, "Memproses...");
            await action();
            if (okMessage is not null) Ui.Ok(StatusText, okMessage);
            else if (StatusText.Text == "Memproses...") StatusText.Text = "";
        }
        catch (Exception ex)
        {
            Ui.Err(StatusText, ex.Message);
        }
    }
}

/// <summary>Base for small code-built modal dialogs with OK/Batal and an error line.</summary>
public class FormDialog : Window
{
    protected readonly TextBlock ErrorText = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#DC2626") };
    private readonly Button _ok;
    public bool Saved { get; private set; }

    protected FormDialog(string title, double width = 560)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 780;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _ok = new Button { Content = "Simpan", MinWidth = 100 };
        _ok.Click += async (_, _) =>
        {
            _ok.IsEnabled = false;
            ErrorText.Text = "";
            try
            {
                await OnSaveAsync();
                Saved = true;
                Close();
            }
            catch (Exception ex)
            {
                ErrorText.Text = ex.Message;
            }
            finally
            {
                _ok.IsEnabled = true;
            }
        };
    }

    protected void SetBody(Control body, string okText = "Simpan")
    {
        _ok.Content = okText;
        var batal = new Button { Content = "Batal", MinWidth = 90 };
        batal.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { _ok, batal } };
        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(ErrorText, Dock.Bottom);
        ErrorText.Margin = new Thickness(0, 8);
        root.Children.Add(buttons);
        root.Children.Add(ErrorText);
        root.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = root;
    }

    protected virtual Task OnSaveAsync() => Task.CompletedTask;

    public async Task<bool> ShowForAsync(Control owner)
    {
        if (TopLevel.GetTopLevel(owner) is Window w) await ShowDialog(w);
        else Show();
        return Saved;
    }
}
