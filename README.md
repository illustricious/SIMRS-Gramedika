# SIMRS Gramedika

A cross-platform reimplementation of [SIMRS Khanza](https://github.com/mas-elkhanza/SIMRS-Khanza) — the open-source Indonesian hospital
information system — written in C# on .NET 10 with Avalonia UI.

It runs directly on an existing SIMRS Khanza MySQL/MariaDB database (`sik`): same tables, same user accounts and access rights,
same stock and journal effects. The old Java application and this one can be used side by side during migration.

## Status

The menu structure follows the original Khanza main menu (categories A–U). Implemented so far:

| Menu | Area | Status |
|------|------|--------|
| A | Registration, outpatient/inpatient services, billing | Most tiles |
| B | Barcode-based procedures & drugs, environmental health lab | Complete |
| C | Attendance, staff management, K3, infection-control audits, patient-safety (SKP) | Complete |
| D | Pharmacy inventory (masters, reports, transfers, stock take, stock out, requests, goods receipt) | In progress |
| U | Application settings, users & access rights | Partial |

Tiles that are not implemented yet show a "not implemented" notice.

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
