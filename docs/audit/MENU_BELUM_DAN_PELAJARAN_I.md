# Inventaris tile yang belum tersedia dan awal studi Menu I

Tanggal: 21 November 2026. Ini audit statis kode dan katalog, bukan hasil uji interaktif atau bukti paritas fungsi.

**Pembaruan implementasi:** Menu I sekarang memiliki 60 dari 60 tile yang membuka layar. Enam layar terbaru mencakup detail dan rekap jasa dokter dari tindakan, rekap penerimaan Payment Point per shift, serta rekap penerimaan per rekening dan per shift closing. Sumber jasa penunjang, pengeluaran, piutang dan rekonsiliasi belum dicakup pada laporan baru tersebut. Rekap Poli Anak menampilkan kunjungan dan biaya, dengan rincian klinis menyusul. Pembayaran Per Akun Bayar 1 memakai shift. Versi 2 dan 3 berbagi tampilan rekening, sedangkan versi 4 dan 5 berbagi tampilan akun bayar per sumber. Format cetak dan perbedaan tata letak tiap versi Khanza belum tersedia. Pembayaran Per Akun Bayar 2 menyediakan rincian per rekening dari enam sumber penerimaan; filter jam/petugas dan format cetak legacy belum tersedia. Jasa Tindakan Pasien dan Ringkasan Jasa Tindakan Pasien baru mencakup tindakan ralan dan ranap berdasarkan tanggal registrasi; operasi, lab, radiologi dan filter status/jam belum tercakup. Detail Tindakan menampilkan tindakan ralan dan ranap, tetapi operasi, lab, radiologi dan filter status/jam belum tersedia. Detail VK & OK menampilkan komponen jasa dan peserta operasi/VK; informasi lokasi dan format cetak legacy belum tersedia. Tiga layar sebelumnya adalah Harian Kamar, Harian Dokter Poli dan Harian Dokter Ralan. Harian Kamar belum memuat biaya operasi dan nilai kunjungan dapat berulang jika muncul pada beberapa bangsal. Kedua laporan dokter menampilkan komponen tagihan berdasarkan dokter registrasi; tindakan yang dilakukan dokter lain tidak termasuk, sehingga angka dapat berbeda dari laporan legacy. Detail pasien, filter khusus dan cetak legacy juga belum tersedia. Payment Point dan Payment Point 2 menampilkan penerimaan per shift atau periode, dengan pencarian, total dan CSV; pengaturan modal awal, pilihan jam dan cetak legacy belum tersedia. Tile sebelumnya adalah Ringkasan Tindakan, yang baru merangkum tindakan rawat jalan berdasarkan tanggal tindakan; operasi, rawat inap, laboratorium, radiologi dan filter status/jam belum dicakup. Tujuh tile sebelumnya adalah laporan harian dan bulanan dokter dan paramedis, fee visit dokter dan bacaan EKG, serta obat per kamar. Empat laporan dokter/paramedis baru menghitung tindakan ralan dan ranap; operasi, laboratorium, radiologi, filter status bayar dan pilihan jam belum dicakup. Fee visit dan obat per kamar memperlihatkan risiko pengulangan data jika satu kunjungan memiliki beberapa riwayat kamar. Tile sebelumnya mencakup rekap pembayaran ralan dan ranap per hari, pembayaran per unit dan shift, rekap pembayaran per unit, tagihan masuk, serta piutang per akun. Tagihan masuk menampilkan baris subtotal, sehingga tidak dijumlahkan otomatis. Pembayaran per unit memakai jam shift closing kasir dan tanggal registrasi. Laporan mendukung periode, pencarian dan ekspor CSV. Laporan jasa sarana/KSO/manajemen/BHP juga baru mencakup tindakan ralan/ranap. Laporan pembayaran dan piutang bersifat baca saja; cetak seperti legacy, detail dan transaksi cicilan belum tersedia. Query belum diuji pada database. Hitungan pada tabel di bawah adalah kondisi awal sebelum perubahan.

## Cakupan awal

README proyek mencatat 407 dari 1.224 tile membuka layar; angka dihitung per kemunculan pada kategori, termasuk tile bersama. Artinya 817 kemunculan belum membuka layar. Layar yang terbuka pun mungkin masih terbatas; lihat status masing-masing menu sebelum menganggapnya setara Khanza.

