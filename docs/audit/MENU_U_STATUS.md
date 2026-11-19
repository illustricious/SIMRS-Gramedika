# Status Menu U

**19 November 2026 · 30 dari 30 tile memiliki layar Avalonia.** Empat layar sudah tersedia sebelumnya. Dua puluh enam layar tambahan terhubung melalui dashboard dan mengikuti hak akses pengguna Khanza.

| Tile | Fungsi Avalonia |
|---|---|
| Set Aplikasi | Profil koneksi database dan opsi lokal Avalonia. Identitas instansi, logo, dan wallpaper di aplikasi lama masih dikelola melalui Khanza Java. |
| Set Admin | Kelola akun Admin Utama. |
| Set P.J. Unit Penunjang | Pilih dokter penanggung jawab Lab PK, PA, MB, Radiologi, Hemodialisa, dan UTD. |
| Set Oto Lokasi | Simpan stok utama, sumber stok, dan pemetaan depo ralan dan ranap. |
| Set Kamar Inap | Simpan jam minimal, tagihan hari awal, fee perujuk, batas bayi, diagnosis akhir, dan opsi kamar legacy. |
| Set Harga Kamar | Tambah, ubah, dan hapus tarif kamar per cara bayar. |
| Set Embalase & Tuslah | Simpan nilai embalase dan tuslah per obat. |
| Set User | Kelola akun dan hak akses Khanza. |
| Set E-Eksekutif | Simpan atau hapus kredensial portal menggunakan skema legacy. Formulir hanya menampilkan status penyimpanan. |
| Tracker Login | Cari catatan login. |
| Vakum Table | Cari tabel dan lihat perkiraan jumlah baris. Pengosongan berjalan per tabel setelah Admin Utama mengetik nama database dan tabel. |
| Display Antrian Registrasi & Poli | Simpan teks, status aktif, dan gambar. Papan Poli native menampilkan pengumuman. |
| Display Antrian Apotek | Simpan teks, status aktif, dan gambar. Papan Apotek, Racikan, dan Nonracikan native menampilkan pengumuman. |
| Set Harga Obat | Atur sumber harga, PPN, dan margin umum, per jenis, atau per barang. Harga jual barang di master dapat dihitung ulang dengan konfirmasi. |
| Set Obat Ralan | Kelola kenaikan harga per cara bayar. |
| Set Obat Ranap | Kelola kenaikan harga per cara bayar dan kelas. |
| Set Penggunaan Tarif | Atur pembatasan pemilihan tarif berdasarkan poli, ruang, kelas, atau cara bayar. |
| Set Oto Ralan | Kelola aturan tindakan otomatis dokter, petugas, dan dokter–petugas untuk Khanza. |
| Biaya Harian | Kelola biaya dan jumlah per kamar. |
| Biaya Masuk Sekali | Kelola biaya satu kali per kamar. |
| Set RM | Simpan nomor terakhir, format penomoran, opsi alamat dan kelengkapan, validasi registrasi dan catatan, serta opsi data TNI/Polri. |
| Set Billing | Simpan konfigurasi nota, kuitansi, cetak, dan service ranap tunai atau piutang. |
| Closing Kasir | Kelola jam masuk dan pulang per shift. |
| Set Keterlambatan Presensi | Atur toleransi dan batas keterlambatan. |
| Set Input Parsial | Kelola cara bayar yang memakai input parsial. |
| Password BPJS | Pilih cara bayar untuk menyimpan, mengganti, atau menghapus kredensial. Formulir hanya menampilkan status penyimpanan. |
| Set Harga Toko | Simpan persentase distributor, grosir, dan retail. |
| Jam Diet Pasien | Kelola jadwal makan per slot. Slot tersedia di pemilih pada Diet Pasien. |
| Ruang Operasi | Kelola ruang yang tersedia pada Jadwal Operasi. Penghapusan ruang yang masih dirujuk oleh jadwal ditolak. |
| Set Resep Per Cara Bayar | Kelola pasangan cara bayar dan obat yang tersimpan di tabel legacy. |

Pengumuman pada papan native ditampilkan sebagai teks dan gambar. Pengaturan tarif dan harga yang sudah dibaca modul Avalonia tetap menggunakan tabel Khanza yang sama. Integrasi lanjutan masih diperlukan untuk aturan otomatis registrasi, penomoran RM, beberapa aturan tarif dan biaya kamar, pembatasan resep, serta opsi billing yang belum dipakai oleh modul terkait di Avalonia.

Verifikasi tahap ini mencakup rute dashboard, sintaks sumber, dan pencocokan terhadap skema di repo. Build dan pengujian database belum dijalankan untuk perubahan Menu U. Pengosongan tabel dan penulisan kredensial belum dijalankan.
