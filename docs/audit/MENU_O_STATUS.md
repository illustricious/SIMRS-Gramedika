# Menu O (Analisa & Info Grafik)

113 dari 113 tile berlayar. Kode: `MenuOSpecs.cs` dan `MenuOMoreSpecs.cs` (bagian 2), tes: `MenuOSpecsTests.cs`.

Bentuk: tabel agregat hanya-baca (kategori, jumlah, total) lewat `ReportView`. Grafik batang/pai belum digambar. Tile Per Tahun memakai semua data, Per Bulan memilih tahun, Per Tanggal memilih rentang tanggal.

## Sudah
- Registrasi (24): Per Poli, Dokter, Pekerjaan, Pendidikan, Agama, Umur, Suku, Bahasa, Cacat Fisik, Cara Bayar, Demografi (kabupaten/kecamatan), Perujuk, Per Tahun/Bulan/Tanggal, Lama dan Baru x3, Batal x3.
- Rawat inap (6): Per Tahun/Bulan/Tanggal/Ruang, Status Pulang, Pasien Meninggal Per Bulan.
- Lab dan Rad ralan Per Tahun/Bulan/Tanggal (6, hitung no_rawat berbeda).
- Pegawai (9), Item Apotek (4), Inventaris (5).

## Bagian 2 (59)
IKP x5, TB x11, air PDAM/tanah x4, limbah B3/domestik/cair x6, K3 x10, pengajuan aset x3, HAIs x8, penerimaan obat, hemodialisa x3, diet x4, perbaikan inventaris x4.

## Asumsi yang perlu dicek dengan data nyata
- Pegawai tidak difilter `stts_aktif` (semua baris dihitung).
- Item apotek hanya `databarang.status='1'`.
- Batal = `reg_periksa.stts='Batal'`. Lama/Baru = `stts_daftar`.
- Kelompok umur memakai `umurdaftar` dan `sttsumur`; bucket kemungkinan beda dari grafik Khanza.
- Status Pulang Ranap memakai tanggal keluar dan mengabaikan `stts_pulang='-'`.
- Belum diuji ke database nyata, hanya cocok dengan `schema_sik.sql`.
- Laju HAIs = kasus / hari alat x 1000. Pasangan: VAP-ETT, IAD-CVL, PLEB-IVL, ISK-UC, HAP-ETT. ILO tanpa alat (hanya kasus). Pasangan ini tebakan dari skema, cocokkan dengan rumus Khanza.
- Tile TB memakai semua baris `data_tb` tanpa filter tanggal.
- Hemodialisa/diet dihitung per baris, bukan per pasien.
