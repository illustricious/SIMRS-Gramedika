# Audit awal pelayanan inti — 24 November 2026

Audit statis terbatas pada jalur berikut, bukan audit seluruh tombol. Sumber pembanding adalah Java di `../simrs-khanza/src` dan kode kerja Avalonia. Status di bawah tidak menyatakan siap produksi. Uji transaksi memakai data sintetis; belum diuji pada salinan database RS.

| Alur | Bukti dan temuan | Pekerjaan berikutnya |
|---|---|---|
| Farmasi: penjualan | Java `inventory/DlgPenjualan.java:1338,4217,4239,4312` menulis penjualan/detail, mengubah stok sesuai pengaturan, serta menyiapkan jurnal. `FarmasiRepository.SimpanPenjualanAsync` sebelumnya menulis tabel pengeluaran dan detailnya, bukan tabel penjualan, tanpa alur stok/jurnal tersebut. Simpan penjualan kini ditolak dengan pesan yang mengarahkan pengguna ke Khanza. `GetPenjualanAsync` kini membaca `penjualan`/`detailjual`; fixture sintetis memeriksa nota dan total baris. | Implementasikan satu transaksi utuh termasuk stok per lokasi/batch, verifikasi kasir, PPN dan jurnal; cocokkan setiap cabang dan hak akses. Rekonsiliasi setiap data lama yang mungkin ditulis lewat versi sebelumnya sebelum digunakan sebagai dasar laporan. Total laporan baru menjumlahkan baris barang, belum termasuk ongkir/PPN nota. |
| Laboratorium | `LabPeriksaRepository.SaveAsync`/`DeleteAsync` menulis atau menghapus pemeriksaan dalam transaksi, tetapi guard billing `EnsureNotLockedAsync` dilakukan sebelum transaksi dan kode tersebut belum memposting jurnal. Java `DlgPeriksaLaboratorium.java:3094` menyiapkan jurnal. | Jangan menilai setara atau siap operasional: audit semua kategori, jurnal dan pembalik, stok/BHP, permintaan/hasil, izin, rollback serta perlombaan dengan billing. |
| Radiologi | `RadiologiPeriksaRepository.SaveAsync`/`DeleteAsync` menulis pemeriksaan, hasil, dan BHP; guard billing dilakukan sebelum transaksi dan kode tersebut belum memposting jurnal. Java `DlgPeriksaRadiologi.java:2294` menyiapkan jurnal. | Audit jurnal/pembalik, konsekuensi stok BHP, hak akses, permintaan/hasil dan penguncian lintas koneksi sebelum menyatakan aman. |
| Kasir | `KasirRepository.MarkPaidAsync` dan `BayarRanapAsync` mengunci `reg_periksa`; rawat inap membatasi komponen yang dapat dilunasi dan memposting jurnal. Tes dua koneksi fixture menunjukkan simpan tindakan menunggu kunci kunjungan lalu menolak setelah billing dikomit. | Tes arah sebaliknya juga lulus menggunakan `KasirRepository.MarkPaidAsync`; uji interaksi dengan klien Java yang belum terbukti mengikuti kunci yang sama. Audit rincian, deposit, pelunasan, rollback dan hak `kasir_ralan`/`billing_ralan`/`billing_ranap` per cabang. |
| IGD | `IgdView.OnSimpanClick` membuat registrasi lewat `RegistrasiRepository`; edit/hapus pada layar ini masih placeholder. `IgdRepository.SaveTriaseAsync` menyimpan triase; pemilihan poli menggunakan daftar kode tetap. | Cocokkan alur registrasi, triase, izin, kode poli yang dikonfigurasi RS, validasi dan perubahan data dengan `DlgIGD.java` dan dialog triase. |
| Pasien | `PasienRepository.NextNoRekamMedisAsync` menggunakan jumlah pasien + 1 dengan pola `D`; `DlgPasien.java:10030` memakai konfigurasi penomoran Khanza. Hitungan ini dapat bertabrakan ketika nomor tidak berurutan atau pengguna membuat pasien bersamaan. | Implementasikan aturan penomoran yang dikonfigurasi Khanza dan pengujian konkurensi, periksa duplikasi identitas, seluruh field dan hak simpan/ubah/hapus. |

Jalur-jalur lain, seluruh popup, pengujian hak akses per aksi, serta rekonsiliasi data dan jurnal belum diaudit. Jangan menggunakan daftar ini sebagai pengganti audit per kontrol di `status.json`.

## Pelunasan registrasi dan jual bebas (25 November 2026)

