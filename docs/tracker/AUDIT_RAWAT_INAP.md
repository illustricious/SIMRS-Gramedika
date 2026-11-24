# Audit Rawat Inap

23 November 2026, diperbarui dengan catatan lanjutan 24 November. Pemeriksaan statis atas `DlgRawatInap.java`, jalur masuk dari `DlgKamarInap.java`, dan implementasi Avalonia. Bukti per kontrol ada di [fitur.tsv](fitur.tsv).

| Sumber Java | Dinilai | Kurang | Sebagian | Selesai | Belum diaudit |
|---|---:|---:|---:|---:|---:|
| `DlgRawatInap.java` | 164 | 150 | 14 | 0 | 0 |
| `DlgKamarInap.java` | 40 | 9 | 25 | 6 | 232 |

Audit kamar mencakup jendela, seluruh 32 aksi yang diinventarisasi, dan tujuh popup utama. Sisa popup kamar meliputi formulir, laporan, surat, dan cabang lain. Kesetaraannya belum diverifikasi.

## Pemetaan alur

| Alur | Bukti Java | Implementasi Avalonia |
|---|---|---|
| Daftar, masuk, pulang, dan pindah kamar | `DlgKamarInap.java:5552`, `:5578`, `:5853`, `:7163`, `:7298`, `:20074` | `RawatInapView` dan `RawatInapRepository` memuat daftar, memasukkan pasien, menutup segmen, dan membuka segmen baru saat pindah. Waktu keluar atau pindah memakai waktu simpan. Cabang tindak lanjut rujuk dan meninggal, penggabungan ranap, serta semua variasi perhitungan dan filter Java belum setara atau belum diverifikasi. |
| Jenis bayar dan diagnosis sementara | `DlgKamarInap.java:7583`, `:11382`, `:11398` | Satu panel Ganti. Jenis bayar berlaku untuk kunjungan. Diagnosis sementara berlaku untuk segmen kamar yang dipilih. Java menyimpan kedua jenis diagnosis berdasarkan `no_rawat`. |
| Buka layanan klinis dari kamar | `DlgKamarInap.java:6310` membuka `DlgRawatInap` dengan konteks pasien, kamar, dan penjamin. Handler juga memeriksa verifikasi billing dan menangani kunjungan gabungan. | Tombol **SOAPIE** membuka pemeriksaan untuk nomor rawat yang dipilih. Rute menuju tindakan dan konteks pasien gabungan masih tertunda. |
| Tindakan dokter, petugas, gabungan | `DlgRawatInap.java:11661` menangani simpan, termasuk pemeriksaan billing. Handler hapus mulai di `:4412` dan ubah di `:5317`. | `RanapMenuRepository.TindakanAsync` memuat daftar baca-saja pada layar terpisah. Input, ubah, hapus, dan pemilihan pelaksana dari kunjungan belum tersedia. |
| SOAPIE | `DlgRawatInap.java:11939` menyimpan ke `pemeriksaan_ranap` dengan NIK pegawai. Akun biasa hanya dapat menyimpan atas NIK sendiri. `:10444` memuat riwayat, `:5399` menangani ubah, `:4816` menangani hapus, `:7295` membuka lima catatan terakhir, dan `:8886` menangani salin SOAPIE. | Riwayat dan simpan/ubah/hapus satu catatan tersedia di `PemeriksaanRanapWindow`. Lima SOAPIE terakhir dan salin narasi tersedia untuk draf baru pasien yang sama. Cetak dan hapus banyak baris masih tertunda. |
| Obstetri dan ginekologi | `DlgRawatInap.java:2558`, `:2907`, `:10533`, `:10652` | Kedua tab pemeriksaan belum tersedia dari kunjungan ranap. Tombol formulir terkait dicatat berdasarkan handler. Kesetaraan tile terpisah belum diverifikasi. |
| Resep, obat dan BHP, lab, radiologi, resume, formulir | Handler di `DlgRawatInap.java` meneruskan konteks kunjungan ke dialog klinis, misalnya `:6624`, `:6661`, `:6730`, `:6747`, `:6853`. | Menu pasien menampilkan beberapa label, tetapi cabang ini belum tersambung ke dialog klinis dari baris kamar. Riwayat pasien dapat dibuka lewat `PatientHistoryWindow`. |

## Perbaikan integritas data

