using Avalonia.Controls;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.MenuA;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Controls;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Informasi;

public sealed class MenuAView : MasterView
{
    private readonly MasterSpec _spec;
    private readonly string? _permintaanKfr;
    private readonly MenuAQueries _queries = new(DatabaseSettings.Load());

    public MenuAView(MasterSpec spec, string? permintaanKfr = null)
        : base(ApplyPermissions(spec), permintaanKfr, DateTime.Today.AddDays(1 - DateTime.Today.Day))
    {
        _spec = spec;
        _permintaanKfr = permintaanKfr;
        if (spec.CanInsert && spec.Fields.Any(f => f.Column is "no_rawat" or "no_rkm_medis"))
            AddCommand("Pilih Pasien", PilihPasienAsync);
        if (spec == MenuASpecs.PermintaanRanap && AppSession.CurrentUser?.HasPermission("kamar_inap") == true)
            AddCommand("Masukkan ke Rawat Inap", MasukkanRawatInapAsync);
        if (spec == MenuASpecs.PermintaanKfr && BolehLayaniKfr)
            AddCommand("Sesi dan Riwayat Program", () =>
            {
                SesiKfrRequested?.Invoke(this, Pilihan().S("no_rawat"));
                return Task.CompletedTask;
            });
        if (spec == MenuASpecs.PerkiraanBiaya) AddCommand("Rujukan Tarif", PilihTarifAsync);
        if (spec == MenuASpecs.ResepPulang)
        {
            AddCommand("Pilih Stok dan Harga", PilihStokResepAsync);
            AddCommand("Validasi Permintaan Resep", LayaniPermintaanResepAsync);
            AddCommand("Catat Serah Terima Obat", CatatSerahResepAsync);
        }
        AddCommand("Simpan Daftar PDF", () => SimpanPdfAsync(ListedRows, perPasien: false));
        AddCommand(spec == MenuASpecs.Deposit ? "Simpan Bukti Deposit PDF" : "Simpan Data Terpilih PDF",
            () => SimpanPdfAsync([Pilihan()], perPasien: true));
    }

    public event EventHandler<string>? SesiKfrRequested;

    private static bool BolehLayaniKfr => AppSession.CurrentUser?.HasPermission("layanan_program_kfr") == true;

    private static MasterSpec ApplyPermissions(MasterSpec spec) =>
        spec == MenuASpecs.PermintaanKfr && !BolehLayaniKfr ? spec with { Actions = [] } : spec;

    protected override void InitializeNew()
    {
        if (_permintaanKfr is not null) SetField("no_rawat_layanan", _permintaanKfr);
        if (_spec.Fields.Any(f => f.Column == "nip"))
            SetField("nip", AppSession.CurrentUser?.UserId);
    }

    private Row Pilihan() => SelectedRow ?? throw new InvalidOperationException("Pilih data terlebih dahulu.");

