# Handover SIMRS Gramedika

Status 26 November 2026. Repo GitHub `illustricious/SIMRS-Gramedika`, branch `master`. Aplikasi desktop C#, .NET 10, dan Avalonia memakai skema MySQL/MariaDB Khanza. Folder `simrs-khanza` berada di samping repo Avalonia dan dipakai sebagai referensi sumber Java.

## Mulai sesi berikutnya

1. Buka [log sesi terbaru](10-SESSION-2026-11-26.md) untuk hasil kerja, verifikasi, batas operasional, dan urutan lanjutan.
2. Cek `git status --short` dan `git log -5 --oneline`. Commit source dan rilis installer GitHub adalah dua hal terpisah. Build lokal berada di `dist/`.
3. Buka [tracker HTML](../tracker/GRAFIK.html) dan [prioritas](../tracker/PRIORITAS.md). Angka 1.041 dari 1.224 adalah cakupan tile yang punya layar. Tile Menu L yang ditunda berjumlah 183.
4. Gunakan [runbook](01-RUNBOOK.md) dan skema sintetis. Akses database rumah sakit memerlukan persetujuan pemilik sistem.

| Dokumen | Isi |
|---|---|
| [Runbook](01-RUNBOOK.md) | Build, uji, EXE, dan pembaruan tracker |
| [Arsitektur](02-ARCHITECTURE.md) | Peta kode, izin, transaksi, dan cara menambah layar |
| [Status Menu](03-MENU-STATUS.md) | Cakupan dashboard dan keterbatasan per menu |
| [Arsip](04-HISTORY.md) | Catatan kerja awal |
| [Log 05](05-SESSION-2026-11-22.md) · [Log 06](06-SESSION-2026-11-23.md) · [Log 07](07-SESSION-2026-11-24.md) · [Log 08](08-SESSION-2026-11-24.md) · [Log 09](09-SESSION-2026-11-24.md) | Riwayat rilis, tindakan, tema, dan rujukan |

## Posisi terakhir

- Registrasi memiliki pemilih pasien dan rujukan internal/eksternal. Rawat Jalan memiliki tindakan dengan jurnal, resep nonracikan, permintaan Lab/Radiologi, dan resume.
- Rawat Inap memiliki tindakan dengan jurnal, resume, penutupan kamar saat pulang, serta permintaan obat pulang yang tersambung ke validasi dan serah terima Farmasi.
- Kasir mendukung pelunasan gabungan Rawat Jalan dan Rawat Inap dengan satu akun bayar tanpa biaya admin. Kuitansi PDF dibaca ulang dari data pelunasan. Farmasi memiliki jual bebas tunai nonbatch.
- Verifikasi terbaru mencakup 160 tes lulus dan 5 dilewati di skema sintetis, serta build EXE yang bisa membuka layar login. Rekonsiliasi klien Java dengan konfigurasi RS masih terbuka, terutama akun suspen.

- Checkout aktif: "SimrsGramedika". Publish Windows x64 setelah rename berhasil dan EXE baru membuka layar login. Lihat [log sesi terbaru](10-SESSION-2026-11-26.md#hasil-build-windows-setelah-rename) untuk perintah build, warning, dan status pengujian.