| Menu | Area | Jumlah tile | Kondisi menurut README dan pemeriksaan awal |
|---|---|---:|---|
| I | Tagihan rawat inap dan rawat jalan | 60 | 1 tersedia, 59 belum; Deposit Pasien memakai layar Menu A |
| K | Tarif, keuangan dan akuntansi | 87 | Bagian dari 4 tile tersedia bersama Menu I, M, S; rincian perlu audit |
| L | Bridging dan pihak ketiga | 183 | Belum tercatat membuka layar |
| M | Rekam medis | 255 | Bagian dari 4 tile tersedia bersama Menu I, K, S; rincian perlu audit |
| N | Transfusi darah | 11 | Belum tercatat membuka layar |
| O | Analisis, dashboard dan grafik | 113 | Belum tercatat membuka layar |
| P | Surat dan pengumuman | 45 | Belum tercatat membuka layar |
| Q | Perpustakaan | 16 | Belum tercatat membuka layar |
| R | Pre-Construction dan Infection Control Risk Assessment | 10 | Belum tercatat membuka layar |
| S | Toko / minimarket / koperasi | 25 | Bagian dari 4 tile tersedia bersama Menu I, K, M; rincian perlu audit |
| T | Filantropi, CSR, zakat, infak dan sedekah | 16 | Belum tercatat membuka layar |

Jumlah pada tabel berasal dari `LegacyMenuCatalog.cs`; status global berasal dari README. Menu A–H, J dan U telah memiliki layar untuk seluruh tile menurut README, tetapi masih memiliki kesenjangan proses. Status README perlu diperbarui bila routing berubah.

## Pemetaan Menu I

Fee Rujukan Ranap menggunakan nominal jasa perujuk yang tercatat pada rujukan masuk untuk kunjungan berstatus Ranap, berdasarkan tanggal registrasi. Handler Java kosong sehingga laporan ini menggunakan data yang tersedia langsung.

 Katalog memuat laporan jasa dan tindakan (dokter, paramedis, sarana, KSO, manajemen, BHP dan fee); obat berdasarkan poli, kamar, dokter dan cara bayar; pembayaran, piutang dan rekap; serta tambahan/potongan biaya, deposit dan payment point. Sebelum implementasi di atas, pemeriksaan nama kunci Menu I di kode UI dan Data hanya menemukan `btnDeposit` di `MenuASpecs.cs` di luar katalog. Ini bukan penilaian bahwa semua data atau logika terkait sama sekali tidak ada di aplikasi.

## Studi awal: Tambahan Biaya Pasien dan Potongan Biaya Pasien

- `frmUtama.java` membuka `keuangan/DlgDetailTambahan.java` dan `keuangan/DlgDetailPotongan.java`. Kedua tile adalah **laporan**, bukan form entri tambahan atau potongan.
- Pengguna memilih rentang tanggal. Kedua laporan memfilter `reg_periksa.tgl_registrasi` secara inklusif, kemudian menampilkan pengelompokan tanggal dan kunjungan, nomor rawat, nama pasien, nama komponen dan nilai. Total seluruh nilai ditampilkan jika lebih dari nol. Tanggal ini adalah tanggal registrasi, bukan tanggal pembebanan atau pembayaran.
- Data tambahan berasal dari `tambahan_biaya(no_rawat, nama_biaya, besar_biaya)`; potongan dari `pengurangan_biaya(no_rawat, nama_pengurangan, besar_pengurangan)`. Keduanya terhubung ke `reg_periksa`, lalu ke `pasien`; kunci masing-masing adalah kombinasi nomor rawat dan nama komponen. Skema mengizinkan nilai potongan NULL.
- Cetak legacy menggunakan `rptRTambahan.jasper` dan `rptRPengurangan.jasper` melalui tabel sementara. Kedua sumber JRXML berukuran A4 potret dan memuat identitas serta logo RS, nomor, tanggal, pasien, nama komponen, nominal, nomor halaman dan waktu cetak. Nama pasien pada baris data digabung dengan nomor rawat. Jangan menganggap ekspor CSV setara cetak asli.
- Hak akses tile masing-masing adalah `tambahan_biaya` dan `potongan_biaya`; pemetaan tersebut juga sudah ada di `LegacyTilePermissions.g.cs`.
- Repository kasir saat ini mendeteksi keberadaan kedua jenis komponen sebagai kondisi yang membatasi pelunasan di Avalonia. Implementasi laporan baca saja tidak boleh dianggap telah menambahkan perhitungan atau pelunasan kedua komponen.

Rancangan awal: laporan baca saja berparameter periode dengan satu query berparameter yang menggabungkan kunjungan, pasien, dan komponen; pengelompokan tanggal/kunjungan serta total mengikuti tampilan legacy. Uji data sintetis termasuk beberapa komponen per kunjungan, nilai nol/NULL, batas tanggal dan total. Nilai NULL dibaca Java JDBC dengan `getDouble` tanpa pemeriksaan `wasNull`, sehingga diperlakukan sebagai nol dalam laporan legacy. Urutan data legacy tidak dinyatakan dengan `ORDER BY`; urutan yang konsisten perlu ditentukan saat implementasi.

