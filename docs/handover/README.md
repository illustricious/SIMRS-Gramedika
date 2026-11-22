# Handover SIMRS Gramedika (rewrite SIMRS Khanza ke C# Avalonia)

Keadaan per **22 November 2026**, rilis `v0.2.0-alpha.202611221622` di `master`. Mulai dari file ini, lalu buka file yang relevan.

| File | Isi |
|---|---|
| [README.md](README.md) | Ringkasan, status, keputusan penting, langkah berikutnya (file ini) |
| [01-RUNBOOK.md](01-RUNBOOK.md) | Cara menjalankan, build, tes, release, dan jebakan lingkungan |
| [02-ARCHITECTURE.md](02-ARCHITECTURE.md) | Struktur solusi dan pola menambah layar baru |
| [03-MENU-STATUS.md](03-MENU-STATUS.md) | Cakupan tile per menu, keterbatasan, dan sisa pekerjaan |
| [04-HISTORY.md](04-HISTORY.md) | Arsip log kronologis lama (alasan keputusan, bug lama) |

Dokumen terkait di `docs/audit/`: [TILE_BACKLOG.md](../audit/TILE_BACKLOG.md) (daftar tile belum berlayar), `MENU_*_STATUS.md` (rincian per menu), [DB_SCHEMA.md](../audit/DB_SCHEMA.md) dan `schema_sik.sql` (skema tanpa data), [AUDIT_REPORT.md](../audit/AUDIT_REPORT.md) (audit repo Khanza lama), [PLANNING_APLIKASI.md](../audit/PLANNING_APLIKASI.md).

## Apa proyek ini

Penulisan ulang SIMRS Khanza (Java Swing, 1.199 tabel) menjadi aplikasi desktop **C# + .NET 10 + Avalonia**. Aplikasi memakai database Khanza yang sudah ada (`sik`, MySQL/MariaDB), akun dan hak akses Khanza, dan meniru menu dashboard A–U aslinya. Nama produk: **SIMRS Gramedika**.

- Repo GitHub: `illustricious/SIMRS-Gramedika`, branch `master`.
- Folder kerja lokal: `C:\Users\PC IT\Documents\Vibecoding\REWRITE KHANZA\SimrsKhanza.Avalonia`.
- Repo legacy (source Java asli, data produksi, **hanya dibaca**): `C:\Users\PC IT\Documents\Vibecoding\REWRITE KHANZA\simrs-khanza`. Jangan diubah. Isinya bisa memuat data pasien asli.

## Status

- **787 dari 1.224 tile** dashboard membuka layar. 437 sisanya belum.
- Selesai penuh (100% berlayar): A, B, C, D, E, F, G, H, I, J, K, N, O, P, Q, R, S, T, U.
- Belum: L (183), M (254 dari 255). Rincian di [03-MENU-STATUS.md](03-MENU-STATUS.md).
- Build bersih, **127 tes lolos, 1 dilewati** (tes billing ranap butuh DB nyata).
- "Berlayar" artinya tile membuka layar, bukan berarti setara fungsi Khanza. Keterbatasan tiap menu ada di `docs/audit/MENU_*_STATUS.md`.
- **Belum pernah diuji ke database produksi.** Layar Menu K, N, P, Q, R, S, dan kebanyakan menu lain baru lolos build, tes struktur (layar bisa dibangun), dan pencocokan nama tabel/kolom dengan `schema_sik.sql`. Kesalahan nama kolom atau perilaku data nyata baru terlihat saat dibuka dengan data.

## Keputusan yang perlu diingat

1. **Layar transaksi uang bersifat hanya-baca.** Khanza memposting jurnal di setiap transaksi. Mencatat tanpa jurnal membuat pembukuan tidak sinkron. Pengecualian yang sudah dibangun dengan jurnal seimbang: Deposit, billing/pelunasan ranap (terbatas), Parkir Masuk, dan Posting Jurnal manual.
2. **Master dinonaktifkan, tidak dihapus**, bila terkait riwayat (tarif, cara bayar, kategori, peminjam). Skema Khanza dapat menghapus berantai.
3. **Total tarif dihitung otomatis** dari komponen saat disimpan.
4. **Asumsi Menu K yang perlu dicocokkan dengan bagan akun RS:** Cash Flow memakai rekening di Akun Bayar sebagai rekening kas/bank. Keuangan (laba rugi) memakai rekening bertipe `R`. Keduanya tebakan dari skema.
5. Mekanisme hak akses: pemetaan tile ke permission Khanza ada di `LegacyTilePermissions.g.cs` (dihasilkan, jangan diedit manual).
6. Lisensi QuestPDF Community (cetak PDF) terbatas menurut pendapatan organisasi. Cek sebelum pemakaian produksi.

## Langkah berikutnya yang disarankan

1. **Uji ke salinan database nyata** (bukan produksi): buka tiap layar Menu K, N, P, Q, R, S, J, I, dan perbaiki kolom yang salah. Ini risiko terbesar saat ini.
2. **Grafik Menu O**: 113 tile baru berupa tabel agregat, belum ada grafik batang/pai.
3. **Menu M (254)**: rekam medis, sebagian besar formulir `penilaian*`/`skrining*`/`surat*`. Perlu keputusan "formulir dinamis" di `PLANNING_APLIKASI.md` sebelum mulai.
4. **Menu L (183)**: bridging BPJS/PCare/INACBG/Kemenkes. Butuh kredensial dan API eksternal, kerjakan paling akhir.
5. Rilis: lihat [01-RUNBOOK.md](01-RUNBOOK.md) bagian Release. Alpha 0.2.0 (rilis `v0.2.0-alpha.202611221622`, exe tunggal Windows x64) dibuild lokal, CI tidak jalan untuk tag `v0.1.5-alpha.*` dan `v0.2.0-alpha.*`. Build OS lain menunggu versi 1.

## Aturan kerja dengan pemilik proyek

- Setelah setiap fitur: `dotnet build` dan `dotnet test` (tercatat di memori sesi).
- Pemilik suka batch fitur besar, jadi kerjakan satu menu penuh per putaran.
- Jangan push exe/installer ke GitHub (`dist/` dan `publish/` di-gitignore).
- Perintah shell Windows yang terlalu panjang gagal (`ENAMETOOLONG`). Tulis file besar lewat editor/Write, bukan heredoc panjang.