- **Kasir Rawat Jalan** dapat mencatat pelunasan biaya registrasi saja memakai satu akun pembayaran tanpa biaya admin. Pembanding `keuangan/DlgBilingRalan.java` memakai nomor `/RJ`, billing, detail akun, jurnal registrasi dan riwayat Sadewa. Semua perubahan dan status lunas dikomit bersama setelah konteks dan nilai yang dikonfirmasi diperiksa ulang. Bukti lunas tersedia sebagai PDF dari rincian yang tersimpan.
- **Jual Bebas** di Farmasi untuk pasien dengan nomor RM merekam nota `PJ`, detail, mutasi stok lokasi `F.A`, riwayat stok, jurnal kas/pendapatan dan HPP/persediaan, dan riwayat Sadewa. Cabang dibandingkan dengan `inventory/DlgPenjualan.java:1338,3964,4217,4312`. Lokasi memakai kode yang sudah dipakai katalog. Harga kelipatan Rp100 menghindari selisih konfigurasi pembulatan. Profil batch, verifikasi Kasir, PPN, biaya tambahan, racikan, diskon, dan penjualan dari resep masih memerlukan Khanza.
- **Pelunasan tindakan Ranap** tetap diblokir. `DlgRawatInap.java` memposting debit suspensi dan kredit pendapatan saat input, sementara `DlgBilingRanap.java:7566` juga mengkredit pendapatan saat pelunasan. Rekonsiliasi kedua posting pada konfigurasi RS diperlukan sebelum membuka cabang ini. Deposit, layanan lain, pelunasan campuran, perubahan stok batch dan penggunaan bersamaan dengan klien Java belum diuji.
- `PelunasanPenjualanTests` pada skema Khanza kosong di MariaDB memeriksa nota/detail, status, mutasi, jurnal, bukti tersimpan, izin dan rollback ketika pembayaran kurang atau rekening tidak dikonfigurasi. Uji ini memakai data sintetis dan membandingkan efek database dengan cabang sumber Java, tanpa menjalankan klien Java.

## Tagihan gabungan Rawat Jalan (26 November 2026)

- Kasir memakai tarif tindakan yang tersimpan, pemeriksaan Lab dan Radiologi yang tercatat, biaya item Lab berdasarkan kunci pemeriksaan, serta total pemberian obat termasuk embalase dan tuslah. Permintaan yang menunggu tetap terlihat sebagai penghalang pelunasan sampai dituntaskan atau dibatalkan. Penjualan tunai Farmasi tetap memiliki nota sendiri. PPN obat mengikuti flag legacy di set_nota.
- Pelunasan memuat ulang tagihan dengan kunci kunjungan. Perubahan rincian mengharuskan konfirmasi ulang. Pemeriksaan Lab/Radiologi dan input obat lewat Barcode memakai kunci yang sama. Snapshot billing, nota, jurnal dan status lunas dikomit bersama. Bukti PDF membaca snapshot billing.
- Pemetaan pendapatan merujuk DlgBilingRalan.java. Jika saldo suspen untuk kunjungan sudah terbentuk saat pelayanan, pelunasan mengkredit rekening suspen agar pendapatan tidak dicatat ulang. Selisih yang belum diposting masuk rekening pendapatan layanan. Saldo suspen tidak wajar atau alokasi ambigu menghentikan transaksi. Ini berbeda dari kode pelunasan Java yang mengkredit pendapatan langsung. Saldo dan pemetaan rekening RS perlu direkonsiliasi sebelum penggunaan operasional bersamaan dengan Java.
- Ruang lingkup pelunasan menggunakan satu akun bayar tanpa biaya admin. Deposit, piutang, retur obat kunjungan, operasi, tagihan obat langsung, tambahan dan potongan tetap diarahkan ke Kasir Khanza.

## Penguncian pelunasan dan cetak ulang kuitansi (26 November 2026)

- Kasir hanya mencatat pelunasan melalui transaksi yang menyimpan nota, rincian pembayaran, billing, jurnal dan status bayar. Jalur lama yang hanya menandai status lunas telah dihapus.
- Kuitansi PDF memakai nomor dan waktu dari nota, rincian billing, serta akun dan nominal dari detail pembayaran. Tombol cetak ulang membaca snapshot pelunasan tanpa menulis nota atau jurnal tambahan.
- Uji Avalonia headless mengirim dua klik dan memeriksa bahwa satu dialog konfirmasi terbuka. Uji MariaDB memakai dua koneksi kasir, lalu memaksa kegagalan insert detail jurnal setelah header disimpan untuk memverifikasi rollback. Dua kali baca kuitansi mengembalikan pembayaran yang sama dengan jumlah nota dan jurnal tetap.

## Validasi, pembatalan dan penyerahan resep (27 November 2026)

Layar **Farmasi > Daftar Resep Dokter** (`DaftarResepView`, `PilihObatWindow`, `ValidasiResepService`) kini dapat memproses resep, sebelumnya baca saja. Pembanding: `inventory/DlgDaftarPermintaanResep.java`, `DlgCariObat.java`, `DlgCariObat2.java`, `DlgPemberianObat.java` dan `webapps/penyerahanresep`.

