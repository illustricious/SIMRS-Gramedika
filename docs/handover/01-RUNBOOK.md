# Runbook: jalankan, build, tes, release

Semua perintah dari folder repo `SimrsKhanza.Avalonia`. Prasyarat: .NET SDK 10, Windows 10/11 (atau Linux Debian/Ubuntu).

## Koneksi database

Aplikasi membaca koneksi dari pengaturan tersimpan per pengguna: `%APPDATA%\SimrsKhanza\database.settings.json` (Linux/macOS: `~/.config/SimrsKhanza`). Diatur lewat layar **Pengaturan** (khusus admin) atau jendela koneksi saat login pertama. Host/IP bebas, termasuk server database RS.

Untuk pengembangan, `DatabaseSettings.Load()` juga membaca variabel lingkungan `SIMRS_DB_HOST`, `SIMRS_DB_PORT`, `SIMRS_DB_NAME`, `SIMRS_DB_USER`, `SIMRS_DB_PASSWORD`. Pengaturan tersimpan didahulukan. Akun dev dan data dummy lama dicatat di [04-HISTORY.md](04-HISTORY.md) (bagian "MULAI DARI SINI"). Jangan arahkan pengembangan ke database produksi.

Login memakai akun Khanza (`user`/`petugas`). Hak akses menentukan tile yang tampil.

## Jalankan, build, tes

```powershell
dotnet run --project src\SimrsKhanza.UI\SimrsKhanza.UI.csproj
dotnet build
dotnet test
```

- Matikan instance aplikasi yang masih berjalan sebelum build ulang, kalau tidak DLL terkunci: `Get-Process SimrsKhanza.UI | Stop-Process -Force`.
- Tes integrasi ke database nyata melewati diri sendiri bila DB tidak terjangkau (return, bukan gagal). Satu tes billing ranap selalu dilewati tanpa DB.
- Tes layar (`MenuCScreensTests`) membangun semua layar dalam Avalonia headless. Variabel opsional: `SIMRS_DB_SMOKE=1` memuat data juga, `SIMRS_SCREENSHOT_DIR=<folder>` menyimpan PNG per layar. Layar menu baru **wajib** ditambahkan ke tes ini.

## Exe tunggal lokal (portabel)

```powershell
$v = "0.2.0-alpha.$(Get-Date -Format yyyyMMddHHmm)"
dotnet publish src\SimrsKhanza.UI -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -p:DebugType=none -p:DebugSymbols=false -p:Version=$v -o dist\single-win-x64
```

Hasilnya `SimrsKhanza.UI.exe` (sekitar 59 MB, .NET sudah termasuk) dan satu folder `LatoFont`. Exe berjalan sendiri tanpa folder itu. Ganti nama menjadi `SIMRS-Gramedika-<versi>-win-x64-single.exe`. Folder `dist/` di-gitignore. Exe hanya diunggah sebagai aset Release GitHub bila pemilik proyek memintanya (`gh release create ... --prerelease`), tidak pernah di-commit ke repo.

## Installer (Windows/Linux/macOS)

Lihat `installer/README.md`. Windows lokal: `powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 -Only win -Arch x64` (butuh Inno Setup 6, `winget install JRSoftware.InnoSetup`). Keluaran ke `dist\`.

## Versi dan release

- Versi dasar di `src/SimrsKhanza.UI/SimrsKhanza.UI.csproj` (`<Version>0.2.0-alpha</Version>`). Build alpha menambah stempel waktu: `0.2.0-alpha.yyyyMMddHHmm`.
- Seri alpha 0.1.5 dan 0.2.0 dibuild lokal. `.github/workflows/release.yml` mengecualikan tag `v0.1.5-alpha.*` dan `v0.2.0-alpha.*`. Tag `v*` lain memicu CI semua platform dan Release GitHub. Jalankan manual lewat Actions untuk artefak saja.
- Catatan rilis lama dan checksum disimpan lokal di `publish/release-tools/` (tidak di git).

## Tracker fitur

Lihat `docs/tracker/README.md`. Ringkas: `node tools/tracker.mjs` (Node 22+) membaca `LegacyMenuCatalog.cs`, snapshot source Java (`java.jsonl`), kode produksi, dan `status.json`, lalu menulis ulang `fitur.tsv`, `RINGKASAN.md`, dan `POPUP_REGISTRASI.md`. Commit ketiganya bersama `status.json`; CI (`.github/workflows/tracker.yml`) gagal bila laporan kedaluwarsa.

- Tile berstatus otomatis **layar tersedia** bila kuncinya (`btnXxx`, tanpa membedakan huruf besar/kecil) dirutekan di kode produksi (selain katalog, peta hak akses, `DashboardIcons.cs`, dan tes). Tile Menu L otomatis **ditunda**.
- **selesai/sebagian/kurang** hanya lewat `status.json` dengan bukti (path implementasi atau tes). Layar baca-saja untuk alur yang di Java menyimpan data = sebagian.
- Snapshot Java baru: `node tools/tracker.mjs --java ..\simrs-khanza\src`.

## Jebakan lingkungan

1. **Perintah shell terlalu panjang** (`ENAMETOOLONG`) pada Windows: jangan kirim file besar lewat heredoc. Tulis lewat editor.
2. **Build gagal karena DLL terkunci** bila aplikasi atau exe hasil publish masih berjalan. Hentikan dulu.
3. **Kolom AUTO_INCREMENT diisi 0** diganti MySQL menjadi nomor urut kecuali `sql_mode` memuat `NO_AUTO_VALUE_ON_ZERO`. Penting untuk tabel lookup baru.
4. **Enum C# `.ToString()` tidak sama dengan nilai enum MySQL legacy** (mis. `"Sudah Bayar"`). Selalu petakan eksplisit (`HasConversion`).
5. **Ukuran:** folder `bin`/`obj` bisa >1,5 GB. Aman dihapus, akan dibuat ulang saat build.
6. **MariaDB lokal** bukan service Windows. Cek prosesnya sebelum bekerja bila memakai DB dev lokal.
7. Tabel yang dibutuhkan fitur baru tetapi tidak ada di DB dev: ambil `CREATE TABLE`-nya dari `docs/audit/schema_sik.sql` (struktur saja, tanpa data).
