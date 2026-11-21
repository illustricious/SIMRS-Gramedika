# Status Menu A

**19 November 2026 · 34 dari 34 tile membuka layar Avalonia.** Tabel ini merangkum fungsi yang tersedia pada checkout saat ini.

| Tile | Fungsi Avalonia | Selisih utama dengan Khanza Java |
|---|---|---|
| Informasi Kamar | Daftar kamar dan pencarian | Tampilan informasi eksternal mengikuti cakupan modul anjungan. |
| Jadwal Praktek | Daftar, pencarian, dan tambah | Ubah, hapus, dan cetakan khusus. |
| Registrasi | Daftar, registrasi baru, perubahan sebagian data, dan cetak daftar | Sebagian aksi dan tab legacy. |
| Booking Periksa | Daftar dan pencarian | Pengelolaan data booking. |
| Booking Registrasi | Daftar dan pencarian | Pengelolaan data booking. |
| Booking MCU Perusahaan | Daftar dan pencarian | Pengelolaan data booking. |
| IGD/UGD | Daftar, registrasi IGD, dan triase | Perubahan/penghapusan di layar utama dan sebagian aksi triase. |
| Tindakan Ralan | Daftar tindakan dan pemeriksaan rawat jalan | Sebagian aksi tindakan dan submenu pasien. |
| Permintaan Rawat Inap | Cari, tambah, ubah, batal, pesan kamar, proses masuk rawat inap, dan simpan PDF | Template surat dan persetujuan khusus legacy. |
| Rawat Inap | Daftar, masuk, pindah kamar, pulang, cetak, dan buka tagihan pasien di Kasir. Tarif mengikuti cara bayar pada saat masuk atau pindah dan disimpan per periode kamar. | Sebagian aksi tambahan di layar kamar inap legacy. |
| Jadwal Operasi | Cari, tambah, ubah status/jadwal, hapus, cek bentrok, dan simpan PDF | Tampilan agenda dan format cetak khusus legacy. |
| Permintaan Lab PK | Daftar, filter, tambah, perubahan waktu sampel/hasil, dan cetak | Paritas aturan dan format khusus legacy belum diuji menyeluruh. |
| Permintaan Lab PA | Daftar, filter, tambah, perubahan waktu sampel/hasil, dan cetak | Paritas aturan dan format khusus legacy belum diuji menyeluruh. |
| Permintaan Lab MB | Daftar, filter, tambah, perubahan waktu sampel/hasil, dan cetak | Paritas aturan dan format khusus legacy belum diuji menyeluruh. |
| Permintaan Radiologi | Daftar, filter, tambah, perubahan waktu, dan cetak | Paritas aturan dan format khusus legacy belum diuji menyeluruh. |
| Permintaan Layanan Program KFR | Daftar permintaan, pencatatan dan riwayat sesi, selesaikan/buka kembali program, dan simpan PDF | Foto bukti, e-signature, dan tautan submodul klinis legacy. |
| DPJP Ranap | Daftar, pencarian, tambah, ubah, dan hapus | Cetakan khusus DPJP. |
| Tindakan Ranap | Daftar dan filter tindakan | Input dan pengelolaan transaksi tindakan. |
| Operasi/VK | Daftar dan pencarian tindakan operasi | Input operasi, tagihan, dan pengelolaan tim. |
| Rujukan Keluar | Daftar dan pencarian | Input, perubahan, dan cetak surat rujukan. |
| Rujukan Masuk | Daftar dan pencarian | Input dan perubahan rujukan. |
| Beri Obat/BHP | Daftar dan pencarian | Input dan pengelolaan transaksi obat/BHP pada tile ini. |
| Resep Pulang | Daftar, input obat, pemilihan stok dan harga depo, proses permintaan dokter, pembalikan stok saat hapus, dan simpan PDF | Format etiket dan cetakan resep khusus legacy. |
| No. Resep | Daftar dan buat nomor resep | Racikan, hapus, dan cetak belum lengkap. |
| Diet Pasien | Cari, catat pemberian per kamar dan waktu makan, hapus, dan simpan PDF | Format rekap dan alur sisa diet legacy. |
| Skrining Rawat Jalan | Cari pasien, catat dan hapus skrining, simpan PDF | Navigasi lanjutan ke modul klinis lain dari menu konteks Java. |
| Deteksi Dini Corona | Cari kunjungan, catat, ubah, hapus skrining, dan simpan PDF | Format cetak khusus legacy. |
| Periksa Lab PK | Daftar, entri hasil, dan cetak | Paritas aturan dan format khusus legacy belum diuji menyeluruh. |
| Periksa Lab PA | Daftar, entri hasil, dan cetak | Paritas aturan dan format khusus legacy belum diuji menyeluruh. |
| Periksa Lab MB | Daftar, entri hasil, dan cetak | Paritas aturan dan format khusus legacy belum diuji menyeluruh. |
| Periksa Radiologi | Daftar, entri hasil, serta template hasil | Paritas aturan dan format khusus legacy belum diuji menyeluruh. |
| Rawat Jalan (kasir) | Cari tagihan per No. Rawat, lihat rincian tindakan dan pemberian obat/BHP, serta tandai lunas tagihan rawat jalan sederhana. Dengan hak Billing Ranap, layar juga menampilkan rincian kamar dan mencatat pelunasan sederhana rawat inap. | Pelunasan tagihan dengan tindakan atau pemberian obat/BHP dilakukan di Kasir Khanza. |
| Deposit Pasien | Cari, catat, hitung PPN/admin, posting jurnal dan Sadewa, batalkan dengan jurnal balik, dan simpan bukti PDF | Format kuitansi khusus rumah sakit. |
| Perkiraan Biaya Ranap | Cari pasien, simpan/hapus estimasi manual, pilih tarif terendah/tertinggi dari riwayat RS atau INACBG, dan simpan PDF | Rincian pembanding kasus serta format cetak legacy. |

