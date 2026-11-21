# SIMRS Gramedika

A cross-platform reimplementation of [SIMRS Khanza](https://github.com/mas-elkhanza/SIMRS-Khanza) — the open-source Indonesian hospital
information system — written in C# on .NET 10 with Avalonia UI.

It connects to the existing SIMRS Khanza MySQL/MariaDB database (`sik`) and uses Khanza accounts and access rights.

## Status

The dashboard follows the original Khanza menu categories A–U. As of 21 November 2026, **483 of 1,224 legacy dashboard tiles** open a screen. The count includes shared tiles in each category where they appear. Avalonia also adds a database backup tile for signed-in users. Screen coverage and feature parity are tracked separately.

| Menu | Area | Tiles with a screen |
|------|------|--------|
| A | Registration, outpatient/inpatient services, billing | **34/34** |
| B | Barcode-based procedures & drugs, environmental health lab | 16/16 |
| C | Attendance, staff management, K3, infection-control audits, patient safety (SKP) | 61/61 |
| D | Pharmacy inventory, patient stock, prescription review, and reports | 75/75 |
| E | Nonmedical logistics masters, stock movements, procurement and receiving reports | **33/33** |
| F | Kitchen inventory, purchasing, receiving, stocktake, and circulation | **27/27** |
| G | Assets, inventory, CSSD, facility maintenance, and environmental health | **28/28** |
| H | Parking types, barcode cards, and vehicle check-in | **3/3** |
| I | Billing, patient deposits, receivables, drug and procedure reports | 60/60 |
| K, M, S | Shared master data, patient deposits, patient receivables, and patient records | 4 |
| J | ICD masters, visit and diagnosis reports, and operational reports | 96/96 |
| T | Philanthropy, CSR, zakat, infaq, and shodaqoh reference data | **16/16** |
| U | Application settings, tariffs, queue displays, and account administration | **30/30 legacy tiles** + database backup |

Menu A now includes inpatient requests, surgery schedules, KFR requests, discharge prescriptions, diet delivery, outpatient screening, early COVID screening, deposits, and inpatient cost estimates. See [Menu A status](docs/audit/MENU_A_STATUS.md) for each screen's current workflows and remaining legacy differences. Several Menu D screens provide lists or reports for existing transactions.

Kasir shows a read-only breakdown of recorded procedures, drugs, and medical supplies for open visits. Settlement for visits with these charges uses Kasir Khanza.

Menu G includes editable asset masters, facility maintenance, CSSD catalog entries, and environmental health logs. The inventory, procurement, repairs, and CSSD circulation lists support search and CSV export. Asset transactions, approvals, accounting entries, and CSSD circulation updates are recorded in Khanza.

Menu E includes editable item and supplier masters, transaction history, procurement and receiving summaries, order-to-receipt comparisons, stocktake results, and circulation reports. Reports support search, period filters, and CSV export. Nonmedical stock transactions and approvals are recorded in Khanza.

Menu F includes editable wet and dry kitchen inventory and supplier masters. Its reports cover requests, purchasing, receiving, grants, returns, outgoing stock, stocktake, and circulation, with search and CSV export. Kitchen stock transactions and approvals are recorded in Khanza.

Menu H includes parking type and tariff settings, barcode card registration, and vehicle check-in with date and text filters. A card or vehicle with an open visit cannot be checked in again. Checkout, fee collection, and ticket printing are outside the current implementation.

Menu I includes read-only reports for patient charges, discounts, receivables, billing, drugs and selected procedure fees with period filters, search and CSV export. Summaries include daily billing, payments per unit and shift, and receivables by account. The daily/monthly doctor, paramedic, facility, KSO, management and BHP reports currently cover outpatient and inpatient procedures only: lab, radiology and surgery are excluded. Debt payments and legacy report printing are not included. See [Menu I audit](docs/audit/MENU_BELUM_DAN_PELAJARAN_I.md) for the remaining tiles and limitations.

See [Menu U status](docs/audit/MENU_U_STATUS.md) for the settings screens and which workflows use those settings in Avalonia.

See [Menu J status](docs/audit/MENU_J_STATUS.md) for available ICD masters, visit reports, diagnosis reports, and their scope.

Pending tiles show a "not implemented" notice.

Menu T supports search, creation, editing, and deletion for all 16 Dankes reference lists. The legacy Kepemilikan Rumah tile opens the Atap Rumah screen. Avalonia stores ownership categories independently. Apply the [Kepemilikan Rumah SQL script](docs/audit/menu_t_kepemilikan_rumah.sql) to the configured SIMRS database before using that reference list.

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

[v1.5.0 Windows AMD64 installer](https://github.com/illustricious/SIMRS-Gramedika/releases/tag/v1.5.0) is available as a self-contained `.exe` for Windows 10/11 x64.

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
