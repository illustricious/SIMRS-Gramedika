# Handover — Audit Repo SIMRS Khanza (menuju rewrite C# Avalonia)

Dokumen ini diupdate setiap ada progres. Jangan dihapus — ini adalah log kerja agar sesi berikutnya (termasuk AI/sesi chat lain) bisa lanjut tanpa mengulang dari awal.

---

# 🚀 MULAI DARI SINI — Ringkasan Cepat (TL;DR)

## Status terkini — 19 November 2026

- Katalog Avalonia mencakup 1.224 tile. Sebanyak 200 tile membuka layar, termasuk **34 dari 34 tile Menu A**, 16 dari 16 tile B, 61 dari 61 tile C, dan 75 dari 75 tile D. Deposit Pasien juga muncul di kategori I dan K.
- Penutupan sembilan tile A mencakup pemesanan dan masuk kamar, jadwal operasi, sesi KFR, pengeluaran dan pembalikan stok resep pulang, pemberian diet, dua formulir skrining, deposit dan jurnalnya, serta estimasi tarif ranap. Cakupan per layar dan pekerjaan lanjutan tercatat di [MENU_A_STATUS.md](MENU_A_STATUS.md).
- Ringkasan dan riwayat di bawah merekam keadaan saat masing-masing sesi ditulis. [README proyek](../../README.md) memuat hitungan dashboard saat ini.

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
- **Rawat Inap** (`Modules/RawatInap`, dibuka dari toolbar shortcut "Rawat Inap") — admit pasien ke kamar kosong + pulangkan (hitung Lama & Biaya Kamar otomatis), **real write** ke `kamar_inap`/`kamar`/`reg_periksa` — **v1 belum ditest interaktif, lihat catatan sesi 2026-11-07**.
- **Kasir** (`Modules/Kasir`, dibuka dari tile dashboard "Keuangan") — cari tagihan by No.Rawat (biaya reg + biaya kamar), tandai lunas — **real write** ke `reg_periksa.status_bayar`.
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

## Sesi lanjutan 2026-11-07: diagnosis "data pasien tidak tampil" + fitur Rawat Inap & Kasir

### Diagnosis: grid Registrasi "data pasien tidak tampil" — SEBAB DITEMUKAN

### Fitur Rawat Inap + Kasir/Billing — SELESAI (v1, real write, belum ditest ke DB asli)
User pilih ini sebagai prioritas lanjutan (dari 3 opsi yang ditawarkan: Rawat Inap+Kasir / Lab-Radiologi / perluas cetak PDF).

- **Core entities diperluas** (`SimrsKhanza.Core/Entities/MasterData.cs`): `Kamar.Status`/`Kelas` sekarang benar2 dipetakan (sebelumnya `Ignore()`) via `HasConversion` string legacy persis (`"ISI"/"KOSONG"/...`, `"Kelas 1"/"Kelas VIP"/...`). `KamarInap` ditambah `TarifKamar`, `Lama`, `TotalBiaya`, `StatusPulang` (enum baru) — field yang sebelumnya tidak ada sama sekali di entity meski kolomnya ada di skema legacy (`trf_kamar`, `lama`, `ttl_biaya`, `stts_pulang`).
  - **Catatan skema penting**: `kamar_inap.stts_pulang` adalah `NOT NULL` tanpa default di skema asli — direpresentasikan sebagai `StatusPulang.BelumPulang` yang di-convert ke string `"-"` (salah satu nilai enum legacy sendiri), dipakai di `AdmitAsync` supaya insert tidak gagal constraint sebelum pasien benar-benar dipulangkan.
- **`RawatInapRepository`** (`SimrsKhanza.Data/RawatInapRepository.cs`) — baru:
  - `GetAvailableKamarAsync()` — kamar dengan `Status == Kosong`.
  - `GetActiveInpatientsAsync()` — join `kamar_inap` (`tgl_keluar IS NULL`) + `reg_periksa` + `pasien` + `bangsal`.
  - `AdmitAsync(noRawat, kodeKamar, diagnosaAwal)` — **real INSERT** `kamar_inap` + set `kamar.Status = Isi` + set `reg_periksa.StatusLanjut = RawatInap`. Mensyaratkan `reg_periksa` untuk `noRawat` itu sudah ada (pasien harus registrasi dulu lewat Registrasi/Rawat Jalan).
  - `DischargeAsync(noRawat, statusPulang, diagnosaAkhir)` — stamp `tgl_keluar`/`jam_keluar`, hitung `Lama` (hari, minimal 1) dan `TotalBiaya` (`Lama * TarifKamar`), bebaskan kamar (`Status = Kosong`). **Real UPDATE**, return nilai biaya kamar yang dihitung.
- **`KasirRepository`** (`SimrsKhanza.Data/KasirRepository.cs`) — baru:
  - `GetBillAsync(noRawat)` — gabung `reg_periksa.biaya_reg` + `SUM(kamar_inap.ttl_biaya)` untuk `noRawat` itu (biasanya 1 baris, tapi dijaga untuk kasus "Pindah Kamar" yang bisa punya >1 baris kamar_inap per no_rawat) → total tagihan + status bayar saat ini.
  - `MarkPaidAsync(noRawat)` — **real UPDATE** `reg_periksa.status_bayar = 'Sudah Bayar'`.
- **UI baru**:
  - `Modules/RawatInap/RawatInapView` — panel kiri: daftar pasien sedang dirawat; panel kanan: form "Admit Pasien Baru" (No.Rawat + pilih kamar kosong + diagnosa awal) dan form "Pulangkan Pasien Terpilih" (pilih dari grid kiri, pilih status pulang + diagnosa akhir, hitung & tampilkan biaya kamar). Dibuka dari toolbar shortcut **"Rawat Inap"** (sebelumnya placeholder).
  - `Modules/Kasir/KasirView` — cari tagihan by No.Rawat, tampilkan breakdown (biaya reg + biaya kamar + total) + status bayar, tombol "Tandai Lunas" (real write). Dibuka dari tile dashboard **"Keuangan"** (`moduleKey == "keuangan"`, sebelumnya placeholder "belum diimplementasikan").
- **Build sukses (0 error), 12 unit test lama tetap PASS.** `.exe` di-publish ulang.
- **BELUM dilakukan** (butuh keputusan/aksi user sebelum dipakai produksi):
  - **Belum ada integration test baru** untuk `RawatInapRepository`/`KasirRepository` (pola test lama butuh DB dev lokal yang kosong aman untuk ditulisi — MariaDB lokal sedang tidak jalan saat sesi ini; menulis integration test yang nge-hit DB **asli** tidak dilakukan, sesuai instruksi user "jangan sampai mengubah data pasien yang asli"). Rekomendasi sesi depan: nyalakan MariaDB dev lokal dulu, baru tulis test di sana.
  - **Belum diverifikasi visual/interaktif** (klik tombol admit/pulangkan/bayar beneran) — sesi ini tidak punya tool screenshot/klik desktop app. User disarankan coba `.exe` yang baru dipublish dan admit/pulangkan **1 pasien dummy/test** dulu (bukan pasien asli) untuk verifikasi sebelum dipakai ke data sungguhan.
  - Form "Admit" masih manual ketik No.Rawat (belum ada picker pencarian pasien dari daftar Registrasi) — cukup untuk v1, bisa diperbaiki nanti.
  - Lama inap dihitung dari `DateTime.Now` saat tombol "Pulangkan" diklik (bukan field tanggal keluar yang bisa diedit manual) — kalau RS butuh catat tanggal keluar yang beda dari waktu input data, ini perlu ditambah field date picker terpisah.
  - Kolom `kamar.status`/`kamar.kelas` di skema asli **nullable**, tapi property C# `Kamar.Status`/`Kelas` non-nullable — kalau ada baris kamar asli dengan nilai NULL di salah satu kolom ini, baris itu akan gagal dibaca EF Core (exception, bukan crash — tertangkap di try/catch UI jadi list kosong/parsial). Belum diverifikasi apakah data kamar asli punya baris NULL seperti ini.

## Sesi lanjutan 2026-11-07 (lanjutan ke-2): akar masalah lag + "data tidak tampil" — DITEMUKAN & DIPERBAIKI

User kirim screenshot Khanza asli ("Registrasi Periksa Hari Ini", Periode 05-11 s.d. 06-11-2026, Record: 167) dan laporkan: grid rewrite masih belum nampilin data + aplikasi jadi lag tiap pindah fitur, SETELAH publish ulang di sesi sebelumnya.




**Catatan untuk sesi berikutnya**: kalau mau menambah filter/list baru yang query langsung dari tabel besar (`reg_periksa` 169k+, `pasien` 59k+), JANGAN biarkan default query tanpa batas (tanggal/keyword/`.Take()`) — selalu beri default yang wajar atau cap jumlah baris, baru biarkan user melebarkan secara eksplisit.

## Sesi lanjutan 2026-11-07 (lanjutan ke-3): tabel DataGrid tidak kelihatan sama sekali — DITEMUKAN & DIPERBAIKI

Setelah fix "lag" di atas, user lapor: performa sudah bagus ("Record : 2" muncul benar), tapi **area tabel di bawah tab "Registrasi Awal" kosong blank** — tidak ada header kolom, tidak ada baris, padahal data sudah ke-load (tombol Cetak berhasil generate PDF dari data yang sama, jadi datanya memang ada di memori).

**Akar masalah**: `App.axaml` TIDAK meng-include tema/style `DataGrid` (`Avalonia.Controls.DataGrid` adalah package terpisah dari `FluentTheme` — control-nya pakai template sendiri yang harus di-`StyleInclude` manual). Sesi 2026-11-03 sempat menambahkan include ini lalu MENGHAPUSNYA dengan asumsi salah "FluentTheme versi ini sudah include DataGrid otomatis" (lihat catatan "Layar Registrasi — SELESAI" di atas) — asumsi itu ternyata salah, dan karena waktu itu testingnya cuma smoke-run tanpa klik manual, masalah tidak ketahuan sampai sekarang.

**Diperbaiki**: `App.axaml` → `<Application.Styles>` ditambah `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml" />` (path yang benar — percobaan pertama pakai akhiran `.axaml` gagal dengan `AVLN2000: Unable to resolve XAML resource`, yang benar adalah `.xaml`). Build sukses, 12 test tetap pass, `.exe` di-publish ulang.

**Catatan untuk sesi berikutnya**: ini kedua kalinya DataGrid bermasalah gara-gara asumsi soal styling tanpa verifikasi visual. **Build sukses + test pass TIDAK membuktikan UI benar-benar tampil** — begitu ada tool screenshot/klik desktop tersedia di sesi manapun, jadikan prioritas untuk verifikasi visual tiap kali menyentuh `App.axaml`/tema/DataGrid.

## Rebranding tampilan ke "SIMRS Gramedika" — SELESAI (2026-11-07)
User minta ganti nama tampilan dari "SIMRS Khanza" jadi **"SIMRS Gramedika"** (konsisten dengan referensi warna RS Gramedika 10 yang sudah dipakai sejak awal untuk palet warna). Diganti di: `MainWindow.axaml` (Title window + teks hero/welcome panel "SIMRS GRAMEDIKA" + teks status bar kanan bawah), `LoginWindow.axaml` (Title window). Nama internal project/namespace (`SimrsKhanza.*`, nama file .sln/.csproj) **TIDAK diubah** — itu identitas teknis proyek, bukan branding yang user lihat; ganti nama project itu sendiri kalau diminta eksplisit, scopenya lebih besar (rename semua namespace, file, referensi).

## Fitur Laboratorium + Radiologi — SELESAI (v1, real write, belum ditest ke DB asli) — 2026-11-07

User pilih ini sebagai prioritas lanjutan berikutnya.

