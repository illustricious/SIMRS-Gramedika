# Audit Rawat Jalan (DlgRawatJalan.java vs RawatJalanView)

Diaudit 22 November 2026 dengan membandingkan kode Java dan C#, tanpa menjalankan aplikasi atau database. Java: `simrskhanza/DlgRawatJalan.java` (tab Penanganan Dokter, Petugas, Dokter & Petugas, Pemeriksaan, obstetri, Catatan, dan sekitar 150 tombol formulir). Avalonia: `RawatJalanView.axaml(.cs)`, `RawatJalanRepository`, `TindakanRalanView`. Status per ID ada di [fitur.tsv](fitur.tsv).

Hasil awal 207 baris: 1 selesai, 37 sebagian, 164 kurang (ditambah 1 jendela sebagian dan 4 popup kurang). Setelah perbaikan A-E: 2 selesai, 43 sebagian (termasuk jendela), 158 kurang untuk aksi dan jendela, ditambah 4 popup kurang.

> Bagian berikut hingga sebelum “Audit lanjutan” adalah catatan historis saat audit dilakukan. Beberapa celah sudah diperbaiki; gunakan bagian “Hak akses submenu dan input tindakan” di bawah untuk status terbaru.

## Temuan yang berisiko data

1. **NIP pemeriksa palsu.** `RawatJalanView` menyimpan setiap pemeriksaan dengan `PlaceholderExaminerNip = "D0000099"`. Kolom `pemeriksaan_ralan.nip` punya foreign key ke `pegawai.nik`, jadi di database nyata penyimpanan akan gagal kecuali pegawai itu ada. Bila ada, semua SOAP tercatat atas nama orang yang salah. Java memakai pegawai yang dipilih lewat tombol cari.
2. **Pemeriksaan yang sudah disimpan hilang dari layar.** Antrian hanya memuat kunjungan yang belum punya baris `pemeriksaan_ralan`. SOAP tidak bisa dilihat, diubah, dihapus, atau ditambah lagi untuk kunjungan yang sama (Java mengizinkan banyak baris per kunjungan).
3. **Antrian memuat kunjungan non-rawat-jalan.** `GetQueueAsync` tidak memfilter `status_lanjut='Ralan'`, jadi kunjungan rawat inap dan IGD ikut muncul. Hanya `GetUnexaminedVisitAsync` yang memfilter.
4. **Koreksi:** dugaan awal bahwa simpan SOAP ditolak bila billing terverifikasi keliru. Di Java, pengecekan `Sequel.cariRegistrasi` hanya ada di tab tindakan (dokter, petugas, gabungan), bukan di tab Pemeriksaan.

## Selisih fungsi lain
- Antrian hanya 7 hari terakhir tanpa pencarian, filter poli atau dokter.
- Form SOAP hanya tanda vital dan SOAP dasar: belum ada GCS, alergi, lingkar perut, tanggal dan jam, template, 5 SOAP terakhir, copy/paste SOAPIE.
- Tindakan dokter, petugas, dan gabungan (rawat_jl_dr/pr/drpr) tidak bisa diinput di sini; `TindakanRalanView` hanya daftar. Input tindakan hanya lewat barcode Menu B.
- Resep, obat dan BHP, permintaan lab/radiologi, jadwal operasi, rujukan, SKDP, triase, resume: tombolnya tidak ada; entri menu klik kanan sebagian besar tanpa aksi.
- Sekitar 120 tombol formulir (penilaian awal medis dan keperawatan, checklist, catatan, hasil USG/EKG/endoskopi, pemantauan EWS, dll.) belum ada sebagai input. Menu M menyediakan daftar baca-saja untuk banyak formulir itu, belum dipetakan per tombol.
- Skrining (sekitar 30 tombol) hanya bisa dibaca per kunjungan dari klik kanan Registrasi/IGD, bukan dari layar Rawat Jalan.

## Yang sudah ada
Tombol Keluar (Kembali ke Menu); simpan satu SOAP; Riwayat Perawatan dan Kamar Inap dari klik kanan (sebagian).

## Perbaikan A-E (22 November 2026)

Kode: `RawatJalanService.cs`, `RawatJalanView.axaml(.cs)`, tes `RawatJalanServiceTests.cs`.

