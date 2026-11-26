# Laporan Audit — Repository `simrs-khanza`
Tanggal: 2026-11-03 • Mode: Read-only (tidak ada file yang diubah/dihapus) • Total ukuran working tree: **~3.7 GB**, 2.143 commit, branch `master`

---

## 0. TEMUAN KRITIS — Baca Ini Dulu

1. **Ini bukan repository source-code bersih — ini direktori kerja/produksi yang di-commit apa adanya.** Tidak ada `.gitignore` sama sekali, sehingga cache, hasil compile, dan **data pasien sungguhan** semuanya masuk ke git history (2.143 commit).
2. **Data pasien (PHI) nyata ditemukan tercommit**, contoh konkret:
   - `sik.sql` (12.8 MB), `sik_bridging_lab.sql`, `sik_bridging_radiologi.sql` — dump database di root, kemungkinan berisi data pasien asli.
   - `webapps/billing/temp/` berisi gambar tanda tangan PNG dengan **nama pasien asli** sebagai nama file.
   - Ratusan file laporan (`.html/.csv/.wps/.pdf`) di root dengan nama seperti `RPP20250618000001.pdf` (resep pasien), `DataPenilaian*.html`, dll.
   - `resumepasien/`, `report/` kemungkinan juga memuat output per-pasien.
3. **Kredensial database & API tersimpan plaintext di source control**, ditemukan di:
   - `setting/database.ini` (host/user/pass plaintext)
   - `webapps/conf/conf.php` dan seluruh salinannya di modul lain (mysqli ke db `sik`, user `root`)
   - Konstanta `USERHYBRIDWEB`/`PASHYBRIDWEB` hardcoded di `conf.php`
   - `setting/database.xml` (Java) — terenkripsi AES, tapi kunci/metode dekripsi ada di source (`fungsi.AESsecurity`), jadi secara efektif tetap "secret in repo".
4. **Proyek Java utama (`src/`) tidak bisa di-build apa adanya** — 280 referensi JAR pihak ketiga di `nbproject/project.properties` menunjuk ke path absolut di komputer developer lama (macOS), tidak ada satupun JAR itu ikut tercommit.
5. Konsekuensi: **Tidak ada operasi destruktif (hapus file, rewrite git history, dsb.) yang akan dilakukan tanpa konfirmasi eksplisit Anda per kategori**, dan isi file-file sensitif di atas tidak akan diunggah/dipublikasikan ke layanan pihak ketiga mana pun.

---

## 1. Identifikasi Teknologi & Entry Point

Repo ini berisi **dua aplikasi utama** plus puluhan modul satelit:

| Aplikasi | Teknologi | Entry point | Catatan |
|---|---|---|---|
| **Desktop client utama "SIMRS Khanza"** | Java Swing, NetBeans + Ant (`build.xml`, `nbproject/`) | `simrskhanza.SIMRSKhanza` (class `main`) | Target Java 8. 1.678 file `.java`, 31 package. Tidak bisa dibuild tanpa merekonstruksi classpath 280 JAR. |
| **Web app** (`webapps/`) | PHP prosedural murni, tanpa framework | Puluhan entry file lepas (`antrian.php`, `apiteras.php`, dll) + ~100 folder modul (masing-masing `pages/index.php`) | Tidak ada composer/router — pola "satu folder per fitur". |
| **Portal web terpisah**: `edokter`, `eeksekutif`, `emcu`, `epasien`, `esign` | PHP, pola sama (conf/css/images/js/pages + login.php) | masing-masing `pages/login.php` | Portal dokter, eksekutif, MCU, pasien, e-sign — tiap satu aplikasi PHP berdiri sendiri 35-56MB. |
| **Modul satelit Java/NetBeans** (`Khanza*`) | Java Swing, masing-masing proyek NetBeans sendiri | kelas main masing-masing (belum ditelusuri satu per satu) | Antrian kiosk, fingerprint anjungan, auto-verify BPJS, resume cetak, bridging BPJS/JKN/SatuSehat/PCare/SPDGT/Aplicare, enkripsi teks, updater. |
| **Modul Delphi/Pascal**: `alarm` | Delphi/Pascal (.pas/.dfm/.dpr) | project `.dpr` | Util alarm desktop, compile artifact `.exe`/`.dcu` ikut tercommit. |
| **`presensi`** | Tidak diketahui (diduga Delphi, sejenis `alarm`) | — | **Source TIDAK DITEMUKAN** — hanya ada binary/resource. Tidak bisa direkonstruksi tanpa source asli. |
| **`driverFlexCode`** | Native Windows installer/SDK pihak ketiga | `Setup.exe` | Bukan kode kita — driver fingerprint/barcode reader. |
| **Bridging kecil PHP**: `api-bpjsfktl`, `api-bpjsfktp`, `api-bridgingradiologi`, `bankjateng`, `bankpapua`, `bjb`, `mandiri` | PHP | masing-masing entry sendiri | Integrasi BPJS, radiologi, dan pembayaran bank. |