- **Core entities baru** (`SimrsKhanza.Core/Entities/Penunjang.cs`): `PermintaanLab`, `PermintaanRadiologi` (sama persis strukturnya, map ke `permintaan_lab`/`permintaan_radiologi`), `HasilLab` (map ke `saran_kesan_lab` — field `saran`/`kesan`, dipakai sebagai hasil naratif v1), `HasilRadiologi` (map ke `hasil_radiologi` — satu field `hasil` text, sudah sesederhana itu di skema asli).
  - **Keputusan scope penting**: skema lab asli jauh lebih kompleks dari ini (`detail_periksa_lab` = 1 baris per parameter pemeriksaan dengan nilai + nilai rujukan + tarif per kategori PA/PK/MB/kelas/penjab, `periksa_lab` = record billing terpisah). v1 ini **SENGAJA tidak port itu** — pakai `saran_kesan_lab` (kesan/saran naratif, 2 kolom teks) sebagai hasil lab v1, supaya ada fitur nyata yang jalan dulu, bukan separuh-jadi. Radiologi kebetulan sudah sesederhana itu di skema aslinya (`hasil_radiologi` cuma 1 kolom teks).
  - `permintaan_lab`/`permintaan_radiologi.status` tersimpan **lowercase** (`'ralan'/'ranap'`) — beda dari `reg_periksa.status_lanjut` (`'Ralan'/'Ranap'`) dan `kamar_inap`/`periksa_radiologi` (`'Ralan'/'Ranap'` juga tapi kapital) — tiga casing berbeda untuk konsep yang sama di skema legacy, jadi dibuatkan enum + konversi sendiri (`StatusKunjunganPenunjang`), tidak reuse `StatusLanjut`.
  - `noorder` (PK `permintaan_lab`/`permintaan_radiologi`) BUKAN kolom auto-increment — digenerate manual (timestamp + 2 digit random, lihat `PenunjangRepository.GenerateNoOrder`). Skema penomoran asli Khanza belum di-reverse-engineer; cukup unik untuk throughput aplikasi ini, revisit kalau ada kebutuhan proteksi collision lintas-proses.
- **`PenunjangRepository`** (`SimrsKhanza.Data/PenunjangRepository.cs`) — baru: `GetAntrianLabAsync`/`GetAntrianRadiologiAsync` (permintaan yang BELUM punya baris hasil, **disederhanakan dikunci by `no_rawat` saja, bukan `noorder`** — lihat catatan di kode, kalau 1 kunjungan punya 2 permintaan jenis sama, keduanya hilang bareng dari antrian begitu salah satu dapat hasil — cukup untuk v1, 1 kunjungan 1 permintaan adalah kasus umum), `CreatePermintaanLabAsync`/`CreatePermintaanRadiologiAsync` (**real INSERT**), `SaveHasilLabAsync`/`SaveHasilRadiologiAsync` (**real INSERT**).
- **UI baru**: `Modules/Penunjang/PenunjangView` — satu layar, 2 tab (Laboratorium/Radiologi), tiap tab: antrian menunggu hasil (kiri) + form "Buat Permintaan Baru" & "Input Hasil" (kanan). Dibuka dari toolbar shortcut **"Laboratorium"** (buka tab Lab) dan **"Radiologi"** (buka tab Radiologi) — sebelumnya keduanya placeholder.
- **Build sukses (0 error), 12 test lama tetap PASS.** `.exe` di-publish ulang.
- **BELUM dilakukan**:
  - Belum ada integration test baru (alasan sama seperti Rawat Inap/Kasir — MariaDB dev lokal tidak jalan saat sesi ini, dan sengaja tidak menulis test yang nge-hit DB produksi asli).
  - **Belum diverifikasi visual/interaktif** — coba dulu kirim permintaan + input hasil untuk **1 pasien test**, bukan pasien asli, sebelum dipakai ke data sungguhan.
  - Form "Dokter Perujuk" masih ketik manual kode dokter (belum ada picker `SearchPickerDialog` ke tabel `dokter`).
  - Struktur hasil lab per-parameter (`detail_periksa_lab`) dan alur pengambilan sampel (`tgl_sampel`/`jam_sampel` sebagai langkah terpisah) belum diimplementasikan — v1 ini murni "permintaan → hasil naratif", bukan replikasi penuh workflow lab RS.

## Sesi lanjutan 2026-11-07 (lanjutan ke-4): polish UI + bugfix setelah feedback user

User kirim screenshot menu Laboratorium Khanza ASLI yang ternyata punya 6 sub-shortcut (Permintaan Lab PK/PA/PA/MB, Periksa Lab PK/PA/MB — kategori PK/PA/MB = Patologi Klinik/Anatomi/Mikrobiologi) — jauh lebih granular dari `PenunjangView` v1 yang dibuat sesi sebelumnya (1 layar gabungan Lab+Radiologi, tanpa breakdown kategori). **BELUM diputuskan/dikerjakan** — dicatat di sini sebagai perbedaan yang diketahui, menunggu keputusan user apakah mau dipecah jadi per-kategori PK/PA/MB atau cukup yang sekarang. Request lain dari sesi ini (semua sudah dikerjakan):

1. **Login page direstyle + rebranding** — `LoginWindow.axaml` sebelumnya hardcoded warna gelap (`#2E4057`) lepas dari palet tema aplikasi (`App.axaml`), teks masih "SIMRS KHANZA". Diganti: card putih rounded di tengah gradient biru (`#1E3A8A`→`#172B66`, sama dengan `ThemePrimaryBrush`/`ThemePrimaryDarkBrush`), ikon 🏥, teks "SIMRS GRAMEDIKA" + subjudul, tombol biru. Nama control (`UserIdBox`/`PasswordBox`/`ErrorText`/`LoginButton`) TIDAK diubah supaya `LoginWindow.axaml.cs` tetap kompatibel.
2. **Grid Registrasi "kebesaran"** — lebar kolom fixed sebelumnya (90-160px, total ~1850px) terlalu lebar, bikin scroll horizontal terus-terusan. Dipersempit semua kolom (55-130px, proporsional ke isi), ditambah `CanUserResizeColumns="True"` (user masih bisa lebarkan manual kalau perlu) + `HorizontalScrollBarVisibility="Auto"`.
3. **Bug "Input Data harus diklik dulu" — DIPERBAIKI**: panel "Input Data" di Registrasi seharusnya terbuka otomatis saat layar dibuka (toggle default `IsChecked="True"`), tapi visibility Border-nya cuma pernah di-set di handler `OnToggleInputData` (hanya jalan saat diklik) — tidak pernah disinkronkan ke state awal toggle saat konstruksi. Diperbaiki: constructor `RegistrasiView` sekarang eksplisit set `InputDataPanel.IsVisible` dari `InputDataToggle.IsChecked` saat start.

Build sukses, 12 test tetap pass. `.exe` di-publish ulang (sempat gagal sekali percobaan pertama — kemungkinan file masih dikunci instance app lama yang masih terbuka; percobaan kedua langsung sukses tanpa perlu intervensi lain. **Catatan**: selalu tutup dulu `.exe` yang sedang jalan sebelum minta publish ulang, supaya tidak perlu retry).

## Lab dipecah jadi PK/PA/MB — SELESAI (2026-11-07)

User konfirmasi mau menu Laboratorium dipecah per kategori (PK/PA/MB), bukan 1 tab gabungan. Diimplementasikan:
- **`KategoriLab` enum baru** (`Core/Entities/Penunjang.cs`): `PatologiKlinik`/`PatologiAnatomi`/`Mikrobiologi`. **Catatan penting**: tabel `permintaan_lab` di skema asli TIDAK punya kolom kategori sama sekali (kategori sebenarnya ada di `jns_perawatan_lab.kategori`/`periksa_lab.kategori`, tabel katalog/billing yang belum diimplementasikan). Jadi kategori di-tag via **prefix teks `"[PK]"`/`"[PA]"`/`"[MB]"`** di awal kolom `informasi_tambahan` (varchar(60), sudah ada di skema, dipakai ulang) — bukan perubahan skema. Ini workaround yang didokumentasikan jelas, bukan port 1:1 dari sistem kategori asli.
- `PenunjangRepository.GetAntrianLabAsync` sekarang terima parameter `kategori`, filter `InformasiTambahan.StartsWith(tag)`. `CreatePermintaanLabAsync` terima `kategori`, otomatis prepend tag saat insert.
- **UI**: tab "Laboratorium" tunggal dipecah jadi 3 tab — **"Lab PK"**, **"Lab PA"**, **"Lab MB"** — masing-masing isinya sama persis (antrian + buat permintaan + input hasil), cuma data-nya terpisah per kategori. Tab "Radiologi" tetap 1 (sudah sesederhana aslinya, tidak ada sub-kategori di skema `permintaan_radiologi`). Jadi total 4 tab di `PenunjangView`: index 0=Lab PK, 1=Lab PA, 2=Lab MB, 3=Radiologi — `MainWindow.OpenPenunjangAsync` diupdate (tombol toolbar "Laboratorium" buka tab 0, "Radiologi" buka tab 3).
- Build sukses, 12 test tetap pass, `.exe` di-publish ulang.
- **Catatan untuk sesi berikutnya**: kalau nanti mau port kategori secara "benar" (pakai `jns_perawatan_lab`/`periksa_lab.kategori` asli, bukan text-tag workaround ini), itu berarti harus bangun juga katalog jenis pemeriksaan + tarif per kategori — pekerjaan jauh lebih besar, belum ada rencana kapan dikerjakan.

## Sesi lanjutan 2026-11-07 (lanjutan ke-5): UI scale down + Lab jadi submenu 6-tile nyata — 2026-11-07

User klarifikasi maksud "kebesaran" sebelumnya: bukan cuma lebar kolom grid, tapi seluruh UI (font, padding, dll) dirasa besar — maunya seperti efek "Ctrl+scroll zoom-out" di Chrome. Juga klarifikasi struktur Lab: bukan 3 tab gabungan (Permintaan+Periksa jadi satu per kategori), tapi persis seperti screenshot legacy — 1 grid 6-ikon (Permintaan Lab PK/PA/MB + Periksa Lab PK/PA/MB), tiap ikon buka layar sendiri-sendiri.

### Scale UI ~15% lewat `LayoutTransformControl` — SELESAI
`MainWindow.axaml` root `Grid` sekarang dibungkus `<LayoutTransformControl><LayoutTransformControl.LayoutTransform><ScaleTransform ScaleX="0.85" ScaleY="0.85"/></...></LayoutTransformControl>`. Ini BUKAN ubah tiap `FontSize`/`Padding` satu-satu — `LayoutTransformControl` mengukur child di ukuran natural lalu me-render-nya mengecil, persis seperti browser zoom-out (lebih banyak konten muat di area yang sama, bukan cuma font kecil tapi semuanya termasuk DataGrid row height, ikon, dll). Window `Width`/`Height` disesuaikan jadi 1040x680 (dari 1200x760) supaya pas dengan footprint hasil scale. **Kalau user merasa masih kurang/kebihan kecil, tinggal ubah angka `ScaleX`/`ScaleY` di `MainWindow.axaml`** (0.85 = 85%, makin kecil makin padat).