- **A. NIP:** placeholder `D0000099` dihapus. Akun non-Admin Utama terkunci pada NIK-nya (harus ada di `pegawai`); Admin Utama memilih dokter/paramedis. Non-Admin hanya bisa menyimpan, mengganti, dan menghapus data atas NIK sendiri, seperti Java.
- **B. Antrian:** hanya `status_lanjut='Ralan'` dan bukan poli IGDK.
- **C. Daftar pemeriksaan per kunjungan:** banyak SOAP per kunjungan, tambah, ganti, hapus dengan konfirmasi. Ganti dan hapus untuk non-Admin Utama dibatasi 48 jam, dan waktu input tidak boleh sebelum waktu registrasi. Isian tensi dan SpO2 tidak lagi wajib (Java hanya meminta minimal satu isian).
- **D. Field:** GCS, alergi, lingkar perut, tanggal, jam, dan pilihan kesadaran lengkap 12 nilai.
- **E. Cari:** rentang tanggal (bawaan hari ini) dan kata kunci, serta jumlah SOAP per kunjungan.

Belum diuji ke database nyata; tes hanya untuk aturan murni (isi minimal, hak milik sendiri, waktu registrasi, daftar kesadaran).

## Area kerja dari Registrasi (23 November 2026)

- `DlgReg.java:8398` membuka `DlgRawatJalan`. Avalonia membuka `RawatJalanKunjunganView` di `PageHost` untuk nomor rawat dari baris klik kanan dan mengembalikan halaman Registrasi ketika pengguna memilih kembali.
- Bagian Tindakan memuat daftar dokter, petugas, dan gabungan dengan filter nomor rawat persis. SOAPIE memakai editor sebelumnya. Obstetri, ginekologi, diagnosa, dan catatan dokter tersedia baca saja untuk kunjungan tersebut.
- Panel samping mengikuti 178 tombol `FormMenu` Java dalam urutan sumber. Sebanyak 142 tombol dipetakan lewat kelas Java yang sama di menu dashboard. Lima lainnya menampilkan riwayat pasien, resume, obat/BHP, permintaan lab, dan permintaan radiologi. Sisanya tampil nonaktif. Hak akses tetap menentukan ketersediaan tiap laporan. Kueri laporan memakai nomor rawat persis. Riwayat Pasien mencakup kunjungan pasien yang sama.
- Tes headless memeriksa bahwa konteks dari baris klik kanan tetap benar ketika pilihan tabel berubah. MariaDB 11.4.8 sintetis meloloskan `EXPLAIN` untuk 151 kueri laporan. Memuat data klinis melalui aplikasi belum diverifikasi.

## Audit lanjutan (24 November 2026)

| Bagian | Bukti Khanza | Perubahan Gramedika dan selisih |
|---|---|---|
| Submenu klinis | `DlgRawatJalan.java:5055` memakai `BorderLayout.WEST`. | `RawatJalanKunjunganView` menempatkan submenu di kolom kiri. Tes headless memeriksa kolom dan konteks baris klik kanan. |
| Catatan Dokter | `DlgRawatJalan.java:6177` menerapkan izin catatan, `:14855` menyimpan, `:6633` mengubah, dan `:5715` menghapus. | `CatatanDokterView` dan `CatatanDokterService` menambah, mengubah, dan menghapus satu catatan. Akun biasa memakai kode dokter sendiri, dengan batas ubah/hapus 48 jam. Transaksi menolak catatan berubah atau kunci yang ganda. Hapus massal dan cetak belum tersedia. |
| Diagnosa | `DlgRawatJalan.java:12933` memeriksa izin `diagnosa_pasien`. | Tombol laporan kini memeriksa izin tersebut. Ubah diagnosa dan prosedur masih tertunda. |
| 5 SOAPIE Terakhir | `DlgRawatJalan.java:8227` dan `RMCari5SOAPTerakhir.java:359` memakai pasien yang sama, NIK penulis, pencarian, dan batas lima. | `RawatJalanService.LimaTerakhirAsync` memuat riwayat. Formulir menyalin enam narasi ke draf baru. Nomor rawat, waktu, dokter dan tanda vital mengikuti draf. Refresh setelah perubahan SOAPIE tetap pada kunjungan aktif jika dibuka dari Registrasi. |
| Tindakan dan tagihan | `DlgRawatJalan.java:12950` membedakan daftar tarif dan tindakan yang sudah dilakukan. `:12957` menyimpan tindakan dan memposting jurnal. | Tiga tab menampilkan tindakan yang sudah dilakukan secara baca saja. Daftar tarif pada tab tersebut, input, perubahan, dan pembalikan jurnal belum tersedia. |

Tracker Rawat Jalan saat ini memuat 207 entri. **2 selesai, 157 sebagian, 48 kurang.** Sebanyak 147 dari 178 menu kiri membuka laporan baca saja sesuai hak akun. 31 menu kiri masih nonaktif.

