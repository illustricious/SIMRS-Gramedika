# Status menu A

Pencocokan 10 November 2026 terhadap `LegacyMenuCatalog` dan routing `MainWindow`. Terhubung berarti layar dapat dibuka, bukan berarti seluruh perilaku Khanza telah selesai atau diverifikasi pada database.

| Menu | Status saat ini |
|---|---|
| Informasi Kamar | Terhubung; daftar dan pencarian. |
| Jadwal Praktek | Terhubung; daftar, pencarian, dan tambah. Hapus dan cetak belum dipindahkan. |
| Registrasi | Terhubung; pemilihan dan perubahan sebagian kolom. Perlu pemeriksaan terhadap aturan lengkap `DlgReg`. |
| Booking Periksa | Terhubung; hanya daftar dan pencarian. |
| Booking Registrasi | Terhubung; hanya daftar dan pencarian. |
| Booking MCU Perusahaan | Terhubung; hanya daftar dan pencarian. |
| IGD/UGD | Terhubung; perlu pencocokan semua tindakan dengan `DlgIGD`. |
| Tindakan Ralan | Terhubung; perlu pencocokan semua tindakan. |
| Permintaan Rawat Inap | Belum terhubung. |
| Rawat Inap | Terhubung; perlu pencocokan seluruh tindakan. |
| Jadwal Operasi | Belum terhubung. |
| Permintaan Lab PK, PA, MB | Terhubung; masing-masing perlu pencocokan semua tindakan. |
| Permintaan Radiologi | Terhubung; perlu pencocokan semua tindakan. |
| Permintaan Layanan Program KFR | Belum terhubung. |
| DPJP Ranap | Belum terhubung; sumber `DlgDpjp`, tabel `dpjp_ranap`, pemilihan beberapa dokter, simpan, hapus dan cetak. |
| Tindakan Ranap | Belum terhubung. |
| Operasi/VK | Belum terhubung. |
| Rujukan Keluar dan Masuk | Belum terhubung. |
| Beri Obat/BHP | Belum terhubung. |
| Resep Pulang | Belum terhubung. |
| No. Resep | Terhubung; sebagian fungsi, termasuk daftar dan membuat nomor. Racikan, cetak dan hapus belum lengkap. |
| Diet Pasien | Belum terhubung. |
| Skrining Rawat Jalan | Belum terhubung. |
| Deteksi Dini Corona | Belum terhubung. |
| Periksa Lab PK, PA, MB | Terhubung; masing-masing perlu pencocokan semua tindakan. |
| Periksa Radiologi | Terhubung; perlu pencocokan semua tindakan. |
| Rawat Jalan (Kasir) | Terhubung; cakupan billing perlu dicocokkan dengan Khanza. |
| Deposit Pasien | Belum terhubung. |
| Perkiraan Biaya Ranap | Belum terhubung. |

Prioritas implementasi per layar: baca source Java dan skema, petakan aturan dan hak akses, buat alur lengkap dan penanganan kesalahan, lalu verifikasi dengan database uji. Jangan menganggap tampilan daftar saja sebagai implementasi lengkap.