    private async Task PilihPasienAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var kunjungan = _spec.Fields.Any(f => f.Column == "no_rawat");
        var key = kunjungan ? "no_rawat" : "no_rkm_medis";
        var row = await SearchPickerDialog.ShowAsync<Row>(owner, "Pilih Pasien",
            q => _queries.CariPasienAsync(q, kunjungan),
            r => kunjungan ? $"{r.S(key)}  •  {r.S("no_rkm_medis")}  •  {r.S("nm_pasien")}"
                          : $"{r.S(key)}  •  {r.S("nm_pasien")}");
        if (row is not null) SetField(key, row.S(key));
    }

    private async Task MasukkanRawatInapAsync()
    {
        var row = Pilihan();
        if (!await Dialogs.ConfirmAsync(this, "Masukkan ke Rawat Inap", $"Masukkan {row.S("nm_pasien")} ke kamar {row.S("kd_kamar")}?")) return;
        using var db = SimrsGramedikaDbContextFactory.Create(DatabaseSettings.Load());
        await new RawatInapRepository(db).AdmitAsync(row.S("no_rawat"), row.S("kd_kamar"), row.S("diagnosa"));
        await ReloadAsync();
        Ui.Ok(StatusText, "Pasien sudah tercatat masuk rawat inap.");
    }

    private async Task PilihTarifAsync()
    {
        var diagnosis = ReadField("kd_penyakit");
        if (diagnosis.Length == 0) throw new InvalidOperationException("Pilih diagnosis terlebih dahulu.");
        var pilihan = await _queries.RujukanTarifAsync(diagnosis);
        if (pilihan.Count == 0) throw new InvalidOperationException("Tidak ada riwayat biaya untuk diagnosis ini dalam dua tahun terakhir.");
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var row = await SearchPickerDialog.ShowAsync<Row>(owner, "Pilih Rujukan Tarif",
            q => Task.FromResult<IReadOnlyList<Row>>(pilihan.Where(r => r.S("sumber").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList()),
            r => $"{r.S("sumber")}  •  Rp {Ui.Money(r.D("tarif"))}");
        if (row is not null) SetField("tarif", row.D("tarif"));
    }

    private async Task PilihStokResepAsync()
    {
        var rawat = ReadField("no_rawat");
        var depo = ReadField("kd_bangsal");
        if (rawat.Length == 0 || depo.Length == 0) throw new InvalidOperationException("Pilih pasien dan depo terlebih dahulu.");
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var resep = new ResepPulangRepository(DatabaseSettings.Load());
        var row = await SearchPickerDialog.ShowAsync<Row>(owner, "Pilih Stok Obat di Depo",
            q => resep.CariStokAsync(rawat, depo, q),
            r => $"{r.S("kode_brng")} • {r.S("nama_brng")} • {r.S("no_batch")} • Stok {r.S("stok")} • Rp {Ui.Money(r.D("harga"))}");
        if (row is null) return;
        if (row["harga"] is null) throw new InvalidOperationException("Harga obat belum tersedia.");
        SetField("kode_brng", row.S("kode_brng"));
        SetField("no_batch", row.S("no_batch"));
        SetField("no_faktur", row.S("no_faktur"));
        SetField("harga", row.D("harga"));
    }

    private async Task LayaniPermintaanResepAsync()
    {
        if (AppSession.CurrentUser is not { } user) return;
        var depo = ReadField("kd_bangsal");
        if (depo.Length == 0) throw new InvalidOperationException("Pilih depo pengeluaran obat terlebih dahulu.");
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var resep = new ResepPulangRepository(DatabaseSettings.Load());
        var row = await SearchPickerDialog.ShowAsync<Row>(owner, "Pilih Permintaan Resep Pulang", q => resep.CariPermintaanAsync(user, q),
            r => $"{r.S("no_permintaan")} • {r.S("nm_pasien")} • {r.S("obat")}");
        if (row is null) return;
        var noRawat = row.S("no_rawat");
        var snapshot = await resep.MuatDrafAsync(user, noRawat, row.S("no_permintaan"),
            row.S("tgl_permintaan"), row.S("jam"), farmasi: true);
        var obat = string.Join("\n", snapshot.Obat.Select(x => $"{x.Nama} · {x.Isi.Jumlah} {x.Satuan} · {x.Isi.Dosis}"));
        if (!await Dialogs.ConfirmAsync(this, "Validasi Resep Pulang",
                $"Keluarkan obat untuk {row.S("nm_pasien")} dari depo {depo}?\n{obat}")) return;
        await resep.ProsesPermintaanAsync(user, snapshot.Nomor, depo,
            noRawat, snapshot.Tanggal, snapshot.Jam, snapshot);
        await ReloadAsync();
        Ui.Ok(StatusText, "Resep sudah divalidasi. Catat serah terima setelah obat diberikan.");
    }

    private async Task CatatSerahResepAsync()
    {
        if (AppSession.CurrentUser is not { } user || TopLevel.GetTopLevel(this) is not Window owner) return;
        var repo = new ResepPulangRepository(DatabaseSettings.Load());
        var row = await SearchPickerDialog.ShowAsync<Row>(owner, "Pilih Resep yang Sudah Divalidasi",
            q => repo.CariSiapSerahAsync(user, q),
            r => $"{r.S("no_permintaan")} · {r.S("no_rawat")} · {r.S("nm_pasien")} · {r.S("obat")}");
        if (row is null) return;

        var dialog = new SerahResepDialog(repo, user, row);
        await dialog.ShowDialog(owner);
        if (dialog.Saved) Ui.Ok(StatusText, "Serah terima obat pulang sudah dicatat.");
    }

    private sealed class SerahResepDialog : FormDialog
    {
        private readonly TextBox _penerima = new() { MaxLength = 100, PlaceholderText = "Nama penerima obat" };
        private readonly ResepPulangRepository _repo;
        private readonly AuthenticatedUser _user;
        private readonly string _nomor;

        public SerahResepDialog(ResepPulangRepository repo, AuthenticatedUser user, Row row)
            : base("Serah Terima Obat Pulang")
        {
            _repo = repo;
            _user = user;
            _nomor = row.S("no_permintaan");
            Avalonia.Automation.AutomationProperties.SetName(_penerima, "Nama penerima obat");
            SetBody(new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Ui.Hint($"{row.S("nm_pasien")} · {row.S("no_rawat")} · {_nomor}"),
                    Ui.Hint(row.S("obat")),
                    new TextBlock { Text = "Nama penerima obat" },
                    _penerima,
                },
            }, "Konfirmasi Sudah Diserahkan");
        }

        protected override Task OnSaveAsync() => _repo.CatatSerahAsync(_user, _nomor, _penerima.Text ?? "");
    }

    private async Task SimpanPdfAsync(IReadOnlyList<Row> rows, bool perPasien)
    {
        if (rows.Count == 0) throw new InvalidOperationException("Tidak ada data untuk disimpan.");
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var title = _spec == MenuASpecs.Deposit && perPasien ? "Bukti Deposit Pasien" : _spec.Title;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Simpan PDF",
            SuggestedFileName = title + ".pdf",
            FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
        });
        if (file is null) return;

        var display = _spec.Display.Select(x => new Col(x.Column, x.Header)).ToList();
        if (perPasien && _spec == MenuASpecs.DeteksiCorona)
            display.AddRange(_spec.Fields.Where(f => display.All(c => c.Name != f.Column) && f.Column != "nip")
                .Select(f => new Col(f.Column, f.Label)));
        QuestPDF.Settings.License = LicenseType.Community;
        using var pdf = new MemoryStream();
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(perPasien ? PageSizes.A4 : PageSizes.A4.Landscape());
            page.Margin(28);
            page.Header().Column(header =>
            {
                header.Item().Text("SIMRS Gramedika").Bold().FontSize(14);
                header.Item().PaddingBottom(10).Text(title).FontSize(11);
            });
            page.DefaultTextStyle(s => s.FontSize(perPasien ? 10 : 8));
            if (perPasien)
            {
                page.Content().Column(form =>
                {
                    foreach (var col in display)
                        form.Item().PaddingBottom(5).Text($"{col.Header}: {Ui.Fmt(rows[0], col)}");
                });
            }
            else
            {
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        foreach (var col in display) cols.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var col in display) header.Cell().Padding(3).Text(col.Header).Bold();
                    });
                    foreach (var row in rows)
                    foreach (var col in display)
                        table.Cell().Padding(3).Text(Ui.Fmt(row, col));
                });
            }
        })).GeneratePdf(pdf);
        pdf.Position = 0;
        await using var target = await file.OpenWriteAsync();
        if (target.CanSeek) target.SetLength(0);
        await pdf.CopyToAsync(target);
        Ui.Ok(StatusText, "PDF tersimpan.");
    }
}
