# Handover — Audit Repo SIMRS Khanza (menuju rewrite C# Avalonia)

Dokumen ini diupdate setiap ada progres. Jangan dihapus — ini adalah log kerja agar sesi berikutnya bisa lanjut tanpa mengulang dari awal.

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

## Belum dilakukan (menunggu keputusan user)
- Commit pertama di repo baru `SimrsKhanza.Avalonia` (sudah di-stage, belum di-commit).
- Isi nyata MainWindow/dashboard/login/dll — scaffold saat ini masih kosong (template default), belum ada port logic dari legacy sama sekali (sesuai instruksi awal: belum mulai rewrite/implementasi).
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
