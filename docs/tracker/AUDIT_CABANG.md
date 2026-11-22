# Audit cabang pelayanan dan tindakan

[Peta hubungan seluruh source utama](CABANG_SOURCE.tsv) dapat difilter berdasarkan file, metode, jenis hubungan, kontrol, dan tujuan. Untuk menelusuri satu fitur, mulai dari file dan kontrol di fitur.tsv, ikuti handler dan panggilan lokal, lalu periksa pembukaan dialog atau operasi tabel beserta nomor barisnya. Nama metode dapat dipakai beberapa overload. Pemanggilan konstruktor menandai kandidat layar tujuan; konfirmasikan pemanggilan setVisible dan syarat akses pada handler.

## Tindakan dokter dan petugas

| Alur | Bukti di Java (folder src/simrskhanza) | Hasil pemeriksaan Avalonia |
|---|---|---|
| Ralan → Penanganan Petugas → pilih jasa → catat pelaksana → simpan | DlgRawatJalan.java:1937 dan 1956 memisahkan pilihan tarif dari tindakan dilakukan. Baris 13062 menyimpan rawat_jl_pr dengan status pembayaran. | Sebagian. TindakanRalanView menampilkan jasa, NIP/nama petugas, tanggal, jam, tarif dan biaya dari GetAsync. Input tindakan baru dari layar itu masih kurang. |
| Ralan → Dokter atau Dokter & Petugas | DlgRawatJalan.java:1867 dan 2086. Aksi gabungan menyimpan rawat_jl_drpr pada 13160. | Sebagian. Repository membedakan rawat_jl_dr, rawat_jl_pr dan rawat_jl_drpr. Penentuan dua pelaksana dan pencatatan tarif pada transaksi gabungan harus diperiksa terpisah. |
| Ranap → Penanganan Petugas → simpan / ubah | DlgRawatInap.java:1627,11764,12217; tabel rawat_inap_pr. Dokter dan tim gabungan mempunyai tab sendiri di 1507 dan 1780. | Data ranap dibaca pada RanapMenuRepository dan KasirRepository. Jalur entri di bangsal, koreksi transaksi, hak petugas dan dampak tagihan perlu verifikasi per aksi. |

## Pemeriksaan dan SOAPIE

| Alur | Bukti Java | Hasil pemeriksaan Avalonia |
|---|---|---|
| Rawat jalan → isi pemeriksaan → simpan enam komponen | DlgRawatJalan.java:2512,14740. Copy dan Paste menunjukkan keluhan, pemeriksaan, penilaian, tindak lanjut, instruksi dan evaluasi di 10343–10354. | Sebagian. RawatJalanView mengisi enam komponen dan memanggil SaveExaminationAsync. Kekurangan terkonfirmasi: NIP pemeriksa masih memakai konstanta D0000099 di RawatJalanView.axaml.cs, bukan identitas petugas sesi. |
| Rawat jalan → koreksi catatan sebelumnya | DlgRawatJalan.java:6310,6361 mengubah pemeriksaan_ralan. | Jalur koreksi catatan lama perlu verifikasi tersendiri dari simpan pemeriksaan baru. |
| Rawat inap → pemeriksaan → simpan / koreksi | DlgRawatInap.java:2180,11949,5408; pemeriksaan_ranap. | MenuMSpecs menampilkan riwayat SOAP ranap baca-saja. Form bangsal untuk isi, simpan dan koreksi masih perlu dicocokkan. |
| Copy dan Paste SOAPIE pada kedua layanan | DlgRawatJalan.java:10343,10347 dan DlgRawatInap.java:8886,8890. Copy mengambil enam bidang dan Paste mengisi kembali editor. | Empat jalur popup kurang. Pemetaan ini khusus perintah salin/tempel seluruh catatan pada form, terpisah dari clipboard teks biasa. |
| Cari lima SOAP terakhir | DlgRawatJalan.java:8236 dan DlgRawatInap.java:7304 membuka RMCari5SOAPTerakhir. | Periksa pemanggil, pemilihan hasil dan pengisian kembali editor pada masing-masing layanan. |
| Obstetri, ginekologi, diagnosis dan catatan dokter | DlgRawatJalan.java:2889,3237,3241,3356. Simpan pada 14792,14823,14855. Ranap mempunyai tab obstetri dan ginekologi pada 2558,2907. | Audit tiap form, tabel tujuan, validasi dan hak akses terpisah dari SOAPIE umum. |

## Memperbarui audit

Dari root repo, jalankan `node tools/audit-cabang.mjs ../simrs-khanza/src` saat source Java berubah. Tracker push memakai status.json dan fitur.tsv. Peta hubungan adalah indeks navigasi tambahan. Jumlah hubungan bukan jumlah fitur selesai. Audit ini berbasis source, tanpa transaksi pasien atau pengujian DB.

Pemindaian membaca Java utama di luar folder bridging. Ia mengenali tab berlabel literal, sejumlah listener Swing, kandidat konstruktor layar, panggilan metode lokal serta operasi tulis SQL/helper dengan nama tabel literal. Nomor baris dan konteks metode merupakan petunjuk untuk pemeriksaan manual. Query rakitan, refleksi, nama tabel dinamis, transaksi lintas kelas, serta subproyek seperti anjungan masih membutuhkan penelusuran terpisah.
