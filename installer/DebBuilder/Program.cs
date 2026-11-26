using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

// Usage: DebBuilder <publishDir> <linuxDir> <output> <version> [amd64|arm64]
//   publishDir : dotnet publish -r linux-x64 / linux-arm64 output
//   linuxDir   : installer/linux (desktop file, icon)
//   output     : *.deb  -> Debian package installing to /opt/simrs-gramedika (+ /usr/bin launcher, menu entry)
//                *.tar.gz -> portable archive (folder simrs-gramedika/ with the app and a launcher script)
// Built on any OS with correct unix permissions and root ownership.
if (args.Length is < 4 or > 5)
{
    Console.Error.WriteLine("Usage: DebBuilder <publishDir> <linuxDir> <output.deb|output.tar.gz> <version> [amd64|arm64]");
    return 2;
}

var publishDir = Path.GetFullPath(args[0]);
var linuxDir = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args[2]);
var version = args[3];
var arch = args.Length == 5 ? args[4] : "amd64";
if (arch is not ("amd64" or "arm64")) { Console.Error.WriteLine("arch must be amd64 or arm64"); return 2; }

const string Package = "simrs-gramedika";
const string MainBinary = "SimrsGramedika.UI";

var exec = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
           UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
var normal = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
var mtime = DateTimeOffset.UtcNow;

bool IsExecutable(string rel) =>
    rel == MainBinary || rel == "createdump" || rel.EndsWith(".so", StringComparison.Ordinal) || rel.Contains(".so.", StringComparison.Ordinal);

var appFiles = Directory.EnumerateFiles(publishDir, "*", SearchOption.AllDirectories)
    .Select(p => (Rel: Path.GetRelativePath(publishDir, p).Replace('\\', '/'), Path: p))
    .Where(f => !f.Rel.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f.Rel, StringComparer.Ordinal)
    .ToList();
var desktop = File.ReadAllBytes(Path.Combine(linuxDir, "simrs-gramedika.desktop"));
var icon = File.ReadAllBytes(Path.Combine(linuxDir, "simrs-gramedika.png"));

/// Writes (path, bytes, executable) entries into a gzipped GNU tar, creating parent directories once.
long WriteTar(Stream target, IEnumerable<(string Path, byte[] Data, bool Exec)> entries, StringBuilder? md5 = null)
{
    long bytes = 0;
    using var gz = new GZipStream(target, CompressionLevel.SmallestSize, leaveOpen: true);
    using var tar = new TarWriter(gz, TarEntryFormat.Gnu, leaveOpen: true);
    var dirs = new HashSet<string>();
    void Dir(string path)
    {
        var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i <= parts.Length; i++)
        {
            var d = string.Join('/', parts[..i]);
            if (!dirs.Add(d)) continue;
            tar.WriteEntry(new GnuTarEntry(TarEntryType.Directory, $"./{d}/")
            {
                Mode = exec, Uid = 0, Gid = 0, UserName = "root", GroupName = "root", ModificationTime = mtime,
            });
        }
    }
    foreach (var (path, data, isExec) in entries)
    {
        Dir(Path.GetDirectoryName(path)!.Replace('\\', '/'));
        tar.WriteEntry(new GnuTarEntry(TarEntryType.RegularFile, "./" + path)
        {
            Mode = isExec ? exec : normal, Uid = 0, Gid = 0, UserName = "root", GroupName = "root",
            ModificationTime = mtime, DataStream = new MemoryStream(data),
        });
        md5?.Append(Convert.ToHexStringLower(MD5.HashData(data))).Append("  ").Append(path).Append('\n');
        bytes += data.Length;
    }
    return bytes;
}

Directory.CreateDirectory(Path.GetDirectoryName(output)!);

