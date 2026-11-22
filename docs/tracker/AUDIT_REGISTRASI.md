# Audit Registrasi (DlgReg.java vs RegistrasiView)

Diaudit 22 November 2026. Sumber: `simrskhanza/DlgReg.java` (handler tombol, baris 6794-7017 dan sekitarnya). Avalonia: `RegistrasiView.axaml(.cs)` dan `RegistrasiRepository`. Status per ID ada di [fitur.tsv](fitur.tsv); 348 popup klik kanan sudah dinilai sebelumnya, 27 tombol dan jendela dinilai di audit ini.

Hasil tombol/jendela: **3 selesai, 8 sebagian, 14 kurang, 1 ditunda (bridging), 1 jendela sebagian**.

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

## Usulan perbaikan (belum dikerjakan)
A. Registrasi baru (pilih pasien + cek rawat inap + status Baru/Lama + biaya + kuota) dengan tes. B. Hapus dengan aturan 48 jam dan cek tagihan. C. Cek tagihan/48 jam/hak pada Ubah. D. Kecualikan IGDK di daftar. E. Perluas pencarian.
