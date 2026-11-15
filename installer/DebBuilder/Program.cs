using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

// Usage: DebBuilder <publishDir> <linuxDir> <outputDeb> <version>
//   publishDir : dotnet publish -r linux-x64 output
//   linuxDir   : installer/linux (desktop file, icon, launcher)
// Produces a .deb that installs to /opt/simrs-gramedika with a launcher in /usr/bin
// and a menu entry, built with correct unix permissions and root ownership.
if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: DebBuilder <publishDir> <linuxDir> <outputDeb> <version>");
    return 2;
}

var publishDir = Path.GetFullPath(args[0]);
var linuxDir = Path.GetFullPath(args[1]);
var outputDeb = Path.GetFullPath(args[2]);
var version = args[3];

const string Package = "simrs-gramedika";
const string InstallDir = "opt/simrs-gramedika";
const string MainBinary = "SimrsKhanza.UI";

var exec = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
           UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
var normal = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

var md5 = new StringBuilder();
long installedBytes = 0;
var mtime = DateTimeOffset.UtcNow;

// ---- data.tar.gz ----
var dataTar = new MemoryStream();
using (var gz = new GZipStream(dataTar, CompressionLevel.SmallestSize, leaveOpen: true))
using (var tar = new TarWriter(gz, TarEntryFormat.Gnu, leaveOpen: true))
{
    var dirsWritten = new HashSet<string>();

    void Dir(string path)
    {
        var parts = path.Trim('/').Split('/');
        for (var i = 1; i <= parts.Length; i++)
        {
            var d = string.Join('/', parts[..i]);
            if (!dirsWritten.Add(d)) continue;
            tar.WriteEntry(new GnuTarEntry(TarEntryType.Directory, $"./{d}/")
            {
                Mode = exec, Uid = 0, Gid = 0, UserName = "root", GroupName = "root", ModificationTime = mtime,
            });
        }
    }

    void File(string target, byte[] content, bool executable)
    {
        Dir(Path.GetDirectoryName(target)!.Replace('\\', '/'));
        tar.WriteEntry(new GnuTarEntry(TarEntryType.RegularFile, "./" + target)
        {
            Mode = executable ? exec : normal, Uid = 0, Gid = 0, UserName = "root", GroupName = "root",
            ModificationTime = mtime, DataStream = new MemoryStream(content),
        });
        md5.Append(Convert.ToHexStringLower(MD5.HashData(content))).Append("  ").Append(target).Append('\n');
        installedBytes += content.Length;
    }

    foreach (var path in Directory.EnumerateFiles(publishDir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        var rel = Path.GetRelativePath(publishDir, path).Replace('\\', '/');
        if (rel.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
        var executable = rel == MainBinary || rel.EndsWith(".so", StringComparison.Ordinal) || rel.Contains(".so.", StringComparison.Ordinal) || rel == "createdump";
        File($"{InstallDir}/{rel}", System.IO.File.ReadAllBytes(path), executable);
    }

    var launcher = "#!/bin/sh\n# SIMRS Gramedika launcher\nexec /opt/simrs-gramedika/SimrsKhanza.UI \"$@\"\n";
    File("usr/bin/simrs-gramedika", Encoding.UTF8.GetBytes(launcher), executable: true);
    File("usr/share/applications/simrs-gramedika.desktop",
        System.IO.File.ReadAllBytes(Path.Combine(linuxDir, "simrs-gramedika.desktop")), executable: false);
    var icon = System.IO.File.ReadAllBytes(Path.Combine(linuxDir, "simrs-gramedika.png"));
    File("usr/share/icons/hicolor/256x256/apps/simrs-gramedika.png", icon, executable: false);
    File("usr/share/pixmaps/simrs-gramedika.png", icon, executable: false);
}

// ---- control.tar.gz ----
var control = $"""
Package: {Package}
Version: {version}
Section: misc
Priority: optional
Architecture: amd64
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
 per user di ~/.config/SimrsKhanza.

""";

var postinst = """
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database -q /usr/share/applications || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true; fi
exit 0

""";
var postrm = postinst;

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
    Add("md5sums", md5.ToString(), false);
    Add("postinst", postinst, true);
    Add("postrm", postrm, true);
}

// ---- ar archive ----
Directory.CreateDirectory(Path.GetDirectoryName(outputDeb)!);
using (var deb = System.IO.File.Create(outputDeb))
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

Console.WriteLine($"OK {outputDeb} ({new FileInfo(outputDeb).Length / 1024 / 1024} MB, installed {installedBytes / 1024 / 1024} MB)");
return 0;
