# Menu M — Olah Data Rekam Medis Pasien

Per 22 November 2026, **89 dari 255 kemunculan tile** sudah membuka layar: tile Pasien yang sudah ada dan 88 tile pertama dari backlog. Sebanyak 166 tile masih belum memiliki rute. Screen coverage tidak berarti seluruh alur legacy sudah direplikasi.

## Selesai pada batch pertama

**Master yang bisa dikelola:** Instansi/Perusahaan Pasien, Suku/Bangsa, Bahasa, Golongan/Satuan/Jabatan/Pangkat TNI, Golongan/Satuan/Jabatan/Pangkat POLRI, Cacat Fisik, dan Master Insiden Keselamatan.

**Daftar baca-saja:** Kelahiran Bayi, Catatan Pasien, Pasien Meninggal, Diagnosa Pasien, Data HAIs, Klasifikasi Pasien Ranap, dan SOAP Perawatan Rawat Inap.

Tile dihubungkan di `MainWindow` melalui `MenuMSpecs`. Master referensi memakai `MasterView`; catatan klinis memakai `ReportView` dan tetap dicatat atau diubah melalui alur pelayanan Khanza.

## Batch kedua

**Daftar baca-saja:** SOAP Rawat Jalan/Rawat Inap untuk pasien POLRI dan TNI, Riwayat Kamar, Data Insiden Keselamatan, Peminjaman Berkas, Riwayat Perawatan, Retensi Berkas RM, Mutasi Berkas, Berkas Digital Perawatan, Pengaduan Pasien, dan Data Triase IGD.

**Master triase yang bisa dikelola:** Pemeriksaan, Macam Kasus, serta Skala 1–5. Kode pemeriksaan pada master skala dipilih dari Master Triase Pemeriksaan. Master yang sudah dipakai tidak dapat dihapus dari layar ini.

## Batch ketiga

**Daftar baca-saja:** Resume Pasien Rawat Jalan, Resume Pasien Rawat Inap, Skrining Gizi Lanjut, Asuhan Gizi, Monitoring Asuhan Gizi, Catatan ADIME Gizi, Skrining Nutrisi Dewasa, Lansia, dan Anak, serta Skrining Gizi Kehamilan. Semua layar mendukung pencarian pasien dan rentang tanggal.

## Batch keempat

**Master template:** Hasil Radiologi, Laporan Operasi, Pemeriksaan Dokter, dan Informasi & Edukasi. Data dapat dicari, ditambah, atau diubah; penghapusan dinonaktifkan untuk menjaga template yang mungkin sudah dipakai.

**Master keperawatan:** Katalog Masalah dan Rencana untuk umum, gigi, bayi/anak, mata, IGD, psikiatri, geriatri, dan neonatus. Setiap Rencana memilih Masalah dari master yang sesuai. Penghapusan dinonaktifkan untuk menjaga referensi asuhan yang sudah tersimpan.

## Batch kelima

**Master:** Imunisasi. Penghapusan dinonaktifkan untuk menjaga pilihan yang mungkin sudah digunakan pada riwayat imunisasi.

**Daftar baca-saja:** Pengkajian awal keperawatan rawat jalan, rawat inap, IGD, gigi, kebidanan rawat jalan, dan kebidanan rawat inap. Layar menampilkan ringkasan identitas kunjungan, keluhan/riwayat, serta parameter utama dari masing-masing tabel dan mendukung filter tanggal.

## Batch keenam

**Daftar baca-saja:** Pengkajian awal keperawatan rawat jalan bayi/anak, rawat inap bayi/anak, rawat jalan psikiatri, rawat jalan geriatri, dan rawat inap neonatus. Setiap daftar menampilkan ringkasan spesifik sesuai formnya dan dapat difilter berdasarkan tanggal.

## Batch ketujuh

**Daftar baca-saja:** Awal Medis IGD, IGD Psikiatri, Rawat Jalan Umum, Rawat Inap Umum, Rawat Inap Neonatus, dan Pengkajian Bayi Baru Lahir. Masing-masing mendukung pencarian pasien dan filter tanggal.

## Batasan yang diketahui

- Data kelahiran, catatan pasien, diagnosis, HAIs, klasifikasi rawat inap, dan SOAP hanya ditampilkan. Form pencatatan klinis tidak dipindahkan ke layar ini.
- SOAP TNI/POLRI dibatasi dengan data keanggotaan pasien pada tabel `pasien_tni`/`pasien_polri`. Data triase menggabungkan penilaian primer, sekunder, dan rincian skala yang tersedia.
- Resume dan catatan gizi batch ketiga baca-saja. Resume memakai tanggal registrasi kunjungan; catatan gizi memakai tanggal pada tabel masing-masing.
- Template Pemeriksaan Dokter menampilkan dan mengelola data utama template; daftar diagnosa, tindakan, resep, dan permintaan penunjangnya tetap dikelola melalui alur template di Khanza.
- Pengkajian awal batch kelima baca-saja; data lengkap tetap diakses dari formulir klinis legacy.
- Pengkajian spesialis batch keenam juga baca-saja; laporan tidak menggantikan formulir penilaian klinis legacy.
- Form awal medis dan pengkajian bayi baru lahir batch ketujuh baca-saja; input klinis tetap dilakukan lewat formulir Khanza.
- Peminjaman, retensi, mutasi, berkas digital, pengaduan, dan insiden ditampilkan baca-saja; pengelolaan alur dan file fisik tetap melalui prosedur Khanza/rumah sakit.
- Daftar kematian, diagnosis, HAIs, klasifikasi rawat inap, dan SOAP menyediakan filter tanggal sesuai kolom tanggal pada schema.
- Penomoran ID pada master dengan kolom `AUTO_INCREMENT` dihitung dari nilai maksimum saat membuka formulir; hindari penyimpanan bersamaan untuk master yang sama.
- Kueri menggunakan `schema_sik.sql`; belum dicocokkan dengan data klinis produksi atau seluruh variasi skema instalasi.

Daftar 166 tile yang belum terbuka ada di [TILE_BACKLOG.md](TILE_BACKLOG.md).
