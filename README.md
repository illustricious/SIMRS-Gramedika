# SIMRS Gramedika

A cross-platform reimplementation of [SIMRS Khanza](https://github.com/mas-elkhanza/SIMRS-Khanza) — the open-source Indonesian hospital
information system — written in C# on .NET 10 with Avalonia UI.

It connects to the existing SIMRS Khanza MySQL/MariaDB database (`sik`) and uses Khanza accounts and access rights.

## Status

As of 21 November 2026, **381 of 1,224 dashboard tiles** in the published branch open a screen. **843 tiles** still need a screen. Counts include shared tiles in each category where they appear. Screen coverage and feature parity are tracked separately.

| Menu | Area | Tiles with a screen | Pending |
|---|---|---:|---:|
| A | Registrasi, Tagihan Ranap & Ralan, Pelayanan & Billing Pasien | 34/34 | 0 |
| B | Tindakan & Obat & BHP Via Barcode, Lab Kesehatan Lingkungan | 16/16 | 0 |
| C | Presensi, Manajemen & Penggajian Pegawai Rumah Sakit | 61/61 | 0 |
| D | Transaksi Inventory Obat, BHP Medis, Alat Kesehatan Pasien | 75/75 | 0 |
| E | Transaksi Inventory Barang Non Medis, Penunjang ( Lab & RO ) | 2/33 | 31 |
| F | Transaksi Inventory Barang Dapur Kering & Basah | 2/27 | 25 |
| G | Aset, Inventaris Barang & Instalasi Kesehatan Lingkungan | 1/28 | 27 |
| H | Menejemen Parkir Kendaraan Pasien & Karyawan | 0/3 | 3 |
| I | Olah Data Tagihan Rawat Inap & Rawat Jalan | 60/60 | 0 |
| J | Olah Data Penyakit, Laporan DKK, Laporal RL & Laporan Internal | 96/96 | 0 |
| K | Tarif Pelayanan, Menejemen Keuangan & Akuntansi | 2/87 | 85 |
| L | Bridging VClaim, Aplicare, PCare, INACBG, Kemenkes & Pihak Ke 3 | 0/183 | 183 |
| M | Olah Data Rekam Medis Pasien | 1/255 | 254 |
| N | Unit Pelayanan Tranfusi Darah | 0/11 | 11 |
| O | Analisa, Dashboard & Info Grafik | 0/113 | 113 |
| P | Manajemen Surat Masuk & Keluar, Pengumuman E-Pasien | 0/45 | 45 |
| Q | Manajemen Perpustakaan & Koleksi Pustaka Digital | 0/16 | 16 |
| R | Pre-Construction & Infection Control Risk Assessment | 0/10 | 10 |
| S | Toko / Minimarket / Koperasi | 1/25 | 24 |
| T | Pengelolaan Data Filantropi, CSR, Zakat, Infaq & Shodaqoh | 0/16 | 16 |
| U | Pengaturan Program Aplikasi HMS | 30/30 | 0 |

See the [full backlog by tile](docs/audit/TILE_BACKLOG.md) and [Menu I implementation notes](docs/audit/MENU_BELUM_DAN_PELAJARAN_I.md).

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