### Menu Laboratorium jadi submenu 6-tile nyata — SELESAI
Modul `Modules/Penunjang` (tab gabungan PK/PA/MB + Radiologi sesi sebelumnya) **DIHAPUS TOTAL**, diganti struktur baru yang match 1:1 ke screenshot legacy:
- **`Modules/Laboratorium/LabMenuView`** — grid 6 tile (🧪 Permintaan Lab PK/PA/MB, 🔬 Periksa Lab PK/PA/MB), persis pola `WrapPanel`+`Button.menuTile` yang sudah dipakai di dashboard utama `MainWindow`. Klik tile → raise event `TileClicked(LabAction)`.
- **`Modules/Laboratorium/LabPermintaanView`** — layar SATU TUJUAN, cuma form "Buat Permintaan Baru" (tidak ada grid/antrian). Reusable lintas PK/PA/MB via `SetKategori(KategoriLab)` (bukan 3x copy-paste XAML seperti percobaan sebelumnya) — judul & tag kategori berubah sesuai parameter.
- **`Modules/Laboratorium/LabPeriksaView`** — layar SATU TUJUAN, antrian menunggu hasil + input hasil. Sama, reusable via `SetKategori`.
- **`Modules/Laboratorium/KategoriLabel`** — helper kecil `KategoriLab → "PK"/"PA"/"MB"` dipakai di judul kedua view di atas.
- **`Modules/Radiologi/RadiologiView`** — diekstrak jadi modul BERDIRI SENDIRI (sebelumnya nebeng 1 tab di `PenunjangView` dengan judul gabungan ":: [ Laboratorium / Radiologi ] ::" yang bikin bingung user — sekarang judulnya cuma ":: [ Radiologi ] ::"). Isinya sama persis (antrian + permintaan + hasil dalam 1 layar, tidak dipecah PK/PA/MB karena skema `permintaan_radiologi` memang tidak punya kategori).
- **`MainWindow.axaml.cs`**: `OnFeatureShortcutClick` → "Laboratorium" buka `LabMenuView` (bukan langsung form), tiap tile klik → `OpenLabTileAsync` yang routing ke `LabPermintaanView`/`LabPeriksaView` sesuai `LabAction`, tombol "⬅ Kembali" dari situ balik ke `LabMenuView` (bukan ke Hero — pola sama seperti RawatJalan→Registrasi). "Radiologi" toolbar langsung buka `RadiologiView` seperti sebelumnya, tidak lewat submenu (sesuai struktur legacy, radiologi tidak dipecah-pecah).
- `PenunjangRepository`/`PenunjangAntrianItem` (Data layer) **TIDAK berubah** — sudah cukup general (terima parameter `KategoriLab` per panggilan), cuma caller-nya di UI yang dirombak total.
- Build sukses, 12 test tetap pass, `.exe` di-publish ulang.
- **Belum diverifikasi visual** — cek tampilan scale 85% nyaman dipakai atau tidak, dan konfirmasi 6 tile Lab + judul per-layar sudah sesuai yang dimaksud.

## Revert global zoom, scope font-fix ke Registrasi saja — 2026-11-07

User koreksi: maksud "kebesaran" itu CUMA soal font di layar Registrasi, BUKAN minta seluruh aplikasi di-zoom-out. `LayoutTransformControl` scale 85% di `MainWindow.axaml` (sesi sebelumnya) **DIREVERT SEPENUHNYA** — Window kembali ke `Width="1200" Height="760"` tanpa wrapper transform.

Sebagai gantinya, font-size dipersempit **HANYA di `RegistrasiView.axaml`** lewat `UserControl.Styles` lokal (`Selector="TextBlock"`/`"TextBox, DatePicker, ComboBox"` → `FontSize="12"`, scoped ke view ini saja, TIDAK bocor ke layar lain) + `RegistrasiGrid` dapat `FontSize="12"` `RowHeight="26"` eksplisit. Judul layar (":: [ Registrasi / Daftar Pasien Rawat Jalan ] ::") di-override manual `FontSize="15"` supaya tetap lebih besar dari body text (local attribute override style generik di atas).

Build sukses, 12 test tetap pass, `.exe` di-publish ulang.

**Pelajaran untuk sesi berikutnya**: kalau user bilang satu layar spesifik "kebesaran", JANGAN lompat ke solusi app-wide (global transform/zoom) tanpa konfirmasi dulu — scope fix ke layar yang dikeluhkan saja.

## Fitur Farmasi — SELESAI (v1, real write, belum ditest ke DB asli) — 2026-11-07

User kirim screenshot toolbar Farmasi legacy (5 ikon: Input/Data Penjualan Obat & BHP, Data Penyerahan Darah, Daftar Resep Dokter, No. Resep), minta dibuatkan dengan isi serupa.

- **Entities baru** (`Core/Entities/Farmasi.cs`): `DataBarang` (→ `databarang`, katalog obat/BHP — v1 cuma pakai kolom `Dasar`/`JualBebas`, bukan semua 8 kolom tarif per-kelas/penjab), `ResepObat` (→ tabel `resep_obat` — **nama tabel legacy TERBALIK dari dugaan**: `resep_obat` ternyata HEADER resep per no_rawat/dokter/tanggal, bukan `resep_dokter`), `ResepDokterItem` (→ tabel `resep_dokter` — ini yang isinya baris obat per resep, no_resep+kode_brng+jml+aturan_pakai), `PengeluaranObatBhp`+`DetailPengeluaranObatBhp` (→ `pengeluaran_obat_bhp`/`detail_pengeluaran_obat_bhp`, dipakai sebagai backing table "Penjualan Obat & BHP" — **catatan penting**: ini sebenarnya tabel pengeluaran/issuance stok gudang farmasi di skema asli, bukan tabel POS/penjualan-ke-pasien dengan pembayaran; dipakai karena paling dekat secara struktur tanpa harus bangun alur billing farmasi penuh yang belum ada).
- **`resep_dokter`/`detail_pengeluaran_obat_bhp` TIDAK punya PK terdeklarasi** di skema asli — dipetakan pakai composite key buatan (`NoResep+KodeBarang`, `NoKeluar+KodeBarang`) khusus untuk kebutuhan EF; baca-nya pakai `.AsNoTracking()` supaya kalau ada baris "duplikat key" (jarang tapi mungkin, misal racikan) tidak bikin EF Core identity-map error.
- **`FarmasiRepository`** (`Data/FarmasiRepository.cs`): `SearchBarangAsync` (cari obat aktif, `.Take(50)`), `GetResepDokterAsync` (list resep_obat 7 hari terakhir, join pasien/dokter, `.Take(200)` safety net — tabel asli 94.973 baris, belajar dari insiden Registrasi sebelumnya supaya tidak nge-lag lagi), `GetResepItemsAsync` (detail item 1 resep), `InputPenjualanAsync` (**real INSERT** header+1 detail line — **v1 sengaja cuma 1 item per transaksi**, bukan multi-baris seperti form asli), `GetPenjualanAsync` (list transaksi 7 hari terakhir).
- **UI**: `Modules/Farmasi/FarmasiMenuView` — submenu 5-tile sama persis pola `LabMenuView`, 3 tile terhubung ke layar nyata (`InputPenjualanView`, `DataPenjualanView`, `DaftarResepView`), 2 tile ("Data Penyerahan Darah", "No. Resep") **sengaja placeholder** "belum diimplementasikan" — blood bank (UTD) adalah subsistem besar tersendiri (8 tabel `utd_*`), dan fungsi "No. Resep" belum jelas maksudnya tanpa referensi layar aslinya lebih lanjut.
  - `InputPenjualanView` — cari barang (Enter di kotak cari) → pilih dari dropdown → isi jumlah/keterangan/no.faktur opsional → Simpan (real write).
  - `DataPenjualanView` — list transaksi 7 hari terakhir, read-only.
  - `DaftarResepView` — list resep 7 hari terakhir (read-only, TIDAK ADA aksi tandai-sudah-diserahkan — lihat alasan di bawah), klik baris → tampil item obat resep itu di panel kanan.
- Tombol toolbar **"Farmasi"** sekarang buka `FarmasiMenuView` (sebelumnya placeholder).
- Build sukses, 12 test tetap pass, `.exe` di-publish ulang.
- **Kenapa "Daftar Resep Dokter" read-only (tidak ada tombol "Serahkan"/dispense)**: kolom `resep_obat.tgl_penyerahan`/`jam_penyerahan` NOT NULL tanpa default — tidak jelas dari skema bagaimana legacy membedakan "resep belum diserahkan" vs "sudah" (beda dengan `kamar_inap.stts_pulang` yang punya nilai `'-'` eksplisit untuk "belum"). Daripada menebak dan berpotensi menulis data yang salah makna ke DB asli, diputuskan v1 cukup jadi viewer. Perlu klarifikasi lebih lanjut (ke user yang paham alur asli, atau baca kode Java `resep_dokter`/`penjualanobat` di `simrs-khanza` legacy) sebelum menambah aksi tulis di sini.
- **BELUM dilakukan**: integration test baru (sama alasan modul lain — MariaDB dev lokal mati saat sesi ini, sengaja tidak menulis test yang nge-hit DB produksi asli), verifikasi visual/klik manual (coba dulu 1 transaksi test sebelum dipakai ke data sungguhan), racikan resep (`resep_dokter_racikan`) belum disentuh sama sekali, harga per kelas/penjab (`databarang` kolom `ralan`/`kelas1-3`/dst) belum dipakai — `InputPenjualanView` cuma pakai `Dasar`.

## Fitur IGD/UGD — SELESAI (v1, real write, belum ditest ke DB asli) — 2026-11-07

- **Temuan skema penting**: Khanza TIDAK memodelkan IGD sebagai tipe pasien terpisah — IGD cuma `poliklinik` biasa (`kd_poli` = `IGD`/`U0020`/`IGD2`/`IGD3`/`IGDK`, dicek langsung ke `poliklinik.nm_poli` di DB asli). Jadi pasien IGD tetap lewat `reg_periksa` normal (via layar Registrasi yang sudah ada), yang beda cuma ADA LANGKAH TRIASE tambahan sebelum diperiksa dokter.
- **Entities baru** (`Core/Entities/Igd.cs`): `MasterTriaseKasus` (→ `master_triase_macam_kasus`, lookup kecil "macam kasus" buat ComboBox), `TriaseIgd` (→ `data_triase_igd`, PK `no_rawat` — cara masuk/alat transport/alasan datang/macam kasus + 6 tanda vital).
  - **Sengaja TIDAK diimplementasikan**: 5 tabel `data_triase_igddetail_skala1-5` (skoring triase terstruktur ala ATS/Australasian Triage Scale) — jauh lebih kompleks, v1 cukup catat data kedatangan + vital sign mentah.
- **`IgdRepository`**: `GetAntrianBelumTriaseAsync` (reg_periksa di poli IGD yang belum ada baris `data_triase_igd`, window 7 hari, pola sama seperti RawatJalan), `GetMacamKasusAsync` (lookup kasus), `SaveTriaseAsync` (**real INSERT**).
- **UI**: `Modules/Igd/IgdView` — pola dua panel sama seperti RawatJalanView (antrian kiri, form triase kanan: cara masuk/alat transport/alasan datang/macam kasus + 6 tanda vital wajib diisi karena NOT NULL di skema). Dibuka dari toolbar **"IGD/UGD"** (sebelumnya placeholder).
- **Setelah triase**: TIDAK ada langkah "periksa" terpisah di modul ini — pasien yang sudah ditriase otomatis tetap muncul di antrian layar **Rawat Jalan** yang sudah ada (query-nya memang tidak difilter per-poliklinik), jadi dokter lanjut periksa dari situ. Tidak perlu modul/kode baru buat exam IGD, cukup reuse yang sudah ada — dicatat eksplisit di komentar kode supaya sesi depan tidak bingung kenapa `IgdView` tidak punya form pemeriksaan.
- Build sukses, 12 test tetap pass, `.exe` di-publish ulang.
- **BELUM dilakukan**: integration test baru (alasan sama — DB dev lokal mati), verifikasi visual/klik manual (coba 1 pasien test dulu), skoring triase terstruktur (5 tabel skala), dan `id_observation_*`/`id_composition` (field integrasi SatuSehat di `data_triase_igd`, nullable, belum dipakai).

## Sesi lanjutan 2026-11-07 (lanjutan ke-6): logo, zebra stripe, konsistensi desain, Radiologi jadi submenu — 2026-11-07

User kirim screenshot submenu Radiologi legacy (2 tile: Permintaan Radiologi / Periksa Radiologi — pola sama seperti Lab, cuma 2 bukan 6), minta: (1) bahasa desain IGD & modul lain dibuat seperti Registrasi (form input DI ATAS, collapsible, **default TERTUTUP** — kebalikan dari fix sesi sebelumnya yang bikin selalu terbuka; data/grid DI BAWAH), (2) logo aplikasi pakai file `logo.png` yang dikirim user, (3) baris tabel diberi warna selang-seling (zebra stripe) tapi subtle.