Uji integrasi C# pada MariaDB sintetis mencakup tambah, ubah, dan hapus Catatan Dokter, penolakan penulis lain, duplikat, dan konflik catatan lama, serta riwayat SOAPIE lintas kunjungan pasien yang sama.

## Sisa pekerjaan pada saat itu (sebelum audit hak akses)

Daftar tarif dan input tindakan dokter/petugas/gabungan dengan jurnal, input pada tab obstetri, ginekologi serta diagnosa, fungsi tulis untuk laporan yang baru baca saja, 31 menu kiri nonaktif, template SOAPIE, salin/tempel bebas, hapus massal, dan cetak.

## Hak akses submenu dan input tindakan (24 November 2026)

| Bagian | Bukti Khanza | Gramedika |
|---|---|---|
| Akses submenu kiri | `DlgRawatJalan.java:11332`–`:11929` dan kolom dari getter `fungsi/akses.java`. Semua 178 kontrol katalog punya pemetaan langsung. | `MenuKlinisRalanCatalog` menyimpan kolom izin per tombol. `RawatJalanKunjunganView` menyembunyikan menu tanpa izin, dan laporan tetap memakai nomor rawat yang dipilih. |
| Resume dan Riwayat | `DlgRawatJalan.java:11396` memakai `data_resume_pasien`; `:11436` memakai `resume_pasien`. | Tes headless menguji akun yang hanya memiliki izin Resume. Resume tersedia baca saja, Riwayat tersembunyi. |
| Hemodialisa dan Iterasi Resep BPJS | Getter HD di `fungsi/akses.java:5017` membaca `penilaian_medis_hemodialisa`. Di `DlgRawatJalan.java:11343`, iterasi memerlukan pengaturan aktif, penjamin BPJS dan `resep_dokter`. | Kolom HD sudah sesuai. Iterasi Resep BPJS disembunyikan sampai pengaturan dan alur BPJS tersedia. |
| Tarif dan pelaksana | `tampilTindakanDr/Pr/DrPr` memakai tarif aktif dan positif, dengan poli dan penjamin sesuai `set_tarif`. | Form **Input Tindakan** memilih jenis, pelaksana terdaftar, tanggal dan jam serta satu atau beberapa tarif. Tarif spesifik dan tarif umum `-` mengikuti aturan kunjungan. |
| Simpan dan jurnal | `DlgRawatJalan.java:5115`, `:12957`, `:13055`, `:13153` dan `:14656` memeriksa waktu, akses tindakan, billing serta menulis tindakan dan jurnal. | `TindakanRalanInputService` menyimpan ke tiga tabel `rawat_jl_*` dan memposting jurnal dalam satu transaksi. Kunjungan dikunci, tarif diperiksa ulang, dan registrasi batal, sudah bayar atau punya baris billing ditolak. Input parsial setelah billing masih tertunda. |
| Master Tarif | `DlgRawatJalan.java:7223` membuka `DlgJnsPerawatanRalan` dengan izin `tarif_ralan`. | Pintasan ke master dari area kerja belum tersedia. |

Tracker saat ini mencatat 207 entri Rawat Jalan dengan **3 selesai, 164 sebagian dan 40 kurang**. Katalog tetap berisi 178 tombol, termasuk 147 laporan baca saja. Dari 31 fungsi yang belum aktif, Iterasi Resep BPJS disembunyikan dan menu lain tampil nonaktif sesuai izin masing-masing.

`dotnet build` selesai dengan 0 error. `dotnet test` lulus 154, gagal 0 dan dilewati 0, dengan fixture Rawat Jalan serta Rawat Inap di MariaDB 11.4.8 lokal. Fixture Rawat Jalan memakai DDL Khanza dan data sintetis. Tes mencakup filter poli dan penjamin, tiga kategori tindakan, jurnal Rp690 pada tiap sisi, hak akses, tarif kedaluwarsa, penolakan duplikasi dan billing, serta rollback saat rekening kosong. Uji di salinan database rumah sakit dan alur billing bersamaan dengan klien Java belum diverifikasi.

Untuk mengulang tes tindakan, gunakan skema Khanza kosong di database lokal "simrs_ralan_fixture". Isi SIMRS_RALAN_FIXTURE_PORT dan SIMRS_RALAN_FIXTURE_PASSWORD, lalu jalankan dotnet test dengan filter TindakanRalanInputTests. Tes memasukkan data sintetis dan mensyaratkan tabel registrasi kosong.