## Studi awal: pembayaran dan piutang

### Pembayaran Ralan (`btnRalanMasuk`, izin `pembayaran_ralan`)

- `DlgPembayaranRalan` menampilkan daftar berdasarkan `reg_periksa.tgl_registrasi` dan `status_lanjut='Ralan'`, dengan filter dokter, poli, cara bayar dan kata kunci nomor rawat, nomor RM atau nama. Kunjungan dengan baris di `piutang_pasien` dikecualikan.
- Nilai komponen dibaca dari `billing.totalbiaya` menurut status, termasuk registrasi, tindakan, obat, operasi, lab, radiologi, tambahan dan potongan. Ini merupakan data billing yang sudah tercatat, bukan perhitungan ulang semua biaya sumber.
- Status tampilan `Sudah Bayar` diturunkan dari jumlah komponen billing yang lebih dari nol; bukan sekadar pembacaan `reg_periksa.status_bayar`. Daftar dapat difilter sudah/belum/semua, dan menyediakan akses ke layar billing serta cetak `rptRTagihanRalan`.
- Total legacy dihitung saat membaca semua hasil sebelum penyaringan status di tampilan. Ini berpotensi membuat total berbeda dari jumlah baris yang terlihat; perilaku dan ekspektasi perlu diuji sebelum mereplikasi.

### Pembayaran Ranap (`btnRanapMasuk`, izin `pembayaran_ranap`)

- `DlgPembayaranRanap` memilih berdasarkan `kamar_inap.tgl_keluar`, bukan tanggal registrasi, serta mengecualikan baris `Pindah Kamar` dan kunjungan yang memiliki piutang. Baris dengan status pulang `-` juga tidak ditampilkan. Filter yang tersedia adalah bangsal dan cara bayar.
- Daftar mengelompokkan nilai billing per status, termasuk kamar, tindakan ranap dan ralan, obat, retur/resep pulang, lab, radiologi, operasi, tambahan dan potongan. Ada akses billing dan cetak `rptRTagihanRanap`. Penyamaan dengan laporan ralan tanpa memeriksa daftar status dan rumusnya akan menghasilkan angka yang keliru.

### Piutang Pasien (`btnLihatPiutang`, izin `piutang_pasien`)

- `DlgLhtPiutang` memfilter `piutang_pasien.tgl_piutang`, status lunas/belum/semua, cara bayar serta nomor rawat, nomor RM atau nama. Data menggabungkan `piutang_pasien`, `reg_periksa`, `pasien`, `penjab` dan agregat `bayar_piutang`.
- Untuk tiap kunjungan sisa yang ditampilkan adalah `piutang_pasien.sisapiutang - SUM(besar_cicilan) - SUM(diskon_piutang) - SUM(tidak_terbayar)`; masing-masing agregat cicilan memakai nol bila kosong. Jangan mengurangi uang muka sekali lagi karena field dasarnya sudah `sisapiutang`. Status filter berasal dari `piutang_pasien.status`, bukan dihitung ulang dari sisa.
- Daftar menyediakan rincian billing dan rincian cicilan. Menu rincian cicilan diaktifkan oleh izin tambahan `bayar_piutang` dan membuka `DlgBayarPiutang`; ini lebih luas daripada laporan baca saja dan harus diaudit tersendiri sebelum port transaksi. Laporan cetak menggunakan `rptRPiutangMasuk`.

Langkah berikutnya: rancang dan uji laporan tambahan/potongan berdasarkan temuan di atas; lanjutkan pemeriksaan rumus setiap status dan alur jurnal sebelum implementasi pembayaran atau cicilan. Semua temuan di sini berasal dari pembacaan kode, belum diverifikasi dengan aplikasi Java berjalan atau data sintetis.

Sumber: `src/SimrsGramedika.UI/Modules/Shared/LegacyMenuCatalog.cs`, `MainWindow.axaml.cs`, `src/SimrsGramedika.Core/Auth/LegacyTilePermissions.g.cs`, `src/SimrsGramedika.Data/MenuA/MenuASpecs.cs`, `src/SimrsGramedika.Data/KasirRepository.cs`, `docs/audit/schema_sik.sql`; repo legacy `src/simrskhanza/frmUtama.java`, `src/keuangan/DlgDetailTambahan.java`, `DlgDetailPotongan.java`, `DlgPembayaranRalan.java`, `DlgPembayaranRanap.java`, `DlgLhtPiutang.java`, serta `report/rptRTambahan.jrxml` dan `rptRPengurangan.jrxml`.
