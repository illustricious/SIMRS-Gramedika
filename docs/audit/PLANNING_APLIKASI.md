# Dokumen Planning — Rewrite Aplikasi Utama SIMRS Khanza (C# + Avalonia)
Tanggal: 2026-11-03 • Status: **Planning saja, belum ada kode rewrite ditulis**

Fokus dokumen ini: titik awal konkret untuk mulai merancang ulang **aplikasi desktop utama** (`src/simrskhanza`, entry point `SIMRSKhanza.java`), karena ini adalah shell yang pertama dilihat/dipakai user dan jadi fondasi navigasi ke semua modul lain.

---

## 1. Temuan Struktur Aplikasi Lama (`src/simrskhanza`)

- **Bukan MDI klasik** (tidak pakai `JDesktopPane`/`JInternalFrame`). Entry point `SIMRSKhanza.main()` langsung menampilkan satu `JFrame` raksasa: `frmUtama` (~51.000 baris — hasil NetBeans GUI builder, satu file melakukan banyak hal sekaligus).
- **Navigasi utama bukan menu teks**, tapi **dashboard grid ikon** yang di-generate runtime berdasarkan hak akses user yang login (field `akses`, `coder_nik`, `jmlmenu`, `grid`) — ini ciri khas tampilan SIMRS Khanza yang terkenal (grid tombol warna-warni). **Ini bagian UI paling penting untuk ditiru persis** di Avalonia karena itu identitas visual utama aplikasi.
- Ada 5 menu teks sekunder di menu bar: **Program, Presensi Pegawai, Informasi, Anjungan & Antrian, Tentang Program** — ini menu admin/util, bukan menu fitur utama.
- **Login menyatu di dalam `frmUtama`** (bukan class terpisah) — field `edAdmin`/`edPwd` + dialog overlay `DlgLogin`, dengan efek transparansi dibaca dari `setting/database.xml`. Auth & dashboard tercampur jadi satu class, langsung konek DB (`koneksiDB.condb()`) dan baca file config di constructor — **coupling yang akan kita pecah di rewrite**.
- **Pola reuse yang jelas**: 16 dialog `Dlg*Cari*` (CariPasien, CariPoli, CariBangsal, CariSpesialis, dll) — semuanya varian dari satu pola "popup pencarian/pemilihan data master". Ini **target reuse pertama paling jelas**: satu komponen `SearchPickerDialog` generik di Avalonia bisa menggantikan 16 class duplikat.
- Sisanya di package ini terbagi: ~20 dialog CRUD master data (Bahasa, Poli, Spesialis, dll — sederhana), ~15 dialog alur pendaftaran/klinis inti (Pasien, RawatInap, RawatJalan, IGD, Rujuk, dll), 6 dialog lab/radiologi, 5 dialog farmasi/billing kamar operasi, 2 dialog util (About, pilihan cetak).

## 2. Rencana Pemisahan Tanggung Jawab (untuk rewrite)

| Legacy (1 class `frmUtama`) | Jadi di rewrite (terpisah) |
|---|---|
| Baca `setting/database.xml` langsung | `IAppConfigService` (baca config terenkripsi, dengan cache — perbaikan dari bug "baca ulang tiap getter" di versi lama) |
| Cek user/pass manual di constructor | `IAuthService` (login terhadap DB, return role/permission) |
| Generate grid ikon manual dari field `akses` | `IMenuPermissionService` + `DashboardViewModel` (daftar menu = data, bukan logic tertanam di View) |
| Koneksi DB langsung (`koneksiDB.condb()`) | `IDbConnectionFactory` / repository pattern per modul |
| 16x `Dlg*Cari*` duplikat | 1x `SearchPickerDialog<T>` generik + konfigurasi per entitas (query, kolom tampil, kolom hasil) |
| Dialog CRUD master data (~20 class hampir identik) | 1x `MasterDataCrudView` generik + definisi field per entitas (pola sama dengan picker di atas) |

## 3. Urutan Kerja yang Disarankan (mulai dari aplikasi ini)

1. **Tiru tampilan shell & dashboard dulu (tanpa logic)** — bikin `MainWindow.axaml` Avalonia: layout grid ikon menu + area login overlay, warna/ikon diambil dari `gambar/`/`picture/`/`48x48/` yang sudah ada sebagai referensi visual. Tujuan: begitu dibuka, tampilannya sudah mirip, meski menu belum berfungsi.
2. **Bangun `IAuthService` + layar login** terpisah dari shell, test dengan skema DB yang sudah ada (pakai struktur tabel dari `sik.sql`, **tanpa menyalin data pasien riilnya** — buat seed data dummy baru untuk dev).
3. **Bangun `IMenuPermissionService`** — defenisikan menu/ikon dashboard sebagai data (JSON/DB), bukan kode manual per tombol seperti versi lama.
4. **Bangun `SearchPickerDialog` generik** — langsung dapat "untung" besar karena dipakai ulang di 16+ tempat begitu modul lain mulai di-port.
5. Baru lanjut ke modul fitur sesuai urutan di `AUDIT_REPORT.md` §10 (rekam medis → keuangan → kepegawaian → laporan → dst), masing-masing memakai `SearchPickerDialog`/`MasterDataCrudView` yang sudah jadi.

## 4. Yang Masih Perlu Digali (belum masuk scope sesi ini)
- Isi detail `frmUtama.java` (51rb baris) belum dibaca utuh — baru field-level/struktural. Perlu sesi audit lanjutan kalau mau port logic login persis (state saat ini: cukup untuk mulai mendesain ulang tampilan & arsitektur, belum cukup untuk port 1:1 logic).
- Modul `webapps/` (web PHP) belum dipetakan ke urutan kerja yang sama — bisa jadi jalur paralel (portal web vs app desktop) tergantung prioritas bisnis Anda.
- Skema tabel riil (dari `sik.sql`) belum diekstrak jadi dokumen skema terpisah (tanpa data) — ini langkah lanjutan yang masih relevan & tidak menyentuh isi data pasien, hanya struktur `CREATE TABLE`.

## 5. Status Data Sensitif (per keputusan Anda)
- `sik.sql`, PDF/PNG pasien, file ekspor loose di root — **dibiarkan apa adanya, tidak disentuh**, sesuai arahan Anda. Dicatat sebagai potensi sumber *contoh data* untuk pengembangan fitur nanti, tapi **direkomendasikan dianonimkan dulu** sebelum dipakai sebagai seed/dummy data di proyek baru, supaya NIK/nama asli tidak ikut terbawa ke repo baru.