---

## 2. Inventarisasi Modul (Java `src/`, 31 package)

| Package | Fungsi (perkiraan) | #File |
|---|---|---|
| simrskhanza | Shell aplikasi utama / entry point | 72 |
| bridging | Bridge sistem eksternal (lab analyzer, BPJS, dll) | 288 |
| rekammedis | Rekam medis | 252 |
| keuangan | Keuangan/akuntansi | 169 |
| grafikanalisa | Grafik/analitik statistik | 148 |
| inventory / inventaris | Inventaris (dua package tumpang tindih) | 118 / 35 |
| laporan | Reporting | 108 |
| kepegawaian | HR/kepegawaian | 73 |
| fungsi | Util bersama (termasuk `koneksiDB`) | 47 |
| surat | Surat/sertifikat | 47 |
| ipsrs | Pemeliharaan fasilitas | 44 |
| widget | Widget UI | 38 |
| dapur | Gizi/dapur | 36 |
| toko | Retail/apotek toko | 35 |
| setting | UI pengaturan aplikasi | 34 |
| viabarcode | Barcode | 24 |
| restore | Backup/restore DB | 18 |
| permintaan | Workflow permintaan | 17 |
| perpustakaan | Perpustakaan | 16 |
| ziscsr | Zakat/CSR | 15 |
| pcraicra | Diduga skoring ICU/PCRA | 12 |
| informasi | Layar info/display | 11 |
| tranfusidarah | Transfusi darah | 10 |
| smsui/smsobj/smsservice | Subsistem notifikasi SMS | 6/1/1 |
| parkir | Parkir | 3 |
| 48x48, picture, smsimage | Resource ikon/gambar (bukan source) | 0 |

**Report templates**: `report/` = 1.313 file `.jrxml` (JasperReports, source cetak — **referensi UI wajib dipertahankan**) + 1.313 file `.jasper` (compiled, regenerable).

## 3. Inventarisasi Modul Satelit (Top-Level)

| Direktori | Tech | Source vs Artifact | Ukuran |
|---|---|---|---|
| `KhanzaAntrianApotek/Loket/Loket2/Poli`, `KhanzaCetakAntrianLoket` | Java/NetBeans | Source asli + `.class/.jar` ikut tercommit | 7-8M masing-masing |
| `KhanzaHMSAnjungan[FingerPrint]`, `KhanzaHMSAutoVerify[2]`, `KhanzaHMSResume` | Java/NetBeans | sama | 4.6-11M |
| `KhanzaHMSServiceAplicare/Mandiri/MobileJKN[ERM/FKTP]/PCare/SIRSYankes/SPDGT/SatuSehat` | Java/NetBeans | Source asli | 0.5-1.6M |
| `KhanzaPengenkripsiTeks`, `KhanzaSecurity16bit`, `KhanzaUpdater` | Java/NetBeans | Source asli | kecil |
| `alarm` | Delphi | Mixed source+binary (+ `data.mdb`) | 4.3M |
| `presensi` | ? | **Hanya binary, source hilang** | 7.9M |
| `driverFlexCode` | Native binary pihak ketiga | **Bukan source kita** | 21M |
| `report` | JasperReports | Source (.jrxml) + artifact (.jasper) | 105M |
| `resumepasien` | Java | App + **110 JAR vendor tercommit** (kandidat cleanup terbesar) | 83M |
| `cache` | — | Runtime cache murni | 105M |
| `gambar`, `gambarradiologi`, `suara` | Asset | **UI asset, pertahankan** | 1.9-2.7M |
| `setting` | Config | **Berisi kredensial plaintext** | 210K |
| `edokter`, `eeksekutif`, `emcu`, `epasien`, `esign` | PHP | Source (portal web mandiri) | 35-56M |
| `api-bpjsfktl/fktp`, `api-bridgingradiologi`, `bankjateng`, `bankpapua`, `bjb`, `mandiri` | PHP | Source kecil, kredensial di `conf.php` | 36-193K |
| `wearable` | Java | Source kecil | 214K |

