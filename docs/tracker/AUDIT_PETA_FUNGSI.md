# Audit peta fungsi (27 November 2026)

Pemetaan seluruh jendela dan tile Khanza terhadap Gramedika. Angka ada di [PETA_FUNGSI.md](PETA_FUNGSI.md) (dibuat otomatis),
rincian di [peta-fungsi.tsv](peta-fungsi.tsv) per jendela Java dan [peta-tile.tsv](peta-tile.tsv) per tile.

Angka temuan diperbarui sesudah [fitur rawat inap](../handover/12-SESSION-2026-11-27.md#fitur-rawat-inap), rawat gabung, operasi, resep racikan dokter, piutang pasien, lembar observasi serta skrining dan penilaian risiko; hasil cek sampel berlaku untuk peta sebelum perubahan itu.

## Cara kerja

`node tools/peta-fungsi.mjs --java ../simrs-khanza/src` (Node 22+, .NET SDK 10):

1. **Java.** Untuk tiap kelas `JDialog`/`JFrame`: tabel yang ditambah, diubah, dihapus (panggilan `Sequel.menyimpan/mengedit/meghapus`
   dan SQL literal), tabel yang dibaca, tanda jurnal (`jur.simpanJurnal`, `tampjurnal`) dan riwayat stok (`catatRiwayat`).
2. **Gramedika.** SQL literal pada kode yang dirujuk antarmuka; layar generik dan formulir dibaca dari objek aplikasinya
   (`PetaFungsiDumpTests`); penulisan lewat EF Core atau nama tabel rakitan didaftar tangan di skrip. Skrip berhenti bila ada
   berkas baru yang merakit nama tabel tetapi belum didaftar.
3. **Cocokkan** per pasangan tabel dan aksi, lalu klasifikasikan jendela dan tile.

Bridging (241 jendela, termasuk 237 di folder `bridging`) dipisah. Tabel sementara, `trackersql`, `jurnal`, `detailjurnal` dan `riwayat_barang_medis` tidak dihitung sebagai fungsi.

## Arti status jendela

| Status | Arti | Tingkat percaya |
|---|---|---|
| tulis ada | Semua aksi tulisnya juga dilakukan Gramedika | Batas atas |
| tulis sebagian | Sebagian aksi tulisnya ada | Batas atas; bagian "kurang" cukup dapat dipercaya |
| hanya baca | Tabelnya dibaca, tidak ada aksi tulis yang setara | Cukup dapat dipercaya |
| belum ada | Tabelnya tidak disentuh | Cukup dapat dipercaya |
| baca ada / sebagian | Jendela tanpa tulis; tabelnya dibaca di suatu tempat | Lemah: tidak berarti laporan atau grafiknya setara |

## Hasil cek sampel

Cek tangan membandingkan baris tulis Java dengan berkas penulis Gramedika. Alur penuh, validasi dan cetak tidak dibandingkan,
dan tidak ada yang dijalankan di database.

| Sampel | Diperiksa | Hasil |
|---|---:|---|
| Acak, "tulis ada" | 12 | 12 cocok pada tingkat tabel (sebagian besar layar master) |
| Acak, "tulis sebagian" | 8 | 6 cocok; 2 terlalu tinggi karena master barang dianggap bagian dari transaksi stok |
| Acak, "hanya baca" dan "belum ada" yang menulis data | 10 | 10 cocok: tidak ada perintah tulis ke tabel utamanya |
| Alur inti berstatus "tulis ada/sebagian" | 14 | 11 wajar; 3 terlalu tinggi |

Sesudah sampel pertama, aturan "transaksi stok yang hanya tersentuh editor master = hanya baca" ditambahkan. Sesudah sampel kedua
ditemukan penulisan antrean yang nama tabelnya dirakit dan sudah didaftarkan. Sampel tidak diulang penuh sesudah kedua perbaikan.

Tiga contoh yang masih terlalu tinggi di peta, karena dinilai per tabel:

- `DlgBilingParsialRalan` "sebagian": Gramedika tidak punya billing parsial sama sekali; tabelnya ditulis alur lain.
- `DlgIKBBayi` "sebagian": data bayi (`pasien_bayi`) tidak ditulis; yang tercakup hanya pendaftaran pasien biasa.
- `DlgPermintaanRadiologi` "tulis ada": Gramedika hanya membuat order rawat jalan; cabang rawat inap belum ada.

Perkiraan dari sampel kecil: sekitar 1 dari 5 jendela alur inti yang berstatus "ada" dinilai lebih tinggi dari kenyataan.
Perlakukan angka "tulis ada" sebagai langit-langit.

## Temuan utama

1. **Sepertiga lebih aksi tulis Java punya padanan.** 931 dari 2.202 pasangan tabel-aksi (42%), sebelum dikoreksi ke bawah.
2. **Sebagian besar yang "lengkap" adalah master.** 179 dari 317 jendela "tulis ada" tercakup layar master satu tabel.
3. **261 tile hanya menampilkan** data yang di Java bisa diisi; 125 di antaranya Menu M (rekam medis).
4. **Rekam medis adalah kelompok terbesar yang tersisa:** 87 formulir satu tabel isi dan 36 dengan tabel tambahan. "Satu tabel
   isi" belum berarti muat di mesin formulir; pemilik ganda, bentuk kunci dan tipe kolom harus dicek per formulir.
5. **Fungsi inti yang dikonfirmasi tidak ada penulisannya:** pengurangan dan tambahan
   biaya, data bayi lahir, pengadaan farmasi,
   retur beli, retur jual dan piutang obat.
6. Pembelian, pemesanan, pengeluaran dan retur Dapur, Non Medis dan Toko hanya tampil; master barangnya yang bisa diedit.
7. Tiga berkas Data tidak dipakai antarmuka: `DokterRepository`, `PenunjangRepository`, `RawatJalanRepository`.

## Yang tidak dicakup

Validasi, hak akses, penomoran, isi jurnal, cetakan Jasper, grafik, bridging (241 jendela; pemilik menyiapkannya di aplikasi terpisah yang tidak ikut dipetakan), layanan latar, aplikasi KhanzaHMS
selain `src`, perilaku saat dipakai bersamaan, dan kebenaran angka pada data rumah sakit. Frekuensi pemakaian juga tidak diketahui:
urutan "celah terpenting" memakai keterhubungan antar jendela. Daftar menu yang benar-benar dipakai rumah sakit akan jauh lebih
berguna untuk menentukan prioritas.

## Saran urutan kerja

1. Daftar menu yang dipakai sehari-hari dari rumah sakit, lalu saring [peta-fungsi.tsv](peta-fungsi.tsv) dengannya.
2. Uji alur inti pada salinan database RS (perlu persetujuan pemilik sistem).
3. Celah alur uang dan pasien: deposit dan penyesuaian di kasir.
4. Formulir rekam medis per kelompok lewat mesin formulir.
5. Transaksi stok non-farmasi.
