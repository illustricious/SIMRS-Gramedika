# Audit Rawat Jalan (DlgRawatJalan.java vs RawatJalanView)

Diaudit 22 November 2026 dengan membandingkan kode Java dan C#, tanpa menjalankan aplikasi atau database. Java: `simrskhanza/DlgRawatJalan.java` (tab Penanganan Dokter, Petugas, Dokter & Petugas, Pemeriksaan, obstetri, Catatan, dan sekitar 150 tombol formulir). Avalonia: `RawatJalanView.axaml(.cs)`, `RawatJalanRepository`, `TindakanRalanView`. Status per ID ada di [fitur.tsv](fitur.tsv).

Hasil 207 baris: **1 selesai, 37 sebagian, 164 kurang, 5 jendela/popup tercatat** (1 jendela sebagian, 4 popup kurang).

## Temuan yang berisiko data

1. **NIP pemeriksa palsu.** `RawatJalanView` menyimpan setiap pemeriksaan dengan `PlaceholderExaminerNip = "D0000099"`. Kolom `pemeriksaan_ralan.nip` punya foreign key ke `pegawai.nik`, jadi di database nyata penyimpanan akan gagal kecuali pegawai itu ada. Bila ada, semua SOAP tercatat atas nama orang yang salah. Java memakai pegawai yang dipilih lewat tombol cari.
2. **Pemeriksaan yang sudah disimpan hilang dari layar.** Antrian hanya memuat kunjungan yang belum punya baris `pemeriksaan_ralan`. SOAP tidak bisa dilihat, diubah, dihapus, atau ditambah lagi untuk kunjungan yang sama (Java mengizinkan banyak baris per kunjungan).
3. **Antrian memuat kunjungan non-rawat-jalan.** `GetQueueAsync` tidak memfilter `status_lanjut='Ralan'`, jadi kunjungan rawat inap dan IGD ikut muncul. Hanya `GetUnexaminedVisitAsync` yang memfilter.
4. **Tidak menolak billing.** Java menolak simpan bila tagihan sudah terverifikasi (`Sequel.cariRegistrasi`). Avalonia tidak mengeceknya.

## Selisih fungsi lain
- Antrian hanya 7 hari terakhir tanpa pencarian, filter poli atau dokter.
- Form SOAP hanya tanda vital dan SOAP dasar: belum ada GCS, alergi, lingkar perut, tanggal dan jam, template, 5 SOAP terakhir, copy/paste SOAPIE.
- Tindakan dokter, petugas, dan gabungan (rawat_jl_dr/pr/drpr) tidak bisa diinput di sini; `TindakanRalanView` hanya daftar. Input tindakan hanya lewat barcode Menu B.
- Resep, obat dan BHP, permintaan lab/radiologi, jadwal operasi, rujukan, SKDP, triase, resume: tombolnya tidak ada; entri menu klik kanan sebagian besar tanpa aksi.
- Sekitar 120 tombol formulir (penilaian awal medis dan keperawatan, checklist, catatan, hasil USG/EKG/endoskopi, pemantauan EWS, dll.) belum ada sebagai input. Menu M menyediakan daftar baca-saja untuk banyak formulir itu, belum dipetakan per tombol.
- Skrining (sekitar 30 tombol) hanya bisa dibaca per kunjungan dari klik kanan Registrasi/IGD, bukan dari layar Rawat Jalan.

## Yang sudah ada
Tombol Keluar (Kembali ke Menu); simpan satu SOAP; Riwayat Perawatan dan Kamar Inap dari klik kanan (sebagian).

## Usulan perbaikan (belum dikerjakan)
A. NIP dari akun login (Admin Utama memilih pegawai) menggantikan placeholder. B. Filter `status_lanjut='Ralan'` dan poli bukan IGDK di antrian. C. Daftar pemeriksaan per kunjungan dengan tambah, ubah, hapus, dan tolak bila billing terverifikasi. D. Tambahkan GCS, alergi, lingkar perut, tanggal dan jam. E. Cari dan rentang tanggal di antrian.