### Logo aplikasi — SELESAI
`logo.png` user disalin ke `src/SimrsKhanza.UI/Assets/logo.png`, `.csproj` ditambah `<AvaloniaResource Include="Assets\**" />` (sebelumnya TIDAK ADA — folder `Assets/` cuma placeholder kosong dari scaffold awal, belum pernah benar-benar dipakai). Dipasang di 3 tempat: toolbar atas `MainWindow` (kiri, 28x28), hero/welcome panel `MainWindow` (96x96, ganti teks judul polos), dan `LoginWindow` (56x56, ganti emoji 🏥 sebelumnya).

### Zebra stripe tabel — SELESAI
**Catatan teknis penting**: Avalonia `DataGrid` (beda dari WPF) **TIDAK PUNYA** properti `AlternatingRowBackground` — percobaan pertama pakai itu gagal build (`AVLN2000`). Solusi yang jalan: style global di `App.axaml` pakai **structural pseudo-class** `Selector="DataGridRow:nth-child(2n)"` → `Setter Property="Background"`. Warna baru `ThemeRowAltBrush` ditambah ke kedua tema (Light `#EFF5FC` biru sangat muda, Dark `#30384A`) — sengaja subtle sesuai permintaan user "jangan terlalu mencolok". Berlaku OTOMATIS ke SEMUA DataGrid di aplikasi (Registrasi, IGD, Lab, Farmasi, dst) tanpa perlu diset per-layar.

### Konsistensi desain: form-atas-collapsible + grid-bawah — SEBAGIAN DITERAPKAN
User minta pola Registrasi (toggle "Input Data" collapsible, default **TERTUTUP**) jadi bahasa desain standar. Diterapkan ke:
- **`RegistrasiView`**: `InputDataToggle` dikembalikan ke `IsChecked="False"` (sesi sebelumnya sempat diubah jadi default terbuka atas permintaan lain — user sekarang eksplisit minta sebaliknya, "kebalikan dari behaviour sekarang").
- **`IgdView`**: DIROMBAK TOTAL dari pola dua-panel (antrian kiri/form kanan) jadi form-atas-collapsible (default tertutup) + grid "Belum Triase" di bawah — pola persis Registrasi. Memilih baris di grid otomatis membuka panel input (toggle `IsChecked=true`) supaya tetap 1 klik, bukan 2.
- **`PeriksaRadiologiView`** (baru, lihat bawah): sama, form "Input Hasil" collapsible di atas + grid "Menunggu Hasil" di bawah.
- **BELUM diterapkan** ke: `RawatInapView`, `KasirView`, `DaftarResepView`/`InputPenjualanView`/`DataPenjualanView` (Farmasi), `LabPermintaanView`/`LabPeriksaView`, `RawatJalanView` — masih pakai pola dua-panel lama. **Ini scope besar kalau mau dirapikan semua sekaligus** — belum dikerjakan sesi ini, tunggu konfirmasi user apakah mau disamakan semua atau cukup modul yang baru/sering dipakai.

### Radiologi dipecah jadi submenu 2-tile — SELESAI
Sama persis pola `LabMenuView`: `Modules/Radiologi/RadiologiMenuView` (2 tile: 📋 Permintaan Radiologi, 🩻 Periksa Radiologi) menggantikan `RadiologiView` lama (1 layar gabungan form+antrian+hasil, DIHAPUS). `PermintaanRadiologiView` (form saja, pola sama `LabPermintaanView`) dan `PeriksaRadiologiView` (antrian+hasil, pola form-atas-collapsible+grid-bawah yang baru) keduanya reuse `PenunjangRepository` yang sudah ada (tidak ada perubahan Data layer). Tombol toolbar "Radiologi" sekarang buka `RadiologiMenuView`.

Build sukses, 12 test tetap pass, `.exe` di-publish ulang.

## Sesi lanjutan 2026-11-07 (lanjutan ke-7): logo jadi ikon .exe, RawatInap ikut pola baru, Registrasi Simpan/Baru nyata — 2026-11-07

### Logo — dipindah dari topbar jadi ikon aplikasi
User koreksi: logo TIDAK perlu di toolbar, tapi di-compile jadi ikon `.exe`. Logo dihapus dari `MainWindow` row 0 toolbar (tetap ada di hero panel + LoginWindow). `logo.png` (1254x1254) di-convert ke `Assets/app.ico` (256x256, PNG-compressed single-frame ICO — Avalonia DataGrid-style tooling tidak tersedia untuk convert, jadi dibuat manual via `System.Drawing` + ICO container ditulis byte-per-byte). `.csproj` ditambah `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`. `MainWindow` juga dapat `Icon="avares://SimrsKhanza.UI/Assets/logo.png"` (ikon window/taskbar saat app jalan, beda dari ikon file `.exe` di File Explorer). **Diverifikasi**: ikon berhasil ter-embed, dicek pakai `System.Drawing.Icon.ExtractAssociatedIcon` terhadap exe hasil publish — berhasil extract 32x32.

### RawatInapView — direstyle ikut pola Registrasi/IGD
Sama seperti IGD sebelumnya: dari dua-panel (daftar kiri/form kanan) jadi form-atas-collapsible (default tertutup, isi 2 kolom: "Admit Pasien Baru" + "Pulangkan Pasien Terpilih") + grid "Sedang Dirawat" di bawah. Pilih baris di grid otomatis buka panel (konsisten dengan IGD).

### Registrasi: tombol "Simpan"/"Baru" SEKARANG NYATA (sebelumnya placeholder total!) — temuan penting
User minta audit fitur yang "belum terkonek database". Temuan PALING PENTING: form "Input Data" di Registrasi **sudah ada sejak awal** (No.Reg/No.Rawat/Dr Dituju/dst) tapi tombol **Simpan/Baru/Ganti/Hapus semuanya cuma dialog "belum diimplementasikan"** — artinya sebelum sesi ini, **tidak mungkin registrasi pasien baru dari aplikasi rewrite sama sekali**, padahal tampilannya kelihatan lengkap. Ini gap paling signifikan yang ditemukan, sekarang diperbaiki:
- Form "Input Data" dirombak: field `No.Reg`/`No.Rawat`/`Tgl.Reg`/`Asal Rujukan` (auto-generate atau belum relevan di v1) dihapus dari input manual; `Dr Dituju`/`Unit`/`Jenis Bayar` yang tadinya TextBox kosong diganti **ComboBox nyata** terisi dari DB (`dokter`, `poliklinik`, `penjab`).
- **`RegistrasiRepository.CreateAsync`** (baru) — **real INSERT** ke `reg_periksa`. Generate `no_rawat`/`no_reg` otomatis mengikuti pola asli yang dikonfirmasi dari DB produksi: `no_rawat = "yyyy/MM/dd/NNNNNN"`, `no_reg = "NNN"`, sama-sama sequence harian mulai dari 1 (dihitung dari `COUNT(*)` baris `reg_periksa` hari itu + 1).
  - **Simplifikasi v1 yang didokumentasikan**: `StatusPoli` selalu di-set `Lama` (membedakan pasien "baru di poli ini" vs "lama" butuh cek riwayat registrasi pasien itu — belum dikerjakan), `BiayaReg` diambil dari `poliklinik.registrasi` (tarif default poli tsb, bukan tarif per-kelas/penjab).
- Tombol "💾 Simpan" (di panel Input Data) sekarang panggil `CreateAsync` beneran, "🆕 Baru (kosongkan)" bersihkan form. Tombol "Ganti"/"Hapus" di action bar bawah **TETAP placeholder** — edit/hapus registrasi yang SUDAH ADA (apalagi di data produksi asli) butuh spek lebih jelas & lebih berisiko, sengaja tidak digarap sesi ini.
- Build sukses, 12 test tetap pass, `.exe` di-publish ulang.
- **BELUM dilakukan**: verifikasi klik manual (coba registrasi **1 pasien test**, BUKAN pasien asli dulu). Integration test baru untuk `CreateAsync` juga belum ditulis (alasan sama — DB dev lokal mati saat sesi ini).

### Audit lengkap — fitur lain yang masih "belum diimplementasikan" (placeholder dialog)
Hasil `grep` semua `ShowNotImplementedAsync`/`OnPlaceholderClick` di codebase, untuk referensi prioritas sesi berikutnya:
- **Toolbar atas** (baris pertama): "Program", "Presensi Pegawai", "Informasi", "Anjungan & Antrian", "Tentang Program" — semua placeholder. User kirim screenshot menu "Informasi" legacy (submenu: Pasien Kamar Inap, Telusur Kunjungan Pasien, Penggunaan Kamar Inap, Ketersediaan Stok Darah UTD, Tarif per modul, Dashboard, dll) sebagai referensi — **belum dikerjakan**, disebut user sendiri sebagai "buat menu antrian" (konteksnya belum sepenuhnya jelas, perlu klarifikasi next session fitur apa persis yang dimaksud).
- **Dashboard tile**: "Kepegawaian", "Inventaris", "Laporan", "Bridging" — placeholder (modul besar, belum disentuh sama sekali).
- **Registrasi**: tombol "Ganti"/"Hapus" (lihat di atas).
- **Farmasi**: "Data Penyerahan Darah", "No. Resep" (sudah didokumentasikan sejak awal dibuat, lihat bagian Farmasi di atas).
- **Pengaturan**: "Identitas & Aset" (logo RS, wallpaper, dsb — kosmetik, prioritas rendah).

## Sesi lanjutan 2026-11-07 (lanjutan ke-8): "Informasi" jadi window terpisah + "Tentang Program" — 2026-11-07

### "Tentang Program" — SELESAI
`AboutWindow` baru (modal, `ShowDialog`): logo, judul, **"Dibuat oleh: Yusnur Rofiq"**, dan blok "Spesifikasi Teknis" — fat client (bukan web, koneksi DB langsung dari client), Avalonia UI 12, C#/.NET 10, EF Core + Pomelo MySQL, QuestPDF, arsitektur Core/Data/Config/Integrations/UI, self-contained single-file .exe. Dibuka dari toolbar atas "Tentang Program" (sebelumnya placeholder).

### "Informasi" — jadi WINDOW TERPISAH BENERAN (bukan swap PageHost) — SELESAI
User klarifikasi: di legacy, "Informasi" sengaja dibuka sebagai window OS terpisah supaya bisa di-drag ke monitor kedua sementara app utama tetap di monitor pertama — beda dari semua modul lain yang sengaja dibikin single-window (PageHost swap). Jadi `InformasiWindow` BENERAN `new Window().Show()` (non-modal, bukan `ShowDialog`, supaya tidak terkunci ke window utama) — satu-satunya modul di app ini yang sengaja melanggar pola single-window, dengan alasan eksplisit didokumentasikan di komentar kode.

**Scope v1**: dari submenu legacy (Pasien Kamar Inap, Telusur Kunjungan Pasien, Penggunaan Kamar Inap, Ketersediaan Stok Darah UTD, Tarif per-modul, Dashboard, Koleksi Penelitian, Cari Koleksi Ebook, Cari Inventaris Perpustakaan) — HANYA 4 yang diimplementasi (yang datanya sudah ada di model kita), sisanya (Tarif, Koleksi Penelitian, Ebook, Inventaris Perpustakaan) **sengaja ditunda**, butuh modul data baru yang belum dibangun sama sekali (perpustakaan/penelitian khususnya di luar scope RS, kemungkinan modul tambahan Khanza yang jarang dipakai):
- **Pasien Kamar Inap** — reuse query sama seperti `RawatInapRepository.GetActiveInpatientsAsync` (query ulang langsung, tidak reuse class-nya biar `InformasiRepository` berdiri sendiri).
- **Telusur Kunjungan Pasien** — cari by No.RM/No.Rawat/Nama, **wajib isi keyword dulu** (tidak ada default-load tanpa filter — pelajaran dari insiden Registrasi awal, `reg_periksa` 169rb+ baris), `.Take(100)`.
- **Penggunaan Kamar Inap** — daftar SEMUA kamar + status (ISI/KOSONG/DIBERSIHKAN/dst) + kelas, dari tabel `kamar` yang sudah dipetakan (sesi Rawat Inap).
- **Ketersediaan Stok Darah UTD** — entity baru `UtdStokDarah` (→ `utd_stok_darah`), grouped by golongan darah + resus, cuma hitung kantong `status='Ada'`.
- Semua READ-ONLY, tidak ada tombol simpan/tulis di window ini.
- Dibuka dari toolbar atas "Informasi" (sebelumnya placeholder).

