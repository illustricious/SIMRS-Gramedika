# Audit Registrasi (DlgReg.java vs RegistrasiView)

Diaudit 22 November 2026. Sumber: `simrskhanza/DlgReg.java` (handler tombol, baris 6794-7017 dan sekitarnya). Avalonia: `RegistrasiView.axaml(.cs)` dan `RegistrasiRepository`. Status per ID ada di [fitur.tsv](fitur.tsv); 348 popup klik kanan sudah dinilai sebelumnya, 27 tombol dan jendela dinilai di audit ini.

Hasil tombol setelah perbaikan A-D: 4 selesai, 10 sebagian, 11 kurang, 1 ditunda; jendela sebagian. (Sebelum perbaikan: 3 selesai, 8 sebagian, 14 kurang, 1 ditunda.)

## Selisih terpenting (urut dampak)

1. **Tidak ada registrasi baru dari layar Registrasi.** Tombol Simpan hanya mengubah registrasi yang sudah dipilih. `CreateAsync` hanya dipakai layar IGD. Java `BtnSimpan` juga menolak pasien yang masih dirawat inap, memeriksa kuota dokter (jadwal aktif), menentukan status daftar Baru/Lama dan biaya sesuai status. `CreateAsync` selalu "Lama" dan memakai satu biaya poli.
2. **Hapus tidak berfungsi** (placeholder). Java menghapus `reg_periksa` dengan aturan 48 jam kecuali Admin Utama.
3. **Ubah tidak menjaga aturan Java:** tagihan terverifikasi (`Sequel.cariRegistrasi`) harus menolak ubah, ada batas 48 jam dan hak `edit_registrasi`. Avalonia mengubah tanpa cek itu.
4. **Daftar mencampur IGD.** Java mengecualikan `poliklinik.kd_poli='IGDK'` di daftar dan cetak; Avalonia tidak.
5. **Pencarian lebih sempit:** Java mencari juga tanggal, status daftar, poli, dokter, penanggung jawab, alamat PJ, jenis bayar, hubungan PJ.
6. **Belum ada:** pemilih pasien, dialog cari dokter/poli/penjab (diganti ComboBox), dialog perujuk (asal rujukan hanya dibaca), tab Rujukan Internal Poli, grafik dan demografi dari layar ini (tabelnya ada di Menu O), cetak pasien terpilih. Cek bridging ditunda bersama Menu L.
7. **Cetak** memakai PDF sederhana, belum berkop RS seperti `rptReg.jasper`.

## Yang sudah setara
Tombol Semua, Keluar, dan toggle Input Data.

## Perbaikan A-D (22 November 2026)

Kode: `RegistrasiService.cs`, `RegistrasiView.axaml(.cs)`, tes `RegistrasiServiceTests.cs`.

- **A. Registrasi baru:** tombol Baru, isi No. RM lalu Enter (nama, penanggung jawab, jenis bayar terisi dari pasien), pilih dokter dan unit, Simpan. Aturan Java: tolak pasien rawat inap aktif, no. reg per dokter per hari, no. rawat `yyyy/MM/dd/NNNNNN` dari MAX+1 (bukan hitung baris, sehingga aman setelah ada yang dihapus) dengan ulang saat nomor bentrok, status daftar Baru/Lama, status poli, biaya registrasi baru/lama, umur. Kuota jadwal tersedia tetapi nonaktif bawaan karena pengaturannya belum ada di Gramedika.
- **B. Hapus:** konfirmasi, batas 48 jam untuk non-Admin Utama, ditolak bila sudah ada billing.
- **C. Ubah:** ditolak bila ada billing atau batal, hak `edit_registrasi` dan batas 48 jam untuk non-Admin Utama, biaya dihitung ulang bila poli berubah.
- **D. IGD:** kunjungan poli IGDK tidak lagi tampil di daftar Registrasi. Pencarian juga diperluas (E).

Belum diuji ke database nyata: tes hanya untuk aturan murni (batas 48 jam, umur, kolom biaya, hari jadwal).

## Sisa pekerjaan per 25 November 2026
Pendaftaran pasien baru dari dialog dengan penomoran RM sesuai pengaturan Khanza, grafik dan demografi dari layar ini, tanggal-jam mundur, mode urut no. reg poli atau dokter+poli, closing kasir, catatan pasien, cetak berkop RS, dan hapus banyak baris.

