# Status per menu

Hitungan per 22 November 2026 (rilis 0.2.5-alpha): **1.041 dari 1.224 tile** punya layar. Tile yang muncul di beberapa menu dihitung di tiap menu. Daftar tile belum berlayar ada di [TILE_BACKLOG.md](../audit/TILE_BACKLOG.md).

Legenda: **Edit** = bisa tambah/ubah. **Baca** = daftar hanya-baca (transaksi dicatat di Khanza). **Khusus** = layar dengan alur sendiri.

## Selesai berlayar

| Menu | Area | Tile | Bentuk utama | Rincian |
|---|---|---:|---|---|
| A | Registrasi, tagihan, pelayanan | 34 | Khusus + Edit | [MENU_A_STATUS](../audit/MENU_A_STATUS.md). Rawat Inap dan Kasir punya tulis nyata dengan jurnal, pelunasan terbatas pada kamar dan registrasi, satu akun bayar, tanpa biaya admin |
| B | Barcode, lab kesling | 16 | Khusus | Tindakan/obat via barcode, jurnal lewat `JurnalPoster` |
| C | Presensi, pegawai, K3, SKP | 61 | Edit + Baca + Khusus | Penggajian, penilaian SKP, rekap |
| D | Inventori farmasi, stok pasien | 75 | Edit + Baca + Khusus | Transaksi stok (mutasi, opname, stok keluar, permintaan), penerimaan, review resep |
| E | Barang non medis, penunjang | 33 | Edit + Baca | Master Jenis/Barang/Suplier, laporan, sirkulasi 2 dari opname pertama |
| F | Dapur kering dan basah | 27 | Edit + Baca | Master Barang Dapur (Basah/Kering), laporan lengkap |
| G | Aset, inventaris, kesling | 28 | Edit + Baca | CSSD, pemeliharaan gedung, penghapusan master Jenis/Ruang dinonaktifkan |
| H | Parkir | 3 | Khusus | Jenis parkir, barcode, parkir masuk. Parkir keluar, penagihan, cetak tiket belum |
| I | Olah data tagihan | 60 | Baca | [MENU_BELUM_DAN_PELAJARAN_I](../audit/MENU_BELUM_DAN_PELAJARAN_I.md). Laporan jasa harian/bulanan hanya ralan dan ranap, tanpa lab, radiologi, operasi |
| J | Penyakit, DKK, RL, internal | 96 | Edit + Baca | [MENU_J_STATUS](../audit/MENU_J_STATUS.md). Master ICD, laporan kunjungan dan diagnosis |
| K | Tarif, keuangan, akuntansi | 87 | Edit + Baca + Khusus | [MENU_K_STATUS](../audit/MENU_K_STATUS.md). Tarif (total otomatis), master akun, hutang/piutang, bank, jurnal, Posting Jurnal manual |
| M | Rekam medis, triase, gizi, template | 255 | Edit + Baca | [MENU_M_STATUS](../audit/MENU_M_STATUS.md). Master referensi, template, dan katalog masalah/rencana keperawatan bisa diedit. Catatan klinis, skrining, penilaian, SOAP, dan resume hanya-baca; input tetap di alur pelayanan Khanza |
| N | Unit transfusi darah (UTD) | 11 | Edit + Baca | [MENU_NR_STATUS](../audit/MENU_NR_STATUS.md). Komponen darah dan pendonor bisa diedit. Donor, stok, penyerahan, dan BHP hanya-baca |
| O | Analisa, dashboard, infografik | 113 | Baca | [MENU_O_STATUS](../audit/MENU_O_STATUS.md). Tabel agregat hanya-baca, grafik belum digambar |
| P | Surat, persetujuan, pengumuman e-pasien | 45 | Edit + Baca | [MENU_P_STATUS](../audit/MENU_P_STATUS.md). Register surat masuk/keluar dan referensi bisa diedit. 31 surat pasien dan persetujuan hanya-baca |
| Q | Perpustakaan | 16 | Edit + Baca | [MENU_Q_STATUS](../audit/MENU_Q_STATUS.md). Katalog, inventaris, anggota, pengaturan bisa diedit. Peminjaman dan denda hanya-baca |
| R | PCRA/ICRA | 10 | Edit + Baca | [MENU_NR_STATUS](../audit/MENU_NR_STATUS.md). 9 referensi bisa diedit, pengkajian proyek hanya-baca |
| S | Toko, minimarket, koperasi | 25 | Edit + Baca | [MENU_S_STATUS](../audit/MENU_S_STATUS.md). Transaksi toko dicatat di Khanza |
| T | Filantropi, CSR, zakat | 16 | Edit | 16 daftar referensi Dankes. Kepemilikan Rumah butuh `docs/audit/menu_t_kepemilikan_rumah.sql` |
| U | Pengaturan program | 30 (+1 backup) | Edit + Khusus | [MENU_U_STATUS](../audit/MENU_U_STATUS.md). Tile Backup Database ditambah Avalonia untuk semua akun login |

Kesetaraan fungsi alur klinis diaudit terpisah di [tracker](../tracker/README.md): **Registrasi** (registrasi baru, hapus, ubah dengan penjagaan, IGD dikecualikan; lihat `AUDIT_REGISTRASI.md`) dan **Rawat Jalan** (NIP nyata, banyak SOAP, antrian Ralan; lihat `AUDIT_RAWAT_JALAN.md`) sudah diperbaiki sebagian. **Farmasi (penjualan)** kini memakai petugas dari akun login. Alur lain belum diaudit.

Fitur klinis di luar katalog menu (toolbar): Registrasi, Rawat Jalan (pemeriksaan), Pasien, Rawat Inap, Kasir, IGD, Laboratorium PK/PA/MB, Radiologi, Farmasi (penjualan), Laporan, papan Antrian (Anjungan, Poli, Apotek), Informasi, Tentang Program. Klik kanan baris pasien membuka menu konteks (Data Rekam Medis, Riwayat Perawatan, Kamar Inap, Tindakan, Billing, dst, sesuai hak akses).

## Belum berlayar

| Menu | Area | Sudah/Total | Catatan pengerjaan |
|---|---|---:|---|
| L | Bridging VClaim, Aplicare, PCare, INACBG, Kemenkes, pihak ke-3 | 0/183 | Butuh kredensial dan API eksternal. Lihat [BRIDGING_RECON](../audit/BRIDGING_RECON.md). Kerjakan terakhir |

## Keterbatasan lintas menu

- Layar transaksi uang hanya-baca. Transaksi, persetujuan, penyesuaian stok, dan cetak laporan legacy dilakukan di Khanza.
- Cetak PDF nyata baru untuk daftar Registrasi dan kuitansi pembayaran. Cetak laporan legacy lain belum ada.
- Pemetaan tile ke hak akses mengikuti Khanza. Tile tanpa hak akses tidak tampil.
- Penyimpanan password: tabel legacy `user`/`admin` menyimpan plaintext di versi lama. Rencana "rehash saat login pertama" belum diselesaikan (lihat [04-HISTORY.md](04-HISTORY.md), bagian `LegacyUserRepository`).
