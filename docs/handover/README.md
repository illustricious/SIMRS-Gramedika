# Handover SIMRS Gramedika (rewrite SIMRS Khanza ke C# Avalonia)

Keadaan per **22 November 2026**, rilis `v0.2.5-alpha.202611222238` di `master`. Mulai dari file ini, lalu buka file yang relevan.

| File | Isi |
|---|---|
| [README.md](README.md) | Ringkasan, status, keputusan penting, langkah berikutnya (file ini) |
| [01-RUNBOOK.md](01-RUNBOOK.md) | Cara menjalankan, build, tes, release, dan jebakan lingkungan |
| [02-ARCHITECTURE.md](02-ARCHITECTURE.md) | Struktur solusi dan pola menambah layar baru |
| [03-MENU-STATUS.md](03-MENU-STATUS.md) | Cakupan tile per menu, keterbatasan, dan sisa pekerjaan |
| [04-HISTORY.md](04-HISTORY.md) | Arsip log kronologis lama (alasan keputusan, bug lama) |
| [05-SESSION-2026-11-22.md](05-SESSION-2026-11-22.md) | Log rinci sesi 22 November 2026: apa yang dikerjakan, commit, keputusan, jebakan, risiko, cara melanjutkan |
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
- **Tracker fitur** (`docs/tracker`): mengukur kesetaraan fungsi, bukan sekadar layar. Sudah diaudit: Registrasi (`AUDIT_REGISTRASI.md`) dan Rawat Jalan (`AUDIT_RAWAT_JALAN.md`), lengkap dengan perbaikannya. Angka: aksi 169 kurang, 52 sebagian, 6 selesai; popup 304 kurang, 80 sebagian; sisanya "belum diaudit". Cakupan layar per menu dihitung otomatis di `RINGKASAN.md`.
- **Sudah diperbaiki setelah audit:** registrasi baru/hapus/penjagaan ubah, NIP pemeriksa nyata di Rawat Jalan (placeholder `D0000099` dihapus), banyak SOAP per kunjungan, petugas nyata di penjualan Farmasi.
- Build bersih, **146 tes lolos, 1 dilewati** (tes billing ranap butuh DB nyata).
- "Berlayar" artinya tile membuka layar, bukan berarti setara fungsi Khanza. Keterbatasan tiap menu ada di `docs/audit/MENU_*_STATUS.md`.
- **Belum pernah diuji ke database produksi.** Layar Menu K, N, P, Q, R, S, dan kebanyakan menu lain baru lolos build, tes struktur (layar bisa dibangun), dan pencocokan nama tabel/kolom dengan `schema_sik.sql`. Kesalahan nama kolom atau perilaku data nyata baru terlihat saat dibuka dengan data.

## Keputusan yang perlu diingat

1. **Layar transaksi uang bersifat hanya-baca.** Khanza memposting jurnal di setiap transaksi. Mencatat tanpa jurnal membuat pembukuan tidak sinkron. Pengecualian yang sudah dibangun dengan jurnal seimbang: Deposit, billing/pelunasan ranap (terbatas), Parkir Masuk, dan Posting Jurnal manual.
2. **Master dinonaktifkan, tidak dihapus**, bila terkait riwayat (tarif, cara bayar, kategori, peminjam). Skema Khanza dapat menghapus berantai.
3. **Total tarif dihitung otomatis** dari komponen saat disimpan.
4. **Asumsi Menu K yang perlu dicocokkan dengan bagan akun RS:** Cash Flow memakai rekening di Akun Bayar sebagai rekening kas/bank. Keuangan (laba rugi) memakai rekening bertipe `R`. Keduanya tebakan dari skema.
5. Mekanisme hak akses: pemetaan tile ke permission Khanza ada di `LegacyTilePermissions.g.cs` (dihasilkan, jangan diedit manual).
6. Lisensi QuestPDF Community (cetak PDF) terbatas menurut pendapatan organisasi. Cek sebelum pemakaian produksi.

Log rinci hari ini (urutan kerja, commit, jebakan teknis, risiko) ada di [05-SESSION-2026-11-22.md](05-SESSION-2026-11-22.md).

## Langkah berikutnya yang disarankan

1. **Uji ke salinan database nyata** (bukan produksi): buka tiap layar Menu K, N, P, Q, R, S, J, I, dan perbaiki kolom yang salah. Ini risiko terbesar saat ini.
2. **Audit fitur lewat tracker**: alur berikutnya Rawat Inap, Kasir, IGD, Laboratorium, Radiologi, Farmasi, Pasien. Bandingkan dengan Java, isi `docs/tracker/status.json` dengan bukti, jalankan `node tools/tracker.mjs`. Menu M dan O sebagian besar hanya-baca, jadi input/ubah formulir (keputusan "formulir dinamis" di `PLANNING_APLIKASI.md`) dan grafik Menu O adalah celah terbesar.
3. **Menu L (183)**: bridging BPJS/PCare/INACBG/Kemenkes. Butuh kredensial dan API eksternal, kerjakan paling akhir.
4. Rilis: lihat [01-RUNBOOK.md](01-RUNBOOK.md) bagian Release. Alpha 0.2.5 (rilis `v0.2.5-alpha.202611222238`, exe tunggal Windows x64) dibuild lokal, CI tidak jalan untuk tag `v0.1.5-alpha.*`, `v0.2.0-alpha.*`, `v0.2.5-alpha.*`. Build OS lain menunggu versi 1.

## Aturan kerja dengan pemilik proyek

- Setelah setiap fitur: `dotnet build` dan `dotnet test` (tercatat di memori sesi).
- Pemilik suka batch fitur besar, jadi kerjakan satu menu penuh per putaran.
- Jangan push exe/installer ke GitHub (`dist/` dan `publish/` di-gitignore).
- Perintah shell Windows yang terlalu panjang gagal (`ENAMETOOLONG`). Tulis file besar lewat editor/Write, bukan heredoc panjang.
