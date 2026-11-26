using Avalonia.Controls;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Keuangan;

/// <summary>Manual balanced journal entry (menu K, Posting Jurnal).</summary>
public sealed class PostingJurnalView : PageView
{
    private readonly ManualJournalRepository _repo = new(DatabaseSettings.Load());
    private readonly TextBox _bukti = Ui.Text("No. bukti", 30);
    private readonly TextBox _keterangan = Ui.Text("Keterangan jurnal", 350);
    private readonly LookupBox _rekening = new() { PlaceholderText = "Cari rekening", MinWidth = 320 };
    private readonly TextBox _debet = Ui.Number();
    private readonly TextBox _kredit = Ui.Number();
    private readonly TextBlock _total = Ui.L("");
    private readonly DataGrid _grid = Ui.Grid(
        new("kd_rek", "Kode Rekening"), new("nm_rek", "Nama Rekening"), new("debet", "Debet"), new("kredit", "Kredit"));
    private readonly List<JurnalLine> _lines = [];
    private readonly Control _form;
    private bool _busy;

    public PostingJurnalView()
    {
        _form = new StackPanel
        {
            Children =
            {
                Ui.Bar(Ui.L("No. Bukti"), _bukti, Ui.L("Keterangan"), _keterangan),
                Ui.Bar(Ui.L("Rekening"), _rekening, Ui.L("Debet"), _debet, Ui.L("Kredit"), _kredit,
                    Ui.Btn("Tambah Baris", (_, _) => AddLine()), Ui.Btn("Hapus Baris", (_, _) => RemoveLine()),
                    Ui.Btn("Posting Jurnal", async (_, _) => await PostAsync())),
                Ui.Bar(_total),
            },
        };
        var body = new DockPanel();
        DockPanel.SetDock(_form, Dock.Top);
        body.Children.Add(_form);
        body.Children.Add(_grid);
        Build("Posting Jurnal", body, "Jurnal manual: tambahkan baris rekening hingga debet sama dengan kredit, lalu posting.");
        Refresh();
    }

    public override async Task ReloadAsync()
    {
        if (_busy) return;
        _busy = true;
        try { await RunAsync(async () => _rekening.SetItems((await _repo.AccountsAsync()).Select(a => (a.Kode, $"{a.Kode} - {a.Nama}")))); }
        finally { _busy = false; }
    }

    private void AddLine()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_rekening.Kode)) throw new ArgumentException("Pilih rekening terlebih dahulu.");
            var debet = Ui.ParseNumber(_debet.Text);
            var kredit = Ui.ParseNumber(_kredit.Text);
            if (debet < 0 || kredit < 0 || (debet > 0) == (kredit > 0))
                throw new ArgumentException("Isi salah satu: debet atau kredit, bernilai lebih dari nol.");
            var name = (_rekening.Text ?? "").Split(" - ", 2).Last();
            _lines.Add(new JurnalLine(_rekening.Kode, name, debet, kredit));
            _rekening.Text = "";
            _debet.Text = "0";
            _kredit.Text = "0";
            Refresh();
        }
        catch (Exception ex) { Ui.Err(StatusText, ex.Message); }
    }

    private void RemoveLine()
    {
        var index = _grid.SelectedIndex;
        if (index < 0 || index >= _lines.Count) return;
        _lines.RemoveAt(index);
        Refresh();
    }

    private void Refresh()
    {
        Ui.FillRows(_grid, _lines.Select(l => new GridRow([l.KdRek, l.NmRek, Ui.Money(l.Debet), Ui.Money(l.Kredit)], l)));
        var debet = _lines.Sum(l => l.Debet);
        var kredit = _lines.Sum(l => l.Kredit);
        _total.Text = $"Total debet {Ui.Money(debet)}  |  total kredit {Ui.Money(kredit)}  |  selisih {Ui.Money(debet - kredit)}";
    }

    private async Task PostAsync()
    {
        if (_busy) return;
        _busy = true;
        _form.IsEnabled = false;
        try
        {
            await RunAsync(async () =>
            {
                await _repo.PostAsync(_bukti.Text ?? "", _keterangan.Text ?? "", _lines.ToList());
                _lines.Clear();
                _bukti.Text = "";
                _keterangan.Text = "";
                Refresh();
            }, "Jurnal berhasil diposting.");
        }
        finally
        {
            _form.IsEnabled = true;
            _busy = false;
        }
    }
}
