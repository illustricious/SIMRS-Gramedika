# Status Menu Q

Semua 16 tile Menu Q membuka layar. Cakupan layar bukan jaminan kesetaraan fungsi dengan Khanza.

| Tile | Layar Avalonia |
|---|---|
| Ruang, Kategori, Jenis Koleksi, Pengarang | Edit referensi (kode dan nama). Tidak dihapus dari layar ini |
| Penerbit Koleksi | Edit. Alamat, telepon, email, website |
| Koleksi Perpustakaan | Edit judul, pengarang, penerbit, tahun, ISBN, kategori, jenis. Kolom Eksemplar menghitung inventaris per buku |
| Inventaris Perpustakaan | Edit eksemplar: buku, asal, tanggal dan harga pengadaan, status, ruang, rak, box. Status Dipinjam diatur peminjaman di Khanza, jangan diubah manual |
| Pengaturan Peminjaman | Satu baris pengaturan: maksimal pinjam, lama pinjam, denda per hari. Nilai minimal 1 untuk batas dan lama |
| Denda Perpustakaan | Edit jenis dan besar denda |
| Anggota Perpustakaan | Edit. Kolom Sedang Dipinjam menghitung peminjaman yang belum kembali. Tidak dihapus |
| Data Koleksi Ebook | Edit. Berkas diisi sebagai lokasi atau URL, tidak ada unggah berkas |
| Koleksi Penelitian | Edit riwayat penelitian pegawai (tabel `riwayat_penelitian`). Berkas diisi sebagai lokasi |
| Peminjaman Koleksi | Baca. Filter tanggal pinjam. Kolom Hari Terlambat untuk buku yang masih dipinjam melewati batas kembali |
| Bayar Denda | Baca. Gabungan denda manual dan denda keterlambatan harian |
| Cari Koleksi Ebook | Baca. Pencarian judul, pengarang, penerbit, kategori, jenis |
| Cari Inventaris | Baca. Pencarian judul, pengarang, status. Ketik "Ada" untuk eksemplar tersedia |

Peminjaman, pengembalian, dan pembayaran denda dilakukan di Khanza karena mengubah status eksemplar dan jurnal.
