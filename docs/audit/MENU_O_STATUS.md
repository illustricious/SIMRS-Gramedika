# Menu O (Analisa & Info Grafik) - bagian 1

54 dari 113 tile berlayar. Kode: `src/SimrsKhanza.Data/Master/MenuOSpecs.cs`, tes: `MenuOSpecsTests.cs`.

Bentuk: tabel agregat hanya-baca (kategori, jumlah, total) lewat `ReportView`. Grafik batang/pai belum digambar. Tile Per Tahun memakai semua data, Per Bulan memilih tahun, Per Tanggal memilih rentang tanggal.

## Sudah
- Registrasi (24): Per Poli, Dokter, Pekerjaan, Pendidikan, Agama, Umur, Suku, Bahasa, Cacat Fisik, Cara Bayar, Demografi (kabupaten/kecamatan), Perujuk, Per Tahun/Bulan/Tanggal, Lama dan Baru x3, Batal x3.
- Rawat inap (6): Per Tahun/Bulan/Tanggal/Ruang, Status Pulang, Pasien Meninggal Per Bulan.
- Lab dan Rad ralan Per Tahun/Bulan/Tanggal (6, hitung no_rawat berbeda).
- Pegawai (9), Item Apotek (4), Inventaris (5).

## Belum (59)
TB, Kejadian IKP, K3, HAIs, limbah B3/domestik, air PDAM/tanah, porsi diet, perbaikan inventaris, pengajuan aset, hemodialisa, penerimaan obat per bulan.

## Asumsi yang perlu dicek dengan data nyata
- Pegawai tidak difilter `stts_aktif` (semua baris dihitung).
- Item apotek hanya `databarang.status='1'`.
- Batal = `reg_periksa.stts='Batal'`. Lama/Baru = `stts_daftar`.
- Kelompok umur memakai `umurdaftar` dan `sttsumur`; bucket kemungkinan beda dari grafik Khanza.
- Status Pulang Ranap memakai tanggal keluar dan mengabaikan `stts_pulang='-'`.
- Belum diuji ke database nyata, hanya cocok dengan `schema_sik.sql`.