Build sukses, 12 test tetap pass, `.exe` di-publish ulang.

## Revert: Informasi & Tentang Program kembali ke pola single-window — 2026-11-07

User koreksi lagi: TIDAK jadi mau "Informasi" dibuka sebagai Window OS terpisah (sesi sebelumnya sempat dibuat begitu untuk alasan "bisa di-drag ke monitor 2") — ternyata maunya tetap konsisten dengan modul lain, 1 window saja, bahasa desain sama. `InformasiWindow`/`AboutWindow` (Window) **DIHAPUS**, diganti `Modules/Informasi/InformasiView` dan `Modules/About/AboutView` (UserControl, pola PageHost standar sama seperti modul lain — judul `:: [ ... ] ::`, tombol "⬅ Kembali ke Menu" `Classes="accentText"`). Isi/fitur TIDAK berubah (4 tab Informasi read-only, konten About sama persis). `MainWindow.OnTopShortcutClick` untuk "Informasi"/"Tentang Program" sekarang buka lewat `PageHost` seperti modul lainnya, bukan `new Window().Show()`/`ShowDialog()`.

Build sukses, 12 test tetap pass, `.exe` di-publish ulang.

## Fitur Laporan — SELESAI (v1, read-only) — 2026-11-07

User pilih Laporan sebagai fitur lanjutan. Dashboard tile "Laporan" (sebelumnya placeholder) sekarang buka submenu 3-tile (pola sama `LabMenuView`/`FarmasiMenuView`):
- **Laporan Kunjungan** — reuse `RegistrasiRepository.GetByPeriodeAsync` yang sudah ada (periode picker default hari ini, tombol "Tampilkan") + ringkasan jumlah kunjungan.
- **Laporan Pendapatan** — **method baru** `KasirRepository.GetLaporanPendapatanAsync(fromDate,toDate)` (list tagihan per kunjungan dalam periode, capped `.Take(300)`, reuse logika `GetBillAsync` per baris) + ringkasan total tagihan & total sudah dibayar.
- **Laporan Penjualan Farmasi** — reuse `FarmasiRepository.GetPenjualanAsync` yang sudah ada (sebelumnya cuma dipanggil hardcode 7 hari di `DataPenjualanView`, sekarang dengan periode picker bebas) + ringkasan total.
- Semua READ-ONLY, semua default periode = hari ini (pelajaran dari insiden Registrasi: jangan default ke range lebar tanpa filter terhadap tabel besar).
- **BELUM dilakukan**: cetak PDF (pola QuestPDF sudah ada di `RegistrasiReport.cs`, tinggal direplikasi — belum sempat di sesi ini), laporan lain yang legacy py punya (kepegawaian, inventaris, dll — modul sumbernya sendiri belum dibangun jadi laporannya juga belum).

Build sukses, 12 test tetap pass, `.exe` di-publish ulang.

## Right-click patient context menu — SELESAI (v1, 4 dari 17 item nyata) — 2026-11-07

User kirim screenshot menu klik-kanan legacy pada daftar pasien (Data Rekam Medis, Permintaan, Kamar Inap, Tindakan & Pemeriksaan, Obat, Billing/Pembayaran Pasien, Laporan, Grafik Analisa, Surat-Surat, Check List Kelengkapan Pendaftaran, Lembar Periksa Pasien, Label & Barcode, Rujukan, Bridging, Input Data, Urutkan Data Berdasar, Hapus & Ubah Data). Minta: bangun dulu **behaviour**-nya (mekanisme klik-kanan di semua grid pasien), isi menunya dilengkapi pelan-pelan menyusul.

- **`Modules/Shared/PatientContextMenu.cs`** (baru) — definisi SATU menu klik-kanan dipakai bareng oleh 4 grid pasien (Registrasi/IGD/Rawat Inap/Rawat Jalan), bukan 4 salinan terpisah. `PatientMenuAction` enum berisi ke-17 item persis urutan screenshot (dengan separator grup yang sama). `PatientContextMenu.Build(getSelected, onSelect)` — reusable, dipasang ke `DataGrid.ContextMenu` tiap view lewat constructor.
  - **Catatan simplifikasi**: context menu pakai baris yang SEDANG TERPILIH (`SelectedItem`) di grid saat item menu diklik — BUKAN literal "baris di bawah kursor klik-kanan" (klik-kanan di Avalonia `DataGrid` tidak otomatis re-select baris itu). Asumsinya user klik-kiri dulu buat pilih baris, baru klik-kanan buat buka menu — behaviour umum, tapi beda dari beberapa app native Windows. Didokumentasikan di komentar kode.
- **4 dari 17 item SUDAH nyata** (navigasi ke layar yang sudah ada + prefill No.Rawat/No.RM):
  - **Data Rekam Medis** → buka `Modules/Pasien/PasienView`, auto-search by No.RM (`PasienView.SearchForAsync` — method baru).
  - **Kamar Inap** → buka `Modules/RawatInap/RawatInapView`, prefill No.Rawat di form Admit + buka panel (`RawatInapView.PrefillAdmit` — method baru).
  - **Tindakan & Pemeriksaan** → buka `Modules/RawatJalan/RawatJalanView` (belum prefill pasien spesifik — antrian-nya sudah termasuk semua pasien lintas poli termasuk yang dipilih, tinggal diklik manual dari situ).
  - **Billing/Pembayaran Pasien** → buka `Modules/Kasir/KasirView`, prefill No.Rawat + auto-cari tagihan (`KasirView.SearchForAsync` — method baru).
- **13 item sisanya** (Permintaan, Obat, Laporan, Grafik Analisa, Surat-Surat, Check List Kelengkapan Pendaftaran, Lembar Periksa Pasien, Label & Barcode, Rujukan, Bridging, Input Data, Urutkan Data Berdasar, Hapus & Ubah Data) — klik masih munculin dialog "belum diimplementasikan" (format pesan include nama pasien, misal `"Permintaan" untuk BUDI SANTOSO`). **Sengaja progresif** sesuai instruksi user "pelan-pelan" — pindahkan satu-per-satu dari `MainWindow.HandlePatientMenuAsync`'s default case ke `case` nyata begitu layar terkaitnya dibangun.
- Build sukses, 12 test tetap pass, `.exe` di-publish ulang.
- **Belum diverifikasi visual** — coba klik-kanan di grid Registrasi/IGD/Rawat Inap/Rawat Jalan, pastikan menu muncul dengan urutan+separator yang benar dan ke-4 item nyata beneran pindah layar dengan data terisi.

## Polish terakhir sesi ini: tile dinamis, grid auto-fit, tab header kecil — 2026-11-07

- **`Modules/Shared/MenuTileBuilder.cs`** (baru) — satu helper dipakai `LabMenuView`/`RadiologiMenuView`/`FarmasiMenuView`/`LaporanMenuView` (sebelumnya 4 salinan kode identik). Ukuran tile sekarang **menyesuaikan jumlah item**: ≤2 tile → 260x190 font 16, ≤4 tile → 190x150 font 14, lebih → 150x110 font 13 (default lama). Jadi submenu Radiologi (2 tile) sekarang tile-nya besar & proporsional, bukan kecil nyempil di area kosong.
- **Grid Registrasi** — 18 kolom dikonversi dari pixel tetap ke **star-sizing proporsional** (`Width="0.7*"` dst, + `MinWidth` per kolom) — otomatis mengisi penuh lebar layar tanpa perlu resize manual, tetap bisa di-resize manual kalau mau (`CanUserResizeColumns` tetap `True`). **Catatan untuk sesi berikutnya**: grid lain (IGD/RawatInap/Lab/Farmasi/dll) MASIH pakai kolom pixel tetap — belum dikonversi semua karena scope besar (banyak file), tapi pola star-sizing ini sudah established, tinggal direplikasi kalau user minta grid lain ikut auto-fit juga.
- **Tab "Registrasi Awal"/"Rujukan Internal Poli"** — header diganti `TextBlock` custom (bukan `Header="..."` string biasa) dengan `FontSize="12"` + `FontWeight="Medium"` + `ThemeTextBrush`, lebih kecil & konsisten dengan body text layar ini (sebelumnya pakai default styling `TabItem` dari FluentTheme yang terasa lebih besar dari teks lain).

Build sukses, 12 test tetap pass, `.exe` di-publish ulang.

## Permintaan Radiologi dirombak jadi layar daftar+filter (sesuai screenshot legacy) — 2026-11-07

User kirim screenshot legacy "Data Pemeriksaan Radiologi": filter bar atas (No.Rawat/Pasien/Tanggal s.d./Petugas), grid daftar permintaan dengan rincian per-baris biaya/proyeksi pemeriksaan, lalu bar bawah Key Word + Hapus/Semua/Cetak/Keluar. Sebelumnya `PermintaanRadiologiView` cuma form kirim-permintaan-baru polos, tanpa daftar sama sekali.

- **`Modules/Radiologi/PermintaanRadiologiView.axaml(.cs)`** — dirombak total ikut pola layout "form input collapsible di atas (default tertutup) + grid di bawah" yang sudah established (IGD/RawatInap/Registrasi):
  - Form kirim permintaan baru (No.Rawat/Dokter Perujuk/Status/Diagnosa) **tetap ada**, dipindah ke panel collapsible (toggle "Input Permintaan Baru"), bukan dihapus.
  - **Filter bar baru**: No.Rawat (contains-match) + rentang Tanggal dari-s.d. (pakai `PenunjangRepository.GetAntrianRadiologiAsync` yang sudah ada, default 7 hari terakhir biar tidak lag — pelajaran dari insiden Registrasi). Tombol "Cari" reload dari DB sesuai rentang baru.
  - **Grid baru** (`DataGrid` nama `DataGrid`) menampilkan No.Rawat/Pasien/Tgl.Periksa/Jam Periksa/Dokter Perujuk/Diagnosa Klinis — kolom star-sizing + MinWidth (ikut pola auto-fit terbaru).
  - **Bar bawah**: "Key Word" (filter client-side tambahan di atas hasil filter tanggal — cocok ke Nama Pasien/No.Rawat/Dokter, live saat ketik), "Semua" (reset semua filter + load 1 tahun terakhir), "Hapus"/"Cetak" (placeholder, lihat di bawah), "Keluar" (kembali ke submenu Radiologi, sama kayak `BackRequested` lama).
  - **Record count label** ("Record: N") di atas grid, meniru status bar legacy.
- **Sengaja TIDAK dibangun** (didokumentasikan biar jelas, bukan lupa):
  - Rincian per-baris "Proyeksi & Dosis Radiasi / Kode Periksa / Nama Pemeriksaan / Biaya / Cara Bayar" dari screenshot — ini datang dari tabel tarif `jns_perawatan_radiologi` + billing `periksa_radiologi`, yang **belum dimodelkan sama sekali** di v1 (`PermintaanRadiologi` entity cuma order header, tidak ada tarif/biaya). Butuh modul tarif+billing radiologi baru dulu sebelum ini bisa jadi nyata — bukan tugas kecil, sengaja ditunda.
  - **Hapus** — klik cuma munculin status "belum didukung". Sengaja: hapus order radiologi nyata butuh konfirmasi + mikirin efek ke data terkait (hasil, billing), dan instruksi user eksplisit jangan ubah/hapus data pasien asli tanpa hati-hati.
  - **Cetak** — klik cuma munculin status "belum didukung" (gantung di rincian biaya yang belum ada, sama kayak di atas).
- Build sukses, `.exe` di-publish ulang. **Belum diverifikasi visual** — belum ada tooling screenshot/computer-use sesi ini untuk cek tampilan beneran mirip screenshot legacy.
- **Catatan untuk sesi berikutnya**: layout yang sama ini (filter bar + grid + Key Word/Hapus/Semua/Cetak/Keluar) kemungkinan juga cocok dipakai ulang untuk "Permintaan Lab" (`Modules/Laboratorium/LabPermintaanView`) yang sekarang masih bentuk form polos sama kayak Radiologi dulu — belum disentuh sesi ini, tapi pola & helper bisa ditiru langsung begitu diminta.

