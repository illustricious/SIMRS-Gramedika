# SIMRS Gramedika

A cross-platform reimplementation of [SIMRS Khanza](https://github.com/mas-elkhanza/SIMRS-Khanza), the open-source Indonesian
hospital information system, written in C# on .NET 10 with Avalonia UI.

It connects to the existing Khanza MySQL/MariaDB database (`sik`) and uses Khanza accounts and access rights.

> **Alpha software.** Many screens are read-only, most legacy actions have not been audited yet, and transactions have only been
> tested against synthetic databases. See [Before using real data](#before-using-real-data).

- [How big is this project](#how-big-is-this-project)
- [Progress](#progress)
- [What you can do today](#what-you-can-do-today)
- [Technical details](#technical-details)
- [Build, test, install](#build-test-install)

## How big is this project

SIMRS Khanza is a very large application: registration, outpatient, inpatient and emergency care, medical records, pharmacy,
laboratory, radiology, billing, accounting, HR, inventory, assets, kitchen, blood bank, library, parking, analytics and dozens of
integrations with national health services. Rebuilding it is a long-running effort.

### The system being rewritten

Measured on the upstream source snapshot `6da4e15` (2 November 2026) used as the reference:

| Legacy SIMRS Khanza (Java) | Size |
|---|---:|
| Java source files | 1,678 |
| Lines in those files (including generated Swing UI code) | about 2.2 million |
| NetBeans form definitions (`.form`) | 1,492 |
| Jasper report templates (`.jrxml`) | 1,326 |
| Folders of companion web apps (`webapps/`) | 62 |
| `CREATE TABLE` statements in the [schema dump](docs/audit/schema_sik.sql) | 1,199 |
| Dashboard tiles in menus A–U | 1,224 |
| Tracked features (windows, actions, popup entries, tiles) | 15,843 |

### The rewrite so far

Measured at commit `ec33aad` (27 November 2026). Line counts include blank lines and comments.

| Project | C# files | C# lines | Contents |
|---|---:|---:|---|
| `SimrsGramedika.UI` | 109 | 19,757 | Avalonia desktop app, plus 40 AXAML files (4,124 lines) |
| `SimrsGramedika.Data` | 129 | 22,363 | Repositories, services, screen specifications, journals |
| `SimrsGramedika.Tests` | 43 | 4,620 | xUnit tests, headless UI tests, database fixtures |
| `SimrsGramedika.Core` | 13 | 1,987 | Entities, authentication, legacy permission map (partly generated) |
| `SimrsGramedika.Config` | 2 | 198 | Per-user connection profiles |
| **Total** | **296** | **48,925** | About 53,000 lines including AXAML |

First commit: 3 November 2026. The line counts of the two code bases are not a progress measure: many screens here are generated from
short specifications, and covering a screen is far less than matching its behaviour.

## Progress

There are two separate measures. Please read both.

### 1. Screen coverage: 1,041 of 1,224 dashboard tiles (85%)

A tile counts when it opens a screen. It does **not** mean the screen does everything the Java version does. Shared tiles are counted in
each category where they appear.

| Menu | Area | Tiles with a screen | Scope of these screens |
|---|---|---:|---|
| A | Registration, outpatient/inpatient services, billing | 34/34 | Main care workflows, see below ([notes](docs/audit/MENU_A_STATUS.md)) |
| B | Barcode-based procedures and drugs, environmental health lab | 16/16 | |
| C | Attendance, staff, occupational safety, infection-control audits, patient safety | 61/61 | |
| D | Pharmacy inventory, patient stock, prescription review, reports | 75/75 | Some stock transactions; several screens are lists or reports |
| E | Non-medical logistics | 33/33 | Editable masters and reports; transactions are entered in Khanza |
| F | Kitchen inventory and purchasing | 27/27 | Editable masters and reports; transactions are entered in Khanza |
| G | Assets, CSSD, maintenance, environmental health | 28/28 | Editable masters and logs; asset transactions are entered in Khanza |
| H | Parking | 3/3 | Tariffs, cards, check-in; no checkout or fees |
| I | Billing, deposits, receivables, drug and procedure reports | 60/60 | Read-only reports; fee summaries cover procedures only ([notes](docs/audit/MENU_BELUM_DAN_PELAJARAN_I.md)) |
| J | ICD masters, visit, diagnosis and operational reports | 96/96 | ([notes](docs/audit/MENU_J_STATUS.md)) |
| K | Tariffs, finance masters, payables, receivables, journals | 87/87 | Editable tariffs and accounts, manual journal; ledgers read-only ([notes](docs/audit/MENU_K_STATUS.md)) |
| L | Bridging (BPJS VClaim, Aplicare, PCare, INA-CBG, Ministry of Health, third parties) | 0/183 | Deferred: needs credentials and external test environments |
| M | Patient records | 255/255 | Clinical records are mostly read-only here ([notes](docs/audit/MENU_M_STATUS.md)) |
| N | Blood transfusion unit | 11/11 | Editable references; donations and stock read-only ([notes](docs/audit/MENU_NR_STATUS.md)) |
| O | Analytics and infographics | 113/113 | Aggregate tables; charts not drawn yet ([notes](docs/audit/MENU_O_STATUS.md)) |
| P | Letters, consent forms, patient announcements | 45/45 | Editable registers; patient letters read-only ([notes](docs/audit/MENU_P_STATUS.md)) |
| Q | Library and digital collection | 16/16 | Editable catalogue; loans read-only ([notes](docs/audit/MENU_Q_STATUS.md)) |
| R | Construction and infection-control risk assessment | 10/10 | Editable references; project lists read-only ([notes](docs/audit/MENU_NR_STATUS.md)) |
| S | Store, minimarket, cooperative | 25/25 | Editable masters; transaction reports read-only ([notes](docs/audit/MENU_S_STATUS.md)) |
| T | Philanthropy, CSR and zakat reference data | 16/16 | Full create, edit and delete |
| U | Settings, tariffs, queue displays, accounts | 30/30 | Plus a database backup tile ([notes](docs/audit/MENU_U_STATUS.md)) |

### 2. Feature parity: the long road

The [feature tracker](docs/tracker/README.md) inventories the Java source and records evidence for every item that has been assessed.
Status on 27 November 2026:

| Item type | Total | Complete | Partial | Missing | Deferred | Not yet audited |
|---|---:|---:|---:|---:|---:|---:|
| Actions (buttons and controls) | 10,673 | 13 | 300 | 199 | 1 | 10,160 |
| Popup menu entries | 2,652 | 0 | 161 | 237 | 0 | 2,254 |
| Windows | 1,294 | 0 | 6 | 0 | 0 | 1,288 |

That is **917 of 14,619 non-tile items assessed (about 6%)**, and only 13 marked complete. "Complete" requires evidence for the whole
flow, so working features often stay "partial". Current numbers are in the [generated summary](docs/tracker/RINGKASAN.md); the
[chart view](docs/tracker/GRAFIK.html) can be opened locally in a browser.

## What you can do today

These workflows write to the database. Each follows the Java source for tables, permissions and, where listed, journals, but many
have limits that are described in the audit documents.

| Area | Available |
|---|---|
| Registration ([audit](docs/tracker/AUDIT_REGISTRASI.md)) | Register visits with Khanza queue numbering; new patients with Khanza medical record numbers; patient notes and status; deceased record; internal, incoming and outgoing referrals; sick-leave letter; letterhead printing |
| Emergency | Registration and triage entry |
| Outpatient ([audit](docs/tracker/AUDIT_RAWAT_JALAN.md)) | a tabbed visit page like the Java one; SOAPIE notes; doctor notes; doctor and staff procedures with journals; ICD-10/ICD-9 entry; send, edit and cancel prescriptions including compounded ones, lab and radiology requests; write and print the medical resume |
| Inpatient ([audit](docs/tracker/AUDIT_RAWAT_INAP.md)) | Admission; SOAPIE; procedures with journals, including correction and deletion; obstetric and gynaecology exams; removal of wrongly entered room stays; mother and baby rooming-in with a combined bill; surgery charges with theatre drugs and operative reports; discharge summary and room release; discharge prescriptions from request to validation, handover, and full or partial return |
| Medical records ([audit](docs/tracker/AUDIT_REKAM_MEDIS.md)) | Fill, edit, delete and print 15 record forms: outpatient nursing notes, emergency observation, nutrition screening, adult fall risk, outpatient medical assessment, and admission and discharge criteria checklists for HCU, ICU, NICU, PICU and isolation |
| Pharmacy | Validate outpatient and inpatient prescriptions including compounded ones, add or substitute drugs, cancel a validation, record outpatient handover; direct sales to registered patients (non-batch stock, one location); stock transfer, stocktake, outgoing stock and requests |
| Laboratory and radiology | Record results; post journals on save and reverse them on delete |
| Cashier ([audit](docs/tracker/AUDIT_PELAYANAN_INTI.md)) | Combined outpatient and inpatient billing and receipt reprint, for one payment account without admin fee. Deposits, receivables, outpatient surgery, returns and adjustments are still settled in Khanza |
| Finance | Manual balanced journal, tariffs and accounts |

Generic report screens can be exported to CSV or printed as a letterhead PDF. A connection profile setting mirrors Khanza's
`TANGGALMUNDUR`: when backdating is off, only the main administrator can choose the date and time on clinical entries.
As in Khanza, a visit is locked for billing purposes only once it has billing rows.

Right-clicking a patient row in the main patient lists opens a context menu filtered by Khanza permissions.

Referral letters, sick-leave letters and the outpatient resume carry a QR code identifying the signing doctor, as Khanza does. It is
**not** a certified electronic signature.

### Next priorities

1. Run the transactions on a safe copy of a real hospital database and compare journals and stock with Khanza Java.
2. Test concurrent use alongside Java clients.
3. Close gaps in the core care flows: room corrections, cashier settlement cases.
4. Continue the per-action audit, then printing and report parity.
5. Bridging (menu L), once specifications and test credentials are available.

The working list is in [PRIORITAS.md](docs/tracker/PRIORITAS.md) (Indonesian).

## Technical details

| Layer | Technology |
|---|---|
| Language and runtime | C#, .NET 10 |
| Desktop UI | Avalonia 12 (Fluent theme, DataGrid, Inter font), light and dark themes |
| Database | MySQL / MariaDB through MySqlConnector; Pomelo Entity Framework Core for earlier clinical screens, direct SQL for most newer features |
| Printing | QuestPDF, QRCoder |
| Tests | xUnit, Avalonia.Headless with Skia |
| Packaging | Inno Setup, WiX, nfpm, AppImage, dmg ([installer notes](installer/README.md)) |
| Tracker tooling | Node.js 22 |

### Architecture

- **One window, many pages.** The A–U dashboard is built from a catalogue of the 1,224 legacy tiles and filtered by each account's
  Khanza access rights.
- **Specification-driven screens.** `MasterSpec` (search, add, edit, delete, CSV) and `ReportSpec` (search, period filter, totals, CSV)
  describe most menu screens; two generic views render them. Complex workflows have dedicated views and services.
- **Existing schema first.** The app works on the Khanza tables so that data stays readable by Khanza. Features that need extra tables
  ship SQL scripts: [discharge prescription handover](docs/migrations/20261125_serah_resep_pulang.sql) and
  [home ownership reference (menu T)](docs/audit/menu_t_kepemilikan_rumah.sql).
- **Transaction safety.** The transactional care and billing workflows lock the visit row (`reg_periksa ... FOR UPDATE`), re-check
  billing status and current data, then commit documents, stock movements and double-entry journals together or not at all.

More in [architecture](docs/handover/02-ARCHITECTURE.md) (Indonesian).

### Project layout

```
src/SimrsGramedika.Config   connection profiles
src/SimrsGramedika.Core     domain types, legacy access-right mapping
src/SimrsGramedika.Data     repositories and services on the legacy sik schema
src/SimrsGramedika.UI       Avalonia desktop application
src/SimrsGramedika.Tests    unit, headless-UI, fixture and optional database smoke tests
installer/                  Windows, Linux and macOS packaging
tools/                      feature tracker (node tools/tracker.mjs)
docs/handover               developer handover, runbook, session logs
docs/tracker                feature parity tracker and per-flow audits
docs/audit                  schema, per-menu status and backlog
```

Most internal documents are written in Indonesian.

## Build, test, install

### Requirements

- .NET SDK 10 to build
- An existing SIMRS Khanza database (MySQL 5.7+ or MariaDB 10.4+)
- Windows 10/11 or a Debian/Ubuntu-based Linux desktop (macOS packages are built but ad-hoc signed)

### Build and run

```bash
dotnet build SimrsGramedika.sln
dotnet test SimrsGramedika.sln
dotnet run --project src/SimrsGramedika.UI
```

On first start the app asks for the database connection (host, port, user, password, database). Profiles are stored per user
(`%APPDATA%\SimrsGramedika` on Windows, `~/.config/SimrsGramedika` on Linux), never in the repository. Settings in the former
`SimrsKhanza` folder are read automatically; saving writes to the new folder and keeps the old file.

### Tests

Latest local run (27 November 2026, 263 tests):

- Without a database: **221 passed, 0 failed, 42 skipped** because they need one.
- Against synthetic MariaDB databases, all fixture suites run by the script passed (106 tests).

On Windows, `tools\uji-fixture.ps1` starts a temporary MariaDB and runs those suites and then the whole test project.
Fixture tests are enabled with `SIMRS_<NAME>_FIXTURE_PORT` and expect empty local databases named `simrs_<name>_fixture`; see the
[runbook](docs/handover/01-RUNBOOK.md). Optional smoke tests run list and report queries, read-only, against the configured database:

```bash
SIMRS_DB_SMOKE=1 dotnet test SimrsGramedika.sln
```

GitHub Actions checks that the tracker reports are up to date on each push. Packages are built on demand or on a version tag; the
.NET tests are run locally.

### Download

Pre-built packages for Windows, Linux and macOS (x64 and ARM64) are on the
[Releases page](https://github.com/illustricious/SIMRS-Gramedika/releases). The newest published pre-release is
[0.3.0-alpha.202611261200](https://github.com/illustricious/SIMRS-Gramedika/releases/tag/v0.3.0-alpha.202611261200). It is older than
this source tree and does not include the prescription validation, lab/radiology journals, discharge medicine return or outpatient
order editing described above.

To package it yourself, see [installer/README.md](installer/README.md).

## Before using real data

- Do not develop or experiment against a production database. Use a copy, with the data owner's approval.
- Nothing here has been verified against a real hospital database copy or while Java Khanza clients are writing to it.
- Account mappings and journals need to be reconciled by your finance team.
- The published pre-releases up to `0.3.0-alpha.202611261200` save laboratory and radiology examinations **without journals**.
  Releases up to `0.2.5-alpha` also recorded pharmacy sales in the wrong tables. Data written by those builds needs reconciliation.

## Documentation

- [Developer handover](docs/handover/README.md) and [runbook](docs/handover/01-RUNBOOK.md)
- [Feature tracker](docs/tracker/README.md) and [priorities](docs/tracker/PRIORITAS.md)
- [Core services audit](docs/tracker/AUDIT_PELAYANAN_INTI.md)

## Credits

Based on the data model and business rules of SIMRS Khanza by Windiarto Nugroho and contributors. This project is an independent
reimplementation and is not affiliated with the original authors.