## 4. Dependensi

- **Java (`src/`)**: 280 JAR eksternal dideklarasikan tapi **tidak ada yang ikut tercommit** — classpath harus direkonstruksi dari nol (Batik, barcode4j, Apache HttpComponents, driver MySQL Connector/J lama `com.mysql.jdbc.jdbc2.optional.MysqlDataSource`, dll). Ini blocker utama untuk membangun ulang/menjalankan versi lama sebagai referensi hidup.
- **`resumepasien/lib/`**: 110 JAR vendor (Ant, Antlr, HttpClient, dll) tercommit langsung — seharusnya dependency, bukan source.
- **PHP (`webapps/` & portal lain)**: tidak ada Composer; dependency vendored manual (`assets/vendor/bootstrap`, `jquery.min.js`, `phpqrcode/`).
- **Tidak ada package.json/node_modules** ditemukan.

## 5. Integrasi Database

- **MySQL/MariaDB** sebagai DB utama, nama db `sik` (cocok dengan `sik.sql` di root).
  - Java: via `fungsi.koneksiDB`, JDBC mentah, driver MySQL Connector/J versi lama, tanpa ORM/DAO — pola "satu class koneksi global" dipakai di seluruh 1.678 file.
  - PHP: `mysqli_connect` langsung di setiap `conf.php` (duplikat di puluhan modul), sanitasi manual via `mysqli_real_escape_string` + whitelist regex (bukan prepared statement) — berisiko SQL injection di beberapa titik (ditemukan contoh string-concat SQL).
- **Konfigurasi/feature-flag** (Java): ~130 kredensial & parameter integrasi (BPJS, SatuSehat, PCare, Mobile JKN, Aplicare, SIRS, Dukcapil, SFTP Bank Mandiri, eSign/SertiSign, PACS Orthanc) disimpan flat di `setting/database.xml`, dienkripsi AES, **dibaca ulang dari disk setiap kali getter dipanggil (tidak ada caching)** — catatan desain penting untuk layanan konfigurasi di rewrite.
- **Dump SQL tercommit**: `sik.sql` (12.8MB), `sik_bridging_lab.sql`, `sik_bridging_radiologi.sql` — berpotensi memuat skema + data produksi sekaligus. **Tidak dibuka/dibaca detail isinya dalam audit ini demi privasi pasien** — hanya dikonfirmasi keberadaannya.

## 6. Pemetaan UI ↔ Backend

| Layer UI | Lokasi | Backend terkait | Harus dipertahankan untuk rewrite? |
|---|---|---|---|
| Layout/form Java Swing | `src/*/  ` (ratusan `*.java` dengan GUI builder NetBeans, pola nama `frm*.java`/`panel*.java` umum di aplikasi sejenis) | `fungsi.koneksiDB` + package masing-masing modul | **Ya** — referensi utama tampilan desktop |
| Print layout | `report/*.jrxml` (1.313 file) | Dipanggil dari Java via JasperReports API | **Ya** — ini definisi visual laporan cetak, krusial untuk kesamaan tampilan |
| Halaman web modul | `webapps/*/pages/*.php`, portal `edokter/eeksekutif/emcu/epasien/esign` | `conf/conf.php` → `mysqli` ke db `sik` | **Ya** — referensi tampilan web (warna, form, navigasi) |
| Styling | `webapps/assets/css`, per-modul `css/` | — | **Ya**, kecuali vendor (`bootstrap`, `jquery`) yang akan diganti komponen native Avalonia |
| Ikon/gambar/suara | `gambar/`, `gambarradiologi/`, `suara/`, `src/picture`, `src/48x48`, `src/smsimage` | Dirujuk langsung oleh kode Java/PHP | **Ya** — aset UI |
| Data pasien ter-upload | `webapps/billing/temp/`, `medrec/pages/upload/`, `penggajian/upload/`, kemungkinan isi `resumepasien/`, `report/` output | — | **Tidak relevan untuk migrasi kode** — ini data runtime, bukan source; harus dikeluarkan dari proses migrasi source code (tapi tetap data produksi yang harus dijaga, bukan dihapus sembarangan) |

