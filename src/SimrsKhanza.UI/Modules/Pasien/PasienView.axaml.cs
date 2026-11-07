using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;

namespace SimrsKhanza.UI.Modules.Pasien;

public partial class PasienView : UserControl
{
    public event EventHandler? BackRequested;

    public PasienView()
    {
        InitializeComponent();

        this.FindControl<ComboBox>("GolDarahCombo")!.ItemsSource = Enum.GetValues<GolonganDarah>();
        this.FindControl<ComboBox>("StatusNikahCombo")!.ItemsSource = Enum.GetValues<StatusPernikahan>();
        this.FindControl<ComboBox>("HubunganPjCombo")!.ItemsSource = Enum.GetValues<HubunganPenanggungJawab>();
        this.FindControl<ComboBox>("HubunganPjCombo")!.SelectedItem = HubunganPenanggungJawab.DiriSendiri;
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        await ClearFormAsync();
        await SearchAsync(string.Empty);
    }

    /// <summary>Called from the right-click "Data Rekam Medis" patient menu (Modules/Shared/PatientContextMenu) — searches straight to that patient instead of clearing/listing everything.</summary>
    public async System.Threading.Tasks.Task SearchForAsync(string keyword)
    {
        this.FindControl<TextBox>("KeyWordBox")!.Text = keyword;
        await SearchAsync(keyword);
    }

