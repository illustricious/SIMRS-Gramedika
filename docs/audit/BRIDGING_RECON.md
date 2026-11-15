# Recon bridging instalasi Khanza

Pemeriksaan 10 November 2026. Tidak ada layanan yang dijalankan atau pengiriman yang dilakukan.

## Shortcut dan program terpasang

Ketiga shortcut di `Shortcut bridging` menunjuk ke folder `C:\Users\PC\Documents\SIMRS Khanza Update 2026`:

| Shortcut | Peluncur | JAR |
|---|---|---|
| Service Antrol Mobile JKN (2) | AplikasiServiceMobileJKN.bat | KhanzaHMSServiceMobileJKN.jar |
| Service Aplicare | AplikasiServiceAplicare.bat | KhanzaHMSServiceAplicare.jar |
| Service Mobile JKNERM | AplikasiServiceMobileJKNERM.bat | KhanzaHMSServiceMobileJKNERM.jar |

Ketiga JAR ada di instalasi. JAR SATUSEHAT juga ada, tetapi tidak ada dalam tiga shortcut yang diberikan. Nama kelas utama dalam JAR Antrol dan JKNERM sama, sehingga keduanya harus diperiksa sebagai artefak terpisah. Kesesuaian bytecode JAR dengan source clone belum dibuktikan.

## Source clone

- Aplicare: `KhanzaHMSServiceAplicare/src/khanzahmsserviceaplicare/frmUtama.java`. Membaca mapping `aplicare_ketersediaan_kamar`, menghitung kondisi kamar dan memanggil `/rest/bed/update`. Trigger di source setiap jam pada menit 01.
- Antrol: `KhanzaHMSServiceMobileJKN/src/khanzahmsservicemobilejkn/frmUtama.java`. Trigger di source setiap 10 menit; mengakses `referensi_mobilejkn_bpjs`, `referensi_mobilejkn_bpjs_taskid`, dan endpoint antrean, pembatalan, waktu, serta farmasi.
- JKNERM: `KhanzaHMSServiceMobileJKNERM/src/khanzahmsservicemobilejkn/frmUtama.java`. Trigger di source setiap 5 menit; juga menggunakan antrean dan task ID, termasuk waktu dari `pemeriksaan_ralan`. Kedua service Mobile JKN memiliki overlap: jangan hidupkan pengganti secara paralel tanpa memetakan kondisi setiap cabang dan mekanisme klaim.
- SATUSEHAT: `KhanzaHMSServiceSatuSehat` pada clone memiliki proses Encounter dan CarePlan; keberadaan JAR tidak membuktikan service berjalan.

## Konfigurasi yang perlu disediakan

Koneksi database, endpoint Aplicare dan Mobile JKN, cons ID, secret key, user key, dan kode fasilitas. Konfigurasi Khanza menyimpan beberapa nilai terenkripsi; nilai yang dicopy dari XML belum tentu plaintext yang dapat langsung dipakai. Untuk SATUSEHAT tambahkan endpoint, client ID, client secret dan organization ID. Jangan menyimpan nilai aktual di repository atau log.

## Pekerjaan sebelum pengganti aktif

1. Cocokkan bytecode JAR terpasang terhadap source, termasuk kondisi pemilihan antrean, task ID, pembatalan dan farmasi.
2. Bangun penyimpanan konfigurasi terenkripsi dan form pengaturan; buat monitoring tanpa memulai pengiriman otomatis.
3. Buat worker dan klaim pekerjaan yang tahan restart, audit status, retry dan rekonsiliasi hasil yang tidak pasti; hindari POST ulang tanpa pemeriksaan.
4. Uji dengan database dan endpoint uji, termasuk koneksi putus setelah server menerima request.
5. Alihkan satu modul pada satu waktu dan matikan pengirim lama untuk modul tersebut; validasi hasil dengan operator sebelum produksi.
