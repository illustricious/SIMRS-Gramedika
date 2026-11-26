using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.UI.Modules.Shared;

/// <summary>
/// Generic list + form editor for one legacy table described by a <see cref="MasterSpec"/>
/// (Lab Kesling masters, K3, audit PPI, riwayat kepegawaian, presensi, ...).
/// </summary>
public class MasterView : PageView
{
    private readonly MasterSpec _spec;
    private readonly MasterRepository _repo = new(DatabaseSettings.Load());
    private readonly DataGrid _grid;
    private readonly TextBox _cari = Ui.Text("Cari...");
    private readonly PeriodPicker? _period;
    private readonly WrapPanel _commands;
    private readonly ToggleButton? _input;
    private readonly Dictionary<string, Control> _editors = new();
    private readonly TextBlock _total = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _score = new() { FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
    private Dictionary<string, object?>? _editingKeys;
    private bool _loading;

    public MasterView(MasterSpec spec, string? initialSearch = null, DateTime? initialFrom = null)
    {
        _spec = spec;
        _cari.Text = initialSearch;
        _grid = Ui.Grid(spec.Display.Select(d => new Col(d.Column, d.Header)).ToArray());
        _grid.SelectionChanged += (_, _) => { if (Ui.Selected<Row>(_grid) is { } r) Edit(r); };
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await LoadAsync(); };
        if (spec.Period != PeriodFilter.None) _period = new PeriodPicker(spec.Period == PeriodFilter.Month, initialFrom);

        var editable = spec.CanInsert || spec.CanEdit;
        var formRows = new List<(string, Control)>();
        foreach (var f in spec.Fields)
        {
            var c = Editor(f);
            c.IsEnabled = editable && !f.AutoKey;
            _editors[f.Column] = c;
            formRows.Add((f.Label + " :", c));
        }
        if (spec.TotalColumn is not null) formRows.Add(("Total :", _total));
        if (spec.ScoreOf is not null) formRows.Add(("Nilai Kepatuhan :", _score));

        var buttons = new List<Control>();
        Button? save = null;
        if (spec.CanInsert && !spec.Singleton) buttons.Add(Ui.Btn("Baru", async (_, _) =>
        {
            _input!.IsChecked = true;
            await NewAsync();
        }));
        if (editable)
        {
            save = Ui.Btn("Simpan", async (_, _) => await SaveAsync());
            save.Classes.Add("accent");
            buttons.Add(save);
        }
        if (spec.CanDelete) buttons.Add(Ui.Btn("Hapus", async (_, _) => await DeleteAsync()));
        foreach (var a in spec.Actions) buttons.Add(Ui.Btn(a.Label, async (_, _) => await RunActionAsync(a)));

        _commands = Ui.Bar(buttons.ToArray());
        _commands.Margin = default;

        if (spec.Singleton)
        {
            _commands.IsEnabled = false;
            var single = Ui.FormColumns(formRows, formRows.Count > 8 ? 2 : 1);
            if (formRows.Count <= 8)
            {
                single.Width = 560;
                single.HorizontalAlignment = HorizontalAlignment.Left;
            }
            Build(spec.Title, Ui.Panel(new ScrollViewer { Content = single, Padding = new Thickness(12, 8, 24, 8) }),
                spec.Hint, _commands);
            return;
        }

        var filters = new List<Control> { _cari };
        _cari.MinWidth = 260;
        if (_period is not null) filters.Add(_period);
        filters.Add(Ui.Btn("Cari", async (_, _) => await LoadAsync()));
        var bar = Ui.Bar(filters.ToArray());
        bar.Margin = default;

        // Form di atas tabel, seperti PanelInput Khanza dan layar Registrasi.
        var columns = formRows.Count <= 3 ? 1 : formRows.Count <= 16 ? 2 : 3;
        var fields = Ui.FormColumns(formRows, columns);
        if (columns == 1)
        {
            fields.Width = 560;
            fields.HorizontalAlignment = HorizontalAlignment.Left;
        }
        var (panel, toggle) = Ui.InputPanel(new ScrollViewer { Content = fields, MaxHeight = 330, Padding = new Thickness(0, 0, 24, 0) });
        _input = toggle;
        // Simpan hanya aktif saat isiannya terlihat, sama dengan Registrasi.
        if (save is not null)
        {
            save.IsEnabled = false;
            toggle.IsCheckedChanged += (_, _) => save.IsEnabled = toggle.IsChecked == true;
        }

        var body = new Avalonia.Controls.Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 6 };
        Avalonia.Controls.Grid.SetRow(_grid, 1);
        body.Children.Add(panel);
        body.Children.Add(_grid);
        Build(spec.Title, body, spec.Hint, new StackPanel { Spacing = 4, Children = { bar, _commands } });
    }

    private Control Editor(MasterField f)
    {
        Control c = f.Kind switch
        {
            FieldKind.Choice => Ui.Choice(f.Choices!),
            FieldKind.Lookup => new LookupBox(),
            FieldKind.Number or FieldKind.Integer => Ui.Number(),
            FieldKind.Date => new CalendarDatePicker { SelectedDate = DateTime.Today, SelectedDateFormat = CalendarDatePickerFormat.Custom, CustomDateFormatString = "dd/MM/yyyy" },
            FieldKind.DateTime => new DateTimeBox(DateTime.Now),
            FieldKind.Time => Ui.Text("HH:mm:ss", 8),
            FieldKind.Memo => new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 64, MaxLength = f.MaxLen, MinWidth = 160 },
            _ => Ui.Text(maxLen: f.MaxLen),
        };
        if (f.Kind == FieldKind.Number && _spec.TotalOf is not null && c is TextBox tb) tb.TextChanged += (_, _) => UpdateTotal();
        if (_spec.ScoreOf?.Contains(f.Column) == true && c is ComboBox cb) cb.SelectionChanged += (_, _) => UpdateScore();
        if (f.Column == _spec.AutoKeyDateField && c is CalendarDatePicker dp)
            dp.SelectedDateChanged += async (_, _) => { if (_editingKeys is null && !_loading) await RenumberAsync(); };
        return c;
    }

    public override async Task ReloadAsync()
    {
        if (_spec.Singleton)
        {
            await RunAsync(async () =>
            {
                await LoadLookupsAsync();
                await LoadSingletonAsync();
            });
            return;
        }
        await RunAsync(LoadLookupsAsync);
        await LoadAsync();
        await NewAsync();
    }

    private async Task LoadLookupsAsync()
    {
        foreach (var f in _spec.Fields.Where(f => f.Kind == FieldKind.Lookup))
            ((LookupBox)_editors[f.Column]).SetItems(await _repo.LookupAsync(f.LookupSql!));
    }

    private async Task LoadSingletonAsync()
    {
        await NewAsync();
        var rows = await _repo.ListAsync(_spec, null);
        if (rows.Count > 1)
        {
            Ui.Err(StatusText, "Ada lebih dari satu baris pengaturan. Periksa data sebelum menyimpan.");
            return;
        }
        if (rows.Count == 1)
            foreach (var field in _spec.Fields) SetValue(field, rows[0][field.Column]);
        _commands.IsEnabled = true;
        StatusText.Text = rows.Count == 0 ? "Pengaturan belum diisi." : "";
    }

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var rows = await _repo.ListAsync(_spec, _cari.Text, _period?.From, _period?.To);
        Ui.Fill(_grid, rows);
        Ui.Info(StatusText, $"{rows.Count} data");
    });

    private void UpdateTotal()
    {
        try { _total.Text = Ui.Money(_spec.TotalOf!.Sum(c => Ui.ParseNumber(((TextBox)_editors[c]).Text))); }
        catch { _total.Text = "-"; }
    }

    private void UpdateScore()
    {
        var answers = _spec.ScoreOf!.Select(c => (_editors[c] as ComboBox)?.SelectedItem as string).ToList();
        var counted = answers.Count(a => a is "Ya" or "Tidak");
        _score.Text = counted == 0 ? "-" : $"{answers.Count(a => a == "Ya") * 100.0 / counted:0.##} %";
    }

    protected Row? SelectedRow => Ui.Selected<Row>(_grid);

    protected IReadOnlyList<Row> ListedRows => _grid.ItemsSource is IEnumerable<GridRow> rows
        ? rows.Select(r => r.Tag).OfType<Row>().ToArray() : [];

    protected void AddCommand(string label, Func<Task> execute) =>
        _commands.Children.Add(Ui.Btn(label, async (_, _) => await RunCommandAsync(execute)));

    protected string ReadField(string column) => Str(ReadValue(_spec.Fields.Single(f => f.Column == column)));

    protected void SetField(string column, object? value) => SetValue(_spec.Fields.Single(f => f.Column == column), value);

    protected virtual void InitializeNew() { }

    protected virtual double ParseNumber(string? value) => Ui.ParseNumber(value);

    private async Task NewAsync()
    {
        _loading = true;
        _editingKeys = null;
        foreach (var f in _spec.Fields) SetValue(f, f.DefaultFactory?.Invoke() ?? f.Default);
        InitializeNew();
        _loading = false;
        await RenumberAsync();
        _grid.SelectedItem = null;
        if (_spec.TotalOf is not null) UpdateTotal();
        if (_spec.ScoreOf is not null) UpdateScore();
    }

    private async Task RenumberAsync()
    {
        foreach (var f in _spec.Fields.Where(f => f.AutoKey))
        {
            var date = _spec.AutoKeyDateField is { } df ? _editors[df] switch
            {
                CalendarDatePicker dp => dp.SelectedDate,
                DateTimeBox dt => dt.Value.Date,
                _ => null,
            } : null;
            try { SetValue(f, await _repo.NextKeyAsync(_spec, date)); } catch { /* shown on save */ }
        }
    }

    private void Edit(Row r)
    {
        _loading = true;
        foreach (var f in _spec.Fields) SetValue(f, r[f.Column]);
        _loading = false;
        _editingKeys = _spec.Keys.ToDictionary(f => f.Column, f => r[f.Column]);
        if (_spec.TotalOf is not null) UpdateTotal();
        if (_spec.ScoreOf is not null) UpdateScore();
    }

    private static string Str(object? v) => v switch
    {
        null => "",
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
        TimeSpan t => t.ToString(@"hh\:mm\:ss"),
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss"),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
    };

    private void SetValue(MasterField f, object? v)
    {
        switch (_editors[f.Column])
        {
            case LookupBox lb: lb.Kode = Str(v); break;
            case ComboBox cb:
                var s = Str(v);
                cb.SelectedItem = f.Choices!.Contains(s) ? s : f.NullWhenEmpty ? null : f.Choices!.FirstOrDefault();
                break;
            case CalendarDatePicker dp: dp.SelectedDate = v is DateTime d ? d.Date : f.Key || f.Required ? DateTime.Today : null; break;
            case DateTimeBox dt when f.NullWhenEmpty: dt.SelectedValue = v is DateTime selected ? selected : null; break;
            case DateTimeBox dt: dt.Value = v is DateTime d2 ? d2 : DateTime.Now; break;
            case TextBox tb when f.Kind is FieldKind.Number or FieldKind.Integer:
                tb.Text = v is null && !f.NullWhenEmpty ? "0" : Convert.ToString(v, CultureInfo.InvariantCulture);
                break;
            case TextBox tb: tb.Text = Str(v); break;
        }
    }

    private Dictionary<string, object?> ReadValues()
        => _spec.Fields.ToDictionary(f => f.Column, ReadValue);

    private object? ReadValue(MasterField f) => _editors[f.Column] switch
    {
        LookupBox lb when f.NullWhenEmpty && string.IsNullOrWhiteSpace(lb.Kode) => null,
        LookupBox lb => lb.Kode,
        ComboBox cb => cb.SelectedItem as string,
        CalendarDatePicker dp => dp.SelectedDate?.Date,
        DateTimeBox dt when f.NullWhenEmpty => dt.SelectedValue,
        DateTimeBox dt => dt.Value,
        TextBox tb when f.NullWhenEmpty && string.IsNullOrWhiteSpace(tb.Text) => null,
        TextBox tb when f.Kind == FieldKind.Number => ParseNumber(tb.Text),
        TextBox tb when f.Kind == FieldKind.Integer => (long)Math.Round(ParseNumber(tb.Text)),
        TextBox tb when f.Kind == FieldKind.Time => ParseTime(f, tb.Text),
        TextBox tb => (tb.Text ?? "").Trim(),
        _ => null,
    };

    private static object? ParseTime(MasterField f, string? s)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return null;
        if (TimeSpan.TryParseExact(s, [@"hh\:mm\:ss", @"hh\:mm", @"h\:mm"], CultureInfo.InvariantCulture, out var t)) return t;
        throw new FormatException($"{f.Label}: format jam harus HH:mm atau HH:mm:ss.");
    }

    private async Task RunCommandAsync(Func<Task> execute)
    {
        if (!_commands.IsEnabled) return;
        _commands.IsEnabled = false;
        try { await RunAsync(execute); }
        finally { _commands.IsEnabled = true; }
    }

    private async Task SaveAsync() => await RunCommandAsync(async () =>
    {
        if (_spec.Singleton)
        {
            await _repo.SaveAsync(_spec, ReadValues(), null);
            Ui.Ok(StatusText, "Pengaturan tersimpan.");
            return;
        }
        if (_editingKeys is not null && SelectedRow is null) throw new InvalidOperationException("Pilih ulang data atau klik Baru.");
        if (_editingKeys is not null && !_spec.CanEdit) throw new InvalidOperationException("Data di menu ini tidak bisa diubah.");
        if (_editingKeys is null && !_spec.CanInsert) throw new InvalidOperationException("Pilih data yang akan diubah.");
        var wasNew = _editingKeys is null;
        var no = await _repo.SaveAsync(_spec, ReadValues(), _editingKeys);
        await LoadAsync();
        await NewAsync();
        Ui.Ok(StatusText, wasNew ? $"Data baru tersimpan{(no is { Length: > 0 } ? $" ({no})" : "")}." : "Perubahan tersimpan.");
    });

    private async Task DeleteAsync()
    {
        if (_editingKeys is null || SelectedRow is null) { Ui.Err(StatusText, "Pilih data yang akan dihapus."); return; }
        if (!await Dialogs.ConfirmAsync(this, "Hapus", "Hapus data terpilih?")) return;
        await RunCommandAsync(async () =>
        {
            await _repo.DeleteAsync(_spec, _editingKeys);
            await LoadAsync();
            await NewAsync();
            Ui.Ok(StatusText, "Data dihapus.");
        });
    }

    private async Task RunActionAsync(MasterAction a)
    {
        if (_editingKeys is null || SelectedRow is null) { Ui.Err(StatusText, "Pilih data terlebih dahulu."); return; }
        if (!await Dialogs.ConfirmAsync(this, a.Label, a.Confirm)) return;
        await RunCommandAsync(async () =>
        {
            await _repo.RunActionAsync(a, _editingKeys, SimrsGramedika.Core.Auth.AppSession.CurrentUser?.KodeLegacy ?? "");
            await LoadAsync();
            await NewAsync();
            Ui.Ok(StatusText, "Selesai.");
        });
    }
}

