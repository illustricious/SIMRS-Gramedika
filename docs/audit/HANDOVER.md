# Handover — Audit Repo SIMRS Khanza (menuju rewrite C# Avalonia)

Dokumen ini diupdate setiap ada progres. Jangan dihapus — ini adalah log kerja agar sesi berikutnya (termasuk AI/sesi chat lain) bisa lanjut tanpa mengulang dari awal.

---

# 🚀 MULAI DARI SINI — Ringkasan Cepat (TL;DR)

> Bagian ini ditulis supaya AI/orang baru yang baca dokumen ini bisa langsung paham & lanjut kerja dalam &lt;5 menit. Detail lengkap + kronologi alasan tiap keputusan ada di bagian bawah dokumen ini (urut dari paling lama ke paling baru).

## Apa proyek ini?
Rewrite **SIMRS Khanza** (software hospital management system Indonesia, awalnya Java Swing + PHP, 1.199 tabel database, sangat legacy) menjadi **C# + .NET 10 + Avalonia UI** (desktop app lintas-platform), dengan tampilan yang meniru look & feel aplikasi asli (warna, layout, navigasi) tapi backend/arsitektur dirancang ulang dari nol.

## Dua repo terpisah — JANGAN TERTUKAR
| Repo | Isi | Status |
|---|---|---|
| `B:\Documents\Script\simrs-khanza` | **Repo LEGACY/AUDIT** — source code asli Java/PHP SIMRS Khanza + data produksi asli (ada PHI/data pasien nyata!). Hanya dibaca untuk referensi, **TIDAK PERNAH diubah**. | Read-only, audit selesai |
| `B:\Documents\Script\SimrsKhanza.Avalonia` | **Repo BARU (rewrite)** — di sinilah semua kode C#/Avalonia ditulis & commit. Repo git terpisah, lokal, belum ada remote. | **Aktif dikerjakan** |

## Cara menjalankan SEKARANG (di Windows, PowerShell)
```powershell
# 1. Hidupkan MariaDB (SELALU cek dulu, sering mati karena bukan service — lihat bagian "Gotcha" di bawah)
Get-Process -Name "mariadbd" -ErrorAction SilentlyContinue
# kalau kosong/mati, jalankan:
Start-Process "C:\Program Files\MariaDB 13.0\bin\mariadbd.exe" -ArgumentList '--defaults-file="C:\Program Files\MariaDB 13.0\data\my.ini" --console'

# 2. Set koneksi DB (dev, data dummy — BUKAN data pasien asli)
$env:SIMRS_DB_HOST = "127.0.0.1"
$env:SIMRS_DB_PORT = "3306"
$env:SIMRS_DB_NAME = "sik"
$env:SIMRS_DB_USER = "khanza_dev"
$env:SIMRS_DB_PASSWORD = "DevPassword123!"

# 3. SEBELUM build ulang: matikan dulu instance app yang mungkin masih jalan (kalau tidak, build gagal karena file dll terkunci)
Get-Process -Name "SimrsKhanza.UI" -ErrorAction SilentlyContinue | Stop-Process -Force

# 4. Jalankan
& "C:\Program Files\dotnet\dotnet.exe" run --project "B:\Documents\Script\SimrsKhanza.Avalonia\src\SimrsKhanza.UI\SimrsKhanza.UI.csproj"
```
Login dev: `admin` / `admin123` (full akses termasuk Pengaturan) atau `petugas` / `petugas123` (tanpa akses Pengaturan — buat tes gate admin-only).

**Build & test:**
```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "B:\Documents\Script\SimrsKhanza.Avalonia\SimrsKhanza.sln"
& "C:\Program Files\dotnet\dotnet.exe" test "B:\Documents\Script\SimrsKhanza.Avalonia\src\SimrsKhanza.Tests\SimrsKhanza.Tests.csproj"
```
**Publish .exe standalone:**
```powershell
& "C:\Program Files\dotnet\dotnet.exe" publish "B:\Documents\Script\SimrsKhanza.Avalonia\src\SimrsKhanza.UI\SimrsKhanza.UI.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "B:\Documents\Script\SimrsKhanza.Avalonia\publish\win-x64"
```

## Yang SUDAH jalan (fitur nyata, bukan placeholder kecuali disebutkan)
- **Login** — `IAuthService`, password hash PBKDF2 (dev in-memory), + `LegacyUserRepository` yang bisa baca langsung dari tabel `user`/`petugas` legacy (plaintext password di-handle khusus) — **belum ditest ke DB asli**.
- **Dashboard** — grid ikon menu, data-driven dari permission user (`IMenuPermissionService`), bukan hardcoded.
- **Registrasi / Daftar Pasien Rawat Jalan** (`Modules/Registrasi`) — SATU layar yang diakses dari tombol toolbar "Registrasi" MAUPUN "Rawat Jalan" (keduanya sama, ini dikonfirmasi user). Form collapsible + grid 17 kolom lengkap + search + **cetak PDF nyata** (QuestPDF) + tombol "🩺 Periksa" yang lempar ke layar pemeriksaan.
- **Rawat Jalan — Pemeriksaan** (`Modules/RawatJalan`) — form vital sign + catatan klinis, **Simpan beneran nulis ke DB** (`pemeriksaan_ralan`). Diakses lewat tombol "🩺 Periksa" di Registrasi.
- **Pasien** (`Modules/Pasien`, dibuka dari tile dashboard "Rekam Medis") — form identitas pasien (field dicocokkan ke `DlgPasien.form` legacy), Simpan beneran nulis pasien baru.
- **Pengaturan** (`Modules/Pengaturan`, **admin-only** via permission `pengaturan.admin`) — edit/tes/simpan koneksi database (host/port/db/user/password), tersimpan di `%APPDATA%\SimrsKhanza\database.settings.json` di komputer user — **BISA connect ke database manapun (IP/host apa saja), sudah dikonfirmasi user berhasil connect ke database asli mereka sendiri**.
- **SearchPickerDialog<T>** generik — pengganti pola 16 dialog `Dlg*Cari*` legacy.
- **Cetak laporan (PDF)** — baru ada 1 contoh (daftar Registrasi/Rawat Jalan), pakai QuestPDF. **Lisensi QuestPDF Community gratis cuma untuk perusahaan di bawah threshold revenue tertentu** — cek questpdf.com/license sebelum dipakai produksi di RS yang lebih besar.
- **12 unit test, semua pass** (termasuk beberapa integration test ke MariaDB dev asli, auto-skip kalau DB tidak reachable).