## Tampilan menu klik kanan (24 November 2026)

Menu pasien memakai palet Gramedika pada panel, sorotan, teks dan penanda submenu di tema terang dan gelap. Tinggi minimum baris turun dari 30 ke 25 piksel logis. Jarak tepi panel dan pemisah dirapatkan. Tes headless di `MenuCScreensTests.cs` memeriksa tinggi baris, warna panel utama dan submenu, sorotan, serta chevron pada kedua tema. Bukti ditambahkan ke entri popup terkait di tracker.

## Pemilih pasien Registrasi

Pada mode Baru, tombol Cari mencari pasien berdasarkan nomor RM atau nama dan mengisi formulir dari data pasien terpilih. Tombol tidak aktif pada mode ubah. Isian pasien lama dibersihkan saat nomor RM berubah; hasil pemuatan yang terlambat tidak diterapkan ke nomor RM yang berbeda. Pencarian dibatasi 100 hasil. Pendaftaran pasien baru dari dialog, uji database dan uji interaksi dialog lengkap masih tertunda. Entri `BtnPasien` berstatus sebagian karena Java juga membuka pengelolaan pasien.

## Daftar rujukan internal poli

Daftar awal pada 24 November menampilkan nomor rawat asal serta dokter dan poli tujuan. Filter periode, dokter, poli dan pencarian berlaku untuk daftar ini. Layanan memeriksa izin `registrasi`. Tombol dan formulir untuk registrasi awal dinonaktifkan ketika tab rujukan dibuka. Fixture MariaDB sintetis menguji filter tujuan, periode, pencarian dan izin. Tes headless memeriksa perpindahan tab.

## Operasi rujukan (25 November 2026)

- Registrasi Awal membuka formulir **Rujukan → Poli Internal** dari baris yang diklik. Simpan memerlukan hak `rujukan_poli_internal`; satu kunjungan hanya bisa memiliki satu rujukan per dokter tujuan. Saat tab rujukan dibuka, satu baris bisa dihapus dengan konfirmasi lewat tombol atau klik kanan, dan daftar hasil filter bisa dicetak ke PDF. Pilihan tujuan memakai lookup dokter dan poli Registrasi. Filter jadwal seperti Java, penghapusan beberapa rujukan sekaligus, dan uji dialog end-to-end masih tertunda.
- **Rujukan → Masuk** dan tombol **Rujukan Masuk** membuka data kunjungan tersimpan. Pengguna dengan hak `rujukan_masuk` dapat memilih perujuk dan alamat dari riwayat, mengisi formulir, lalu menambah atau mengubah data. Saat registrasi baru, draf rujukan mengikuti pasien terpilih dan disimpan bersama kunjungan dalam satu transaksi. Nomor balasan diisi manual. Penghapusan rujukan masuk dan cetak surat balasan masih tertunda.
- Fixture MariaDB 11.4.8 sintetis memeriksa izin, filter, tambah dan hapus rujukan internal, duplikat, tambah dan ubah rujukan masuk, pencarian perujuk, penolakan perubahan berdasarkan data lama, dan pembuatan PDF. Tes juga memeriksa penyimpanan bersama registrasi baru serta rollback ketika kode diagnosis tidak ditemukan. Tampilan PDF dan interaksi dialog dengan database belum diperiksa secara visual.

## Menu klik kanan lanjutan (26 November 2026)

Kode: `PatientContextMenu.cs`, `ContextReports.cs`, `ReportView.cs`, `CatatanPasienWindow.cs`, `PatientLabel.cs`, `RegistrasiService.SetStatusAsync`. Tes: `RegistrasiMenuTests.cs`.

