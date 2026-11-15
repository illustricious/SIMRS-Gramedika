# Installer SIMRS Gramedika

Membuat dua paket self-contained (komputer tujuan tidak perlu memasang .NET):

| File | Untuk |
|---|---|
| `dist/SIMRS-Gramedika-Setup-<versi>-win-x64.exe` | Windows 10/11 64-bit (Inno Setup) |
| `dist/simrs-gramedika_<versi>_amd64.deb` | Debian 11+/Ubuntu 20.04+ 64-bit |

## Build

Butuh .NET SDK 10 dan Inno Setup 6 (`winget install JRSoftware.InnoSetup`).

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1            # dua-duanya
powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 -Only win  # hanya Windows
powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 -Version 1.0.1
```

Versi default diambil dari `<Version>` di `src/SimrsKhanza.UI/SimrsKhanza.UI.csproj`.
Paket `.deb` dibuat oleh `installer/DebBuilder` (C#), jadi tidak perlu WSL/dpkg-deb.

## Instal

- **Windows**: jalankan Setup, ikuti wizard. Menu Start + ikon desktop (opsional) bernama "SIMRS Gramedika".
- **Linux**: `sudo apt install ./simrs-gramedika_1.0.0_amd64.deb` (apt ikut memasang dependensi
  seperti libicu, libssl, fontconfig). Aplikasi ada di menu sebagai "SIMRS Gramedika" atau
  jalankan `simrs-gramedika`. Terpasang di `/opt/simrs-gramedika`.

## Koneksi database

Saat pertama dibuka, muncul layar **Koneksi Database** (host, port, user, password, database —
bisa "Ambil daftar" dari server). Bisa menyimpan beberapa profil server; profil dipilih di layar
login, dan tombol ⚙ di layar login membuka layar koneksi lagi.

Disimpan per user di `%APPDATA%\SimrsKhanza\database.settings.json` (Windows) atau
`~/.config/SimrsKhanza/database.settings.json` (Linux, izin 600). Uninstall tidak menghapus file
ini. Folder bisa dipindah dengan environment variable `SIMRS_CONFIG_DIR`.