- Hak `beri_obat` untuk validasi, pembatalan dan penyerahan; `resep_dokter` hanya melihat.
- Rawat jalan: depo `set_depo_ralan`, cadangan `set_lokasi`; harga kolom `ralan` atau `h_beli` ditambah `set_harga_obat_ralan`; rekening `set_akun_ralan`.
- Rawat inap: kamar aktif (atau terakhir) menentukan kelas dan bangsal; depo `set_depo_ranap`, lalu stok bangsal atau depo utama sesuai `set_lokasi.asal_stok`; harga kolom kelas atau `h_beli` ditambah `set_harga_obat_ranap`; rekening `set_akun_ranap`.
- Pembulatan per Rp100 mengikuti pengaturan koneksi (PEMBULATANHARGAOBAT). Mode batch memakai satu batch berkadaluarsa terdekat per obat. Java rawat inap pada cabang kenaikan harga mengurutkan kadaluarsa terjauh; di sini disamakan dengan rawat jalan.
- Racikan: `obat_racikan` dan `detail_obat_racikan`. Obat yang sama di dalam dan di luar racikan digabung menjadi satu baris `detail_pemberian_obat`, karena kunci utamanya tidak memuat racikan; jumlah, embalase, tuslah dan total dijumlahkan.
- Jumlah (0 sampai jumlah resep), embalase dan tuslah dapat diubah; awalnya dari `set_embalase`, untuk setiap baris termasuk bahan racikan.
- Tambah atau ganti obat: obat aktif berstok di depo, aturan pakai wajib untuk obat di luar racikan. Pengganti bahan racikan tetap dalam racikannya. Resep dokter tidak diubah.
- Validasi menulis seluruh perubahan dalam satu transaksi. Harga atau batch yang berubah sejak dimuat, stok kurang, billing terverifikasi, kunjungan batal, obat nonaktif yang tetap diberikan atau rekening kosong membatalkan seluruhnya.
- Batal validasi: seluruh resep. Stok, `data_batch`, riwayat (`Hapus`), catatan obat dan racikan dikembalikan, jurnal dibalik, resep menunggu lagi. Ditolak bila sudah diserahkan, billing, ada retur obat itu pada kunjungan, tidak ada catatan pada waktu validasi, ada resep lain pada waktu yang sama, atau lewat 48 jam bagi selain Admin Utama.
- Urutan kunci: kunjungan, resep, stok. Dua validasi atau dua pembatalan bersamaan hanya satu yang berhasil.
- Penyerahan mengisi waktu serah resep rawat jalan.
- `ValidasiResepTests` lulus pada MariaDB sintetis. Belum diuji: database RS atau salinannya, penggunaan bersamaan dengan klien Java, dan klik dialog konfirmasi di aplikasi.
- Belum ada: resep pulang di layar ini, hapus per obat, pilihan depo dan jenis harga, hitung kandungan/kapasitas racikan, ubah nama atau jumlah bungkus racikan, input parsial, telaah resep, etiket, antrean apotek, foto bukti, kirim obat PCare.

## Jurnal Laboratorium dan Radiologi (27 November 2026)

Bagian ini menggantikan temuan 24 November pada tabel di atas untuk jurnal dan letak cek billing: cek billing sudah berada di dalam transaksi dengan kunci kunjungan, dan jurnal kini diposting (`PenunjangJurnal`, dipakai `LabPeriksaRepository` PK/PA/MB dan `RadiologiPeriksaRepository`).

- Simpan: suspen/pendapatan, jasa dokter, petugas, KSO, sarana, perujuk, menejemen, serta HPP/persediaan BHP, mengikuti `DlgPeriksaLaboratorium*.java` dan `DlgPeriksaRadiologi.java`; rekening dari `set_akun_ralan`/`set_akun_ranap`. Radiologi menambahkan nilai `beri_bhp_radiologi` ke BHP. Rekening yang kosong membatalkan seluruh simpan.
- Item permintaan berstatus bayar `Sudah` tidak dijurnal. Item di luar permintaan tetap dijurnal; Java melewatkannya.
- Hapus: jurnal dibalik dalam transaksi yang sama. Jurnal tidak menyimpan kunci pemeriksaan, sehingga saldo suspen kunjungan dibandingkan dengan nilai seluruh pemeriksaan unit itu. Saldo nol (data versi aplikasi sebelumnya) dihapus tanpa pembalik; saldo kurang dari nilai pemeriksaan ditolak dan diarahkan ke Khanza. Java selalu membalik.
- Data yang pernah disimpan versi aplikasi sebelumnya tetap tanpa jurnal dan perlu direkonsiliasi bagian keuangan.
- `JurnalPenunjangTests` lulus pada MariaDB sintetis (batasan relasi dilepas pada skema uji ini). Belum diuji: database RS atau salinannya, bersamaan dengan klien Java, dan klik di aplikasi.
- Masih terbuka: audit hak akses per aksi, perubahan tarif/pemeriksaan tersimpan, gambar radiologi dan alur permintaan penuh.
