# Audit formulir rekam medis (28 November 2026)

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

Klik kanan pasien mengelompokkan formulir: Pengkajian & Catatan, Observasi & Pemantauan, Kriteria Ruang Rawat, Skrining, serta Risiko Jatuh, Dekubitus & Fungsional.

Dibuka dari: tile Menu M (tombol **Isi / Ubah Formulir** pada daftar), klik kanan pasien → Data Rekam Medis → Formulir Rekam Medis, dan menu klinis Rawat Jalan.

## Lembar observasi dan pemantauan (28 November 2026)

Definisi di `Data/RekamMedis/FormObservasi.cs`. Semuanya satu baris per waktu, batas ubah/hapus 2 x 24 jam, waktu minimal registrasi, dan ikut kunci tanggal mundur. Pemilik petugas, kecuali Monitoring TOLAC (pegawai, kolom `nip`).

| Formulir | Sumber Java |
|---|---|
| Observasi Rawat Inap, Kebidanan, Post Partum/Post SC, Rawat Bayi, CHBP | `RMDataCatatanObservasiRanap*.java`, `...Bayi`, `...CHBP` |
| Observasi Induksi Persalinan, Ruang Operasi, Restrain Nonfarmakologi, Ventilator | `RMDataCatatanObservasi*.java` |
| Observasi Hemodialisa, Cairan Hemodialisa | `RMDataCatatanObservasiHemodialisa.java`, `RMDataCatatanCairanHemodialisa.java` |
| Keseimbangan Cairan, Cek GDS | `RMDataCatatanKeseimbanganCairan.java`, `RMDataCatatanCekGDS.java` |
| Follow Up DBD, Reaksi Tranfusi, Selama TOLAC | `RMDataFollowUpDBD.java`, `RMDataMonitoringReaksiTranfusi.java`, `RMDataMonitoringTOLAC.java` |
| Intervensi Nyeri Farmakologi dan Non Farmakologi | `RMDataIntervensiNyeri*.java` |
| Monitoring & Evaluasi Asuhan Gizi | `RMDataMonitoringAsuhanGizi.java` |

Perbedaan khusus kelompok ini:

- Keseimbangan cairan selalu dihitung (input dikurangi output) dan tidak bisa diketik. Kolom `keseimbangan` hanya 4 karakter, jadi hasil seperti `-1250` atau `262.5` ditolak dengan pesan; di layar Java hasil yang terlalu panjang tidak masuk ke kotaknya.
- Cetak satu catatan per halaman; Khanza mencetak tabel banyak catatan. Formulir kosong Khanza (cetak untuk diisi tangan) dan grafik tidak ada.
- Dari menu klinis Rawat Jalan hanya Monitoring Gizi yang terhubung; lembar lain dibuka dari Menu M atau klik kanan pasien. Belum ada tombol dari layar Rawat Inap.

## Skrining dan penilaian risiko (28 November 2026)

Empat puluh formulir di `Data/RekamMedis/FormSkrining.cs`: 7 penilaian risiko dan fungsional, serta 33 skrining. Klik kanan pasien mengelompokkannya ke **Skrining** dan **Risiko Jatuh, Dekubitus & Fungsional**.

Aturan waktu mengikuti handler tiap jendela:

- Satu per kunjungan: skrining dengan kunci `no_rawat`. Hampir semuanya memakai batas 2 x 24 jam, waktu minimal registrasi dan kunci tanggal mundur; skrining nutrisi anak, dewasa dan lansia tidak memakai ketiganya.
- Banyak catatan per kunjungan (penilaian lanjutan, dekubitus, neonatus, gizi kehamilan, MPP): sebagian besar tanpa batas waktu. Skrining MPP satu catatan per tanggal, tanpa jam.

Pilihan, isian awal, nilai tiap pilihan, ambang kesimpulan dan batas panjang diambil dari layar Java. Pada 33 formulir yang punya hitungan, 600 isian acak per formulir dibandingkan dengan method Java yang diterjemahkan ke JavaScript (170.913 nilai, tanpa selisih di luar perbedaan di bawah). Alat pembandingnya sementara dan tidak disimpan. Label disalin dari layar Java; yang terpotong dua baris atau tanpa konteks ditulis ulang.

Isian "usulan" (misalnya kesimpulan CURB-65, SRQ, ACRS, AMT, ESAT, TBC, TOLAC, hasil gigi dan mulut, tindak lanjut thalassemia) terisi otomatis tetapi bisa diganti; saran baru hanya diterapkan bila isian sumbernya berubah.

