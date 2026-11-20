# Status Menu J

**96 dari 96 tile Menu J** membuka layar Avalonia. Tile mengikuti hak akses Khanza. Laporan baru membaca tabel Khanza dan menyediakan ekspor CSV.

| Tile | Cakupan Avalonia |
|---|---|
| Kedatangan Pasien Per Jam | Jumlah registrasi per tanggal dan jam untuk rentang tanggal dan poli terpilih. Registrasi yang kemudian dibatalkan tetap dihitung. |
| Sensus Harian Poli | Jumlah registrasi baru, lama, dan total per hari dan poli. Registrasi batal dikeluarkan. |
| Kunjungan Ralan | Daftar pasien, poli, dokter, cara bayar, dan status. Rentang menggunakan tanggal registrasi. Registrasi batal dikeluarkan. |
| Kunjungan Ranap | Daftar pasien, bangsal, kamar terakhir, dan status pulang. Rentang menggunakan tanggal registrasi. Registrasi batal dan baris pindah kamar dikeluarkan. |
| Rekap Kunjungan | Rekap bulanan untuk tahun yang dipilih, dikelompokkan menurut layanan dan status baru atau lama, dengan rincian jenis kelamin dan subtotal Poli Jiwa. Registrasi batal dikeluarkan. |
| ICD 9 dan ICD 10 | Cari, tambah, ubah, dan hapus kode pada master Khanza. ICD 10 menyediakan pemilih kategori penyakit. |
| Kunjungan Lab Ralan dan Ranap, Kunjungan Radiologi Ralan dan Ranap | Daftar sesi pemeriksaan per nomor rawat, tanggal, dan jam, dengan jumlah tindakan. Periode berdasarkan tanggal periksa. |
| Frekuensi Penyakit Ralan dan Ranap | Jumlah diagnosis per kode dengan rincian kasus baru dan lama. Periode ralan berdasarkan tanggal registrasi dan periode ranap berdasarkan tanggal keluar. |
| Penyakit Menular dan Tidak Menular Ralan dan Ranap | Rekap diagnosis yang dibatasi menurut klasifikasi pada master penyakit. |
| Poli Asal Pasien Ranap dan Dokter Asal Pasien Ranap | Jumlah registrasi yang berlanjut ke rawat inap menurut poli atau dokter asal pada registrasi. |
| Pembatalan Periksa Per Dokter | Jumlah registrasi batal, tidak batal, dan total menurut dokter pada registrasi. |
| Laporan Bulanan IRJ | Layar laporan bulanan yang sudah tersedia sebelumnya. |
| Registrasi Poli Per Tanggal | Jumlah registrasi baru, lama, batal, dan tidak batal per poli dan tanggal registrasi. |
| Rekap Mutasi Berkas, Lama Penyiapan RM | Riwayat perpindahan berkas dan durasi dari registrasi sampai berkas dikirim. |
| Dosis Radiologi | Daftar parameter penyinaran dan dosis yang tersimpan per pemeriksaan. |
| Rekap Bulanan Porsi Diet, Rekap Bulanan Macam Diet, Rekap Permintaan Diet | Porsi tercatat per tanggal dan bangsal, jenis diet, atau kelas kamar. |
| Sisa Diet Pasien | Daftar sisa makanan per komponen, pasien, dan waktu makan. Entri dan perubahan data masih melalui Khanza Java. |
| Rekap Lab Per Tahun, Rekap Radiologi Per Tahun | Jumlah tindakan per bulan pada tahun yang dipilih. Rekap lab juga menampilkan hitungan parameter. |
| Perujuk Lab Per Tahun, Perujuk Radiologi Per Tahun | Jumlah tindakan per dokter perujuk dan bulan. |
| Operasi Per Bulan, Lama Operasi | Jumlah tindakan per paket dan bulan serta daftar durasi menurut waktu pada laporan operasi. |
| Lama Pelayanan Ralan, Lama Pelayanan Poli | Durasi dari registrasi sampai nota terbit atau pemeriksaan pertama. |
| Lama Pelayanan Apotek, Radiologi, Lab PK, Lab PA, dan Lab MB | Durasi antar tahap resep atau permintaan pemeriksaan berdasarkan waktu yang tercatat. |
| Lama Pelayanan CSSD | Durasi pengambilan, sterilisasi, dan pengembalian per sirkulasi. |
| Obat Penyakit | Kelola pasangan kode penyakit, obat, dan referensi. |
| RL 1.3 Ketersediaan Tempat Tidur | Kapasitas aktif, terisi, dan kosong per bangsal dan kelas. |
| Ranap Per Ruang, Masuk Ruang Per Tahun, Daftar Pasien Ranap | Kunjungan keluar per bangsal, jumlah masuk per bulan, dan daftar pasien bulanan dengan kamar terakhir. |
| Penyakit Ranap Per Cara Bayar | Jumlah diagnosis per penyakit dan cara bayar berdasarkan tanggal keluar. |
| Sensus Harian Ralan, Status Data RM, Cek Entry Ralan | Sensus registrasi dengan diagnosis utama, keberadaan dokumen per kunjungan, dan volume entri per hari. |
| Demografi Umur Kunjungan | Jumlah kunjungan berdasarkan umur yang tercatat, satuan umur, jenis kelamin, dan layanan. |
| KIP Pasien Ralan dan Ranap | Daftar diagnosis utama. Ralan memakai tanggal registrasi dan ranap memakai tanggal keluar. Tabel silang umur dan mortalitas pada cetakan lama belum tersedia. |
| Anggota TNI dan POLRI Dirawat, Daftar Pasien Ranap TNI dan POLRI | Daftar registrasi serta daftar rawat inap bulanan menurut data keanggotaan. |
| Laporan Penyakit TNI dan POLRI, Jumlah Pengunjung Ralan TNI dan POLRI | Hitungan diagnosis atau kunjungan menurut jabatan anggota, dengan pasien lain dikelompokkan menurut cara bayar. |
| RL 3.7 Radiologi, RL 3.8 Laboratorium | Rekap pemeriksaan menurut jenis dan jenis kelamin. Parameter hasil lab dihitung terpisah. |
| Harian dan Bulanan Klasifikasi Pasien Ranap, Klasifikasi Pasien Per Ruang | Daftar penilaian dan rekap kategori minimal, parsial, dan total menurut tanggal atau bangsal. |