---

## 7. Daftar Kandidat Cleanup

> **UPDATE 2026-11-03**: Item #1-6 (kategori risiko rendah) **SUDAH DIEKSEKUSI** atas konfirmasi user — lihat detail di `HANDOVER.md`. ~2.615 file terhapus dari working tree saja (belum di-commit/push, masih bisa dibatalkan via `git checkout -- .`). Item #7-11 BELUM disentuh.

> Catatan: menghapus dari *working tree* tidak otomatis menghapus dari *git history*. Karena kemungkinan ada PHI di history, penghapusan history (misal `git filter-repo`) adalah operasi jauh lebih berat yang **butuh keputusan terpisah dan sangat hati-hati** (lihat §9).

| # | Kandidat | Alasan | Risiko jika dihapus | Perkiraan ukuran dibebaskan |
|---|---|---|---|---|
| 1 | `report/*.jasper` (1.313 file) | Compiled binary, regenerable dari `.jrxml` yang masih ada | Rendah — bisa di-compile ulang dari source `.jrxml` kapan saja | besar bagian dari 105M |
| 2 | `resumepasien/lib/*.jar` (110 file) | Vendor JAR pihak ketiga, bukan source | Rendah jika hanya untuk tujuan "jadikan referensi source", tapi app lama jadi tidak jalan tanpa ini — perlu disimpan di luar git (artifact repo) kalau masih dipakai | sebagian besar dari 83M |
| 3 | `cache/` (seluruh isi) | Runtime cache murni | Rendah | 105M |
| 4 | `driverFlexCode/` (installer/binary pihak ketiga) | Bukan source kita, driver hardware vendor | Rendah — bisa didownload ulang dari vendor bila perlu | 21M |
| 5 | `alarm/*.dcu`, `*.exe`, `*.~dpr/*.~ddp/*.~pas` (backup editor) | Compiled artifact & file backup editor | Rendah | kecil |
| 6 | Semua `*.class`/`*.jar` hasil compile di tiap modul `Khanza*` | Regenerable dari `.java` yang ada | Rendah | beberapa MB per modul |
| **1-6** | ✅ **SUDAH DIEKSEKUSI** (working tree saja, belum commit) | — | — | ~300MB dibebaskan, 3.7G→3.4G |
| 7 | Ratusan file laporan lepas di root (`.html/.csv/.wps`, hasil export) | Output runtime, bukan source — **tapi berpotensi berisi data pasien** | **Perlu klasifikasi per-file dulu**, jangan hapus massal sebelum dicek mana yang benar contoh dummy vs data asli | bervariasi |
| 8 | `sik.sql`, `sik_bridging_*.sql` | Dump DB — **sangat sensitif (PHI)** | **TIDAK direkomendasikan dihapus dari working tree saat ini tanpa rencana penyimpanan aman terpisah** — ini mungkin satu-satunya referensi skema DB yang Anda punya. Rekomendasi: pindahkan ke storage aman di luar git, lalu hapus dari repo (bukan sekadar working tree) | 12.8MB+ |
| 9 | PDF/PNG per-pasien (`RPP*.pdf`, `billing/temp/*.png`, dll) | **Data pasien asli (PHI)** | **Jangan dihapus sembarangan** — ini bisa jadi satu-satunya kopi; perlu kebijakan retensi data medis resmi (sering ada aturan hukum minimum penyimpanan), bukan keputusan teknis semata | — |
| 10 | `presensi/` (binary tanpa source) | Tidak bisa dipakai sebagai referensi kode (hanya bisa dicoba reverse-engineer sebagai black box bila benar-benar perlu) | Keputusan Anda — tidak menghalangi rewrite, tapi hilang referensi perilaku modul ini | 7.9M |
| 11 | `Claude outputs/menampilkanPenilaianBishop.java`, `rptFormulirPenilaianBishop.jrxml` | Sisa WIP sesi sebelumnya, `.jrxml`-nya duplikat dengan `report/rptFormulirPenilaianBishop.jrxml` yang sudah final | Perlu konfirmasi: apakah fitur BISHOP Score ini sudah selesai/dibatalkan? | kecil |

