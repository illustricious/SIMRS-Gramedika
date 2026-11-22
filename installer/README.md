# Installers

All packages are self-contained: target machines do not need .NET installed.

| Platform | Architectures | Formats | Built by |
|----------|---------------|---------|----------|
| Windows 10/11, Server 2016+ | x64, ARM64 | `.exe` (Inno Setup), `.msi` (WiX v5) | `build-installers.ps1` or CI |
| Debian / Ubuntu / Mint | x64, ARM64 | `.deb` | `build-installers.ps1` or CI |
| Any Linux | x64, ARM64 | `.tar.gz` (portable) | `build-installers.ps1` or CI |
| RHEL / Rocky / Alma / Fedora / openSUSE | x64, ARM64 | `.rpm` (nfpm) | CI |
| Any Linux | x64, ARM64 | `.AppImage` | CI |
| macOS 12+ | Apple Silicon, Intel | `.dmg` (not notarized) | CI |

Windows 7/8.1 are not supported (.NET 10 requires Windows 10 or later).

## Local build (Windows)

Requirements: .NET SDK 10 and Inno Setup 6 (`winget install JRSoftware.InnoSetup`). WiX is restored from NuGet.

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1                 # everything, x64 + arm64
powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 -Only win -Arch x64
powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 -Only win -Arch x64 -WindowsSetupOnly -FileVersion 0.2.0
```

Output goes to `dist\` (git-ignored).

## CI build (all platforms)

`.github/workflows/release.yml` builds every format on GitHub-hosted Windows, Linux and macOS runners:

- **Actions → Build installers → Run workflow**: packages are attached to the run as artifacts.
- **Push a tag** such as `v1.1.0`: the packages are also published as a GitHub Release.

## Installing

- **Windows**: run the `.exe`, or deploy the `.msi` silently: `msiexec /i SIMRS-Gramedika-<ver>-win-x64.msi /qn`.
- **Debian/Ubuntu**: `sudo apt install ./simrs-gramedika_<ver>_amd64.deb`
- **Fedora/RHEL**: `sudo dnf install ./simrs-gramedika-<ver>-1.x86_64.rpm`
- **AppImage**: `chmod +x SIMRS-Gramedika-*.AppImage && ./SIMRS-Gramedika-*.AppImage`
- **Portable**: extract the `.tar.gz` and run `./simrs-gramedika/simrs-gramedika`
- **macOS**: open the `.dmg`, drag the app to Applications, then right-click it and choose **Open** the first time
  (the build is ad-hoc signed, not notarized). If macOS still refuses: `xattr -cr "/Applications/SIMRS Gramedika.app"`.

The database connection is configured on first start and stored per user
(`%APPDATA%\SimrsKhanza` on Windows, `~/.config/SimrsKhanza` on Linux and macOS). Uninstalling keeps it.