Daftar rawat jalan dan rawat inap berfokus pada data kunjungan inti. Rincian diagnosis, filter wilayah, cetak format lama, dan mode filter tanggal keluar rawat inap masih di luar cakupan.

Laporan diagnosis saat ini menyediakan rekap dasar. Tabel silang demografi dan format cetak lama belum tersedia.

## 28 tile terakhir yang disambungkan

| Tile | Cakupan Avalonia |
|---|---|
| Penyakit AFP & PD3I | Tambah dan hapus kode ICD-10 dari daftar surveilans. |
| Surveilans AFP & PD3I, Surveilans Ralan, Surveilans Ranap | Rekap diagnosis utama menurut tanggal registrasi, layanan, umur, jenis kelamin, dan status kasus. AFP & PD3I memakai daftar kode surveilans. |
| RL 3.2 Rawat Darurat, RL 3.3 Gigi dan Mulut, RL 3.4 Kebidanan, RL 3.6 Pembedahan | Rekap aktivitas unit darurat, tindakan gigi, paket kebidanan dan rujukan, serta operasi menurut paket dan kategori. |
| RL 4A Morbiditas Ranap, RL 4B Morbiditas Ralan, RL 4A Sebab Morbiditas Ranap, RL 4B Sebab Morbiditas Ralan | Rekap diagnosis utama. Laporan sebab memakai awalan ICD-10 V/W/X/Y. Rawat inap memakai tanggal keluar dan status pulang. |
| Lama Pelayanan Pasien | Waktu registrasi, mutasi berkas, pemeriksaan pertama, resep pertama, dan nota. Durasi ditampilkan untuk tiga tahap yang bermula dari registrasi. |
| Harian HAIs, Harian HAIs 2 | Daftar observasi dengan susunan kolom indikator atau hasil kultur lebih dahulu. |
| Bulanan HAIs, HAIs Per Kamar/Bangsal | Rekap jumlah entri dan nilai indikator tercatat per tanggal pada bulan terpilih atau per bangsal. |
| Hitung BOR, Hitung ALOS | BOR berdasarkan kapasitas aktif saat ini dan irisan episode kamar dengan periode. ALOS berdasarkan jumlah hari rawat tercatat per pasien keluar, termasuk perpindahan kamar. |
| Data TB | Lihat data TB tersimpan. Entri dan perubahan data masih melalui Khanza Java. |
| Skrining Pernapasan Ralan Per Tahun | Jumlah hasil skrining per kategori dan bulan. |
| Laporan Tahunan IRJ, Laporan Tahunan IGD | Rekap registrasi per bulan, poli, dan cara bayar. Pemisahan memakai kode poli IGDK. |
| Kepatuhan Kelengkapan Keselamatan Bedah | Status keberadaan lima dokumen per nomor rawat. |
| Laporan Tahunan Penolakan Anjuran Medis | Jumlah surat per bulan, jenis penolakan, dan layanan. |
| Pemeriksaan Fisik Ralan Per Penyakit | Daftar ukuran fisik pasien dan diagnosis yang tercatat. |
| Data Sasaran Usia Produktif, Data Sasaran Usia Lansia | Daftar pasien usia 19–59 dan 60+ pada akhir tahun laporan, termasuk jumlah kunjungan dan bulan dengan kunjungan. |

Laporan regulatori saat ini berupa tabel dan CSV. Tata letak formulir asli, tabel silang lama, pengiriman eksternal, dan validasi angka untuk pelaporan resmi masih di luar cakupan.