**Yang TIDAK saya rekomendasikan untuk cleanup sama sekali** (ini adalah referensi UI/UX utama sesuai permintaan Anda): semua `.jrxml`, seluruh `src/*.java`, seluruh `webapps/*/pages/*.php`, `css/`, `gambar*/`, `suara/`, `picture/`, `48x48/`, `smsimage/`.

---

## 8. Masalah Keamanan Teridentifikasi (di luar scope perbaikan, dicatat untuk kesadaran)
- Kredensial DB plaintext di `setting/database.ini`, `webapps/conf/conf.php` (dan duplikatnya di ~15 modul), hardcoded webhook creds `USERHYBRIDWEB`/`PASHYBRIDWEB`.
- SQL dibangun via string-concat di beberapa tempat (potensi SQL injection) — bukan prepared statement.
- Tidak ada `.gitignore` → risiko kebocoran data pasien & kredensial terus bertambah selama repo ini masih dipakai apa adanya.
- **Rekomendasi**: jangan push repo ini ke remote publik mana pun dalam kondisi saat ini.

---

## 9. Rekomendasi Struktur Proyek Baru (C# + Avalonia UI)

Tujuan: mempertahankan tampilan/UX, mendesain ulang backend & arsitektur.

```
SimrsGramedika.sln
├── src/
│   ├── SimrsGramedika.UI/           # Avalonia UI (views, dipetakan 1:1 dari layout Swing/PHP lama per modul)
│   │   ├── Modules/
│   │   │   ├── RekamMedis/          # ganti nama Indonesia tetap dipertahankan agar mudah dilacak ke modul lama
│   │   │   ├── Keuangan/
│   │   │   ├── Kepegawaian/
│   │   │   ├── Inventaris/
│   │   │   ├── Laporan/
│   │   │   ├── Bridging/            # BPJS, SatuSehat, PCare, dst — masing-masing sub-folder per integrasi
│   │   │   └── ... (mengikuti 31 package Java + modul web sejenis)
│   │   ├── Assets/                  # dipindahkan dari gambar/, suara/, picture/, 48x48/, css asli sbg referensi warna/ikon
│   │   └── ReportTemplates/         # migrasi dari .jrxml → format cetak Avalonia (QuestPDF/Avalonia.Controls.PrintToDocument, dipetakan per-laporan)
│   ├── SimrsGramedika.Core/         # Domain model & business logic
│   ├── SimrsGramedika.Data/         # Data access layer untuk MySQL/MariaDB
│   ├── SimrsGramedika.Config/       # Pengaturan koneksi per pengguna
│   └── SimrsGramedika.Tests/        # Unit, UI headless, dan tes database opsional
├── docs/
│   ├── audit/                       # laporan ini + handover, dipindahkan ke sini setelah disetujui
│   ├── legacy-reference/            # cross-reference modul lama ↔ modul baru (tabel pemetaan)
│   └── migration-plan.md
├── legacy-reference/                 # (opsional, read-only) salinan ringkas .jrxml/.php/.java asli utk dibuka saat rewrite, TANPA data pasien/kredensial
└── .gitignore                        # WAJIB ada sejak awal: cache/, *.jasper (jika di-generate build), data pasien, kredensial
```

