using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatJalan;

internal sealed class ObgynRalanView : UserControl
{
    private readonly PemeriksaanObgynService _service;
    private readonly CalendarDatePicker _date = new() { SelectedDate = DateTime.Today };
    private readonly TextBox _time = new() { Text = DateTime.Now.ToString("HH:mm:ss"), Width = 110 };
    private readonly DataGrid _grid = Ui.Grid(new("tanggal", "Tanggal", 120), new("jam", "Jam", 100));
    private readonly TextBlock _status = Ui.Status();
    private readonly Control[] _fields;
    private readonly Grid _layout;
    private bool _busy;
    private readonly string _noRawat;
    private readonly string _title;

    public ObgynRalanView(string noRawat, AuthenticatedUser user, bool obstetri, bool ranap = false)
    {
        _noRawat = noRawat;
        if (KunciTanggal.Aktif(DatabaseSettings.Load(), user.IsAdminUtama)) _date.IsEnabled = _time.IsEnabled = false;
        _title = (obstetri ? "Pemeriksaan Obstetri" : "Pemeriksaan Ginekologi") + (ranap ? " Rawat Inap" : "");
        _service = new(DatabaseSettings.Load(), noRawat, user, obstetri, ranap);
        _fields = _service.Fields.Select(f => f.Choices is { } choices
            ? (Control)new ComboBox { ItemsSource = choices, SelectedIndex = 0 }
            : new TextBox { MaxLength = f.Max }).ToArray();
        var entries = _service.Fields.Select((f, i) => (f.Label, _fields[i])).ToArray();
        var form = Ui.Form([("Tanggal dan jam", Ui.Bar(_date, _time)), .. entries]);
        _grid.SelectionChanged += (_, _) => { if (Ui.Selected<Row>(_grid) is { } row) Isi(row); };
        _layout = new Grid { RowDefinitions = new("Auto,160,*,Auto,Auto"), RowSpacing = 8, Margin = new Avalonia.Thickness(12) };
        Control[] controls = [Ui.Title(_title), _grid,
            new ScrollViewer { Content = form }, Ui.Bar(Ui.Btn("Simpan baru", async (_, _) => await JalankanAsync(SimpanAsync)), Ui.Btn("Ubah", async (_, _) => await JalankanAsync(UbahAsync)), Ui.Btn("Hapus", async (_, _) => await JalankanAsync(HapusAsync)), Ui.Btn("Simpan PDF", async (_, _) => await JalankanAsync(CetakAsync)), Ui.Btn("Muat ulang", async (_, _) => await JalankanAsync(MuatAsync))), _status];
        for (var i = 0; i < controls.Length; i++) { Grid.SetRow(controls[i], i); _layout.Children.Add(controls[i]); }
        Content = _layout;
        Loaded += async (_, _) => await JalankanAsync(MuatAsync);
    }
    private async Task MuatAsync()
    {
        var rows = await _service.DaftarAsync();
        Ui.FillRows(_grid, rows.Select(r => new GridRow([r.S("tgl_perawatan"), r.S("jam_rawat")], r)));
        Ui.Info(_status, $"{rows.Count} pemeriksaan tersimpan.");
    }
    private async Task SimpanAsync()
    {
        var (waktu, values) = Input();
        await _service.SimpanAsync(waktu, values);
        await MuatAsync(); Ui.Ok(_status, "Pemeriksaan tersimpan.");
    }
    private (DateTime, string[]) Input()
    {
        if (_date.SelectedDate is not { } date || !TimeOnly.TryParseExact(_time.Text, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new ArgumentException("Isi tanggal dan jam berformat HH:mm:ss.");
        var values = _fields.Select(c => c is ComboBox combo ? combo.SelectedItem as string ?? "" : ((TextBox)c).Text?.Trim() ?? "").ToArray();
        return (date.Date + time.ToTimeSpan(), values);
    }
    private Row Terpilih() => Ui.Selected<Row>(_grid) ?? throw new InvalidOperationException("Pilih pemeriksaan pada daftar.");
    private void Isi(Row row)
    {
        _date.SelectedDate = row.Dt("tgl_perawatan");
        _time.Text = row.S("jam_rawat");
        for (var i = 0; i < _fields.Length; i++)
            if (_fields[i] is ComboBox combo) combo.SelectedItem = row.S(_service.Fields[i].Name);
            else ((TextBox)_fields[i]).Text = row.S(_service.Fields[i].Name);
    }
    private async Task UbahAsync()
    {
        var selected = Terpilih(); var (waktu, values) = Input();
        await _service.UbahAsync(selected, waktu, values);
        await MuatAsync(); Ui.Ok(_status, "Pemeriksaan diubah.");
    }
    private async Task HapusAsync()
    {
        var selected = Terpilih();
        if (!await Dialogs.ConfirmAsync(this, "Hapus pemeriksaan", $"Hapus pemeriksaan {selected.S("tgl_perawatan")} {selected.S("jam_rawat")}?") ) return;
        await _service.HapusAsync(selected);
        await MuatAsync(); Ui.Ok(_status, "Pemeriksaan dihapus.");
    }
    private async Task CetakAsync()
    {
        var selected = Terpilih();
        var row = (await _service.DaftarAsync()).SingleOrDefault(r => r.Dt("tgl_perawatan") == selected.Dt("tgl_perawatan") && Equals(r["jam_rawat"], selected["jam_rawat"]))
            ?? throw new InvalidOperationException("Pemeriksaan tidak ditemukan lagi. Muat ulang daftar.");
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Simpan pemeriksaan sebagai PDF", SuggestedFileName = _title + ".pdf", FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }] });
        if (file is null) return;
        QuestPDF.Settings.License = LicenseType.Community;
        using var pdf = new MemoryStream();
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Text(_title).Bold().FontSize(15);
            page.Content().PaddingTop(12).Column(col =>
            {
                col.Item().Text($"No. Rawat: {_noRawat}   Tanggal: {row.S("tgl_perawatan")} {row.S("jam_rawat")}");
                foreach (var field in _service.Fields) col.Item().PaddingTop(5).Text($"{field.Label}: {row.S(field.Name)}");
            });
        })).GeneratePdf(pdf);
        pdf.Position = 0;
        await using var target = await file.OpenWriteAsync();
        if (target.CanSeek) target.SetLength(0);
        await pdf.CopyToAsync(target);
        Ui.Ok(_status, "Pemeriksaan tersimpan sebagai PDF.");
    }
    private async Task JalankanAsync(Func<Task> action)
    {
        if (_busy) return; _busy = true; _layout.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Ui.Err(_status, ex.Message); }
        catch (Exception) { Ui.Err(_status, "Pemeriksaan gagal diproses. Periksa koneksi atau waktu yang sudah tercatat."); }
        finally { _layout.IsEnabled = true; _busy = false; }
    }
}
