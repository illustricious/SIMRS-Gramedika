# Audit Rawat Jalan (DlgRawatJalan.java vs RawatJalanView)

Diaudit 22 November 2026 dengan membandingkan kode Java dan C#, tanpa menjalankan aplikasi atau database. Java: `simrskhanza/DlgRawatJalan.java` (tab Penanganan Dokter, Petugas, Dokter & Petugas, Pemeriksaan, obstetri, Catatan, dan sekitar 150 tombol formulir). Avalonia: `RawatJalanView.axaml(.cs)`, `RawatJalanRepository`, `TindakanRalanView`. Status per ID ada di [fitur.tsv](fitur.tsv).

Hasil awal 207 baris: 1 selesai, 37 sebagian, 164 kurang (ditambah 1 jendela sebagian dan 4 popup kurang). Setelah perbaikan A-E: 2 selesai, 43 sebagian (termasuk jendela), 158 kurang untuk aksi dan jendela, ditambah 4 popup kurang.

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

## Masih tertinggal
Tab tindakan dokter/petugas/gabungan, obstetri, catatan pasien, template dan 5 SOAP terakhir, resep dan obat, permintaan lab/radiologi, jadwal operasi, rujukan, SKDP, triase, resume, sekitar 120 tombol formulir klinis, hapus banyak baris, cetak. Layar Farmasi `InputPenjualanView` memakai NIP placeholder yang sama; sudah diperbaiki: petugas dipilih dari tabel `petugas` aktif, akun biasa terkunci pada NIP sendiri (`PetugasPilihan.cs`), tidak ada lagi `D0000099` di kode produksi.
