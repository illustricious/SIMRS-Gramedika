# Tracker fitur Khanza

Status otomatis: tile berstatus "layar tersedia" bila kuncinya dirutekan di kode (cakupan layar per menu ada di ringkasan); tile Menu L "ditunda". Status "selesai", "sebagian", dan "kurang" hanya dari status.json dengan bukti.

Buka [ringkasan](RINGKASAN.md) atau filter [inventaris lengkap](fitur.tsv) menurut status, jenis, file Java, atau jalur submenu. Kolom ID menggunakan nama kontrol sehingga perubahan nomor baris tidak menghilangkan penilaian.

## Saat mengerjakan fitur

1. Cari ID di fitur.tsv. Untuk satu alur, periksa jendela, popup dan kontrol terkait pada masing-masing layar.
2. Tambah atau ubah ID di status.json. Isi status dengan kurang, sebagian, selesai atau ditunda. Isi bukti dengan path implementasi atau tes serta hasil pemeriksaan. Tandai sebagian bila baru ada daftar baca-saja tetapi alur Java juga menyimpan perubahan.
3. Jalankan node tools/tracker.mjs dari root repo. Sertakan status.json, fitur.tsv dan RINGKASAN.md dalam commit fitur. Push dan pull request menghitung ulang laporan, menerbitkan ringkasan dan artefak, serta mendeteksi laporan kedaluwarsa atau ID yang hilang.

## Saat sumber Java berubah

Gunakan Node.js 22 atau lebih baru. Jalankan node tools/tracker.mjs --java ../simrs-khanza/src lalu sertakan java.jsonl dan kedua laporan hasilnya. CI memakai snapshot tersebut karena source Java berada di luar repo Avalonia. Tile dibaca ulang dari LegacyMenuCatalog.cs pada setiap eksekusi.

## Cakupan awal

Pemetaan statis mengenali JFrame/JDialog dengan pewarisan langsung, rantai popup yang disusun dengan parent.add(child), serta kontrol lain dengan addActionListener. Aksi dihitung per kontrol per file. Popup dihitung per jalur. Nama variabel digunakan bila label literal tidak ditemukan. Baris jendela menunjuk file kelas. Tile L ditunda, paket Java bridging dan cabang popup berlabel Bridging dilewati. Kontrol integrasi di layar campuran dapat ditandai ditunda saat ditinjau.

Kesetaraan fungsi perlu diperiksa per alur. Handler keyboard/mouse tanpa ActionListener, menu yang dibentuk dinamis, validasi, SQL, hasil cetak dan integrasi di balik tombol belum diurai menjadi fitur terpisah. Angka belum diaudit menunjukkan pekerjaan pemeriksaan. Angka kurang dan sebagian menunjukkan kekurangan yang sudah dikonfirmasi. Penilaian disimpan eksplisit agar membuka layar saja tidak otomatis menandai alur sebagai selesai.

Hasil audit per layar: [Registrasi](AUDIT_REGISTRASI.md), [Rawat Jalan](AUDIT_RAWAT_JALAN.md).

Telusuri cabang di dalam layar, termasuk tindakan petugas dan SOAPIE, melalui [audit cabang pelayanan](AUDIT_CABANG.md).
