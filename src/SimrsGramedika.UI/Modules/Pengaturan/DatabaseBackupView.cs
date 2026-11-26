using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Pengaturan;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Pengaturan;

public sealed class DatabaseBackupView : PageView
{
    private readonly DatabaseSettings _settings = DatabaseSettings.Load();
    private readonly AuthenticatedUser? _user = AppSession.CurrentUser;
    private readonly Button _save;
    private readonly Button _cancel;
    private CancellationTokenSource? _backup;

    public DatabaseBackupView()
    {
        _save = Ui.Btn("Simpan Backup SQL", async (_, _) => await SaveAsync());
        _save.IsEnabled = HasActiveSession;
        _cancel = Ui.Btn("Batalkan Backup", (_, _) => _backup?.Cancel());
        _cancel.IsEnabled = false;
        BackRequested += (_, _) => _backup?.Cancel();
        DetachedFromVisualTree += (_, _) => _backup?.Cancel();

        Build("Backup Database", new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                Ui.L($"Database aktif: {_settings.Summary}"),
                Ui.Hint("File SQL berisi data SIMRS. Simpan di folder yang hanya dapat diakses petugas berwenang."),
                Ui.Bar(_save, _cancel),
            },
        }, "Ekspor struktur dan isi database aktif ke file SQL.");
    }

    private bool HasActiveSession => _user is not null && ReferenceEquals(_user, AppSession.CurrentUser);

    public override Task ReloadAsync() => Task.CompletedTask;

    private async Task SaveAsync()
    {
        if (_backup is not null || TopLevel.GetTopLevel(this) is not { } top) return;

        using var cancellation = new CancellationTokenSource();
        _backup = cancellation;
        _save.IsEnabled = false;
        try
        {
            if (!HasActiveSession)
                throw new UnauthorizedAccessException("Login ke SIMRS untuk menjalankan backup database.");

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Simpan backup database",
                SuggestedFileName = $"simrs-backup-{DateTime.Now:yyyyMMdd-HHmmss}.sql",
                DefaultExtension = "sql",
                FileTypeChoices = [new FilePickerFileType("SQL") { Patterns = ["*.sql"] }],
            });
            if (file is null) return;
            var path = file.TryGetLocalPath()
                ?? throw new IOException("Pilih lokasi penyimpanan di sistem file lokal.");

            cancellation.Token.ThrowIfCancellationRequested();
            if (!HasActiveSession)
                throw new UnauthorizedAccessException("Sesi SIMRS sudah berakhir. Silakan login kembali.");

            _cancel.IsEnabled = true;
            Ui.Info(StatusText, "Membuat backup database...");
            await DatabaseBackup.SaveAsync(_user, _settings, path, cancellation.Token);
            Ui.Ok(StatusText, $"Backup tersimpan di {path}");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Ui.Info(StatusText, "Backup dibatalkan.");
        }
        catch (Exception ex)
        {
            Ui.Err(StatusText, ex.Message);
        }
        finally
        {
            _backup = null;
            _cancel.IsEnabled = false;
            _save.IsEnabled = HasActiveSession;
        }
    }
}