// ---------------------------------------------------------------- portable .tar.gz
if (output.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
{
    const string Root = "simrs-gramedika";
    var run = "#!/bin/sh\n# SIMRS Gramedika (portable)\nDIR=\"$(cd \"$(dirname \"$0\")\" && pwd)\"\nexec \"$DIR/app/SimrsGramedika.UI\" \"$@\"\n";
    var readme = $"SIMRS Gramedika {version} (linux-{(arch == "amd64" ? "x64" : "arm64")}, portable)\n\nJalankan: ./simrs-gramedika\n" +
                 "Koneksi database diatur saat pertama dibuka dan disimpan di ~/.config/SimrsGramedika.\n";
    using var fs = File.Create(output);
    WriteTar(fs,
    [
        .. appFiles.Select(f => ($"{Root}/app/{f.Rel}", File.ReadAllBytes(f.Path), IsExecutable(f.Rel))),
        ($"{Root}/simrs-gramedika", Encoding.UTF8.GetBytes(run), true),
        ($"{Root}/simrs-gramedika.png", icon, false),
        ($"{Root}/BACA-SAYA.txt", Encoding.UTF8.GetBytes(readme), false),
    ]);
    Console.WriteLine($"OK {output} ({new FileInfo(output).Length / 1024 / 1024} MB)");
    return 0;
}

// ---------------------------------------------------------------- .deb
var md5sums = new StringBuilder();
var dataTar = new MemoryStream();
var launcher = "#!/bin/sh\n# SIMRS Gramedika launcher\nexec /opt/simrs-gramedika/SimrsGramedika.UI \"$@\"\n";
var installedBytes = WriteTar(dataTar,
[
    .. appFiles.Select(f => ($"opt/simrs-gramedika/{f.Rel}", File.ReadAllBytes(f.Path), IsExecutable(f.Rel))),
    ("usr/bin/simrs-gramedika", Encoding.UTF8.GetBytes(launcher), true),
    ("usr/share/applications/simrs-gramedika.desktop", desktop, false),
    ("usr/share/icons/hicolor/256x256/apps/simrs-gramedika.png", icon, false),
    ("usr/share/pixmaps/simrs-gramedika.png", icon, false),
], md5sums);

var control = $"""
Package: {Package}
Version: {version.Replace('-', '~')}
Section: misc
Priority: optional
Architecture: {arch}
Maintainer: RSU Gramedika <it@localhost>
Installed-Size: {(installedBytes + 1023) / 1024}
Depends: libc6, libgcc-s1, libstdc++6, zlib1g, libfontconfig1, libfreetype6, libx11-6, libice6, libsm6, libxext6, libxrandr2, libxi6, libxcursor1, libicu76 | libicu74 | libicu72 | libicu71 | libicu70 | libicu67 | libicu66 | libicu63, libssl3t64 | libssl3 | libssl1.1
Recommends: pulseaudio-utils | alsa-utils
Description: SIMRS Gramedika - Sistem Informasi Manajemen Rumah Sakit
 Aplikasi desktop SIMRS Gramedika (penulisan ulang SIMRS Khanza dengan
 Avalonia/.NET). Terhubung ke database MySQL/MariaDB SIMRS Khanza yang
 sudah ada, memakai akun dan hak akses yang sama.
 .
 Koneksi database diatur saat pertama kali aplikasi dibuka dan disimpan
 per user di ~/.config/SimrsGramedika.

""";

var postinst = """
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database -q /usr/share/applications || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true; fi
exit 0

""";

var controlTar = new MemoryStream();
using (var gz = new GZipStream(controlTar, CompressionLevel.SmallestSize, leaveOpen: true))
using (var tar = new TarWriter(gz, TarEntryFormat.Gnu, leaveOpen: true))
{
    tar.WriteEntry(new GnuTarEntry(TarEntryType.Directory, "./") { Mode = exec, UserName = "root", GroupName = "root", ModificationTime = mtime });
    void Add(string name, string text, bool executable) => tar.WriteEntry(new GnuTarEntry(TarEntryType.RegularFile, "./" + name)
    {
        Mode = executable ? exec : normal, UserName = "root", GroupName = "root", ModificationTime = mtime,
        DataStream = new MemoryStream(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n"))),
    });
    Add("control", control, false);
    Add("md5sums", md5sums.ToString(), false);
    Add("postinst", postinst, true);
    Add("postrm", postinst, true);
}

using (var deb = File.Create(output))
{
    deb.Write("!<arch>\n"u8);
    void Member(string name, byte[] data)
    {
        var header = $"{name,-16}{mtime.ToUnixTimeSeconds(),-12}{0,-6}{0,-6}{"100644",-8}{data.Length,-10}`\n";
        deb.Write(Encoding.ASCII.GetBytes(header));
        deb.Write(data);
        if (data.Length % 2 == 1) deb.WriteByte((byte)'\n');
    }
    Member("debian-binary", "2.0\n"u8.ToArray());
    Member("control.tar.gz", controlTar.ToArray());
    Member("data.tar.gz", dataTar.ToArray());
}

Console.WriteLine($"OK {output} ({new FileInfo(output).Length / 1024 / 1024} MB, installed {installedBytes / 1024 / 1024} MB)");
return 0;