## MainWindow fullscreen on login + Input Penjualan Farmasi dirombak jadi katalog multi-item — 2026-11-07

User kirim screenshot legacy "Transaksi Penjualan Obat, Alkes & BHP Medis" dan minta MainWindow buka maximized (bukan window kecil 1200x760) begitu login.

- **`MainWindow.axaml`** — tambah `WindowState="Maximized"` di root `Window`. Tidak mengubah `Width`/`Height` default (tetap dipakai kalau user restore-down window-nya).
- **Skema real yang baru diverifikasi read-only lewat `DESCRIBE`/`SELECT` ke DB live** (bukan tebakan):
  - `databarang.kdjns` → tabel `jenis` (kdjns/nama) = kolom "Kategori" di screenshot (TABLET/ALKES/INJEKSI/…). **Bukan** `kategori_barang` (FORNAS/GENERIK/…) meski namanya mirip — sempat salah duga, dicek dulu ke live DB sebelum ngoding.
  - `gudangbarang` (kode_brng+kd_bangsal+no_batch+no_faktur → stok) = sumber kolom "Stok". Kolom "Lokasi: F.A" di screenshot cocok persis dengan `bangsal.kd_bangsal='F.A'` → "INSTALASI FARMASI". v1 cuma dukung satu lokasi ini (yang dipakai Farmasi), stok dijumlah lintas batch/faktur per kode_barang.
- **`Modules/Shared/Entities/Farmasi.cs`** (sebenarnya `Core/Entities/Farmasi.cs`) — tambah `DataBarang.KodeJenis`, entity baru `JenisBarang` dan `GudangBarang`. `DbContext` di-extend sesuai.
- **`FarmasiRepository`** — `InputPenjualanAsync` (1 item/transaksi) **diganti total** jadi `SimpanPenjualanAsync` (N item/transaksi, 1 row `detail_pengeluaran_obat_bhp` per item) + method baru `GetKatalogPenjualanAsync` (seluruh katalog barang aktif, ~1377 item, di-join ke `jenis` utk Kategori + stok `gudangbarang` lokasi F.A).
- **`Modules/Farmasi/InputPenjualanView.axaml(.cs)`** — dirombak total meniru layout legacy:
  - Header: No.Nota (readonly, bilang "dibuat otomatis saat Simpan" — **sengaja tidak meniru format "PJ..." legacy** karena algoritma penomoran aslinya belum di-reverse-engineer; no_keluar v1 tetap skema timestamp yang sudah ada), Pasien (opsional, teks bebas — tidak ada kolom pasien di `pengeluaran_obat_bhp` jadi digabung ke Keterangan saat Simpan), Catatan (wajib, jadi Keterangan), Petugas (placeholder NIP, sama seperti sebelumnya), Tanggal, Jenis Jual (Jual Bebas berfungsi; "Resep Dokter" cuma munculin pesan arahkan ke menu Daftar Resep Dokter, belum terhubung otomatis), Lokasi (readonly F.A).
  - Tab **Umum**: grid SELURUH katalog barang aktif (bukan satu-per-satu pilih dari dropdown seperti versi lama) — kolom Jml/Ptg(%)/Tmb(Rp)/Emb(Rp)/Tsl(Rp)/Aturan Pakai/No.Batch/No.Faktur bisa diedit langsung di baris (persis behaviour legacy: ketik jumlah di barang yang mau dijual, baris lain dibiarkan Jml=0). Subtotal/Ptg(Rp)/Total/Grand Total terhitung otomatis (`PenjualanLine.cs`, class baru dgn `INotifyPropertyChanged` manual).
  - Tab **Racikan**: stub teks "belum didukung" — racikan (obat formula/campuran) butuh pemodelan resep racikan terstruktur yang belum ada.
  - Baris bawah: Grand Total, PPN Obat(%), Akun Bayar (kosmetik, belum ada tabel akun), Ongkos Kirim, Tagihan+PPN, Jumlah Bayar, Kembali — **semua cuma hitungan UI**, TIDAK ada kolom buat PPN/ongkos-kirim/pembayaran di `pengeluaran_obat_bhp` legacy, jadi tidak ada yang disimpan dari situ (hanya per-baris Total barang yang tersimpan ke `detail_pengeluaran_obat_bhp.total`). Key Word (filter client-side nama/kode), Cari (reload dari DB), Nota (stub, belum ada cetak), Simpan (real, multi-item), Keluar.
  - **Sengaja TIDAK dibangun**: kolom "Kadaluarsa" (expire per-batch, butuh tabel batch master yang belum dimodelkan — `databarang.expire` ada tapi itu level katalog bukan per-batch terjual), cetak Nota, racikan.
- Build sukses, 12 test tetap pass, `.exe` di-publish ulang. **Belum diverifikasi visual** (tidak ada tooling screenshot/computer-use di sesi ini) — terutama perlu dicek: grid 1377 baris tidak lag, kolom bisa diedit inline dengan benar, dan kalkulasi Total/Grand Total akurat.
- **Catatan untuk sesi berikutnya**: `DataPenjualanView` (layar "Data Penjualan") masih query lama yang nge-join satu detail per header lewat `GetPenjualanAsync` — sekarang dengan transaksi multi-item, satu `no_keluar` bisa punya banyak baris, jadi layar itu akan menampilkan baris terpisah per item (bukan grup per transaksi) — belum masalah fungsional tapi tampilannya belum disesuaikan, catat kalau user minta dirapikan nanti.

## Rawat Inap dirombak jadi "Daftar Pasien Rawat Inap" + title bar menyatu dengan header biru — 2026-11-08

User kirim screenshot legacy "Daftar Pasien Rawat Inap" (DlgKamarInap) dan minta: tabel sama persis (urutan & isi), tombol bawah fungsional (termasuk "Ganti"), dan coba title bar window digabung dengan header biru atas (tombol min/max/close satu level).

- **Sumber kebenaran = kode Java legacy** (`simrs-khanza/src/simrskhanza/DlgKamarInap.java`, method `tampil()`), bukan tebakan dari screenshot:
  - "Belum Pulang" = `kamar_inap.stts_pulang='-'` **bukan** `tgl_keluar IS NULL` (versi lama kita salah untuk data real: legacy menyimpan `'0000-00-00'`, dan ada baris yatim 2017-2019 tanpa `reg_periksa`). Dengan join ke `reg_periksa` hasilnya **9 baris, persis sama dengan screenshot** (diverifikasi read-only ke DB live lewat console app diagnostik).
  - Kolom (21, urutan legacy): No.Rawat, Nomer RM, Nama Pasien (+umur), Alamat, Penanggung Jawab, Hubungan P.J., Jenis Bayar, Kamar (`kd_kamar nm_bangsal`), Tarif Kamar, Diagnosa Awal/Akhir, Tgl/Jam Masuk, Tgl/Jam Keluar (kosong kalau zero-date), Ttl.Biaya, Stts.Pulang, Lama, Dokter P.J., Status Bayar, Agama. Order: `nm_bangsal, tgl_masuk, jam_masuk`.
  - Filter: No.Rawat/No.RM/Nama (3 kotak di atas), Stts.Bayar, radio Belum Pulang / Tgl.Masuk s.d / Pulang s.d, Kamar, Key Word (Enter atau tombol Cari), "Semua" = reset. Cap 2000 baris.
- **`ConvertZeroDateTime=True` ditambahkan ke connection string** (`DatabaseSettings.ToConnectionString`) — tanpa itu membaca `'0000-00-00'` dari DB legacy bisa melempar exception. Berlaku global ke semua query (aman; zero-date jadi `MinValue`).
- **`StatusPulang` enum diperluas ke semua 15 nilai enum legacy** (`Core/Entities/MasterData.cs`, `StatusPulangLabels.Map` dipakai bersama DbContext). Dulu cuma 7 dan nilai asing (mis. "Atas Persetujuan Dokter" = 1453 baris riil!) di-map salah ke "-". Combo status pulang di form sekarang pakai nilai-nilai real.
- **`RawatInapRepository`** ditulis ulang: `GetDaftarAsync(filter)`, `AdmitAsync` (+guard: kamar harus KOSONG, visit tidak boleh sudah dirawat), `DischargeAsync(key, …)`, `PindahKamarAsync` (tutup baris lama `stts_pulang='Pindah Kamar'` + hitung biaya, kosongkan kamar lama, buka baris `kamar_inap` baru di kamar tujuan — sama seperti legacy), `GantiJenisBayarAsync` (`reg_periksa.kd_pj`), `GantiDiagnosaAsync`. Edit dikunci pakai kunci legacy `no_rawat+kd_kamar+tgl_masuk+jam_masuk`.
- **Tombol bawah**: Masuk, Pulang, Pindah, **Ganti** (di legacy "Ganti" ternyata submenu klik-kanan: Jenis Bayar / Diagnosa Masuk / Diagnosa Akhir — dijadikan tombol di bar bawah), Cetak (PDF QuestPDF `RawatInapReport.cs`, pola sama dgn `RegistrasiReport`), Keluar. Masuk/Pulang/Pindah/Ganti membuka panel aksi kecil di atas tabel (legacy pakai dialog modal; app ini tetap single-window).
- **Title bar**: `MainWindow` pakai `WindowDecorations="BorderOnly"` (Avalonia 12 — `ExtendClientAreaChromeHints` sudah tidak ada) + tombol min/max-restore/close custom di header biru (`Button.caption`, hover close merah), area kosong header = drag handle, double-click = maximize/restore, judul "SIMRS Gramedika" di tengah, logo kecil di kiri. Ini **eksperimen** sesuai permintaan user ("sekalian testing apakah bagus") — kalau kurang cocok, tinggal hapus `WindowDecorations` + blok `TitleBar` di `MainWindow.axaml`.
- Build sukses, 12 test pass, `.exe` dipublish ulang. **Belum diverifikasi visual** (tidak ada tooling screenshot) — terutama cek: title bar (drag/resize tepi/maximize), kolom tabel, dan **tes Masuk/Pulang/Pindah/Ganti pakai pasien TEST** (aksi-aksi ini menulis ke DB asli — jangan dicoba di pasien riil dulu).
- **Belum dilakukan**: bayi gabung (`ranap_gabung`, baris tambahan di legacy), filter Kamar via picker bangsal, laporan cetak 2/3 (Laporan 2 & Lembar Bimbingan Rohani), menu klik-kanan lengkap legacy Rawat Inap (puluhan item), alamat lengkap kelurahan/kecamatan/kabupaten (kolom itu belum dimodelkan di `Pasien`).

## Registrasi vs Rawat Jalan dipisah, grid Registrasi sesuai DlgReg, UI dipadatkan — 2026-11-08

