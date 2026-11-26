using System.Diagnostics;
using System.Globalization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;

namespace SimrsGramedika.Data.Pengaturan;

public static class DatabaseBackup
{
    public static async Task SaveAsync(AuthenticatedUser? user, DatabaseSettings settings, string destination,
        CancellationToken cancellationToken = default)
    {
        if (user is null)
            throw new UnauthorizedAccessException("Login ke SIMRS untuk menjalankan backup database.");

        var target = Path.GetFullPath(destination);
        if (File.Exists(target))
            throw new IOException("File sudah ada. Pilih nama file backup yang baru.");

        await using var connection = new MySqlConnection(settings.ToConnectionString());
        await connection.OpenAsync(cancellationToken);
        var mariaDb = connection.ServerVersion.Contains("MariaDB", StringComparison.OrdinalIgnoreCase);
        await connection.CloseAsync();

        var client = mariaDb ? "mariadb-dump" : "mysqldump";
        var executable = FindClient(client);
        var optionsPath = Path.Combine(Path.GetTempPath(), $"simrs-dump-{Guid.NewGuid():N}.cnf");
        var partialPath = Path.Combine(Path.GetDirectoryName(target)!, $".simrs-dump-{Guid.NewGuid():N}.part");
        try
        {
            await using (var options = CreatePrivateFile(optionsPath))
            {
                var config = $"[client]\nuser={QuoteOption(settings.User)}\npassword={QuoteOption(settings.Password)}\n";
                await options.WriteAsync(Encoding.UTF8.GetBytes(config), cancellationToken);
            }

            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.Environment.Remove("MYSQL_PWD");
            foreach (var argument in new[]
            {
                $"--defaults-file={optionsPath}", "--protocol=tcp", $"--host={settings.Host}",
                $"--port={settings.Port.ToString(CultureInfo.InvariantCulture)}",
                "--default-character-set=utf8mb4", "--single-transaction", "--quick",
                "--skip-lock-tables", "--hex-blob", "--routines", "--events", "--triggers",
            })
                start.ArgumentList.Add(argument);
            if (!mariaDb)
            {
                start.Environment["MYSQL_TEST_LOGIN_FILE"] = optionsPath + ".unused";
                start.ArgumentList.Add("--no-tablespaces");
                start.ArgumentList.Add("--set-gtid-purged=OFF");
            }
            start.ArgumentList.Add("--databases");
            start.ArgumentList.Add("--");
            start.ArgumentList.Add(settings.Database);

            await using (var output = CreatePrivateFile(partialPath))
            {
                using var process = Process.Start(start)
                    ?? throw new InvalidOperationException($"Utilitas {client} tidak dapat dijalankan.");
                var readErrors = ReadDiagnosticAsync(process.StandardError);
                try
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
                    await process.WaitForExitAsync(cancellationToken);
                    var diagnostic = await readErrors;
                    if (process.ExitCode != 0)
                        throw new IOException($"Backup gagal ({client}, kode {process.ExitCode}). {diagnostic}");
                    if (output.Length == 0)
                        throw new IOException("Utilitas dump tidak menghasilkan data SQL.");
                    await output.FlushAsync(cancellationToken);
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        try { process.Kill(); }
                        catch (InvalidOperationException) when (process.HasExited) { }
                        await process.WaitForExitAsync();
                    }
                    await readErrors;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialPath, target);
        }
        finally
        {
            try { File.Delete(optionsPath); }
            finally { File.Delete(partialPath); }
        }
    }

    private static async Task<string> ReadDiagnosticAsync(StreamReader errors)
    {
        const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;
        string? diagnostic = null;
        while (await errors.ReadLineAsync() is { } line)
        {
            if (diagnostic is not null) continue;
            if (line.Contains("SSL", ignoreCase) || line.Contains("TLS", ignoreCase) || line.Contains("certificate", ignoreCase))
            {
                diagnostic = line.Contains("server does not support", ignoreCase)
                    ? "Server database tidak menawarkan TLS. Aktifkan TLS di server atau pilih profil koneksi yang mendukung TLS."
                    : "Periksa konfigurasi TLS dan sertifikat server database.";
            }
            else if (line.Contains("Access denied", ignoreCase) || line.Contains("command denied", ignoreCase)
                || line.Contains("privilege", ignoreCase))
                diagnostic = "Periksa kredensial dan izin ekspor pada akun koneksi MySQL/MariaDB di profil aktif.";
            else if (line.Contains("Unknown database", ignoreCase))
                diagnostic = "Periksa nama database pada profil koneksi aktif.";
            else if (line.Contains("Can't connect", ignoreCase) || line.Contains("Lost connection", ignoreCase))
                diagnostic = "Periksa koneksi jaringan, host, dan port database pada profil aktif.";
            else if (line.Contains("unknown option", ignoreCase) || line.Contains("unknown variable", ignoreCase))
                diagnostic = "Versi utilitas dump tidak mendukung salah satu opsi backup.";
        }
        return diagnostic ?? "Periksa kompatibilitas versi utilitas dump dan izin akun koneksi database.";
    }

    private static string FindClient(string client)
    {
        var fileName = OperatingSystem.IsWindows() ? client + ".exe" : client;
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var directory = entry.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(directory)) continue;
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException($"Utilitas {client} tidak ditemukan. Pasang klien database yang sesuai, tambahkan folder bin ke PATH, lalu buka ulang aplikasi.");
    }

    private static FileStream CreatePrivateFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
            return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl,
                FileShare.None, 4096, FileOptions.Asynchronous, security);
        }

        return new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Options = FileOptions.Asynchronous,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
    }

    private static string QuoteOption(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
}