Kasir rawat inap menghitung tarif kamar per periode, biaya harian sebagai tarif × jumlah × lama rawat, serta biaya masuk sekali untuk setiap periode kamar. Biaya registrasi rawat inap mengikuti opsi administrasi di Set Billing. Pelunasan setelah pasien pulang memakai satu akun yang memiliki rekening dan tanpa biaya admin. Aplikasi menyimpan rincian, nota, jurnal, dan status bayar bersama dalam satu transaksi. Tagihan dengan komponen lain seperti deposit, piutang, obat, atau tindakan diselesaikan di Kasir Khanza.

Kasir menyediakan **Simpan Bukti Pelunasan PDF** untuk rawat inap yang berstatus lunas, memiliki nomor nota, dan mempunyai rincian tersimpan. PDF mengambil rincian dari tagihan yang sudah dibukukan.

Untuk kunjungan yang belum lunas dan belum dibukukan, subtotal tindakan dan obat/BHP ditampilkan tersendiri. Tindakan dikelompokkan menurut pelaksana, jenis, dan tarif. Tagihan rawat inap mencakup tindakan rawat jalan pada No. Rawat yang sama dan mengikuti tarif master Khanza untuk tindakan khusus paramedis. Total obat/BHP mengambil nilai pemberian obat, termasuk embalase dan tuslah yang tercatat. Penghitungan obat langsung, resep pulang, retur, lab, dan radiologi tetap dilakukan di Kasir Khanza.

Pengujian tampilan tanpa database mencakup sembilan layar tambahan dan layar sesi KFR. Uji baca terhadap skema Khanza bisa dijalankan dengan `SIMRS_DB_SMOKE=1`.

Uji transaksi pada database sintetis `simrs_ranap_fixture` mencakup penyimpanan tarif per cara bayar, masuk, pindah, pulang, dan pelunasan. Rincian uji berjumlah **Rp1.035.000**, sesuai hitungan SQL dari aturan Khanza Java. Uji juga memeriksa jurnal seimbang, pembayaran ganda, serta pembatalan transaksi jika rekening jurnal hilang. Untuk mengulang pengujian, siapkan database kosong dengan tabel Khanza, lalu isi `SIMRS_RANAP_FIXTURE_PORT` dan `SIMRS_RANAP_FIXTURE_PASSWORD` untuk server MariaDB lokal. Tes memasukkan data sintetis dan mensyaratkan tabel registrasi kosong.
