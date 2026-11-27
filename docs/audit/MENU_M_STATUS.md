# Menu M — Olah Data Rekam Medis Pasien

Per 22 November 2026, **255 dari 255 kemunculan tile** sudah membuka layar: tile Pasien dan seluruh 254 tile lainnya di Menu M. Tidak ada tile Menu M yang tersisa tanpa rute. Screen coverage tidak berarti seluruh alur legacy sudah direplikasi.

## Formulir isian (28 November 2026)

Sebagian tile catatan klinis kini punya formulir isian lewat tombol **Isi / Ubah Formulir** (34 formulir, termasuk 19 lembar observasi dan pemantauan). Daftar dan batasnya di [audit rekam medis](../tracker/AUDIT_REKAM_MEDIS.md). Bagian di bawah yang menyebut catatan klinis hanya-baca ditulis sebelum formulir itu ada.

## Identitas pasien — perubahan 26 November 2026

Memilih pasien pada daftar kini memuat data ke form sebagai mode ubah. Tombol Simpan memperbarui kolom identitas yang tersedia di form dan mempertahankan kolom legacy yang belum tersedia, termasuk kode lokasi serta referensi TNI/POLRI. Tombol Baru mengembalikan form ke mode pembuatan pasien. Nomor RM baru membaca aturan `set_urut_no_rkm_medis` dan nomor terakhir `set_no_rkm_medis`; penyimpanan pasien mengunci urutan, mencari nomor yang belum dipakai, menyimpan pasien dan memperbarui nomor terakhir dalam satu transaksi. Pencarian pasien mendukung nama, No. RM, KTP, telepon, nama ibu, alamat, dan email serta dapat dijalankan dengan Enter.

Perubahan database dan interaksi UI belum diuji pada sesi ini.

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

## Batch kedelapan

**Daftar baca-saja:** 20 pengkajian awal medis spesialis, 9 hasil USG/diagnostik/endoskopi, 11 pengkajian tambahan, dan 5 catatan observasi. Tiap daftar menampilkan kolom ringkasan yang sesuai form legacy serta mendukung pencarian dan rentang tanggal.

## Batch kesembilan

**Daftar baca-saja:** Catatan observasi bayi/CHBP/induksi/restrain/ventilator/hemodialisa, keseimbangan dan catatan cairan, Follow Up DBD, catatan keperawatan dan persalinan, reaksi transfusi, GRACE/BISHOP dan pemantauan PEWS/EWS/MEOWS, hemodialisa, fisioterapi/terapi wicara/okupasi/psikologi, 14 form perioperatif, serta 10 checklist kriteria HCU/NICU/PICU/ICU/isolasi. Layar menampilkan ringkasan kunjungan, hasil, dan status sesuai form serta mendukung pencarian dan rentang tanggal.

## Batch kesepuluh

**Master yang bisa dikelola:** Kesimpulan & Anjuran MCU. Penghapusan dinonaktifkan karena referensi dapat dipakai pada pengkajian MCU.

**Daftar baca-saja:** Perencanaan pulang, enam pengkajian lanjutan risiko jatuh, skrining fungsional, penilaian/intervensi nyeri, risiko dekubitus, layanan rehabilitasi/KFR dan ESWL, pengkajian MCU, layanan informasi/konsultasi, transfer dan restrain, rekonsiliasi/GDS/fibrinolitik, alur MPP dan edukasi, skrining kesehatan masyarakat, instrumen mental, TOLAC, serta laporan tindakan. Layar menampilkan ringkasan kunjungan dengan pencarian dan rentang tanggal.

## Batasan yang diketahui

- Data kelahiran, catatan pasien, diagnosis, HAIs, klasifikasi rawat inap, dan SOAP hanya ditampilkan. Form pencatatan klinis tidak dipindahkan ke layar ini.
- SOAP TNI/POLRI dibatasi dengan data keanggotaan pasien pada tabel `pasien_tni`/`pasien_polri`. Data triase menggabungkan penilaian primer, sekunder, dan rincian skala yang tersedia.
- Resume dan catatan gizi batch ketiga baca-saja. Resume memakai tanggal registrasi kunjungan; catatan gizi memakai tanggal pada tabel masing-masing.
- Template Pemeriksaan Dokter menampilkan dan mengelola data utama template; daftar diagnosa, tindakan, resep, dan permintaan penunjangnya tetap dikelola melalui alur template di Khanza.
- Pengkajian awal batch kelima baca-saja; data lengkap tetap diakses dari formulir klinis legacy.
- Pengkajian spesialis, hasil diagnostik, skrining tambahan, observasi, pemantauan, layanan terapi, form perioperatif dan layanan klinis batch kesepuluh tetap baca-saja; laporan tidak menggantikan formulir klinis legacy.
- Skrining Instrumen Mental Emosional Anak mengikuti implementasi legacy yang menggunakan tabel `skrining_instrumen_acrs`.
- Form awal medis dan pengkajian bayi baru lahir batch ketujuh baca-saja; input klinis tetap dilakukan lewat formulir Khanza.
- Peminjaman, retensi, mutasi, berkas digital, pengaduan, dan insiden ditampilkan baca-saja; pengelolaan alur dan file fisik tetap melalui prosedur Khanza/rumah sakit.
- Daftar kematian, diagnosis, HAIs, klasifikasi rawat inap, dan SOAP menyediakan filter tanggal sesuai kolom tanggal pada schema.
- Penomoran ID pada master dengan kolom `AUTO_INCREMENT` dihitung dari nilai maksimum saat membuka formulir; hindari penyimpanan bersamaan untuk master yang sama.
- Kueri menggunakan `schema_sik.sql`; belum dicocokkan dengan data klinis produksi atau seluruh variasi skema instalasi.

Menu M sudah lengkap. Sisa 183 tile tanpa layar berada di Menu L dan tercatat di [TILE_BACKLOG.md](TILE_BACKLOG.md).
