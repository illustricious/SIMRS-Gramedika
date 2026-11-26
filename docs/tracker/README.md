# Tracker fitur Khanza

Status otomatis: tile berstatus "layar tersedia" bila kuncinya dirutekan di kode (cakupan layar per menu ada di ringkasan); tile Menu L "ditunda". Status "selesai", "sebagian", dan "kurang" hanya dari status.json dengan bukti.

Buka [grafik progres](GRAFIK.html) secara lokal di browser, [prioritas pekerjaan](PRIORITAS.md), [ringkasan](RINGKASAN.md), atau filter [inventaris lengkap](fitur.tsv) menurut status, jenis, file Java, atau jalur submenu. Grafik membutuhkan `grafik-data.js` di folder yang sama; file ini dibuat ulang oleh tracker. Kolom ID menggunakan nama kontrol sehingga perubahan nomor baris tidak menghilangkan penilaian.

## Saat mengerjakan fitur

1. Cari ID di fitur.tsv. Untuk satu alur, periksa jendela, popup dan kontrol terkait pada masing-masing layar.
2. Tambah atau ubah ID di status.json. Isi status dengan kurang, sebagian, selesai atau ditunda. Isi bukti dengan path implementasi atau tes serta hasil pemeriksaan. Tandai sebagian bila baru ada daftar baca-saja tetapi alur Java juga menyimpan perubahan.
3. Jalankan node tools/tracker.mjs dari root repo. Sertakan status.json dan semua laporan yang berubah, termasuk grafik-data.js, dalam commit fitur. Push dan pull request menghitung ulang laporan, menerbitkan ringkasan dan artefak, serta mendeteksi laporan kedaluwarsa atau ID yang hilang.

## Saat sumber Java berubah

Gunakan Node.js 22 atau lebih baru. Jalankan node tools/tracker.mjs --java ../simrs-khanza/src lalu sertakan java.jsonl dan kedua laporan hasilnya. CI memakai snapshot tersebut karena source Java berada di luar repo Avalonia. Tile dibaca ulang dari LegacyMenuCatalog.cs pada setiap eksekusi.

## Cakupan awal

Pemetaan statis mengenali JFrame/JDialog dengan pewarisan langsung, rantai popup yang disusun dengan parent.add(child), serta kontrol lain dengan addActionListener. Aksi dihitung per kontrol per file. Popup dihitung per jalur. Nama variabel digunakan bila label literal tidak ditemukan. Baris jendela menunjuk file kelas. Tile L ditunda, paket Java bridging dan cabang popup berlabel Bridging dilewati. Kontrol integrasi di layar campuran dapat ditandai ditunda saat ditinjau.

Kesetaraan fungsi perlu diperiksa per alur. Handler keyboard/mouse tanpa ActionListener, menu yang dibentuk dinamis, validasi, SQL, hasil cetak dan integrasi di balik tombol belum diurai menjadi fitur terpisah. Angka belum diaudit menunjukkan pekerjaan pemeriksaan. Angka kurang dan sebagian menunjukkan kekurangan yang sudah dikonfirmasi. Penilaian disimpan eksplisit agar membuka layar saja tidak otomatis menandai alur sebagai selesai.

Hasil audit per layar tersedia untuk [Registrasi](AUDIT_REGISTRASI.md), [Rawat Jalan](AUDIT_RAWAT_JALAN.md), dan [Rawat Inap](AUDIT_RAWAT_INAP.md).

