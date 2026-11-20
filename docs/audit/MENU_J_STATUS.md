# Status Menu J

**43 dari 96 tile Menu J** membuka layar Avalonia. Tile mengikuti hak akses Khanza. Laporan baru membaca tabel Khanza dan menyediakan ekspor CSV.

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

Daftar rawat jalan dan rawat inap berfokus pada data kunjungan inti. Rincian diagnosis, filter wilayah, cetak format lama, dan mode filter tanggal keluar rawat inap masih di luar cakupan.

Laporan diagnosis saat ini menyediakan rekap dasar. Tabel silang demografi dan format cetak lama belum tersedia. Masih ada 53 tile yang belum tersambung, termasuk surveilans, laporan RL, Lama Pelayanan Pasien, HAIs, BOR/ALOS, dan laporan khusus lainnya.
