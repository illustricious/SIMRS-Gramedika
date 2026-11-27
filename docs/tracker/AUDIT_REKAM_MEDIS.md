# Audit formulir rekam medis (27 November 2026)

## Mesin formulir

Satu definisi per formulir di `Data/RekamMedis/FormKlinis.cs`; aturan di `FormKlinisService`, layar di `UI/Modules/RekamMedis/FormKlinisView.cs`, cetak di `FormKlinisReport.cs`.

| Formulir | Sumber Java | Pemilik | Batas ubah/hapus 2 x 24 jam | Waktu minimal registrasi |
|---|---|---|---|---|
| Catatan Keperawatan Rawat Jalan | `RMDataCatatanKeperawatanRalan.java` | petugas | ya | ya |
| Catatan Observasi IGD | `RMDataCatatanObservasiIGD.java` | petugas | ya | ya |
| Skrining Gizi Lanjut | `RMDataSkriningGiziLanjut.java` | petugas | ya | ya |
| Penilaian Lanjutan Risiko Jatuh Dewasa | `RMPenilaianLanjutanRisikoJatuhDewasa.java` | petugas | tidak | tidak |
| Penilaian Awal Medis Rawat Jalan | `RMPenilaianAwalMedisRalanDewasa.java` | dokter | ya | ya |
| Check List Kriteria Masuk dan Keluar HCU, ICU, NICU, PICU, Isolasi (10 formulir) | `RMChecklistKriteria*.java` | pegawai | ya | ya |

Definisi sepuluh check list ada di `Data/RekamMedis/FormKriteriaRuang.cs`; label, urutan, pilihan dan isian awal disalin dari layar Java, termasuk salah ketiknya (misalnya "SBP > 08 mmHg" pada Keluar ICU). Isian awal check list Keluar HCU, Keluar ICU dan Keluar Isolasi adalah "Ya", sama seperti Java; konfirmasi ke pengguna klinis belum dilakukan. Keluar Isolasi tidak menawarkan "Tidak Berlaku" walau kolomnya menerima.

Aturan bersama:

- Hak menu formulir diperlukan untuk melihat, menyimpan, mengubah, menghapus dan mencetak.
- Selain Admin Utama, pemilik catatan adalah akun yang login dan harus terdaftar sebagai petugas atau dokter. Hanya pemilik yang boleh mengubah atau menghapus. Batas waktu dan waktu registrasi tidak berlaku bagi Admin Utama.
- Ubah dan hapus mengunci baris dan ditolak bila catatan berubah sejak dimuat.
- Skor gizi dan skala Morse dihitung ulang di layanan saat disimpan.

Dibuka dari: tile Menu M (tombol **Isi / Ubah Formulir** pada daftar), klik kanan pasien → Data Rekam Medis → Formulir Rekam Medis, dan menu klinis Rawat Jalan.

## Perbedaan dari Khanza

- Cetakan memakai satu susunan umum, bukan templat Jasper tiap formulir. QR bukan tanda tangan elektronik tersertifikasi.
- Pengaturan profil **Izinkan tanggal mundur** (`TANGGALMUNDUR`) berlaku pada empat formulir yang di Java juga menguncinya; Penilaian Lanjutan Risiko Jatuh Dewasa tidak ikut. Lihat [arsitektur](../handover/02-ARCHITECTURE.md#tanggal-mundur).
- Layar formulir menampilkan satu kunjungan; pencarian dan filter tanggal lintas pasien tetap di daftar Menu M.
- Isian baru memakai pilihan pertama seperti Khanza, termasuk "Normal" pada pemeriksaan fisik.
- Uraian catatan keperawatan dibatasi 1000 karakter sesuai kolom; layar Java mengizinkan 2000.

## Pengujian

`FormKlinisTests`: definisi dicocokkan dengan DDL (kolom, enum, panjang, kunci), skor, lalu simpan/ubah/hapus/cetak kelima formulir pada MariaDB sintetis (batasan relasi dilepas) termasuk hak akses, pemilik, batas waktu, duplikat dan dua perubahan bersamaan. Layar diisi dan disimpan lewat uji headless sementara. Belum diuji: klik di aplikasi asli, database RS atau salinannya, dan bersamaan dengan klien Java.

## Menambah formulir

Pencocokan struktur pada 27 November: 171 formulir `rekammedis` tampak hanya menulis satu tabel; selain 5 formulir pertama, **115** punya bentuk kunci, satu pemilik dan tipe kolom yang sudah didukung. Angka itu dari pencocokan pola dan dihitung sebelum pemilik `pegawai` didukung, jadi sepuluh check list kriteria tidak mengurangi 115. Label, isian wajib, pilihan, rumus skor dan aturan tiap formulir tetap harus dibaca dari Java sebelum ditambahkan. Yang belum didukung: pemilik ganda, kunci bentuk lain, kolom tanggal atau desimal, dan formulir yang menulis tabel tambahan.

Langkah: tambah satu `FormKlinis` pada katalog, tambahkan tabelnya ke fixture, jalankan `FormKlinisTests`, lalu perbarui `status.json`.