    private async System.Threading.Tasks.Task SearchAsync(string keyword)
    {
        var grid = this.FindControl<DataGrid>("PasienGrid")!;
        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new PasienRepository(db);
            grid.ItemsSource = await repo.SearchAsync(keyword);
        }
        catch (Exception)
        {
            grid.ItemsSource = new List<Core.Entities.Pasien>();
        }
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e)
        => await SearchAsync(this.FindControl<TextBox>("KeyWordBox")!.Text ?? string.Empty);

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Legacy behavior: picking a row from the list loads it for viewing.
        // Editing an existing patient (Ganti) is out of scope for v1 — this
        // view only creates new patients; see docs/audit/HANDOVER.md.
        if (this.FindControl<DataGrid>("PasienGrid")!.SelectedItem is not Core.Entities.Pasien p) return;

        this.FindControl<TextBox>("NoRmBox")!.Text = p.NoRekamMedis;
        this.FindControl<TextBox>("NamaBox")!.Text = p.NamaPasien;
        this.FindControl<TextBox>("AlamatBox")!.Text = p.Alamat;
        this.FindControl<TextBox>("TelpBox")!.Text = p.NoTelepon;
        this.FindControl<TextBox>("KtpBox")!.Text = p.NoKtp;
        this.FindControl<TextBox>("PekerjaanBox")!.Text = p.Pekerjaan;
        this.FindControl<TextBox>("EmailBox")!.Text = p.Email;
        this.FindControl<TextBox>("NamaIbuBox")!.Text = p.NamaIbu;
        this.FindControl<TextBox>("NamaPjBox")!.Text = p.NamaKeluarga;
    }

    private async void OnBaruClick(object? sender, RoutedEventArgs e) => await ClearFormAsync();

    private async System.Threading.Tasks.Task ClearFormAsync()
    {
        foreach (var name in new[] { "NamaBox", "TempatLahirBox", "AgamaBox", "TelpBox", "KtpBox",
                                      "PekerjaanBox", "EmailBox", "NamaIbuBox", "AlamatBox",
                                      "NamaPjBox", "PekerjaanPjBox", "AlamatPjBox" })
        {
            this.FindControl<TextBox>(name)!.Text = string.Empty;
        }
        this.FindControl<DatePicker>("TglLahirPicker")!.SelectedDate = null;
        this.FindControl<RadioButton>("JkLakiRadio")!.IsChecked = true;

        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        status.IsVisible = false;

        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new PasienRepository(db);
            this.FindControl<TextBox>("NoRmBox")!.Text = await repo.NextNoRekamMedisAsync();

            var penjabCombo = this.FindControl<ComboBox>("PenjabCombo")!;
            penjabCombo.ItemsSource = await db.PenjabSet.ToListAsync();
            penjabCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Penjab.NamaPenanggungJawab));
        }
        catch (Exception)
        {
            this.FindControl<TextBox>("NoRmBox")!.Text = "(tidak konek DB)";
        }
    }

    private async void OnSimpanClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText")!;
        var nama = this.FindControl<TextBox>("NamaBox")!.Text;
        var namaIbu = this.FindControl<TextBox>("NamaIbuBox")!.Text;
        var penjab = this.FindControl<ComboBox>("PenjabCombo")!.SelectedItem as Penjab;

        if (string.IsNullOrWhiteSpace(nama) || string.IsNullOrWhiteSpace(namaIbu) || penjab is null)
        {
            status.Text = "Nama Pasien, Nama Ibu, dan Cara Bayar wajib diisi.";
            status.IsVisible = true;
            return;
        }

        var jk = this.FindControl<RadioButton>("JkLakiRadio")!.IsChecked == true
            ? JenisKelamin.LakiLaki : JenisKelamin.Perempuan;

        var pasien = new Core.Entities.Pasien
        {
            NoRekamMedis = this.FindControl<TextBox>("NoRmBox")!.Text ?? string.Empty,
            NamaPasien = nama,
            JenisKelamin = jk,
            GolonganDarah = this.FindControl<ComboBox>("GolDarahCombo")!.SelectedItem as GolonganDarah?,
            TempatLahir = EmptyToNull(this.FindControl<TextBox>("TempatLahirBox")!.Text),
            TanggalLahir = this.FindControl<DatePicker>("TglLahirPicker")!.SelectedDate is { } d
                ? DateOnly.FromDateTime(d.Date) : null,
            NamaIbu = namaIbu,
            Alamat = EmptyToNull(this.FindControl<TextBox>("AlamatBox")!.Text),
            Agama = EmptyToNull(this.FindControl<TextBox>("AgamaBox")!.Text),
            StatusPernikahan = this.FindControl<ComboBox>("StatusNikahCombo")!.SelectedItem as StatusPernikahan?,
            NoTelepon = EmptyToNull(this.FindControl<TextBox>("TelpBox")!.Text),
            NoKtp = EmptyToNull(this.FindControl<TextBox>("KtpBox")!.Text),
            Pekerjaan = EmptyToNull(this.FindControl<TextBox>("PekerjaanBox")!.Text),
            Email = this.FindControl<TextBox>("EmailBox")!.Text ?? string.Empty,
            TanggalDaftar = DateOnly.FromDateTime(DateTime.Today),
            KodePenjab = penjab.KodePenjab,
            Keluarga = this.FindControl<ComboBox>("HubunganPjCombo")!.SelectedItem as HubunganPenanggungJawab?
                       ?? HubunganPenanggungJawab.DiriSendiri,
            NamaKeluarga = this.FindControl<TextBox>("NamaPjBox")!.Text ?? nama,
            PekerjaanPj = EmptyToNull(this.FindControl<TextBox>("PekerjaanPjBox")!.Text) ?? "-",
            AlamatPj = EmptyToNull(this.FindControl<TextBox>("AlamatPjBox")!.Text) ?? "-",
        };

        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new PasienRepository(db);
            await repo.SaveNewAsync(pasien);

            status.Foreground = Avalonia.Media.Brushes.Green;
            status.Text = $"Tersimpan — No. RM {pasien.NoRekamMedis}, {pasien.NamaPasien}.";
            status.IsVisible = true;

            await ClearFormAsync();
            await SearchAsync(string.Empty);
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brush.Parse("#DC2626");
            status.Text = $"Gagal simpan: {ex.Message}";
            status.IsVisible = true;
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void OnBackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
