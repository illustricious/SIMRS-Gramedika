using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Globalization;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data;
using SimrsKhanza.UI.Controls;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Registrasi;

public partial class RegistrasiView
{
    private void OnRujukanMasukClick(object? sender, RoutedEventArgs e)
    {
        BukaRujukanMasuk(_newMode ? null : _selected);
    }

    private void UpdateRujukanMasukButton() =>
        this.FindControl<Button>("RujukanMasukButton")!.IsEnabled = (_newMode ? _pasienBaru is not null : _selected is not null)
            && RujukanMasukService.BisaMengelola(AppSession.CurrentUser);

    private async void BukaRujukanMasuk(RegistrasiListItem? row)
    {
        var user = AppSession.CurrentUser;
        if (user is null || !RujukanMasukService.BisaMengelola(user) || TopLevel.GetTopLevel(this) is not Window owner) return;
        var pasienBaru = row is null && _newMode ? _pasienBaru : null;
        if (row is null && pasienBaru is null) return;
        try
        {
            var service = new RujukanMasukService(DatabaseSettings.Load(), user);
            var sebelumnya = row is null ? _drafRujukan : await service.BacaAsync(row.NoRawat);
            if (!ReferenceEquals(user, AppSession.CurrentUser) || !IsLoaded) return;
            var form = new StackPanel { Margin = new Thickness(16), Spacing = 6 };
            form.Children.Add(new TextBlock
            {
                Text = row is null ? $"{pasienBaru!.NoRekamMedis} · {pasienBaru.Nama}" : $"{row.NoRawat} · {row.NamaPasien}",
                TextWrapping = TextWrapping.Wrap
            });
            TextBox Field(string label, string? value, int limit)
            {
                var box = new TextBox { Text = value, MaxLength = limit };
                Avalonia.Automation.AutomationProperties.SetName(box, label);
                form.Children.Add(new TextBlock { Text = label });
                form.Children.Add(box);
                return box;
            }
            var perujuk = Field("Perujuk", sebelumnya?.Perujuk, 60);
            var alamat = Field("Alamat perujuk", sebelumnya?.Alamat, 70);
            var noRujuk = Field("Nomor rujukan", sebelumnya?.NoRujuk, 40);
            var jasa = Field("Jasa perujuk", (sebelumnya?.Jasa ?? 0).ToString(CultureInfo.CurrentCulture), 24);
            var dokter = Field("Dokter perujuk", sebelumnya?.Dokter, 50);
            var diagnosa = Field("Kode diagnosis masuk (ICD-10)", sebelumnya?.Diagnosa, 15);
            var kategori = new ComboBox { ItemsSource = new[] { "-", "Bedah", "Non-Bedah", "Kebidanan", "Anak" } };
            kategori.SelectedItem = string.IsNullOrEmpty(sebelumnya?.Kategori) ? "-" : sebelumnya.Kategori;
            form.Children.Add(new TextBlock { Text = "Kategori" });
            form.Children.Add(kategori);
            var keterangan = Field("Keterangan", sebelumnya?.Keterangan, 200);
            var balasan = Field("Nomor balasan", sebelumnya?.NoBalasan, 20);
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var simpan = new Button { Content = row is null ? "Gunakan Rujukan" : "Simpan" };
            var batal = new Button { Content = "Batal" };
            form.Children.Add(status);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { simpan, batal } };
            form.Children.Add(actions);
            var dialog = new Window
            {
                Title = "Rujukan Masuk",
                Width = 500,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new ScrollViewer { Content = form }
            };
            var cariPerujuk = new Button { Content = "Cari Perujuk" };
            form.Children.Insert(1, cariPerujuk);
            cariPerujuk.Click += async (_, _) =>
            {
                cariPerujuk.IsEnabled = false;
                try
                {
                    var pilihan = await SearchPickerDialog.ShowAsync(dialog, "Cari Perujuk", service.CariPerujukAsync,
                        item => $"{item.Nama} · {item.Alamat}");
                    if (pilihan is null) return;
                    perujuk.Text = pilihan.Nama;
                    alamat.Text = pilihan.Alamat;
                }
                catch (Exception ex)
                {
                    status.Text = $"Pencarian perujuk gagal. {ex.Message}";
                }
                finally { cariPerujuk.IsEnabled = true; }
            };
            bool saving = false;
            dialog.Closing += (_, args) => args.Cancel = saving;
            batal.Click += (_, _) => dialog.Close(false);
            if (row is null && sebelumnya is not null)
            {
                var hapusDraf = new Button { Content = "Lepas Rujukan" };
                actions.Children.Add(hapusDraf);
                hapusDraf.Click += (_, _) =>
                {
                    if (_newMode && ReferenceEquals(_pasienBaru, pasienBaru))
                    {
                        _drafRujukan = null;
                        SetInputText("InputAsalRujukanBox", null);
                    }
                    dialog.Close(false);
                };
            }
            simpan.Click += async (_, _) =>
            {
                if (saving || !ReferenceEquals(user, AppSession.CurrentUser)) return;
                if (!double.TryParse(jasa.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var nominal))
                {
                    status.Text = "Jasa perujuk harus berupa angka.";
                    return;
                }
                static string Value(TextBox box) => box.Text?.Trim() ?? "";
                var data = new RujukanMasukData(Value(perujuk), Value(alamat), Value(noRujuk), nominal, Value(dokter),
                    Value(diagnosa), kategori.SelectedItem as string ?? "-", Value(keterangan), Value(balasan));
                saving = true;
                form.IsEnabled = false;
                try
                {
                    if (row is not null)
                        await service.SimpanAsync(row.NoRawat, data, sebelumnya);
                    else
                    {
                        if (!_newMode || !ReferenceEquals(_pasienBaru, pasienBaru))
                            throw new InvalidOperationException("Pasien registrasi sudah berubah.");
                        RujukanMasukService.Validasi(data);
                        _drafRujukan = data;
                        SetInputText("InputAsalRujukanBox", data.Perujuk);
                    }
                    saving = false;
                    dialog.Close(true);
                }
                catch (Exception ex)
                {
                    saving = false;
                    form.IsEnabled = true;
                    status.Text = $"Gagal menyimpan rujukan. {ex.Message}";
                }
            };
            if (await dialog.ShowDialog<bool>(owner) && row is not null && ReferenceEquals(_selected, row))
                await LoadVisitDetailsAsync(row);
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(this, "Rujukan Masuk", $"Data rujukan tidak dapat dimuat. {ex.Message}");
        }
    }

    private async void BukaRujukanInternal(RegistrasiListItem row)
    {
        var user = AppSession.CurrentUser;
        if (user is null || !RujukanInternalService.BisaMengelola(user) || TopLevel.GetTopLevel(this) is not Window owner) return;

        try
        {
            var settings = DatabaseSettings.Load();
            using var db = SimrsKhanzaDbContextFactory.Create(settings);
            var repo = new RegistrasiRepository(db);
            var dokter = new ComboBox
            {
                ItemsSource = await repo.GetDokterAsync(),
                DisplayMemberBinding = new Binding(nameof(Dokter.NamaDokter)),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var poli = new ComboBox
            {
                ItemsSource = await repo.GetPoliklinikAsync(),
                DisplayMemberBinding = new Binding(nameof(Poliklinik.NamaPoli)),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            if (!ReferenceEquals(user, AppSession.CurrentUser) || !IsLoaded) return;
            var simpan = new Button { Content = "Simpan", IsEnabled = false };
            var batal = new Button { Content = "Batal" };
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var dialog = new Window
            {
                Title = "Rujukan Internal Poli",
                Width = 460,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = $"{row.NoRawat} · {row.NamaPasien}", TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = "Poli tujuan" }, poli,
                        new TextBlock { Text = "Dokter tujuan" }, dokter,
                        status,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { simpan, batal } }
                    }
                }
            };
            bool saving = false;
            void UpdateSave() => simpan.IsEnabled = !saving && dokter.SelectedItem is Dokter && poli.SelectedItem is Poliklinik;
            dokter.SelectionChanged += (_, _) => UpdateSave();
            poli.SelectionChanged += (_, _) => UpdateSave();
            batal.Click += (_, _) => dialog.Close(false);
            dialog.Closing += (_, e) => e.Cancel = saving;
            simpan.Click += async (_, _) =>
            {
                if (saving || dokter.SelectedItem is not Dokter tujuan || poli.SelectedItem is not Poliklinik unit) return;
                if (!ReferenceEquals(user, AppSession.CurrentUser)) return;
                saving = true;
                UpdateSave();
                dokter.IsEnabled = poli.IsEnabled = batal.IsEnabled = false;
                try
                {
                    await new RujukanInternalService(settings, user).TambahAsync(row.NoRawat, tujuan.KodeDokter, unit.KodePoli);
                    saving = false;
                    dialog.Close(true);
                }
                catch (Exception ex)
                {
                    saving = false;
                    dokter.IsEnabled = poli.IsEnabled = batal.IsEnabled = true;
                    status.Text = $"Gagal menyimpan rujukan. {ex.Message}";
                    UpdateSave();
                }
            };
            if (await dialog.ShowDialog<bool>(owner))
                await Dialogs.InfoAsync(this, "Rujukan Internal Poli", $"Rujukan untuk {row.NoRawat} tersimpan.");
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(this, "Rujukan Internal Poli", $"Pilihan tujuan tidak dapat dimuat. {ex.Message}");
        }
    }

    private bool _menghapusRujukan;

    private void OnRujukanContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not DataGrid grid || !RujukanInternalService.BisaMengelola(AppSession.CurrentUser)) return;
        var pointer = e.TryGetPosition(grid, out _);
        var visualRow = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
        if ((pointer ? visualRow?.DataContext : grid.SelectedItem) is not RujukanInternalItem row) return;

        grid.SelectedItem = row;
        var source = grid.ItemsSource;
        var hapus = new MenuItem { Header = "Hapus Rujukan Poli Internal", IsEnabled = !_menghapusRujukan };
        hapus.Click += async (_, _) =>
        {
            if (ReferenceEquals(source, grid.ItemsSource) && ReferenceEquals(row, grid.SelectedItem))
                await HapusRujukanTerpilihAsync();
        };
        var menu = new ContextMenu { ItemsSource = new[] { hapus }, Placement = pointer ? PlacementMode.Pointer : PlacementMode.Bottom };
        e.Handled = true;
        menu.Open(pointer ? grid : visualRow ?? (Control)grid);
    }

    private void OnRujukanSelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateHapusRujukan();

    private void UpdateHapusRujukan()
    {
        if (this.FindControl<Button>("HapusRujukanButton") is { } hapus)
            hapus.IsEnabled = !_menghapusRujukan && RujukanInternalService.BisaMengelola(AppSession.CurrentUser)
                && this.FindControl<DataGrid>("RujukanInternalGrid")?.SelectedItem is RujukanInternalItem;
    }

    private async void OnHapusRujukanClick(object? sender, RoutedEventArgs e) => await HapusRujukanTerpilihAsync();

    private async Task HapusRujukanTerpilihAsync()
    {
        var user = AppSession.CurrentUser;
        var grid = this.FindControl<DataGrid>("RujukanInternalGrid")!;
        if (_menghapusRujukan || user is null || !RujukanInternalService.BisaMengelola(user) || grid.SelectedItem is not RujukanInternalItem row)
            return;

        _menghapusRujukan = true;
        UpdateHapusRujukan();
        try
        {
            if (!await Dialogs.ConfirmAsync(this, "Hapus Rujukan Internal", $"Hapus rujukan {row.NoRawat} ke {row.Dokter} di {row.Poli}?")
                || !ReferenceEquals(user, AppSession.CurrentUser)) return;
            var deleted = await new RujukanInternalService(DatabaseSettings.Load(), user).HapusAsync(row);
            await LoadAsync();
            if (!deleted)
                await Dialogs.InfoAsync(this, "Hapus Rujukan Internal", "Rujukan sudah berubah atau dihapus. Daftar telah dimuat ulang.");
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(this, "Hapus Rujukan Internal", $"Gagal menghapus rujukan. {ex.Message}");
        }
        finally
        {
            _menghapusRujukan = false;
            UpdateHapusRujukan();
        }
    }
}
