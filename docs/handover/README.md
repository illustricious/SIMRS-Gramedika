# Handover SIMRS Gramedika (rewrite SIMRS Khanza ke C# Avalonia)

Keadaan per **24 November 2026**. Rilis GitHub terakhir `v0.2.5-alpha.202611222238` di `master`. Perubahan lokal terbaru tercatat di log sesi.

| File | Isi |
|---|---|
| [README.md](README.md) | Ringkasan, status, keputusan penting, langkah berikutnya (file ini) |
| [01-RUNBOOK.md](01-RUNBOOK.md) | Cara menjalankan, build, tes, release, dan jebakan lingkungan |
| [02-ARCHITECTURE.md](02-ARCHITECTURE.md) | Struktur solusi dan pola menambah layar baru |
| [03-MENU-STATUS.md](03-MENU-STATUS.md) | Cakupan tile per menu, keterbatasan, dan sisa pekerjaan |
| [04-HISTORY.md](04-HISTORY.md) | Arsip log kronologis lama (alasan keputusan, bug lama) |
| [05-SESSION-2026-11-22.md](05-SESSION-2026-11-22.md) | Log rinci sesi 22 November 2026: apa yang dikerjakan, commit, keputusan, jebakan, risiko, cara melanjutkan |
| [06-SESSION-2026-11-23.md](06-SESSION-2026-11-23.md) | Audit Rawat Inap, perbaikan integritas data kamar, dan verifikasi |
| [07-SESSION-2026-11-24.md](07-SESSION-2026-11-24.md) | Hak akses submenu Rawat Jalan, input tindakan dengan jurnal, Catatan Dokter, 5 SOAPIE, dan uji MariaDB |
| [08-SESSION-2026-11-24.md](08-SESSION-2026-11-24.md) | Tema terang/gelap dan tampilan ringkas untuk menu klik kanan |
| [../tracker/README.md](../tracker/README.md) | Tracker kesetaraan fitur terhadap source Java (15.843 baris) dan cara memakainya |

Dokumen terkait di `docs/audit/`: [TILE_BACKLOG.md](../audit/TILE_BACKLOG.md) (daftar tile belum berlayar), `MENU_*_STATUS.md` (rincian per menu), [DB_SCHEMA.md](../audit/DB_SCHEMA.md) dan `schema_sik.sql` (skema tanpa data), [AUDIT_REPORT.md](../audit/AUDIT_REPORT.md) (audit repo Khanza lama), [PLANNING_APLIKASI.md](../audit/PLANNING_APLIKASI.md).

## Apa proyek ini

Penulisan ulang SIMRS Khanza (Java Swing, 1.199 tabel) menjadi aplikasi desktop **C# + .NET 10 + Avalonia**. Aplikasi memakai database Khanza yang sudah ada (`sik`, MySQL/MariaDB), akun dan hak akses Khanza, dan meniru menu dashboard A–U aslinya. Nama produk: **SIMRS Gramedika**.

- Repo GitHub: `illustricious/SIMRS-Gramedika`, branch `master`.
- Folder kerja lokal: `C:\Users\PC IT\Documents\Vibecoding\REWRITE KHANZA\SimrsKhanza.Avalonia`.
- Repo legacy (source Java asli, data produksi, **hanya dibaca**): `C:\Users\PC IT\Documents\Vibecoding\REWRITE KHANZA\simrs-khanza`. Jangan diubah. Isinya bisa memuat data pasien asli.

## Status

- **1.041 dari 1.224 tile** dashboard membuka layar. 183 sisanya (Menu L) belum.
- Selesai penuh (100% berlayar): A sampai K, M sampai U. Belum: hanya L (183, bridging). Rincian di [03-MENU-STATUS.md](03-MENU-STATUS.md).
- **Tracker fitur** mencakup Registrasi, Rawat Jalan, dan Rawat Inap. Klik kanan Registrasi membuka Tindakan Rawat Jalan dalam jendela utama, dengan katalog 178 submenu yang disaring menurut hak akses. Input tindakan dokter, petugas, dan gabungan dengan jurnal lulus uji di MariaDB sintetis. Audit Rawat Inap menilai 164 entri klinis dan 40 entri kamar. SOAPIE Ranap memuat lima riwayat terakhir dan salin narasi. [Ringkasan tracker](../tracker/RINGKASAN.md).
- **Sudah diperbaiki setelah audit:** registrasi baru/hapus/penjagaan ubah, NIP pemeriksa nyata di Rawat Jalan (placeholder `D0000099` dihapus), banyak SOAP per kunjungan. Input penjualan Farmasi sekarang dinonaktifkan setelah audit menemukan transaksi stok/jurnal belum setara Khanza.
- Hasil tes historis berbeda menurut fixture yang aktif: log tindakan mencatat 154 lulus dengan fixture MariaDB, sedangkan sesi tema mencatat 152 lulus dan 2 dilewati. Ini bukan hasil verifikasi ulang saat ini. Lihat [log tindakan](07-SESSION-2026-11-24.md) dan [log tema](08-SESSION-2026-11-24.md).
- "Berlayar" artinya tile membuka layar, bukan berarti setara fungsi Khanza. Keterbatasan tiap menu ada di `docs/audit/MENU_*_STATUS.md`.
- **Belum pernah diuji ke database produksi.** Layar Menu K, N, P, Q, R, S, dan kebanyakan menu lain baru lolos build, tes struktur (layar bisa dibangun), dan pencocokan nama tabel/kolom dengan `schema_sik.sql`. Kesalahan nama kolom atau perilaku data nyata baru terlihat saat dibuka dengan data.

## Verifikasi sebelumnya (24 November 2026)