- **Set Status:** Berkas R.M. Diterima, Sudah, Belum, Batal, Dirujuk, Dirawat, Meninggal, Pulang Paksa, dan Status Poli Baru/Lama. Ditolak bila pasien punya data kamar inap atau billing (Status Poli tidak terhalang). Batal membuat `biaya_reg` 0; Berkas Diterima juga mencatat `mutasi_berkas`. Berbeda dari Java: perlu hak `edit_registrasi` atau Admin Utama, dibatasi 2x24 jam untuk non-Admin Utama, dan penjagaan kamar inap serta billing berlaku untuk semua pilihan `stts`. Seperti Java, mengembalikan registrasi Batal ke status lain tidak memulihkan biaya. Dirawat membuka Kamar Inap; Dirujuk membuka formulir Rujukan Keluar.
- **Pasien Meninggal:** Set Status Meninggal membuka formulir `pasien_mati` bagi akun berhak `pasien_meninggal` (`PasienMeninggalWindow.cs`, `PasienMeninggalService.cs`). Data yang sudah ada dimuat lalu diperbarui. Belum ada: pencarian ICD-X, hapus, daftar, serta cetak surat kematian dan angkut jenazah. Fixture memakai tambahan tabel `dokter` dan `pasien_mati`.
- **Catatan Pasien:** baca, simpan, dan hapus pada `catatan_pasien` dengan hak `catatan_pasien`. Klik kiri nama pasien (tab Registrasi Awal dan Rujukan Internal) memunculkan catatan di bawah tabel bila `set_validasi_catatan.tampilkan_catatan` bernilai Yes.
- **Laporan, Grafik Analisa, Input Data:** membuka laporan baca-saja yang sudah ada. Laporan pasien disaring persis menurut No. RM. Laporan berperiode tetap memakai periode bawaan layar; ubah tanggal untuk melihat data lama. Berkas digital dan insiden hanya daftar, belum input.
- **Cetak:** Bukti Register 1-3, Check List 1-4, dan Lembar Periksa 1-6 dari template Jasper Khanza. Template-nya tidak memuat logo dan e-mail RS.
- Pemeriksaan: seluruh format cetak digambar dengan data sintetis; status dan catatan diuji pada MariaDB 11.4.8 lokal dengan sembilan tabel dari `schema_sik.sql`. Untuk mengulang, buat `simrs_registrasi_fixture` (`pasien`, `reg_periksa`, `kamar_inap`, `billing`, `catatan_pasien`, `set_validasi_catatan`, `mutasi_berkas`, `dokter`, `pasien_mati`) lalu set `SIMRS_REGISTRASI_FIXTURE_PORT`. Query cetak, query laporan, klik menu di aplikasi, dan printer fisik belum diuji.

## Pasien baru dari Registrasi (26 November 2026)

Kode: `PasienBaruService.cs`, `PasienBaruWindow.cs`, tombol **Pasien Baru** di `RegistrasiView`. Tes: `PasienBaruTests.cs`.

- Aktif pada mode Baru untuk akun berhak `pasien`. Setelah disimpan, No. RM langsung terisi di formulir registrasi.
- No. RM otomatis mengikuti `set_urut_no_rkm_medis` dan memajukan `set_no_rkm_medis` dalam satu transaksi dengan data pasien dan kode wilayah.
- Aturan wajib dan panjang minimal dibaca dari `set_kelengkapan_data_pasien`. Umur disimpan sebagai `Th Bl Hr`. Wilayah diisi sebagai nama; nama baru ditambahkan ke master, isian kosong menjadi `-`.
- P.J. "DIRI SENDIRI": isian P.J. kosong disalin dari data pasien.
- Belum ada: No. RM manual, pemilih wilayah, TNI/POLRI, NIP pegawai, cek BPJS/Dukcapil, foto, peringatan NIK ganda, dan cetak kartu.
- Pemeriksaan: MariaDB 11.4.8 lokal dengan 13 tabel dari `schema_sik.sql` (foreign key aktif) menguji penomoran Straight dan Terminal, kegagalan di tengah transaksi, serta aturan kelengkapan. Dialog digambar headless. Database RS dan dua loket bersamaan belum diuji. Untuk mengulang, muat tabel tersebut ke `simrs_pasien_fixture` kosong lalu set `SIMRS_PASIEN_FIXTURE_PORT`.

## Simpan di layar Pasien (26 November 2026)

Perbaikan: layar Pasien sebelumnya mengisi kode wilayah, suku, bahasa, dan cacat dengan 0 untuk pasien baru, sehingga ditolak foreign key pada database tanpa baris tersebut. Tambah dan ubah sekarang memakai `PasienBaruService`.