1. **Pasien tujuan bisa berubah saat formulir terbuka.** Panel Pulang, Pindah, dan Ganti kini menyimpan pilihan baris saat dibuka, termasuk kunci segmen kamar. Memilih pasien lain di tabel atau memuat ulang daftar tidak mengganti target formulir. Klik Simpan kedua diabaikan selama operasi pertama berlangsung.
2. **Jenis bayar bisa tersimpan walau diagnosis gagal.** `RawatInapRepository.GantiDataKamarAsync` mengunci kunjungan dan segmen, memeriksa perubahan bersamaan pada field yang diedit, lalu menyimpan keduanya dalam satu transaksi. Field yang tidak diedit dipertahankan. Guard billing tetap berlaku untuk perubahan jenis bayar. Pilihan penjamin membawa kode dari objek penjamin, sehingga nama tampilan yang sama tidak menentukan kode.
3. **Kamar booking muncul pada pilihan pindah meski operasi menolaknya.** Pemilih kamar tujuan hanya memuat kamar kosong dan tetap menampilkan tarif sesuai penjamin. Formulir masuk tetap dapat menawarkan kamar yang dipesan untuk kunjungan tersebut.

`BillingRanapTests.MasukPindahPulangPelunasan_MenjagaTarifDanJurnal` mencakup penyimpanan diagnosis dan rollback ketika jenis bayar tidak memenuhi foreign key. Verifikasi ke salinan MariaDB belum dilakukan pada sesi ini.

## Celah berikutnya yang berisiko terhadap data

- Jalankan ulang fixture C# SOAPIE dan transaksi kamar di MariaDB uji serta catat cakupannya. Catatan awal terkendala koneksi; [sesi 24 November](../handover/07-SESSION-2026-11-24.md) kemudian mencatat tes dengan fixture Rawat Inap aktif. Ini belum membuktikan seluruh alur UI atau penguncian lintas koneksi.
- Uji penguncian tindakan dan billing dengan dua koneksi MariaDB sebelum membuka perubahan tindakan. Verifikasi juga jurnal pada simpan, ubah, dan hapus.
- Koreksi data kamar salah dan penggabungan kunjungan memengaruhi okupansi serta status registrasi. Jalur pulang perlu mencocokkan tindak lanjut menurut status, termasuk meninggal dan rujuk.

## Tarif, billing, dan jurnal tindakan

| Kategori | Sumber nilai saat tindakan dicatat | Sumber nilai saat billing ranap |
|---|---|---|
| Dokter | `jns_perawatan_inap.total_byrdr` disalin ke `rawat_inap_dr.biaya_rawat` | `rawat_inap_dr.biaya_rawat` |
| Petugas | `jns_perawatan_inap.total_byrpr` disalin ke `rawat_inap_pr.biaya_rawat` | `jns_perawatan_inap.total_byrpr` saat billing |
| Dokter dan petugas | `jns_perawatan_inap.total_byrdrpr` disalin ke `rawat_inap_drpr.biaya_rawat` | `rawat_inap_drpr.biaya_rawat` |

Bukti sumber: `DlgRawatInap.java:9719`, `:9730`, `:9741`, dan `keuangan/DlgBilingRanap.java:149`, `:159`, `:176`. Uji SQL mengubah ketiga tarif master setelah tindakan disimpan. Tarif billing petugas berubah dari 1.800 ke 1.801. Tarif billing dokter tetap 1.700 dan gabungan tetap 2.100. `KasirRepository.MuatRincianTindakanObatAsync` sudah memakai sumber nilai yang sama.

`fungsi/sekuel.java:1443` menghitung baris `billing` atau registrasi `Batal`. Handler Java memeriksa aturan itu saat simpan, ubah, dan hapus. Uji SQL mengonfirmasi predikat billing hanya mengenai nomor rawat yang memiliki billing. Urutan kunci lintas koneksi belum diuji.

Simpan tindakan dan jurnal satu transaksi di Java (`DlgRawatInap.java:11661`). Hapus membalik jurnal komponen lama (misalnya `:4524`). Ubah membalik nilai lama dan memposting nilai baru (misalnya `:12123`, `:12177`). Akun tindakan dibaca dari `set_akun_ranap`. `JurnalPoster` dapat digunakan untuk posting di transaksi yang sama. Pelunasan di Gramedika masih ditolak bila kunjungan punya tindakan. Jurnal dan perilaku pelunasan tindakan belum diverifikasi lewat aplikasi.


## Simpan PDF SOAPIE (24 November, terbaru)

`PemeriksaanRanapWindow` dapat menyimpan catatan SOAPIE terpilih sebagai PDF setelah memeriksa izin `tindakan_ranap` dan memuat ulang catatan. Hasil PDF belum diverifikasi visual atau disamakan dengan template Jasper Khanza. Tindakan rawat inap dengan jurnal, koreksi kamar dan alur pulang yang tersisa tetap terbuka.