- `node tools/tracker.mjs` berhasil; laporan dibuat ulang dari snapshot dan status lokal.
- Build Release `--no-restore -m:1 -p:UseSharedCompilation=false` berhasil, 0 error dan 12 warning, dengan workload resolver dinonaktifkan. Build Debug `--no-restore` gagal CS1061 pada `WithDeveloperTools`; perlu memeriksa restore aset konfigurasi Debug. Peringatan mencakup advisori MySqlConnector dan EF1002 yang perlu ditinjau, serta audit paket yang terhalang akses.
- Tes Release `--no-build --no-restore` menemukan assembly tetapi tidak memberikan hasil selama lebih dari 40 detik dan dihentikan; hasil tes saat pemeriksaan ini belum terverifikasi. Hasil historis dan fixture yang aktif tercatat di log sesi.

## Keputusan yang perlu diingat

1. **Layar transaksi uang bersifat hanya-baca.** Khanza memposting jurnal di setiap transaksi. Mencatat tanpa jurnal membuat pembukuan tidak sinkron. Pengecualian yang sudah dibangun dengan jurnal seimbang: Deposit, billing/pelunasan ranap (terbatas), input tindakan rawat jalan, Parkir Masuk, dan Posting Jurnal manual.
2. **Master dinonaktifkan, tidak dihapus**, bila terkait riwayat (tarif, cara bayar, kategori, peminjam). Skema Khanza dapat menghapus berantai.
3. **Total tarif dihitung otomatis** dari komponen saat disimpan.
4. **Asumsi Menu K yang perlu dicocokkan dengan bagan akun RS:** Cash Flow memakai rekening di Akun Bayar sebagai rekening kas/bank. Keuangan (laba rugi) memakai rekening bertipe `R`. Keduanya tebakan dari skema.
5. Mekanisme hak akses: pemetaan tile ke permission Khanza ada di `LegacyTilePermissions.g.cs` (dihasilkan, jangan diedit manual).
6. Lisensi QuestPDF Community (cetak PDF) terbatas menurut pendapatan organisasi. Cek sebelum pemakaian produksi.

Log sesi terakhir: [07 — tindakan dan Catatan Dokter](07-SESSION-2026-11-24.md) dan [08 — tema menu](08-SESSION-2026-11-24.md). Log adalah catatan historis; status terkini dan urutan kerja ada di [prioritas lanjutan](../tracker/PRIORITAS.md).

## Langkah berikutnya yang disarankan

Lihat [prioritas dan kriteria selesai](../tracker/PRIORITAS.md). Mulai dengan verifikasi alur klinis dan keuangan pada database uji, lalu lengkapi tindakan Rawat Jalan dan Rawat Inap. Audit IGD, Kasir, Farmasi, Laboratorium dan Radiologi per alur sebelum menganggap layar-layarnya siap. Menu L tetap ditunda sampai integrasi dan akses uji tersedia.

**Kondisi Git saat pemeriksaan 24 November:** `master` satu commit di depan `origin/master`; ada perubahan lokal dan file baru yang belum dilacak, termasuk layanan dan tes klinis serta log sesi 06/07. Jangan menghapus atau mengabaikannya saat menyerahkan pekerjaan. Rilis terakhir adalah `v0.2.5-alpha.202611222238`; perubahan lokal dan commit sesudahnya tidak otomatis termasuk rilis tersebut.

## Aturan kerja dengan pemilik proyek

- Setelah setiap fitur: `dotnet build` dan `dotnet test` (tercatat di memori sesi).
- Pemilik suka batch fitur besar, jadi kerjakan satu menu penuh per putaran.
- Jangan push exe/installer ke GitHub (`dist/` dan `publish/` di-gitignore).
- Perintah shell Windows yang terlalu panjang gagal (`ENAMETOOLONG`). Tulis file besar lewat editor/Write, bukan heredoc panjang.


## Catatan historis audit Rawat Jalan — 24 November

Izin SOAPIE `tindakan_ralan` kini diperiksa di layanan; pilihan riwayat lima SOAPIE Ralan dan Ranap tersedia. Ubah/hapus SOAPIE memeriksa data lama dan mengunci baris. Lihat [audit dan pekerjaan tersisa](../tracker/AUDIT_RAWAT_JALAN.md#audit-catatan-dokter-tindakan-dan-riwayat-soapie-24-november-2026-lanjutan). Build Release dan 154 tes lulus; dua fixture MariaDB dilewati. Perubahan SQL baru belum diverifikasi dengan database. Ubah/hapus tindakan dengan jurnal pembalik tetap belum diimplementasikan.


## Pemeriksaan lanjutan 24 November

Restore Debug dan tes berhasil. MariaDB 11.4.8 sintetis dipakai untuk fixture dua koneksi, SOAPIE, transaksi tindakan dan kamar; hasil terakhir serta batasannya ada di [audit pelayanan inti](../tracker/AUDIT_PELAYANAN_INTI.md). Rawat Jalan kini mendukung ubah/hapus satu tindakan dan jurnal dalam transaksi; PDF SOAPIE Rawat Inap dapat disimpan. Diagnosa, obstetri/ginekologi, tindakan Rawat Inap dan audit per tombol masih belum lengkap.


## Hasil verifikasi terbaru

Restore Debug berdasarkan konfigurasi memulihkan build. Tes Debug lengkap dengan fixture Ralan dan Ranap pada MariaDB 11.4.8 sintetis: 156 lulus, 0 gagal, 0 dilewati. Tes lanjutan tindakan lulus setelah tambahan pengujian dua arah kunci antara tindakan dan kasir. Ubah/hapus satu tindakan tiga kategori dengan jurnal dan penolakan billing diuji; interaksi klien Java belum diuji. PDF SOAPIE Ranap telah dibangun tetapi belum diverifikasi visual. Database rumah sakit tidak diakses.