Pembaruan visual menu klik kanan, termasuk tema terang/gelap dan jarak antarpilihan, dicatat di [audit Registrasi](AUDIT_REGISTRASI.md#tampilan-menu-klik-kanan-24-november-2026). Status fitur mengikuti cakupan alur masing-masing.

Telusuri cabang di dalam layar, termasuk tindakan petugas dan SOAPIE, melalui [audit cabang pelayanan](AUDIT_CABANG.md).

## Status audit per alur (26 November 2026)

| Alur | Java | Dokumen | Hasil |
|---|---|---|---|
| Registrasi | `DlgReg.java` | [AUDIT_REGISTRASI.md](AUDIT_REGISTRASI.md) | 348 popup dinilai, 295 kurang dan 53 sebagian. Dari 26 tombol/jendela, 4 selesai, 13 sebagian, 8 kurang dan 1 ditunda. Rujukan internal mendukung tambah, hapus satu baris dan PDF. Rujukan masuk mendukung tambah, ubah dan pilih perujuk. |
| Rawat Jalan | `DlgRawatJalan.java` | [AUDIT_RAWAT_JALAN.md](AUDIT_RAWAT_JALAN.md) | 207 entri. 3 selesai, 164 sebagian, 40 kurang. Submenu kiri disaring menurut hak akses Khanza. Sebanyak 147 dari 178 menu membuka laporan baca saja. Input tindakan dokter, petugas dan gabungan dengan jurnal lulus uji MariaDB sintetis. Catatan Dokter mendukung tambah, ubah dan hapus. 5 SOAPIE Terakhir mengisi draf baru |
| Rawat Inap | `DlgRawatInap.java`, `DlgKamarInap.java` | [AUDIT_RAWAT_INAP.md](AUDIT_RAWAT_INAP.md) | Lima SOAPIE terakhir, salin narasi dan input tindakan dokter/petugas/gabungan tersedia. Penguncian billing, izin, jurnal dan rollback diuji di MariaDB sintetis. Uji visual dan penggunaan bersamaan dengan klien Java masih terbuka. |
| Pelayanan inti | beberapa sumber Java | [AUDIT_PELAYANAN_INTI.md](AUDIT_PELAYANAN_INTI.md) | Pelunasan gabungan Rawat Jalan dan Rawat Inap, kuitansi, serta jual bebas Farmasi nonbatch diuji pada skema sintetis. Rekonsiliasi konfigurasi RS dengan klien Java masih terbuka. Cabang lain tetap mengikuti batas di audit. |

Audit per tombol Kasir, IGD, Laboratorium, Radiologi, dan Pasien masih tertunda. Farmasi baru dinilai sebagian untuk No. Resep dan Data Penjualan; audit alur lainnya masih terbuka. Di Rawat Inap, 232 popup kamar belum diaudit. Lihat [log 23 November](../handover/06-SESSION-2026-11-23.md).

## Cara membaca kemajuan

[Ringkasan otomatis](RINGKASAN.md) menampilkan jumlah tile, popup, aksi dan jendela menurut status terbaru. Cakupan tile berasal dari pemeriksaan rute di kode. Satu alur kerja dapat mencakup beberapa tile, jendela, popup dan aksi, sehingga kesiapan operasional perlu dinilai per alur.

Laporan dibuat ulang dari `java.jsonl`, kode routing, dan `status.json`; status manual hanya berubah melalui audit berbukti. Dokumen audit mencatat sejarah dan bagian terbaru mengoreksi temuan lama. Untuk status pekerjaan saat ini gunakan [prioritas](PRIORITAS.md) bersama [ringkasan](RINGKASAN.md).

## Pembaruan otomatis tiap commit

[GRAFIK.html](GRAFIK.html) membaca `grafik-data.js`, yang dibuat oleh `node tools/tracker.mjs`. Agar progres selalu sesuai setiap commit:

1. **Sekali per clone**, aktifkan hook: `git config core.hooksPath .githooks`.
2. Hook `.githooks/pre-commit` menjalankan `node tools/tracker.mjs` lalu menambahkan `fitur.tsv`, `RINGKASAN.md`, `POPUP_REGISTRASI.md`, dan `grafik-data.js` ke commit. Bila `status.json` tidak valid, commit dibatalkan.
3. Jika Node tidak ada atau hook dilewati (`--no-verify`, sandbox), CI `tracker.yml` gagal pada laporan yang kedaluwarsa, jadi ketidaksesuaian tidak lolos ke master.
4. Buka `GRAFIK.html` lewat server lokal (`python -m http.server` di folder ini) atau langsung dari file di browser biasa; angka di bagian atas selalu mencakup seluruh data.