Prinsip kunci:
- **Satu modul lama = satu folder modul baru**, nama domain Indonesia dipertahankan agar mudah ditelusuri balik ke kode asli selama migrasi.
- **Pisahkan tegas**: UI (Avalonia) ↔ Core/domain ↔ Data access ↔ Integrations ↔ Config/secrets. Tidak ada kredensial di source.
- **Report migration**: setiap `.jrxml` dipetakan manual ke template cetak baru; `report/` dijadikan referensi visual (dibuka berdampingan, tidak di-port otomatis).
- **Tidak ada data pasien/kredensial ikut ke repo baru** — kalau perlu data contoh untuk testing, buat data dummy/anonim baru.

## 10. Rencana Migrasi Bertahap (usulan)

1. **Fase 0 — Persiapan & keamanan (sebelum coding apa pun)**
   - Putuskan nasib file sensitif (§7 #8-9) dengan tim/manajemen RS (bukan keputusan teknis semata — ada aspek hukum rekam medis).
   - Buat `.gitignore` baru + pertimbangkan repo baru yang bersih (riwayat git lama tetap disimpan sbg arsip, tidak dihapus tanpa rencana retensi resmi).
   - Inventaris kredensial produksi asli (di luar chat ini, langsung oleh tim RS) dan rotasi bila repo sempat bocor ke pihak luar.
2. **Fase 1 — Referensi UI/UX**
   - Screenshot/dokumentasikan setiap layar Swing & halaman PHP per modul (mapping di §6) sebagai acuan desain Avalonia — gunakan file ini sebagai starting checklist per modul.
   - Ekstrak palet warna, font, ikon dari `gambar/`, css, dan Swing LookAndFeel yang dipakai.
3. **Fase 2 — Fondasi teknis baru**
   - Setup solution C#/Avalonia + Core/Data/Integrations/Config sesuai struktur §9.
   - Desain skema DB baru (bisa reuse skema `sik.sql` yang sudah dianonimkan untuk DEV, bukan data produksi asli).
   - Bangun Config/Secrets service pengganti `database.xml`/`conf.php`.
4. **Fase 3 — Migrasi modul inti lebih dulu** (urutan disarankan berdasar ukuran/kepentingan): `rekammedis` → `keuangan` → `kepegawaian` → `laporan` → `inventaris`/`inventory` → sisanya.
   - Tiap modul: port logic dari Java/PHP ke C# Core, bangun UI Avalonia yang menyamai layout lama, tulis test dasar.
5. **Fase 4 — Migrasi integrasi eksternal** (`bridging`, `KhanzaHMSService*`, `api-*`, bank): paling berisiko karena tergantung kredensial & API pihak ketiga yang masih aktif — migrasi terakhir & paling hati-hati, idealnya dites di lingkungan sandbox masing-masing provider (BPJS/SatuSehat punya sandbox resmi).
6. **Fase 5 — Modul satelit kiosk/device** (`KhanzaAntrian*`, `KhanzaHMSAnjungan*`, `alarm`, `presensi`, `driverFlexCode`): evaluasi apakah masih perlu dipertahankan sebagai app terpisah atau diintegrasikan ke aplikasi utama baru.
7. **Fase 6 — Cutover & validasi paralel**: jalankan sistem baru berdampingan dengan lama, bandingkan output laporan (`.jrxml` lama vs cetak baru) sebelum go-live penuh.

---

## 11. Yang Belum Dilakukan / Perlu Keputusan Anda

- [ ] Konfirmasi: file mana di §7 yang boleh dihapus dari working tree (per nomor).
- [ ] Konfirmasi: apakah perlu dibuatkan repo baru yang bersih (tanpa data pasien di history) untuk kerja rewrite, dan bagaimana riwayat lama diarsipkan.
- [ ] Konfirmasi: nasib file `Claude outputs/` (WIP BISHOP Score — lanjutkan, integrasikan ke `src/`, atau buang).
- [ ] Keputusan non-teknis (bersama tim RS/legal): kebijakan retensi data pasien di `sik.sql`, PDF resep, dsb.

**Tidak ada tindakan destruktif yang telah dieksekusi.** Laporan ini + `HANDOVER.md` adalah deliverable audit; menunggu arahan Anda untuk langkah berikutnya.