## Pola/arsitektur yang harus diikuti untuk fitur baru
1. **Satu fitur = satu folder di `SimrsKhanza.UI/Modules/<NamaFitur>/`** berisi `<Nama>View.axaml` (UserControl, BUKAN Window — semua navigasi dalam SATU window via `MainWindow.PageHost`) + `.axaml.cs`.
2. View punya event `BackRequested` (wajib) dan event lain sesuai kebutuhan (contoh: `PeriksaRequested` di RegistrasiView).
3. Data access lewat `SimrsKhanza.Data` — repository per fitur (`XxxRepository` + `IXxxRepository`), pakai `SimrsKhanzaDbContextFactory.Create(DatabaseSettings.Load())` (bukan `FromEnvironment()` langsung — `Load()` prioritaskan setting tersimpan user).
4. Entity baru → tambahkan ke `SimrsKhanza.Core/Entities/`, lalu petakan di `SimrsKhanzaDbContext.OnModelCreating` — **WAJIB** deklarasikan semua relasi `HasOne()` eksplisit (EF bikin shadow FK aneh kalau tidak) dan **WAJIB** mapping enum pakai `HasConversion` dengan string PERSIS sama seperti nilai enum MySQL legacy (jangan andalkan `.ToString()` C#, beda formatnya — baca § "Bug kumpulan" di bawah).
5. Wire ke `MainWindow.axaml.cs`: tambah case di `OnFeatureShortcutClick`/`OnModuleTileClick`, method `OpenXxxAsync()` yang buat/reuse View lewat `PageHost`.
6. **Tulis integration test** tiap kali bikin repository baru yang nulis/baca DB (pola: coba connect, kalau gagal `return;` alih-alih fail — supaya tidak merusak CI/dev tanpa DB). Ini sudah beberapa kali menangkap bug nyata sebelum sempat dikirim ke user — JANGAN skip langkah ini.

## ⚠️ Gotcha / hal yang sering bikin bingung
1. **MariaDB BUKAN Windows Service** — `mariadbd --install` butuh admin elevation yang sesi-sesi sebelumnya tidak punya. Server mati tiap komputer idle/restart. **SELALU cek proses sebelum kerja** (lihat langkah 1 di atas). Kalau user mau permanen, mereka perlu jalankan `mariadbd --install` sendiri sebagai Administrator.
2. **Kalau mau `dotnet build` tapi app masih kebuka** → build gagal karena file DLL terkunci proses yang jalan. Selalu `Stop-Process -Name "SimrsKhanza.UI" -Force` dulu.
3. **Database dev (`sik` di MariaDB lokal) HANYA punya 11 tabel** (10 inti + `pemeriksaan_ralan`) + 8 tabel lookup kosong (placeholder `id=0`/`"-"`) — BUKAN restore dari `sik.sql` legacy yang 12.8MB (sengaja tidak disentuh, demi privasi data pasien asli). Kalau butuh tabel baru, extract schema-nya dari `docs/audit/schema_sik.sql` (hasil ekstraksi CREATE TABLE saja, tanpa data, dari repo legacy).
4. **Insert ke kolom `AUTO_INCREMENT` dengan nilai literal `0`** otomatis diganti MySQL/MariaDB jadi nomor urut berikutnya, KECUALI `sql_mode` include `NO_AUTO_VALUE_ON_ZERO`. Sudah kejadian sekali (tabel lookup kelurahan/kecamatan/dst), sudah diperbaiki — ingat ini kalau nambah tabel lookup baru.
5. **Enum C# `.ToString()` ≠ string MySQL legacy** (contoh: `StatusBayar.SudahBayar.ToString()` = `"SudahBayar"`, bukan `"Sudah Bayar"` yang dipakai legacy). Selalu mapping eksplisit, jangan andalkan `.ToString()`, untuk apapun yang ditampilkan ke user atau dibandingkan ke nilai DB.
6. **Dua screenshot "Registrasi Periksa Hari Ini" vs "Daftar Pasien Rawat Jalan"** yang user kasih ternyata LAYAR YANG SAMA (dikonfirmasi user), diakses dari dua tombol toolbar berbeda ("Registrasi" dan "Rawat Jalan") — makanya `MainWindow` wire keduanya ke `OpenRegistrasiAsync()`.

## Urutan prioritas kerja selanjutnya (rekomendasi, belum dikerjakan)
1. **Rawat Inap** & **Kasir/Billing** — pelengkap alami alur yang sudah ada.
2. **Laboratorium** & **Radiologi** — hasil pemeriksaan penunjang (user sempat typo "Labgrat" dikoreksi jadi "Laboratorium", tapi fiturnya sendiri belum dibangun — masih placeholder).
3. Perluas fitur cetak PDF ke layar lain (Pasien, Rawat Jalan) — pola sudah ada di `Modules/Registrasi/RegistrasiReport.cs`, tinggal direplikasi.
4. Keputusan arsitektur "formulir dinamis" untuk 177 tabel `penilaian*`/`skrining*`/`surat*` legacy (lihat `PLANNING_APLIKASI.md`) — BELUM diputuskan, butuh persetujuan user karena mengubah pendekatan data model secara fundamental.
5. Selesaikan `LegacyUserRepository` — test ke DB `sik` ASLI (bukan dev), rencana migrasi "rehash on first login" dari plaintext ke PBKDF2 (lihat bagian detail di bawah).
6. MariaDB jadi Windows Service permanen (butuh admin elevation dari user sendiri).
7. Isi IGD/UGD, Farmasi, Rawat Inap, Keuangan, Kepegawaian, Inventaris, Laporan, Bridging — semua masih placeholder "belum diimplementasikan".

---

## Status: SEDANG BERJALAN (audit read-only, belum ada file yang diubah/dihapus)

## Konteks penting
- Working dir: `B:\Documents\Script\simrs-khanza`, git repo, branch `master`, bersih (clean), 2143 commit.
- **TEMUAN KRITIS**: Direktori ini BUKAN sekadar source code — ini tampak seperti direktori kerja/produksi sungguhan yang di-`git add` apa adanya:
  - Tidak ada `.gitignore` sama sekali → cache, build output, dan data runtime semua masuk git history.
  - Ada dump database nyata: `sik.sql` (~12.8 MB), `sik_bridging_lab.sql`, `sik_bridging_radiologi.sql` — kemungkinan berisi data pasien asli (PHI).
  - Ada ratusan file hasil generate laporan di root (`.html`, `.csv`, `.wps`, `.pdf`) dengan nama pasien/tanggal (contoh: banyak `RPP2024*.pdf`, `RPP2025*.pdf` — kemungkinan resep/dokumen pasien).
  - Folder `cache/` ikut tercommit.
  - **Implikasi**: Harus sangat hati-hati — ini kemungkinan berisi data kesehatan pasien sungguhan. TIDAK melakukan apa pun yang destruktif tanpa konfirmasi eksplisit, dan tidak mem-publish/mengunggah isi file-file ini ke mana pun.

## Arsitektur yang teridentifikasi sejauh ini (perlu konfirmasi lebih lanjut dari sub-agent)
- Ini adalah **dua aplikasi** dalam satu repo:
  1. **Aplikasi desktop Java (Swing)** di `src/` — proyek NetBeans/Ant (`build.xml` + `nbproject/` di root). Paket utama kemungkinan `src/simrskhanza`. Menggunakan JasperReports (`.jrxml`) untuk cetak laporan.
  2. **Aplikasi web PHP** di `webapps/` — tampak PHP prosedural (belum dikonfirmasi apakah pakai framework).
- Banyak **modul satelit** terpisah di top-level (folder `Khanza*`, `api-bpjsfktl`, `api-bpjsfktp`, `api-bridgingradiologi`, `bankjateng`, `bankpapua`, `bjb`, `mandiri`, dll) — kemungkinan aplikasi Java/VB6 kecil terpisah untuk: antrian, fingerprint anjungan, auto-verify BPJS, updater, enkripsi, bridging bank/BPJS/SatuSehat. Sedang diaudit oleh sub-agent.
- `Claude outputs/` berisi sisa pekerjaan sesi sebelumnya yang belum selesai (`menampilkanPenilaianBishop.java`, `rptFormulirPenilaianBishop.jrxml`) — sejalan dengan commit log terakhir "Belum fix, Penilaian BISHOP Score". Ini kemungkinan WIP developer sebelumnya, bukan artefak yang bisa dihapus begitu saja.

## Ukuran repo (du -sh per folder top-level, terbesar)
Total repo: **3.7 GB** (working tree, belum termasuk overhead .git). Breakdown terbesar:
- `src/` 202M (Java desktop source — perlu dicek kenapa sebesar ini, kemungkinan ada aset/gambar/compiled class ikut)
- `report/` 105M — **diduga runtime output**, bukan source
- `cache/` 105M — **runtime cache**, kandidat cleanup kuat
- `resumepasien/` 83M — **diduga data/dokumen pasien (PHI)**, bukan source
- `webapps/` 76M (PHP web app)
- `epasien/` 56M, `eeksekutif/` 36M, `edokter/` 36M, `emcu/` 35M — diduga masing-masing adalah sub-aplikasi web/portal terpisah (pasien, eksekutif, dokter, MCU) — perlu konfirmasi apakah source atau data
- `driverFlexCode/` 21M — nama menunjukkan driver perangkat (fingerprint/flexcode reader) — kemungkinan installer/binary, bukan source kita
- `KhanzaHMSAnjunganFingerPrint/`, `KhanzaHMSAnjungan/` ~11M masing-masing
- Modul `Khanza*` lainnya 1-8M masing-masing

## Progres audit (sub-agent paralel, read-only)
- [x] Sub-agent 1: survei modul satelit top-level — **SELESAI**, hasil ringkas:
  - Root Java project: `nbproject/project.properties` → `src.dir=src`, `main.class=simrskhanza.SIMRSKhanza`.
  - `alarm`, `presensi` = Delphi/Pascal app (campur source `.pas/.dfm/.dpr` + binary `.exe/.dcu`; `presensi` cuma ketemu binary, source-nya TIDAK ADA/hilang).
  - `api-bpjsfktl/fktp`, `api-bridgingradiologi`, `bankjateng`, `bankpapua`, `bjb`, `mandiri` = PHP bridging kecil, source asli, tapi tiap modul punya `conf.php` sendiri dengan kredensial.
  - `edokter`, `eeksekutif`, `emcu`, `epasien`, `esign` = portal web PHP genuine (35-56MB tiap satu, 130-300 file php), masing-masing punya `conf/conf.php` + `login.php`.
  - Semua modul `Khanza*` (antrian, fingerprint anjungan, auto-verify, resume, service BPJS/JKN/SatuSehat/PCare/SPDGT, enkripsi, updater) = proyek NetBeans Java tersendiri; source `.java` asli, tapi tiap punya `.class`/`.jar` hasil compile yang ikut tercommit (kandidat cleanup, regenerable).
  - `driverFlexCode` = **binary/installer pihak ketiga murni** (driver fingerprint/barcode reader) — bukan source kita, 21MB, kandidat cleanup kuat.
  - `report/` = 1313 file `.jrxml` (source JasperReports, SIMPAN — referensi layout cetak) + 1314 file `.jasper` (compiled binary, regenerable dari .jrxml, **kandidat cleanup terbesar yang aman**).
  - `resumepasien/` = app Java dengan `lib/` berisi **110 JAR vendor pihak ketiga** (ant, antlr, httpclient, dll) yang ikut tercommit — ini seharusnya dependency, bukan source, **kandidat cleanup terbesar berdasarkan ukuran**.
  - `setting/database.ini` → **ditemukan kredensial database plaintext (host/user/pass) tersimpan di repo** — temuan keamanan prioritas tinggi.
  - **Temuan silang**: hampir semua modul PHP (`conf.php`) dan `setting/database.ini` menyimpan kredensial DB/API secara plaintext di source control.
- [x] Sub-agent 2: survei `webapps/` — **SELESAI**, hasil ringkas:
  - PHP murni prosedural, TANPA framework (tidak ada composer.json/router). Pola "satu folder per fitur" (conf/css/images/js/pages), bukan MVC — tiap modul duplikasi kredensial & helper sendiri.
  - DB: `mysqli_connect` ke `localhost`/db `sik`/user `root`/password kosong (gaya kredensial lokal/dev) — cocok dengan `sik.sql` di root. Juga ada 2 kredensial webhook hardcoded (konstanta `USERHYBRIDWEB`/`PASHYBRIDWEB`).
  - Total 76MB, 2872 file. Area fitur: antrian/display, billing/keuangan, rekam medis & formulir consent (puluhan modul form digital), hasil pemeriksaan diagnostik (echo/EKG/USG/endoskopi dll — struktur identik), logistik/inventaris, integrasi BPJS (inacbg), presensi HR, lab/radiologi, surat.
  - **PHI nyata ditemukan tercommit**: `billing/temp/` berisi gambar tanda tangan PNG dengan NAMA PASIEN ASLI di nama filenya. Ini bukti konkret data pasien sungguhan ada di working tree & kemungkinan di git history.
  - Aset vendor pihak ketiga (`assets/vendor/bootstrap`, `jquery.min.js`, `phpqrcode/`) → aman diganti saat rewrite. Aset custom (css/js/images per modul) → **referensi UI/UX wajib dipertahankan**.
- [x] Sub-agent 3: survei `src/` Java — **SELESAI**, hasil ringkas:
  - NetBeans Ant project, `main.class=simrskhanza.SIMRSKhanza`, target Java 8.
  - **Proyek Java TIDAK BISA DI-BUILD saat ini** — 280 referensi JAR di `nbproject/project.properties` menunjuk path absolut di Mac developer lama, tidak ada satu pun JAR itu ikut tercommit. Classpath harus direkonstruksi dulu kalau mau compile versi lama.
  - 1.678 file `.java` di 31 package, TIDAK ada `.class`/`.jar` ikut tercommit di dalam `src/` (bersih).
  - `build.xml` mengonfirmasi `report/`, `setting/`, `suara/`, `gambar/` adalah **runtime resource yang di-copy ke build output**, bukan source buang — harus dipertahankan sebagai referensi.
  - DB access: satu class global `fungsi.koneksiDB`, JDBC mentah (driver MySQL Connector/J lama), tanpa ORM. ~130 kredensial/konfigurasi (BPJS, SatuSehat, PCare, Mobile JKN, bank, PACS, dst) disimpan di `setting/database.xml` terenkripsi AES, dibaca ulang dari disk tiap kali dipanggil (tidak ada caching) — catatan desain untuk rewrite.
  - Ditemukan SQL string-concat (rawan SQL injection) — dicatat untuk migrasi, tidak diperbaiki sekarang.
  - `Claude outputs/menampilkanPenilaianBishop.java` = fragmen method belum selesai (tidak bisa dicompile berdiri sendiri); `rptFormulirPenilaianBishop.jrxml` DUPLIKAT dengan versi final yang sudah ada di `report/rptFormulirPenilaianBishop.jrxml`. Ini sisa WIP sesi sebelumnya — **jangan dihapus tanpa konfirmasi user**, tapi juga tidak terintegrasi ke `src/`.

## Selesai — Semua Survei
Audit read-only selesai. Laporan lengkap: lihat `AUDIT_REPORT.md` (file terpisah, di folder yang sama dengan handover ini).

## Status: CLEANUP TAHAP 1 (kategori "aman") SUDAH DIEKSEKUSI — 2026-11-03

User menyetujui "cleanup aman dulu" (kategori risiko rendah di §7 AUDIT_REPORT.md #1-6). Yang sudah dihapus dari **working tree saja** (BELUM di-commit, BELUM di-push):

| # | Yang dihapus | Jumlah file | Ukuran |
|---|---|---|---|
| 1 | `report/*.jasper` (compiled JasperReports, source `.jrxml` TETAP ADA) | 1.314 | ~67M |
| 2 | `resumepasien/lib/*.jar` (vendor jar pihak ketiga) | 109 | ~73M |
| 3 | `cache/` (seluruh isi) | 137 | ~105M |
| 4 | `driverFlexCode/` (installer/driver pihak ketiga) | seluruh folder | ~21M |
| 5 | `alarm/Alarm.exe`, `Alarm.~dpr`, `UUtama.dcu`, `UUtama.~ddp`, `UUtama.~pas` (binary + backup editor) | 5 | kecil |
| 6 | `*.class`/`*.jar` hasil compile di tiap folder `Khanza*` | 1.040 | ~7.3M |

**Total**: ~2.615 file terhapus dari working tree, repo lokal turun dari **3.7 GB → 3.4 GB** (~300MB).

**PENTING — status git saat ini:**
- Repo punya remote `origin` → `https://github.com/mas-elkhanza/simrs-khanza` (push access ada). **TIDAK ada commit, TIDAK ada push** yang dilakukan.
- `git status --short` menunjukkan 2.615 baris ` D <path>` — artinya perubahan ini **unstaged, hanya di working tree**, dan **masih bisa dibatalkan 100%** kapan saja dengan:
  ```bash
  git checkout -- .
  ```
  (ini akan mengembalikan semua 2.615 file yang baru dihapus, karena belum di-commit).
- Belum ada keputusan soal commit/push — tunggu instruksi user selanjutnya.
- Riwayat git (2.143 commit) TIDAK disentuh sama sekali — file-file ini masih ada di history sampai ada tindakan eksplisit terpisah (misalnya `git filter-repo`) yang **belum dan tidak akan dilakukan tanpa konfirmasi khusus**.

## Keputusan user (2026-11-03): data sensitif DIBIARKAN
- User mengonfirmasi: kategori #7-#11 (file ekspor loose root, `sik.sql`, PDF/PNG pasien, `presensi/`, WIP `Claude outputs/`) **dibiarkan apa adanya, TIDAK dihapus** — berpotensi dipakai sebagai contoh data fitur nanti (dengan catatan: harus dianonimkan dulu kalau dipakai).
- Catatan klarifikasi yang disampaikan ke user: nama "RS SIMRS KHANZA" (Guwosari, Pajangan, Bantul) di tabel `setting` kemungkinan besar **nilai default vendor Khanza Softmedia**, bukan nama RS asli — tapi baris data pasien (nama/NIK/HP) tetap terlihat seperti entri nyata & aktif (tanggal cocok dengan tanggal sistem sekarang 2026-11-03). Status: tidak dihapus, tidak dibuka lebih lanjut.

## Planning aplikasi utama — SELESAI (tahap 1)
Dokumen `PLANNING_APLIKASI.md` sudah dibuat: hasil survei struktur navigasi `src/simrskhanza` (shell utama, bukan MDI, dashboard grid ikon runtime per-role, login menyatu di `frmUtama`, 16 dialog `Dlg*Cari*` duplikat sebagai target reuse pertama) + rencana pemisahan tanggung jawab (Auth/Config/Menu/DB jadi service terpisah) + urutan kerja 5 langkah mulai dari shell & dashboard dulu. **Belum ada kode rewrite ditulis** — ini murni dokumen planning.

## Scaffold proyek C#/Avalonia — SELESAI (2026-11-03)
- Lokasi: **`B:\Documents\Script\SimrsKhanza.Avalonia`** — folder BARU, TERPISAH dari `simrs-khanza` (repo legacy sama sekali tidak disentuh/dimodifikasi dalam proses ini).
- `.NET 8 SDK` diinstall via `winget` (sebelumnya belum ada SDK di komputer ini) — sudah dikonfirmasi oleh user.
- Struktur solution (`SimrsKhanza.sln`) sesuai rekomendasi di `AUDIT_REPORT.md` §9:
  - `src/SimrsKhanza.UI` — Avalonia App (net8.0, Avalonia 11.2.3 — versi 12.1.5 bawaan template awalnya gagal build karena butuh compiler lebih baru dari SDK 8; sudah di-pin ke 11.2.3 + fix `Program.cs` buang `WithDeveloperTools()` yang tidak ada di versi ini). Sudah ada folder placeholder `Modules/{RekamMedis,Keuangan,Kepegawaian,Inventaris,Laporan,Bridging}`, `Assets/`, `ReportTemplates/` (semua kosong, pakai `.gitkeep`).
  - `src/SimrsKhanza.Core`, `SimrsKhanza.Data`, `SimrsKhanza.Integrations`, `SimrsKhanza.Config` — class library kosong, referensi sudah dikawat: UI → Core/Data/Integrations/Config; Data → Core; Integrations → Core.
  - `src/SimrsKhanza.Tests` — xUnit project, referensi ke Core & Data.
  - `docs/audit/` — berisi salinan `AUDIT_REPORT.md`, `PLANNING_APLIKASI.md`, `HANDOVER.md` (dokumen ini) untuk referensi tim.
  - `docs/legacy-reference/` — folder kosong disiapkan untuk nanti diisi cuplikan referensi dari repo lama (screenshot, cuplikan .jrxml, dll) saat migrasi per modul berjalan.
- **`dotnet build` solution BERHASIL (0 error, 0 warning)** — scaffold siap dipakai, belum ada business logic/UI nyata (masih default template Avalonia kosong).
- Git repo baru sudah di-`init` di folder ini + `.gitignore` utk .NET, semua file sudah di-`git add` (28 file) tapi **BELUM di-commit** — menunggu konfirmasi user kapan mau commit pertama (dan apakah mau dihubungkan ke remote GitHub baru atau cukup lokal dulu).

## Commit pertama — SELESAI
2 commit sudah masuk ke repo `SimrsKhanza.Avalonia` (scaffold solution + docs audit). Repo masih lokal saja (belum ada remote).

## Baca skema database — SELESAI (2026-11-03)
- Diekstrak **HANYA statement `CREATE TABLE`** dari `sik.sql` + 2 file bridging — dikonfirmasi 0 baris `INSERT INTO` ikut (tidak ada data pasien yang dibaca/disalin).
- Hasil: **1.199 tabel total**. Dominan grup: `penilaian*` (87, formulir asesmen klinis), `satu*` (57, integrasi SatuSehat), `surat*` (55), `set*` (53), `skrining*` (35), `master*` (34).
- 10 tabel inti didokumentasikan detail (kolom + FK + relasi): `pasien`, `reg_periksa`, `dokter`, `pegawai`, `petugas`, `penjab`, `poliklinik`, `bangsal`, `kamar`, `kamar_inap`.
- **Keputusan arsitektur penting yang diusulkan** (BELUM dieksekusi, perlu persetujuan): grup `penilaian*`/`skrining*`/`surat*` (177 tabel) jangan di-port 1:1 jadi 177 entity/dialog — usul digeneralisasi jadi satu pola "formulir dinamis" (`FormKlinis` + `FormKlinisJawaban` + skema formulir sebagai data). Ini mengubah pendekatan data model secara fundamental dari versi lama.
- Dokumen lengkap: `DB_SCHEMA.md` + file referensi `schema_sik.sql` (1.3MB, schema-only), keduanya sudah disalin ke `docs/audit/` di repo baru.

## Entity Core + UI shell tahap 1 — SELESAI (2026-11-03)
- 10 entity C# dibuat di `SimrsKhanza.Core/Entities/` (Pasien, RegPeriksa, Dokter, Pegawai, Petugas, Penjab, Poliklinik, Bangsal, Kamar, KamarInap) — mapping 1:1 konsep dari 10 tabel inti di atas, enum MySQL → C# enum.
- `MainWindow.axaml` diisi placeholder: dashboard grid ikon menu (6 tile: Rekam Medis, Keuangan, Kepegawaian, Inventaris, Laporan, Bridging) + top bar + status bar — meniru pola navigasi grid-ikon-per-role dari `frmUtama.java` lama. **Belum ada logic apa pun** (tidak konek DB, tidak ada login nyata, tombol belum berfungsi) — murni tampilan shell.
- `dotnet build` sukses 0 error. App dicoba jalan sebentar (`dotnet run`) — window terbuka tanpa error, lalu dihentikan.
- File placeholder `Class1.cs` di 4 class library dihapus (sudah digantikan Entities asli / akan diisi menyusul).

## Upgrade ke .NET 10 — SELESAI (2026-11-03)
- Atas permintaan user, semua project di-upgrade dari `net8.0` → **`net10.0`**. `.NET 10 SDK` (10.0.403, rilis stabil) diinstall via winget (SDK 8 tetap ada, tidak dihapus — keduanya coexist).
- Avalonia dikembalikan ke versi terbaru **12.1.5** (sebelumnya sempat di-downgrade ke 11.2.3 karena SDK 8 belum cukup baru), `WithDeveloperTools()` di `Program.cs` dikembalikan juga.
- `dotnet build` sukses, 0 error, 0 warning.

## IAuthService + Login nyata — SELESAI (2026-11-03)
- **Temuan penting dari skema DB** yang memicu desain ini: tabel legacy `user` punya **~1.225 kolom boolean, satu kolom per fitur** (PK `id_user`, MyISAM, tanpa FK) — persis pola "satu baris permission raksasa" yang sudah diduga sebelumnya, sekarang terkonfirmasi langsung dari skema. Tabel `admin` (`usere`/`passworde` bertipe `text`) juga mengindikasikan **password tersimpan plaintext** di versi lama.
- Dibuat di `SimrsKhanza.Core/Auth/`: `UserAccount`, `Permission` (katalog), `UserPermission` (grant per-permission — pengganti 1.225 kolom boolean jadi baris-baris key-value), `AuthenticatedUser`, `LoginResult`, `IAuthService`, `IUserRepository`, `PasswordHasher` (PBKDF2 bawaan .NET, tanpa dependency tambahan — pengganti plaintext lama), `AuthService` (implementasi).
- Dibuat di `SimrsKhanza.Data/Auth/`: `InMemoryUserRepository` — **dummy dev-only**, seed 1 akun (`admin`/`admin123`), BUKAN koneksi ke `sik` asli.
- UI: `LoginWindow.axaml(.cs)` (window terpisah, panggil `IAuthService`) + `MainWindow` diupdate menerima `AuthenticatedUser` dan menampilkan nama login. `App.axaml.cs` di-wire: login dulu → sukses → MainWindow; gagal/ditutup → app exit.
- **4 unit test** di `SimrsKhanza.Tests/AuthServiceTests.cs` — semua PASS (login sukses, password salah, user tidak ada, verifikasi hash).
- App dicoba jalan (`dotnet run`) — window login tampil tanpa crash, lalu dihentikan.

## Commit ke-3 — SELESAI (2026-11-03)
`7df3204` — upgrade .NET 10, Entities, Auth, UI shell+login, docs skema. Masuk, working tree bersih.

## IMenuPermissionService (dashboard data-driven) — SELESAI (2026-11-03)
- `Core/Auth/MenuPermission.cs`: `MenuItemDefinition` (Key, Label, Module, RequiredPermission), `IMenuCatalog`/`DefaultMenuCatalog` (6 item, cocok folder `Modules/`), `IMenuPermissionService`/`MenuPermissionService` — filter katalog menu berdasar `AuthenticatedUser.Permissions`.
- `MainWindow.axaml` 6 tombol hardcoded DIHAPUS, diganti `WrapPanel x:Name="DashboardPanel"` kosong. `MainWindow.axaml.cs` membangun tile secara terprogram dari `IMenuPermissionService.GetVisibleMenu(user)` setelah login sukses — benar-benar data-driven, bukan XAML statis.
- Tile "Rekam Medis" diklik → buka demo `SearchPickerDialog` (lihat bawah). Tile lain → dialog placeholder "belum diimplementasikan".

## SearchPickerDialog generik — SELESAI (2026-11-03)
- `UI/Controls/SearchPickerDialog.axaml(.cs)` — window generik (`SearchPickerDialog.ShowAsync<T>(owner, title, searchFunc, displaySelector)`), pengganti pola 16 dialog `Dlg*Cari*` di legacy jadi SATU window reusable. (Catatan teknis: Avalonia tidak mendukung code-behind XAML generik langsung, jadi window-nya non-generik internal pakai `object`, dibungkus method static generik di luar — caller tidak melihat detail ini.)
- Demo nyata: `UI/Modules/RekamMedis/CariDokterDemo.cs` — memanggil `SearchPickerDialog.ShowAsync<Dokter>(...)` yang di-backing oleh `IDokterRepository` (lihat EF Core di bawah).

## Koneksi MySQL nyata via EF Core — SETUP SELESAI, BELUM BISA DIVERIFIKASI END-TO-END
- **Tidak ada MySQL/MariaDB maupun Docker di komputer ini** (dicek: tidak ada service, tidak ada port 3306 listening, tidak ada `docker` command) — jadi koneksi nyata ke `sik` TIDAK BISA dites langsung dalam sesi ini. Yang dikerjakan:
  - `SimrsKhanza.Data` ditambah `Pomelo.EntityFrameworkCore.MySql` 8.0.2 + `MySqlConnector` 2.5.0 (dipin manual, versi terpatch terbaru yang tersedia — advisori NU1902 moderate tetap muncul karena memang belum ada rilis yang menghapusnya sepenuhnya; dicatat sebagai risiko yang dipantau, bukan diabaikan) + `Microsoft.Extensions.Caching.Memory` 8.0.1 (fix advisori high-severity NU1903).
  - `SimrsKhanzaDbContext.cs` — Fluent API mapping 10 entity inti ke nama tabel/kolom snake_case ASLI dari legacy (`pasien.no_rkm_medis`, `reg_periksa.no_rawat`, dst) — didesain supaya BISA langsung diarahkan ke database `sik` yang sudah ada tanpa migrasi data, begitu server MySQL tersedia.
  - `SimrsKhanza.Config/DatabaseSettings.cs` — baca kredensial dari **environment variable** (`SIMRS_DB_HOST/PORT/NAME/USER/PASSWORD`), TIDAK ada kredensial hardcoded di source.
  - `IDokterRepository`/`DokterRepository` — query nyata via `DbContext` ke tabel `dokter`.
  - `CariDokterDemo` memanggil repository ini; **kalau gagal konek (pasti gagal di environment ini karena tidak ada server), otomatis fallback ke 2 data dummy berlabel jelas "(demo — tidak konek DB)"** supaya UI picker tetap bisa didemokan tanpa crash.
- **Build solution sukses (0 error)**, 4 unit test tetap pass, app dicoba jalan (`dotnet run`) — login window tampil tanpa crash dalam 6 detik observasi.
- **Belum diverifikasi secara visual/interaktif** (klik tombol dashboard, buka dialog Cari Dokter sungguhan) — environment ini tidak punya tool screenshot desktop; verifikasi sejauh ini dari review kode + build + smoke-run tanpa crash, bukan klik manual.

## MySQL lokal — SELESAI, koneksi nyata TERVERIFIKASI (2026-11-03)
- **MariaDB 13.0.2** diinstall via winget (`MariaDB.Server`), terpasang di `C:\Program Files\MariaDB 13.0`.
- **PENTING — bukan Windows Service**: sesi ini tidak punya hak admin untuk register service (`mariadbd --install` gagal "Access is denied"). Server dijalankan sebagai **proses background manual** (`mariadbd.exe --console`), jadi **TIDAK auto-start setelah reboot/logoff**. Kalau servernya mati, jalankan ulang:
  ```powershell
  Start-Process "C:\Program Files\MariaDB 13.0\bin\mariadbd.exe" -ArgumentList '--defaults-file="C:\Program Files\MariaDB 13.0\data\my.ini" --console'
  ```
  Untuk permanen (auto-start), user perlu jalankan `mariadbd --install` sendiri dari PowerShell **as Administrator**.
- Database `sik` dibuat, user dev `khanza_dev` (password `DevPassword123!` — **ini kredensial LOKAL/DEV di komputer ini saja, tidak pernah dikomit ke git**, hanya dicatat di sini untuk referensi tim).
- **10 tabel inti diimpor** (struktur saja, dari `schema_sik.sql`, FK checks dimatikan sementara saat import karena tabel referensi seperti `kelurahan`/`departemen` belum ada — ini database testing kosong, bukan hasil restore `sik.sql` produksi).
- **2 baris dokter dummy di-seed** (`D0000099`/`D0000098`, nama jelas berlabel "(seed dev)") — BUKAN data pasien/dokter asli, murni untuk tes query.
- **Integration test baru**: `SimrsKhanza.Tests/DokterRepositoryIntegrationTests.cs` — connect ke DB nyata via `SimrsKhanzaDbContextFactory`, query via `DokterRepository`, assert hasil cocok dengan 2 baris seed. **PASS** saat env var `SIMRS_DB_*` diset ke server lokal ini. Test ini otomatis skip (tidak fail) kalau tidak ada server reachable, supaya tidak merusak CI/dev machine lain.
- **Total 5 unit test, semua PASS** (4 lama + 1 integration test baru).
- App (`dotnet run`) dicoba lagi dengan env var DB nyata terpasang — tidak crash.

## Redesign UI shell meniru tampilan asli SIMRS Khanza — SELESAI (2026-11-03)
User menunjukkan 2 screenshot aplikasi Khanza asli (layar login kosong "RS GRAMEDIKA 10" dan layar Menu dengan grid ikon) + referensi warna dari situs rsugramedika10.com. Permintaan: tampilan topbar 2 baris (shortcut), grid menu saat "Menu" diklik, status bar bawah, warna dominan biru + aksen merah tipis, background putih, plus dark mode abu-abu nyaman.
- **Palet warna** diambil nyata dari https://www.rsugramedika10.com/ via browser (bukan tebakan): primary blue `#1E3A8A`, accent red `#DC2626`, neutral slate (`#1E293B`/`#64748B`/`#E2E8F0`/`#F1F5F9`). Dituangkan di `App.axaml` sebagai `ResourceDictionary.ThemeDictionaries` (Light/Dark) — light pakai palet di atas, dark pakai varian abu-abu nyaman (`#2A2E35` bg, bukan hitam pekat) dengan blue/red yang di-lighten biar tetap kontras.
- **`MainWindow.axaml` dirombak total** meniru struktur legacy:
  - Row 0: topbar biru — shortcut "Program / Presensi Pegawai / Informasi / Anjungan & Antrian / Tentang Program" + toggle dark/light mode (🌙/☀) di kanan.
  - Row 1: toolbar kedua (background surface terang) — "Menu" (toggle ke grid dashboard), lalu shortcut fitur langsung (Registrasi, IGD/UGD, Labgrat, Radiologi, Farmasi, Rawat Inap, Rawat Jalan), "Keluar" (merah, accent) di kanan.
  - Row 2: area konten — default tampil **hero/welcome panel** (nama app + instruksi), begitu "Menu" diklik berganti ke **panel dashboard**: dropdown "Tampilkan Menu" (baru 1 kategori karena katalog baru 6 modul, meniru dropdown kategori A/B/C/dst di legacy) + grid ikon tile (data-driven dari `IMenuPermissionService`, sama seperti sebelumnya, cuma di-restyle).
  - Row 3: status bar biru tua — nama user login, jam real-time (update tiap detik), teks kredit.
- **Dark mode nyata berfungsi**: toggle button mengubah `Application.Current.RequestedThemeVariant` antara `Light`/`Dark`, semua warna di XAML pakai `DynamicResource` jadi otomatis ganti tanpa restart app.
- Sempat ada bug build: `ToggleButton` ternyata TIDAK mewarisi `Button` di Avalonia (sama-sama turunan `ButtonBase` tapi grammar selector XAML tidak resolve `ButtonBase` langsung) — diperbaiki dengan selector ganda `Button.shortcut, ToggleButton.shortcut`.
- Build sukses, 5 unit test tetap pass, app dicoba jalan (`dotnet run` dengan env var DB nyata) — tidak crash.
- **TIDAK bisa diverifikasi secara visual** (screenshot/klik manual) — environment sesi ini tidak punya tool computer-use/screenshot desktop yang aktif. Verifikasi sejauh ini murni dari review kode + build sukses + smoke-run tanpa crash. **User disarankan menjalankan `dotnet run` sendiri di proyek `SimrsKhanza.UI` untuk konfirmasi visual** sebelum dianggap final.

## Layar Registrasi — SELESAI (2026-11-03)
User kirim screenshot layar "Registrasi" asli (ditandai lingkaran hijau pada tombol toggle "Input Data", area data pasien di-blok merah karena berisi data asli). Diminta: menu Registrasi minimal bisa jalan dulu, lalu di-compile jadi `.exe`.
- **Data dummy di-seed ke DB dev** (`sik` lokal, BUKAN data pasien asli): 1 poliklinik ("Poli Umum (seed dev)"), 2 pasien ("PASIEN CONTOH SATU/DUA (seed dev)"), 2 reg_periksa tertanggal hari ini (03-11-2026), terhubung ke 2 dokter dummy yang sudah ada dari sesi sebelumnya.
- **`RegistrasiRepository`** (Data) — join `reg_periksa`+`pasien`+`poliklinik`+`dokter`, filter hari ini, proyeksi ke `RegistrasiListItem` (bukan entity EF, murni view-model baca).
- **`Modules/Registrasi/RegistrasiWindow`** (UI) — meniru layout screenshot: blok form "Input Data" atas (No.Reg/No.Rawat/Tgl.Reg/Dr Dituju/Unit | No.RM/Penanggung Jawab/Alamat P.J./Jenis Bayar/Asal Rujukan) yang **bisa di-collapse/expand** via `ToggleButton` (persis tombol lingkaran hijau di screenshot user) menggunakan `IsVisible` binding manual di code-behind; `DataGrid` (paket `Avalonia.Controls.DataGrid` ditambahkan) menampilkan hasil `RegistrasiRepository` (grid merah di screenshot — di versi kita isinya data dummy, bukan disensor); bar bawah: filter Dokter/Unit/Periode/Key Word + tombol Simpan/Baru/Ganti/Hapus/Cetak/Semua/Keluar (sebagian besar masih placeholder, "Semua" reload grid, "Keluar" tutup window).
- Tombol "Registrasi" di toolbar `MainWindow` sekarang membuka window ini (sebelumnya placeholder "belum diimplementasikan").
- Bug build sempat muncul: compiled-binding DataGrid butuh `x:DataType` eksplisit (Avalonia 12 compiled bindings default) — diperbaiki dengan `xmlns:data="using:SimrsKhanza.Data"` + `x:DataType="data:RegistrasiListItem"` di elemen `DataGrid`. Juga sempat salah path `StyleInclude` untuk tema DataGrid — ternyata FluentTheme versi ini sudah include DataGrid otomatis, baris itu dihapus.
- Build sukses, 5 unit test tetap pass, app jalan (`dotnet run` dengan DB nyata) tanpa crash.

## Compile .exe standalone — SELESAI (2026-11-03)
`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` → `publish/win-x64/SimrsKhanza.UI.exe` (87.4MB, self-contained, tidak perlu install .NET di komputer target). **Dicoba dijalankan langsung (bukan lewat `dotnet run`) — berhasil, tidak crash.** File `.exe` ini sudah dikirim ke user. Folder `publish/` ditambahkan ke `.gitignore` (binary besar, regenerable via command publish di atas, tidak perlu ikut git).

## Belum dilakukan (menunggu keputusan/aksi user)
- Commit untuk layar Registrasi + .exe — **lihat apakah user minta lanjut besok dulu** (sesuai permintaan "lanjut besok lagi bang").
- **Verifikasi visual oleh user sendiri** — ini prioritas tertinggi, karena sesi ini tidak bisa screenshot desktop app. User sudah dikirimi `.exe` untuk dicoba langsung.
- **MariaDB perlu di-install sebagai service permanen oleh user sendiri** (butuh admin elevation yang sesi ini tidak punya) kalau mau tetap jalan setelah reboot — exe yang dikirim butuh MariaDB jalan di 127.0.0.1:3306 (sik/khanza_dev/DevPassword123!) + env var `SIMRS_DB_*` supaya grid Registrasi terisi; kalau tidak, grid tampil kosong (tidak crash).
- Keputusan arsitektur "formulir dinamis" (gantikan 177 tabel penilaian/skrining/surat) — masih menunggu persetujuan, belum dieksekusi.
- Form input Registrasi (field atas) belum punya logic Simpan/Baru/Ganti/Hapus/Cetak nyata — masih placeholder.
- Shortcut toolbar baris 1 & 2 lainnya (selain Menu & Registrasi) masih placeholder dialog "belum diimplementasikan".
- Tile dashboard selain "Rekam Medis" masih placeholder kosong.
- 1.189 tabel sisanya belum diimpor/didokumentasikan satu per satu — baru 10 tabel inti yang ada di database dev ini.

## Sesi lanjutan 2026-11-05: navigasi single-window + reuse login legacy

### Bug "data tetap kosong" — SUDAH DITEMUKAN PENYEBABNYA & DIPERBAIKI
Dua penyebab sekaligus:
1. **MariaDB mati** — karena dijalankan sebagai proses manual (bukan service), begitu komputer idle/restart prosesnya hilang. Sudah dihidupkan lagi dengan command yang sama (lihat bagian "MySQL lokal" di atas).
2. **Bug di `RegistrasiRepository`**: filter `WHERE tgl_registrasi = HARI INI` hardcoded — padahal data seed ditanggal 2026-11-03, sedangkan hari ini sudah 2026-11-05. Jadi walau DB nyala, query selalu kosong karena tanggalnya nggak pernah cocok.
   - **Diperbaiki**: `GetTodayAsync` sekarang cuma wrapper tipis, repository utama `GetByPeriodeAsync(fromDate, toDate)` menerima rentang tanggal. `RegistrasiView` sekarang default-load 7 hari terakhir (bukan cuma hari ini persis) supaya data seed/demo yang tanggalnya bukan "hari ini pas" tetap muncul.
   - Sempat ada bug kompilasi: parameter `from`/`to` bentrok dengan keyword LINQ query syntax (`from x in ...`) — diganti jadi `fromDate`/`toDate`.

### Navigasi single-window — SELESAI
User minta: klik "Registrasi" jangan buka window baru, lanjut di window yang sama (sesuai perilaku legacy).
- `Modules/Registrasi/RegistrasiWindow` (Window terpisah) **dihapus**, diganti `Modules/Registrasi/RegistrasiView` (**UserControl**).
- `MainWindow` sekarang punya `ContentControl Name="PageHost"` di `ContentHost`, jadi ada 3 "halaman" yang saling eksklusif: Hero (awal) / Menu (grid ikon) / PageHost (fitur aktif, mis. Registrasi). Method `ShowPage(Page.Hero/Menu/Feature)` yang atur switching.
- `RegistrasiView` punya event `BackRequested` — tombol "⬅ Kembali ke Menu" (dulu "Keluar", diganti supaya tidak ambigu dengan keluar aplikasi) memicu event ini, `MainWindow` subscribe dan balik ke Hero.
- Pola ini jadi **template untuk fitur-fitur berikutnya**: tiap fitur baru = 1 UserControl baru + wiring serupa di `MainWindow`, bukan bikin Window baru lagi.

### Jawaban: bisa nggak tabel `petugas`/`user` dipakai login di app baru?
**Bisa, dan sudah diimplementasikan** (`LegacyUserRepository` di `SimrsKhanza.Data/Auth/`) — ini langsung connect ke tabel `user`/`petugas` di database `sik` yang ASLI, jadi tidak perlu migrasi data dulu kalau mau pakai. Tapi ada 2 catatan penting:
1. **Password di tabel `user`/`admin` tersimpan PLAINTEXT** (kolom `text`, tidak ada kolom hash/salt) — dikonfirmasi dari skema. `UserAccount.IsLegacyPlaintext` + percabangan di `AuthService.LoginAsync` menangani ini (banding string langsung, bukan PBKDF2). **Rekomendasi migrasi** (belum diimplementasikan): begitu login lama berhasil, langsung hash passwordnya dengan `PasswordHasher` dan simpan ke tabel BARU milik app baru (jangan tulis balik ke tabel `user` lama) — supaya pelan-pelan semua akun pindah ke hash yang aman tanpa memaksa reset password massal.
2. **Tabel `user` legacy punya ~1.225 kolom, satu kolom per fitur** (bukan desain ternormalisasi) — `LegacyUserRepository` baca ini pakai ADO.NET mentah (`MySqlConnector` langsung, bukan EF Core, karena 1.225 kolom tidak praktis dipetakan jadi entity), lalu ubah tiap kolom bernilai `'true'` jadi satu permission key = nama kolomnya. **Belum dioptimalkan** — scan 1.225 kolom tiap login; desain final disarankan: migrasi satu kali baca tabel lebar ini → tulis ke `UserPermission` (tabel ternormalisasi yang sudah ada), baru pakai itu seterusnya.
3. **Asumsi BELUM diverifikasi**: `LegacyUserRepository` mengasumsikan `user.id_user` = `petugas.nip` (pola umum Khanza, tapi belum dicek langsung ke database produksi asli). Perlu dikonfirmasi ke instalasi `sik` yang sebenarnya.
4. **Belum ditest terhadap DB nyata** — `user` table tidak ada di 10-tabel dev DB kita (dan tidak diseed, karena ~1.225 kolom NOT NULL terlalu berat untuk seed manual). Ada 1 unit test (`Login_WithLegacyPlaintextAccount_ComparesRawStringNotHash`) yang mengetes logic percabangan plaintext di `AuthService` pakai fake repository — bukan test terhadap `LegacyUserRepository` yang sesungguhnya.

Total sekarang: **6 unit test, semua pass**.

## Fitur Rawat Jalan — SELESAI, termasuk WRITE NYATA PERTAMA (2026-11-05)
User pilih "Rawat Jalan" sebagai fitur berikutnya (dari 4 opsi: Rawat Jalan/IGD-UGD/Farmasi/selesaikan Login — alasan direkomendasikan: nyambung langsung ke alur Registrasi yang baru jadi).

- **Tabel legacy `pemeriksaan_ralan` diimpor** ke DB dev (vital sign + catatan klinis: tensi, nadi, suhu, respirasi, tinggi, berat, spo2, gcs, kesadaran, keluhan, pemeriksaan, alergi, rtl, penilaian, instruksi, evaluasi, nip). PK: `no_rawat+tgl_perawatan+jam_rawat`.
- **Entity `PemeriksaanRalan`** (Core) + mapping EF Core, termasuk `HasConversion` custom untuk enum `Kesadaran` (C# enum ↔ string legacy `'Compos Mentis'` dkk — field ini NOT NULL di skema asli jadi tidak bisa di-`Ignore()` seperti enum lain yang hanya dibaca).
- **`RawatJalanRepository`** (Data): `GetQueueAsync(fromDate,toDate)` — daftar antrian pasien Ralan yang BELUM punya baris `pemeriksaan_ralan` (pakai `!examinedNoRawat.Contains(...)`); `SaveExaminationAsync(...)` — **tulis beneran ke DB**, bukan placeholder lagi seperti tombol-tombol di Registrasi.
- **`Modules/RawatJalan/RawatJalanView`** (UserControl, pola sama seperti Registrasi — masuk ke `PageHost` di window yang sama): panel kiri = antrian (klik pasien → form kanan terisi), panel kanan = form pemeriksaan (vital sign + keluhan/pemeriksaan/penilaian/rtl/instruksi/evaluasi), tombol "💾 Simpan Pemeriksaan" nyata — setelah simpan sukses, pasien otomatis hilang dari antrian (reload).
- Perlu seed tambahan: 1 baris `pegawai` (`D0000099`, dipakai sebagai target FK `dokter.kd_dokter` DAN `pemeriksaan_ralan.nip`) — sebelumnya FK ini "bolong" karena diseed pakai `FOREIGN_KEY_CHECKS=0`. NIP dokter pemeriksa saat ini **di-hardcode placeholder** `"D0000099"` di kode (karena `AuthenticatedUser` dev belum terhubung ke record pegawai — lihat bagian `LegacyUserRepository` di atas untuk rencana sambungnya nanti).
- **Integration test baru** (`RawatJalanRepositoryIntegrationTests`) — nulis baris pemeriksaan sungguhan ke DB dev, lalu assert pasien itu hilang dari antrian. **PASS**, dan dikonfirmasi manual lewat query SQL langsung (baris benar-benar ada di tabel, kolom `kesadaran` ter-konversi jadi `"Compos Mentis"` dengan benar).
- **Total sekarang 7 unit test, semua pass** (5 lama + `DokterRepositoryIntegrationTests` + `RawatJalanRepositoryIntegrationTests`).
- Build sukses, app smoke-run tanpa crash, `.exe` di-republish dan dikirim ulang ke user.

## Fitur Pasien (Identitas Pasien) — SELESAI, dicocokkan ke source asli (2026-11-05)
User minta "cicil tampilan-tampilan" + dicocokkan ke source code. Sebelum membangun, saya baca langsung `src/simrskhanza/DlgPasien.form` (NetBeans form XML, ~4.900 baris) dari repo legacy via sub-agent, supaya field & urutannya cocok dengan aplikasi asli, bukan tebakan.

- **Field "Identitas Pasien" inti** dibangun persis sesuai form asli: No.RM (auto), Nama, J.K. (radio), Gol.Darah, Tempat/Tgl Lahir, Agama, Stts.Nikah, Alamat, No.Telp, Pekerjaan, No.KTP, Email, Cara Bayar (Penjab), Nama Ibu, plus blok Penanggung Jawab (Hub.PJ/Nama PJ/Pekerjaan PJ/Alamat PJ).
- **Sengaja TIDAK diport dulu (v1, didokumentasikan jelas di source code)**: panel BPJS VClaim/PCare/Dukcapil/SatuSehat, ~15 shortcut cetak surat rekam medis, sub-panel TNI/POLRI, dan 4 picker cascading kelurahan/kecamatan/kabupaten/propinsi (karena butuh data wilayah lengkap, bukan cuma 1 baris placeholder). Kolom-kolom DB untuk field yang dideferred ini tetap ada di entity dengan nilai placeholder (`"-"`/`0`) supaya insert tetap sukses sesuai constraint asli.
- **Tabel lookup tambahan diimpor**: `kelurahan`, `kecamatan`, `kabupaten`, `propinsi`, `suku_bangsa`, `bahasa_pasien`, `cacat_fisik`, `perusahaan_pasien` — masing-masing diseed 1 baris placeholder `id=0`/`"-"`. **Sempat salah**: insert literal `0` ke kolom `AUTO_INCREMENT` otomatis di-override MySQL jadi nilai sequence berikutnya (bukan 0 beneran) — diperbaiki pakai `SET sql_mode=...,NO_AUTO_VALUE_ON_ZERO`.
- **Bug kedua yang ditemukan**: EF Core bikin shadow FK sendiri (`PenjabKodePenjab`) karena relasi `Pasien.Penjab` belum dideklarasikan eksplisit di `OnModelCreating` — insert gagal "Unknown column". Diperbaiki dengan `HasOne(p => p.Penjab).WithMany().HasForeignKey(p => p.KodePenjab)`.
- **`PasienRepository`**: `SearchAsync` (nama/No.RM), `NextNoRekamMedisAsync` (auto-generate pola `D00001` dst, sama seperti seed), `SaveNewAsync` — **real INSERT**.
- **`Modules/Pasien/PasienView`** — wired ke tile dashboard "Rekam Medis" (sebelumnya demo "Cari Dokter", sekarang jadi fitur Pasien yang sesungguhnya — lebih make sense karena "Rekam Medis" ya memang soal data pasien). Form kiri + grid cari kanan, tombol Simpan (nyata)/Baru (clear+generate No.RM baru).
- **Integration test baru** (`PasienRepositoryIntegrationTests`) — insert pasien baru sungguhan, lalu cari lagi, assert data + enum (JenisKelamin) benar tersimpan. **PASS**, dikonfirmasi juga via query SQL langsung.
- **Total sekarang 8 unit test, semua pass.** Build sukses, app smoke-run tanpa crash, `.exe` di-republish & dikirim ulang.
- **Catatan operasional**: MariaDB sempat mati lagi di tengah sesi ini (kedua kalinya, karena bukan service) — dihidupkan ulang dengan command yang sama seperti biasa. **Rekomendasi kuat**: kalau mau kerja lanjut tanpa gangguan ini terus berulang, install MariaDB sebagai Windows Service permanen (butuh PowerShell as Administrator, caranya ada di bagian atas handover ini).

## Sesi 2026-11-06: fix Registrasi, rename Labgrat, menu Pengaturan admin-only

### Bug grid Registrasi "tidak tampil" — DIPERBAIKI
User klarifikasi: bagian bawah layar Registrasi seharusnya tampil **semua** registrasi (bukan dibatasi tanggal). Sebelumnya default filter "7 hari terakhir" bisa menyembunyikan data. **Diperbaiki**: `RegistrasiView` sekarang default TANPA filter tanggal — tampil semua registrasi begitu dibuka; Periode cuma jadi filter opsional yang dipakai lewat tombol "Cari / Semua". (Penyebab lain yang berulang: MariaDB mati lagi karena bukan service — dihidupkan ulang, ketiga kalinya dalam proyek ini. Lihat rekomendasi install sebagai service permanen di bagian atas.)

### Label "Labgrat" → "Laboratorium" — DIPERBAIKI
Typo di toolbar `MainWindow` sudah diperbaiki.

### Menu Pengaturan (admin-only) — SELESAI
- **`DatabaseSettingsStore`** (Config) — baru: menyimpan `DatabaseSettings` ke file JSON lokal di `%APPDATA%\SimrsKhanza\database.settings.json` (BUKAN di repo, tidak pernah ikut git). `DatabaseSettings.Load()` — method baru, prioritas: file tersimpan → environment variable → default. **Semua pemanggilan di UI** (Registrasi/RawatJalan/Pasien/CariDokterDemo) diganti dari `FromEnvironment()` ke `Load()` supaya pengaturan yang disimpan user beneran dipakai. File test tetap pakai `FromEnvironment()` (supaya env var test tidak ketimpa file lokal yang mungkin ada).
- **`Modules/Pengaturan/PengaturanView`** — form Host/Port/Database/User/Password + tombol "Tes Koneksi" (cek `CanConnectAsync()` beneran) dan "Simpan" (tulis ke file). Bagian "Identitas & Aset" didokumentasikan sebagai belum diimplementasikan (merujuk isi `setting/` legacy: `logo.jpg`, `wallpaper.jpg`, `modem.xml` utk gateway SMS, config fingerprint presensi).
- **Akses admin-only**: tile "Pengaturan" ditambah ke `DefaultMenuCatalog` dengan `RequiredPermission = "pengaturan.admin"` — otomatis TIDAK muncul di dashboard untuk user yang tidak punya permission ini. `MainWindow.OpenPengaturanAsync` juga cek ulang permission secara eksplisit (defense in depth) sebelum membuka layar, bukan cuma mengandalkan tile disembunyikan.
- **2 akun dev-seed sekarang**: `admin`/`admin123` (device PUNYA `pengaturan.admin`) dan `petugas`/`petugas123` (TIDAK punya — buat tes gate ini beneran jalan).
- **2 unit test baru** (`MenuPermissionServiceTests`) — admin lihat tile Pengaturan, petugas tidak. **Total sekarang 10 unit test, semua pass.**
- Build sukses, app smoke-run tanpa crash.

## Sesi 2026-11-06 (lanjutan): verifikasi koneksi DB asli + grid "Daftar Pasien Rawat Jalan" lengkap

### Verifikasi koneksi ke database ASLI (bukan dev seed) — DIKONFIRMASI BERHASIL
User set sendiri koneksi ke database produksi/asli lewat layar Pengaturan (tanpa kasih tahu detailnya ke saya — sengaja, demi privasi). Screenshot yang dikirim menunjukkan grid Registrasi/Rawat Jalan terisi data dengan pola yang JELAS BUKAN seed dev saya (kode dokter `DUNDY01`/`erdananda...`, No.RM `20262613`, tanggal aktif `2026/11/06`). **Ini bukti kuat `DatabaseSettings.Load()` + `DatabaseSettingsStore` (dibangun sesi sebelumnya) bekerja general** — bukan hardcode ke DB dev manapun. Konfirmasi: arsitektur koneksi DB di rewrite ini SUDAH BISA dipakai ke database SIMRS Khanza yang sesungguhnya, asal strukturnya cocok dengan skema yang sudah dipetakan (10 tabel inti + `pemeriksaan_ralan` + 8 tabel lookup).

### Grid "Daftar Pasien Rawat Jalan" — DISELARASKAN ke screenshot kedua user
User kirim screenshot kedua (`:: [ Daftar Pasien Rawat Jalan ] ::`) dengan kolom jauh lebih lengkap dari yang saya buat sebelumnya: Kode Dokter, Dokter Dituju, No.RM, Pasien, Poliklinik, Penanggung Jawab, Alamat P.J., Hubungan P.J., Biaya Reg, Jenis Bayar, Status, No.Rawat, Tanggal, Jam, No.Reg, Status Bayar, Stts Poli, No.Telp Pasien — plus search row (No.Rawat/No.Reg/No.R.M./Nama Pasien) dan 2 tab ("Registrasi Awal"/"Rujukan Internal Poli"). Ternyata ini layar YANG SAMA dengan Registrasi yang sudah dibuat (cuma scroll/kolom beda) — jadi **di-upgrade di tempat**, bukan dibuat screen duplikat:
- `RegPeriksa` entity diperluas: `NoReg`, `StatusPoli`, `StatusBayar`, `PenanggungJawab`, `AlamatPj`, `HubunganPj`, `BiayaReg` + enum `Status`/`StatusLanjut` yang sebelumnya `Ignore()` sekarang benar-benar dipetakan (conversion ke string legacy persis, misal `'Sudah Bayar'`).
- `RegistrasiRepository` diperluas: join tambahan ke `Penjab` (buat kolom "Jenis Bayar"), proyeksi 17 kolom sesuai screenshot, plus `RegistrasiSearchFilter` (No.Rawat/No.Reg/No.RM/Nama) buat search row baru.
- **Bug ketemu & diperbaiki lewat test baru**: `enum.ToString()` menghasilkan nama C# (`"SudahBayar"`), BUKAN string legacy (`"Sudah Bayar"`) — integration test baru (`RegistrasiRepositoryIntegrationTests`) langsung menangkap ini sebelum saya kirim ke user. Diperbaiki dengan mapping eksplisit `ToDisplay(...)`.
- UI: tambah search row + `TabControl` ("Registrasi Awal" = grid lengkap yang berfungsi; "Rujukan Internal Poli" = placeholder, didokumentasikan belum diimplementasikan).
- **Total sekarang 11 unit test, semua pass.**

### Catatan teknis: proses yang masih jalan mengunci build
Sempat gagal build karena app yang saya `Start-Process` di akhir sesi sebelumnya (dibiarkan terbuka sesuai permintaan user) mengunci file DLL. **Pelajaran untuk sesi berikutnya**: sebelum `dotnet build`, selalu cek & matikan proses `SimrsKhanza.UI` yang mungkin masih jalan dari sesi sebelumnya (`Get-Process -Name "SimrsKhanza.UI" | Stop-Process -Force`), baru build, baru jalankan lagi di akhir.

## Sesi 2026-11-06 (lanjutan ke-2): fix mapping menu, konfirmasi dukungan IP/host berbeda, cetak PDF nyata

### Koreksi mapping menu — DIPERBAIKI
User koreksi: screenshot "Daftar Pasien Rawat Jalan" itu diakses dari tombol **"Rawat Jalan"**, bukan "Registrasi" seperti asumsi saya sebelumnya. Diperbaiki: kedua tombol toolbar ("Registrasi" & "Rawat Jalan") sekarang sama-sama membuka `RegistrasiView` (layar yang sama). Form pemeriksaan vital sign (`RawatJalanView`, yang sebelumnya dibuka langsung dari tombol "Rawat Jalan") sekarang diakses via tombol baru **"🩺 Periksa"** di dalam `RegistrasiView` — alurnya jadi lebih natural: buka daftar pasien → pilih → periksa. `RawatJalanView.BackRequested` sekarang balik ke `RegistrasiView` (bukan ke Hero), supaya alur baliknya masuk akal.

### Konfirmasi: bisa connect ke IP/host database berbeda — YA, SUDAH BISA
Dijawab langsung (tanpa perlu kode baru, karena memang sudah didesain begitu): field "Host" di layar Pengaturan menerima teks bebas (IP address, hostname, FQDN apa saja), tidak di-hardcode. Ini sudah dibuktikan user sendiri sesi sebelumnya (connect ke DB asli mereka). Catatan praktis yang disampaikan ke user: kalau DB target ada di komputer/jaringan lain, pastikan MySQL/MariaDB di sana mengizinkan remote connection (bind-address bukan cuma 127.0.0.1, user MySQL punya akses dari host `%` atau IP klien, firewall port 3306 terbuka) — ini aspek konfigurasi server DB-nya, bukan sesuatu yang perlu diubah di kode aplikasi.

### Cetak Laporan (PDF) — fitur nyata pertama, SELESAI
- Package **QuestPDF** ditambahkan ke `SimrsKhanza.UI`. **Catatan lisensi**: QuestPDF Community gratis hanya untuk organisasi di bawah threshold revenue tertentu — PERLU DICEK ke questpdf.com/license sebelum dipakai di deployment produksi RS yang lebih besar, atau beli lisensi komersial.
- `Modules/Registrasi/RegistrasiReport.cs` — generate PDF beneran (A4 landscape, tabel 10 kolom) dari data grid Registrasi yang sedang tampil, disimpan ke temp file, langsung dibuka dengan PDF viewer default OS (`Process.Start` + `UseShellExecute=true`).
- Tombol "🖨 Cetak" di `RegistrasiView` sekarang nyata (sebelumnya placeholder) — kalau grid kosong, tetap kasih pesan "belum ada data" alih-alih generate PDF kosong.
- **Test baru** (`RegistrasiReportTests`) — generate PDF beneran, assert file ada, ukurannya wajar (>500 byte), dan punya PDF magic header `%PDF`. **PASS**.
- Ini jadi **pola/contoh untuk port fitur cetak lainnya** (masih ada ratusan `.jrxml` legacy yang belum di-port — lihat `report/` di repo legacy).
- **Total sekarang 12 unit test, semua pass.**

### Restrukturisasi HANDOVER.md — SELESAI
Ditambahkan bagian **"🚀 MULAI DARI SINI"** di paling atas dokumen (sebelum kronologi) — ringkasan cepat, cara jalanin app, daftar fitur yang sudah jalan, pola arsitektur wajib diikuti, daftar gotcha, dan prioritas kerja selanjutnya. Tujuannya supaya AI/sesi lain (user bilang akan pindah chat) bisa langsung paham tanpa baca kronologi panjang di bawah dari awal. Kronologi detail tetap dipertahankan di bawah TL;DR untuk konteks/alasan keputusan.

## Catatan untuk sesi besok (sesuai permintaan user "lanjut besok lagi")
- Lanjutkan dari sini: layar Registrasi sudah ada tapi baru "list + toggle", belum ada Simpan/Baru/Ganti/Hapus nyata.
- Cek dulu apakah MariaDB masih jalan (`Get-Process -Name mariadbd`) sebelum lanjut kerja — kalau sudah mati (misal komputer di-restart), jalankan ulang dengan perintah yang ada di bagian "MySQL lokal" di atas.
- Baca ulang bagian "Belum dilakukan" di atas sebagai starting point.
- Belum ada commit/push ke git.
- Belum mulai rewrite/implementasi apa pun (sesuai instruksi).
- Belum menyentuh isi detail `sik.sql` / PDF pasien / gambar tanda tangan pasien secara substantif — hanya dicatat keberadaannya demi privasi.

## Belum dilakukan / dengan sengaja ditunda
- TIDAK ada file yang dihapus atau diubah.
- TIDAK ada rewrite/implementasi baru dimulai (sesuai instruksi user).
- Belum menyentuh isi `sik.sql` / file PDF pasien secara detail (hanya dicatat eksistensinya) — demi privasi data pasien.

## File deliverable yang sedang disiapkan (di scratchpad, belum ditaruh di repo)
- `HANDOVER.md` (dokumen ini)
- `AUDIT_REPORT.md` (belum dibuat — menunggu hasil sub-agent)

## Catatan untuk sesi berikutnya
- Jangan taruh file audit langsung ke dalam repo produksi tanpa izin user — tanyakan dulu lokasi penyimpanan yang diinginkan (misal folder `_docs/audit/` baru, atau cukup dikirim sebagai file terpisah).
- Jangan commit apa pun ke git tanpa diminta.