/// <summary>Date range (from/to pickers) or a month (tahun + bulan) filter.</summary>
public class PeriodPicker : StackPanel
{
    private readonly CalendarDatePicker _from = DatePick(DateTime.Today);
    private readonly CalendarDatePicker _to = DatePick(DateTime.Today);
    private readonly NumericUpDown _tahun = new() { Minimum = 2000, Maximum = 2100, Value = DateTime.Today.Year, Width = 110, FormatString = "0" };
    private readonly ComboBox _bulan = Ui.Choice(CultureInfo.GetCultureInfo("id-ID").DateTimeFormat.MonthNames.Take(12));
    private readonly bool _month;

    public PeriodPicker(bool month, DateTime? from = null)
    {
        _month = month;
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        if (month)
        {
            _bulan.SelectedIndex = DateTime.Today.Month - 1;
            _bulan.MinWidth = 120;
            Children.Add(Ui.L("Bulan"));
            Children.Add(_bulan);
            Children.Add(_tahun);
        }
        else
        {
            if (from is { } f) _from.SelectedDate = f;
            Children.Add(Ui.L("Tanggal"));
            Children.Add(_from);
            Children.Add(Ui.L("s/d"));
            Children.Add(_to);
        }
    }

    private static CalendarDatePicker DatePick(DateTime d) => new()
    {
        SelectedDate = d, SelectedDateFormat = CalendarDatePickerFormat.Custom, CustomDateFormatString = "dd/MM/yyyy", Width = 130,
    };

    public int Year => (int)(_tahun.Value ?? DateTime.Today.Year);
    public int Month => _bulan.SelectedIndex + 1;

    public DateTime From => _month ? new DateTime(Year, Month, 1) : (_from.SelectedDate ?? DateTime.Today).Date;
    public DateTime To => _month ? new DateTime(Year, Month, 1).AddMonths(1).AddDays(-1) : (_to.SelectedDate ?? DateTime.Today).Date;
}
