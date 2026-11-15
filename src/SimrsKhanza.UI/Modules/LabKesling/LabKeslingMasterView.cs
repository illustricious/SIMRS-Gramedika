using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SimrsKhanza.Config;
using SimrsKhanza.Data.LabKesling;
using SimrsKhanza.Data.Sql;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.LabKesling;

/// <summary>
/// Generic list + form editor for one Lab Kesling master table (legacy LabKeslingPelanggan,
/// LabKeslingMasterSampelBakuMutu, LabKeslingParameterPengujian, LabKeslingNilaiNormalBakuMutu).
/// </summary>
public class LabKeslingMasterView : PageView
{
    private readonly MasterSpec _spec;
    private readonly MasterRepository _repo = new(DatabaseSettings.Load());
    private readonly DataGrid _grid;
    private readonly TextBox _cari = Ui.Text("Cari...");
    private readonly Dictionary<string, Control> _editors = new();
    private readonly TextBlock _total = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private Dictionary<string, object?>? _editingKeys;

    public LabKeslingMasterView(MasterSpec spec)
    {
        _spec = spec;
        _grid = Ui.Grid(spec.Display.Select(d => new Col(d.Column, d.Header)).ToArray());
        _grid.SelectionChanged += (_, _) => { if (Ui.Selected<Row>(_grid) is { } r) Edit(r); };
        _cari.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await LoadAsync(); };

        var formRows = new List<(string, Control)>();
        foreach (var f in spec.Fields)
        {
            Control c = f.Kind switch
            {
                FieldKind.Choice => Ui.Choice(f.Choices!),
                FieldKind.Lookup => new LookupBox(),
                FieldKind.Number => Ui.Number(),
                _ => Ui.Text(maxLen: f.MaxLen),
            };
            if (f.Kind == FieldKind.Number && spec.TotalOf is not null && c is TextBox tb)
                tb.TextChanged += (_, _) => UpdateTotal();
            _editors[f.Column] = c;
            formRows.Add((f.Label + " :", c));
        }
        if (spec.TotalColumn is not null) formRows.Add(("Total :", _total));

        var form = new StackPanel
        {
            Spacing = 6, Width = 380,
            Children =
            {
                Ui.Form(formRows.ToArray()),
                Ui.Bar(Ui.Btn("➕ Baru", async (_, _) => await NewAsync()), Ui.Btn("💾 Simpan", async (_, _) => await SaveAsync()),
                    Ui.Btn("🗑 Hapus", async (_, _) => await DeleteAsync())),
            },
        };
        var left = new DockPanel();
        var bar = Ui.Bar(_cari, Ui.Btn("🔎 Cari", async (_, _) => await LoadAsync()));
        DockPanel.SetDock(bar, Dock.Top);
        left.Children.Add(bar);
        left.Children.Add(_grid);

        var body = new Avalonia.Controls.Grid { ColumnDefinitions = new ColumnDefinitions("*,16,Auto") };
        Avalonia.Controls.Grid.SetColumn(form, 2);
        body.Children.Add(left);
        body.Children.Add(new ScrollViewer { Content = form, [Avalonia.Controls.Grid.ColumnProperty] = 2 });
        Build(spec.Title, body, "Tabel yang sama dengan SIMRS Khanza lama — perubahan di sini langsung terlihat di sana.");
    }

    public override async Task ReloadAsync()
    {
        await RunAsync(async () =>
        {
            foreach (var f in _spec.Fields.Where(f => f.Kind == FieldKind.Lookup))
                ((LookupBox)_editors[f.Column]).SetItems(await _repo.LookupAsync(f.LookupSql!));
        });
        await LoadAsync();
        await NewAsync();
    }

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var rows = await _repo.ListAsync(_spec, _cari.Text);
        Ui.Fill(_grid, rows);
        Ui.Info(StatusText, $"{rows.Count} data");
    });

    private void UpdateTotal()
    {
        try
        {
            var sum = _spec.TotalOf!.Sum(c => Ui.ParseNumber(((TextBox)_editors[c]).Text));
            _total.Text = Ui.Money(sum);
        }
        catch { _total.Text = "-"; }
    }

    private async Task NewAsync()
    {
        _editingKeys = null;
        foreach (var f in _spec.Fields)
            SetValue(f, f.Kind == FieldKind.Number ? 0d : null);
        foreach (var f in _spec.Fields.Where(f => f.AutoKey))
        {
            try { SetValue(f, await _repo.NextKeyAsync(_spec)); } catch { /* shown on save */ }
        }
        _grid.SelectedItem = null;
        UpdateTotal();
    }

    private void Edit(Row r)
    {
        foreach (var f in _spec.Fields) SetValue(f, r[f.Column]);
        _editingKeys = _spec.Fields.Where(f => f.Key).ToDictionary(f => f.Column, f => (object?)r.S(f.Column));
        UpdateTotal();
    }

    private void SetValue(MasterField f, object? v)
    {
        var s = v switch { null => "", double d => d.ToString("0.##", CultureInfo.InvariantCulture), _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" };
        switch (_editors[f.Column])
        {
            case LookupBox lb: lb.Kode = s; break;
            case ComboBox cb: cb.SelectedItem = f.Choices!.Contains(s) ? s : f.Choices!.FirstOrDefault(); break;
            case TextBox tb: tb.Text = s; break;
        }
    }

    private Dictionary<string, object?> ReadValues()
    {
        var v = new Dictionary<string, object?>();
        foreach (var f in _spec.Fields)
        {
            v[f.Column] = _editors[f.Column] switch
            {
                LookupBox lb => lb.Kode,
                ComboBox cb => cb.SelectedItem as string,
                TextBox tb when f.Kind == FieldKind.Number => Ui.ParseNumber(tb.Text),
                TextBox tb => (tb.Text ?? "").Trim(),
                _ => null,
            };
        }
        return v;
    }

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        await _repo.SaveAsync(_spec, ReadValues(), _editingKeys);
        var wasNew = _editingKeys is null;
        await LoadAsync();
        await NewAsync();
        Ui.Ok(StatusText, wasNew ? "Data baru tersimpan." : "Perubahan tersimpan.");
    });

    private async Task DeleteAsync()
    {
        if (_editingKeys is null) { Ui.Err(StatusText, "Pilih data yang akan dihapus."); return; }
        if (!await Dialogs.ConfirmAsync(this, "Hapus", "Hapus data terpilih?")) return;
        await RunAsync(async () =>
        {
            await _repo.DeleteAsync(_spec, _editingKeys);
            await LoadAsync();
            await NewAsync();
            Ui.Ok(StatusText, "Data dihapus.");
        });
    }
}