## Audit Catatan Dokter, tindakan dan riwayat SOAPIE (historis, sebelum perubahan tindakan berikutnya)

| Alur | Hak dan keadaan | Keterbatasan yang masih terbuka |
|---|---|---|
| Catatan Dokter | Izin `catatan_perawatan` diperiksa pada daftar, pemilih, simpan dan hapus di `CatatanDokterService`. Non-admin menyimpan atas akun dokter sendiri; ubah/hapus hanya miliknya dalam 48 jam; Admin Utama dapat memilih dokter. Nomor rawat dan perubahan bersamaan diperiksa. | Hapus massal dan cetak belum tersedia. Catatan sesi sebelumnya mencatat fixture MariaDB sintetis lulus; belum diuji pada salinan RS. |
| Tindakan | Izin `tindakan_ralan` diperiksa pada pilihan tarif/pelaksana dan simpan. Input dokter, petugas dan gabungan memakai tarif kunjungan dan jurnal dalam satu transaksi, dengan guard billing. Java memperbolehkan pemilihan pelaksana lain untuk tindakan; kepemilikan SOAPIE tidak diterapkan pada tindakan. | Ubah/hapus tindakan dan jurnal pembalik belum tersedia. Penguncian bersamaan dengan billing Java belum diverifikasi. Input parsial setelah billing dan master tarif juga belum tersedia. |
| Pemeriksaan dan lima SOAPIE terakhir | Java `DlgRawatJalan.java:6104-6180` memakai `tindakan_ralan` pada pemeriksaan; `RMCari5SOAPTerakhir.java:350-410` menyediakan Ralan dan Ranap. Layanan kini memeriksa izin sebelum membaca/menulis, membatasi pemilihan penulis akun biasa, dan menyediakan riwayat kedua jenis untuk pasien dan penulis yang sama. Ubah/hapus memeriksa kunci kunjungan dan data lama di transaksi terkunci; waktu 48 jam memakai waktu server. | Fixture yang mencakup riwayat Ralan/Ranap, konflik, dan ubah/hapus ditambahkan tetapi belum dijalankan dengan MariaDB pada putaran ini. Cetak, template dan hapus massal belum tersedia. |

Build Release berhasil. Tes otomatis di luar sandbox: 154 lulus, 0 gagal, 2 fixture MariaDB dilewati. Hasil ini tidak memverifikasi perubahan SQL atau alur UI terhadap database. Jalankan fixture `SIMRS_RALAN_FIXTURE_PORT` pada skema uji kosong dan uji lintas koneksi sebelum mengandalkan operasi untuk layanan nyata. Hak akses aplikasi mengikuti akun yang dimuat saat login; ini bukan pengganti pembatasan akses database langsung.


## Tindakan Rawat Jalan dan transaksi (24 November, terbaru)

`TindakanRalanInputService` kini mendukung ubah dan hapus satu tindakan dokter, petugas atau gabungan dengan izin `tindakan_ralan`, batas 48 jam untuk non-admin, pemeriksaan billing dan snapshot, serta jurnal pembalik dalam transaksi yang sama. Ubah memilih satu tarif baru dan memposting nilai baru bersama pembalik. Fixture MariaDB 11.4.8 lulus untuk ketiga kategori, rekening hilang/rollback, billing dan perubahan data. Daftar tindakan di halaman kunjungan menyediakan tombol ubah dan hapus. Input parsial setelah billing, hapus massal, master tarif dan audit UI lengkap masih tertunda.

Tes dua koneksi mencakup billing dikomit sebelum tindakan dan tindakan dikomit sebelum kasir. Yang kedua menjalankan `KasirRepository.MarkPaidAsync`. Keduanya menggunakan kunci kunjungan yang sama; kesesuaian terhadap klien Java yang berjalan bersamaan belum diuji.

## Pemeriksaan awal diagnosa dan prosedur Rawat Jalan

`laporan/PanelDiagnosa.java:1114` menyimpan ICD-10 ke `diagnosa_pasien` dan ICD-9 ke `prosedur_pasien`; keduanya memakai status `Ralan`, prioritas, dan kunci kunjungan. Diagnosis berstatus Lama bila kode yang sama pernah tercatat pada pasien tersebut, selain itu Baru. Java juga mengisi kode diagnosis dan prosedur pada `resume_pasien` untuk urutan tertentu. Handler hapus pada `PanelDiagnosa.java:1291` perlu dicocokkan dengan pembaruan resume dan penghapusan lintas status sebelum operasi tulis dibuka.

