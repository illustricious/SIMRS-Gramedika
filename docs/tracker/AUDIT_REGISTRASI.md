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

- **Set Status** (Sudah, Belum, Batal): ditolak bila pasien punya data kamar inap atau billing. Batal membuat `biaya_reg` 0. Berbeda dari Java: perlu hak `edit_registrasi` atau Admin Utama, dibatasi 2x24 jam untuk non-Admin Utama, dan penolakan billing berlaku untuk ketiga status (Java hanya untuk Batal). Seperti Java, mengembalikan registrasi Batal ke Sudah/Belum tidak memulihkan biaya registrasi.
- **Catatan Pasien:** baca, simpan, dan hapus pada `catatan_pasien` dengan hak `catatan_pasien`.
- **Laporan, Grafik Analisa, Input Data:** membuka laporan baca-saja yang sudah ada. Laporan pasien disaring persis menurut No. RM. Laporan berperiode tetap memakai periode bawaan layar; ubah tanggal untuk melihat data lama. Berkas digital dan insiden hanya daftar, belum input.
- **Cetak:** Bukti Register 1-3, Check List 1-4, dan Lembar Periksa 1-6 dari template Jasper Khanza. Logo dan email RS tidak termasuk.
- Pemeriksaan: seluruh format cetak digambar dengan data sintetis; status dan catatan diuji pada MariaDB 11.4.8 lokal dengan lima tabel dari `schema_sik.sql`. Untuk mengulang, buat `simrs_registrasi_fixture` (`pasien`, `reg_periksa`, `kamar_inap`, `billing`, `catatan_pasien`) lalu set `SIMRS_REGISTRASI_FIXTURE_PORT`. Query cetak, query laporan, klik menu di aplikasi, dan printer fisik belum diuji.

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
