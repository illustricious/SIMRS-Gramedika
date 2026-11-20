# SIMRS Gramedika

A cross-platform reimplementation of [SIMRS Khanza](https://github.com/mas-elkhanza/SIMRS-Khanza) — the open-source Indonesian hospital
information system — written in C# on .NET 10 with Avalonia UI.

It connects to the existing SIMRS Khanza MySQL/MariaDB database (`sik`) and uses Khanza accounts and access rights.

## Status

The dashboard follows the original Khanza menu categories A–U. As of 20 November 2026, **268 of 1,224 dashboard tiles** open a screen. The count includes shared tiles in each category where they appear. Screen coverage and feature parity are tracked separately.

| Menu | Area | Tiles with a screen |
|------|------|--------|
| A | Registration, outpatient/inpatient services, billing | **34/34** |
| B | Barcode-based procedures & drugs, environmental health lab | 16/16 |
| C | Attendance, staff management, K3, infection-control audits, patient safety (SKP) | 61/61 |
| D | Pharmacy inventory, patient stock, prescription review, and reports | 75/75 |
| E, F, G, I, K, M, S | Shared master data, patient deposits, and patient records | 9 |
| J | ICD masters, visit and diagnosis reports, and operational reports | 43/96 |
| U | Application settings, tariffs, queue displays, and account administration | **30/30** |

Menu A now includes inpatient requests, surgery schedules, KFR requests, discharge prescriptions, diet delivery, outpatient screening, early COVID screening, deposits, and inpatient cost estimates. See [Menu A status](docs/audit/MENU_A_STATUS.md) for each screen's current workflows and remaining legacy differences. Several Menu D screens provide lists or reports for existing transactions.

See [Menu U status](docs/audit/MENU_U_STATUS.md) for the settings screens and which workflows use those settings in Avalonia.

See [Menu J status](docs/audit/MENU_J_STATUS.md) for available ICD masters, visit reports, diagnosis reports, and their scope.

Pending tiles show a "not implemented" notice.

## Requirements

- .NET SDK 10 (to build)
- An existing SIMRS Khanza database (MySQL 5.7+/MariaDB 10.4+)
- Windows 10/11 or a Debian/Ubuntu-based Linux desktop

## Build & run

```bash
dotnet build SimrsKhanza.sln
dotnet test SimrsKhanza.sln
dotnet run --project src/SimrsKhanza.UI
```

On first start the app asks for the database connection (host, port, user, password, database). Profiles are stored per user
(`%APPDATA%\SimrsKhanza` on Windows, `~/.config/SimrsKhanza` on Linux), never in the repository.

Optional database smoke tests run every list/report query against the configured database (read-only):

```bash
SIMRS_DB_SMOKE=1 dotnet test SimrsKhanza.sln
```

## Installers

`installer/build-installers.ps1` produces a self-contained Windows installer (Inno Setup) and a `.deb` package — see
[installer/README.md](installer/README.md).

## Project layout

```
src/SimrsKhanza.Config   connection profiles
src/SimrsKhanza.Core     domain types, legacy access-right mapping
src/SimrsKhanza.Data     repositories on the legacy sik schema (menu specs, stock, journals)
src/SimrsKhanza.UI       Avalonia desktop application
src/SimrsKhanza.Tests    unit, headless-UI and optional database smoke tests
installer/               Windows and Debian packaging
```

## Credits

Based on the data model and business rules of SIMRS Khanza by Windiarto Nugroho and contributors. This project is an independent
reimplementation and is not affiliated with the original authors.