- Form menambah wilayah pasien dan P.J., pendidikan, No. Peserta, suku/bangsa, bahasa, cacat fisik, dan perusahaan.
- Aturan kelengkapan, umur, dan kode wilayah menurut nama berlaku juga saat ubah. Nomor RM, tanggal daftar, dan NIP tidak berubah.
- Akun harus berhak `pasien`. Isian yang gagal dimuat tidak dapat disimpan.
- Pemeriksaan: MariaDB 11.4.8 sintetis (`PasienBaruTests`) serta satu uji headless sementara untuk simpan, pilih baris, dan ubah dari layar. Database RS belum diuji.
- Pilihan golongan darah, status nikah, hubungan P.J., dan agama memakai tulisan Khanza, satu daftar untuk layar dan dialog (`PilihanIdentitas.cs`). Agama lama di luar daftar tetap tampil.

## Cetak berkop RS (26 November 2026)

Kode: `KopInstansi.cs`, `RegistrasiReport.cs`. Tes: `KopInstansiTests.cs`.

- Daftar Pasien Rawat Jalan, Daftar Rujukan Internal, dan daftar Registrasi IGD memakai kop dari tabel `setting` (baris `aktifkan = 'Yes'`): logo, nama, alamat, kontak, dan e-mail. Logo kosong atau rusak dilewati.
- Bukti Register, Check List, dan Lembar Periksa tetap mengikuti template Jasper Khanza, yang tidak memuat logo maupun e-mail.
- Pemeriksaan: kop dibaca dari tabel `setting` sintetis pada MariaDB 11.4.8 dan contoh PDF diperiksa secara visual. Logo asli RS dan printer fisik belum diuji.
- Cetak daftar di Registrasi mengikuti `rptReg`: halaman 913 x 595 poin, 14 kolom (tanggal, jam, dokter, No. RM, nama, telepon, umur saat daftar, J.K., poliklinik, penanggung jawab, alamat, status daftar, asal rujukan, jenis bayar), judul "DATA REGISTRASI PERIKSA", dan kaki tanggal serta nomor halaman. Urutan baris sama dengan daftar di layar; datanya dibaca ulang saat cetak (`RegistrasiCetak.cs`, tes `RegistrasiCetakTests.cs`).
- Beda yang tersisa: huruf bawaan QuestPDF ukuran 7,5 (Khanza Tahoma 8). Cetak rujukan internal dan IGD masih memakai susunan ringkas.

## Nomor antrean, kuota, dan simpan IGD (26 November 2026)

Kode: `RegistrasiService.NomorAntreanAsync`, `DatabaseSettings`, `KoneksiDatabaseWindow.cs`, `IgdView.axaml.cs`. Tes: `NomorAntreanTests.cs`.

- Profil koneksi memuat `URUTNOREG` (dokter, poli, dokter + poli), `BASENOREG` (registrasi, booking), dan `JADWALDOKTERDIREGISTRASI`. Bawaan tetap per dokter, dasar registrasi, tanpa kuota; samakan dengan `setting/database.xml` Khanza yang dipakai. Kuota tidak berlaku untuk Admin Utama dan poli IGDK.
- Perbaikan: simpan registrasi IGD sebelumnya menomori dari jumlah baris hari itu (bisa bentrok setelah ada yang dihapus), selalu "Lama", dan melewati cek rawat inap. Sekarang memakai layanan registrasi yang sama; dokter wajib dan isian P.J. kosong mengikuti data pasien.
- Pemeriksaan: MariaDB 11.4.8 sintetis untuk urutan nomor, booking, kuota, dan IGD; satu uji headless sementara untuk simpan IGD dan tampilan jendela koneksi. Dua loket bersamaan dan database RS belum diuji. Untuk mengulang, muat tabel terkait ke `simrs_antrean_fixture` kosong lalu set `SIMRS_ANTREAN_FIXTURE_PORT`.
- Belum ada: daftar dokter dibatasi jadwal saat kuota aktif, dan tanggal-jam mundur.

## Pencocokan tracker dengan kode (27 November 2026)

Banyak popup masih tercatat "kurang" walau sudah tersambung di aplikasi. 48 popup naik menjadi sebagian: label, barcode dan gelang (15), Check List (4), Lembar Periksa (6), Bukti Register 1 dan 2, permintaan Lab dan Radiologi, periksa Lab PK/PA/MB dan Radiologi, Resep Dokter, laporan RL 3.2 dan frekuensi penyakit, grafik berupa tabel (10), Berkas Digital dan Insiden. Penilaian berdasarkan pembacaan kode dan tes yang sudah ada; menu tidak diklik pada aplikasi dengan database.

