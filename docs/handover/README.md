# Handover SIMRS Gramedika

Status 27 November 2026. Repo GitHub `illustricious/SIMRS-Gramedika`, branch `master`. Aplikasi desktop C#, .NET 10, dan Avalonia memakai skema MySQL/MariaDB Khanza. Folder `simrs-khanza` berada di samping repo Avalonia dan dipakai sebagai referensi sumber Java.

## Mulai sesi berikutnya

1. Buka [log sesi terbaru](12-SESSION-2026-11-27.md) untuk hasil kerja, titik masuk fitur, verifikasi, batas, dan calon pekerjaan.
2. Cek `git status --short` dan `git log -5 --oneline`. Commit source, EXE lokal di `dist/`, dan release GitHub adalah tiga hal terpisah.
3. Jalankan `powershell -ExecutionPolicy Bypass -File tools\uji-fixture.ps1` untuk memastikan keadaan awal ([runbook](01-RUNBOOK.md#skrip-tes-fixture)).
4. Buka [peta fungsi](../tracker/AUDIT_PETA_FUNGSI.md), [tracker HTML](../tracker/GRAFIK.html) dan [prioritas](../tracker/PRIORITAS.md). Angka 1.041 dari 1.224 adalah cakupan tile yang punya layar, bukan kesetaraan fungsi. Tile Menu L yang ditunda berjumlah 183; pemilik menyiapkan bridging sebagai aplikasi terpisah ([log 12](12-SESSION-2026-11-27.md#bridging-di-aplikasi-terpisah)).
5. Gunakan skema sintetis. Akses database rumah sakit memerlukan persetujuan pemilik sistem; jangan arahkan pengembangan ke database produksi.

| Dokumen | Isi |
|---|---|
| [Runbook](01-RUNBOOK.md) | Build, uji, EXE, dan pembaruan tracker |
| [Arsitektur](02-ARCHITECTURE.md) | Peta kode, izin, transaksi, dan cara menambah layar atau formulir |
| [Status Menu](03-MENU-STATUS.md) | Cakupan dashboard dan keterbatasan per menu |
| [Arsip](04-HISTORY.md) | Catatan kerja awal |
| [Log 05](05-SESSION-2026-11-22.md) · [Log 06](06-SESSION-2026-11-23.md) · [Log 07](07-SESSION-2026-11-24.md) · [Log 08](08-SESSION-2026-11-24.md) · [Log 09](09-SESSION-2026-11-24.md) · [Log 10](10-SESSION-2026-11-26.md) · [Log 11](11-SESSION-2026-11-27.md) · [Log 12](12-SESSION-2026-11-27.md) | Riwayat kerja per sesi |

## Posisi terakhir

- Farmasi memvalidasi, membatalkan dan menyerahkan resep, serta meretur obat pulang seluruhnya atau sebagian. Lab dan Radiologi memposting dan membalik jurnal.
- Halaman Tindakan Rawat Jalan bertab seperti Java, dengan menu klinis di kiri. Lima belas formulir rekam medis bisa diisi, diubah, dihapus dan dicetak.
- Rawat inap: tindakan bisa diubah dan dihapus dengan jurnal pembalik, pemeriksaan obstetri dan ginekologi bisa diisi, dan ada Hapus Data Salah untuk baris kamar ([log 12](12-SESSION-2026-11-27.md#fitur-rawat-inap)).
- Rawat gabung ibu dan bayi bisa dibuat dan dilepas, bayi tampil di bawah ibunya, dan Kasir menagih keduanya dalam satu nota ([log 12](12-SESSION-2026-11-27.md#rawat-gabung-ibu-dan-bayi)).
- Operasi/VK rawat inap bisa diinput dan dihapus beserta obat kamar operasi, laporan dan jurnalnya, dan ikut tagihan rawat inap ([log 12](12-SESSION-2026-11-27.md#operasi-rawat-inap)).
- Dokter bisa mengirim dan mengubah resep racikan rawat jalan ([log 12](12-SESSION-2026-11-27.md#resep-racikan-dari-dokter)).
- Cek "kunjungan sudah ditagih" disatukan di `TagihanKunjungan` dan mengikuti Khanza (hanya baris `billing`).
- Pengaturan tanggal mundur Khanza tersedia pada profil koneksi.
- Order rawat jalan yang belum diproses bisa dibatalkan; layar laporan generik bisa dicetak PDF berkop.
- Peta fungsi: paling banyak 34% pasangan tabel-aksi tulis jendela Java punya padanan (748 dari 2.202, tanpa bridging); 320 tile hanya menampilkan data yang di Java bisa diisi. Analisis statis, angka batas atas.
- Tes terakhir: skrip fixture lulus seluruhnya (77 + 1 + 30 fixture; tanpa database 223 lulus, 45 dilewati).
- Belum diuji pada database RS, bersama klien Java, atau dengan klik di aplikasi memakai database. EXE lokal terbaru `202611280045` (dari `1266e97`). Pre-release GitHub tertinggal dari source.