- **Bug fix**: `MainWindow` crash setelah login (override `OnPropertyChanged` memanggil `FindControl` sebelum XAML selesai dimuat). Diganti flag `_componentReady`. Ditemukan lewat console app diagnostik yang membuat `MainWindow` di host Avalonia.
- **Registrasi (legacy `DlgReg`) ≠ Rawat Jalan (legacy `DlgRawatJalan`)** — keduanya sebelumnya membuka layar yang sama (hasil salah-tafsir di sesi lama; dibalik sekarang). Tombol toolbar "Registrasi" → `RegistrasiView`; "Rawat Jalan" → **`TindakanRalanView`** baru ("Perawatan/Tindakan Rawat Jalan"): 3 tab (Tindakan Dokter / Paramedis / Dokter+Paramedis) dari tabel `rawat_jl_dr` / `rawat_jl_pr` / `rawat_jl_drpr`, kolom persis legacy (tanpa kolom checkbox "P"), filter tanggal (default hari ini)/No.RM/Key Word, cap 1000 baris. `TindakanRalanRepository` pakai `SqlQueryRaw` (read-only, semua input user lewat parameter). Tombol "🩺 Pemeriksaan" membuka form vitals lama (`RawatJalanView`); tombol Kembali form itu kembali ke layar asalnya (Registrasi atau Tindakan). Diverifikasi ke DB live: 42 / 287 / 541 baris untuk 3 hari terakhir.
- **Belum dibangun**: INPUT tindakan baru (picker `jns_perawatan` + kolom biaya/KSO/BHP), tab Pemeriksaan/Obstetri/Ginekologi/Catatan legacy, kolom checkbox "P".
- **Grid Registrasi** diurutkan sesuai `DlgReg`: No.Reg, No.Rawat, Tanggal, Jam, Kode Dokter, Dokter Dituju, Nomer RM, Pasien, J.K., Umur, Poliklinik, Jenis Bayar, Penanggung Jawab, Alamat P.J., Hubungan P.J., Biaya Registrasi, Status, No.Telp, Stts Rawat, Stts Poli, Kode Poli, Kode PJ, Status Bayar (kolom baru: J.K., Umur, Stts Rawat, Kode Poli, Kode PJ).
- **UI Registrasi dipadatkan** (scoped di `RegistrasiView`, bukan global): font 11, tinggi baris grid 22, header kolom/TextBox/ComboBox/DatePicker/Button `MinHeight` 24, padding & margin form dikecilkan.
- **Title bar**: background `Window` diganti warna header biru (dugaan penyebab garis putih tipis saat un-maximize — **belum dikonfirmasi visual**; kalau masih ada, kemungkinan border `BorderOnly` dari OS, opsi berikutnya `WindowDecorations="None"` atau `ExtendClientAreaToDecorationsHint`).
- Build sukses, 12 test pass, `.exe` dipublish. Belum diverifikasi visual.

## Perbaikan lanjutan: crash Rawat Jalan, garis putih title bar, login borderless — 2026-11-08

- **Crash membuka Rawat Jalan**: `TindakanRalanView.OnTabChanged` memanggil `FindControl` saat `SelectionChanged` pertama (terpicu di dalam `InitializeComponent`, sebelum name scope ada) — pola bug yang sama dengan crash MainWindow sebelumnya. Diberi flag `_ready`. **Pelajaran**: event `SelectionChanged`/`PropertyChanged` yang handler-nya memakai `FindControl` WAJIB di-guard sampai constructor selesai. Smoke test lewat host Avalonia (console app di scratchpad) sekarang membuka MainWindow, Tindakan, Registrasi, Rawat Inap, Login tanpa exception.
- **Garis putih saat un-maximize**: `WindowDecorations` diganti `None` (sebelumnya `BorderOnly` — border OS itulah yang menyisakan garis putih). Karena tak ada frame OS lagi, resize dibuat manual: 8 `Border` grip transparan (4px sisi / 8px sudut) di `MainWindow.axaml` → `BeginResizeDrag` (`OnResizeGripPressed`), nonaktif saat maximized.
- **Login borderless**: `LoginWindow` juga `WindowDecorations="None"` — strip atas jadi drag handle + tombol minimize/close sendiri (gaya sama dengan header utama). Menutup login tanpa masuk = keluar aplikasi.
- Build sukses, 12 test pass, `.exe` dipublish. Belum diverifikasi visual (resize tepi/sudut perlu dicoba manual).

## Menu Utama A–U (katalog legacy), jadi halaman awal; perbaikan frame window — 2026-11-08

- **Menu Utama = halaman awal setelah login** (sama seperti legacy). "Home"/Kembali dari modul sekarang kembali ke grid ini (`ShowPage(Page.Hero)` dialihkan ke `Page.Menu`); panel hero lama tidak lagi tampil.
- **`Modules/Shared/LegacyMenuCatalog.cs` (data hasil generate)** — 21 kategori [A]–[U] persis dari `cmbMenu` di `frmUtama.java`, dan daftar tile per kategori diekstrak dari `isCombo()` (`Panelmenu.add(btnXxx)`) + label dari `btnXxx.setText(...)`. Total **1224 tile** (A=34, B=16, C=61, D=75, E=33, F=27, G=28, H=3, I=60, J=96, K=87, L=183, M=255, N=11, O=113, P=45, Q=16, R=10, S=25, T=16, U=30). Hanya key+label yang dibawa — cek hak akses legacy (`akses.getXxx()`) TIDAK diport, jadi semua tile tampil untuk semua user.
- **Tampilan**: combo "Tampilkan Menu" (lebar penuh, daftar A–U), tile dalam `UniformGrid` 5 kolom yang mengisi seluruh lebar; tinggi baris menyesuaikan viewport (120–220px). Ikon = **placeholder inisial** (permintaan user: jangan pakai ikon legacy dulu).
- **Tile yang sudah nyata** (routing di `MainWindow.OnLegacyTileClick`, by key legacy): btnRegistrasi, btnIGD, btnRalan (Tindakan Ralan), btnKamarInap, btnPasien, btnKasir, btnPermintaanLab/PA/MB, btnLaboratorium/PA/MB (periksa), btnPermintaanRadiologi, btnPeriksaRadiologi, btnPenjualan, btnDaftarPermintaanResep, btnSetupAplikasi (Pengaturan, admin), btnLaporanBulananIRJ (menu laporan kita — tidak ada tile 1:1 di legacy, ditempel di sini sementara). Sisanya (≈1200) → dialog "belum diimplementasikan".
- **Window**: hand-made resize grips DIHAPUS (diduga penyebab "double cursor": hit-test resize bawaan OS di tepi window borderless bertabrakan dengan cursor grip). Diganti bingkai 1px biru (`Border IsHitTestVisible=False`) supaya window tetap terbaca sebagai window saat tidak maximize. **Belum diverifikasi**: apakah resize bawaan OS benar-benar jalan di `WindowDecorations="None"` — kalau tidak, opsi: `WindowDecorations="BorderOnly"` (kembali ada garis putih OS) atau grip manual tanpa `Cursor` custom.
- **Bug kecil yang tertangkap lewat smoke test**: `FindResource("ThemePrimaryLightBrush")` mengembalikan `UnsetValue` (brush ada di ThemeDictionaries per varian) → pakai `GetResourceObservable` + `Bind`.
- Build sukses, 12 test pass, `.exe` dipublish. Belum diverifikasi visual.

## IGD/UGD sesuai legacy DlgIGD; window kembali BorderOnly (resize + snap) — 2026-11-08

- **IGD/UGD ternyata BUKAN layar triase** (tafsiran saya di sesi lama salah). Legacy `DlgIGD.java` = "::[ Registrasi IGD Hari Ini ]::": daftar registrasi dengan poli **`kd_poli='IGDK'` (hardcoded di query legacy)**, periode default hari ini, Key Word, kolom urut: No.Reg, No.Rawat, Tanggal, Jam, Kd.Dokter, Dokter Dituju, Nomer RM, Pasien, J.K., Umur, Poliklinik, Penanggung Jawab, Alamat P.J., Hubungan dg P.J., Biaya Registrasi, Status, Jenis Bayar, Stts Rawat, Kd PJ, Status Bayar (kolom checkbox "P" tidak dibuat). `IgdView` ditulis ulang: blok "Input Data" (No.Rekam Medik, Dr Dituju, Png. Jawab, Almt Png. Jwb, Jenis Bayar; poli dikunci IGDK) → `RegistrasiRepository.CreateAsync` (INSERT nyata ke `reg_periksa`), Cetak PDF, Semua/Cari, menu klik-kanan pasien. `RegistrasiRepository.GetByPeriodeAsync` dapat parameter opsional `kodePoli`. Hapus/Ganti tetap placeholder (sengaja: menyentuh data pasien asli).
- Form triase lama dipertahankan sebagai **`IgdTriaseView`** (file di-rename), dibuka dari tombol "🚑 Triase" di layar IGD (legacy menaruhnya di klik-kanan "Data Triase Gawat Darurat").
- **Window**: `WindowDecorations="None"` membuat window tidak bisa di-resize/di-snap → dikembalikan ke **`BorderOnly`** (border tipis OS; resize tepi + snap Win+panah/drag ke pojok kembali normal), bingkai 1px buatan dihapus. Konsekuensi yang diterima user: kemungkinan masih ada garis tipis OS di atas saat un-maximize. LoginWindow tetap `None` (tidak perlu resize).
- Smoke test host Avalonia: MainWindow (BorderOnly), IGD (1 baris hari ini dari DB live), Triase — tanpa exception. Build sukses, 12 test pass, `.exe` dipublish. Belum diverifikasi visual (snap/resize perlu dicoba manual).

## Laboratorium PK/PA/MB — Permintaan & Periksa sesuai legacy — 2026-11-09

Dikerjakan paralel oleh dua sub-agent (worktree terpisah, di-merge ke master), sumber kebenaran: `DlgCariPermintaanLab{,PA,MB}.java` (simrs-khanza/src/permintaan) dan `DlgCariPeriksaLab{,PA,MB}.java` + `DlgPeriksaLaboratorium{,PA,MB}.java` (simrskhanza).

- **Skema asli ≠ tebakan lama.** PK/PA/MB punya tabel permintaan sendiri (`permintaan_lab`/`_labpa`/`_labmb` + `permintaan_pemeriksaan_*`, detail template hanya PK & MB); hasil di `periksa_lab` (kolom `kategori` enum PA/PK/MB) + `detail_periksa_lab` (PK/MB, per item template) / `detail_periksa_labpa` (PA, naratif: makroskopik/mikroskopik/kesimpulan/kesan); kesan & saran di `saran_kesan_lab`. Pendekatan lama (tag "[PK]" di `informasi_tambahan`, hasil di `saran_kesan_lab`, `PenunjangRepository` lab) **salah dan sudah tidak dipakai** — view lama `LabPermintaanView`/`LabPeriksaView` dihapus; method lab di `PenunjangRepository` + `KategoriLab` masih ada tapi yatim (hapus saat bersih-bersih).
- **Enum bersama** `Data/LabCategory.cs { PK, PA, MB }`. Semua akses lab pakai **raw SQL berparameter** (`SqlQueryRaw`/`ExecuteSqlRawAsync`), nama tabel dipilih dari enum, bukan input user; tulis dalam transaksi.
- **Permintaan Lab** (`PermintaanLabListView(LabCategory)`, `LabPermintaanRepository`, `PermintaanLabReport`): tab Rawat Jalan/Rawat Inap × "Data Permintaan"/"Detail Permintaan", kolom per kategori (PA + kolom spesimen), filter dokter/poli(kamar)/Key Word/periode (default hari ini), Hapus (ditolak bila sudah sampel kecuali admin, atau item sudah dibayar), Sampel, Hasil (→ form Periksa terisi dari permintaan), Semua, Cetak PDF, Keluar. Disederhanakan: tabel bersarang legacy dipecah jadi dua view datar; kolom Metode tak ada (live `template_laboratorium` tidak punya `method`); Cetak = tabel PDF, bukan Jasper; tombol bridging LIS (LICA/Sysmex/ELIMS/…), barcode, dan popup cetak setelah sampel TIDAK diport.
- **Periksa Lab** (`PeriksaLabListView`, `PeriksaLabFormView`, `LabPeriksaRepository`, `PeriksaLabReport`, `LabUiHelpers`): daftar "Data Pemeriksaan Laboratorium …" bersarang, Kesan & Saran, update hasil PA, Hapus, Cetak, dan **form input hasil** (pilih pasien/dokter perujuk/petugas, pemeriksaan dari `jns_perawatan_lab` per kategori, nilai per template, nilai rujukan L/P × dewasa/anak sesuai aturan legacy, biaya). Simpan menulis `periksa_lab` + detail, menandai `tgl_hasil` pada permintaan, mengisi antrian `antrilab*2`; ditolak bila kunjungan sudah ada billing/Batal. Dilewati: jurnal akuntansi (`tampjurnal`/`jurnal`) baik saat Simpan maupun Hapus, tab Detail Kunjungan (HTML), menu Ubah, Nota, cetak lembar hasil, ranap_gabung, mode parsial, impor LIS.
- **Routing** (`MainWindow.OpenLabTileAsync` → `OpenPermintaanLabAsync`/`OpenPeriksaLabAsync`/`OpenPeriksaLabFormAsync`): tile Menu Utama (btnPermintaanLab/PA/MB, btnLaboratorium/PA/MB) dan tombol toolbar Laboratorium; "Hasil" → form → Simpan kembali ke daftar Periksa; Kembali → Menu Utama. `IsAdminUtama` diset bila UserId = "admin".
- **Verifikasi**: build + 12 test lolos; query baca diuji ke DB live (PK ralan 1000/ranap 932 baris dalam 1 tahun, PA 3/9, MB 0; periksa PK/PA/MB); semua view + form dibangun tanpa exception di host Avalonia. Uji tulis Periksa dilakukan di **MariaDB lokal sementara (struktur salin tanpa data)** — 32 pengecekan lolos, termasuk rollback. Hapus/Sampel di Permintaan hanya ditinjau lewat kode (uji rollback ke DB live diblokir pengaman). **Jangan uji tulis ke pasien riil**; pakai data test.
- **Belum dibangun**: form BUAT permintaan baru (legacy `DlgPermintaanLaboratorium`, dibuka dari menu klik-kanan pasien), jurnal akuntansi lab.

