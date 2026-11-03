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

## Belum dilakukan (menunggu keputusan/aksi user)
- Commit ke-4 untuk semua perubahan di atas — belum di-commit.
- **MariaDB perlu di-install sebagai service permanen oleh user sendiri** (butuh admin elevation yang sesi ini tidak punya) kalau mau tetap jalan setelah reboot.
- Belum ada verifikasi visual/klik manual di UI (dashboard → Cari Dokter → lihat hasil dari DB asli) — hanya diverifikasi lewat integration test otomatis, bukan observasi GUI langsung.
- Keputusan arsitektur "formulir dinamis" (gantikan 177 tabel penilaian/skrining/surat) — masih menunggu persetujuan, belum dieksekusi.
- Tile dashboard selain "Rekam Medis" (Keuangan, Kepegawaian, Inventaris, Laporan, Bridging) masih placeholder kosong.
- 1.189 tabel sisanya belum diimpor/didokumentasikan satu per satu — baru 10 tabel inti yang ada di database dev ini.
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
