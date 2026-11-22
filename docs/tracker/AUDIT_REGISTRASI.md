# Audit Registrasi (DlgReg.java vs RegistrasiView)

Diaudit 22 November 2026. Sumber: `simrskhanza/DlgReg.java` (handler tombol, baris 6794-7017 dan sekitarnya). Avalonia: `RegistrasiView.axaml(.cs)` dan `RegistrasiRepository`. Status per ID ada di [fitur.tsv](fitur.tsv); 348 popup klik kanan sudah dinilai sebelumnya, 27 tombol dan jendela dinilai di audit ini.

Hasil tombol/jendela setelah perbaikan A-D: 4 selesai, 6 sebagian, 13 kurang, 1 ditunda, jendela sebagian. (Sebelum perbaikan: 3 selesai, 8 sebagian, 14 kurang.)

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

## Masih tertinggal
Pemilih pasien lewat dialog, dialog perujuk dan asal rujukan, tab Rujukan Internal Poli, grafik/demografi dari layar ini, tanggal-jam mundur, mode urut no. reg poli atau dokter+poli, closing kasir, catatan pasien, cetak berkop RS, hapus banyak baris.