Temuan: Java mencetak Bukti Register 1, 2, 3 dari `rptBuktiRegister`, `rptBuktiRegister3`, `rptBuktiRegister8`. Aplikasi saat itu memberi nomor 1, 2, 3 pada `rptBuktiRegister`, `rptBuktiRegister2`, `rptBuktiRegister3`; sudah diperbaiki (lihat bagian berikut).

Masih kurang di klik kanan Registrasi: Data Rekam Medis (133), Surat-Surat selain Bukti Register 1-2 (format 3 ditambahkan kemudian, lihat bagian berikut), menu tab Rujukan Internal, permintaan selain Lab/Radiologi, Operasi, Obat selain Resep Dokter, Billing Parsial, enam laporan, grafik jenis kelamin/pekerjaan/agama, Rujukan Keluar (ditambahkan kemudian), hapus data operasi dan Gabung Nomor Rawat.

## Rujukan Keluar dan Bukti Register (27 November 2026)

- **Rujukan → Keluar** (juga dibuka setelah Set Status Dirujuk) menambah atau mengubah tabel `rujuk` dengan hak `rujukan_keluar` (`RujukanKeluarWindow.cs`, `RujukanKeluarService.cs`). Kunjungan yang sudah dirujuk dibuka pada data terbaru. Nomor `R` + 9 angka diambil dari nomor terbesar di dalam kunci bernama; Java memakai jumlah baris, sehingga klien Java yang berjalan bersamaan masih dapat membuat nomor ganda. Status Dirujuk diubah setelah tersimpan (Java mengubahnya saat formulir dibuka) dan tidak diubah untuk pasien rawat inap. Belum ada: hapus, daftar, cetak daftar, dan pemilih diagnosa.
- **Bukti Register:** label 1, 2, 3 kini memakai `rptBuktiRegister`, `rptBuktiRegister3`, `rptBuktiRegister8`, sesuai Java. Temuan pada pencocokan tracker di atas sudah diperbaiki. Format 3 memuat logo RS dari `setting.logo`.
- **Surat Rujukan:** tombol pada formulir mencetak rujukan tersimpan sebagai PDF berkop RS (`SuratRujukanReport.cs`) dengan isi `rptSuratRujukan`: Tindakan merangkum rawat inap dokter, laboratorium, radiologi, dan operasi; Terapi memuat nama obat yang diberikan. QR tanda tangan: lihat bagian QR di bawah.
- Fixture memakai tambahan tabel `rujuk`, `rawat_inap_dr`, `periksa_lab`, `periksa_radiologi`, `operasi`, `detail_pemberian_obat`, `databarang`.

## Surat Cuti Sakit (27 November 2026)

- **Surat-Surat → Surat Cuti Sakit** menambah atau mengubah tabel `suratsakit` dengan hak `surat_sakit` (`SuratSakitWindow.cs`, `SuratSakitService.cs`). Nomor `SKS` + tanggal awal + 3 angka seperti Java, dibuat di dalam kunci bernama. Lama sakit terisi otomatis dari kedua tanggal ("3 (Tiga)") dan bisa ditulis ulang. Tanggal akhir sebelum tanggal awal ditolak (Java tidak).
- Cetak memakai isi `rptSuratSakit` ("Cetak Surat Sakit 2") sebagai PDF A5 berkop RS. Belum ada: hapus, daftar, dan empat format lain.
- **QR tanda tangan** (Surat Rujukan dan Surat Cuti Sakit): teks sama dengan parameter `finger` Java, yaitu nama RS, kabupaten, dokter, ID (SHA-1 `sidikjari` pegawai bila ada, selain itu kode dokter), dan tanggal. Dibuat dengan pustaka QRCoder 1.6.0 (MIT), koreksi galat H. Ini identitas penanda tangan, bukan tanda tangan elektronik tersertifikasi. Pemindaian hasil cetak belum diuji.
- Fixture memakai tambahan tabel `suratsakit`, `kelurahan`, `kecamatan`, `kabupaten`, `pegawai`, `sidikjari`.