Di Gramedika, tombol Diagnosa masih membuka laporan baca saja dan disaring dengan izin `diagnosa_pasien`. Implementasi input, penanganan prioritas dan duplikasi, sinkronisasi resume, pengujian hak akses dan transaksi database masih tertunda. Seluruh butir Rawat Jalan lain pada permintaan ini tetap terbuka sesuai [prioritas](PRIORITAS.md).


Input awal ICD-10/ICD-9 tersedia melalui `DiagnosaRalanView` dan `DiagnosaRalanService`: cari, pilih dan tambah satu kode dengan izin `diagnosa_pasien`, prioritas dan pembaruan kode resume. Build dan tes penolakan izin lulus; query dan sinkronisasi resume belum diuji pada database sintetis. Ubah/hapus, audit perbandingan lengkap dan seluruh fitur lain pada permintaan masih tertunda.


### Lanjutan diagnosa dan prosedur

Tambah dan hapus satu kode ICD-10/ICD-9 dengan izin `diagnosa_pasien` telah diuji pada MariaDB sintetis. Penghapusan membersihkan kode resume hanya jika nilainya masih cocok. Tes mencakup prioritas, status baru/lama, duplikasi prioritas, jumlah, dan penolakan tanpa izin. Prioritas dibatasi lima diagnosis dan empat prosedur; ubah, hapus massal, teks resume, pencetakan, uji UI dan penggunaan bersamaan dengan Java masih tertunda.


### Input awal obstetri dan ginekologi

Form `ObgynRalanView` dan `PemeriksaanObgynService` menyediakan simpan, ubah, hapus satu pemeriksaan dan simpan PDF, dengan hak `tindakan_ralan`. Ubah/hapus memeriksa batas 48 jam untuk non-admin dan menolak data yang telah berubah. Fixture MariaDB sintetis memeriksa simpan, duplikat, ubah/hapus, konflik, izin, seluruh field pada panjang maksimum dan pilihan terakhir, serta penolakan nilai enum dan panjang yang tidak valid. Uji UI, verifikasi visual PDF, semua kombinasi pilihan, batas waktu 48 jam di database dan penggunaan bersamaan dengan Java masih tertunda.

## Order penunjang dan resume medis (25 November 2026)

- Dari daftar pemeriksaan Rawat Jalan, pilih kunjungan lalu **Alur Kunjungan**. Jalur klik kanan Registrasi **Tindakan & Pemeriksaan → Tindakan Rawat Jalan** membuka area kerja yang sama. Empat menu disaring dengan izin `resep_dokter`, `permintaan_lab`, `permintaan_radiologi` dan `data_resume_pasien`.
- **Input Resep** mengirim obat nonracikan dengan jumlah dan aturan pakai ke Daftar Resep Farmasi. Akun dokter dicatat sebagai penulis. Admin Utama memakai dokter kunjungan. Racikan, verifikasi/penyerahan, pembatalan resep dan perhitungan stok serta tarif obat dinamis belum tersedia dari alur ini.
- **Permintaan Lab** menerima Patologi Klinik, Patologi Anatomi dan Mikrobiologi. Pilihan tarif mengikuti `set_tarif`. PK/MB mendukung pilihan rincian pemeriksaan dan PA mendukung metadata bahan. **Permintaan Radiologi** mengirim daftar pemeriksaan. Keduanya menyimpan diagnosis klinis dan informasi tambahan, lalu menampilkan status penerimaan dari waktu sampel/hasil. Order ditolak untuk kunjungan yang batal, masuk rawat inap atau sudah masuk billing. Edit order terkirim dari Rawat Jalan dan alur billing parsial masih terbuka.
- **Resume Pasien** menambah dan mengubah narasi kunjungan serta menampilkan diagnosis/prosedur yang tercatat. Simpan pertama mengisi kode dan nama dari diagnosis/prosedur kunjungan. Perubahan narasi memeriksa kepemilikan dan data lama. Cetak, hapus dan penarikan narasi hasil secara otomatis masih terbuka.
- Fixture `AlurPenunjangRalanTests` memakai skema Khanza kosong pada MariaDB 11.4.8 dengan `SIMRS_PENUNJANG_FIXTURE_PORT` dan database `simrs_penunjang_fixture`. Tes membaca order dari repository Farmasi, Laboratorium dan Radiologi yang digunakan layar penerima. Cakupan meliputi rollback order, kategori lab, pembatasan akses dan konsistensi resume. Interaksi visual aplikasi, pengolahan hasil aktual dan penggunaan bersamaan dengan klien Java belum diuji.