| Formulir | Sumber Java |
|---|---|
| Penilaian Lanjutan Risiko Jatuh Anak | `RMPenilaianLanjutanRisikoJatuhAnak.java` |
| Penilaian Lanjutan Risiko Jatuh Geriatri | `RMPenilaianLanjutanRisikoJatuhGeriatri.java` |
| Penilaian Lanjutan Risiko Jatuh Lansia | `RMPenilaianLanjutanRisikoJatuhLansia.java` |
| Penilaian Lanjutan Risiko Jatuh Psikiatri | `RMPenilaianLanjutanRisikoJatuhPsikiatri.java` |
| Penilaian Lanjutan Skrining Fungsional | `RMPenilaianLanjutanSkriningFungsional.java` |
| Penilaian Risiko Dekubitus | `RMPenilaianRisikoDekubitus.java` |
| Penilaian Lanjutan Risiko Jatuh Neonatus | `RMPenilaianRisikoJatuhNeonatus.java` |
| Skrining Gizi Kehamilan/Obstetri/Nifas | `RMDataSkriningGiziKehamilan.java` |
| Skrining Adiksi Nikotin (Fagerstrom) | `RMSkriningAdiksiNikotin.java` |
| Skrining Anemia | `RMSkriningAnemia.java` |
| Skrining CURB-65 | `RMSkriningCURB65.java` |
| Skrining Diabetes Melitus | `RMSkriningDiabetesMelitus.java` |
| Skrining Frailty Syndrome | `RMSkriningFrailtySyndrome.java` |
| Skrining Hipertensi | `RMSkriningHipertensi.java` |
| Skrining Indra Pendengaran | `RMSkriningIndraPendengaran.java` |
| Skrining Instrumen ACRS | `RMSkriningInstrumenACRS.java` |
| Skrining Instrumen AMT | `RMSkriningInstrumenAMT.java` |
| Skrining Instrumen ESAT | `RMSkriningInstrumenESAT.java` |
| Skrining Instrumen SDQ (Usia 11-18 Tahun) | `RMSkriningInstrumenSDQ.java` |
| Skrining Kanker Kolorektal | `RMSkriningKankerKolorektal.java` |
| Skrining Kekerasan Pada Perempuan | `RMSkriningKekerasanPadaPerempuan.java` |
| Skrining Kesehatan Gigi & Mulut Balita | `RMSkriningKesehatanGigiMulutBalita.java` |
| Skrining Kesehatan Gigi & Mulut Dewasa | `RMSkriningKesehatanGigiMulutDewasa.java` |
| Skrining Kesehatan Gigi & Mulut Lansia | `RMSkriningKesehatanGigiMulutLansia.java` |
| Skrining Kesehatan Gigi & Mulut Anak Sekolah & Remaja | `RMSkriningKesehatanGigiMulutRemaja.java` |
| Skrining Kesehatan Penglihatan | `RMSkriningKesehatanPenglihatan.java` |
| Skrining Manager Pelayanan Pasien | `RMSkriningMPP.java` |
| Skrining Nutrisi Pasien Anak | `RMSkriningNutrisiAnak.java` |
| Skrining Nutrisi Pasien Dewasa | `RMSkriningNutrisiDewasa.java` |
| Skrining Nutrisi Pasien Lansia | `RMSkriningNutrisiLansia.java` |
| Skrining Obesitas | `RMSkriningObesitas.java` |
| Skrining PUMA | `RMSkriningPUMA.java` |
| Skrining Pneumonia Severity Index | `RMSkriningPneumoniaSeverityIndex.java` |
| Skrining Risiko Kanker Paru | `RMSkriningRisikoKankerParu.java` |
| Skrining Risiko Kanker Payudara | `RMSkriningRisikoKankerPayudara.java` |
| Skrining Risiko Kanker Serviks | `RMSkriningRisikoKankerServiks.java` |
| Skrining Instrumen SRQ | `RMSkriningSRQ.java` |
| Skrining TBC | `RMSkriningTBC.java` |
| Skrining TOLAC (Poli Kebidanan) | `RMSkriningTOLAC.java` |
| Skrining Thalassemia | `RMSkriningTalasemia.java` |

Perbedaan dari Khanza pada kelompok ini:

- **SDQ.** Layar Java membandingkan ambang "Ambang/Boderline" tiap subskala dan skor kesulitan dengan total 25 butir, dan satu cabang masalah perilaku menulis ke kotak emosional. Di sini tiap kesimpulan memakai nilai subskalanya sendiri, jadi hasilnya bisa berbeda dari Khanza.
- **Risiko jatuh anak** total 12 ke atas: layar Java tidak menampilkan teks tingkat risiko; di sini tampil "Risiko Tinggi (>= 12)". Teks ini tidak disimpan.
- **Thalassemia.** Java mengikuti hasil lab yang terakhir diubah; di sini saran rujuk muncul bila salah satu hasil tidak normal.
- Nilai yang di Java bisa diketik (kotak skor, pilihan nilai nutrisi dewasa) di sini selalu dihitung.
- Skrining yang menghitung IMT, hipertensi dan anemia menolak simpan bila angka yang dibutuhkan kosong atau tidak masuk akal; Khanza menyimpannya.
- Mengikuti Khanza apa adanya, perlu dikonfirmasi pengguna klinis: pada TBC dengan kontak erat, satu gejala dijawab Tidak sudah menghasilkan "Kontak Erat"; ejaan "Risikio Berat" (nutrisi anak) dan "?70" (interpretasi PSI) mengikuti enum tabel; Java menulis "11,9 - 11 g/dl'" dengan tanda petik, di sini nilai enumnya.

Belum ditambahkan:

- Skrining Perilaku Merokok Usia Sekolah: perlu pemilih master sekolah.
- Skrining Mental Emosional: layar Java menulis ke tabel `skrining_instrumen_acrs`, sama dengan ACRS.
- MPP Form B: punya kolom tanggal tambahan.

## Perbedaan dari Khanza

- Cetakan memakai satu susunan umum, bukan templat Jasper tiap formulir. QR bukan tanda tangan elektronik tersertifikasi.
- Pengaturan profil **Izinkan tanggal mundur** (`TANGGALMUNDUR`) berlaku pada empat formulir yang di Java juga menguncinya; Penilaian Lanjutan Risiko Jatuh Dewasa tidak ikut. Lihat [arsitektur](../handover/02-ARCHITECTURE.md#tanggal-mundur).
- Layar formulir menampilkan satu kunjungan; pencarian dan filter tanggal lintas pasien tetap di daftar Menu M.
- Isian baru memakai pilihan pertama seperti Khanza, termasuk "Normal" pada pemeriksaan fisik.
- Uraian catatan keperawatan dibatasi 1000 karakter sesuai kolom; layar Java mengizinkan 2000.

## Pengujian

`FormKlinisTests`: definisi dicocokkan dengan DDL (kolom, enum, panjang, kunci), skor, hitungan cairan, keterhubungan tile, lalu simpan/ubah/hapus/cetak semua formulir pada MariaDB sintetis (batasan relasi dilepas) termasuk hak akses, pemilik, batas waktu, duplikat dan dua perubahan bersamaan. Layar diisi dan disimpan lewat uji headless sementara. Belum diuji: klik di aplikasi asli, database RS atau salinannya, dan bersamaan dengan klien Java.

## Menambah formulir

Pencocokan struktur pada 27 November: 171 formulir `rekammedis` tampak hanya menulis satu tabel; selain 5 formulir pertama, **115** punya bentuk kunci, satu pemilik dan tipe kolom yang sudah didukung. Angka itu dari pencocokan pola dan dihitung sebelum pemilik `pegawai` didukung, jadi sepuluh check list kriteria tidak mengurangi 115. Label, isian wajib, pilihan, rumus skor dan aturan tiap formulir tetap harus dibaca dari Java sebelum ditambahkan. Yang belum didukung: pemilik ganda, kunci bentuk lain, kolom tanggal atau desimal, dan formulir yang menulis tabel tambahan.

Pada 28 November, survei ulang sebelum lembar observasi mencatat 139 jendela satu tabel yang belum ditulis, 117 di antaranya cocok menurut pola tabel; 19 sudah ditambahkan. Kelompok sisa terbesar waktu itu: skrining dan penilaian risiko, lalu penilaian awal medis per spesialis. Sesudah 40 formulir skrining dan risiko, `tools/survei-formulir.py` mencatat 79 jendela satu tabel yang belum ditulis, 57 cocok pola.

Langkah: tambah satu `FormKlinis` pada katalog, tambahkan tabelnya ke fixture, jalankan `FormKlinisTests`, lalu perbarui `status.json`.