## Radiologi (Permintaan + Periksa), Keluar → login, title bar extended — 2026-11-09

- **Permintaan Radiologi** (`PermintaanRadiologiListView`, `RadiologiPermintaanRepository`, `PermintaanRadiologiReport`): meniru `DlgCariPermintaanRadiologi` — tab Rawat Jalan/Rawat Inap × "Data Permintaan"/"Item Permintaan", Hapus (tolak bila sudah sampel kecuali admin / item sudah dibayar), Sampel (ralan juga mengaktifkan ulang `antriradiologi`), Hasil, Semua, Cetak PDF. Catatan: di source legacy handler tombol "Sampel" dan "Hasil" tertukar; label mengikuti yang tampil di layar legacy. Tidak diport: bridging RIS, barcode, lembar cetak. Filter dokter/poli = kotak teks. Live: 705 (ralan) / 52 (ranap) permintaan setahun.
- **Periksa Radiologi** (`PeriksaRadiologiListView`, `PeriksaRadiologiFormView`, `RadiologiPeriksaRepository`): daftar "Data Pemeriksaan Radiologi" (filter No.Rawat/Tanggal/Pasien/Petugas/Key Word + filter Sudah/Belum Keluar Bacaan; baris bersarang Proyeksi & Dosis Radiasi / Kode Periksa / Nama Pemeriksaan / Biaya / Cara Bayar, baris BHP, baris Total), editor hasil bacaan, panel Ubah P.J./Perujuk/Petugas, Hapus (`periksa_radiologi`, `hasil_radiologi`, `gambar_radiologi`, `beri_bhp_radiologi`). Form input: proyeksi/kV/mAS/FFD/BSF/Inak/Jml Penyinaran/Dosis, grid BHP, hasil + template; tarif ikut 4 varian `set_tarif`; Simpan menulis `periksa_radiologi`, `beri_bhp_radiologi`, `hasil_radiologi`, menandai `tgl_hasil` pada permintaan dan `antriradiologi2` (ralan); ditolak untuk kunjungan berbilling/Batal. Dilewati: jurnal akuntansi, Nota, tab foto/DICOM & upload URL, impor Fuji/RIS, cetak hasil model 1–3, menu klik-kanan. BHP hanya terverifikasi di MariaDB lokal sementara (di DB live baris BHP hampir semuanya yatim).
- Uji tulis hanya di MariaDB lokal sementara (schema-only, 36 cek lolos); Hapus/Sampel permintaan hanya ditinjau lewat kode. **Jangan uji tulis ke pasien riil.**
- View lama `PermintaanRadiologiView`/`PeriksaRadiologiView` (tebakan lama) dihapus; routing: `MainWindow.OpenPermintaanRadiologiAsync` / `OpenPeriksaRadiologiAsync` / `OpenPeriksaRadiologiFormAsync` ("Hasil" → form terisi → Simpan kembali ke daftar Periksa).
- **Keluar (toolbar)** kini kembali ke **login** (`MainWindow.LogoutRequested`; `App.ShowLogin` membuat login baru dan menjadikannya MainWindow SEBELUM jendela lama ditutup, supaya app tidak shutdown). Tombol ✕ Windows tetap menutup aplikasi.
- **Title bar**: tombol caption buatan dihapus; `WindowDecorations="Full"` + `ExtendClientAreaToDecorationsHint="True"` + `ExtendClientAreaTitleBarHeightHint="-1"` → tombol Windows asli (resize tepi, snap, snap layouts) digambar di pojok kanan header biru; kolom kanan header menyisakan 140px. **Belum diverifikasi visual** (warna ikon caption mengikuti tema sistem). Fallback: kembali ke `WindowDecorations="BorderOnly"`.
- Housekeeping: worktree sementara fork masih ada di `.claude/worktrees` (repo ini) dan satu worktree nyasar di repo legacy `simrs-khanza/.claude/worktrees/agent-a00163d0b3068de5b` (kosong/bersih, tidak bisa saya hapus karena pengaman — hapus manual dengan `git worktree remove --force` + `git branch -D`). `.claude/` belum di-.gitignore.

## Anjungan & Antrian — papan antrian native — 2026-11-09

- **Arsitektur Khanza asli = hybrid**: menu "Anjungan & Antrian" (`frmUtama.java`, `MnAnjungan`, `MnRekapHadir*`, `MnAntrian*`, `MnInfoLaborat2`, `MnInfoBed4`) menjalankan jar Java terpisah (`anjunganmandiri`, `KhanzaCetakAntrianLoket`, `antrianloket`, `KhanzaAntrianLoket2`, `antrianpoli`) ATAU membuka halaman PHP di `webapps` (`poli.php`, `antrianradiologi.php`, `antrianlaborat*.php`, `antrianfarmasi*.php`, `berkasrawat`) via `Valid.panggilUrl`. Halaman PHP polling `data_antrian*.php` dan membunyikan `bell.wav`/`bell2.wav` bila tabel penanda (`antriradiologi`, `antriradiologi2`, `antrilab*`, `antriapotek*`, `antripoli`) berisi '1', lalu mereset penanda. **App kita murni C# (.NET 10 + Avalonia), tanpa Java/PHP**; tapi menulis tabel penanda yang sama (tombol Sampel/Hasil Lab & Radiologi), jadi papan PHP lama tetap jalan berdampingan.
- **Native** (`Modules/Antrian/`: `AntrianBoardWindow`, `AntrianBoards`, `AntrianLoketWindow`; `Data/AntrianRepository.cs`; `Assets/bell*.wav`; paket `System.Windows.Extensions` untuk `SoundPlayer` — bel hanya Windows): papan Antrian Unit/Poliklinik 2 (jadwal; dobel-klik baris → antrian per dokter), Apotek 2 / 2 Racikan / 2 Non Racikan, Laborat PK/PA/MB, Radiologi, dan Antrian Loket (operator pilih loket 1–9 + nomor → ganti satu baris `antriloket`; display polling 5 dtk, bel saat panggilan berubah; **suara MP3 `./suara/*.mp3` belum diport**). Jendela terpisah (untuk monitor ke-2); membuka papan yang sama dua kali hanya mengaktifkan jendela lama.
- **Menu**: tombol topbar "Anjungan & Antrian" membuka dropdown (`MainWindow.ShowAntrianMenu`) dengan susunan seperti legacy (submenu Apotek/Laborat); item tanpa versi native → dialog "belum diimplementasikan".
- **Belum**: Anjungan Registrasi Mandiri (kiosk penuh), Cetak Antrian Loket (kiosk: nomor 3 digit = max hari ini+1 ke `antriloketcetak` + tiket Jasper — layak dibuat nanti sebagai tiket QuestPDF), Antrian Loket 2 (per-loket + "Ulangi"), Antrian Unit/Poliklinik versi jar (konsol operator 3 kolom yang menulis `reg_periksa.stts`/`jam_reg` pada registrasi riil — butuh spesifikasi eksplisit), Apotek versi 1, Verifikasi Digital Klaim (halaman web `berkasrawat`).
- **Perhatian**: papan native & PHP yang jalan bersamaan berebut mereset penanda yang sama (hanya satu yang berbunyi); di `antriapotek2/3` dan `antripoli` reset = `update … set status='0'` tanpa WHERE (meniru PHP). Papan native me-reset penanda pada DB LIVE saat dibuka — jangan dibuka untuk "coba-coba" di jam operasional kalau papan lama dipakai. Kueri baca sudah diverifikasi ke DB live dengan `resetFlags:false`; logika reset hanya diuji di MariaDB lokal sementara (28 cek). Visual belum dicek.
- **Satu Sehat (belum dikerjakan, hanya estimasi)**: ~50 tabel `satu_sehat_*` sudah ada di skema (encounter, condition, careplan, procedure, observasi TTV, lab, radiologi, obat, mapping lokasi/tindakan); legacy memakai layanan latar belakang `KhanzaHMSServiceSatuSehat` + `bridging/ApiSatuSehat.java`. Urutan yang disarankan: token OAuth2 → IHS pasien via NIK → Encounter → Condition → TTV → Procedure → CarePlan; uji di sandbox dulu; `SimrsKhanza.Integrations` masih kosong.

## Penutup sesi: tema kompak global, header tabel biru, window BorderOnly — 2026-11-09

- **Tema kompak global di `App.axaml`** (model: layar IGD): font dasar Segoe UI 11 (`Window` style, diwariskan), TextBox/ComboBox/DatePicker/Button/TabItem FontSize 11 + MinHeight 24, `DataGrid` FontSize 11 + RowHeight 22 + garis grid lembut (`ThemeBorderBrush`), tab semi-bold. Atribut lokal (FontSize/RowHeight di kontrol) tetap menang, jadi papan antrian layar-besar (FontSize 20/RowHeight 40) tidak ikut mengecil. Style lokal per-view (Registrasi/IGD/Lab/Radiologi) dibiarkan, kini redundan dengan global — boleh dihapus saat bersih-bersih.
- **Header kolom tabel biru** (`DataGridColumnHeader`: latar `ThemePrimaryBrush`, teks putih semi-bold, pemisah biru terang) di semua DataGrid. **Judul kepotong** diperbaiki oleh `Modules/Shared/DataGridHeaderFit` (didaftarkan di `App.Initialize`): saat DataGrid mana pun `Loaded`, `MinWidth` tiap kolom dinaikkan sampai muat teks header + ruang glyph sort (grid jadi bisa scroll horizontal, bukan memotong). Terverifikasi di host: mis. "No.Reg" 40→67, "Kode Dokter" 60→93.
- **Window**: `ExtendClientArea` (tombol Windows asli di header) ternyata membuat window tidak bisa di-resize/snap menurut user → kembali ke **`WindowDecorations="BorderOnly"`** (border tipis OS; resize tepi + snap) dengan tombol caption buatan (min/max-restore/close) di header biru, drag header = `BeginMoveDrag` (snap ke tepi/pojok lewat move-loop OS), dobel-klik = maximize/restore. Kemungkinan garis tipis putih OS saat un-maximize muncul lagi (ongkos border OS — diterima user sebelumnya). Login tetap borderless (`None`). Toolbar "Keluar" = logout ke login; ✕ caption = tutup aplikasi.
- `.claude/` ditambahkan ke `.gitignore`. Worktree sementara sub-agent masih terdaftar (`git worktree list`); hapus dengan `git worktree remove --force <path>` + `git branch -D worktree-agent-*` bila mau. Satu worktree nyasar kosong juga ada di repo legacy `simrs-khanza/.claude/worktrees/agent-a00163d0b3068de5b` (tidak bisa saya hapus).
- Build sukses, 12 test pass, `.exe` dipublish. **Belum diverifikasi visual**: tampilan tema baru di semua layar, warna header, snap/resize window.

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
